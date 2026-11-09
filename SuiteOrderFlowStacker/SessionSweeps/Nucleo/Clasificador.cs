namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using OrderFlowSuite.Comun;

/// <summary>Estados del clasificador (7.5). Migración del NQ: Falso → Ruptura; Dudoso → Indeterminado; Real → Trampa B/A/Premium.</summary>
public enum EstadoBarrido
{
	Pendiente,
	SinLiquidez,
	/// <summary>BARRIDO DE LIQUIDEZ REAL (v3): toma de liquidez y recuperación confirmadas, >= ScoreRealMin.</summary>
	Real,

	/// <summary>BARRIDO DE LIQUIDEZ REAL PREMIUM: >= ScorePremiumMin y todas las puertas.</summary>
	Premium,
	Ruptura,
	RupturaFallida,
	Fallido,
	Indeterminado
}

public enum Tendencia
{
	Rango,
	Alcista,
	Bajista
}

public static class TextosV2
{
	public static string Texto(this EstadoBarrido e) => e switch
	{
		EstadoBarrido.Pendiente => "PENDIENTE",
		EstadoBarrido.SinLiquidez => "SIN LIQ",
		EstadoBarrido.Real => "BARRIDO REAL",
		EstadoBarrido.Premium => "BARRIDO REAL PREMIUM",
		EstadoBarrido.Ruptura => "RUPTURA / ACEPTACIÓN",
		EstadoBarrido.RupturaFallida => "RUPTURA FALLIDA",
		EstadoBarrido.Fallido => "FALLIDO",
		_ => "INDETERMINADO"
	};

	/// <summary>Barrido de liquidez real (REAL o PREMIUM): el que arma el Modelo A.</summary>
	public static bool EsReal(this EstadoBarrido e) => e is EstadoBarrido.Real or EstadoBarrido.Premium;

	public static int Rango(this EstadoBarrido e) => e switch { EstadoBarrido.Real => 1, EstadoBarrido.Premium => 2, _ => 0 };

	/// <summary>Estados guardados por la v2 en el CSV (TRAMPA B/A/PREMIUM) → v3.</summary>
	public static EstadoBarrido? DesdeTexto(string s) => s switch
	{
		"TrampaPremium" => EstadoBarrido.Premium,
		"TrampaA" => EstadoBarrido.Real,
		"TrampaB" => EstadoBarrido.Indeterminado,
		_ => Enum.TryParse<EstadoBarrido>(s, out var e) ? e : null
	};

	public static string Texto(this Tendencia t) => t switch { Tendencia.Alcista => "ALCISTA", Tendencia.Bajista => "BAJISTA", _ => "RANGO" };

	public static int Signo(this Tendencia t) => t switch { Tendencia.Alcista => 1, Tendencia.Bajista => -1, _ => 0 };
}

/// <summary>Un punto del desglose: bloque (A-E o P de penalización), texto y puntos (ya con el peso de la fuente).</summary>
public sealed record PuntoDesglose(char Bloque, string Texto, decimal Puntos);

/// <summary>Lo que el clasificador consulta de los indicadores de la suite (en ATAS, el bus; en las pruebas, datos a mano).</summary>
public interface IFuentesSuite
{
	EstadoFuente Estado(FuenteSuite fuente);

	List<T> Consultar<T>(DateTime desde, DateTime hasta, Func<T, bool>? filtro = null) where T : EventoSuite;
}

/// <summary>Fuentes vacías: todas NO CARGADO.</summary>
public sealed class SinFuentes : IFuentesSuite
{
	public static readonly SinFuentes Instancia = new();

	public EstadoFuente Estado(FuenteSuite fuente) => EstadoFuente.NoCargado;

	public List<T> Consultar<T>(DateTime desde, DateTime hasta, Func<T, bool>? filtro = null) where T : EventoSuite => new();
}

/// <summary>Las fuentes de la suite leídas del bus del instrumento.</summary>
public sealed class FuentesBus : IFuentesSuite
{
	private readonly CanalSuite _canal;
	private readonly Func<DateTime> _ahora;

	public FuentesBus(string instrumento, Func<DateTime> ahoraUtc)
	{
		_canal = SuiteEventBus.Para(instrumento);
		_ahora = ahoraUtc;
	}

	public EstadoFuente Estado(FuenteSuite fuente) => _canal.Estado(fuente, _ahora());

	/// <summary>Eventos deduplicados (3.2) y sin los de fuentes con otra versión del bus.</summary>
	public List<T> Consultar<T>(DateTime desde, DateTime hasta, Func<T, bool>? filtro = null) where T : EventoSuite
	{
		var ahora = _ahora();
		return SuiteEventBus.Deduplicar(_canal.Consultar(desde, hasta, filtro).Where(e => _canal.Estado(e.Fuente, ahora) != EstadoFuente.VersionDistinta));
	}
}

