using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>v4 (correcciones del 13-11-2026): Indicador Global Session Sweep.</summary>
internal static class PruebasV4
{
	public static void Ejecutar(Comprobador c)
	{
		Estructura(c);
	}

	/// <summary>G3: confirmación estructural del giro (BOS + FVG a favor + LVN como objetivo).</summary>
	private static void Estructura(Comprobador c)
	{
		c.Seccion("v4 G3: confirmación estructural después del barrido de un máximo: BOS + FVG a favor + LVN");
		var s = new SimMotor(sinBarridos: true);
		var d = new DateTime(2026, 10, 30, 9, 40, 0);

		// Pierna alcista con un pivote mínimo en 21401 (09:42) y el barrido de 21420 hasta 21422 (09:44).
		s.Minuto(d, SimMotor.Camino(21404, 21408, 3, 3));
		s.Minuto(d.AddMinutes(1), SimMotor.Camino(21408, 21405, 3, 3));
		s.Minuto(d.AddMinutes(2), SimMotor.Camino(21405, 21401, 3, 3).Concat(SimMotor.Camino(21401, 21406, 3, 3)));
		s.Minuto(d.AddMinutes(3), SimMotor.Camino(21406, 21412, 3, 3));
		s.Minuto(d.AddMinutes(4), SimMotor.Camino(21412, 21422, 3, 3));
		s.Minuto(d.AddMinutes(5), SimMotor.Camino(21422, 21410, 3, 3));

		var e = new EvaluacionBarrido
		{
			Id = 99, Nivel = new NivelLiquidez { Id = 99, Precio = 21420, Lado = Lado.Buyside, Grupo = "dia", Origen = "Dia anterior" }, Signo = 1, L = 21420,
			Extremo = 21422, T0Utc = Utc(d.AddMinutes(4).AddSeconds(30)), ExtremoUtc = Utc(d.AddMinutes(4).AddSeconds(59))
		};

		string Leer() => s.M.Leer(m =>
		{
			m.Estructura(e);
			return $"BOS {e.Bos} FVG {e.FvgAFavor} LVN {e.LvnObjetivo} → {e.EstructuraConfirmada}";
		});

		c.Check("sin cierre bajo el pivote, sin FVG ni LVN: nada confirmado", Leer(), "BOS False FVG False LVN False → False");
		s.Minuto(d.AddMinutes(6), SimMotor.Camino(21410, 21398, 3, 3));
		c.Check("BOS (cierre de 1 minuto en 21398 bajo el pivote de la pierna, 21401) y el FVG bajista que deja la caída (21410-21412); sin LVN",
			Leer(), "BOS True FVG True LVN False → False");
		c.Check("el FVG es del motor (nacido después del extremo)", s.M.Leer(m => string.Join(" ", m.Fvg2.Fvgs.Where(f => !f.Alcista).Select(f => $"{F(f.Bajo)}-{F(f.Alto)}"))), "21410-21412");
		s.M.LvnDesdeSuite(PruebasV2Lvn.Z(1, 1, 21380m, 21381m, manual: false));
		c.Check("con un LVN debajo como objetivo: ESTRUCTURA CONFIRMADA", Leer(), "BOS True FVG True LVN True → True");
	}
}
