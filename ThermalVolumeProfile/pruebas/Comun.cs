using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

using Termico;

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

		Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {nombre,-72} got [{obtenido}]{(ok ? "" : $"  expected [{esperado}]")}");
	}

	public void Check(string nombre, bool condicion, string detalle = "") => Check(nombre, condicion ? "True" + (detalle.Length > 0 ? " " + detalle : "") : "False " + detalle, "True" + (detalle.Length > 0 ? " " + detalle : ""));

	public int Resumen()
	{
		Console.WriteLine(_fallos == 0 ? $"\nALL PASS ({_total} checks)" : $"\n{_fallos} FAILURE(S) of {_total} checks");
		return _fallos;
	}
}

internal static class Datos
{
	public const decimal Tick = 0.25m;

	/// <summary>Perfil directo (1 tick por fila) cuya fila 0 está en 'bajo'.</summary>
	public static Perfil PerfilDe(double[] valores, decimal bajo = 15000m) => new()
	{
		Tick = Tick,
		TicksPorFila = 1,
		PrimeraFila = (long)(bajo / Tick),
		Valor = valores,
		DeltaFila = new double[valores.Length],
		Tipo = TiposPerfil.Volume
	};

	/// <summary>Valores por fila a partir de tramos (desde, hasta, valor), el resto 'fondo'.</summary>
	public static double[] Tramos(int filas, double fondo, params (int Desde, int Hasta, double Valor)[] tramos)
	{
		var v = Enumerable.Repeat(fondo, filas).ToArray();

		foreach (var (desde, hasta, valor) in tramos)
		{
			for (var i = desde; i <= hasta; i++)
				v[i] = valor;
		}

		return v;
	}

	public static VelaPerfil VP(DateTime t, params (decimal Precio, decimal Bid, decimal Ask)[] niveles)
	{
		var v = new VelaPerfil
		{
			Time = t,
			High = niveles.Length == 0 ? 0 : niveles.Max(n => n.Precio),
			Low = niveles.Length == 0 ? 0 : niveles.Min(n => n.Precio),
			Volume = niveles.Sum(n => n.Bid + n.Ask),
			Delta = niveles.Sum(n => n.Ask - n.Bid)
		};

		foreach (var n in niveles)
			v.Niveles.Add(new NivelPrecio(n.Precio, n.Bid + n.Ask, n.Bid, n.Ask));

		return v;
	}

	/// <summary>Vela de ATAS con volumen por precio: 'volumen(precio)' por tick entre bajo y alto (mitad Bid, mitad Ask).</summary>
	public static IndicatorCandle Vela(DateTime t, decimal bajo, decimal alto, Func<decimal, decimal> volumen)
	{
		var c = new IndicatorCandle { Time = t, Open = bajo, High = alto, Low = bajo, Close = alto };

		for (var p = bajo; p <= alto; p += Tick)
		{
			var v = volumen(p);

			if (v > 0)
				c.Levels[p] = (v / 2, v / 2);
		}

		c.Volume = c.Levels.Values.Sum(l => l.Bid + l.Ask);
		return c;
	}

	public static string Precio(decimal p) => Utilidades.Formato(p, Tick);
}