/// <summary>Evaluación de un barrido de un nivel (7): datos medidos, bloques A-E, puertas, estado y transiciones.</summary>
public sealed class EvaluacionBarrido
{
	public long Id;
	public NivelLiquidez Nivel = null!;

	/// <summary>+1 = barrido de un máximo (buyside, el precio sube); el giro esperado es -Signo.</summary>
	public int Signo;

	public decimal L, Extremo;

	/// <summary>Extremo de la pierna desde T0, que sigue moviéndose después de congelar (para MOD4: zonas y retroceso).</summary>
	public decimal ExtremoPierna;
	public DateTime T0Utc, ExtremoUtc;
	public int BarraT0, BarraExtremo = -1;

	// Medidas (columnas del CSV).
	public int PenetrationTicks;
	public decimal VolBeyond, VolBeyondRel, DeltaBeyond, AggressorShare, PreT0Delta;
	public bool PreT0DeltaAprox;
	public decimal BurstRatio;
	public string StopsEvento = "", IcebergEstado = "", IcebergModo = "", AbsorptionEvento = "";
	public decimal? IcebergPrecio;
	public int F2Bars, CompositeBars, ReclaimBars = -1, F3RangeTicks;
	public decimal CompositePoc, F3DeltaPct;
	public double F3DurationSec;
	public bool FinishedAuction, UnfinishedAuction, StackedAgainst, StackedNewDir, DeltaFlip, F3Rapida, F3Lenta;
	public int RecoveryTicks;
	public DateTime? Recovery1mUtc, Recovery5mUtc;
	public decimal ScoreA, ScoreB, ScoreC, ScoreD, ScoreE, Penalties, Total;
	public string PremiumGates = "0000000";
	public bool ConflictoPine, Noticia, Aprox;
	public string SourcesLoaded = "";
	public List<PuntoDesglose> Desglose = new();
	public List<string> Faltan = new();

	// Estado y congelación (7.7).
	public EstadoBarrido Estado = EstadoBarrido.Pendiente;
	public bool Congelada;
	public DateTime? TimeConfirmUtc;
	public int BarraConfirmacion = -1;
	public bool UpgradePremium;
	public List<string> Transiciones = new();

	/// <summary>Nace de otra evaluación (FALLIDO → candidato a RUPTURA; RUPTURA FALLIDA → nueva evaluación como TRAMPA).</summary>
	public long? Padre;

	public string Origen = "";

	/// <summary>Filas del CSV del día que mandan sobre esta evaluación al recargar (pendientes de aplicar, en orden).</summary>
	public List<RegistroCsvBarrido>? FilasCsv;

	/// <summary>Su estado viene del CSV (recarga del historial).</summary>
	public bool Restaurada;

	/// <summary>Inicio de la ventana de evaluación (la de T0; una hija tras RUPTURA FALLIDA empieza una nueva).</summary>
	public int? VentanaDesdeBarra;

	public DateTime? VentanaDesdeUtc;

	public int SignoGiro => -Signo;

	public bool Provisional => !Congelada;

	public string Nombre => $"{(Signo > 0 ? "MAX" : "MIN")} {Nivel.Origen}";
}

/// <summary>Lo que necesita el clasificador del resto del motor.</summary>
public sealed class ContextoClasificador
{
	public IReadOnlyList<BarraVolumen> Barras = Array.Empty<BarraVolumen>();
	public ParametrosV2 P = new();
	public decimal Tick = 0.25m;
	public int ContratosPorBarra = 500;
	public IFuentesSuite Fuentes = SinFuentes.Instancia;
	public DateTime Ahora;
	public Func<int, (double Duracion, double Rango)> Medianas = _ => (60, 8);
	public Func<DateTime, DateTime, decimal> PicoVelocidad = (_, _) => 0;
	public Func<double?> MedianaVelocidad = () => null;
	public Func<decimal?> MedianaVolBeyond = () => null;
	public Func<Tendencia> H1 = () => Tendencia.Rango, M15 = () => Tendencia.Rango;
	public Func<EvaluacionBarrido, bool> DivergenciaCvd = _ => false;
	public Func<DateTime, decimal?> Vwap = _ => null;

	/// <summary>Veredicto del Pine para el nivel: true = barrido (trampa), false = falso (aceptación), null = sin zona.</summary>
	public Func<EvaluacionBarrido, bool?> Pine = _ => null;

	public Func<EvaluacionBarrido, int> ToquesNivel = _ => 1;
}

