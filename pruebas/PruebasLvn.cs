using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

using OFT.Rendering.Context;

internal static class PruebasLvn
{
	// 15:00 UTC = 09:00 hora del gráfico: la fecha es la misma.
	private static readonly DateTime Dia1 = new(2026, 10, 21, 15, 0, 0);

	private static IndicatorCandle Vela(DateTime t, decimal minimo, decimal maximo, decimal apertura, decimal cierre, Func<decimal, decimal> volumen)
	{
		var niveles = new Dictionary<decimal, (decimal Bid, decimal Ask)>();

		for (var p = minimo; p <= maximo; p += 0.25m)
			niveles[p] = (volumen(p) / 2, volumen(p) / 2);

		return new IndicatorCandle
		{
			Time = t, Low = minimo, High = maximo, Open = apertura, Close = cierre, Levels = niveles,
			Volume = niveles.Values.Sum(l => l.Bid + l.Ask)
		};
	}

	/// <summary>
	/// Tres días con el mismo perfil en V (el menor volumen en 102.00) y velas que rechazan 102.00:
	/// días 1 y 2, mínimo justo en 102.00 y cierre 1.25 pts arriba (cuenta como Extremo y como Mecha);
	/// día 3, 102.00 dentro de una mecha larga con el mínimo en 101.00 (solo cuenta como Mecha).
	/// Al final, una vela cerrada en 'cierreFinal' y la vela en curso.
	/// </summary>
	internal static List<IndicatorCandle> Datos(decimal cierreFinal = 103.0m)
	{
		Func<decimal, decimal> enV = p => Math.Abs(p - 102m) * 100m + 10m;
		Func<decimal, decimal> uno = _ => 1m;
		var lista = new List<IndicatorCandle>();

		for (var dia = 0; dia < 3; dia++)
		{
			var t = Dia1.AddDays(dia);
			lista.Add(Vela(t, 100, 104, 100.5m, 103.5m, enV));
			lista.Add(dia < 2
				? Vela(t.AddMinutes(1), 102, 103.5m, 103, 103.25m, uno)
				: Vela(t.AddMinutes(1), 101, 103.5m, 103, 103.5m, uno));
		}

		var fin = Dia1.AddDays(2).AddMinutes(2);
		lista.Add(Vela(fin, cierreFinal - 0.25m, cierreFinal, cierreFinal, cierreFinal, uno));
		lista.Add(Vela(fin.AddMinutes(1), cierreFinal - 0.25m, cierreFinal, cierreFinal, cierreFinal, uno));
		return lista;
	}

	private static InstitutionalDeepLvnEngine Nuevo(List<IndicatorCandle> velas, Action<InstitutionalDeepLvnEngine>? ajustes = null)
	{
		var indicador = new InstitutionalDeepLvnEngine
		{
			InstrumentInfo = Comun.Instrumento(),
			ChartInfo = new ChartInfo
			{
				PriceChartContainer = new PriceChartContainer { BarsWidth = 6, BarSpacing = 2 },
				TestYByPrice = EnPantalla
			},
			TestCandles = velas
		};

		ajustes?.Invoke(indicador);
		Comun.CalcularTodo(indicador);
		return indicador;
	}

	/// <summary>De precio a píxel con 100-104 dentro del área visible (102.00 cae en y = 480).</summary>
	internal static int EnPantalla(decimal precio) => (int)(1500 - precio * 10);

	/// <summary>Etiquetas de las líneas de LVN dibujadas.</summary>
	internal static string Etiqueta(RenderContext dibujo) =>
		string.Join(" || ", dibujo.Log.Where(t => t.StartsWith("LVN ")));

	private static string Hud(RenderContext dibujo, string etiqueta)
	{
		var i = dibujo.Log.IndexOf(etiqueta);
		return i < 0 ? "missing" : dibujo.Log[i + 1];
	}

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("4. Institutional 90-Day Deep LVN Profile Engine");

		var basico = Nuevo(Datos());
		var dibujo = Comun.Dibujar(basico);
		c.Check("LVN at 102.00: deep valley rejected on 2 days => high impact", Etiqueta(dibujo),
			"LVN ALTO IMPACTO (2 días probados) [102.00] | Rechazos: 2 (2↑ 0↓)");
		c.Check("one line drawn, thick (width 3)", string.Join(",", dibujo.HorizontalLines.Select(l => l.Pen.Width)), "3");
		c.Check("HUD 1: price inside the 3-day range => 60-day balance", Hud(dibujo, "1. Horizonte Rithmic:"), "Balance compuesto (60 días)");
		c.Check("HUD 2: node count", Hud(dibujo, "2. Nodos Activos:"), "1 Bipolares (3M) | 0 Bordes HVN");
		c.Check("HUD 3: retest target and distance", Hud(dibujo, "3. Objetivo Retest:"), "102.00 (a 1.0 pts) [2 Rechs]");

