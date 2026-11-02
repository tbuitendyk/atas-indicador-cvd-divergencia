namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

/// <summary>
/// EventRecorder: graba cada publicación de un evento (nacimiento y cada cambio de estado) en
/// %APPDATA%\ATAS\OrderFlowSuite\{Instrumento}\yyyy-MM-dd.csv (fecha de Nueva York). La escritura es asíncrona: el hilo
/// de datos solo encola; un temporizador vacía la cola cada FlushSeconds y al descargar el indicador (Dispose).
/// Si el archivo está bloqueado (abierto en Excel), las filas esperan en la cola y se reintenta.
/// </summary>
public sealed class GrabadorEventos : IDisposable
{
	public const string Cabecera =
		"TimestampUTC,TimestampNY,Instrumento,Indicador,Tipo,Lado,Precio,PrecioFin,Volumen,Clip,Recargas,Ratio,Estado,Sesgo,Sesion,Modo,Score,Contexto," +
		"InicioUTC,StopLoss,Id,Version,Texto";

	private const string FormatoFecha = "yyyy-MM-dd HH:mm:ss.fff";

	// Todos los indicadores de la suite corren en el mismo proceso de ATAS: un cerrojo por archivo evita mezclar filas.
	private static readonly ConcurrentDictionary<string, object> Cerrojos = new(StringComparer.OrdinalIgnoreCase);

	private readonly ConcurrentQueue<(string Ruta, string Linea)> _cola = new();
	private readonly List<(string Ruta, string Linea)> _reintentos = new();
	private readonly object _vaciando = new();
	private readonly Timer? _temporizador;
	private DateTime _ultimoError = DateTime.MinValue;

	public GrabadorEventos(string carpetaBase, string instrumento, int flushSeconds = 5, bool conTemporizador = true)
	{
		CarpetaBase = carpetaBase;
		Instrumento = string.IsNullOrWhiteSpace(instrumento) ? "Instrumento" : instrumento;

		if (conTemporizador)
		{
			var periodo = TimeSpan.FromSeconds(Math.Max(1, flushSeconds));
			_temporizador = new Timer(_ => Vaciar(), null, periodo, periodo);
		}
	}

	public string CarpetaBase { get; }

	public string Instrumento { get; }

	public Action<string, Exception?>? Error { get; set; }