/// <summary>
/// Clasificador de barridos (7): F1 nivel con liquidez → F2 toma de stops → F3 absorción/atrapados → F4 recuperación.
/// Puntuación 0-100 por bloques con topes (A 20, B 20, C 25, D 25, E 10), penalizaciones después de los topes, cada
/// hecho una sola vez (las fuentes de la suite se deduplican con el cálculo propio), estados con congelación.
/// La puntuación es un ranking de calidad, NO una probabilidad.
/// </summary>
public static class Clasificador
{
	private static readonly Dictionary<char, decimal> Topes = new() { ['A'] = 20, ['B'] = 20, ['C'] = 25, ['D'] = 25, ['E'] = 10 };

	/// <summary>Reevalúa una evaluación con las barras de volumen cerradas. Devuelve la transición, si la hubo.</summary>
	public static string? Evaluar(EvaluacionBarrido e, ContextoClasificador c)
	{
		var b = c.Barras;
		var ultima = b.Count - 1;

		if (ultima < e.BarraT0)
			return null;

		// Congelada: solo las transiciones permitidas (7.7).
		if (e.Congelada)
			return Transiciones(e, c);

		Medir(e, c);

		var barras = ultima - (e.VentanaDesdeBarra ?? e.BarraT0) + 1;
		var vencida = barras >= c.P.MaxEvaluationBars || (c.Ahora - (e.VentanaDesdeUtc ?? e.T0Utc)).TotalSeconds >= c.P.MaxEvaluationSec;
		var rafaga = e.Desglose.Any(x => x.Texto.StartsWith("Ráfaga", StringComparison.Ordinal));
		var sinLiq = e.PenetrationTicks <= 2 && e.ScoreB < 6 && !rafaga;
		var reclaim = e.ReclaimBars >= 0;
		var absorcion = e.ScoreC > 0;
		var arranque = e.F3Rapida || e.DeltaFlip;
		var tomaStops = e.ScoreB >= 6 || rafaga;

		EstadoBarrido? nuevo = null;

		if (reclaim && sinLiq)
		{
			nuevo = EstadoBarrido.SinLiquidez;
		}
		else if (reclaim && absorcion && arranque && tomaStops && e.Total >= c.P.ScoreRealMin)
		{
			nuevo = e.Total >= c.P.ScorePremiumMin && e.PremiumGates.All(x => x == '1') ? EstadoBarrido.Premium : EstadoBarrido.Real;
		}
		else if (!reclaim && CondicionesRuptura(e, c) >= c.P.RuptureMinConditions)
		{
			nuevo = EstadoBarrido.Ruptura;
		}
		else if (vencida)
		{
			nuevo = sinLiq ? EstadoBarrido.SinLiquidez : EstadoBarrido.Indeterminado;
		}

		Faltantes(e, reclaim, absorcion, arranque, tomaStops, c);

		if (nuevo is not { } estado)
			return null;

		Congelar(e, estado, c);
		return $"{estado.Texto()}";
	}

	private static void Congelar(EvaluacionBarrido e, EstadoBarrido estado, ContextoClasificador c)
	{
		e.Estado = estado;
		e.Congelada = true;
		e.TimeConfirmUtc = c.Barras[^1].FinUtc;
		e.BarraConfirmacion = c.Barras.Count - 1;
		e.Transiciones.Add($"{TiempoV2.HoraEt(e.TimeConfirmUtc.Value)} {estado.Texto()} ({e.Total:0}/100)");
	}

	/// <summary>Transiciones permitidas después de congelar: Trampa → Fallido; Ruptura → RupturaFallida; Trampa B/A → Premium.</summary>
	private static string? Transiciones(EvaluacionBarrido e, ContextoClasificador c)
	{
		var b = c.Barras;
		var u = b[^1];
		var desde = b.Count - 1 - e.BarraConfirmacion;
		var tick = c.Tick;

		if (e.Estado.EsReal())
		{
			// FALLIDO: cierre más allá del extremo + StopBufferTicks.
			if (e.Signo * (u.Close - (e.Extremo + e.Signo * c.P.StopBufferTicks * tick)) > 0)
			{
				e.Estado = EstadoBarrido.Fallido;
				e.Transiciones.Add($"{TiempoV2.HoraEt(u.FinUtc)} FALLIDO (cierre {Precio(u.Close, tick)} más allá del extremo)");
				return "FALLIDO";
			}

			// UPGRADE PREMIUM dentro de PremiumUpgradeMaxBars barras.
			if (e.Estado != EstadoBarrido.Premium && desde <= c.P.PremiumUpgradeMaxBars)
			{
				var congelado = (e.Total, e.Desglose, e.PremiumGates);
				Medir(e, c);

				if (e.Total >= c.P.ScorePremiumMin && e.PremiumGates.All(x => x == '1'))
				{
					e.Estado = EstadoBarrido.Premium;
					e.UpgradePremium = true;
					e.Transiciones.Add($"{TiempoV2.HoraEt(u.FinUtc)} UPGRADE PREMIUM ({e.Total:0}/100)");
					return "UPGRADE PREMIUM";
				}

				// Sin upgrade, lo congelado no cambia.
				(e.Total, e.Desglose, e.PremiumGates) = congelado;
			}
		}

		// RUPTURA FALLIDA: cierre de vuelta más allá de L por FailedBreakTicks dentro de FailedBreakBars.
		if (e.Estado == EstadoBarrido.Ruptura && desde <= c.P.FailedBreakBars && e.Signo * (e.L - u.Close) >= c.P.FailedBreakTicks * tick)
		{
			e.Estado = EstadoBarrido.RupturaFallida;
			e.Transiciones.Add($"{TiempoV2.HoraEt(u.FinUtc)} RUPTURA FALLIDA (cierre {Precio(u.Close, tick)})");
			return "RUPTURA FALLIDA";
		}

		return null;
	}

