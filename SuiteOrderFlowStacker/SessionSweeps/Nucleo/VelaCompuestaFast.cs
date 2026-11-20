namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Franjas horarias del Indicador de Vela Compuesta (FAST) (hora de Nueva York).</summary>
public enum FranjaHoraria
{
	Asia,
	Londres,
	Premercado,
	NyApertura,
	NyManana,
	NyMediodia,
	NyTarde
}

public static class Franjas
{
	public static readonly FranjaHoraria[] Todas = Enum.GetValues<FranjaHoraria>();

	/// <summary>Asia 18:00-02:00, Londres 02:00-08:00, Premercado 08:00-09:30, NY apertura 09:30-10:30, NY mañana 10:30-12:00, NY mediodía 12:00-14:00, NY tarde 14:00-18:00 (ET).</summary>
	public static FranjaHoraria De(DateTime utc)
	{
		var h = Zonas.AEt(utc).TimeOfDay;

		if (h >= new TimeSpan(18, 0, 0) || h < new TimeSpan(2, 0, 0))
			return FranjaHoraria.Asia;

		if (h < new TimeSpan(8, 0, 0))
			return FranjaHoraria.Londres;

		if (h < new TimeSpan(9, 30, 0))
			return FranjaHoraria.Premercado;

		if (h < new TimeSpan(10, 30, 0))
			return FranjaHoraria.NyApertura;

		if (h < new TimeSpan(12, 0, 0))
			return FranjaHoraria.NyManana;

		return h < new TimeSpan(14, 0, 0) ? FranjaHoraria.NyMediodia : FranjaHoraria.NyTarde;
	}

	public static string Texto(FranjaHoraria f) => f switch
	{
		FranjaHoraria.Asia => "Asia (18:00-02:00 NY)",
		FranjaHoraria.Londres => "Londres (02:00-08:00)",
		FranjaHoraria.Premercado => "Premercado (08:00-09:30)",
		FranjaHoraria.NyApertura => "NY apertura (09:30-10:30)",
		FranjaHoraria.NyManana => "NY mañana (10:30-12:00)",
		FranjaHoraria.NyMediodia => "NY mediodía (12:00-14:00)",
		_ => "NY tarde (14:00-18:00)"
	};
}

/// <summary>Un gatillo del Indicador de Vela Compuesta: COMPUESTA (varias velas volumétricas) o VOLUMETRICA (una sola).</summary>
public sealed record SenalVolumetrica(long Id, DateTime Utc, string Tipo, int Direccion, decimal Precio, decimal Bajo, decimal Alto, string Razones);

public sealed class ParametrosVelaCompuesta
{
	/// <summary>Contratos por vela volumétrica en cada franja (el "margen base" es 2,300).</summary>
	public Dictionary<FranjaHoraria, int> Contratos { get; } = Franjas.Todas.ToDictionary(f => f, _ => 2300);

	public bool GatilloComp { get; set; } = true;
	public bool GatilloVolumetrico { get; set; } = true;
	public bool MostrarFvg { get; set; } = true;

	/// <summary>Velas volumétricas que forman la vela compuesta.</summary>
	public int CompBarras { get; set; } = 4;

	public decimal DeltaPctMin { get; set; } = 0.25m;
	public decimal RangoMult { get; set; } = 1.5m;
	public decimal RapidaMult { get; set; } = 0.5m;

	/// <summary>Velas de 1 minuto para el sugerido (el documento: las últimas 20).</summary>
	public int VelasSugerido { get; set; } = 20;
}

/// <summary>
/// v5 Indicador de Vela Compuesta (FAST): velas volumétricas de N contratos (por franja horaria; 2,300 de base), con dos
/// gatillos: GATILLO COMP (la vela compuesta de las últimas CompBarras velas: POC y área de valor en el tercio del
/// extremo, delta que vira a favor en la última y cierre en el tercio contrario = arranque) y GATILLO VELAS VOLUMÉTRICAS
/// (una sola vela de desplazamiento: rango, delta y rapidez respecto a sus medianas, cierre en el tercio a favor). Calcula
/// los FVG de las velas de 1 minuto y el umbral sugerido con el promedio de volumen de las últimas 20 velas. Ligero: solo
/// guarda las velas recientes.
/// </summary>
public sealed class MotorVelaCompuesta
{
	private const int MaximoVelas = 3000;

