namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// v4: una línea de máximo o mínimo de sesión. Nace en la vela del extremo y va hacia la derecha hasta que el precio la
/// rompe (ahí se detiene: nunca se extiende después de la ruptura). BOS = un cierre de 1 minuto más allá.
/// </summary>
public sealed class LineaSesion
{
	public long Id;

	/// <summary>Id de la sesión en la configuración ("Asia", "Londres", "Premercado", "NY AM", "NY PM").</summary>
	public string SesionId = "";

	public string Sesion = "";
	public bool Alto;
	public decimal Precio;
	public int DesdeBar;
	public DateTime DesdeUtc;
	public int? RotaBar, BosBar;
	public DateTime? RotaUtc, BosUtc;

	/// <summary>NY abierta: el último máximo o mínimo reciente (pivote), no el extremo de una sesión terminada.</summary>
	public bool Reciente;

	public bool Viva => RotaUtc is null;

	public bool EsNy => SesionId is ConfiguracionSweeps.NyAm or ConfiguracionSweeps.NyPm;

	public string Nombre => $"{(Alto ? "MÁXIMO" : "MÍNIMO")} {(Reciente ? "reciente " : "")}{Sesion}";
}

/// <summary>
/// v4 (líneas de sesión del propietario): al terminar Asia, Londres, Premercado o NY, una línea en su máximo y otra en su
/// mínimo; con NY abierta, una línea desde el último máximo y el último mínimo recientes (pivotes de 1 minuto). La
/// ruptura se detecta en el tick (alerta inmediata) y el BOS al cierre de la vela.
/// </summary>
public sealed class MotorLineasSesion
{
	private long _siguiente = 1;

	public List<LineaSesion> Lineas { get; } = new();

	/// <summary>Velas a cada lado de un pivote reciente de NY.</summary>
	public int FuerzaPivote { get; set; } = 2;

	public int Maximo { get; set; } = 80;

	public void Reiniciar()
	{
		Lineas.Clear();
		_siguiente = 1;
	}

	/// <summary>Un trade: las líneas vivas que rompe (para la alerta en el momento). 'bar' = índice de la vela en curso.</summary>
	public List<LineaSesion> Tick(DateTime utc, decimal precio, int bar)
	{
		var rotas = new List<LineaSesion>();

		foreach (var l in Lineas.Where(l => l.Viva && utc > l.DesdeUtc))
		{
			if (l.Alto ? precio > l.Precio : precio < l.Precio)
			{
				(l.RotaUtc, l.RotaBar) = (utc, bar);
				rotas.Add(l);
			}
		}

		return rotas;
	}

	/// <summary>
	/// Una vela cerrada: rupturas que no vio el tick (historial), BOS por cierre, líneas nuevas de las sesiones que
	/// terminaron y los pivotes recientes de NY abierta. Devuelve (línea, BOS) de lo que pasó en esta vela.
	/// </summary>
	public List<(LineaSesion Linea, bool Bos)> Vela(Vela v, IReadOnlyList<Vela> velas, IEnumerable<InstanciaSesion> terminadas, InstanciaSesion? nyAbierta)
	{
		var r = new List<(LineaSesion, bool)>();

		foreach (var l in Lineas.Where(l => l.DesdeBar < v.Indice))
		{
			if (l.Viva && (l.Alto ? v.High > l.Precio : v.Low < l.Precio))
			{
				(l.RotaUtc, l.RotaBar) = (v.InicioUtc, v.Indice);
				r.Add((l, false));
			}

			if (l.BosUtc is null && !l.Viva && (l.Alto ? v.Close > l.Precio : v.Close < l.Precio))
			{
				(l.BosUtc, l.BosBar) = (v.FinUtc, v.Indice);
				r.Add((l, true));
			}
		}

		foreach (var s in terminadas)
		{
			// Las líneas recientes de la sesión que termina las sustituyen las de su máximo y su mínimo.
			Lineas.RemoveAll(l => l.Reciente && l.Viva && l.SesionId == s.Id);
			Nueva(s, true, s.Alto, velas, s.DesdeBar, s.HastaBar, false);
			Nueva(s, false, s.Bajo, velas, s.DesdeBar, s.HastaBar, false);
		}

		if (nyAbierta is { } ny)
			Pivotes(ny, velas);

		if (Lineas.Count > Maximo)
			Lineas.RemoveRange(0, Lineas.Count - Maximo);

		return r;
	}

	private void Nueva(InstanciaSesion s, bool alto, decimal precio, IReadOnlyList<Vela> velas, int desde, int hasta, bool reciente)
	{
		var bar = desde;

		for (var i = Math.Max(0, desde); i <= Math.Min(hasta, velas.Count - 1); i++)
		{
			if (alto ? velas[i].High == precio : velas[i].Low == precio)
			{
				bar = i;
				break;
			}
		}

		if (bar < 0 || bar >= velas.Count)
			return;

		Lineas.Add(new LineaSesion
		{
			Id = _siguiente++, SesionId = s.Id, Sesion = s.Nombre, Alto = alto, Precio = precio, DesdeBar = bar, DesdeUtc = velas[bar].FinUtc, Reciente = reciente
		});
	}

	/// <summary>NY abierta: un pivote nuevo reemplaza la línea viva reciente de su lado (solo queda la del último extremo).</summary>
	private void Pivotes(InstanciaSesion ny, IReadOnlyList<Vela> velas)
	{
		var k = Math.Max(1, FuerzaPivote);
		var i = velas.Count - 1 - k;

		if (i - k < 0 || i < ny.DesdeBar)
			return;

		var c = velas[i];
		bool alto = true, bajo = true;

		for (var j = i - k; j <= i + k; j++)
		{
			if (j == i)
				continue;

			alto &= velas[j].High < c.High;
			bajo &= velas[j].Low > c.Low;
		}

		foreach (var (esAlto, precio) in new[] { (true, c.High), (false, c.Low) })
		{
			if (esAlto ? !alto : !bajo)
				continue;

			// Si después del pivote el precio ya lo pasó, no hay línea.
			if (velas.Skip(i + 1).Any(v => esAlto ? v.High > precio : v.Low < precio))
				continue;

			Lineas.RemoveAll(l => l.Reciente && l.Viva && l.Alto == esAlto && l.SesionId == ny.Id);
			Nueva(ny, esAlto, precio, velas, i, i, true);
		}
	}
}
