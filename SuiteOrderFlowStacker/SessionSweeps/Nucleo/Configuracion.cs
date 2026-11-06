namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Zonas horarias con su horario de verano (nombre IANA, o el de Windows si no lo encuentra).</summary>
public static class Zonas
{
	private static readonly Dictionary<ZonaHoraria, TimeZoneInfo> Cache = new();

	public static TimeZoneInfo De(ZonaHoraria zona)
	{
		lock (Cache)
		{
			if (Cache.TryGetValue(zona, out var tz))
				return tz;

			tz = zona switch
			{
				ZonaHoraria.Londres => Buscar("Europe/London", "GMT Standard Time"),
				ZonaHoraria.Tokio => Buscar("Asia/Tokyo", "Tokyo Standard Time"),
				ZonaHoraria.CiudadDeMexico => Buscar("America/Mexico_City", "Central Standard Time (Mexico)"),
				ZonaHoraria.Utc => TimeZoneInfo.Utc,
				_ => Buscar("America/New_York", "Eastern Standard Time")
			};

			Cache[zona] = tz;
			return tz;
		}
	}

	/// <summary>Busca por nombre IANA o de Windows (los dos funcionan en Windows con .NET moderno).</summary>
	public static TimeZoneInfo Buscar(string iana, string windows)
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

	/// <summary>Zona por nombre libre (ajuste del correo): primero tal cual, después como IANA/Windows conocidos.</summary>
	public static TimeZoneInfo PorNombre(string? nombre)
	{
		if (string.IsNullOrWhiteSpace(nombre))
			return De(ZonaHoraria.NuevaYork);

		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(nombre.Trim());
		}
		catch (Exception)
		{
			return nombre.Trim() switch
			{
				"Central Standard Time (Mexico)" => De(ZonaHoraria.CiudadDeMexico),
				"GMT Standard Time" => De(ZonaHoraria.Londres),
				"Tokyo Standard Time" => De(ZonaHoraria.Tokio),
				_ => De(ZonaHoraria.NuevaYork)
			};
		}
	}

	public static DateTime ALocal(DateTime utc, ZonaHoraria zona) =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), De(zona));

	public static DateTime AEt(DateTime utc) => ALocal(utc, ZonaHoraria.NuevaYork);

	/// <summary>Hora local de una zona → UTC (si la hora no existe por el cambio de horario, se adelanta una hora).</summary>
	public static DateTime AUtc(DateTime local, ZonaHoraria zona)
	{
		var tz = De(zona);
		local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

		if (tz.IsInvalidTime(local))
			local = local.AddHours(1);

		return TimeZoneInfo.ConvertTimeToUtc(local, tz);
	}

	/// <summary>Día operativo de futuros: de 18:00 a 17:00 ET (la fecha es la del cierre).</summary>
	public static DateTime DiaOperativo(DateTime utc) => AEt(utc).AddHours(6).Date;

	/// <summary>Lunes de la semana operativa (la semana de Globex empieza el domingo a las 18:00 ET).</summary>
	public static DateTime SemanaOperativa(DateTime utc)
	{
		var dia = DiaOperativo(utc);
		return dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
	}
}

/// <summary>Una de las 5 sesiones: horario por modo, zona y opciones de dibujo.</summary>
public sealed class DefinicionSesion
{
	public DefinicionSesion(string id, TimeSpan completaInicio, TimeSpan completaFin, TimeSpan killInicio, TimeSpan killFin)
	{
		Id = id;
		Nombre = id;
		(CompletaInicio, CompletaFin, KillInicio, KillFin) = (completaInicio, completaFin, killInicio, killFin);
		(CustomInicio, CustomFin) = (completaInicio, completaFin);
	}

	/// <summary>Identificador fijo: "Asia", "Londres", "Premercado", "NY AM", "NY PM".</summary>
	public string Id { get; }

	public string Nombre { get; set; }

	public bool Activa { get; set; } = true;

	public ModoSesion Modo { get; set; } = ModoSesion.Completa;

	public TimeSpan CompletaInicio { get; }

	public TimeSpan CompletaFin { get; }

	public TimeSpan KillInicio { get; }

	public TimeSpan KillFin { get; }

	public TimeSpan CustomInicio { get; set; }

	public TimeSpan CustomFin { get; set; }

