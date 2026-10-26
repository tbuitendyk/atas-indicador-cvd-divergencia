using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

internal static class PruebasCombinado
{
	private static readonly string[] Titulos =
	{
		"PANEL DE INDICADORES", "TENDENCIA MULTITEMPORAL", "CVD INSTITUTIONAL TRIGGER MATRIX", "ESTRUCTURA DE MERCADO",
		"RITHMIC 90-DAY DEEP LVN MONITOR"
	};

	private static IndicadorCombinado Nuevo(List<IndicatorCandle> velas) => new()
	{
		InstrumentInfo = Comun.Instrumento(),
		ChartInfo = new ChartInfo { PriceChartContainer = new PriceChartContainer { BarsWidth = 60, BarSpacing = 4 } },
		TestCandles = velas,
		FuerzaMacro = 4,
		FuerzaMicro = 2
	};

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("5. Indicador Combinado");

		// Cada módulo da lo mismo que su indicador individual con los mismos datos.
		var combinado = Nuevo(PruebasCvd.Alcista());
		PruebasCvd.Ejecutar(combinado, ticks: true);
		var dibujo = Comun.Dibujar(combinado);
		c.Check("CVD module: same green flag on bar 17 as the standalone", PruebasCvd.Banderas(dibujo.Texts, "GATILLO LONG"), "17");
		c.Check("one dashboard: title, then the four sections in order", string.Join(" | ", dibujo.Log.Where(Titulos.Contains)), string.Join(" | ", Titulos));

		var tendencia = Nuevo(PruebasTendencia.Datos());
		Comun.CalcularTodo(tendencia);
		c.Check("Tendencia module: same 5M row as the standalone", PruebasTendencia.Fila(Comun.Dibujar(tendencia).Log, "5 Min"), "▲ Desde: 10:25");

		var footprint = Nuevo(PruebasFootprint.Serie());
		Comun.CalcularTodo(footprint);
		c.Check("Footprint module: same dot as the standalone", PruebasFootprint.Puntos(footprint), "LongExt@30=98.50");

		var lvn = Nuevo(PruebasLvn.Datos());
		lvn.ChartInfo.TestYByPrice = PruebasLvn.EnPantalla;
		Comun.CalcularTodo(lvn);
		c.Check("LVN module: same line as the standalone", PruebasLvn.Etiqueta(Comun.Dibujar(lvn)),
			"LVN ALTO IMPACTO (2 días probados) [102.00] | Rechazos: 2 (2↑ 0↓)");

		// Módulos apagados.
		var sinCvd = Nuevo(PruebasCvd.Alcista());
		sinCvd.MostrarCvd = false;
		PruebasCvd.Ejecutar(sinCvd);
		dibujo = Comun.Dibujar(sinCvd);
		c.Check("CVD module off: no flags", PruebasCvd.Banderas(dibujo.Texts, "GATILLO LONG"), "");
		c.Check("CVD module off: its section leaves the dashboard", dibujo.Log.Contains("CVD INSTITUTIONAL TRIGGER MATRIX").ToString(), "False");

		var sinFootprint = Nuevo(PruebasFootprint.Serie());
		sinFootprint.MostrarFootprint = false;
		Comun.CalcularTodo(sinFootprint);
		c.Check("Footprint module off: its 4 dot series are hidden",
			string.Join(",", sinFootprint.DataSeries.Skip(1).Select(s => s.IsHidden)), "True,True,True,True");
		c.Check("Footprint module off: no dots computed", PruebasFootprint.Puntos(sinFootprint), "");

		// El tablero se arrastra como una unidad: 22 de título + (4 + 136) + (4 + 166) + (4 + 70) + (4 + 120) + 4 = 534.
		Comun.ProbarArrastre(c, combinado, () => (combinado.PanelX, combinado.PanelY), 350, 534, "dashboard");

		// Con el CVD apagado el tablero es 170 px más bajo.
		Comun.ProbarArrastre(c, sinCvd, () => (sinCvd.PanelX, sinCvd.PanelY), 350, 364, "dashboard without CVD");
	}
}
