namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// v3 S15: puente opcional con MT5. APAGADO por defecto. El indicador escucha en 127.0.0.1 (TCP con HTTP mínimo, texto
/// plano); el EA SessionsSweepsBridge.mq5 consulta /comandos y devuelve /reporte. Ninguna credencial pasa por aquí: la
/// cuenta vive en MT5. // VERIFICAR: WebRequest de MT5 a 127.0.0.1 (alternativa: archivos en la carpeta común de MT5).
/// </summary>
public partial class SessionSweepsPro
{
	private const string G38 = "38. MT5 (opcional)";
	private const string FraseReal = "CONFIRMO OPERAR EN REAL";

	private readonly GestorMt5 _mt5 = new();
	private TcpListener? _puente;
	private CancellationTokenSource? _finPuente;
	private int _puertoActivo;

	[Display(Name = "Modo", GroupName = G38, Order = 10, Description = "APAGADO por defecto. SIMULACIÓN no necesita MT5. REAL exige escribir la frase de confirmación.")]
	public ModoMt5 Mt5Mode { get => _mt5.Modo; set { _mt5.Modo = value; ActualizarPuente(); } }

	[Display(Name = "Confirmación para REAL", GroupName = G38, Order = 20, Description = "Escriba exactamente: " + FraseReal)]
	public string Mt5ConfirmReal { get => _mt5.RealConfirmado ? FraseReal : ""; set => _mt5.RealConfirmado = string.Equals(value?.Trim(), FraseReal, StringComparison.Ordinal); }

	[Display(Name = "Puerto local del puente", GroupName = G38, Order = 30)]
	[Range(1024, 65535)]
	public int Mt5Port { get; set; } = 5005;

	[Display(Name = "Operaciones máximas por día", GroupName = G38, Order = 40)]
	[Range(1, 100)]
	public int MaxTradesPerDay { get => _mt5.MaxOperacionesDia; set => _mt5.MaxOperacionesDia = Math.Clamp(value, 1, 100); }

	[Display(Name = "Pérdida máxima diaria (USD)", GroupName = G38, Order = 50)]
	[Range(1, 1000000)]
	public decimal MaxDailyLoss { get => _mt5.MaxPerdidaDiaUsd; set => _mt5.MaxPerdidaDiaUsd = Math.Max(1, value); }

	[Display(Name = "Deslizamiento máximo (puntos)", GroupName = G38, Order = 60)]
	[Range(0, 1000)]
	public int MaxSlippage { get => _mt5.MaxSlippagePuntos; set => _mt5.MaxSlippagePuntos = Math.Clamp(value, 0, 1000); }

	[Display(Name = "SIMULACIÓN sin calibración (prueba)", GroupName = G38, Order = 70, Description = "Solo en SIMULACIÓN: permite simular con un perfil sin calibrar.")]
	public bool Mt5AllowUncalibratedSimulation { get => _mt5.PermitirSinCalibrarEnSimulacion; set => _mt5.PermitirSinCalibrarEnSimulacion = value; }

	[Display(Name = "NQ → símbolo MT5", GroupName = G38, Order = 100)]
	public string Mt5SymbolNQ { get => _mt5.Mapeo(RaizInstrumento.NQ).Simbolo; set => _mt5.Mapeo(RaizInstrumento.NQ).Simbolo = (value ?? "").Trim(); }

	[Display(Name = "NQ: mapeo validado", GroupName = G38, Order = 101, Description = "Márquelo solo después de revisar la prueba de conversión del panel (recuadro 16).")]
	public bool Mt5ValidatedNQ { get => _mt5.Mapeo(RaizInstrumento.NQ).ValidadoPorUsuario; set => _mt5.Mapeo(RaizInstrumento.NQ).ValidadoPorUsuario = value; }

	[Display(Name = "NQ: factor de distancia", GroupName = G38, Order = 102, Description = "Distancia en MT5 por punto de ATAS (1 si el símbolo sigue al mismo índice).")]
	public decimal Mt5FactorNQ { get => _mt5.Mapeo(RaizInstrumento.NQ).FactorPrecio; set => _mt5.Mapeo(RaizInstrumento.NQ).FactorPrecio = Math.Max(0, value); }

	[Display(Name = "ES → símbolo MT5", GroupName = G38, Order = 110)]
	public string Mt5SymbolES { get => _mt5.Mapeo(RaizInstrumento.ES).Simbolo; set => _mt5.Mapeo(RaizInstrumento.ES).Simbolo = (value ?? "").Trim(); }

	[Display(Name = "ES: mapeo validado", GroupName = G38, Order = 111)]
	public bool Mt5ValidatedES { get => _mt5.Mapeo(RaizInstrumento.ES).ValidadoPorUsuario; set => _mt5.Mapeo(RaizInstrumento.ES).ValidadoPorUsuario = value; }

	[Display(Name = "ES: factor de distancia", GroupName = G38, Order = 112)]
	public decimal Mt5FactorES { get => _mt5.Mapeo(RaizInstrumento.ES).FactorPrecio; set => _mt5.Mapeo(RaizInstrumento.ES).FactorPrecio = Math.Max(0, value); }

	[Display(Name = "YM → símbolo MT5", GroupName = G38, Order = 120)]
	public string Mt5SymbolYM { get => _mt5.Mapeo(RaizInstrumento.YM).Simbolo; set => _mt5.Mapeo(RaizInstrumento.YM).Simbolo = (value ?? "").Trim(); }

