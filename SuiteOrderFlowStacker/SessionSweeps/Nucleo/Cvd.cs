namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Vela del CVD (delta acumulado): apertura = cierre anterior; máximo y mínimo con el delta máximo/mínimo de la vela.</summary>
public readonly record struct VelaCvd(DateTime InicioUtc, decimal Open, decimal High, decimal Low, decimal Close, decimal PrecioHigh, decimal PrecioLow);

/// <summary>Divergencia de máximo a máximo (bajista) o de mínimo a mínimo (alcista) entre el precio y el CVD.</summary>
public sealed record DivergenciaCvd(bool Alcista, bool Micro, int I1, int I2, decimal Precio1, decimal Precio2, decimal Cvd1, decimal Cvd2);

/// <summary>
/// CVD del anexo: velas del delta acumulado (reinicio a la hora elegida) y divergencias configurables. MACRO con pivotes
/// de CvdMacroPivotBars velas a cada lado; MICRO con pivotes de CvdMicroPivotBars, solo dentro del tramo de una macro
/// (para afinar el punto exacto de giro). Las divergencias son segmentos entre dos pivotes: sin líneas verticales.
/// </summary>
public static class CalculoCvd
{
	/// <summary>Velas del CVD. 'reinicio' = hora de NY a la que vuelve a cero (18:00 = día de futuros, 09:30 = RTH).</summary>
	public static List<VelaCvd> Velas(IReadOnlyList<(DateTime InicioUtc, decimal High, decimal Low, decimal Delta, decimal MaxDelta, decimal MinDelta)> velas, TimeSpan reinicio)
	{
		var r = new List<VelaCvd>(velas.Count);
		decimal acumulado = 0;
		DateTime? sesion = null;

		foreach (var v in velas)
		{
			var (vela, nuevo, nuevaSesion) = Siguiente(acumulado, sesion, reinicio, v.InicioUtc, v.High, v.Low, v.Delta, v.MaxDelta, v.MinDelta);
			(acumulado, sesion) = (nuevo, nuevaSesion);
			r.Add(vela);
		}

		return r;
	}

	/// <summary>Una vela del CVD a partir del acumulado anterior (vuelve a cero al empezar otra sesión).</summary>
	public static (VelaCvd Vela, decimal Acumulado, DateTime Sesion) Siguiente(decimal acumulado, DateTime? sesion, TimeSpan reinicio, DateTime inicioUtc,
		decimal high, decimal low, decimal delta, decimal maxDelta, decimal minDelta)
	{
		var et = Zonas.AEt(inicioUtc);
		var s = (et.TimeOfDay >= reinicio ? et.Date : et.Date.AddDays(-1)) + reinicio;

		if (sesion != s)
			acumulado = 0;

		var apertura = acumulado;
		acumulado += delta;

		// Sin MaxDelta / MinDelta (0), el rango es el de apertura a cierre.
		var alto = Math.Max(Math.Max(apertura, acumulado), apertura + Math.Max(0, maxDelta));
		var bajo = Math.Min(Math.Min(apertura, acumulado), apertura + Math.Min(0, minDelta));
		return (new VelaCvd(inicioUtc, apertura, alto, bajo, acumulado, high, low), acumulado, s);
	}

	/// <summary>Divergencias macro y micro sobre las últimas 'maximo' velas.</summary>
	public static List<DivergenciaCvd> Divergencias(IReadOnlyList<VelaCvd> v, int macro, int micro, bool conMicro, int maximo)
	{
		var desde = Math.Max(0, v.Count - Math.Max(10, maximo));
		var r = Buscar(v, desde, Math.Max(2, macro), false);

		if (conMicro)
		{
			foreach (var m in Buscar(v, desde, Math.Max(1, micro), true))
			{
				// Solo dentro del tramo de una macro del mismo lado (de su primer pivote a unas velas después del segundo).
				if (r.Any(d => !d.Micro && d.Alcista == m.Alcista && m.I1 >= d.I1 && m.I2 <= d.I2 + 2 * macro) &&
					!r.Any(d => d.Alcista == m.Alcista && d.I1 == m.I1 && d.I2 == m.I2))
				{
					r.Add(m);
				}
			}
		}

		return r;
	}

