namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

/// <summary>Un LVN conocido por Session Sweeps Pro (siempre del Thermal Volume Profile + LVN (Suite)).</summary>
public sealed class ZonaLvn
{
	public long Id, RangoId;
	public decimal Bajo, Alto, Centro;
	public bool Manual;

	/// <summary>ACTIVO, PROBADO, ATRAVESADO o BORRADO (v3, 7.7).</summary>
	public string Estado = "ACTIVO";

	/// <summary>Sin confirmar con velas cerradas (no se usa para setups ni objetivos).</summary>
	public bool Provisional;

	public int Score, WidthTicks;
	public decimal Depth;
	public bool Tocado;
	public decimal? PrecioOriginal;

	public DateTime Utc;

	public string Origen => Manual ? "MANUAL" : "AUTO";

	/// <summary>Utilizable: confirmado y no atravesado (ACTIVO o PROBADO).</summary>
	public bool Activo => !Provisional && Estado is "ACTIVO" or "PROBADO";

	public bool Contiene(decimal precio, decimal margen) => precio >= Bajo - margen && precio <= Alto + margen;
}

/// <summary>
/// LvnRepository (5): Session Sweeps Pro no calcula LVN; los recibe del Thermal (automáticos y los trazados con el botón
/// LVN). Un RangeReset borra las zonas del rango anterior y avisa (los setups PENDIENTES que dependan solo de ellas se
/// cancelan; los ejecutados se mantienen).
/// </summary>
public sealed class RepositorioLvn
{
	private readonly Dictionary<long, ZonaLvn> _zonas = new();

	public long RangoActual { get; private set; }

	/// <summary>Se llama con las zonas que un RangeReset (o un BORRADO) acaba de quitar.</summary>
	public Action<IReadOnlyList<ZonaLvn>>? AlBorrar { get; set; }

	public IEnumerable<ZonaLvn> Activas => _zonas.Values.Where(z => z.Activo);

	public IEnumerable<ZonaLvn> Todas => _zonas.Values;

	public void Reiniciar()
	{
		_zonas.Clear();
		RangoActual = 0;
	}

	public void Aplicar(EventoSuite e)
	{
		switch (e)
		{
			case LvnZone z when z.Estado == "BORRADO":
				if (_zonas.Remove(z.Id, out var quitada))
					AlBorrar?.Invoke(new[] { quitada });

				break;

			case LvnZone z:
				if (z.RangoId < RangoActual)
					return;

				RangoActual = Math.Max(RangoActual, z.RangoId);
				_zonas[z.Id] = new ZonaLvn
				{
					Id = z.Id, RangoId = z.RangoId, Bajo = Math.Min(z.Bajo, z.Alto), Alto = Math.Max(z.Bajo, z.Alto), Centro = z.Centro, Manual = z.Manual,
					Estado = z.Estado switch { "MITIGADO" or "ATRAVESADO" => "ATRAVESADO", "PROBADO" => "PROBADO", _ => "ACTIVO" }, Utc = z.Utc,
					Provisional = z.Provisional, Score = z.Score, WidthTicks = z.WidthTicks, Depth = z.Depth, Tocado = z.WasTouched, PrecioOriginal = z.PrecioOriginal
				};

				break;

			case RangeReset r:
				var viejas = _zonas.Values.Where(x => x.RangoId <= r.RangoAnteriorId || x.RangoId < r.RangoNuevoId).ToList();

				foreach (var x in viejas)
					_zonas.Remove(x.Id);

				RangoActual = Math.Max(RangoActual, r.RangoNuevoId);

				if (viejas.Count > 0)
					AlBorrar?.Invoke(viejas);

				break;
		}
	}

	/// <summary>Una vela que cierra al otro lado de una zona activa la deja ATRAVESADA.</summary>
	public void Mitigar(decimal alto, decimal bajo, decimal cierre)
	{
		foreach (var z in Activas.ToList())
		{
			if (bajo <= z.Bajo && alto >= z.Alto && (cierre < z.Bajo || cierre > z.Alto))
				z.Estado = "ATRAVESADO";
		}
	}