		var mecha = Nuevo(Datos(), i => i.RejectionMode = TipoRechazo.Mecha);
		c.Check("rejection type 'Mecha' also counts the day-3 long wick", Etiqueta(Comun.Dibujar(mecha)),
			"LVN ALTO IMPACTO (3 días probados) [102.00] | Rechazos: 3 (3↑ 0↓)");

		var uniforme = Nuevo(Datos(), i => i.VolumeSource = FuenteVolumen.Uniforme);
		dibujo = Comun.Dibujar(uniforme);
		c.Check("volume spread evenly over each candle: the valley disappears", Etiqueta(dibujo), "");
		c.Check("spread volume: HUD 3 has no target", Hud(dibujo, "3. Objetivo Retest:"), "Sin LVNs en la franja activa");

		var unDia = Nuevo(Datos(), i =>
		{
			i.SelectionMode = ModoHorizonte.Manual;
			i.ManualHistoryDays = 1;
		});
		dibujo = Comun.Dibujar(unDia);
		c.Check("manual 1 day (days 2-3): rejected on 1 day only => medium", Etiqueta(dibujo),
			"LVN Mediano (Borde HVN) [102.00] | Rechazos: 1 (1↑ 0↓)");
		c.Check("manual 1 day: HUD 1", Hud(dibujo, "1. Horizonte Rithmic:"), "Manual (1 día)");
		c.Check("manual 1 day: HUD 2", Hud(dibujo, "2. Nodos Activos:"), "0 Bipolares (3M) | 1 Bordes HVN");

		var descubrimiento = Nuevo(Datos(cierreFinal: 104.0m));
		dibujo = Comun.Dibujar(descubrimiento);
		c.Check("price at the 3-day high => 3-day discovery horizon", Hud(dibujo, "1. Horizonte Rithmic:"), "Descubrimiento / impulso (3 días)");
		c.Check("discovery: target distance from 104.00", Hud(dibujo, "3. Objetivo Retest:"), "102.00 (a 2.0 pts) [2 Rechs]");

		basico.ChartInfo.TestYByPrice = precio => (int)(2000 - precio * 10);
		c.Check("LVN outside the visible price range: no line drawn", Comun.Dibujar(basico).HorizontalLines.Count.ToString(), "0");
		basico.ChartInfo.TestYByPrice = EnPantalla;

		c.Check("low-impact lines hidden by default", basico.ShowLowImpact.ToString(), "False");
		c.Check("HUD default position", $"{basico.HudX},{basico.HudY}", "25,490");

		Comun.ProbarArrastre(c, basico, () => (basico.HudX, basico.HudY), 350, 120, "lvn hud");

		Rendimiento(c);
	}

	/// <summary>60 días de velas de 1 minuto: el histórico se procesa una vez y cada vela nueva cuesta poco.</summary>
	private static void Rendimiento(Comprobador c)
	{
		const int velasPorDia = 1440;
		const int dias = 60;
		var inicio = new DateTime(2026, 8, 1, 6, 0, 0);
		var velas = new List<IndicatorCandle>(velasPorDia * dias + 100);

		IndicatorCandle Minuto(int i)
		{
			var centro = 20000m + Math.Round((decimal)Math.Sin(i / 300.0) * 200m) * 0.25m + i % 7 * 0.25m;
			return new IndicatorCandle { Time = inicio.AddMinutes(i), Low = centro - 1, High = centro + 1, Open = centro - 0.5m, Close = centro + 0.5m, Volume = 100 };
		}

		for (var i = 0; i < velasPorDia * dias; i++)
			velas.Add(Minuto(i));

		var indicador = new InstitutionalDeepLvnEngine
		{
			InstrumentInfo = Comun.Instrumento(),
			ChartInfo = new ChartInfo(),
			TestCandles = velas
		};

		var reloj = Stopwatch.StartNew();
		Comun.CalcularTodo(indicador);
		var historico = reloj.ElapsedMilliseconds;

		reloj.Restart();

		for (var n = 0; n < 60; n++)
		{
			velas.Add(Minuto(velas.Count));
			indicador.TestCalc(velas.Count - 1);
		}

		var porVela = reloj.ElapsedMilliseconds / 60.0;

		Console.WriteLine($"      (history of {velas.Count - 60:N0} one-minute bars: {historico} ms; each new bar: {porVela:F2} ms)");
		c.Check("performance: 86,400-bar history processed in under 15 s", (historico < 15000).ToString(), "True");
		c.Check("performance: each new bar recomputes in under 100 ms", (porVela < 100).ToString(), "True");
	}
}
