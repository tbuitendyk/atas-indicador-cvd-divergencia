namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Zona de stacked imbalance contiguo dentro de una vela (compuesta o de volumen).</summary>
public readonly record struct StackedZona(decimal Bajo, decimal Alto, bool Compra, int Niveles);

/// <summary>
/// Vela compuesta (4.4): varias barras de volumen sumadas por precio. POC, área de valor, tercios, subasta en el
/// extremo, stacked imbalance contiguo y delta acumulado barra a barra.
/// </summary>
public sealed class VelaCompuesta
{
	public int Desde, Hasta;
	public DateTime InicioUtc, FinUtc;
	public decimal Open, High, Low, Close, Volumen, Delta, Poc, VaBajo, VaAlto, Tick;
	public readonly SortedDictionary<long, (decimal Bid, decimal Ask)> Niveles = new();
	public readonly List<decimal> DeltaAcumulado = new();
	public bool Aprox;

	public int Barras => Hasta - Desde + 1;

	public decimal DeltaPct => Volumen > 0 ? Delta / Volumen : 0;

	/// <summary>¿El precio está en el tercio del lado 'signo' (+1 = superior, -1 = inferior)?</summary>
	public bool EnTercio(decimal precio, int signo)
	{
		var rango = High - Low;

		if (rango <= 0)
			return true;

		return signo > 0 ? precio >= High - rango / 3 : precio <= Low + rango / 3;
	}

	/// <summary>POC y centro del área de valor en el tercio del extremo ('signo' = lado del extremo: +1 máximo, -1 mínimo).</summary>
	public bool PocYVaEnTercio(int signo) => EnTercio(Poc, signo) && EnTercio((VaBajo + VaAlto) / 2, signo);

	public (decimal Bid, decimal Ask) En(decimal precio) =>
		Niveles.TryGetValue((long)Math.Round(precio / Tick, MidpointRounding.AwayFromZero), out var n) ? n : (0, 0);

	/// <summary>Subasta terminada en el extremo: el lado más pequeño del precio extremo tiene como mucho 'maximo' contratos.</summary>
	public bool SubastaTerminada(int signoExtremo, int maximo)
	{
		var (bid, ask) = En(signoExtremo > 0 ? High : Low);
		return Math.Min(bid, ask) <= maximo;
	}

	/// <summary>
	/// Stacked imbalance contiguo (P11): niveles diagonales con imbalance del mismo lado en ticks consecutivos y ordenados
	/// (t, t+1, t+2...). Compra: Ask(t) contra Bid(t-1); venta: Bid(t) contra Ask(t+1). Un lado débil a 0 cuenta como 1.
	/// </summary>
	public List<StackedZona> Stacked(decimal ratio, decimal minVolumen, int minNiveles) => StackedDe(Niveles, Tick, ratio, minVolumen, minNiveles);

	public static List<StackedZona> StackedDe(SortedDictionary<long, (decimal Bid, decimal Ask)> niveles, decimal tick, decimal ratio, decimal minVolumen, int minNiveles)
	{
		var r = new List<StackedZona>();

		if (niveles.Count == 0)
			return r;

		(decimal Bid, decimal Ask) N(long t) => niveles.TryGetValue(t, out var n) ? n : (0, 0);

		bool Es(decimal fuerte, decimal debil) => fuerte >= minVolumen && fuerte >= Math.Max(1, ratio) * Math.Max(1, debil);

		foreach (var compra in new[] { true, false })
		{
			var ticks = new List<long>();

			for (var t = niveles.Keys.First(); t <= niveles.Keys.Last(); t++)
			{
				if (compra ? Es(N(t).Ask, N(t - 1).Bid) : Es(N(t).Bid, N(t + 1).Ask))
					ticks.Add(t);
			}

			for (var i = 0; i < ticks.Count;)
			{
				var j = i;

				while (j + 1 < ticks.Count && ticks[j + 1] == ticks[j] + 1)
					j++;

				if (j - i + 1 >= Math.Max(2, minNiveles))
					r.Add(new StackedZona(ticks[i] * tick, ticks[j] * tick, compra, j - i + 1));

				i = j + 1;
			}
		}

		return r;
	}
}

