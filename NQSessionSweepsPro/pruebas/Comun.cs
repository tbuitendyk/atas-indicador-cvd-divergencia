using System;

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
