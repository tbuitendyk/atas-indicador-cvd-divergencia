namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

// =====================================================================================================================
// Núcleo de la Suite Order Flow Stacker: sin tipos de ATAS, para probarlo con datos sintéticos y con los CSV grabados.
// Los eventos se guardan por TIEMPO + PRECIO (nunca por índice de vela): así valen en cualquier temporalidad.
// =====================================================================================================================

#region Enumeraciones de los ajustes

public enum ModoVisual
{
	[Display(Name = "Mínimo (solo el dato en texto)")]
	Minimal,

	[Display(Name = "Marcadores (dato + marcador pequeño)")]
	Markers,

	[Display(Name = "Completo (todo dibujado)")]
	Full
}

public enum EsquinaPanel
{
	[Display(Name = "Arriba a la derecha")]
	TopRight,

	[Display(Name = "Arriba a la izquierda")]
	TopLeft,

	[Display(Name = "Abajo a la derecha")]
	BottomRight,

	[Display(Name = "Abajo a la izquierda")]
	BottomLeft
}

public enum PerfilInstrumento
{
	[Display(Name = "Automático (por el nombre)")]
	Auto,

	[Display(Name = "NQ")]
	NQ,

	[Display(Name = "MNQ")]
	MNQ,

	[Display(Name = "ES")]
	ES,

	[Display(Name = "MES")]
	MES,

	[Display(Name = "YM")]
	YM,

	[Display(Name = "MYM")]
	MYM
}

public enum ModoMbo
{
	[Display(Name = "Automático (MBO si llega; si no, inferido)")]
	Auto,

	[Display(Name = "Solo inferido (sin MBO)")]
	SoloInferido
}

public enum FuenteTendencia
{
	[Display(Name = "VWAP")]
	VWAP,

	[Display(Name = "EMA")]
	EMA
}

#endregion

#region Tipos del modelo

public enum Lado
{
	Compra,
	Venta
}

public enum ModoDatos
{
	MBO,
	Sintetico,
	Inferido,
	Velas
}

public enum Sesion
{
	Asia,
	Londres,
	Premercado,
	AperturaNY,
	Mediodia,
	Cierre,
	FueraDeHorario
}

public static class Textos
{
	public static string Texto(this Lado l) => l == Lado.Compra ? "COMPRA" : "VENTA";

	public static int Signo(this Lado l) => l == Lado.Compra ? 1 : -1;

	public static Lado Contrario(this Lado l) => l == Lado.Compra ? Lado.Venta : Lado.Compra;

	public static string Texto(this ModoDatos m) => m switch
	{
		ModoDatos.MBO => "NATIVO",
		ModoDatos.Sintetico => "SINTÉTICO",
		ModoDatos.Inferido => "INFERIDO",
		_ => "VELAS"
	};

	public static string Csv(this ModoDatos m) => m switch
	{
		ModoDatos.MBO => "MBO",
		ModoDatos.Sintetico => "Sintetico",
		ModoDatos.Inferido => "Inferido",
		_ => "Velas"
	};

	public static string Texto(this Sesion s) => s switch
	{
		Sesion.Asia => "Asia",
		Sesion.Londres => "Londres",
		Sesion.Premercado => "Premercado NY",
		Sesion.AperturaNY => "Apertura NY",
		Sesion.Mediodia => "Mediodía",
		Sesion.Cierre => "Cierre",
		_ => "Fuera de horario"
	};

	public static string Precio(decimal p, decimal tick)
	{
		var texto = tick.ToString(CultureInfo.InvariantCulture).TrimEnd('0');
		var decimales = texto.Contains('.') ? texto.Length - texto.IndexOf('.') - 1 : 0;
		return p.ToString("F" + Math.Max(0, decimales), CultureInfo.InvariantCulture);
	}

	public static string Hora(DateTime utc) => Zonas.AEt(utc).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Evento de la suite (iceberg, stops, barrido, imbalance o señal). Se actualiza en su sitio al cambiar de estado
/// (Version sube): así hay una sola alerta por evento y por cambio de estado.
/// </summary>
public sealed class EventoFlujo
{
	public long Id;
	public int Version;

