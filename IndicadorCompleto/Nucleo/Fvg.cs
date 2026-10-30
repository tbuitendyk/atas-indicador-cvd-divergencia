namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Fair value gap de tres velas cerradas de 1M. Alcista: mínimo de C3 > máximo de C1.</summary>
internal sealed class Fvg
{
	public int Id;
	public Direccion Direccion;
	public decimal Inferior, Superior;
	public Vela C1 = null!, C2 = null!, C3 = null!;
	public bool Activo = true;
	public DateTime? InvalidadoUtc;

	public DateTime FormadoUtc => C3.FinUtc;

	public string Texto(Func<decimal, string> precio) =>
		$"FVG {Id} {(Direccion == Direccion.Larga ? "alcista" : "bajista")} {precio(Inferior)}–{precio(Superior)} · {(Activo ? "ACTIVO" : "INVALIDADO")}";
}

internal sealed class DetectorFvg
{
	private readonly ConfiguracionCompleto _cfg;
	private int _siguienteId = 1;

	public DetectorFvg(ConfiguracionCompleto cfg) => _cfg = cfg;

	public List<Fvg> Fvgs { get; } = new();

	/// <summary>Con cada vela de 1M cerrada: invalida los FVG cerrados más allá y detecta uno nuevo.</summary>
	public void Procesar(List<Vela> velas)
	{
		var ultima = velas[^1];

		// Invalidación: cierre más allá del borde lejano (alcista: bajo el inferior).
		foreach (var f in Fvgs.Where(f => f.Activo && f.C3 != ultima))
		{
			if (f.Direccion == Direccion.Larga ? ultima.Cierre < f.Inferior : ultima.Cierre > f.Superior)
			{
				f.Activo = false;
				f.InvalidadoUtc = ultima.FinUtc;
			}
		}

		if (velas.Count < 3)
			return;

		var (c1, c2, c3) = (velas[^3], velas[^2], velas[^1]);
		var minimo = _cfg.Ticks(_cfg.FvgMinimoTicks);

		if (c3.Minimo - c1.Maximo >= minimo)
			Fvgs.Add(new Fvg { Id = _siguienteId++, Direccion = Direccion.Larga, Inferior = c1.Maximo, Superior = c3.Minimo, C1 = c1, C2 = c2, C3 = c3 });
		else if (c1.Minimo - c3.Maximo >= minimo)
			Fvgs.Add(new Fvg { Id = _siguienteId++, Direccion = Direccion.Corta, Inferior = c3.Maximo, Superior = c1.Minimo, C1 = c1, C2 = c2, C3 = c3 });

		if (Fvgs.Count > 400)
			Fvgs.RemoveAt(0);
	}

	/// <summary>FVG activo de la dirección, formado en el intervalo, que se solapa con la zona o está a la distancia configurada.</summary>
	public Fvg? Cercano(Direccion direccion, decimal inferior, decimal superior, DateTime desdeUtc, DateTime hastaUtc)
	{
		var cerca = _cfg.Ticks(_cfg.FvgCercaniaTicks);

		return Fvgs
			.Where(f => f.Activo && f.Direccion == direccion && f.FormadoUtc >= desdeUtc && f.FormadoUtc <= hastaUtc)
			.Where(f => f.Inferior <= superior + cerca && f.Superior >= inferior - cerca)
			.OrderByDescending(f => f.FormadoUtc)
			.FirstOrDefault();
	}
}

/// <summary>
/// Directriz del retroceso: recta por dos pivotes confirmados de 1M (máximos descendentes antes de un largo,
/// mínimos ascendentes antes de un corto). Solo existe desde la confirmación del segundo pivote; una ruptura
/// anterior a esa disponibilidad no cuenta.
/// </summary>
internal sealed class Directriz
{
	public Direccion Direccion;
	public Pivote P1 = null!, P2 = null!;
	public DateTime? RotaUtc;
	public decimal? PrecioRotura;

	public DateTime DisponibleUtc => P2.ConfirmadoUtc;

	public decimal ValorEn(DateTime utc)
	{
		var minutos = (decimal)(P2.ExtremoUtc - P1.ExtremoUtc).TotalMinutes;
		var pendiente = minutos == 0 ? 0 : (P2.Precio - P1.Precio) / minutos;
		return P1.Precio + pendiente * (decimal)(utc - P1.ExtremoUtc).TotalMinutes;
	}

	public static Directriz? Construir(Direccion direccion, IEnumerable<Pivote> pivotes1M, DateTime desdeUtc, DateTime hastaUtc)
	{
		// Largo: máximos (la línea por encima del retroceso bajista); corto: mínimos.
		var lado = pivotes1M
			.Where(p => p.EsMaximo == (direccion == Direccion.Larga) && p.ExtremoUtc >= desdeUtc && p.ConfirmadoUtc <= hastaUtc)
			.OrderBy(p => p.ExtremoUtc)
			.ToList();

		for (var i = lado.Count - 1; i >= 1; i--)
		{
			var (p1, p2) = (lado[i - 1], lado[i]);

			if (direccion == Direccion.Larga ? p2.Precio < p1.Precio : p2.Precio > p1.Precio)
				return new Directriz { Direccion = direccion, P1 = p1, P2 = p2 };
		}

		return null;
	}

	/// <summary>Revisa la ruptura por cierre con una vela de 1M cerrada, solo después de la disponibilidad.</summary>
	public void Revisar(Vela vela)
	{
		if (RotaUtc is not null || vela.InicioUtc < DisponibleUtc)
			return;

		var valor = ValorEn(vela.FinUtc);

		if (Direccion == Direccion.Larga ? vela.Cierre > valor : vela.Cierre < valor)
		{
			RotaUtc = vela.FinUtc;
			PrecioRotura = vela.Cierre;
		}
	}
}
