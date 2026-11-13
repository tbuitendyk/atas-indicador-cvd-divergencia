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
		LineasDeSesion(c);
	}

	private static Vela V(int i, DateTime inicioEt, decimal h, decimal l, decimal cierre) => new()
	{
		Indice = i, InicioUtc = Utc(inicioEt), FinUtc = Utc(inicioEt.AddMinutes(1)), Open = cierre, High = h, Low = l, Close = cierre
	};

	/// <summary>G4: líneas de sesión que se detienen al romperse, BOS, NY reciente, alerta con aviso y FVG tras barrido.</summary>
	private static void LineasDeSesion(Comprobador c)
	{
		c.Seccion("v4 G4: líneas de máximo y mínimo de sesión: nacen al terminar, se detienen al romperse; BOS al cierre");
		var m = new MotorLineasSesion();
		var d = new DateTime(2026, 11, 2, 2, 0, 0);
		var velas = new List<Vela> { V(0, d, 21410, 21400, 21405), V(1, d.AddMinutes(1), 21412, 21402, 21404), V(2, d.AddMinutes(2), 21408, 21398, 21401) };
		var londres = new InstanciaSesion { Id = ConfiguracionSweeps.Londres, Nombre = "Londres", DesdeBar = 0, HastaBar = 2, Alto = 21412, Bajo = 21398, Abierta = false };
		m.Vela(velas[2], velas, new[] { londres }, null);
		c.Check("al terminar Londres: una línea en su máximo (vela 1) y otra en su mínimo (vela 2)",
			string.Join(" ", m.Lineas.Select(l => $"{(l.Alto ? "MAX" : "MIN")} {F(l.Precio)} @{l.DesdeBar} viva={l.Viva}")), "MAX 21412 @1 viva=True MIN 21398 @2 viva=True");

		velas.Add(V(3, d.AddMinutes(3), 21411, 21404, 21410));
		c.Check("un trade en el máximo exacto no la rompe", m.Tick(Utc(d.AddMinutes(3).AddSeconds(5)), 21412, 3).Count.ToString(), "0");
		var rotas = m.Tick(Utc(d.AddMinutes(3).AddSeconds(9)), 21412.25m, 3);
		c.Check("un trade un tick más allá la rompe en ese momento (alerta inmediata) y se detiene en esa vela",
			string.Join(" ", rotas.Select(l => $"{l.Nombre} rota@{l.RotaBar} viva={l.Viva}")), "MÁXIMO Londres rota@3 viva=False");
		var r3 = m.Vela(velas[3], velas, Array.Empty<InstanciaSesion>(), null);
		c.Check("la vela cierra por debajo (21410 < 21412): rota pero sin BOS", $"{r3.Count(x => x.Bos)} {m.Lineas[0].BosBar?.ToString() ?? "-"}", "0 -");
		velas.Add(V(4, d.AddMinutes(4), 21416, 21409, 21415));
		var r4 = m.Vela(velas[4], velas, Array.Empty<InstanciaSesion>(), null);
		c.Check("la siguiente cierra más allá (21415): BOS una sola vez", $"{string.Join(" ", r4.Where(x => x.Bos).Select(x => x.Linea.Nombre))} @{m.Lineas[0].BosBar}", "MÁXIMO Londres @4");
		c.Check("el BOS no se repite", m.Vela(V(5, d.AddMinutes(5), 21418, 21414, 21417), velas, Array.Empty<InstanciaSesion>(), null).Count(x => x.Bos).ToString(), "0");

		c.Seccion("v4 G4: NY abierta: línea desde el último máximo / mínimo reciente (pivote), la nueva reemplaza a la anterior");
		var ny = new MotorLineasSesion();
		var t = new DateTime(2026, 11, 2, 9, 30, 0);
		var sesion = new InstanciaSesion { Id = ConfiguracionSweeps.NyAm, Nombre = "NY AM", DesdeBar = 0, Abierta = true };
		var alturas = new[] { 21400m, 21404m, 21410m, 21405m, 21403m, 21408m, 21406m, 21404m, 21414m, 21409m, 21406m };
		var vs = new List<Vela>();

		for (var i = 0; i < alturas.Length; i++)
		{
			vs.Add(V(i, t.AddMinutes(i), alturas[i], alturas[i] - 6, alturas[i] - 2));
			ny.Vela(vs[^1], vs, Array.Empty<InstanciaSesion>(), sesion);
		}

		var altas = ny.Lineas.Where(l => l.Alto).ToList();
		c.Check("pivotes 21410 (09:32), 21408 (09:35, reemplaza a la viva anterior) y 21414 (09:38, rompe la de 21408 y la reemplaza)",
			string.Join(" ", altas.Select(l => $"{F(l.Precio)}@{l.DesdeBar}{(l.Viva ? " viva" : $" rota@{l.RotaBar}")}")), "21408@5 rota@8 21414@8 viva");

		c.Seccion("v4 G4: en el motor: las líneas de Asia, Londres y Premercado y la de NY AM del día anterior se rompen con la subida");
		var s = new SimMotor();
		s.Ruptura();
		var nov = s.M.TomarNovedadesV2();
		var lineas = nov.Where(n => n.Tipo == "LÍNEA DE SESIÓN" && n.Utc >= Utc(new DateTime(2026, 10, 30, 9, 41, 0))).Select(n => $"{n.Linea!.Sesion} {F(n.Linea.Precio)}").ToList();
		c.Check("rupturas de línea durante la subida (máximos 21402 de las sesiones de la noche; 21420 de NY AM del 29)",
			string.Join(" | ", lineas.OrderBy(x => x)), "Asia 21402 | Londres 21402 | NY AM 21420 | Premercado 21402");
		c.Check("BOS: los cierres de 1 minuto más allá", nov.Count(n => n.Tipo == "BOS DE SESIÓN" && n.Utc >= Utc(new DateTime(2026, 10, 30, 9, 41, 0))).ToString(), "4");
		var p = s.M.Leer(m2 => m2.P2);
		var ctx = new ContextoMensaje { Instrumento = "NQ", Contrato = "NQZ6", Mt5 = "MT5: APAGADO", Precio = x => F(x) };
		var alertas = new DespachoAlertasV2().Procesar(nov.Where(n => n.Utc >= Utc(new DateTime(2026, 10, 30, 9, 41, 0))), true, p, ctx)
			.Where(a => a.Categoria == CategoriaAlerta.LineaSesion).ToList();
		c.Check("cada ruptura de línea alerta una vez (ATAS con sonido y aviso; correo y Telegram apagados)",
			$"{alertas.Count} {p.Canales(CategoriaAlerta.LineaSesion)}", "4 CanalesAlerta { Atas = True, Correo = False, Telegram = False }");
		p.LineaAlertaAsia = false;
		c.Check("apagando Asia, su ruptura no alerta",
			new DespachoAlertasV2().Procesar(nov.Where(n => n.Utc >= Utc(new DateTime(2026, 10, 30, 9, 41, 0))), true, p, ctx).Count(a => a.Categoria == CategoriaAlerta.LineaSesion).ToString(), "3");
		p.LineaAlertaAsia = true;

		c.Seccion("v4 G4: FVG solo después de un barrido validado y a favor de su giro");
		var fv = new SimMotor(sinBarridos: true);
		var t0 = Utc(new DateTime(2026, 10, 30, 9, 50, 0));
		var e = new EvaluacionBarrido
		{
			Id = 7, Nivel = new NivelLiquidez { Id = 7, Precio = 21420, Lado = Lado.Buyside, Grupo = "dia", Origen = "Dia anterior" }, Signo = 1, L = 21420, Extremo = 21424,
			T0Utc = t0, Estado = EstadoBarrido.Real, Congelada = true
		};
		var antes = new FvgV2 { Alcista = false, Utc = t0.AddMinutes(-10) };
		var bajista = new FvgV2 { Alcista = false, Utc = t0.AddMinutes(3) };
		var alcista = new FvgV2 { Alcista = true, Utc = t0.AddMinutes(3) };
		c.Check("sin barrido validado ningún FVG es relevante", fv.M.Leer(m2 => m2.FvgRelevante(bajista)).ToString(), "False");
		fv.M.Leer(m2 => { m2.Evaluaciones.Add(e); return 0; });
		c.Check("tras un BARRIDO REAL de un máximo: el FVG bajista posterior sí; el anterior y el alcista no",
			fv.M.Leer(m2 => $"{m2.FvgRelevante(bajista)} {m2.FvgRelevante(antes)} {m2.FvgRelevante(alcista)}"), "True False False");
		e.Estado = EstadoBarrido.Ruptura;
		c.Check("una RUPTURA no es un barrido validado", fv.M.Leer(m2 => m2.FvgRelevante(bajista)).ToString(), "False");
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