	[Display(Name = "YM: mapeo validado", GroupName = G38, Order = 121)]
	public bool Mt5ValidatedYM { get => _mt5.Mapeo(RaizInstrumento.YM).ValidadoPorUsuario; set => _mt5.Mapeo(RaizInstrumento.YM).ValidadoPorUsuario = value; }

	[Display(Name = "YM: factor de distancia", GroupName = G38, Order = 122)]
	public decimal Mt5FactorYM { get => _mt5.Mapeo(RaizInstrumento.YM).FactorPrecio; set => _mt5.Mapeo(RaizInstrumento.YM).FactorPrecio = Math.Max(0, value); }

	private string EstadoMt5Texto() => _motor is { } m ? _mt5.Estado(m.Raiz) : _mt5.Estado(RaizInstrumento.NQ);

	/// <summary>Una entrada válida (A o B) en vivo: el gestor decide; el resultado queda en su registro y en el panel.</summary>
	private void Mt5Entrada(MotorSweeps motor, SetupModelo s, DateTime utc)
	{
		if (s.Entrada is not { } entrada || s.Stop is not { } stop || s.Tp1 is not { } tp)
			return;

		var r = _mt5.Evaluar(s.Id * 1000 + (s.Barrido?.Id ?? 0) % 1000, motor.Raiz, s.Signo, Math.Abs(entrada - stop), Math.Abs(tp - entrada), s.Riesgo?.RiesgoUsd ?? 0,
			motor.Instrumento, _enVivo, TiempoV2.EnNoticia(utc, _cfg.V2), utc, $"SSP {s.ModeloOperativo} {s.Etiqueta}");

		if (r.Enviada)
			this.LogWarn("Sessions Sweeps Pro: " + r.Texto);
	}

	/// <summary>Arranca o para el puente según el modo (solo DEMO y REAL lo necesitan).</summary>
	private void ActualizarPuente()
	{
		try
		{
			var necesario = _mt5.Modo is ModoMt5.Demo or ModoMt5.Real;

			if (_puente is not null && (!necesario || _puertoActivo != Mt5Port))
				PararPuente();

			if (!necesario || _puente is not null)
				return;

			_finPuente = new CancellationTokenSource();
			_puente = new TcpListener(IPAddress.Loopback, Mt5Port);
			_puente.Start();
			_puertoActivo = Mt5Port;
			var fin = _finPuente.Token;
			_ = Task.Run(() => Escuchar(_puente, fin));
			_mt5.PedirFichas();
		}
		catch (Exception ex)
		{
			this.LogWarn($"Sessions Sweeps Pro: no se pudo abrir el puente MT5 en 127.0.0.1:{Mt5Port}: {ex.Message}");
			PararPuente();
		}
	}

	private void PararPuente()
	{
		_finPuente?.Cancel();

		try
		{
			_puente?.Stop();
		}
		catch (Exception)
		{
			// Ya parado.
		}

		(_puente, _finPuente) = (null, null);
	}

	private async Task Escuchar(TcpListener l, CancellationToken fin)
	{
		while (!fin.IsCancellationRequested)
		{
			try
			{
				using var cliente = await l.AcceptTcpClientAsync(fin).ConfigureAwait(false);
				await Atender(cliente, fin).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex) when (!fin.IsCancellationRequested)
			{
				this.LogWarn("Sessions Sweeps Pro: puente MT5: " + ex.Message);
			}
		}
	}

	/// <summary>GET /comandos (líneas pendientes), POST /reporte (líneas del EA), GET /estado.</summary>
	private async Task Atender(TcpClient cliente, CancellationToken fin)
	{
		cliente.ReceiveTimeout = cliente.SendTimeout = 5000;
		var flujo = cliente.GetStream();
		using var lector = new StreamReader(flujo, Encoding.UTF8, false, 4096, leaveOpen: true);
		var primera = await lector.ReadLineAsync(fin).ConfigureAwait(false) ?? "";
		var largo = 0;
		string? linea;

		while (!string.IsNullOrEmpty(linea = await lector.ReadLineAsync(fin).ConfigureAwait(false)))
		{
			if (linea.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
				int.TryParse(linea[15..].Trim(), out largo);
		}

		var cuerpo = new char[Math.Clamp(largo, 0, 65536)];
		var leidos = 0;

		while (leidos < cuerpo.Length)
		{
			var n = await lector.ReadAsync(cuerpo.AsMemory(leidos), fin).ConfigureAwait(false);

			if (n == 0)
				break;

			leidos += n;
		}

		var respuesta = "";

		if (primera.StartsWith("GET /comandos", StringComparison.Ordinal))
			respuesta = string.Join("\n", _mt5.TomarComandos());
		else if (primera.StartsWith("POST /reporte", StringComparison.Ordinal))
			foreach (var l in new string(cuerpo, 0, leidos).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				_mt5.Reporte(l);
		else if (primera.StartsWith("GET /estado", StringComparison.Ordinal))
			respuesta = EstadoMt5Texto();

		var bytes = Encoding.UTF8.GetBytes(respuesta);
		var cabecera = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
		await flujo.WriteAsync(cabecera, fin).ConfigureAwait(false);
		await flujo.WriteAsync(bytes, fin).ConfigureAwait(false);
	}
}
