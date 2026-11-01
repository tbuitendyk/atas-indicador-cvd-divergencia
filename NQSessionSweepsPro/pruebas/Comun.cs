using System;
using System.Linq;
using System.Collections.Generic;

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

/// <summary>Datos de prueba: velas de 1 minuto en hora de Nueva York, con footprint.</summary>
internal static class Datos
{
	public const decimal Tick = 0.25m;

	public static DateTime Utc(DateTime et) => NqSweeps.Nucleo.Zonas.AUtc(et, NqSweeps.Nucleo.ZonaHoraria.NuevaYork);

	/// <summary>
	/// Vela de 1 min que empieza a la hora ET dada. El volumen se reparte por igual entre Low y High; 'delta' se reparte
	/// igual (Ask = Bid + delta/n). 'extra' añade (Bid, Ask) en precios concretos.
	/// </summary>
	public static NqSweeps.Nucleo.VelaEntrada V(DateTime et, decimal o, decimal h, decimal l, decimal c, decimal volumen = 100, decimal delta = 0,
		params (decimal Precio, decimal Bid, decimal Ask)[] extra)
	{
		var e = new NqSweeps.Nucleo.VelaEntrada { InicioUtc = Utc(et), Open = o, High = h, Low = l, Close = c };
		var precios = (int)((h - l) / Tick) + 1;
		var mapa = new System.Collections.Generic.Dictionary<decimal, (decimal Bid, decimal Ask)>();

		for (var p = l; p <= h; p += Tick)
			mapa[p] = ((volumen - delta) / 2 / precios, (volumen + delta) / 2 / precios);

		foreach (var (p, bid, ask) in extra)
			mapa[p] = (mapa.GetValueOrDefault(p).Bid + bid, mapa.GetValueOrDefault(p).Ask + ask);

		foreach (var (p, n) in mapa)
			e.Niveles.Add(new NqSweeps.Nucleo.NivelVela(p, n.Bid, n.Ask));

		e.Volume = mapa.Values.Sum(n => n.Bid + n.Ask);
		e.Ask = mapa.Values.Sum(n => n.Ask);
		e.Bid = mapa.Values.Sum(n => n.Bid);
		e.Delta = e.Ask - e.Bid;
		return e;
	}

	/// <summary>Velas planas de 1 min entre dos horas ET (rango 'rango' alrededor de 'centro').</summary>
	public static System.Collections.Generic.IEnumerable<NqSweeps.Nucleo.VelaEntrada> Planas(DateTime desdeEt, DateTime hastaEt, decimal centro, decimal rango = 4)
	{
		for (var t = desdeEt; t < hastaEt; t = t.AddMinutes(1))
			yield return V(t, centro, centro + rango / 2, centro - rango / 2, centro);
	}
}