	/// <summary>"Imbalance", "Iceberg", "Stops", "Sweeps", "Master".</summary>
	public string Indicador = "";

	/// <summary>"StackedImb", "Iceberg", "StopRun", "Sweep", "Signal".</summary>
	public string Tipo = "";

	public Lado Lado;
	public DateTime Utc;

	/// <summary>Último cambio (para "reciente" y para ordenar el panel).</summary>
	public DateTime CambioUtc;

	public decimal Precio;
	public decimal? PrecioFin;
	public decimal Volumen;
	public int? Clip, Recargas, Score;
	public decimal? Ratio;
	public string Estado = "";
	public string Sesgo = "";
	public Sesion Sesion;
	public ModoDatos Modo = ModoDatos.Velas;
	public string Contexto = "";

	/// <summary>El "dato" de una línea que se muestra, se alerta y se graba.</summary>
	public string Texto = "";

	/// <summary>Leído de un CSV de días anteriores (no suena ni se vuelve a grabar).</summary>
	public bool Grabado;

	/// <summary>Aviso solo de panel (p. ej. "precio cerca de una zona de stops"): no suena ni se graba.</summary>
	public bool Informativo;

	/// <summary>Iceberg que supera los umbrales de alta probabilidad (se marca con "*").</summary>
	public bool AltaProbabilidad;

	/// <summary>Stop de la señal del Absorption Master (PrecioFin es el objetivo).</summary>
	public decimal? StopLoss;

	/// <summary>Señal: precio donde está la absorción (iceberg o LVN), antes de los ticks de confirmación.</summary>
	public decimal? Referencia;

	/// <summary>Niveles que abarca (barridos y zonas de imbalance) y duración del barrido en ms.</summary>
	public int? Niveles;

	public double? DuracionMs;

	public string Clave => $"{Id}|{Version}";
}

#endregion

#region Tiempo y sesiones

/// <summary>Zona horaria de Nueva York (con cambio de horario): IANA, o el nombre de Windows.</summary>
public static class Zonas
{
	private static TimeZoneInfo? _ny;

	public static TimeZoneInfo NuevaYork => _ny ??= Buscar("America/New_York", "Eastern Standard Time");

	private static TimeZoneInfo Buscar(string iana, string windows)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(iana);
		}
		catch (Exception)
		{
			return TimeZoneInfo.FindSystemTimeZoneById(windows);
		}
	}

	public static DateTime AEt(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NuevaYork);

	public static DateTime AUtc(DateTime et)
	{
		et = DateTime.SpecifyKind(et, DateTimeKind.Unspecified);

		if (NuevaYork.IsInvalidTime(et))
			et = et.AddHours(1);

		return TimeZoneInfo.ConvertTimeToUtc(et, NuevaYork);
	}

	/// <summary>Día operativo de futuros: de 18:00 a 17:00 ET (la fecha es la del cierre).</summary>
	public static DateTime DiaOperativo(DateTime utc) => AEt(utc).AddHours(6).Date;
}

/// <summary>
/// Sesiones de la suite en hora de Nueva York: Asia 18:00-02:00, Londres 02:00 al premercado, premercado NY
/// (PremercadoMinutos antes de las 09:30, por defecto 60), Apertura NY 09:30-11:00, Mediodía 11:00-14:00, Cierre
/// 14:00-16:00; el resto (16:00-18:00) fuera de horario.
/// </summary>
public sealed class ClasificadorSesion
{
	public int PremercadoMinutos { get; set; } = 60;

	public static readonly TimeSpan AperturaNy = new(9, 30, 0);

	public Sesion De(DateTime utc)
	{
		var h = Zonas.AEt(utc).TimeOfDay;
		var premercado = AperturaNy - TimeSpan.FromMinutes(Math.Clamp(PremercadoMinutos, 0, 450));

		if (h >= new TimeSpan(18, 0, 0) || h < new TimeSpan(2, 0, 0))
			return Sesion.Asia;

		if (h < premercado)
			return Sesion.Londres;

		if (h < AperturaNy)
			return Sesion.Premercado;

		if (h < new TimeSpan(11, 0, 0))
			return Sesion.AperturaNY;

		if (h < new TimeSpan(14, 0, 0))
			return Sesion.Mediodia;

		return h < new TimeSpan(16, 0, 0) ? Sesion.Cierre : Sesion.FueraDeHorario;
	}