public static class Compuesta
{
	/// <summary>Suma las barras [desde, hasta] (índices de la lista) en una vela compuesta. Null si el intervalo está vacío.</summary>
	public static VelaCompuesta? Construir(IReadOnlyList<BarraVolumen> barras, int desde, int hasta, decimal tick, decimal vaPct)
	{
		desde = Math.Max(0, desde);
		hasta = Math.Min(barras.Count - 1, hasta);

		if (desde > hasta || tick <= 0)
			return null;

		var c = new VelaCompuesta
		{
			Desde = desde,
			Hasta = hasta,
			Tick = tick,
			InicioUtc = barras[desde].InicioUtc,
			FinUtc = barras[hasta].FinUtc,
			Open = barras[desde].Open,
			Close = barras[hasta].Close,
			High = decimal.MinValue,
			Low = decimal.MaxValue
		};

		decimal acumulado = 0;

		for (var i = desde; i <= hasta; i++)
		{
			var b = barras[i];
			c.High = Math.Max(c.High, b.High);
			c.Low = Math.Min(c.Low, b.Low);
			c.Volumen += b.Volumen;
			c.Delta += b.Delta;
			c.Aprox |= b.Aprox;
			acumulado += b.Delta;
			c.DeltaAcumulado.Add(acumulado);

			foreach (var (t, n) in b.Niveles)
			{
				var previo = c.Niveles.TryGetValue(t, out var p) ? p : (0, 0);
				c.Niveles[t] = (previo.Item1 + n.Bid, previo.Item2 + n.Ask);
			}
		}

		(c.Poc, c.VaBajo, c.VaAlto) = PocYVa(c.Niveles, tick, vaPct);
		return c;
	}

	/// <summary>POC (empate: el del medio) y área de valor (desde el POC, el vecino mayor; empate: los dos) hasta vaPct.</summary>
	public static (decimal Poc, decimal VaBajo, decimal VaAlto) PocYVa(SortedDictionary<long, (decimal Bid, decimal Ask)> niveles, decimal tick, decimal vaPct)
	{
		if (niveles.Count == 0)
			return (0, 0, 0);

		var ticks = niveles.Keys.ToArray();
		var vol = ticks.Select(t => niveles[t].Bid + niveles[t].Ask).ToArray();
		var total = vol.Sum();
		var maximo = vol.Max();
		var empatados = Enumerable.Range(0, vol.Length).Where(i => vol[i] == maximo).ToList();
		var poc = empatados[(empatados.Count - 1) / 2];
		int bajo = poc, alto = poc;
		var acumulado = vol[poc];
		var objetivo = total * Math.Clamp(vaPct, 0.1m, 1m);

		while (acumulado < objetivo && (bajo > 0 || alto < vol.Length - 1))
		{
			var arriba = alto < vol.Length - 1 ? vol[alto + 1] : -1;
			var abajo = bajo > 0 ? vol[bajo - 1] : -1;

			if (arriba > abajo)
			{
				acumulado += vol[++alto];
			}
			else if (abajo > arriba)
			{
				acumulado += vol[--bajo];
			}
			else
			{
				acumulado += vol[++alto];
				acumulado += vol[--bajo];
			}
		}

		return (ticks[poc] * tick, ticks[bajo] * tick, ticks[alto] * tick);
	}
}

/// <summary>Fases (4.5): F1 llegada (agresión hacia la zona), F2 freno (compresión en el extremo), F3 arranque.</summary>
public sealed class Fases
{
	public int F1Barras, F2Barras;

	/// <summary>Las barras de F2 están pegadas al extremo (a CompressionMaxTicks o menos).</summary>
	public bool F2EnExtremo;

	/// <summary>F2 con un número de barras dentro de [F2MinBars, F2MaxBars] (o F2MaxBarsRetrace en un retroceso).</summary>
	public bool F2Valida;

	/// <summary>Índice (en la lista de barras) de la barra F3, si la hay.</summary>
	public int? F3Indice;

