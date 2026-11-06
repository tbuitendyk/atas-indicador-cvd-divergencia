namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

// --- FVG / IFVG ---------------------------------------------------------------------------------------------------

public sealed class ZonaFvg
{
	public int Id;
	public bool Alcista;
	public decimal Arriba, Abajo;
	public int Izquierda, Derecha;
	public bool Mitigado;

	/// <summary>FVG invertido: un FVG alcista que recibe un cierre por debajo de su base pasa a bajista (y al revés).</summary>
	public bool Ifvg;

	public int? IfvgEn;

	/// <summary>FVG de desplazamiento en contra de un barrido, en las PostSweepFVGWindow velas siguientes.</summary>
	public bool PostSweep;

	/// <summary>Sentido actual: el del FVG, o el contrario si es IFVG.</summary>
	public bool AlcistaAhora => Ifvg ? !Alcista : Alcista;
}

/// <summary>
/// FVG con la regla del Pine (bull = Low - High[2] &gt; ATR144 × filtro, Low &gt; High[2], Close[1] &gt; High[2] y sin
/// hueco en esta vela ni en la anterior). En lugar de una sola caja, se guarda la lista de todos. Mitigación como
/// fvgUpdate: la caja se extiende hasta que el precio entra y cruza su base (alcista) o su techo (bajista).
/// </summary>
public sealed class DetectorFvg
{
	private int _siguiente = 1;

	public List<ZonaFvg> Fvgs { get; } = new();

	/// <summary>Procesa la última vela de la lista. Devuelve (FVG nuevos, FVG que pasaron a IFVG).</summary>
	public (List<ZonaFvg> Nuevos, List<ZonaFvg> Invertidos) Procesar(List<Vela> velas, decimal? atr144, double filtro, int maximo)
	{
		var nuevos = new List<ZonaFvg>();
		var invertidos = new List<ZonaFvg>();
		var n = velas.Count;

		if (n < 3)
			return (nuevos, invertidos);

		var b = velas[n - 1];
		var b1 = velas[n - 2];
		var b2 = velas[n - 3];

		// Mitigación, extensión e inversión de los anteriores (antes de añadir los de esta vela).
		foreach (var f in Fvgs)
		{
			if (!f.Mitigado)
			{
				if (b.High > f.Abajo && b.Low < f.Arriba && (f.Alcista ? b.Low <= f.Abajo : b.High >= f.Arriba))
					f.Mitigado = true;

				f.Derecha = b.Indice;
			}

			if (!f.Ifvg && (f.Alcista ? b.Close < f.Abajo : b.Close > f.Arriba))
			{
				f.Ifvg = true;
				f.IfvgEn = b.Indice;
				invertidos.Add(f);
			}
		}

		var umbral = (atr144 ?? 0) * (decimal)filtro;
		var bullG = b.Low > b1.High;
		var bullG1 = b1.Low > b2.High;
		var bearG = b.High < b1.Low;
		var bearG1 = b1.High < b2.Low;

		if (b.Low - b2.High > umbral && b.Low > b2.High && b1.Close > b2.High && !(bullG || bullG1))
			nuevos.Add(new ZonaFvg { Id = _siguiente++, Alcista = true, Arriba = b.Low, Abajo = b2.High, Izquierda = b.Indice - 1, Derecha = b.Indice });

		if (b2.Low - b.High > umbral && b.High < b2.Low && b1.Close < b2.Low && !(bearG || bearG1))
			nuevos.Add(new ZonaFvg { Id = _siguiente++, Alcista = false, Arriba = b2.Low, Abajo = b.High, Izquierda = b.Indice - 1, Derecha = b.Indice });

		Fvgs.AddRange(nuevos);

		if (Fvgs.Count > maximo)
			Fvgs.RemoveRange(0, Fvgs.Count - maximo);

		return (nuevos, invertidos);
	}
}

// --- MSS / CHoCH --------------------------------------------------------------------------------------------------

public readonly record struct LineaChoch(int Desde, int Hasta, decimal Precio, bool Alcista);

/// <summary>Traducción del bloque MSS del Pine: pivothigh/pivotlow(len, len) y CHoCH al cerrar más allá con shift opuesto.</summary>
public sealed class DetectorMss
{
	private decimal? _ph, _pl;
	private int _phBar, _plBar;
	private bool _phX, _plX;
	private int _shift;

	public List<LineaChoch> Lineas { get; } = new();

	public void Procesar(List<Vela> velas, int len, bool activo, int maximo)
	{
		var n = velas.Count;
		var i = n - 1 - len;

		if (len >= 1 && i >= len)
		{
			var c = velas[i];
			bool alto = true, bajo = true;

			for (var k = i - len; k <= i + len; k++)
			{
				if (k == i)
					continue;

				alto &= velas[k].High < c.High;
				bajo &= velas[k].Low > c.Low;
			}

			if (alto)
				(_ph, _phX, _phBar) = (c.High, false, c.Indice);

			if (bajo)
				(_pl, _plX, _plBar) = (c.Low, false, c.Indice);
		}

		if (!activo || n == 0)
			return;

		var b = velas[^1];

		if (_ph is { } ph && b.Close > ph && !_phX)
		{
			_phX = true;

			if (_shift == -1)
				Lineas.Add(new LineaChoch(_phBar, b.Indice, ph, true));

			_shift = 1;
		}

		if (_pl is { } pl && b.Close < pl && !_plX)
		{
			_plX = true;

			if (_shift == 1)
				Lineas.Add(new LineaChoch(_plBar, b.Indice, pl, false));

			_shift = -1;
		}

		if (Lineas.Count > maximo)
			Lineas.RemoveAt(0);
	}
}

