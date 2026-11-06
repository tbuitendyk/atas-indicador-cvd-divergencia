namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Barra virtual de VolumePerBar contratos (4.1): OHLC, volumen, delta, Bid/Ask por precio, duración y rango.</summary>
public sealed class BarraVolumen
{
	public int Indice;
	public DateTime InicioUtc, FinUtc;
	public decimal Open, High, Low, Close, Volumen, Delta;

	/// <summary>Tick (precio / tamaño de tick) → (Bid = ventas agresivas, Ask = compras agresivas).</summary>
	public readonly SortedDictionary<long, (decimal Bid, decimal Ask)> Niveles = new();

	/// <summary>Reconstruida del footprint de 1 minuto (sin trades tick a tick).</summary>
	public bool Aprox;

	public double DuracionSec => Math.Max(0.001, (FinUtc - InicioUtc).TotalSeconds);

	public decimal DeltaPct => Volumen > 0 ? Delta / Volumen : 0;

	public int RangoTicks(decimal tick) => tick > 0 ? (int)Math.Round((High - Low) / tick) : 0;

	/// <summary>Cierre en el tercio superior (signo +1) o inferior (signo -1) de la barra.</summary>
	public bool CierreEnTercio(int signo)
	{
		var rango = High - Low;

		if (rango <= 0)
			return false;

		return signo > 0 ? Close >= High - rango / 3 : Close <= Low + rango / 3;
	}
}

/// <summary>
/// VirtualVolumeBarEngine (4.1-4.3): barras de VolumePerBar contratos a partir de los trades (un trade que excede la
/// barra se reparte entre las dos), con las medianas de duración, rango y velocidad de las barras RTH.
/// </summary>
public sealed class MotorBarrasVolumen
{
	private readonly List<BarraVolumen> _barras = new();
	private readonly Dictionary<long, decimal> _tramos5s = new();
	private BarraVolumen? _actual;
	private DateTime? _diaTramos;
	private double? _medianaVelocidad;

	public decimal Tick { get; set; } = 0.25m;

	public int ContratosPorBarra { get; set; } = 500;

	public int MedianLookbackBars { get; set; } = 30;

	/// <summary>Se llama al cerrar cada barra.</summary>
	public Action<BarraVolumen>? AlCerrar { get; set; }

	public IReadOnlyList<BarraVolumen> Barras => _barras;

	public BarraVolumen? Actual => _actual;

	public DateTime? UltimoTradeUtc { get; private set; }

	public void Reiniciar()
	{
		_barras.Clear();
		_tramos5s.Clear();
		(_actual, _diaTramos, _medianaVelocidad, UltimoTradeUtc) = (null, null, null, null);
	}

	/// <summary>Un trade (real o reconstruido). compra = agresor comprador (null = desconocido: mitad y mitad).</summary>
	public void Agregar(DateTime utc, decimal precio, decimal volumen, bool? compra, bool aprox = false)
	{
		if (volumen <= 0 || precio <= 0)
			return;

		UltimoTradeUtc = utc;
		Velocidad(utc, volumen);
		var resto = volumen;

		while (resto > 0)
		{
			_actual ??= Nueva(utc, precio, aprox);
			var cabe = Math.Max(0, ContratosPorBarra - _actual.Volumen);
			var parte = Math.Min(resto, cabe);

			if (parte > 0)
				Sumar(_actual, utc, precio, parte, compra, aprox);

			resto -= parte;

			if (_actual.Volumen >= ContratosPorBarra)
				Cerrar();
		}
	}

	private BarraVolumen Nueva(DateTime utc, decimal precio, bool aprox) =>
		new() { Indice = _barras.Count, InicioUtc = utc, FinUtc = utc, Open = precio, High = precio, Low = precio, Close = precio, Aprox = aprox };

	private void Sumar(BarraVolumen b, DateTime utc, decimal precio, decimal vol, bool? compra, bool aprox)
	{
		b.FinUtc = utc;
		b.High = Math.Max(b.High, precio);
		b.Low = Math.Min(b.Low, precio);
		b.Close = precio;
		b.Volumen += vol;
		b.Aprox |= aprox;

		var (bid, ask) = compra switch { true => (0m, vol), false => (vol, 0m), _ => (vol / 2, vol / 2) };
		b.Delta += ask - bid;
		var t = (long)Math.Round(precio / Tick, MidpointRounding.AwayFromZero);
		var previo = b.Niveles.TryGetValue(t, out var p) ? p : (0, 0);
		b.Niveles[t] = (previo.Item1 + bid, previo.Item2 + ask);
	}

	private void Cerrar()
	{
		if (_actual is null)
			return;

		_barras.Add(_actual);
		var cerrada = _actual;
		_actual = null;

		if (_barras.Count > 20000)
		{
			_barras.RemoveRange(0, 5000);

			for (var i = 0; i < _barras.Count; i++)
				_barras[i].Indice = i;
		}

		AlCerrar?.Invoke(cerrada);
	}

	/// <summary>Velocidad: contratos por tramo de 5 s de la sesión RTH del día (para BurstRatio).</summary>
	private void Velocidad(DateTime utc, decimal volumen)
	{
		if (!TiempoV2.EsRth(utc))
			return;

		var dia = Zonas.AEt(utc).Date;

		if (_diaTramos != dia)
		{
			_tramos5s.Clear();
			_diaTramos = dia;
		}

		var tramo = utc.Ticks / TimeSpan.FromSeconds(5).Ticks;
		_tramos5s[tramo] = _tramos5s.GetValueOrDefault(tramo) + volumen;
		_medianaVelocidad = null;
	}

