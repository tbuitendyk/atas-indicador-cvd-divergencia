namespace NqSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Señal de entrada (Modelo A o B). Corto tras un barrido buyside, largo tras uno sellside.</summary>
public sealed class Senal
{
	public int Id;
	public string Modelo = "";
	public ZonaBarrido Zona = null!;
	public int Barra;
	public DateTime Utc;
	public decimal Entrada, Stop;
	public Proyeccion Proyeccion = new();
	public List<(string Nombre, bool Cumple)> Confluencias = new();

	public bool Corto => Zona.Lado == Lado.Buyside;

	/// <summary>"A SHORT", "B LONG"…</summary>
	public string Etiqueta => $"{Modelo} {(Corto ? "SHORT" : "LONG")}";

	public string Titulo => $"ENTRADA {(Corto ? "BAJISTA" : "ALCISTA")} - Modelo {Modelo}";

	public string TextoConfluencias => string.Join(", ", Confluencias.Select(c => $"{c.Nombre} {(c.Cumple ? "SI" : "NO")}"));

	public int Cumplidas => Confluencias.Count(c => c.Cumple);
}

/// <summary>Evaluación del Modelo A de un barrido: condiciones de la vela de barrido y espera de la vela gatillo.</summary>
public sealed class EvaluacionA
{
	public ZonaBarrido Zona = null!;
	public bool Lvn, PinBar, Poc, Va, Cierre;
	public decimal? LvnTocado;

	/// <summary>Velas que quedan para que una cierre de vuelta (si la de barrido cumplió 1, 3 y 4 pero no cerró).</summary>
	public int Espera;

	public bool Terminada;
}

/// <summary>Secuencia del Modelo B (máquina de estados por barrido).</summary>
public sealed class SecuenciaB
{
	public ZonaBarrido Zona = null!;
	public EtapaModeloB Etapa = EtapaModeloB.EsperandoImpulso;
	public int Inicio;

	/// <summary>Máximo (corto) o mínimo (largo) del barrido al empezar la secuencia: referencia fija de impulso y cancelación.</summary>
	public decimal ExtremoBarrido;

	/// <summary>Mínimo (corto) o máximo (largo) del impulso desde el barrido.</summary>
	public decimal ExtremoImpulso;

	public int BarraImpulso;

	/// <summary>Nivel del retroceso: LVN (o borde del FVG / IFVG) que el precio tocó al volver.</summary>
	public decimal? Nivel;

	public string NombreNivel = "";
	public decimal TechoRetroceso;
	public int BarraTecho;
	public bool TocoFvg;
	public (int B1, decimal P1, int B2, decimal P2)? Directriz;
	public Senal? Senal;
	public string Motivo = "";

	public bool Viva => Etapa is EtapaModeloB.EsperandoImpulso or EtapaModeloB.Impulso or EtapaModeloB.Retroceso;
}

public static class Modelos
{
	/// <summary>Pin bar bajista (shooting star): mecha superior >= 2 cuerpos y >= 60 % del rango, cierre en el 40 % inferior.</summary>
	public static bool PinBar(Vela v, Lado ladoBarrido)
	{
		if (v.Rango <= 0)
			return false;

		var mecha = ladoBarrido == Lado.Buyside ? v.High - Math.Max(v.Open, v.Close) : Math.Min(v.Open, v.Close) - v.Low;
		var cierre = ladoBarrido == Lado.Buyside ? v.Close <= v.Low + v.Rango * 0.4m : v.Close >= v.High - v.Rango * 0.4m;
		return mecha >= 2 * v.Cuerpo && mecha >= v.Rango * 0.6m && cierre;
	}

	/// <summary>¿El extremo de la vela toca (a 'tol' o menos) o cruza un LVN en el sentido del barrido? Devuelve el LVN.</summary>
	public static decimal? LvnTocado(Vela v, Lado lado, IEnumerable<decimal> lvns, decimal tol)
	{
		foreach (var p in lvns.OrderBy(p => Math.Abs((lado == Lado.Buyside ? v.High : v.Low) - p)))
		{
			var toca = lado == Lado.Buyside
				? Math.Abs(v.High - p) <= tol || (v.Open < p && v.High >= p)
				: Math.Abs(v.Low - p) <= tol || (v.Open > p && v.Low <= p);

			if (toca)
				return p;
		}

		return null;
	}

	/// <summary>Último par de pivotes (longitud 2) del retroceso: bajos para un corto, altos para un largo.</summary>
	public static (int B1, decimal P1, int B2, decimal P2)? Directriz(List<Vela> velas, int desde, int hasta, bool corto)
	{
		var pivotes = new List<(int, decimal)>();

		for (var i = Math.Max(desde + 2, 2); i <= hasta - 2 && i < velas.Count - 2; i++)
		{
			var v = velas[i];
			var es = true;

			for (var k = i - 2; k <= i + 2; k++)
			{
				if (k != i)
					es &= corto ? velas[k].Low > v.Low : velas[k].High < v.High;
			}

			if (es)
				pivotes.Add((i, corto ? v.Low : v.High));
		}

		if (pivotes.Count < 2)
			return null;

		var (b1, p1) = pivotes[^2];
		var (b2, p2) = pivotes[^1];
		return (b1, p1, b2, p2);
	}

	public static decimal ValorDirectriz((int B1, decimal P1, int B2, decimal P2) d, int bar) =>
		d.B2 == d.B1 ? d.P2 : d.P2 + (d.P2 - d.P1) / (d.B2 - d.B1) * (bar - d.B2);
}