	private readonly object _sync = new();
	private readonly HashSet<DateTime> _minutosConTicks = new();
	private readonly ParametrosV2 _fvg = new();
	private int _ultimaComp = -100;
	private long _siguiente = 1;

	public MotorVelaCompuesta(decimal tick)
	{
		Tick = tick;
		Barras = new MotorBarrasVolumen { Tick = tick, ContratosPorBarra = 2300 };
		Barras.AlCerrar = BarraCerrada;
	}

	public decimal Tick { get; }
	public ParametrosVelaCompuesta P { get; } = new();
	public MotorBarrasVolumen Barras { get; }
	public List<Vela> Velas { get; } = new();
	public MotorFvgV2 Fvg { get; } = new();
	public List<SenalVolumetrica> Senales { get; } = new();

	/// <summary>Cada gatillo nuevo (la carcasa lo publica en el bus para el Sweep Pro).</summary>
	public Action<SenalVolumetrica>? AlSenal { get; set; }

	public T Leer<T>(Func<MotorVelaCompuesta, T> f)
	{
		lock (_sync)
			return f(this);
	}

	public int UmbralDe(DateTime utc) => Math.Max(100, P.Contratos[Franjas.De(utc)]);

	/// <summary>
	/// Una vela de 1 minuto cerrada. Si en ese minuto no llegaron trades en vivo, sus trades se aproximan del footprint.
	/// 'conBarras' = false (días anteriores, carga rápida): solo cuenta para el sugerido y los FVG.
	/// </summary>
	public void Vela(VelaEntrada e, decimal tick, bool conBarras = true)
	{
		lock (_sync)
		{
			if (Velas.Count > 0 && e.InicioUtc <= Velas[^1].InicioUtc)
				return;

			var v = new Vela
			{
				Indice = Velas.Count, InicioUtc = e.InicioUtc, FinUtc = e.InicioUtc.AddMinutes(1), Open = e.Open, High = e.High, Low = e.Low, Close = e.Close,
				Volume = e.Volume, Delta = e.Delta
			};

			if (e.Niveles.Count > 0)
			{
				v.Niveles = new SortedDictionary<long, (double Bid, double Ask)>();

				foreach (var n in e.Niveles)
				{
					var t = (long)Math.Round(n.Precio / tick);
					var p = v.Niveles.TryGetValue(t, out var x) ? x : default;
					v.Niveles[t] = (p.Bid + (double)n.Bid, p.Ask + (double)n.Ask);
				}
			}

			Velas.Add(v);

			if (conBarras && !_minutosConTicks.Contains(v.InicioUtc))
			{
				Barras.ContratosPorBarra = UmbralDe(v.InicioUtc);

				foreach (var (utc, precio, vol, compra) in MotorBarrasVolumen.Aproximar(v, tick))
					Barras.Agregar(utc, precio, vol, compra, aprox: true);
			}

			v.Niveles = null;
			Fvg.Agregar(Velas, 1, _fvg, tick);

			if (Velas.Count > MaximoVelas)
			{
				Velas.RemoveRange(0, Velas.Count - MaximoVelas);

				for (var i = 0; i < Velas.Count; i++)
					Velas[i].Indice = i;
			}

			if (_minutosConTicks.Count > 2000)
				_minutosConTicks.RemoveWhere(m => m < v.InicioUtc.AddHours(-3));
		}
	}

