namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>FVG de 3 velas en 1m o 5m (6.1).</summary>
public sealed class FvgV2
{
	public long Id;
	public int Marco;
	public bool Alcista;
	public decimal Bajo, Alto;
	public DateTime Utc;

	/// <summary>ACTIVO, PARCIAL (el precio entró), MITIGADO (se rellenó entero) o INVALIDADO (cierre que lo atraviesa).</summary>
	public string Estado = "ACTIVO";

	public bool Vivo => Estado is "ACTIVO" or "PARCIAL";

	public int Signo => Alcista ? 1 : -1;
}

/// <summary>Zona donde un modelo espera el gatillo (FVG+LVN, LVN, FVG, nivel roto, EMA, iceberg; BFVG, BFVG+LVN).</summary>
public sealed class ZonaOperable
{
	public long Id;
	public string Tipo = "";
	public decimal Bajo, Alto;

	/// <summary>Dirección de la operación: +1 largo, -1 corto.</summary>
	public int Signo;

	public long? FvgId, BarridoId, LvnId, EdgeId;
	public string LvnOrigen = "";
	public int Calidad;
	public DateTime Utc, ExpiraUtc;

	/// <summary>Estado del gatillo: SIN TOCAR, PENDIENTE, VÁLIDO o INVÁLIDO (S6).</summary>
	public string EstadoGatillo = "SIN TOCAR";

	public string Resultado = "";
	public string Modelo = "";

	/// <summary>Primera barra de volumen que tocó la zona (la compuesta de zona empieza ahí, 4.4 b).</summary>
	public int? BarraToque;

	public bool Cerrada;

	public bool Contiene(decimal precio, decimal margen = 0) => precio >= Bajo - margen && precio <= Alto + margen;

	public bool TocadaPor(decimal alto, decimal bajo) => bajo <= Alto && alto >= Bajo;
}

/// <summary>FvgEngine (6.1-6.3): FVG de 1m y 5m, tipo y calidad de zona, y el registro de los BFVG.</summary>
public sealed class MotorFvgV2
{
	private long _siguiente = 1;

	public List<FvgV2> Fvgs { get; } = new();

	/// <summary>Se llama con cada FVG nuevo (el motor registra los BFVG de las trampas abiertas).</summary>
	public Action<FvgV2>? AlNacer { get; set; }

	public void Reiniciar() => Fvgs.Clear();

	/// <summary>Una vela cerrada de ese marco ('velas' termina en ella): estados de los FVG vivos y FVG nuevo.</summary>
	public void Agregar(IReadOnlyList<Vela> velas, int marco, ParametrosV2 p, decimal tick)
	{
		if (velas.Count == 0)
			return;

		var v = velas[^1];

		foreach (var f in Fvgs.Where(f => f.Vivo && f.Marco == marco && f.Utc <= v.InicioUtc))
		{
			if (f.Alcista ? v.Close < f.Bajo : v.Close > f.Alto)
				f.Estado = "INVALIDADO";
			else if (f.Alcista ? v.Low <= f.Bajo : v.High >= f.Alto)
				f.Estado = "MITIGADO";
			else if (f.Alcista ? v.Low < f.Alto : v.High > f.Bajo)
				f.Estado = "PARCIAL";
		}

		if (velas.Count < 3)
			return;

		var (a, c) = (velas[^3], v);
		var minimo = Math.Max(1, p.FvgMinTicks) * tick;
		FvgV2? nuevo = null;

		if (c.Low - a.High >= minimo)
			nuevo = new FvgV2 { Alcista = true, Bajo = a.High, Alto = c.Low };
		else if (a.Low - c.High >= minimo)
			nuevo = new FvgV2 { Alcista = false, Bajo = c.High, Alto = a.Low };

		if (nuevo is null)
			return;

		nuevo.Id = _siguiente++;
		nuevo.Marco = marco;
		nuevo.Utc = c.FinUtc;
		Fvgs.Add(nuevo);

		// Máximo FvgMaxActive vivos por lado y marco.
		var vivos = Fvgs.Where(f => f.Vivo && f.Marco == marco && f.Alcista == nuevo.Alcista).ToList();

		foreach (var f in vivos.Take(Math.Max(0, vivos.Count - Math.Max(1, p.FvgMaxActive))))
			f.Estado = "MITIGADO";

		if (Fvgs.Count > 500)
			Fvgs.RemoveRange(0, Fvgs.Count - 500);

		AlNacer?.Invoke(nuevo);
	}

	/// <summary>Calidad del tipo de zona (6.2): FVG+LVN > LVN > FVG > nivel roto.</summary>
	public static int CalidadTipo(string tipo) => tipo switch
	{
		"FVG+LVN" or "BFVG+LVN" => 40,
		"FVG+EDGE" or "BFVG+EDGE" => 35,
		"LVN" => 30,
		"EDGE" => 25,
		"FVG" or "BFVG" => 20,
		"ICEBERG" => 20,
		"EMA" => 15,
		_ => 10
	};

	/// <summary>Una zona a partir de un FVG: con LVN solapado (>= 1 tick) es FVG+LVN; 'bfvg' = BFVG / BFVG+LVN (+ConfluenceBonus).</summary>
	public static ZonaOperable ZonaDeFvg(long id, FvgV2 f, RepositorioLvn lvn, ParametrosV2 p, decimal tick, bool bfvg, long? barrido, string modelo,
		RepositorioEdge? edge = null)
	{
		// El LVN cuenta aunque la pierna que formó el FVG lo haya atravesado; no cuentan un BORRADO ni uno PROVISIONAL.
		var conLvn = lvn.Todas.Where(z => z.Estado != "BORRADO" && !z.Provisional).FirstOrDefault(z => Math.Min(f.Alto, z.Alto) - Math.Max(f.Bajo, z.Bajo) >= 0);
		var conEdge = conLvn is null ? edge?.Solapan(f.Bajo, f.Alto).FirstOrDefault() : null;
		var tipo = (bfvg ? "BFVG" : "FVG") + (conLvn is not null ? "+LVN" : conEdge is not null ? "+EDGE" : "");

		return new ZonaOperable
		{
			Id = id,
			Tipo = tipo,
			Bajo = f.Bajo,
			Alto = f.Alto,
			Signo = f.Signo,
			FvgId = f.Id,
			BarridoId = barrido,
			LvnId = conLvn?.Id,
			EdgeId = conEdge?.Id,
			LvnOrigen = conLvn?.Origen ?? "",
			Calidad = CalidadTipo(tipo) + (bfvg && (conLvn is not null || conEdge is not null) ? p.ConfluenceBonus : 0),
			Utc = f.Utc,
			Modelo = modelo
		};
	}
}
