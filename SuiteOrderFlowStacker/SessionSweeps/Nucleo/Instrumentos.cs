namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;

/// <summary>Familias de contrato soportadas (2.1). Desconocido = INSTRUMENTO NO SOPORTADO.</summary>
public enum RaizInstrumento
{
	Desconocido,
	NQ,
	ES,
	YM,
	MNQ,
	MES,
	MYM
}

public enum ModoVolumenBarra
{
	[Display(Name = "AUTO por instrumento")]
	AutoPorInstrumento,

	[Display(Name = "Manual por instrumento")]
	ManualPorInstrumento
}

public enum EstadoCalibracion
{
	Calibrado,
	Insuficiente,
	Manual
}

/// <summary>Especificación de referencia del contrato (CME): tick y valor de un punto en USD.</summary>
public sealed record EspecContrato(RaizInstrumento Raiz, decimal Tick, decimal ValorPunto)
{
	public decimal ValorTick => Tick * ValorPunto;
}

/// <summary>Detección del instrumento y especificaciones (2.2, 2.3).</summary>
public static class Instrumentos
{
	public const string NoSoportado = "INSTRUMENTO NO SOPORTADO — CONFIGURACIÓN MANUAL REQUERIDA";
	public const string NoValidado = "VALOR DE CONTRATO NO VALIDADO";

	/// <summary>Valores de referencia del prompt (se contrastan con el tick que da ATAS).</summary>
	public static readonly IReadOnlyDictionary<RaizInstrumento, EspecContrato> Referencia = new Dictionary<RaizInstrumento, EspecContrato>
	{
		[RaizInstrumento.NQ] = new(RaizInstrumento.NQ, 0.25m, 20m),
		[RaizInstrumento.ES] = new(RaizInstrumento.ES, 0.25m, 50m),
		[RaizInstrumento.YM] = new(RaizInstrumento.YM, 1m, 5m),
		[RaizInstrumento.MNQ] = new(RaizInstrumento.MNQ, 0.25m, 2m),
		[RaizInstrumento.MES] = new(RaizInstrumento.MES, 0.25m, 5m),
		[RaizInstrumento.MYM] = new(RaizInstrumento.MYM, 1m, 0.5m)
	};

	/// <summary>El micro de cada familia (para "RIESGO INSUFICIENTE: usar …").</summary>
	public static RaizInstrumento Micro(RaizInstrumento r) => r switch
	{
		RaizInstrumento.NQ => RaizInstrumento.MNQ,
		RaizInstrumento.ES => RaizInstrumento.MES,
		RaizInstrumento.YM => RaizInstrumento.MYM,
		_ => r
	};

