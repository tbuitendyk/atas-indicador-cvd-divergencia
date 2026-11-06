using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>S4: LVN del Thermal (AUTO y MANUAL), próximo objetivo, mitigación y RangeReset.</summary>
internal static class PruebasV2Lvn
{
	public static LvnZone Z(long id, long rango, decimal bajo, decimal alto, bool manual, string estado = "ACTIVO") => new()
	{
		Id = id, Fuente = FuenteSuite.Thermal, Utc = DateTime.UtcNow, CambioUtc = DateTime.UtcNow, Version = 1, RangoId = rango,
		Bajo = bajo, Alto = alto, Centro = (bajo + alto) / 2, Manual = manual, Estado = estado
	};

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S4 LvnRepository: LVN AUTO y MANUAL del Thermal, objetivo, mitigación, RangeReset");
		var r = new RepositorioLvn();
		var borradas = new List<ZonaLvn>();
		r.AlBorrar = z => borradas.AddRange(z);

		r.Aplicar(Z(1, 1, 21400, 21401, manual: false));
		r.Aplicar(Z(2, 1, 21412.50m, 21412.50m, manual: true));
		r.Aplicar(Z(3, 1, 21380, 21381, manual: false));
		c.Check("tres LVN activos (2 AUTO, 1 MANUAL)", $"{r.Activas.Count()} {r.Activas.Count(z => z.Manual)}", "3 1");

		c.Check("próximo LVN objetivo alcista desde 21395 (>= 8 ticks): 21400.50 AUTO", r.Proximo(21395, +1, 8 * Tick) is { } p ? $"{F(p.Centro)} {p.Origen}" : "ninguno", "21400.5 AUTO");
		c.Check("desde 21399 el AUTO está a 6 ticks: el siguiente es el MANUAL 21412.50", r.Proximo(21399, +1, 8 * Tick) is { } q ? $"{F(q.Centro)} {q.Origen}" : "ninguno", "21412.5 MANUAL");
		c.Check("bajista desde 21395: 21380.50", F(r.Proximo(21395, -1, 8 * Tick)?.Centro), "21380.5");
		c.Check("sin LVN en esa dirección: null (el panel dirá 'Sin objetivo LVN')", (r.Proximo(21420, +1, 8 * Tick) is null).ToString(), "True");

		r.Mitigar(21402, 21399, 21401.75m);
		c.Check("vela que atraviesa la zona y cierra fuera: MITIGADO", r.Todas.Single(z => z.Id == 1).Estado, "MITIGADO");
		c.Check("el MITIGADO ya no es objetivo", F(r.Proximo(21395, +1, 8 * Tick)?.Centro), "21412.5");

		r.Aplicar(Z(3, 1, 21380, 21381, manual: false, estado: "BORRADO"));
		c.Check("BORRADO quita la zona y avisa", $"{r.Todas.Count()} {borradas.Count}", "2 1");

		r.Aplicar(new RangeReset { Id = 9, Fuente = FuenteSuite.Thermal, Utc = DateTime.UtcNow, CambioUtc = DateTime.UtcNow, RangoAnteriorId = 1, RangoNuevoId = 2 });
		c.Check("RangeReset: se borran las zonas del rango anterior (también la MANUAL) y se avisa", $"{r.Todas.Count()} {borradas.Count} {r.RangoActual}", "0 3 2");
		r.Aplicar(Z(4, 1, 21390, 21391, manual: false));
		c.Check("un LVN tardío del rango viejo no entra", r.Todas.Count().ToString(), "0");
		r.Aplicar(Z(5, 2, 21390, 21391, manual: true));
		c.Check("los LVN del rango nuevo sí", r.Activas.Single().Origen, "MANUAL");

		var cfg = new ConfiguracionSweeps { TamanoTick = Tick };
		var motor = new MotorSweeps(cfg);
		c.Check("sin Thermal cargado: SIN FUENTE LVN", motor.HayFuenteLvn.ToString(), "False");
		motor.LvnDesdeSuite(Z(6, 1, 21450, 21451, manual: true));
		c.Check("los LVN del Thermal son objetivos de la proyección", string.Join(",", motor.ObjetivosExtra!().Select(o => $"{o.Nombre} {F(o.Precio)}")), "LVN MANUAL 21450.5");
	}
}
