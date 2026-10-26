using System;

using ATAS.Indicators;

using OFT.Rendering.Context;

/// <summary>Cuenta y muestra los resultados de las comprobaciones.</summary>
internal sealed class Comprobador
{
	private int _total;
	private int _fallos;

	public void Seccion(string titulo) => Console.WriteLine($"\n== {titulo} ==");

	public void Check(string nombre, string obtenido, string esperado)
	{
		_total++;
		var ok = obtenido == esperado;

		if (!ok)
			_fallos++;

		Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {nombre,-66} got [{obtenido}]{(ok ? "" : $"  expected [{esperado}]")}");
	}

	public int Resumen()
	{
		Console.WriteLine(_fallos == 0 ? $"\nALL PASS ({_total} checks)" : $"\n{_fallos} FAILURE(S) of {_total} checks");
		return _fallos;
	}
}

internal static class Comun
{
	/// <summary>Las velas de prueba están en UTC; la hora del gráfico es UTC-6, así que T0 es las 09:00.</summary>
	public static readonly DateTime T0 = new(2026, 10, 26, 15, 0, 0);

	public static InstrumentInfo Instrumento() => new() { TimeZoneOffset = TimeSpan.FromHours(-6), TickSize = 0.25m };

	/// <summary>Carga del histórico: cada vela una vez, en orden.</summary>
	public static void CalcularTodo(Indicator indicador)
	{
		for (var i = 0; i < indicador.TestCandles.Count; i++)
			indicador.TestCalc(i);

		indicador.LastVisibleBarNumber = indicador.TestCandles.Count - 1;
	}

	public static RenderContext Dibujar(Indicator indicador)
	{
		var contexto = new RenderContext();
		indicador.TestRender(contexto);
		return contexto;
	}

	/// <summary>Arrastre de un panel por su barra de título, en un gráfico de 1200 x 800.</summary>
	public static void ProbarArrastre(Comprobador c, Indicator indicador, Func<(int X, int Y)> posicion, int ancho, int alto, string nombre)
	{
		Dibujar(indicador);
		var (x0, y0) = posicion();

		c.Check($"{nombre}: click on the title bar is taken", indicador.ProcessMouseDown(new(x0 + 10, y0 + 5)).ToString(), "True");
		c.Check($"{nombre}: hand cursor while dragging", indicador.GetCursor(new(x0 + 10, y0 + 5)).ToString(), "Hand");

		indicador.ProcessMouseMove(new(x0 + 110, y0 + 55));
		c.Check($"{nombre}: panel follows the mouse (+100,+50)", $"{posicion().X - x0},{posicion().Y - y0}", "100,50");

		indicador.ProcessMouseMove(new(5000, 5000));
		c.Check($"{nombre}: cannot leave the chart area", $"{posicion().X},{posicion().Y}", $"{1200 - ancho},{800 - alto}");

		c.Check($"{nombre}: release ends the drag", indicador.ProcessMouseUp(new(5000, 5000)).ToString(), "True");
		c.Check($"{nombre}: later mouse moves go to the chart", indicador.ProcessMouseMove(new(10, 10)).ToString(), "False");

		Dibujar(indicador);
		var (x1, y1) = posicion();
		c.Check($"{nombre}: click on the panel body goes to the chart", indicador.ProcessMouseDown(new(x1 + 10, y1 + alto - 5)).ToString(), "False");
	}
}