	/// <summary>Próximo LVN objetivo (5.6): el ACTIVO más cercano en la dirección de la operación a TargetMinDistTicks o más.</summary>
	public ZonaLvn? Proximo(decimal precio, int signo, decimal distanciaMinima) =>
		Activas.Where(z => signo * (z.Centro - precio) >= distanciaMinima).OrderBy(z => Math.Abs(z.Centro - precio)).FirstOrDefault();

	/// <summary>LVN (activos) que tocan una zona de precios.</summary>
	public IEnumerable<ZonaLvn> Solapan(decimal bajo, decimal alto, decimal margen) => Activas.Where(z => z.Bajo - margen <= alto && z.Alto + margen >= bajo);
}

/// <summary>Un EDGE conocido por Sessions Sweeps Pro (del Thermal Volume Profile + LVN (Suite)).</summary>
public sealed class ZonaEdge
{
	public long Id, RangoId;
	public decimal Precio, Bajo, Alto;

	/// <summary>TECHO_HVN o PISO_HVN.</summary>
	public string Lado = "TECHO_HVN";

	/// <summary>ACTIVO, PROBADO, ATRAVESADO o BORRADO.</summary>
	public string Estado = "ACTIVO";

	public bool Provisional, Manual;
	public int Score, Toques;
	public DateTime Utc;

	public string Origen => Manual ? "MANUAL" : "AUTO";

	public bool Activo => !Provisional && Estado is "ACTIVO" or "PROBADO";

	public string Texto => Lado == "PISO_HVN" ? "PISO HVN" : "TECHO HVN";
}

/// <summary>Repositorio de EDGE (v3, 8): como el de LVN; un RangeReset los borra y avisa.</summary>
public sealed class RepositorioEdge
{
	private readonly Dictionary<long, ZonaEdge> _zonas = new();

	public long RangoActual { get; private set; }

	public Action<IReadOnlyList<ZonaEdge>>? AlBorrar { get; set; }

	public IEnumerable<ZonaEdge> Activas => _zonas.Values.Where(z => z.Activo);

	public IEnumerable<ZonaEdge> Todas => _zonas.Values;

	public void Aplicar(EventoSuite e)
	{
		switch (e)
		{
			case EdgeZone z when z.Estado == "BORRADO":
				if (_zonas.Remove(z.Id, out var quitada))
					AlBorrar?.Invoke(new[] { quitada });

				break;

			case EdgeZone z:
				if (z.RangoId < RangoActual)
					return;

				RangoActual = Math.Max(RangoActual, z.RangoId);
				_zonas[z.Id] = new ZonaEdge
				{
					Id = z.Id, RangoId = z.RangoId, Precio = z.Precio, Bajo = Math.Min(z.Bajo, z.Alto), Alto = Math.Max(z.Bajo, z.Alto), Lado = z.Lado,
					Estado = z.Estado, Provisional = z.Provisional, Manual = z.Manual, Score = z.Score, Toques = z.Toques, Utc = z.Utc
				};

				break;

			case RangeReset r:
				var viejas = _zonas.Values.Where(x => x.RangoId <= r.RangoAnteriorId || x.RangoId < r.RangoNuevoId).ToList();

				foreach (var x in viejas)
					_zonas.Remove(x.Id);

				RangoActual = Math.Max(RangoActual, r.RangoNuevoId);

				if (viejas.Count > 0)
					AlBorrar?.Invoke(viejas);

				break;
		}
	}

	/// <summary>Próximo EDGE objetivo: el ACTIVO más cercano en la dirección de la operación a la distancia mínima o más.</summary>
	public ZonaEdge? Proximo(decimal precio, int signo, decimal distanciaMinima) =>
		Activas.Where(z => signo * (z.Precio - precio) >= distanciaMinima).OrderBy(z => Math.Abs(z.Precio - precio)).FirstOrDefault();

	/// <summary>EDGE confirmados (no provisionales ni borrados; también atravesados: el retest vale) que tocan una zona.</summary>
	public IEnumerable<ZonaEdge> Solapan(decimal bajo, decimal alto) =>
		_zonas.Values.Where(z => !z.Provisional && z.Estado != "BORRADO" && z.Bajo <= alto && z.Alto >= bajo);
}
