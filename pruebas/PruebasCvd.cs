using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

using Rectangle = System.Drawing.Rectangle;

internal static class PruebasCvd
{
	/// <summary>
	/// Compra completa: mínimo 100 (vela 4), máximo de swing (vela 8), mínimo más bajo 98 (vela 13) con CVD más alto y
	/// mecha de absorción, y giro de delta en la vela 14. Mínimos y máximos se alternan, como de costumbre.
	/// </summary>
	internal static List<IndicatorCandle> Alcista(decimal volumenMecha = 500m, decimal deltaGiro = 450m)
	{
		var spec = new (decimal H, decimal L, decimal D)[]
		{
			(113, 110, -100), (111, 108, -100), (109, 106, -100), (107, 104, -100), (103, 100, -100),
			(105, 102, 200), (107, 104, 200), (109, 106, 200), (111, 108, 200),
			(110, 107, -50), (108, 105, -50), (106, 103, -50), (104, 101, -50),
			(101, 98, -50), (102, 99, deltaGiro), (103, 100, 50), (104, 101, 50), (105, 102, 50), (106, 103, 50)
		};

		var lista = spec.Select((s, i) => new IndicatorCandle
		{
			Time = Comun.T0.AddMinutes(i), High = s.H, Low = s.L, Open = s.L + 1, Close = s.H - 1, Delta = s.D,
			MaxDelta = Math.Max(s.D, 0) + 10, MinDelta = Math.Min(s.D, 0) - 10, Volume = 1000
		}).ToList();

		// Vela 13: mecha inferior larga (mínimo 98, cuerpo 100-100.5) con 'volumenMecha' negociado en 98.00-99.75.
		var swing = lista[13];
		swing.Open = 100;
		swing.Close = 100.5m;

		for (var p = 98m; p < 100m; p += 0.25m)
			swing.Levels[p] = (volumenMecha / 8, 0);

		return lista;
	}

	private static List<IndicatorCandle> Espejo(List<IndicatorCandle> origen) => origen.Select(v => new IndicatorCandle
	{
		Time = v.Time, Open = 220 - v.Open, Close = 220 - v.Close, High = 220 - v.Low, Low = 220 - v.High,
		Delta = -v.Delta, MaxDelta = -v.MinDelta, MinDelta = -v.MaxDelta, Volume = v.Volume,
		Levels = v.Levels.ToDictionary(kv => 220 - kv.Key, kv => kv.Value)
	}).ToList();

	private static InstitutionalCvdTriggerEngine Nuevo(List<IndicatorCandle> velas, Action<InstitutionalCvdTriggerEngine>? ajustes = null,
		double anchoVela = 60)
	{
		var indicador = new InstitutionalCvdTriggerEngine
		{
			InstrumentInfo = Comun.Instrumento(),
			ChartInfo = new ChartInfo { PriceChartContainer = new PriceChartContainer { BarsWidth = anchoVela, BarSpacing = 4 } },
			TestCandles = velas,
			FuerzaMacro = 4,
			FuerzaMicro = 2
		};

		ajustes?.Invoke(indicador);
		return indicador;
	}

	/// <summary>
	/// ticks: cada vela se ve primero en vivo con 1/3 y 2/3 de su delta; el valor final llega sin otra llamada para
	/// esa vela (salvo la última, que sigue en curso). El indicador tiene que releerlo.
	/// </summary>
	internal static void Ejecutar(Indicator indicador, bool ticks = false)
	{
		var n = indicador.TestCandles.Count;

		for (var i = 0; i < n; i++)
		{
			var vela = indicador.TestCandles[i];

			if (!ticks)
			{
				indicador.TestCalc(i);
				continue;
			}

			var delta = vela.Delta;
			vela.Delta = delta / 3;
			indicador.TestCalc(i);
			vela.Delta = delta * 2 / 3;
			indicador.TestCalc(i);
			vela.Delta = delta;

			if (i == n - 1)
				indicador.TestCalc(i);
		}

		indicador.LastVisibleBarNumber = n - 1;
	}

	private static string Hud(List<(string Text, Rectangle Rect)> textos, string etiqueta)
	{
		var i = textos.FindIndex(x => x.Text == etiqueta);
		return i < 0 ? "missing" : textos[i + 1].Text;
	}

	/// <summary>Velas con bandera: la etiqueta se centra en la vela (x = 50·vela + 30) y su rectángulo empieza 60 px antes.</summary>
	internal static string Banderas(List<(string Text, Rectangle Rect)> textos, string etiqueta) =>
		string.Join(",", textos.Where(x => x.Text == etiqueta).Select(x => (x.Rect.X + 30) / 50));

	private static string Cvd(List<(string Text, Rectangle Rect)> textos, int n) => string.Join(" ", textos.Take(n).Select(x => x.Text));

	private static string Siguiente(List<(string Text, Rectangle Rect)> textos, string etiqueta, int salto) =>
		textos[textos.FindIndex(x => x.Text == etiqueta) + salto].Text;

	private const string CvdAlcista = "-100 -200 -300 -400 -500 -300 -100 +100 +300 +250 +200 +150 +100 +50 +500 +550 +600 +650 +700";

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("2. Institutional CVD Trigger & Absorption Engine");

