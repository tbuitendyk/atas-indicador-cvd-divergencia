namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Las alertas: las del prompt v3 (15) y, en la v4, la ruptura de una línea de sesión y las del Modelo 3.</summary>
public enum CategoriaAlerta
{
	Real,
	Premium,
	Ruptura,
	EntradaM1,
	EntradaM2,
	LineaSesion,
	EntradaM3,
	M3Activado
}

/// <summary>Canales de una alerta: ATAS (sonido y ventana), correo y Telegram.</summary>
public readonly record struct CanalesAlerta(bool Atas, bool Correo, bool Telegram);

/// <summary>Resultado del último envío por canal (para el panel, recuadro 15; sin secretos).</summary>
public sealed record ResultadoEnvio(string Canal, DateTime Utc, bool Ok, string Detalle);

/// <summary>
/// Cola de notificaciones (16): quien encola nunca espera (ATAS no se bloquea, prueba 29); un hilo envía con reintentos
/// limitados (3) y registra el resultado de cada canal. Los textos de error pasan por un filtro de secretos.
/// </summary>
public sealed class ColaEnvios : IDisposable
{
	private readonly BlockingCollection<(string Canal, string Clave, Func<CancellationToken, Task> Envio)> _cola = new(500);
	private readonly CancellationTokenSource _fin = new();
	private readonly ConcurrentDictionary<string, ResultadoEnvio> _ultimos = new();
	private readonly ConcurrentDictionary<string, byte> _claves = new();
	private readonly Func<string, string> _limpiar;

	public ColaEnvios(Func<string, string>? limpiarSecretos = null, int reintentos = 3, Func<int, TimeSpan>? espera = null)
	{
		_limpiar = limpiarSecretos ?? (s => s);
		Reintentos = Math.Max(1, reintentos);
		Espera = espera ?? (n => TimeSpan.FromSeconds(n * n));
		Task.Run(Bucle);
	}

	public int Reintentos { get; }

	public Func<int, TimeSpan> Espera { get; }

	public IReadOnlyCollection<ResultadoEnvio> Ultimos => _ultimos.Values.ToList();

	/// <summary>Encola sin esperar; una misma clave (alerta y canal) se envía una sola vez. false si ya estaba o la cola está llena.</summary>
	public bool Encolar(string canal, string clave, Func<CancellationToken, Task> envio) =>
		_claves.TryAdd(canal + "|" + clave, 0) && _cola.TryAdd((canal, clave, envio));

	private async Task Bucle()
	{
		foreach (var (canal, _, envio) in _cola.GetConsumingEnumerable(_fin.Token))
		{
			for (var intento = 1; intento <= Reintentos; intento++)
			{
				try
				{
					await envio(_fin.Token).ConfigureAwait(false);
					_ultimos[canal] = new ResultadoEnvio(canal, DateTime.UtcNow, true, intento == 1 ? "enviado" : $"enviado al intento {intento}");
					break;
				}
				catch (OperationCanceledException) when (_fin.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					_ultimos[canal] = new ResultadoEnvio(canal, DateTime.UtcNow, false, _limpiar($"intento {intento}/{Reintentos}: {ex.Message}"));

					if (intento < Reintentos)
						await Task.Delay(Espera(intento), _fin.Token).ConfigureAwait(false);
				}
			}
		}
	}

	public void Dispose()
	{
		_fin.Cancel();
		_cola.CompleteAdding();
	}
}

/// <summary>Cifrado de las credenciales (16): en Windows, DPAPI del usuario; en las pruebas, un protector falso.</summary>
public interface IProtector
{
	byte[] Proteger(byte[] datos);

	byte[] Desproteger(byte[] datos);
}

/// <summary>DPAPI (CryptProtectData / CryptUnprotectData de crypt32, ámbito del usuario de Windows). // VERIFICAR en ATAS real.</summary>
public sealed class ProtectorDpapi : IProtector
{
	[StructLayout(LayoutKind.Sequential)]
	private struct Blob
	{
		public int Tamano;
		public IntPtr Datos;
	}

	private const int SinInterfaz = 0x1;

	[DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CryptProtectData")]
	private static extern bool Proteger(ref Blob entrada, string? descripcion, IntPtr entropia, IntPtr reservado, IntPtr aviso, int banderas, ref Blob salida);

	[DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CryptUnprotectData")]
	private static extern bool Desproteger(ref Blob entrada, IntPtr descripcion, IntPtr entropia, IntPtr reservado, IntPtr aviso, int banderas, ref Blob salida);

	[DllImport("kernel32.dll")]
	private static extern IntPtr LocalFree(IntPtr memoria);

	public byte[] Proteger(byte[] datos) => Ejecutar(datos, true);

	public byte[] Desproteger(byte[] datos) => Ejecutar(datos, false);

	private static byte[] Ejecutar(byte[] datos, bool proteger)
	{
		var entrada = new Blob { Tamano = datos.Length, Datos = Marshal.AllocHGlobal(Math.Max(1, datos.Length)) };
		var salida = new Blob();

		try
		{
			Marshal.Copy(datos, 0, entrada.Datos, datos.Length);
			var ok = proteger
				? Proteger(ref entrada, "SessionsSweepsPro", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SinInterfaz, ref salida)
				: Desproteger(ref entrada, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SinInterfaz, ref salida);

			if (!ok)
				throw new InvalidOperationException("DPAPI no pudo " + (proteger ? "cifrar" : "descifrar") + " la credencial");

			var r = new byte[salida.Tamano];
			Marshal.Copy(salida.Datos, r, 0, salida.Tamano);
			return r;
		}
		finally
		{
			Marshal.FreeHGlobal(entrada.Datos);

			if (salida.Datos != IntPtr.Zero)
				LocalFree(salida.Datos);
		}
	}
}

/// <summary>
/// Credenciales cifradas (16): una por archivo en %APPDATA%\ATAS\OrderFlowSuite\SessionsSweeps\credenciales. Nunca en
/// texto plano, ni en los ajustes de ATAS, ni en logs ni CSV.
/// </summary>
public sealed class AlmacenCredenciales
{
	private readonly string _carpeta;
	private readonly IProtector _protector;

	public AlmacenCredenciales(string carpeta, IProtector protector) => (_carpeta, _protector) = (carpeta, protector);

	public static string CarpetaPorDefecto =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "OrderFlowSuite", "SessionsSweeps", "credenciales");

	private string Ruta(string nombre) => Path.Combine(_carpeta, string.Concat(nombre.Where(char.IsLetterOrDigit)) + ".bin");

	public void Guardar(string nombre, string secreto)
	{
		Directory.CreateDirectory(_carpeta);
		File.WriteAllBytes(Ruta(nombre), _protector.Proteger(Encoding.UTF8.GetBytes(secreto)));
	}

	public string? Leer(string nombre)
	{
		try
		{
			return File.Exists(Ruta(nombre)) ? Encoding.UTF8.GetString(_protector.Desproteger(File.ReadAllBytes(Ruta(nombre)))) : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public bool Existe(string nombre) => File.Exists(Ruta(nombre));

	public void Borrar(string nombre)
	{
		if (File.Exists(Ruta(nombre)))
			File.Delete(Ruta(nombre));
	}

	/// <summary>Quita de un texto cualquier secreto guardado (para mensajes de error y logs).</summary>
	public string Limpiar(string texto, params string[] nombres)
	{
		foreach (var n in nombres)
		{
			if (Leer(n) is { Length: > 3 } s)
				texto = texto.Replace(s, "***", StringComparison.Ordinal);
		}

		return texto;
	}
}