	/// <summary>Mide todo y calcula los bloques A-E, las penalizaciones, el total y las puertas premium.</summary>
	public static void Medir(EvaluacionBarrido e, ContextoClasificador c)
	{
		var b = c.Barras;
		var p = c.P;
		var tick = c.Tick;
		var s = e.Signo;
		var ultima = b.Count - 1;
		var med = c.Medianas(e.BarraT0);
		var d = new List<PuntoDesglose>();
		var fuentes = new List<string>();

		void Punto(char bloque, string texto, decimal puntos)
		{
			if (puntos != 0)
				d.Add(new PuntoDesglose(bloque, texto, puntos));
		}

		e.PenetrationTicks = (int)Math.Round(Math.Abs(e.Extremo - e.L) / tick);

		// Extremo: la barra que lo marcó.
		e.BarraExtremo = e.BarraT0;

		for (var i = e.BarraT0; i <= ultima; i++)
		{
			if (s > 0 ? b[i].High >= e.Extremo : b[i].Low <= e.Extremo)
			{
				e.BarraExtremo = i;
				break;
			}
		}

		// Cierre de vuelta: la primera barra que cierra del lado original con ReclaimBufferTicks.
		e.ReclaimBars = -1;

		for (var i = e.BarraT0; i <= ultima; i++)
		{
			if (s * ((e.L - s * p.ReclaimBufferTicks * tick) - b[i].Close) >= 0)
			{
				e.ReclaimBars = i - e.BarraT0;
				break;
			}
		}

		var finMedida = e.ReclaimBars >= 0 ? e.BarraT0 + e.ReclaimBars : ultima;

		// Volumen y delta más allá del nivel (P3: VolBeyond relativo).
		decimal vol = 0, delta = 0, agresion = 0;

		for (var i = e.BarraT0; i <= finMedida; i++)
		{
			foreach (var (t, n) in b[i].Niveles)
			{
				if (s * (t * tick - e.L) <= 0)
					continue;

				vol += n.Bid + n.Ask;
				delta += n.Ask - n.Bid;
				agresion += s > 0 ? n.Ask : n.Bid;
			}
		}

		e.VolBeyond = vol;
		e.DeltaBeyond = delta;
		e.AggressorShare = vol > 0 ? agresion / vol : 0;
		var medianaBeyond = c.MedianaVolBeyond();
		e.VolBeyondRel = medianaBeyond is > 0 ? vol / medianaBeyond.Value : vol / Math.Max(1, c.ContratosPorBarra);

		// Fases y vela compuesta del barrido (4.4 a): desde la barra de T0 hasta el cierre de vuelta o F3, CompositeMaxBars como máximo.
		var fases = DetectorFases.Detectar(b, e.BarraT0, ultima, -s, e.Extremo, p, med, tick, retroceso: false);
		var finComp = Math.Min(ultima, e.BarraT0 + Math.Max(1, p.CompositeMaxBars) - 1);

		if (e.ReclaimBars >= 0)
			finComp = Math.Min(finComp, e.BarraT0 + e.ReclaimBars);

		if (fases.F3Indice is { } f3i)
			finComp = Math.Min(finComp, f3i);

		var comp = Compuesta.Construir(b, e.BarraT0, finComp, tick, p.ValueAreaPct);
		e.CompositeBars = comp?.Barras ?? 0;
		e.CompositePoc = comp?.Poc ?? 0;
		e.Aprox = comp?.Aprox ?? false;
		e.F2Bars = fases.F2Barras;
		(e.F3Rapida, e.F3Lenta, e.F3DurationSec, e.F3RangeTicks, e.F3DeltaPct) = (fases.F3, fases.F3Lenta, fases.F3DuracionSec, fases.F3RangoTicks, fases.F3DeltaPct);

		// ============================ A NIVEL (tope 20) ============================
		var grupo = e.Nivel.Grupo;
		var origen = e.Nivel.Origen;

		if (grupo is "dia" or "semana")
			Punto('A', $"Nivel externo ({origen})", 10);
		else if (origen is "Asia" or "Londres" or "Premercado" || grupo == "pine-londres")
			Punto('A', $"Nivel de sesión ({origen})", 6);
		else
			Punto('A', $"Swing interno ({origen})", 2);

		var toques = c.ToquesNivel(e);

		if (grupo == "igual" || toques >= Math.Max(2, p.EqualLevelMinTouches))
			Punto('A', $"Máximos/mínimos iguales ({Math.Max(toques, p.EqualLevelMinTouches)} toques)", 5);

		if (toques <= 1 && (e.T0Utc - e.Nivel.CreadoUtc).TotalMinutes >= p.LevelMinAgeMin)
			Punto('A', $"Nivel intacto con {(e.T0Utc - e.Nivel.CreadoUtc).TotalMinutes:0} min de antigüedad", 3);

		var killzone = TiempoV2.EnKillzone(e.T0Utc, p);

		if (killzone)
			Punto('A', "Dentro de la killzone de NY", 2);

		// ============================ B TOMA DE STOPS (tope 20) ============================
		var medVel = c.MedianaVelocidad();
		var pico = c.PicoVelocidad(e.T0Utc, (e.ExtremoUtc > e.T0Utc ? e.ExtremoUtc : e.T0Utc).AddSeconds(5));
		e.BurstRatio = medVel is > 0 ? pico / (decimal)medVel.Value : 0;
		var ventanaSuite = TimeSpan.FromSeconds(p.SuitePreSec);
		var margen = p.SuitePriceMarginTicks * tick;
		var (pMin, pMax) = s > 0 ? (e.L - margen, e.Extremo + margen) : (e.Extremo - margen, e.L + margen);
		var hasta = b[finComp].FinUtc;
		var cargadas = new List<FuenteSuite>();

		decimal Factor(FuenteSuite f, PesoFuente peso)
		{
			if (c.Fuentes.Estado(f) == EstadoFuente.NoCargado)
				return 0;

			cargadas.Add(f);
			return peso.Factor;
		}

		var fSweeps = Factor(FuenteSuite.Sweeps, p.Sweeps);
		var sweepsSuite = fSweeps > 0
			? c.Fuentes.Consultar<SweepTrackerEvent>(e.T0Utc - ventanaSuite, hasta, x => x.Compra == (s > 0) && Dentro(x.Hasta, pMin, pMax))
			: new List<SweepTrackerEvent>();

		if (e.BurstRatio >= p.BurstSpeedMult)
			Punto('B', $"Ráfaga propia {e.BurstRatio:0.0}x la velocidad mediana", 6);
		else if (sweepsSuite.Count > 0)
			Punto('B', $"Ráfaga del Sweeps Tracker ({sweepsSuite[0].Niveles} niveles, {sweepsSuite[0].Contratos:0}c)", 6 * fSweeps);

		if (DetectorFases.EsRapida(b[e.BarraT0], p, med))
			Punto('B', "Barra del cruce rápida", 4);

		if (e.AggressorShare >= p.AggressorShareMin)
			Punto('B', $"Agresor {e.AggressorShare:P0} más allá del nivel", 4);

		var fStops = Factor(FuenteSuite.Stops, p.Stops);
		var ventanaStops = TimeSpan.FromSeconds(p.StopsEventWindowSec);
		var stops = fStops > 0
			? c.Fuentes.Consultar<StopsEvent>(e.T0Utc - ventanaStops, e.T0Utc + ventanaStops, x => x.Compra == (s > 0) && Dentro(x.Extremo, pMin, pMax))
			: new List<StopsEvent>();

		e.StopsEvento = stops.Count > 0 ? $"{stops[0].Contratos:0}c {stops[0].Zona} {stops[0].Estado}".Trim() : "";

		if (stops.Count > 0)
			Punto('B', $"Stops Tracker: {e.StopsEvento}", 4 * fStops);

		if (e.PenetrationTicks >= p.MinPenetrationTicks && e.PenetrationTicks <= p.MaxSweepTicks)
			Punto('B', $"Penetración {e.PenetrationTicks} ticks", 2);

		// ============================ C ABSORCIÓN (tope 25, sobre la compuesta) ============================
		var divergencia = e.VolBeyond > 0 && s * e.DeltaBeyond / e.VolBeyond < p.DivergenceDeltaPctMin;

		if (divergencia)
		{
			Punto('C', $"Divergencia de delta más allá del nivel ({(e.VolBeyond > 0 ? e.DeltaBeyond / e.VolBeyond : 0):P0})", 6);

			if (e.VolBeyondRel < p.VolBeyondRelMult)
				Punto('C', $"Volumen relativo bajo ({e.VolBeyondRel:0.00}x)", 3);
		}

		// Iceberg del lado contrario (absorbe al agresor) AGUANTANDO cerca del extremo; si no, Absorption Master.
		var fIce = Factor(FuenteSuite.Iceberg, p.Iceberg);
		var distIce = p.IcebergMaxDistTicks * tick;
		var icebergs = fIce > 0
			? c.Fuentes.Consultar<IcebergEvent>(e.T0Utc - ventanaSuite, hasta, x => x.Compra == (s < 0) && Math.Abs(x.Precio - e.Extremo) <= distIce)
			: new List<IcebergEvent>();
		var aguantando = icebergs.Where(x => x.Estado == "AGUANTANDO").OrderByDescending(x => x.Mbo).ThenByDescending(x => x.Ejecutado).FirstOrDefault();
		// Un iceberg ROTO cuenta en todo el camino del barrido (en una ruptura suele estar en el nivel, no en el extremo).
		var roto = fIce > 0
			? c.Fuentes.Consultar<IcebergEvent>(e.T0Utc - ventanaSuite, hasta, x => x.Compra == (s < 0) && x.Estado == "ROTO" && Dentro(x.Precio, pMin, pMax)).FirstOrDefault()
			: null;
		var mejor = aguantando ?? icebergs.OrderByDescending(x => x.Ejecutado).FirstOrDefault();
		e.IcebergEstado = roto is not null && aguantando is null ? "ROTO" : mejor?.Estado ?? "";
		e.IcebergPrecio = (aguantando ?? roto ?? mejor)?.Precio;
		e.IcebergModo = mejor is null ? "" : mejor.Mbo ? "MBO" : "INFERIDO";

		if (aguantando is not null)
			Punto('C', $"Iceberg AGUANTANDO {(aguantando.Mbo ? "MBO" : "INFERIDO")} en {Precio(aguantando.Precio, tick)}", (aguantando.Mbo ? 6 : 3) * fIce);

		var fAbs = Factor(FuenteSuite.Absorption, p.Absorption);
		var absorciones = fAbs > 0
			? c.Fuentes.Consultar<AbsorptionEvent>(e.T0Utc - ventanaSuite, hasta, x => x.Compra == (s < 0) && Math.Abs(x.Precio - e.Extremo) <= distIce)
			: new List<AbsorptionEvent>();

		// 3.4 / T18: la misma absorción del Iceberg Tracker y del Absorption Master cuenta una vez.
		var absorcion = absorciones.FirstOrDefault(a => aguantando is null || !Duplicado(a.Precio, a.Utc, aguantando.Precio, aguantando.CambioUtc, p, tick));
		e.AbsorptionEvento = absorcion is null ? "" : $"{absorcion.Score}/100 {absorcion.Setup}";

		if (absorcion is not null)
			Punto('C', $"Absorption Master {absorcion.Score}/100 en {Precio(absorcion.Precio, tick)}", 3 * fAbs);

		if (fases.F2Valida && fases.F2EnExtremo)
			Punto('C', $"F2: {fases.F2Barras} barras de freno en el extremo", 4);

		if (comp is not null && comp.PocYVaEnTercio(s))
			Punto('C', "POC y VA de la compuesta en el tercio del extremo", 4);

		e.FinishedAuction = comp?.SubastaTerminada(s, p.FinishedAuctionMaxContracts) ?? false;
		e.UnfinishedAuction = comp is not null && !e.FinishedAuction;

		if (e.FinishedAuction)
			Punto('C', "Subasta terminada en el extremo", 3);

		var cerca = 4 * tick;
		var stackedContra = comp?.Stacked(p.ImbalanceRatio, p.ImbalanceMinVolume, p.StackedMinLevels).Any(z => z.Compra == (s < 0) && Cerca(z, e.Extremo, cerca)) ?? false;
		var fStk = Factor(FuenteSuite.Stacked, p.Stacked);

		if (!stackedContra && fStk > 0)
			stackedContra = c.Fuentes.Consultar<StackedEvent>(e.T0Utc - ventanaSuite, hasta, x => x.Compra == (s < 0) && x.Bajo - cerca <= e.Extremo && x.Alto + cerca >= e.Extremo).Count > 0;

		e.StackedAgainst = stackedContra;

		if (stackedContra)
			Punto('C', "Stacked imbalance contrario en el extremo", 2);

		// ============================ D RECUPERACIÓN (tope 25) ============================
		if (e.ReclaimBars >= 0 && e.ReclaimBars <= p.ReclaimMaxBars)
			Punto('D', $"Cierre de vuelta en {e.ReclaimBars} barras", 6);

		if (fases.F3)
			Punto('D', $"F3 rápida ({fases.F3RangoTicks} ticks, {fases.F3DuracionSec:0.0} s, delta {fases.F3DeltaPct:P0})", 6);

		decimal deltaIda = 0, deltaVuelta = 0;

		for (var i = e.BarraT0; i <= ultima; i++)
		{
			if (i <= e.BarraExtremo)
				deltaIda += b[i].Delta;
			else
				deltaVuelta += b[i].Delta;
		}

		var umbralGiro = p.DeltaFlipMin * c.ContratosPorBarra;
		e.DeltaFlip = s * deltaIda >= umbralGiro && -s * deltaVuelta >= umbralGiro;

		if (e.DeltaFlip)
			Punto('D', $"Giro de delta ({deltaIda:+0;-0} → {deltaVuelta:+0;-0})", 5);

		// 1.6: el stacked del extremo ya sumó en C; aquí solo cuenta una zona de la nueva dirección lejos del extremo.
		var despues = e.BarraExtremo + 1 <= ultima ? Compuesta.Construir(b, e.BarraExtremo + 1, ultima, tick, p.ValueAreaPct) : null;
		var stackedNueva = despues?.Stacked(p.ImbalanceRatio, p.ImbalanceMinVolume, p.StackedMinLevels).Any(z => z.Compra == (s < 0) && !Cerca(z, e.Extremo, cerca)) ?? false;

		if (!stackedNueva && fStk > 0 && e.BarraExtremo + 1 <= ultima)
		{
			stackedNueva = c.Fuentes.Consultar<StackedEvent>(b[e.BarraExtremo].FinUtc, b[ultima].FinUtc,
				x => x.Compra == (s < 0) && !(x.Bajo - cerca <= e.Extremo && x.Alto + cerca >= e.Extremo)).Count > 0;
		}

		e.StackedNewDir = stackedNueva;

		if (stackedNueva)
			Punto('D', "Stacked imbalance en la nueva dirección", 4);

		// Recuperación: RecoveryTicks desde el extremo sin volver a tocarlo.
		var tocado = false;
		var mejorRecuperacion = 0m;

		for (var i = e.BarraExtremo + 1; i <= ultima; i++)
		{
			tocado |= s > 0 ? b[i].High >= e.Extremo : b[i].Low <= e.Extremo;
			mejorRecuperacion = Math.Max(mejorRecuperacion, s > 0 ? e.Extremo - b[i].Low : b[i].High - e.Extremo);
		}

		e.RecoveryTicks = (int)Math.Round(mejorRecuperacion / tick);

		if (e.RecoveryTicks >= p.RecoveryTicks && !tocado)
			Punto('D', $"Recuperación {e.RecoveryTicks} ticks sin tocar el extremo", 4);

		// ============================ E CONTEXTO (tope 10) ============================
		var h1 = c.H1();
		var m15 = c.M15();
		var enContra15 = m15.Signo() == s;
		e.Noticia = TiempoV2.EnNoticia(e.T0Utc, p);

		if (!e.Noticia)
		{
			if (h1.Signo() != s && !enContra15)
				Punto('E', $"Sesgo 15m ({m15.Texto()}) y H1 ({h1.Texto()}) no en contra", 3);

			if (c.DivergenciaCvd(e))
				Punto('E', "Divergencia de CVD", 3);

			if (e.ReclaimBars >= 0 && c.Vwap(b[e.BarraT0 + e.ReclaimBars].FinUtc) is { } vwap && -s * (b[e.BarraT0 + e.ReclaimBars].Close - vwap) > 0)
				Punto('E', "Vuelta al otro lado de la VWAP", 2);

			var pine = c.Pine(e);

			if (pine == true)
				Punto('E', "El Pine coincide", 2);

			e.ConflictoPine = pine == false;
		}
		else
		{
			Punto('E', "NOTICIA: el contexto no suma y no hay alertas", 0);
		}

		// Topes por bloque y penalizaciones después de los topes.
		decimal Bloque(char x) => Math.Min(Topes[x], d.Where(y => y.Bloque == x).Sum(y => y.Puntos));

		(e.ScoreA, e.ScoreB, e.ScoreC, e.ScoreD, e.ScoreE) = (Bloque('A'), Bloque('B'), Bloque('C'), Bloque('D'), Bloque('E'));
		var penal = 0m;

		if (e.UnfinishedAuction)
		{
			d.Add(new PuntoDesglose('P', "Subasta sin terminar en el extremo", -4));
			penal += 4;
		}

		if (fases.F3Lenta && !fases.F3)
		{
			d.Add(new PuntoDesglose('P', "Barra de arranque lenta", -3));
			penal += 3;
		}

		e.Penalties = penal;
		e.Total = Math.Clamp(e.ScoreA + e.ScoreB + e.ScoreC + e.ScoreD + e.ScoreE - penal, 0, 100);
		e.Desglose = d;
		e.SourcesLoaded = string.Join(",", cargadas.Distinct());

		// Puertas premium (7.6).
		var puertas = new[]
		{
			e.ScoreA >= p.PremiumMinLevelScore,
			e.BurstRatio >= p.BurstSpeedMult || sweepsSuite.Count > 0 || stops.Count > 0,
			aguantando is not null || (fases.F2Barras >= 3 && comp is not null && comp.EnTercio(comp.Poc, s)),
			e.FinishedAuction && !e.UnfinishedAuction,
			e.ReclaimBars >= 0 && e.ReclaimBars <= p.PremiumMaxReclaimBars && fases.F3 && e.DeltaFlip,
			e.RecoveryTicks >= p.RecoveryTicks && !tocado,
			killzone && !e.Noticia && !enContra15
		};

		e.PremiumGates = new string(puertas.Select(x => x ? '1' : '0').ToArray());
	}

