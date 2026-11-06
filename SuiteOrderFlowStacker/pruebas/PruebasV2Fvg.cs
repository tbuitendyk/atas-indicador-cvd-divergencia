using System;
using System.Collections.Generic;
using System.Linq;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>S5: FVG de 3 velas, estados, zona FVG+LVN y BFVG para TODOS los FVG de la pierna de recuperación (T22).</summary>
internal static class PruebasV2Fvg
{
	public static VelaEntrada E(DateTime et, decimal o, decimal h, decimal l, decimal c) =>
		new() { InicioUtc = Utc(et), Open = o, High = h, Low = l, Close = c, Volume = 100 };

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S5 FVG de 3 velas: estados ACTIVO, PARCIAL, MITIGADO, INVALIDADO");
		var p = new ParametrosV2();
		var m = new MotorFvgV2();
		var velas = new List<Vela>();
		var t = Utc(new DateTime(2026, 11, 2, 10, 0, 0));

		void V(decimal o, decimal h, decimal l, decimal cierre)
		{
			velas.Add(new Vela { InicioUtc = t, FinUtc = t.AddMinutes(1), Open = o, High = h, Low = l, Close = cierre });
			t = t.AddMinutes(1);
			m.Agregar(velas, 1, p, Tick);
		}

		V(21400, 21402, 21398, 21401);
		V(21401, 21408, 21400, 21407);
		V(21407, 21410, 21405, 21409);
		c.Check("FVG alcista 21402-21405 (3 puntos >= 4 ticks)", m.Fvgs.Count == 1 ? $"{m.Fvgs[0].Alcista} {F(m.Fvgs[0].Bajo)}-{F(m.Fvgs[0].Alto)} {m.Fvgs[0].Estado}" : $"{m.Fvgs.Count}", "True 21402-21405 ACTIVO");
		V(21409, 21410, 21404, 21406);
		c.Check("el precio entra en el hueco: PARCIAL", m.Fvgs[0].Estado, "PARCIAL");
		V(21406, 21407, 21401.5m, 21403);
		c.Check("lo rellena entero sin cerrar fuera: MITIGADO", m.Fvgs[0].Estado, "MITIGADO");

		var m2 = new MotorFvgV2();
		velas.Clear();
		m = m2;
		V(21400, 21402, 21398, 21401);
		V(21401, 21408, 21400, 21407);
		V(21407, 21410, 21405, 21409);
		V(21409, 21409, 21400, 21401);
		c.Check("cierre que lo atraviesa completo: INVALIDADO", m2.Fvgs[0].Estado, "INVALIDADO");

		var lvn = new RepositorioLvn();
		lvn.Aplicar(PruebasV2Lvn.Z(1, 1, 21403, 21403.5m, manual: true));
		var zona = MotorFvgV2.ZonaDeFvg(1, new FvgV2 { Id = 9, Alcista = true, Bajo = 21402, Alto = 21405 }, lvn, p, Tick, bfvg: false, null, "MOD2");
		c.Check("FVG que solapa un LVN: FVG+LVN (calidad 40, origen MANUAL)", $"{zona.Tipo} {zona.Calidad} {zona.LvnOrigen}", "FVG+LVN 40 MANUAL");

		c.Seccion("S5 T22: tres FVG tras la trampa (uno con LVN) = tres zonas BFVG, la del LVN etiquetada BFVG+LVN");
		var cfg = new ConfiguracionSweeps { TamanoTick = Tick };
		cfg.V2.Fvg5m = false;
		var motor = new MotorSweeps(cfg);

		for (var et = new DateTime(2026, 11, 2, 9, 30, 0); et < new DateTime(2026, 11, 2, 9, 45, 0); et = et.AddMinutes(1))
			motor.Procesar(E(et, 21418, 21421, 21417, 21419));

		var nivel = new NivelLiquidez { Id = 99, Precio = 21420, Lado = Lado.Buyside, Grupo = "dia", Origen = "Dia anterior", CreadoUtc = Utc(new DateTime(2026, 11, 2, 8, 0, 0)) };
		var trampa = new EvaluacionBarrido
		{
			Id = 5, Nivel = nivel, Signo = 1, L = 21420, Extremo = 21422, T0Utc = Utc(new DateTime(2026, 11, 2, 9, 44, 30)), ExtremoUtc = Utc(new DateTime(2026, 11, 2, 9, 44, 40)),
			Estado = EstadoBarrido.TrampaA, Congelada = true
		};

		motor.Leer(mm => { mm.Evaluaciones.Add(trampa); return 0; });
		motor.LvnDesdeSuite(PruebasV2Lvn.Z(1, 1, 21412, 21412.5m, manual: false));

		var bajada = new[] { (21421m, 21416m), (21417m, 21410m), (21409m, 21405m), (21404m, 21400m), (21399m, 21395m) };
		var hora = new DateTime(2026, 11, 2, 9, 45, 0);

		foreach (var (h, l) in bajada)
		{
			motor.Procesar(E(hora, h - 0.5m, h, l, l + 0.5m));
			hora = hora.AddMinutes(1);
		}

		var bfvg = motor.Leer(mm => mm.Zonas2.Where(z => z.BarridoId == 5).ToList());
		c.Check("T22 tres zonas BFVG (cada una con su propio análisis de gatillo en S6)", bfvg.Count.ToString(), "3");
		c.Check("T22 la que contiene el LVN es BFVG+LVN con el bonus de confluencia (20 + 10 → 50)",
			string.Join(" ", bfvg.Select(z => $"{z.Tipo}:{z.Calidad}")), "BFVG+LVN:50 BFVG:20 BFVG:20");
		c.Check("T22 todas en la dirección del giro (cortas)", bfvg.All(z => z.Signo == -1).ToString(), "True");
	}
}