	public static string CarpetaPorDefecto =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "OrderFlowSuite");

	public static string Carpeta(string carpetaBase, string instrumento)
	{
		var limpio = new string(instrumento.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
		return Path.Combine(carpetaBase, limpio);
	}

	public static string Archivo(string carpetaBase, string instrumento, DateTime fechaNy) =>
		Path.Combine(Carpeta(carpetaBase, instrumento), fechaNy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv");

	public void Encolar(EventoFlujo e)
	{
		if (e.Grabado || e.Informativo)
			return;

		var ruta = Archivo(CarpetaBase, Instrumento, Zonas.AEt(e.CambioUtc).Date);
		_cola.Enqueue((ruta, Linea(e, Instrumento)));
	}

	/// <summary>Escribe lo que haya en la cola (lo llama el temporizador y Dispose).</summary>
	public void Vaciar()
	{
		lock (_vaciando)
		{
			var lote = new List<(string Ruta, string Linea)>(_reintentos);
			_reintentos.Clear();

			while (_cola.TryDequeue(out var x))
				lote.Add(x);

			foreach (var grupo in lote.GroupBy(x => x.Ruta))
			{
				try
				{
					lock (Cerrojos.GetOrAdd(grupo.Key, _ => new object()))
					{
						Directory.CreateDirectory(Path.GetDirectoryName(grupo.Key)!);

						using var fs = new FileStream(grupo.Key, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
						var texto = new StringBuilder();

						// UTF-8 con BOM al crear el archivo: Excel muestra bien los acentos.
						if (fs.Length == 0)
						{
							var bom = Encoding.UTF8.GetPreamble();
							fs.Write(bom, 0, bom.Length);
							texto.Append(Cabecera).Append("\r\n");
						}

						foreach (var (_, linea) in grupo)
							texto.Append(linea).Append("\r\n");

						var bytes = new UTF8Encoding(false).GetBytes(texto.ToString());
						fs.Write(bytes, 0, bytes.Length);
					}
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					_reintentos.AddRange(grupo);

					if (DateTime.UtcNow - _ultimoError > TimeSpan.FromMinutes(1))
					{
						_ultimoError = DateTime.UtcNow;
						Error?.Invoke($"No se pudo grabar {grupo.Key} (¿abierto en Excel?): se reintenta.", ex);
					}
				}
			}

			// Límite de memoria si el archivo sigue bloqueado mucho tiempo.
			if (_reintentos.Count > 20000)
				_reintentos.RemoveRange(0, _reintentos.Count - 20000);
		}
	}

	public void Dispose()
	{
		_temporizador?.Dispose();
		Vaciar();
	}

	#region CSV

	public static string Linea(EventoFlujo e, string instrumento)
	{
		var esSenal = e.Tipo == "Signal";
		var lado = esSenal ? (e.Lado == Lado.Compra ? "Largo" : "Corto") : (e.Lado == Lado.Compra ? "Compra" : "Venta");

		var campos = new[]
		{
			e.CambioUtc.ToString(FormatoFecha, CultureInfo.InvariantCulture),
			Zonas.AEt(e.CambioUtc).ToString(FormatoFecha, CultureInfo.InvariantCulture),
			instrumento, e.Indicador, e.Tipo, lado,
			D(e.Precio), D(e.PrecioFin), D(e.Volumen), I(e.Clip), I(e.Recargas), D(e.Ratio),
			e.Estado, e.Sesgo, e.Sesion.ToString(), e.Modo.Csv(), I(e.Score), e.Contexto,
			e.Utc.ToString(FormatoFecha, CultureInfo.InvariantCulture), D(e.StopLoss),
			e.Id.ToString(CultureInfo.InvariantCulture), e.Version.ToString(CultureInfo.InvariantCulture), e.Texto
		};

		return string.Join(",", campos.Select(Escapar));
	}

	private static string D(decimal? v) => v?.ToString("0.########", CultureInfo.InvariantCulture) ?? "";

	private static string I(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "";

	private static string Escapar(string? s)
	{
		s ??= "";
		return s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
	}

	public static List<string> Campos(string linea)
	{
		var r = new List<string>();
		var actual = new StringBuilder();
		var comillas = false;

		for (var i = 0; i < linea.Length; i++)
		{
			var c = linea[i];

			if (comillas)
			{
				if (c == '"' && i + 1 < linea.Length && linea[i + 1] == '"')
				{
					actual.Append('"');
					i++;
				}
				else if (c == '"')
				{
					comillas = false;
				}
				else
				{
					actual.Append(c);
				}
			}
			else if (c == '"')
			{
				comillas = true;
			}
			else if (c == ',')
			{
				r.Add(actual.ToString());
				actual.Clear();
			}
			else
			{
				actual.Append(c);
			}
		}

		r.Add(actual.ToString());
		return r;
	}

	/// <summary>
	/// LoadRecordedEvents: lee los CSV de los últimos 'dias' días (hoy incluido) de ese instrumento y devuelve la última
	/// fila de cada evento (por Id), solo del indicador pedido (null = todos).
	/// </summary>
	public static List<EventoFlujo> Leer(string carpetaBase, string instrumento, string? indicador, DateTime hoyUtc, int dias, Action<string, Exception?>? error = null)
	{
		var porId = new Dictionary<long, EventoFlujo>();
		var hoy = Zonas.AEt(hoyUtc).Date;

		for (var d = Math.Max(1, dias) - 1; d >= 0; d--)
		{
			var ruta = Archivo(carpetaBase, instrumento, hoy.AddDays(-d));

			if (!File.Exists(ruta))
				continue;

			try
			{
				using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				using var lector = new StreamReader(fs, Encoding.UTF8, true);
				var cabecera = Campos(lector.ReadLine() ?? "");
				var col = cabecera.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
				string? linea;

				while ((linea = lector.ReadLine()) is not null)
				{
					if (string.IsNullOrWhiteSpace(linea))
						continue;

					if (Fila(Campos(linea), col) is not { } e || (indicador is not null && e.Indicador != indicador))
						continue;

					if (!porId.TryGetValue(e.Id, out var previo) || previo.Version <= e.Version)
						porId[e.Id] = e;
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				error?.Invoke($"No se pudo leer {ruta}", ex);
			}
		}

		return porId.Values.OrderBy(e => e.Utc).ToList();
	}

	private static EventoFlujo? Fila(List<string> f, Dictionary<string, int> col)
	{
		string V(string nombre) => col.TryGetValue(nombre, out var i) && i < f.Count ? f[i] : "";

		static decimal? Dec(string s) => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

		static int? Ent(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

		static DateTime? Fecha(string s) =>
			DateTime.TryParseExact(s, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var v)
				? DateTime.SpecifyKind(v, DateTimeKind.Utc)
				: null;

		if (!long.TryParse(V("Id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || Fecha(V("TimestampUTC")) is not { } cambio)
			return null;

		var lado = V("Lado") is "Compra" or "Largo" ? Lado.Compra : Lado.Venta;

		return new EventoFlujo
		{
			Id = id,
			Version = Ent(V("Version")) ?? 1,
			Indicador = V("Indicador"),
			Tipo = V("Tipo"),
			Lado = lado,
			Utc = Fecha(V("InicioUTC")) ?? cambio,
			CambioUtc = cambio,
			Precio = Dec(V("Precio")) ?? 0,
			PrecioFin = Dec(V("PrecioFin")),
			Volumen = Dec(V("Volumen")) ?? 0,
			Clip = Ent(V("Clip")),
			Recargas = Ent(V("Recargas")),
			Ratio = Dec(V("Ratio")),
			Estado = V("Estado"),
			Sesgo = V("Sesgo"),
			Sesion = Enum.TryParse<Sesion>(V("Sesion"), out var s) ? s : Sesion.FueraDeHorario,
			Modo = V("Modo") switch { "MBO" => ModoDatos.MBO, "Sintetico" => ModoDatos.Sintetico, "Inferido" => ModoDatos.Inferido, _ => ModoDatos.Velas },
			Score = Ent(V("Score")),
			Contexto = V("Contexto"),
			StopLoss = Dec(V("StopLoss")),
			Texto = V("Texto"),
			AltaProbabilidad = V("Texto").StartsWith("*", StringComparison.Ordinal),
			Grabado = true
		};
	}

	#endregion
}
