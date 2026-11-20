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
		CargaRapida(c);
		ZonasPersistentes(c);
	}

	/// <summary>v5 F4: los LVN y EDGE del Thermal se guardan y el Sweep Pro los usa con el Thermal cerrado.</summary>
	private static void ZonasPersistentes(Comprobador c)
	{
		c.Seccion("v5 F4: LVN y EDGE guardados por el LVN Thermal Profile; el Sweep Pro los usa aunque el Thermal esté cerrado");
		var carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssp-zonas-" + Guid.NewGuid().ToString("N"));
		var antes = ZonasGuardadas.Carpeta;
		ZonasGuardadas.Carpeta = carpeta;

		try
		{
			var t = Utc(new DateTime(2026, 11, 19, 9, 40, 0));
			var manual = new LvnZone
			{
				Id = 11, Fuente = FuenteSuite.Thermal, Utc = t, CambioUtc = t, Version = 3, RangoId = 2, Bajo = 21380, Alto = 21381, Centro = 21380.5m, Manual = true,
				Estado = "ACTIVO", Score = 77, PrecioOriginal = 21380.25m
			};
			var borrado = new LvnZone { Id = 12, Fuente = FuenteSuite.Thermal, Utc = t, CambioUtc = t, Version = 2, RangoId = 2, Bajo = 21390, Alto = 21391, Centro = 21390.5m, Estado = "BORRADO" };
			var edge = new EdgeZone
			{
				Id = 21, Fuente = FuenteSuite.Thermal, Utc = t, CambioUtc = t, Version = 1, RangoId = 2, Precio = 21360, Bajo = 21359.5m, Alto = 21360.5m,
				Lado = EdgeZone.PisoHvn, Estado = "ACTIVO", Manual = true
			};
			ZonasGuardadas.Guardar("MNQZ6", new[] { manual, borrado }, new[] { edge }, t);
			var leido = ZonasGuardadas.Leer("MNQZ6");
			c.Check("se guardan las vigentes (no la BORRADO) con sus datos (manual, precio original, rango, lado del EDGE)",
				leido is null ? "nada" : $"{leido.Lvn.Count} {leido.Lvn[0].Id} {F(leido.Lvn[0].Centro)} {leido.Lvn[0].Manual} {F(leido.Lvn[0].PrecioOriginal ?? 0)} {leido.Lvn[0].RangoId} | " +
					$"{leido.Edge.Count} {F(leido.Edge[0].Precio)} {leido.Edge[0].Lado} {leido.Edge[0].Manual}",
				$"1 11 21380.5 True 21380.25 2 | 1 21360 {EdgeZone.PisoHvn} True");
			c.Check("sin archivo para otro instrumento: nada", (ZonasGuardadas.Leer("ESZ6") is null).ToString(), "True");
			System.IO.File.WriteAllText(ZonasGuardadas.Archivo("YMZ6"), "{ dañado");
			c.Check("un archivo dañado no rompe nada", (ZonasGuardadas.Leer("YMZ6") is null).ToString(), "True");

			// Lo que hace la carcasa al entrar en vivo, con el Thermal cerrado (sin latido).
			var s = new SimMotor(sinBarridos: true);
			s.Fuentes.Estados[FuenteSuite.Thermal] = EstadoFuente.NoCargado;
			s.M.Leer(m =>
			{
				foreach (var z in leido!.Lvn)
					m.LvnDesdeSuite(z);

				foreach (var z in leido.Edge)
					m.EdgeDesdeSuite(z);

				return 0;
			});
			c.Check("el Sweep Pro tiene el LVN manual y el EDGE aunque el Thermal no esté cargado", s.M.Leer(m => $"{m.Lvn.Activas.Count()} {m.Edge.Activas.Count()} {m.HayFuenteLvn}"), "1 1 False");
			var panel = s.M.Panel(new OpcionesPanel());
			c.Check("el panel lo dice (y no 'SIN FUENTE LVN')",
				$"{panel.Lineas.Any(l => l.Texto.StartsWith("LVN Thermal Profile cerrado", StringComparison.Ordinal))} {panel.Lineas.Any(l => l.Texto.StartsWith("SIN FUENTE LVN", StringComparison.Ordinal))}", "True False");
		}
		finally
		{
			ZonasGuardadas.Carpeta = antes;

			if (System.IO.Directory.Exists(carpeta))
				System.IO.Directory.Delete(carpeta, true);
		}
	}

	/// <summary>v5 F3: carga rápida: solo los últimos días operativos llevan detalle; los anteriores, solo niveles.</summary>
	private static void CargaRapida(Comprobador c)
	{
		c.Seccion("v5 F3: carga rápida: detalle (footprint, barras de volumen, clasificador) solo en los últimos días operativos");
		var lunes = Utc(new DateTime(2026, 11, 16, 9, 45, 0));
		c.Check("2 días desde un lunes: desde el jueves 18:00 ET (el viernes y el lunes; el fin de semana no cuenta)",
			TiempoV2.HoraEt(Zonas.InicioDetalle(lunes, 2), "ddd dd HH:mm"), "Thu 12 18:00 ET");
		c.Check("1 día desde un miércoles: desde el martes 18:00 ET", TiempoV2.HoraEt(Zonas.InicioDetalle(Utc(new DateTime(2026, 11, 18, 11, 0, 0)), 1), "ddd dd HH:mm"), "Tue 17 18:00 ET");

		// 15 días operativos de velas de 1 minuto con footprint (como las de ATAS), con un vaivén diario.
		var velas = new List<VelaEntrada>();
		var dia = new DateTime(2026, 10, 26);
		var rnd = new Random(7);
		var precio = 21400m;

		for (var d = 0; d < 21; d++, dia = dia.AddDays(1))
		{
			if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
				continue;

			for (var t = dia.AddHours(-6); t < dia.AddHours(17); t = t.AddMinutes(1))
			{
				var o = precio;
				precio += (rnd.Next(0, 9) - 4) * Tick;
				var (h, l) = (Math.Max(o, precio) + Tick, Math.Min(o, precio) - Tick);
				var e = new VelaEntrada { InicioUtc = Utc(t), Open = o, High = h, Low = l, Close = precio, Volume = 0, Delta = 0 };

				for (var p = l; p <= h; p += Tick)
				{
					var (bid, ask) = (rnd.Next(5, 40), rnd.Next(5, 40));
					e.Niveles.Add(new NivelVela(p, bid, ask));
					(e.Volume, e.Delta) = (e.Volume + bid + ask, e.Delta + ask - bid);
				}

				velas.Add(e);
			}
		}

		(long Ms, int Barras, int Activos, int Rancios) Correr(DateTime desde)
		{
			var cfg = new ConfiguracionSweeps { TamanoTick = Tick, MinutosGrafico = 1 };
			cfg.V2.VolumePerBar = 500;
			var m = new MotorSweeps(cfg) { DetalleDesdeUtc = desde };
			var reloj = System.Diagnostics.Stopwatch.StartNew();

			foreach (var e in velas)
				m.Procesar(e);

			reloj.Stop();
			return m.Leer(x =>
			{
				var rancios = x.Niveles.Niveles.Count(n => n.Activo && x.Velas.Skip(n.CreadoBar + 1).Any(v => n.Lado == Lado.Buyside ? v.High > n.Precio : v.Low < n.Precio));
				return (reloj.ElapsedMilliseconds, x.BarrasV.Barras.Count, x.Niveles.Niveles.Count(n => n.Activo), rancios);
			});
		}

		var completo = Correr(DateTime.MinValue);
		var rapido = Correr(Zonas.InicioDetalle(velas[^1].InicioUtc, 2));
		c.Check($"barras de volumen construidas: {completo.Barras} con todo el historial, {rapido.Barras} con 2 días (de 15); tiempo {completo.Ms} ms → {rapido.Ms} ms",
			(rapido.Barras * 4 < completo.Barras).ToString(), "True");
		// Con todo el historial también quedan vigentes los niveles cruzados fuera de la ventana de barridos (comportamiento
		// de siempre); los días sin detalle retiran los que el precio tomó, así que nunca quedan más.
		c.Check("la carga rápida no deja más niveles vigentes ya superados que el historial completo", (rapido.Rancios <= completo.Rancios).ToString(), "True");
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