		var alcista = Nuevo(Alcista());
		Ejecutar(alcista);
		var r = Comun.Dibujar(alcista).Texts;
		c.Check("bull: green flag on bar 17 (macro confirms there)", Banderas(r, "GATILLO LONG"), "17");
		c.Check("bull: flag shows the shift delta", Siguiente(r, "GATILLO LONG", 1), "Δ +450");
		c.Check("bull: flag shows wick % and wick volume %", Siguiente(r, "GATILLO LONG", 2), "Abs 67% · Vol 50%");
		c.Check("bull: HUD 1 macro", Hud(r, "1. Macro CVD (5M+):"), "ALCISTA (Absorción)");
		c.Check("bull: HUD 2 micro consumed by the trigger", Hud(r, "2. Micro-divergencia:"), "SIN AGOTAMIENTO");
		c.Check("bull: HUD 4 live delta / threshold", Hud(r, "4. Delta actual / umbral:"), "+50 / ±400");
		c.Check("bull: banner, chart time", r[^1].Text, "DISPARO COMPRA [LONG] [09:17]");
		c.Check("bull: session CVD per candle", Cvd(r, 19), CvdAlcista);

		var ticks = Nuevo(Alcista());
		Ejecutar(ticks, ticks: true);
		r = Comun.Dibujar(ticks).Texts;
		c.Check("ticks: same single flag on bar 17", Banderas(r, "GATILLO LONG"), "17");
		c.Check("ticks: CVD not inflated by repeated ticks", Cvd(r, 19), CvdAlcista);

		var pocoVolumen = Nuevo(Alcista(volumenMecha: 100m));
		Ejecutar(pocoVolumen);
		r = Comun.Dibujar(pocoVolumen).Texts;
		c.Check("wick volume 10% < 30%: no flag", Banderas(r, "GATILLO LONG"), "");
		c.Check("wick volume 10%: HUD 3 shows why", Hud(r, "3. Absorción en mecha:"), "Mecha 67% · Vol 10%");
		c.Check("wick volume 10%: banner waits", r[^1].Text, "ESPERANDO GATILLO...");

		var giroDebil = Nuevo(Alcista(deltaGiro: 300m));
		Ejecutar(giroDebil);
		r = Comun.Dibujar(giroDebil).Texts;
		c.Check("shift +300 < 400: no flag", Banderas(r, "GATILLO LONG"), "");
		c.Check("shift +300: micro still armed", Hud(r, "2. Micro-divergencia:"), "ALCISTA ACTIVA");

		var vencida = Nuevo(Alcista(), i => i.VigenciaMicro = 1);
		Ejecutar(vencida);
		c.Check("by bars, micro 1 bar: expired before macro confirms", Banderas(Comun.Dibujar(vencida).Texts, "GATILLO LONG"), "");

		var hastaOpuesta = Nuevo(Alcista(), i =>
		{
			i.VigenciaMicro = 1;
			i.Vigencia = ModoVigencia.HastaOpuesta;
		});
		Ejecutar(hastaOpuesta);
		c.Check("until opposite, micro 1 bar: still fires on bar 17", Banderas(Comun.Dibujar(hastaOpuesta).Texts, "GATILLO LONG"), "17");

		var bajista = Nuevo(Espejo(Alcista()));
		Ejecutar(bajista, ticks: true);
		r = Comun.Dibujar(bajista).Texts;
		c.Check("bear: red flag on bar 17", Banderas(r, "GATILLO SHORT"), "17");
		c.Check("bear: flag shows the shift delta", Siguiente(r, "GATILLO SHORT", 1), "Δ -450");
		c.Check("bear: HUD 1 macro", Hud(r, "1. Macro CVD (5M+):"), "BAJISTA (Absorción)");
		c.Check("bear: banner", r[^1].Text, "DISPARO VENTA [SHORT] [09:17]");

		var angosto = Nuevo(Alcista(), anchoVela: 4);
		Ejecutar(angosto);
		c.Check("narrow candles: no per-candle CVD text", Comun.Dibujar(angosto).Texts.Any(x => x.Text == "+700").ToString(), "False");

		// Reinicio por sesión, panel de velas CVD, y los dos indicadores coinciden.
		List<IndicatorCandle> Sesion() => new decimal[] { 100, -40, 60, 30, -10 }.Select((d, i) => new IndicatorCandle
		{
			Time = Comun.T0.AddMinutes(i), Open = 100, High = 101, Low = 99, Close = 100, Delta = d,
			MaxDelta = Math.Max(d, 0) + 15, MinDelta = Math.Min(d, 0) - 5, Volume = 1000
		}).ToList();

		var panel = new VelasCvd { TestCandles = Sesion(), TestSessions = new() { 3 } };
		Ejecutar(panel, ticks: true);
		var velas = (CandleDataSeries)panel.DataSeries[1];
		c.Check("CVD panel: own panel, candle series added", $"{panel.Panel} {panel.DataSeries.Count}", "NewPanel 2");
		c.Check("CVD panel: candles O/H/L/C, reset at session (bar 3)",
			string.Join(" | ", Enumerable.Range(0, 5).Select(i => $"{velas[i].Open}/{velas[i].High}/{velas[i].Low}/{velas[i].Close}")),
			"0/115/-5/100 | 100/115/55/60 | 60/135/55/120 | 0/45/-5/30 | 30/45/15/20");

		var mismoCvd = Nuevo(Sesion());
		mismoCvd.TestSessions = new() { 3 };
		Ejecutar(mismoCvd, ticks: true);
		c.Check("engine CVD text matches the panel's closes", Cvd(Comun.Dibujar(mismoCvd).Texts, 5), "+100 +60 +120 +30 +20");

		Comun.ProbarArrastre(c, alcista, () => (alcista.HudX, alcista.HudY), 330, 166, "cvd hud");
	}
}