	/// <summary>Zona de las horas personalizadas (los modos completa y Kill Zone están en ET).</summary>
	public ZonaHoraria Zona { get; set; } = ZonaHoraria.NuevaYork;

	public bool Sombrear { get; set; } = true;

	public bool ExtenderMaxMin { get; set; } = true;

	public bool ExtenderMedia { get; set; }

	public bool MostrarNombre { get; set; } = true;
}

/// <summary>Todos los parámetros del cálculo, con los valores por defecto del documento.</summary>
public sealed class ConfiguracionSweeps
{
	/// <summary>Parámetros de Session Sweeps Pro v2.0 (sección 15).</summary>
	public ParametrosV2 V2 { get; } = new();

	public const string Asia = "Asia", Londres = "Londres", Premercado = "Premercado", NyAm = "NY AM", NyPm = "NY PM";

	public decimal TamanoTick { get; set; } = 0.25m;

	/// <summary>Minutos por vela del gráfico (1 a 5). Lo detecta el indicador.</summary>
	public int MinutosGrafico { get; set; } = 1;

	// 1. Sesiones
	public List<DefinicionSesion> Sesiones { get; } = new()
	{
		new(Asia, new(18, 0, 0), new(2, 0, 0), new(20, 0, 0), new(0, 0, 0)),
		new(Londres, new(2, 0, 0), new(8, 30, 0), new(2, 0, 0), new(5, 0, 0)),
		new(Premercado, new(8, 30, 0), new(9, 30, 0), new(8, 30, 0), new(9, 30, 0)),
		new(NyAm, new(9, 30, 0), new(12, 0, 0), new(9, 30, 0), new(11, 0, 0)),
		new(NyPm, new(13, 30, 0), new(16, 0, 0), new(13, 30, 0), new(16, 0, 0))
	};

	public PresetHorarios Preset { get; set; } = PresetHorarios.Estandar;
	public TimeSpan NYOpen { get; set; } = new(9, 30, 0);
	public int PremarketMinutesBeforeOpen { get; set; } = 60;

	/// <summary>Ajuste manual en horas por si el horario de verano no coincidiera (se suma a la hora local).</summary>
	public double AjusteManualHoras { get; set; }

	public bool ExtendUntilSwept { get; set; } = true;

	// 3. Niveles de liquidez
	public bool NivelesSesion { get; set; } = true;
	public bool NivelesPdhPdl { get; set; } = true;
	public bool NivelesDiasAnteriores { get; set; } = true;
	public int DiasAnteriores { get; set; } = 3;
	public bool NivelesSemanaAnterior { get; set; }
	public bool NivelesIguales { get; set; }
	public int EqualTolTicks { get; set; } = 3;
	public int EqualPivotLen { get; set; } = 5;
	public VentanaBarridos SweepDetectionWindow { get; set; } = VentanaBarridos.NY;
	public TimeSpan VentanaInicio { get; set; } = new(8, 30, 0);
	public TimeSpan VentanaFin { get; set; } = new(16, 0, 0);
	public bool PineCompatibilityMode { get; set; }

	// 4. Barridos (Pine)
	public bool BuysideZones { get; set; } = true;
	public double BuysideMargin { get; set; } = 1.3;
	public bool SellsideZones { get; set; } = true;
	public double SellsideMargin { get; set; } = 1.3;
	public int SweepMarginLength { get; set; } = 5;
	public bool DetectOncePerSession { get; set; }
	public bool HideFakeSweepZones { get; set; }

	// 5. Order Flow
	public bool OrderFlowValidation { get; set; } = true;
	public decimal MinVolBeyond { get; set; } = 150;
	public double AbsVolMult { get; set; } = 2.0;
	public int AbsMaxTicks { get; set; } = 8;

	// 6. Contexto H1
	public int H1PivotLen { get; set; } = 3;
	public bool RequireH1Alignment { get; set; }

	// 7. Proyección
	public int MinTargetTicks { get; set; } = 20;
	public int StopBufferTicks { get; set; } = 8;
	public ContratoValor Contrato { get; set; } = ContratoValor.NQ;
	public int AdrDias { get; set; } = 10;

	// 8. FVG / IFVG
	public bool Fvg { get; set; } = true;
	public double FVGWidthFilter { get; set; } = 1.0;
	public int PostSweepFVGWindow { get; set; } = 30;