	/// <summary>Condiciones de RUPTURA (de 5): aceptación, POC más allá, delta sostenido, sin absorción, retest que aguanta.</summary>
	public static int CondicionesRuptura(EvaluacionBarrido e, ContextoClasificador c)
	{
		var b = c.Barras;
		var s = e.Signo;
		var ultima = b.Count - 1;
		var n = 0;

		var seguidos = 0;

		for (var i = ultima; i >= e.BarraT0 && s * (b[i].Close - e.L) > 0; i--)
			seguidos++;

		if (seguidos >= c.P.AcceptanceCloses)
			n++;

		if (e.CompositePoc != 0 && s * (e.CompositePoc - e.L) > 0)
			n++;

		if (e.VolBeyond > 0 && s * e.DeltaBeyond / e.VolBeyond >= c.P.DivergenceDeltaPctMin && !e.DeltaFlip)
			n++;

		if (e.ScoreC < c.P.RuptureMaxAbsorptionScore || e.IcebergEstado == "ROTO")
			n++;

		// Retest: una barra posterior vuelve al nivel (a ReclaimBufferTicks) y cierra otra vez más allá.
		for (var i = e.BarraT0 + 1; i <= ultima; i++)
		{
			var toca = s > 0 ? b[i].Low <= e.L + c.P.ReclaimBufferTicks * c.Tick : b[i].High >= e.L - c.P.ReclaimBufferTicks * c.Tick;

			if (toca && s * (b[i].Close - e.L) > 0)
			{
				n++;
				break;
			}
		}

		return n;
	}

