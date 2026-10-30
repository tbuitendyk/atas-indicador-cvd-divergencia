using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Completo.Nucleo;

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

		Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {nombre,-70} got [{obtenido}]{(ok ? "" : $"  expected [{esperado}]")}");
	}

	public void Check(string nombre, bool condicion, string detalle = "") => Check(nombre, condicion ? "true" : $"false {detalle}".Trim(), "true");

	public int Resumen()
	{
		Console.WriteLine(_fallos == 0 ? $"\nALL PASS ({_total} checks)" : $"\n{_fallos} FAILURE(S) of {_total} checks");
		return _fallos;
	}
}

internal static class Velas
{
	public const decimal Tick = 0.25m;

	/// <summary>
	/// Vela de 1M con volumen por precio repartido según 'peso' (uniforme si no se da). El Ask y el Bid de cada
	/// precio se reparten de forma que el delta de la vela sea el indicado.
	/// </summary>
	public static EntradaVela1M V(DateTime inicio, decimal o, decimal h, decimal l, decimal c, decimal volumen = 100, decimal delta = 0,
		Func<decimal, decimal>? peso = null, bool conNiveles = true)
	{
		var e = new EntradaVela1M
		{
			InicioUtc = inicio,
			Apertura = o,
			Maximo = h,
			Minimo = l,
			Cierre = c,
			Volumen = volumen,
			Delta = delta,
			DeltaMaximo = Math.Max(delta, 0),
			DeltaMinimo = Math.Min(delta, 0)
		};

		if (!conNiveles)
			return e;

		var precios = new List<decimal>();

		for (var p = l; p <= h; p += Tick)
			precios.Add(p);

		var pesos = precios.Select(p => peso?.Invoke(p) ?? 1m).ToList();
		var total = pesos.Sum();
		var fraccionAsk = volumen > 0 ? (volumen + delta) / (2 * volumen) : 0.5m;

		for (var i = 0; i < precios.Count; i++)
		{
			var v = volumen * pesos[i] / total;
			e.Niveles.Add(new NivelEntrada(precios[i], v * (1 - fraccionAsk), v * fraccionAsk));
		}

		return e;
	}

	/// <summary>Peso con el volumen concentrado en la parte baja de la vela (POC y área de valor abajo: gatillo largo).</summary>
	public static Func<decimal, decimal> Abajo(decimal l, decimal h) => p => p <= l + (h - l) * 0.35m ? 8m : 1m;

	/// <summary>Peso con el volumen concentrado arriba (gatillo corto, o vela que no sirve de gatillo largo).</summary>
	public static Func<decimal, decimal> Arriba(decimal l, decimal h) => p => p >= h - (h - l) * 0.35m ? 8m : 1m;

	/// <summary>Velas de relleno planas: apertura = cierre, rango ±2 puntos.</summary>
	public static IEnumerable<EntradaVela1M> Planas(DateTime desde, int cantidad, decimal centro, decimal volumen = 100)
	{
		for (var i = 0; i < cantidad; i++)
			yield return V(desde.AddMinutes(i), centro, centro + 2, centro - 2, centro, volumen);
	}

	public static void Fijar(object objeto, string propiedad, object? valor) =>
		objeto.GetType().GetProperty(propiedad, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.GetSetMethod(true)!.Invoke(objeto, new[] { valor });

	public static ZonaLvn Zona(int id, decimal inferior, decimal superior, PrioridadLvn prioridad) => new()
	{
		Id = id,
		Inferior = inferior,
		Superior = superior,
		Centro = (inferior + superior) / 2,
		Prioridad = prioridad,
		Profundidad = 0.8m,
		Origenes = new List<string> { "prueba" }
	};
}