	/// <summary>RTH = de las 09:30 a las 16:00 ET.</summary>
	public static bool Rth(DateTime utc)
	{
		var h = Zonas.AEt(utc).TimeOfDay;
		return h >= AperturaNy && h < new TimeSpan(16, 0, 0);
	}
}

/// <summary>Umbrales de icebergs de una sesión: ruido mínimo, alta probabilidad y recargas mínimas.</summary>
public sealed class UmbralSesion
{
	public UmbralSesion(decimal minEjecutado, decimal altaProbabilidad, int minRecargas) =>
		(MinEjecutado, AltaProbabilidad, MinRecargas) = (minEjecutado, altaProbabilidad, minRecargas);

	public decimal MinEjecutado { get; set; }

	public decimal AltaProbabilidad { get; set; }

	public int MinRecargas { get; set; }
}

/// <summary>Tabla de umbrales por sesión (NQ y MNQ). Valores iniciales: CALIBRAR con 1-2 semanas de CSV grabados.</summary>
public sealed class TablaUmbrales
{
	public Dictionary<Sesion, UmbralSesion> Nq { get; } = new()
	{
		[Sesion.Asia] = new(40, 120, 3),
		[Sesion.Londres] = new(80, 250, 4),
		[Sesion.Premercado] = new(100, 300, 4),
		[Sesion.AperturaNY] = new(150, 400, 5),
		[Sesion.Mediodia] = new(100, 300, 4),
		[Sesion.Cierre] = new(150, 400, 5),
		[Sesion.FueraDeHorario] = new(40, 120, 3)
	};

	// CALIBRAR: punto de partida = valores de NQ × 0,5.
	public Dictionary<Sesion, UmbralSesion> Mnq { get; } = new()
	{
		[Sesion.Asia] = new(20, 60, 3),
		[Sesion.Londres] = new(40, 125, 4),
		[Sesion.Premercado] = new(50, 150, 4),
		[Sesion.AperturaNY] = new(75, 200, 5),
		[Sesion.Mediodia] = new(50, 150, 4),
		[Sesion.Cierre] = new(75, 200, 5),
		[Sesion.FueraDeHorario] = new(20, 60, 3)
	};

	// PROVISIONAL (v3, 2.4): cada familia tiene su tabla; punto de partida = NQ × su factor (ES 2,5; MES 1,25; YM 0,4;
	// MYM 0,2), igual que la de MNQ (NQ × 0,5). CALIBRAR con los CSV del propio instrumento.
	public Dictionary<Sesion, UmbralSesion> Es { get; } = Escalada(2.5m);
	public Dictionary<Sesion, UmbralSesion> Mes { get; } = Escalada(1.25m);
	public Dictionary<Sesion, UmbralSesion> Ym { get; } = Escalada(0.4m);
	public Dictionary<Sesion, UmbralSesion> Mym { get; } = Escalada(0.2m);

	private static Dictionary<Sesion, UmbralSesion> Escalada(decimal factor) => new TablaUmbralesBase().Nq.ToDictionary(
		kv => kv.Key, kv => new UmbralSesion(Math.Max(1, Math.Round(kv.Value.MinEjecutado * factor)), Math.Max(1, Math.Round(kv.Value.AltaProbabilidad * factor)), kv.Value.MinRecargas));

	public UmbralSesion De(Sesion s, bool mnq) => (mnq ? Mnq : Nq)[s];

	/// <summary>La tabla de la familia del instrumento (Auto/desconocido = NQ).</summary>
	public UmbralSesion De(Sesion s, PerfilInstrumento r) => (r switch
	{
		PerfilInstrumento.MNQ => Mnq,
		PerfilInstrumento.ES => Es,
		PerfilInstrumento.MES => Mes,
		PerfilInstrumento.YM => Ym,
		PerfilInstrumento.MYM => Mym,
		_ => Nq
	})[s];

