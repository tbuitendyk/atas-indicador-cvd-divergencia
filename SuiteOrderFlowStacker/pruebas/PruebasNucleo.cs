using System;

internal static class PruebasNucleo
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("Compila");
		c.Check("núcleo compila", true);
	}
}
