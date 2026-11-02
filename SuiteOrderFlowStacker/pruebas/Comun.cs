using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Nucleo;

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

/// <summary>Datos de prueba en hora de Nueva York: velas con footprint, trades, DOM y MBO.</summary>
internal static class Datos
{
	public const decimal Tick = 0.25m;

	public static DateTime Utc(DateTime et) => Zonas.AUtc(et);

	public static long T(decimal precio) => PrecioMath.Tick(precio, Tick);

	/// <summary>Vela de 1 min con el volumen repartido entre Low y High (Bid = Ask) y niveles extra (precio, bid, ask).</summary>
	public static VelaFlujo V(DateTime et, decimal o, decimal h, decimal l, decimal c, decimal volumenPorNivel = 10, params (decimal Precio, decimal Bid, decimal Ask)[] extra)
	{
		var v = new VelaFlujo { InicioUtc = Utc(et), Open = o, High = h, Low = l, Close = c };

		for (var p = l; p <= h; p += Tick)
			v.Niveles[T(p)] = (volumenPorNivel / 2, volumenPorNivel / 2);

		foreach (var (p, bid, ask) in extra)
			v.Niveles[T(p)] = (bid, ask);

		v.Volume = v.Niveles.Values.Sum(n => n.Bid + n.Ask);
		v.Delta = v.Niveles.Values.Sum(n => n.Ask - n.Bid);
		return v;
	}

	public static IEnumerable<VelaFlujo> Planas(DateTime desdeEt, DateTime hastaEt, decimal centro, decimal rango = 4)
	{
		for (var t = desdeEt; t < hastaEt; t = t.AddMinutes(1))
			yield return V(t, centro, centro + rango / 2, centro - rango / 2, centro);
	}

	public static TradeFlujo Tr(DateTime utc, decimal precio, decimal vol, Lado agresor, long? idAgresor = null, long? idPasivo = null) =>
		new(utc, precio, vol, agresor, idAgresor, idPasivo);
}