	/// <summary>Un trade en vivo.</summary>
	public void Trade(DateTime utc, decimal precio, decimal volumen, bool? compra)
	{
		lock (_sync)
		{
			_minutosConTicks.Add(new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc));
			Barras.ContratosPorBarra = UmbralDe(utc);
			Barras.Agregar(utc, precio, volumen, compra);
		}
	}

	/// <summary>Umbral sugerido: promedio de volumen de las últimas 20 velas de 1 minuto (de la franja, si se da), en centenas.</summary>
	public int Sugerido(FranjaHoraria? franja = null)
	{
		lock (_sync)
		{
			var velas = Velas.Where(v => franja is null || Franjas.De(v.InicioUtc) == franja).TakeLast(Math.Max(1, P.VelasSugerido)).ToList();

			if (velas.Count == 0)
				return 0;

			var promedio = velas.Average(v => v.Volume);
			return Math.Max(100, (int)(Math.Round(promedio / 100m) * 100));
		}
	}

	private void BarraCerrada(BarraVolumen b)
	{
		var barras = Barras.Barras;
		var i = barras.Count - 1;

		if (i < 0)
			return;

		if (P.GatilloVolumetrico && Volumetrica(b, i) is { } v)
			Emitir(v);

		if (P.GatilloComp && i - _ultimaComp >= P.CompBarras && Compuesta(i) is { } c)
		{
			_ultimaComp = i;
			Emitir(c);
		}
	}

	private void Emitir(SenalVolumetrica s)
	{
		Senales.Add(s);

		if (Senales.Count > 500)
			Senales.RemoveAt(0);

		AlSenal?.Invoke(s);
	}

	/// <summary>Una vela volumétrica de desplazamiento: rango, delta y rapidez respecto a sus medianas; cierre en el tercio a favor.</summary>
	private SenalVolumetrica? Volumetrica(BarraVolumen b, int i)
	{
		var (duracion, rango) = Barras.Medianas(i);
		var signo = Math.Sign(b.Delta);

		if (signo == 0 || Math.Sign(b.Close - b.Open) != signo)
			return null;

		var grande = b.RangoTicks(Tick) >= (double)P.RangoMult * rango;
		var delta = b.DeltaPct * signo >= P.DeltaPctMin;
		var rapida = b.DuracionSec <= (double)P.RapidaMult * Math.Max(0.001, duracion);

		if (!grande || !delta || !rapida || !b.CierreEnTercio(signo))
			return null;

		return new SenalVolumetrica(_siguiente++, b.FinUtc, "VOLUMETRICA", signo, b.Close, b.Low, b.High,
			$"rango {b.RangoTicks(Tick)} ticks (≥ {P.RangoMult:0.#}× mediana) · delta {b.DeltaPct:P0} · {b.DuracionSec:0.#} s · cierre en el tercio a favor");
	}

	/// <summary>
	/// Vela compuesta de las últimas CompBarras velas volumétricas: alcista con POC y área de valor en el tercio inferior
	/// (absorción abajo), delta de las anteriores en contra y la última a favor (≥ DeltaPctMin), y cierre en el tercio
	/// superior; bajista, al revés. Con stacked imbalance a favor, se anota.
	/// </summary>
	private SenalVolumetrica? Compuesta(int i)
	{
		var n = Math.Max(2, P.CompBarras);

		if (i + 1 < n)
			return null;

		var c = SessionSweeps.Nucleo.Compuesta.Construir(Barras.Barras, i - n + 1, i, Tick, 0.70m);

		if (c is null)
			return null;

		var ultima = Barras.Barras[i];
		var previas = c.Delta - ultima.Delta;

		foreach (var signo in new[] { 1, -1 })
		{
			if (!c.PocYVaEnTercio(-signo) || signo * previas > 0 || ultima.DeltaPct * signo < P.DeltaPctMin || !c.EnTercio(ultima.Close, signo))
				continue;

			var stacked = c.Stacked(3, 5, 3).Any(z => z.Compra == signo > 0);
			return new SenalVolumetrica(_siguiente++, ultima.FinUtc, "COMPUESTA", signo, ultima.Close, c.Low, c.High,
				$"{n} velas · POC {c.Poc} en el tercio {(signo > 0 ? "inferior" : "superior")} · delta vira a {(signo > 0 ? "compra" : "venta")} " +
				$"({ultima.DeltaPct:P0}) · cierre en el tercio {(signo > 0 ? "superior" : "inferior")}{(stacked ? " · stacked imbalance a favor" : "")}");
		}

		return null;
	}
}