	public double F3DuracionSec;
	public int F3RangoTicks;
	public decimal F3DeltaPct;

	/// <summary>Hubo una barra de arranque (rango y delta) pero lenta: penalización en el bloque D.</summary>
	public bool F3Lenta;

	public bool F3 => F3Indice is not null;
}

public static class DetectorFases
{
	/// <summary>
	/// Fases entre las barras [desde, hasta]. 'signoNuevo' = dirección esperada después del giro (+1 alcista). 'extremo' =
	/// precio extremo de la llegada (máximo si la llegada fue alcista).
	/// </summary>
	public static Fases Detectar(IReadOnlyList<BarraVolumen> b, int desde, int hasta, int signoNuevo, decimal extremo, ParametrosV2 p,
		(double Duracion, double Rango) mediana, decimal tick, bool retroceso)
	{
		var f = new Fases();
		hasta = Math.Min(hasta, b.Count - 1);

		if (desde > hasta || tick <= 0)
			return f;

		// F1: hasta la barra que marca el extremo.
		var iExtremo = desde;

		for (var i = desde; i <= hasta; i++)
		{
			if (signoNuevo < 0 ? b[i].High >= extremo : b[i].Low <= extremo)
			{
				iExtremo = i;
				break;
			}
		}

		f.F1Barras = iExtremo - desde + 1;

		// F2: barras comprimidas seguidas desde el extremo.
		var limite = Math.Max(1m, Math.Min((decimal)mediana.Rango * p.CompressionRangeMult, p.CompressionMaxTicks));
		var i2 = iExtremo + 1;
		f.F2EnExtremo = true;

		while (i2 <= hasta && b[i2].RangoTicks(tick) <= limite && !EsF3(b[i2], signoNuevo, p, mediana, tick, out _))
		{
			var lejos = signoNuevo < 0 ? (extremo - b[i2].Low) / tick : (b[i2].High - extremo) / tick;
			f.F2EnExtremo &= lejos <= p.CompressionMaxTicks;
			f.F2Barras++;
			i2++;
		}

		if (f.F2Barras == 0)
			f.F2EnExtremo = false;

		var maximo = retroceso ? p.F2MaxBarsRetrace : p.F2MaxBars;
		f.F2Valida = f.F2Barras >= p.F2MinBars && f.F2Barras <= maximo;

		// F3: la primera barra de arranque después del extremo.
		for (var i = iExtremo + 1; i <= hasta; i++)
		{
			if (EsF3(b[i], signoNuevo, p, mediana, tick, out var lenta))
			{
				f.F3Indice = i;
				(f.F3DuracionSec, f.F3RangoTicks, f.F3DeltaPct) = (b[i].DuracionSec, b[i].RangoTicks(tick), b[i].DeltaPct);
				break;
			}

			if (lenta)
			{
				f.F3Lenta = true;
				(f.F3DuracionSec, f.F3RangoTicks, f.F3DeltaPct) = (b[i].DuracionSec, b[i].RangoTicks(tick), b[i].DeltaPct);
			}
		}

		return f;
	}

	/// <summary>F3 (7.3): rango >= F3RangeMult x mediana, rápida y DeltaPct a favor >= F3DeltaPctMin. 'lenta' = todo menos la rapidez.</summary>
	public static bool EsF3(BarraVolumen b, int signo, ParametrosV2 p, (double Duracion, double Rango) mediana, decimal tick, out bool lenta)
	{
		var rango = b.RangoTicks(tick) >= (double)p.F3RangeMult * mediana.Rango;
		var delta = b.DeltaPct * signo >= p.F3DeltaPctMin;
		var rapida = EsRapida(b, p, mediana);
		lenta = rango && delta && !rapida;
		return rango && delta && rapida;
	}

	public static bool EsRapida(BarraVolumen b, ParametrosV2 p, (double Duracion, double Rango) mediana) =>
		b.DuracionSec <= (double)p.FastBarDurationMult * Math.Max(0.001, mediana.Duracion);
}