// --- Contexto H1 --------------------------------------------------------------------------------------------------

/// <summary>Estructura H1: pivotes de longitud H1PivotLen; HH + HL alcista, LH + LL bajista; desempate con la EMA 20.</summary>
public sealed class CalculoH1
{
	private readonly Ema _ema = new(20);
	private readonly List<decimal> _altos = new(), _bajos = new();

	public ContextoH1 Valor { get; private set; } = ContextoH1.Rango;

	public decimal? Ema => _ema.Valor;

	public void Agregar(List<Vela> h1, int len)
	{
		var b = h1[^1];
		_ema.Agregar(b.Close);
		var i = h1.Count - 1 - len;

		if (len >= 1 && i >= len)
		{
			bool alto = true, bajo = true;

			for (var k = i - len; k <= i + len; k++)
			{
				if (k == i)
					continue;

				alto &= h1[k].High < h1[i].High;
				bajo &= h1[k].Low > h1[i].Low;
			}

			if (alto)
				_altos.Add(h1[i].High);

			if (bajo)
				_bajos.Add(h1[i].Low);
		}

		ContextoH1? estructura = null;

		if (_altos.Count >= 2 && _bajos.Count >= 2)
		{
			bool hh = _altos[^1] > _altos[^2], hl = _bajos[^1] > _bajos[^2];
			bool lh = _altos[^1] < _altos[^2], ll = _bajos[^1] < _bajos[^2];
			estructura = hh && hl ? ContextoH1.Alcista : lh && ll ? ContextoH1.Bajista : null;
		}

		if (estructura is { } e)
		{
			Valor = e;
			return;
		}

		// Desempate: pendiente de la EMA 20 y cierre por encima o por debajo.
		var pendiente = _ema.Valor - _ema.Anterior;
		Valor = pendiente > 0 && b.Close > _ema.Valor ? ContextoH1.Alcista
			: pendiente < 0 && b.Close < _ema.Valor ? ContextoH1.Bajista
			: ContextoH1.Rango;
	}
}

// --- Proyección ---------------------------------------------------------------------------------------------------

public sealed record Objetivo(string Nombre, decimal Precio, decimal Puntos, decimal Usd, decimal Rr, bool FueraDeRango);

public sealed class Proyeccion
{
	public decimal Entrada, Stop;
	public List<Objetivo> Objetivos = new();
	public decimal? RangoRestante;

	/// <summary>"T1 18190.00 (60 pts) | T2 ..." (ASCII).</summary>
	public string Texto(Func<decimal, string> precio) =>
		string.Join(" | ", Objetivos.Select((o, i) => $"T{i + 1} {precio(o.Precio)} ({o.Puntos:0.##} pts){(o.FueraDeRango ? " fuera de rango diario" : "")}"));

	/// <summary>
	/// Objetivos tras un barrido (bajistas tras uno buyside): los candidatos al otro lado del precio, por distancia;
	/// T1 = el primero a MinTargetTicks o más, T2 y T3 los siguientes. Stop = extremo del barrido ± StopBufferTicks.
	/// </summary>
	public static Proyeccion Calcular(Lado ladoBarrido, decimal entrada, decimal extremo, IEnumerable<(string Nombre, decimal Precio)> candidatos,
		ConfiguracionSweeps cfg, decimal? adr, (decimal Alto, decimal Bajo)? hoy)
	{
		var bajista = ladoBarrido == Lado.Buyside;
		var tick = cfg.TamanoTick;
		var stop = bajista ? extremo + cfg.Ticks(cfg.StopBufferTicks) : extremo - cfg.Ticks(cfg.StopBufferTicks);
		var riesgo = Math.Abs(stop - entrada);
		var restante = adr is { } a && hoy is { } h ? Math.Max(0, a - (h.Alto - h.Bajo)) : (decimal?)null;
		var p = new Proyeccion { Entrada = entrada, Stop = stop, RangoRestante = restante };

		var ordenados = candidatos
			.Where(c => bajista ? c.Precio < entrada : c.Precio > entrada)
			.OrderBy(c => Math.Abs(entrada - c.Precio))
			.ToList();

		var primero = ordenados.FindIndex(c => Math.Abs(entrada - c.Precio) >= cfg.Ticks(cfg.MinTargetTicks));

		if (primero < 0)
			return p;

		foreach (var c in ordenados.Skip(primero))
		{
			if (p.Objetivos.Count == 3)
				break;

			if (p.Objetivos.Any(o => Math.Abs(o.Precio - c.Precio) <= tick))
				continue;

			var puntos = Math.Abs(entrada - c.Precio);
			p.Objetivos.Add(new Objetivo(c.Nombre, c.Precio, puntos, puntos * cfg.ValorPunto, riesgo > 0 ? puntos / riesgo : 0,
				restante is { } r && puntos > r));
		}

		return p;
	}
}
