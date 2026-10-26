using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

internal static class PruebasTendencia
{
	/// <summary>Velas de 1 minuto: 09:00-09:59 entre 100 y 110, 10:00-10:19 en 105, 10:20-10:25 en 112.</summary>
	internal static List<IndicatorCandle> Datos()
	{
		var lista = new List<IndicatorCandle>();

		void Agregar(DateTime t, decimal p) => lista.Add(new IndicatorCandle { Time = t, Open = p, High = p, Low = p, Close = p });

		for (var m = 0; m < 60; m++)
			Agregar(Comun.T0.AddMinutes(m), m % 2 == 0 ? 100 : 110);

		for (var m = 0; m < 20; m++)
			Agregar(Comun.T0.AddMinutes(60 + m), 105);

		for (var m = 0; m < 6; m++)
			Agregar(Comun.T0.AddMinutes(80 + m), 112);

		return lista;
	}

	internal static string Fila(List<string> log, string etiqueta)
	{
		var i = log.IndexOf(etiqueta);
		return i < 0 ? "missing" : $"{log[i + 1]} {log[i + 2]}";
	}

	private static IndicadorTendenciaHourly Nuevo(List<IndicatorCandle> velas, string tipoGrafico = "TimeFrame") => new()
	{
		InstrumentInfo = Comun.Instrumento(),
		ChartInfo = new ChartInfo { ChartType = tipoGrafico },
		TestCandles = velas
	};

	private static string Aviso(IndicadorTendenciaHourly indicador) =>
		Comun.Dibujar(indicador).Log.Any(s => s.StartsWith("Aviso")).ToString();

	private static List<IndicatorCandle> Cada(int minutos) => Enumerable.Range(0, 12).Select(i => new IndicatorCandle
	{
		Time = Comun.T0.AddMinutes(i * minutos), Open = 100, High = 101, Low = 99, Close = 100
	}).ToList();

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("1. Indicador de Tendencia Hourly");

		// Modo en vivo (Confirmar al cierre desactivado), histórico.
		var vivo = Nuevo(Datos());
		vivo.ConfirmarAlCierre = false;
		Comun.CalcularTodo(vivo);
		var log = Comun.Dibujar(vivo).Log;
		c.Check("live 1H: close 112 > prev hour high 110", Fila(log, "1 Hora"), "▲ Desde: 10:20");
		c.Check("live 15M: close 112 > prev 15m high 105", Fila(log, "15 Min"), "▲ Desde: 10:20");
		c.Check("live 5M: close 112 inside prev 5m (112)", Fila(log, "5 Min"), "(-) Desde: 10:25");
		c.Check("no warning on a 1-minute chart", Aviso(vivo), "False");

		// Tick en la vela en curso.
		var ultima = vivo.TestCandles[^1];
		ultima.Close = 99;
		ultima.Low = 99;
		vivo.TestCalc(vivo.TestCandles.Count - 1);
		log = Comun.Dibujar(vivo).Log;
		c.Check("tick 1H: drops to 99 < prev hour low 100", Fila(log, "1 Hora"), "▼ Desde: 10:25");
		c.Check("tick 15M: drops below prev 15m low 105", Fila(log, "15 Min"), "▼ Desde: 10:25");
		c.Check("tick 5M: drops below prev 5m low 112", Fila(log, "5 Min"), "▼ Desde: 10:25");

		// La vela cerrada se incorpora con sus valores finales (mínimo 99, no el 112 viejo).
		var t = ultima.Time;

		for (var m = 1; m <= 4; m++)
			vivo.TestCandles.Add(new IndicatorCandle { Time = t.AddMinutes(m), Open = 100, High = 100, Low = 100, Close = 100 });

		vivo.TestCandles.Add(new IndicatorCandle { Time = t.AddMinutes(5), Open = 99.5m, High = 99.5m, Low = 99.5m, Close = 99.5m });

		for (var i = vivo.TestCandles.Count - 5; i < vivo.TestCandles.Count; i++)
			vivo.TestCalc(i);

		c.Check("fold 5M: 99.5 inside prev 5m 99-112 => range", Fila(Comun.Dibujar(vivo).Log, "5 Min"), "(-) Desde: 10:30");

		// Confirmar al cierre (por defecto).
		var confirmado = Nuevo(Datos());
		c.Check("default mode is 'Confirmar al cierre'", confirmado.ConfirmarAlCierre.ToString(), "True");
		Comun.CalcularTodo(confirmado);
		log = Comun.Dibujar(confirmado).Log;
		c.Check("confirmed 1H: only one completed hour => no data", Fila(log, "1 Hora"), "(-) Desde: --:--");
		c.Check("confirmed 15M: completed bars all inside", Fila(log, "15 Min"), "(-) Desde: 09:30");
		c.Check("confirmed 5M: 10:20-24 bar closed 112 > 105", Fila(log, "5 Min"), "▲ Desde: 10:25");

		var enCurso = confirmado.TestCandles[^1];
		enCurso.Close = 90;
		enCurso.Low = 90;
		confirmado.TestCalc(confirmado.TestCandles.Count - 1);
		c.Check("confirmed 5M: mid-period drop to 90 => state held", Fila(Comun.Dibujar(confirmado).Log, "5 Min"), "▲ Desde: 10:25");

		// Aviso de gráfico no compatible.
		var quince = Nuevo(Cada(15));
		Comun.CalcularTodo(quince);
		c.Check("warning on a 15-minute chart", Aviso(quince), "True");

		var tres = Nuevo(Cada(3));
		Comun.CalcularTodo(tres);
		c.Check("warning on a 3-minute chart (does not divide 5)", Aviso(tres), "True");

		var cinco = Nuevo(Cada(5));
		Comun.CalcularTodo(cinco);
		c.Check("no warning on a 5-minute chart", Aviso(cinco), "False");

		var ticks = Nuevo(Cada(15), "Tick");
		Comun.CalcularTodo(ticks);
		c.Check("no warning on non-time charts (tick)", Aviso(ticks), "False");

		Comun.ProbarArrastre(c, confirmado, () => (confirmado.PanelX, confirmado.PanelY), 240, 136, "tendencia");
	}
}
