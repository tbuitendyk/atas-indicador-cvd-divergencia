using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

internal static class PruebasFootprint
{
	private const decimal Tick = 0.25m;

	private static Dictionary<decimal, (decimal Bid, decimal Ask)> Plano(decimal minimo, decimal maximo)
	{
		var niveles = new Dictionary<decimal, (decimal Bid, decimal Ask)>();

		for (var p = minimo; p <= maximo; p += Tick)
			niveles[p] = (5, 5);

		return niveles;
	}

	/// <summary>Footprint de la vela del barrido: 3 desequilibrios compradores diagonales desde el mínimo, POC en mínimo + 0.75.</summary>
	private static Dictionary<decimal, (decimal Bid, decimal Ask)> Desequilibrado(decimal minimo, decimal maximo)
	{
		var niveles = Plano(minimo, maximo);
		niveles[minimo] = (10, 5);
		niveles[minimo + Tick] = (10, 30);
		niveles[minimo + 2 * Tick] = (10, 30);
		niveles[minimo + 3 * Tick] = (40, 60);
		return niveles;
	}

	private static IndicatorCandle Vela(int i, decimal minimo, decimal maximo, decimal apertura, decimal cierre, decimal delta = 10,
		decimal volumen = 100, Dictionary<decimal, (decimal Bid, decimal Ask)>? niveles = null) => new()
	{
		Time = Comun.T0.AddMinutes(i), Low = minimo, High = maximo, Open = apertura, Close = cierre, Delta = delta, Volume = volumen,
		Levels = niveles ?? Plano(minimo, maximo)
	};

	/// <summary>
	/// Velas 0-31: mínimo de swing 100.00 en la vela 10 (el más bajo de la hora anterior: externo) y 100.25 en la 20
	/// (100.00 es más bajo dentro de la hora: interno). La vela 30 barre; la 31 está en curso.
	/// </summary>
	internal static List<IndicatorCandle> Serie(Func<int, IndicatorCandle?>? reemplazo = null)
	{
		var lista = new List<IndicatorCandle>();

		for (var i = 0; i < 32; i++)
		{
			lista.Add(reemplazo?.Invoke(i) ?? i switch
			{
				10 => Vela(i, 100.00m, 102, 101, 101.5m),
				20 => Vela(i, 100.25m, 102, 101, 101.5m),
				30 => Vela(i, 99.25m, 101.25m, 100.0m, 100.75m, 150, 300, Desequilibrado(99.25m, 101.25m)),
				_ => Vela(i, 100.75m, 102, 101, 101.5m)
			});
		}

		return lista;
	}

	private static List<IndicatorCandle> Espejo(List<IndicatorCandle> origen) => origen.Select(v => new IndicatorCandle
	{
		Time = v.Time, Open = 200 - v.Open, Close = 200 - v.Close, High = 200 - v.Low, Low = 200 - v.High,
		Delta = -v.Delta, Volume = v.Volume,
		Levels = v.Levels.ToDictionary(kv => 200 - kv.Key, kv => (kv.Value.Ask, kv.Value.Bid))
	}).ToList();

	private static FootprintPredictorPro Nuevo(List<IndicatorCandle> velas) => new()
	{
		InstrumentInfo = Comun.Instrumento(),
		ChartInfo = new ChartInfo(),
		TestCandles = velas
	};

	/// <summary>
	/// ticks: cada vela se ve primero en vivo con Close = Open y sin delta (en la vela del barrido, por debajo del nivel
	/// interno); los valores finales llegan sin otra llamada para esa vela, salvo la última.
	/// </summary>
	private static void Ejecutar(FootprintPredictorPro indicador, bool ticks = false)
	{
		for (var i = 0; i < indicador.TestCandles.Count; i++)
		{
			var vela = indicador.TestCandles[i];

			if (!ticks)
			{
				indicador.TestCalc(i);
				continue;
			}

			var (cierre, delta) = (vela.Close, vela.Delta);
			vela.Close = vela.Open;
			vela.Delta = 0;
			indicador.TestCalc(i);
			indicador.TestCalc(i);
			vela.Close = cierre;
			vela.Delta = delta;

			if (i == indicador.TestCandles.Count - 1)
				indicador.TestCalc(i);
		}
	}