	private static List<DivergenciaCvd> Buscar(IReadOnlyList<VelaCvd> v, int desde, int n, bool micro)
	{
		var r = new List<DivergenciaCvd>();
		var maximos = new List<int>();
		var minimos = new List<int>();

		for (var i = desde + n; i < v.Count - n; i++)
		{
			var alto = true;
			var bajo = true;

			for (var k = i - n; k <= i + n; k++)
			{
				if (k == i)
					continue;

				alto &= v[k].PrecioHigh < v[i].PrecioHigh;
				bajo &= v[k].PrecioLow > v[i].PrecioLow;
			}

			if (alto)
				maximos.Add(i);

			if (bajo)
				minimos.Add(i);
		}

		// Máximo a máximo: precio más alto y CVD más bajo = bajista. Mínimo a mínimo: precio más bajo y CVD más alto = alcista.
		for (var k = 1; k < maximos.Count; k++)
		{
			var (a, b) = (maximos[k - 1], maximos[k]);

			if (v[b].PrecioHigh > v[a].PrecioHigh && v[b].High < v[a].High)
				r.Add(new DivergenciaCvd(false, micro, a, b, v[a].PrecioHigh, v[b].PrecioHigh, v[a].High, v[b].High));
		}

		for (var k = 1; k < minimos.Count; k++)
		{
			var (a, b) = (minimos[k - 1], minimos[k]);

			if (v[b].PrecioLow < v[a].PrecioLow && v[b].Low > v[a].Low)
				r.Add(new DivergenciaCvd(true, micro, a, b, v[a].PrecioLow, v[b].PrecioLow, v[a].Low, v[b].Low));
		}

		return r;
	}
}

/// <summary>
/// CVD incremental para los indicadores: una vela cerrada cada vez (índice = vela del gráfico) y la vela abierta aparte.
/// Un pivote solo se detecta con sus velas a la derecha ya cerradas, así que una divergencia detectada no cambia: se
/// devuelve una sola vez (Nuevas) y queda en Todas.
/// </summary>
public sealed class SeguidorCvd
{
	private readonly List<VelaCvd> _velas = new();
	private readonly HashSet<(bool, bool, int, int)> _vistas = new();
	private decimal _acumulado;
	private DateTime? _sesion;

	public TimeSpan Reinicio { get; set; } = new(18, 0, 0);

	public IReadOnlyList<VelaCvd> Velas => _velas;

	public List<DivergenciaCvd> Todas { get; } = new();

	public void Reiniciar()
	{
		_velas.Clear();
		_vistas.Clear();
		Todas.Clear();
		(_acumulado, _sesion) = (0, null);
	}

	public VelaCvd Cerrar(DateTime inicioUtc, decimal high, decimal low, decimal delta, decimal maxDelta, decimal minDelta)
	{
		var (vela, acumulado, sesion) = CalculoCvd.Siguiente(_acumulado, _sesion, Reinicio, inicioUtc, high, low, delta, maxDelta, minDelta);
		(_acumulado, _sesion) = (acumulado, sesion);
		_velas.Add(vela);
		return vela;
	}

	/// <summary>La vela en curso (no se guarda).</summary>
	public VelaCvd Abierta(DateTime inicioUtc, decimal high, decimal low, decimal delta, decimal maxDelta, decimal minDelta) =>
		CalculoCvd.Siguiente(_acumulado, _sesion, Reinicio, inicioUtc, high, low, delta, maxDelta, minDelta).Vela;

	/// <summary>Divergencias que aparecen con la última vela cerrada (cada una una sola vez).</summary>
	public List<DivergenciaCvd> Nuevas(int macro, int micro, bool conMicro, int maximo)
	{
		var nuevas = CalculoCvd.Divergencias(_velas, macro, micro, conMicro, maximo).Where(d => _vistas.Add((d.Alcista, d.Micro, d.I1, d.I2))).ToList();
		Todas.AddRange(nuevas);

		if (Todas.Count > 2000)
			Todas.RemoveRange(0, Todas.Count - 2000);

		return nuevas;
	}
}