	/// <summary>Mediana de los contratos por tramo de 5 s de la sesión RTH (null si aún no hay tramos).</summary>
	public double? MedianaVelocidad5s
	{
		get
		{
			if (_medianaVelocidad is null && _tramos5s.Count >= 5)
				_medianaVelocidad = Mediana(_tramos5s.Values.Select(v => (double)v));

			return _medianaVelocidad;
		}
	}

	/// <summary>Contratos en los tramos de 5 s entre dos horas (el pico de una ráfaga).</summary>
	public decimal PicoVelocidad5s(DateTime desde, DateTime hasta)
	{
		var a = desde.Ticks / TimeSpan.FromSeconds(5).Ticks;
		var b = hasta.Ticks / TimeSpan.FromSeconds(5).Ticks;
		decimal pico = 0;

		for (var t = a; t <= b; t++)
			pico = Math.Max(pico, _tramos5s.GetValueOrDefault(t));

		return pico;
	}

	/// <summary>Medianas de DurationSec y RangeTicks de las últimas MedianLookbackBars barras RTH anteriores a 'antesDe' (4.3).</summary>
	public (double Duracion, double Rango) Medianas(int antesDe)
	{
		var rth = new List<BarraVolumen>();

		for (var i = Math.Min(antesDe, _barras.Count) - 1; i >= 0 && rth.Count < Math.Max(3, MedianLookbackBars); i--)
		{
			if (TiempoV2.EsRth(_barras[i].InicioUtc))
				rth.Add(_barras[i]);
		}

		// Sin barras RTH aún (madrugada): las últimas que haya.
		if (rth.Count < 3)
			rth = _barras.Take(Math.Min(antesDe, _barras.Count)).TakeLast(Math.Max(3, MedianLookbackBars)).ToList();

		if (rth.Count == 0)
			return (60, 8);

		return (Mediana(rth.Select(b => b.DuracionSec)), Mediana(rth.Select(b => (double)b.RangoTicks(Tick))));
	}

	public static double Mediana(IEnumerable<double> valores)
	{
		var v = valores.OrderBy(x => x).ToArray();

		if (v.Length == 0)
			return 0;

		return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
	}

	/// <summary>Primera barra (cerrada o la actual) cuyo intervalo contiene la hora dada; -1 si no hay.</summary>
	public int BarraEn(DateTime utc)
	{
		var i = BusquedaBarras.UltimaHasta(_barras, utc);
		return i;
	}

	/// <summary>
	/// APROX (4.2): sin trades tick a tick, una vela de 1 minuto con footprint se convierte en trades sintéticos que
	/// recorren apertura → extremo más cercano → el otro extremo → cierre, repartidos en el minuto. El volumen de cada
	/// precio se pone al pasar por él la primera vez; sin footprint, se reparte por igual.
	/// </summary>
	public static IEnumerable<(DateTime Utc, decimal Precio, decimal Volumen, bool? Compra)> Aproximar(Vela v, decimal tick)
	{
		if (tick <= 0 || v.Volume <= 0)
			yield break;

		var arribaPrimero = v.High - v.Open <= v.Open - v.Low;
		var camino = new List<decimal>();

		void Tramo(decimal desde, decimal hasta)
		{
			var paso = hasta >= desde ? tick : -tick;

			for (var p = desde; paso > 0 ? p <= hasta : p >= hasta; p += paso)
			{
				if (camino.Count == 0 || camino[^1] != p)
					camino.Add(p);
			}
		}

		var (e1, e2) = arribaPrimero ? (v.High, v.Low) : (v.Low, v.High);
		Tramo(v.Open, e1);
		Tramo(e1, e2);
		Tramo(e2, v.Close);

		var usados = new HashSet<long>();
		var paso2 = TimeSpan.FromTicks(Math.Max(1, (v.FinUtc - v.InicioUtc).Ticks / Math.Max(1, camino.Count)));
		var porPrecio = v.Niveles is { Count: > 0 } ? null : (decimal?)(v.Volume / Math.Max(1, (int)Math.Round((v.High - v.Low) / tick) + 1));

		for (var i = 0; i < camino.Count; i++)
		{
			var t = (long)Math.Round(camino[i] / tick, MidpointRounding.AwayFromZero);

			if (!usados.Add(t))
				continue;

			var utc = v.InicioUtc + paso2 * i;

			if (porPrecio is { } igual)
			{
				var compra = v.Delta >= 0;
				yield return (utc, camino[i], igual, compra);
				continue;
			}

			if (v.Niveles!.TryGetValue(t, out var n))
			{
				if (n.Ask > 0)
					yield return (utc, camino[i], (decimal)n.Ask, true);

				if (n.Bid > 0)
					yield return (utc, camino[i], (decimal)n.Bid, false);
			}
		}
	}
}

public static class BusquedaBarras
{
	/// <summary>Última barra que empieza en o antes de 'utc' (búsqueda binaria). -1 si no hay.</summary>
	public static int UltimaHasta(IReadOnlyList<BarraVolumen> barras, DateTime utc)
	{
		int bajo = 0, alto = barras.Count;

		while (bajo < alto)
		{
			var medio = (bajo + alto) / 2;

			if (barras[medio].InicioUtc <= utc)
				bajo = medio + 1;
			else
				alto = medio;
		}

		return bajo - 1;
	}
}