	/// <summary>Puntos de las cuatro series del Footprint (DataSeries 1 a 4).</summary>
	internal static string Puntos(Indicator indicador)
	{
		var nombres = new[] { "LongExt", "LongInt", "ShortExt", "ShortInt" };

		return string.Join(" ", Enumerable.Range(1, 4).SelectMany(s => ((ValueDataSeries)indicador.DataSeries[s]).Values
			.Where(v => v.Value != 0).Select(v => $"{nombres[s - 1]}@{v.Key}={v.Value:0.00}")));
	}

	private static string Panel(Indicator indicador) => string.Join(" | ", Comun.Dibujar(indicador).Log);

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("3. Footprint Predictor Pro (Sweeps & Context)");

		var basico = Nuevo(Serie());
		Ejecutar(basico);
		c.Check("external sweep: one big green dot, 3 ticks under bar 30's low", Puntos(basico), "LongExt@30=98.50");
		c.Check("context turns bullish at bar 30, chart time", Panel(basico), "ESTRUCTURA DE MERCADO | Contexto: ALCISTA | Inicio: 26/10/2026 09:30");
		c.Check("dot series hide zero values", ((ValueDataSeries)basico.DataSeries[1]).ShowZeroValue.ToString(), "False");

		var ticks = Nuevo(Serie());
		Ejecutar(ticks, ticks: true);
		c.Check("ticks: same single dot (no premature 'broken' level)", Puntos(ticks), "LongExt@30=98.50");

		var interno = Nuevo(Serie(i => i == 30 ? Vela(30, 99.75m, 101.25m, 100.25m, 100.75m, 150, 300, Desequilibrado(99.75m, 101.25m)) : null));
		Ejecutar(interno);
		c.Check("internal sweep only: small blue dot, 2 ticks under", Puntos(interno), "LongInt@30=99.25");
		c.Check("internal sweep leaves the context alone", Panel(interno), "ESTRUCTURA DE MERCADO | Contexto: RANGO | Inicio: Esperando datos...");

		var roto = Nuevo(Serie(i => i == 25 ? Vela(25, 99.5m, 101, 100.5m, 99.75m) : null));
		Ejecutar(roto);
		c.Check("levels broken by a close at bar 25 are retired: no sweep", Puntos(roto), "");

		var tranquilo = Nuevo(Serie(i => i == 30 ? Vela(30, 99.25m, 101.25m, 100.0m, 100.75m, 150, 150, Desequilibrado(99.25m, 101.25m)) : null));
		Ejecutar(tranquilo);
		c.Check("volume 150 < 2x average: no dot", Puntos(tranquilo), "");

		var conHueco = Plano(99.25m, 101.25m);
		conHueco[99.25m] = (10, 5);
		conHueco[99.50m] = (10, 30);
		conHueco[99.75m] = (10, 30);
		conHueco.Remove(100.00m);
		conHueco[100.25m] = (10, 5);
		conHueco[100.50m] = (5, 30);
		var hueco = Nuevo(Serie(i => i == 30 ? Vela(30, 99.25m, 101.25m, 100.0m, 100.75m, 150, 300, conHueco) : null));
		Ejecutar(hueco);
		c.Check("imbalances split by an empty price are not 'stacked'", Puntos(hueco), "");

		var bajista = Nuevo(Espejo(Serie()));
		Ejecutar(bajista, ticks: true);
		c.Check("bear: one big red dot, 3 ticks over the high", Puntos(bajista), "ShortExt@30=101.50");
		c.Check("bear: context turns bearish", Panel(bajista), "ESTRUCTURA DE MERCADO | Contexto: BAJISTA | Inicio: 26/10/2026 09:30");

		basico.MinPenetrationTicks = 10;
		Ejecutar(basico);
		c.Check("after raising Min Penetration to 10 ticks: dot gone", Puntos(basico), "");
		c.Check("panel default position", $"{basico.PanelX},{basico.PanelY}", "25,410");

		Comun.ProbarArrastre(c, interno, () => (interno.PanelX, interno.PanelY), 240, 70, "footprint");

		interno.ShowContextPanel = false;
		Comun.Dibujar(interno);
		c.Check("footprint: hidden panel cannot be grabbed", interno.ProcessMouseDown(new(interno.PanelX + 10, interno.PanelY + 5)).ToString(), "False");
	}
}