	/// <summary>Solo para escalar las tablas provisionales desde los valores iniciales de NQ.</summary>
	private sealed class TablaUmbralesBase
	{
		public Dictionary<Sesion, UmbralSesion> Nq { get; } = new()
		{
			[Sesion.Asia] = new(40, 120, 3),
			[Sesion.Londres] = new(80, 250, 4),
			[Sesion.Premercado] = new(100, 300, 4),
			[Sesion.AperturaNY] = new(150, 400, 5),
			[Sesion.Mediodia] = new(100, 300, 4),
			[Sesion.Cierre] = new(150, 400, 5),
			[Sesion.FueraDeHorario] = new(40, 120, 3)
		};
	}
}

/// <summary>Umbrales en contratos de los trackers que dependen del instrumento (v3, 2.4): uno por familia.</summary>
public sealed class ContratosTracker
{
	public int MinLevelVolumeContracts { get; set; } = 15;
	public int MinVisibleClipContracts { get; set; } = 1;
	public int MaxVisibleClipContracts { get; set; } = 50;
	public int MinCascadeContractsRth { get; set; } = 100;
	public int MinCascadeContractsEth { get; set; } = 50;
	public int MinSweepContracts { get; set; } = 50;

	/// <summary>NQ = los valores de siempre; el resto PROVISIONAL (NQ × el factor de su familia; MNQ con su recomendación previa).</summary>
	public static ContratosTracker PorDefecto(PerfilInstrumento r)
	{
		var factor = r switch { PerfilInstrumento.MNQ => 2m, PerfilInstrumento.ES => 2.5m, PerfilInstrumento.MES => 1.25m, PerfilInstrumento.YM => 0.4m, PerfilInstrumento.MYM => 0.2m, _ => 1m };
		int E(int v) => Math.Max(1, (int)Math.Round(v * factor));
		return r is PerfilInstrumento.NQ or PerfilInstrumento.Auto
			? new ContratosTracker()
			: new ContratosTracker
			{
				MinLevelVolumeContracts = E(15), MinVisibleClipContracts = 1, MaxVisibleClipContracts = E(50), MinCascadeContractsRth = E(100),
				MinCascadeContractsEth = E(50), MinSweepContracts = E(50)
			};
	}
}

/// <summary>Los umbrales en contratos de cada familia (nunca se comparten).</summary>
public sealed class ContratosPorInstrumento
{
	public ContratosTracker NQ { get; } = ContratosTracker.PorDefecto(PerfilInstrumento.NQ);
	public ContratosTracker MNQ { get; } = ContratosTracker.PorDefecto(PerfilInstrumento.MNQ);
	public ContratosTracker ES { get; } = ContratosTracker.PorDefecto(PerfilInstrumento.ES);
	public ContratosTracker MES { get; } = ContratosTracker.PorDefecto(PerfilInstrumento.MES);
	public ContratosTracker YM { get; } = ContratosTracker.PorDefecto(PerfilInstrumento.YM);
	public ContratosTracker MYM { get; } = ContratosTracker.PorDefecto(PerfilInstrumento.MYM);

	public ContratosTracker De(PerfilInstrumento r) => r switch
	{
		PerfilInstrumento.MNQ => MNQ,
		PerfilInstrumento.ES => ES,
		PerfilInstrumento.MES => MES,
		PerfilInstrumento.YM => YM,
		PerfilInstrumento.MYM => MYM,
		_ => NQ
	};
}

#endregion

#region Utilidades

public static class PrecioMath
{
	public static decimal Redondear(decimal precio, decimal tick) => tick > 0 ? Math.Round(precio / tick, MidpointRounding.AwayFromZero) * tick : precio;

	public static long Tick(decimal precio, decimal tick) => (long)Math.Round(precio / tick, MidpointRounding.AwayFromZero);
}

public static class BusquedaTiempo
{
	/// <summary>Última posición con tiempo &lt;= t (búsqueda binaria; los tiempos están en orden). -1 si no hay.</summary>
	public static int UltimaHasta(Func<int, DateTime> tiempo, int total, DateTime t)
	{
		int bajo = 0, alto = total;

		while (bajo < alto)
		{
			var medio = (bajo + alto) / 2;

			if (tiempo(medio) <= t)
				bajo = medio + 1;
			else
				alto = medio;
		}

		return bajo - 1;
	}
}

#endregion