	/// <summary>Qué falta para confirmar (panel en modo Detalle, PROVISIONAL).</summary>
	private static void Faltantes(EvaluacionBarrido e, bool reclaim, bool absorcion, bool arranque, bool tomaStops, ContextoClasificador c)
	{
		e.Faltan.Clear();

		if (!tomaStops)
			e.Faltan.Add("falta toma de stops");

		if (!absorcion)
			e.Faltan.Add("falta absorción");

		if (!reclaim)
			e.Faltan.Add("falta cierre de vuelta");

		if (!arranque)
			e.Faltan.Add("falta F3 o giro de delta");

		if (e.Total < c.P.ScoreRealMin)
			e.Faltan.Add($"puntuación {e.Total:0} < {c.P.ScoreRealMin}");
	}

	private static bool Dentro(decimal precio, decimal min, decimal max) => precio >= min && precio <= max;

	private static bool Cerca(StackedZona z, decimal precio, decimal margen) => z.Bajo - margen <= precio && z.Alto + margen >= precio;

	public static bool Duplicado(decimal p1, DateTime t1, decimal p2, DateTime t2, ParametrosV2 p, decimal tick) =>
		Math.Abs(p1 - p2) <= p.DedupTicks * tick && Math.Abs((t1 - t2).TotalSeconds) <= p.DedupSec;

	public static string Precio(decimal p, decimal tick)
	{
		var texto = tick.ToString(CultureInfo.InvariantCulture).TrimEnd('0');
		var decimales = texto.Contains('.') ? texto.Length - texto.IndexOf('.') - 1 : 0;
		return p.ToString("F" + Math.Max(0, decimales), CultureInfo.InvariantCulture);
	}
}