	/// <summary>
	/// Raíz y contrato desde el símbolo de ATAS ("NQZ6", "MESH27", "YM 12-26"…). Primero los alias configurables
	/// ("ENQ=NQ;EP=ES"), después la raíz conocida más larga al principio del símbolo (MES antes que ES). El contrato es
	/// el símbolo completo en mayúsculas sin espacios.
	/// </summary>
	public static (RaizInstrumento Raiz, string Contrato) Detectar(string? simbolo, string alias = "")
	{
		if (string.IsNullOrWhiteSpace(simbolo))
			return (RaizInstrumento.Desconocido, "");

		var limpio = new string(simbolo.Trim().ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
		var candidatos = new List<(string Prefijo, RaizInstrumento Raiz)>();

		foreach (var par in alias.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
		{
			var partes = par.Split('=');

			if (partes.Length == 2 && Enum.TryParse<RaizInstrumento>(partes[1].Trim(), true, out var r) && r != RaizInstrumento.Desconocido)
				candidatos.Add((partes[0].Trim().ToUpperInvariant(), r));
		}

		foreach (var r in Referencia.Keys)
			candidatos.Add((r.ToString(), r));

		var elegido = candidatos.Where(c => c.Prefijo.Length > 0 && limpio.StartsWith(c.Prefijo, StringComparison.Ordinal))
			.OrderByDescending(c => c.Prefijo.Length)
			.FirstOrDefault();

		// La raíz debe ir seguida del vencimiento (o nada): "ESZ6" sí, "ESTX50" no.
		if (elegido.Prefijo is null)
			return (RaizInstrumento.Desconocido, limpio);

		var resto = limpio[elegido.Prefijo.Length..];

		if (resto.Length > 0 && !char.IsDigit(resto[0]) && "FGHJKMNQUVXZ-_.".IndexOf(resto[0]) < 0)
			return (RaizInstrumento.Desconocido, limpio);

		return (elegido.Raiz, limpio);
	}

	/// <summary>¿El tick de ATAS coincide con la especificación? Sin dato de ATAS no se puede validar.</summary>
	public static bool TickValido(RaizInstrumento r, decimal? tickAtas) =>
		Referencia.TryGetValue(r, out var e) && tickAtas is > 0 && Math.Abs(tickAtas.Value - e.Tick) < 0.0000001m;
}

/// <summary>
/// InstrumentProfile (2.4): los umbrales que dependen del instrumento (contratos, ticks y los que el prompt pide no
/// compartir). Uno por familia; al abrir un gráfico se usa SOLO el de su familia. Los valores iniciales de NQ son los de
/// la v2; los de ES, YM y micros son PROVISIONALES (no se copian de NQ) y se calibran con el CSV.
/// </summary>
public sealed class PerfilInstrumento
{
	public PerfilInstrumento(RaizInstrumento raiz) => Raiz = raiz;

	public RaizInstrumento Raiz { get; }

	public ModoVolumenBarra VolumePerBarMode { get; set; } = ModoVolumenBarra.AutoPorInstrumento;

	/// <summary>Manual, o provisional mientras AUTO no tenga datos.</summary>
	public int VolumePerBar { get; set; } = 500;

	public int VolumePerBarMin { get; set; } = 50;
	public int VolumePerBarMax { get; set; } = 5000;
	public int CalibrationLookbackSessions { get; set; } = 20;
	public int MinimumCalibrationSessions { get; set; } = 20;
	public int TimeOfDayBucketMinutes { get; set; } = 15;

	// Contratos (brutos).
	public int MinVolBeyondFloor { get; set; } = 50;
	public int SuperBigTradeMinContracts { get; set; } = 150;
	public int ImbalanceMinVolume { get; set; } = 5;
	public int FinishedAuctionMaxContracts { get; set; } = 2;

	// Normalizados, pero propios del instrumento (2.4).
	public decimal ImbalanceRatio { get; set; } = 3.0m;
	public int StackedMinLevels { get; set; } = 3;
	public decimal AggressorShareMin { get; set; } = 0.65m;
	public decimal BurstSpeedMult { get; set; } = 2.5m;
	public decimal F3DeltaPctMin { get; set; } = 0.25m;
	public decimal ContDeltaPctMin { get; set; } = 0.30m;
	public decimal DivergenceDeltaPctMin { get; set; } = 0.15m;

	// Ticks del instrumento (un tick de YM no es un tick de NQ).
	public int MinPenetrationTicks { get; set; } = 2;
	public int MaxSweepTicks { get; set; } = 24;
	public int RecoveryTicks { get; set; } = 16;
	public int StopBufferTicks { get; set; } = 3;
	public int MaxStopTicks { get; set; } = 40;

	/// <summary>Valores iniciales de cada familia. NQ = la v2; el resto PROVISIONAL.</summary>
	public static PerfilInstrumento PorDefecto(RaizInstrumento r)
	{
		var p = new PerfilInstrumento(r);

		switch (r)
		{
			case RaizInstrumento.ES:
			case RaizInstrumento.MES:
				(p.VolumePerBar, p.MinVolBeyondFloor, p.SuperBigTradeMinContracts, p.ImbalanceMinVolume, p.FinishedAuctionMaxContracts) = (1500, 150, 400, 15, 5);
				(p.MinPenetrationTicks, p.MaxSweepTicks, p.RecoveryTicks, p.StopBufferTicks, p.MaxStopTicks) = (1, 8, 6, 2, 16);
				break;

			case RaizInstrumento.YM:
			case RaizInstrumento.MYM:
				(p.VolumePerBar, p.MinVolBeyondFloor, p.SuperBigTradeMinContracts, p.ImbalanceMinVolume, p.FinishedAuctionMaxContracts) = (150, 15, 40, 2, 1);
				(p.MinPenetrationTicks, p.MaxSweepTicks, p.RecoveryTicks, p.StopBufferTicks, p.MaxStopTicks) = (1, 12, 8, 2, 25);
				break;

			case RaizInstrumento.MNQ:
				(p.VolumePerBar, p.MinVolBeyondFloor, p.SuperBigTradeMinContracts, p.ImbalanceMinVolume, p.FinishedAuctionMaxContracts) = (1000, 100, 300, 10, 4);
				break;
		}

		return p;
	}

	/// <summary>Copia los valores de este perfil a los parámetros efectivos del motor (y el volumen por barra elegido).</summary>
	public void AplicarA(ParametrosV2 p, int volumenPorBarra)
	{
		p.VolumePerBar = volumenPorBarra;
		(p.MinVolBeyondFloor, p.SuperBigTradeMinContracts, p.ImbalanceMinVolume, p.FinishedAuctionMaxContracts) =
			(MinVolBeyondFloor, SuperBigTradeMinContracts, ImbalanceMinVolume, FinishedAuctionMaxContracts);
		(p.ImbalanceRatio, p.StackedMinLevels, p.AggressorShareMin, p.BurstSpeedMult) = (ImbalanceRatio, StackedMinLevels, AggressorShareMin, BurstSpeedMult);
		(p.F3DeltaPctMin, p.ContDeltaPctMin, p.DivergenceDeltaPctMin) = (F3DeltaPctMin, ContDeltaPctMin, DivergenceDeltaPctMin);
		(p.MinPenetrationTicks, p.MaxSweepTicks, p.RecoveryTicks, p.StopBufferTicks, p.MaxStopTicks) =
			(MinPenetrationTicks, MaxSweepTicks, RecoveryTicks, StopBufferTicks, MaxStopTicks);
	}
}

/// <summary>Los seis perfiles (cada uno con sus propios valores; nunca se comparten).</summary>
public sealed class PerfilesInstrumento
{
	private readonly Dictionary<RaizInstrumento, PerfilInstrumento> _perfiles =
		Instrumentos.Referencia.Keys.ToDictionary(r => r, PerfilInstrumento.PorDefecto);

	public PerfilInstrumento NQ => _perfiles[RaizInstrumento.NQ];
	public PerfilInstrumento ES => _perfiles[RaizInstrumento.ES];
	public PerfilInstrumento YM => _perfiles[RaizInstrumento.YM];
	public PerfilInstrumento MNQ => _perfiles[RaizInstrumento.MNQ];
	public PerfilInstrumento MES => _perfiles[RaizInstrumento.MES];
	public PerfilInstrumento MYM => _perfiles[RaizInstrumento.MYM];

	public PerfilInstrumento? De(RaizInstrumento r) => _perfiles.TryGetValue(r, out var p) ? p : null;
}

/// <summary>Resultado de la calibración de volumen y delta de un instrumento (2.5, 2.6). Se guarda por familia.</summary>
public sealed class CalibracionInstrumento
{
	public RaizInstrumento Raiz { get; set; }
	public string Contrato { get; set; } = "";
	public int Sesiones { get; set; }
	public int MinutosRth { get; set; }
	public decimal MedianaVolumenMinuto { get; set; }
	public decimal MedianaDeltaAbsMinuto { get; set; }
	public decimal Percentil90VolumenMinuto { get; set; }

	/// <summary>Mediana del volumen de 1 minuto por franja (minuto del día ET de inicio de la franja).</summary>
	public Dictionary<int, decimal> MedianaVolumenPorFranja { get; set; } = new();

	public int FranjaMinutos { get; set; } = 15;
	public int VolumePerBarAuto { get; set; }
	public EstadoCalibracion Estado { get; set; } = EstadoCalibracion.Insuficiente;
	public DateTime FechaUtc { get; set; }
	public string Metodo { get; set; } = "";

	public string Texto => Estado switch
	{
		EstadoCalibracion.Calibrado => "CALIBRADO",
		EstadoCalibracion.Manual => "CONFIGURACIÓN MANUAL",
		_ => "CALIBRACIÓN INSUFICIENTE"
	};

	public string ToJson() => JsonSerializer.Serialize(this);

	public static CalibracionInstrumento? DeJson(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<CalibracionInstrumento>(json);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>VolumenRelativo (2.5): volumen de 1 minuto respecto a la mediana de su franja (o la general).</summary>
	public decimal VolumenRelativo(decimal volumenMinuto, DateTime utc)
	{
		var et = Zonas.AEt(utc);
		var franja = (int)(et.TimeOfDay.TotalMinutes / Math.Max(1, FranjaMinutos)) * Math.Max(1, FranjaMinutos);
		var referencia = MedianaVolumenPorFranja.TryGetValue(franja, out var m) && m > 0 ? m : MedianaVolumenMinuto;
		return referencia > 0 ? volumenMinuto / referencia : 0;
	}

	/// <summary>DeltaRelativo (2.5): |delta| respecto a la mediana del instrumento.</summary>
	public decimal DeltaRelativo(decimal delta) => MedianaDeltaAbsMinuto > 0 ? Math.Abs(delta) / MedianaDeltaAbsMinuto : 0;
}

/// <summary>Medidas normalizadas (2.5 A).</summary>
public static class Medidas
{
	public static decimal DeltaPct(decimal delta, decimal volumen) => volumen > 0 ? delta / volumen : 0;

	public static decimal AggressorShare(decimal agresor, decimal volumen) => volumen > 0 ? agresor / volumen : 0;

	public static decimal Ratio(decimal actual, decimal mediana) => mediana > 0 ? actual / mediana : 0;
}

/// <summary>
/// Calibración (2.5): con las velas de 1 minuto del propio instrumento, solo RTH (09:30-16:00 ET) y solo sesiones con al
/// menos una hora de RTH, de las últimas CalibrationLookbackSessions: medianas del volumen y del |delta| por minuto, por
/// franja y percentil 90. VolumePerBar AUTO = mediana del volumen de 1 minuto × fracción, entre el mínimo y el máximo
/// del perfil. CALIBRADO con MinimumCalibrationSessions sesiones o más; si no, CALIBRACIÓN INSUFICIENTE (los valores se
/// usan marcados como PROVISIONAL).
/// </summary>
public static class Calibrador
{
	public static CalibracionInstrumento Calcular(IEnumerable<(DateTime InicioUtc, decimal Volumen, decimal Delta)> velas1m, PerfilInstrumento perfil,
		string contrato, decimal fraccionBarra, DateTime ahoraUtc)
	{
		var rth = velas1m.Where(v => TiempoV2.EsRth(v.InicioUtc) && v.Volumen > 0).ToList();
		var dias = rth.GroupBy(v => Zonas.DiaOperativo(v.InicioUtc)).Where(g => g.Count() >= 60).OrderBy(g => g.Key).ToList();
		var usados = dias.TakeLast(Math.Max(1, perfil.CalibrationLookbackSessions)).SelectMany(g => g).ToList();
		var franja = Math.Max(1, perfil.TimeOfDayBucketMinutes);

		var c = new CalibracionInstrumento
		{
			Raiz = perfil.Raiz,
			Contrato = contrato,
			Sesiones = Math.Min(dias.Count, Math.Max(1, perfil.CalibrationLookbackSessions)),
			MinutosRth = usados.Count,
			FranjaMinutos = franja,
			FechaUtc = ahoraUtc,
			Metodo = $"mediana del volumen de 1 min en RTH × {fraccionBarra.ToString("0.##", CultureInfo.InvariantCulture)}, últimas {perfil.CalibrationLookbackSessions} sesiones"
		};

		if (usados.Count > 0)
		{
			c.MedianaVolumenMinuto = Mediana(usados.Select(v => v.Volumen));
			c.MedianaDeltaAbsMinuto = Mediana(usados.Select(v => Math.Abs(v.Delta)));
			c.Percentil90VolumenMinuto = Percentil(usados.Select(v => v.Volumen), 0.9m);
			c.MedianaVolumenPorFranja = usados.GroupBy(v => (int)(Zonas.AEt(v.InicioUtc).TimeOfDay.TotalMinutes / franja) * franja)
				.ToDictionary(g => g.Key, g => Mediana(g.Select(v => v.Volumen)));
			var auto = (int)(Math.Round(c.MedianaVolumenMinuto * fraccionBarra / 10m) * 10m);
			c.VolumePerBarAuto = Math.Clamp(auto, Math.Max(10, perfil.VolumePerBarMin), Math.Max(perfil.VolumePerBarMin, perfil.VolumePerBarMax));
		}

		c.Estado = perfil.VolumePerBarMode == ModoVolumenBarra.ManualPorInstrumento ? EstadoCalibracion.Manual
			: dias.Count >= perfil.MinimumCalibrationSessions ? EstadoCalibracion.Calibrado
			: EstadoCalibracion.Insuficiente;
		return c;
	}

	/// <summary>Contratos por barra que se usan: manual, o AUTO (con datos), o el provisional del perfil.</summary>
	public static int VolumenPorBarra(PerfilInstrumento p, CalibracionInstrumento? c) =>
		p.VolumePerBarMode == ModoVolumenBarra.ManualPorInstrumento || c is not { VolumePerBarAuto: > 0 } ? Math.Max(10, p.VolumePerBar) : c.VolumePerBarAuto;

	public static decimal Mediana(IEnumerable<decimal> valores) => Percentil(valores, 0.5m);

	public static decimal Percentil(IEnumerable<decimal> valores, decimal q)
	{
		var v = valores.OrderBy(x => x).ToList();

		if (v.Count == 0)
			return 0;

		var pos = (v.Count - 1) * q;
		var i = (int)Math.Floor(pos);
		var f = pos - i;
		return i + 1 < v.Count ? v[i] + (v[i + 1] - v[i]) * f : v[i];
	}
}

/// <summary>Lo que el motor sabe del instrumento del gráfico (panel, riesgo, bloqueo de señales).</summary>
public sealed class EstadoInstrumento
{
	public RaizInstrumento Raiz { get; init; } = RaizInstrumento.Desconocido;
	public string Contrato { get; init; } = "";
	public EspecContrato? Spec { get; init; }
	public decimal? TickAtas { get; init; }
	public bool TickValidado { get; init; }
	public CalibracionInstrumento? Calibracion { get; init; }
	public int VolumenPorBarra { get; init; }

	public bool Soportado => Raiz != RaizInstrumento.Desconocido;

	/// <summary>Sin instrumento reconocido no hay señales; sin contrato validado no hay contratos ni órdenes.</summary>
	public string? Bloqueo => !Soportado ? Instrumentos.NoSoportado : null;

	public decimal ValorTick => TickValidado && Spec is { } e ? e.ValorTick : 0;

	public string Perfil => Calibracion?.Texto ?? "CALIBRACIÓN INSUFICIENTE";

	public bool Provisional => Calibracion?.Estado != EstadoCalibracion.Calibrado && Calibracion?.Estado != EstadoCalibracion.Manual;

	public static EstadoInstrumento Crear(string? simbolo, string alias, decimal? tickAtas, CalibracionInstrumento? calibracion, int volumenPorBarra)
	{
		var (raiz, contrato) = Instrumentos.Detectar(simbolo, alias);
		return new EstadoInstrumento
		{
			Raiz = raiz,
			Contrato = contrato,
			Spec = Instrumentos.Referencia.TryGetValue(raiz, out var e) ? e : null,
			TickAtas = tickAtas,
			TickValidado = Instrumentos.TickValido(raiz, tickAtas),
			Calibracion = calibracion,
			VolumenPorBarra = volumenPorBarra
		};
	}
}