	// 9. Modelo A
	public bool ModeloA { get; set; } = true;
	public bool ACondicionLvn { get; set; } = true;
	public bool ACondicionPinBar { get; set; } = true;
	public bool ACondicionPoc { get; set; } = true;
	public bool ACondicionVa { get; set; } = true;
	public bool ACondicionCierre { get; set; } = true;
	public int LVNTouchTolTicks { get; set; } = 4;
	public int TriggerLookahead { get; set; } = 5;

	// 10. Modelo B
	public bool ModeloB { get; set; } = true;
	public double ImpulseATRMult { get; set; } = 1.5;
	public int MinConfluencesB { get; set; } = 3;
	public decimal FinishedAuctionMaxContracts { get; set; } = 2;
	public double DeltaFlipMult { get; set; } = 1.5;
	public int MaxBarsModelB { get; set; } = 90;

	// 11. LVN / perfil
	public RangoPerfilLvn LVNProfileRange { get; set; } = RangoPerfilLvn.DiaAnteriorRthMasOvernight;
	public int LvnDias { get; set; } = 3;
	public DateTime LvnDesdeEt { get; set; } = DateTime.Today.AddDays(-3);
	public DateTime LvnHastaEt { get; set; } = DateTime.Today.AddDays(1);
	public int RowSizeTicks { get; set; } = 1;
	public int MaxRows { get; set; } = 300;
	public double LvnSigma { get; set; } = 1.5;
	public double LvnProminencia { get; set; } = 20;
	public double LvnMaxPctPoc { get; set; } = 35;
	public int LvnSeparacionTicks { get; set; } = 8;
	public int MaxLvn { get; set; } = 8;
	public List<decimal> LvnManuales { get; set; } = new();

	// 12. Alertas
	public int TriggerCountdownSec { get; set; } = 15;

	// 14. MSS
	public bool Mss { get; set; }
	public int MSSLength { get; set; } = 8;

	// 15. Visual (límites como el Pine)
	public int MaxSweepBoxes { get; set; } = 500;
	public int MaxLines { get; set; } = 500;

	public decimal ValorPunto => Contrato == ContratoValor.MNQ ? 2m : 20m;

	public decimal Ticks(int n) => n * TamanoTick;

	public DefinicionSesion Sesion(string id) => Sesiones.First(s => s.Id == id);

	/// <summary>
	/// Horario efectivo de una sesión: (inicio, fin, zona). Con el preset PineOriginal, los del código Pine (Asia
	/// 09-18 Tokio, Londres 08-17 Londres, NY AM 08-13 y NY PM 13-19 ET; sin premercado). El premercado empieza
	/// PremarketMinutesBeforeOpen minutos antes de NYOpen.
	/// </summary>
	public (TimeSpan Inicio, TimeSpan Fin, ZonaHoraria Zona, bool Activa) Horario(DefinicionSesion s)
	{
		if (Preset == PresetHorarios.PineOriginal)
		{
			return s.Id switch
			{
				Asia => (new(9, 0, 0), new(18, 0, 0), ZonaHoraria.Tokio, s.Activa),
				Londres => (new(8, 0, 0), new(17, 0, 0), ZonaHoraria.Londres, s.Activa),
				NyAm => (new(8, 0, 0), new(13, 0, 0), ZonaHoraria.NuevaYork, s.Activa),
				NyPm => (new(13, 0, 0), new(19, 0, 0), ZonaHoraria.NuevaYork, s.Activa),
				_ => (TimeSpan.Zero, TimeSpan.Zero, ZonaHoraria.NuevaYork, false)
			};
		}

		if (s.Modo == ModoSesion.Custom)
			return (s.CustomInicio, s.CustomFin, s.Zona, s.Activa);

		if (s.Id == Premercado)
			return (NYOpen - TimeSpan.FromMinutes(PremarketMinutesBeforeOpen), NYOpen, ZonaHoraria.NuevaYork, s.Activa);

		return s.Modo == ModoSesion.KillZone
			? (s.KillInicio, s.KillFin, ZonaHoraria.NuevaYork, s.Activa)
			: (s.CompletaInicio, s.CompletaFin, ZonaHoraria.NuevaYork, s.Activa);
	}

	public ConfiguracionSweeps Copiar()
	{
		var c = (ConfiguracionSweeps)MemberwiseClone();
		c.LvnManuales = LvnManuales.ToList();
		return c;
	}
}
