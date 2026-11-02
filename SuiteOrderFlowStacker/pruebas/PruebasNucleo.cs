using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using OrderFlowSuite.Nucleo;

using static Datos;

/// <summary>Pruebas de los motores con datos sintéticos (trades, DOM, MBO y velas con footprint).</summary>
internal static class PruebasNucleo
{
	public static void Ejecutar(Comprobador c)
	{
		Sesiones(c);
		Imbalance(c);
		IcebergInferido(c);
		IcebergNativo(c);
		IcebergSintetico(c);
		StopsYBarridos(c);
		HistoriaSetupB(c);
		SetupDyFiltros(c);
		Grabacion(c);
	}

	private static string F(decimal? v) => v?.ToString("0.##", CultureInfo.InvariantCulture) ?? "null";

	#region Sesiones

	private static void Sesiones(Comprobador c)
	{
		c.Seccion("Sesiones (hora de Nueva York, con cambio de horario)");
		var s = new ClasificadorSesion();

		c.Check("19:00 ET = Asia", s.De(Utc(new DateTime(2026, 10, 29, 19, 0, 0))).ToString(), "Asia");
		c.Check("01:59 ET = Asia", s.De(Utc(new DateTime(2026, 10, 30, 1, 59, 0))).ToString(), "Asia");
		c.Check("03:00 ET = Londres", s.De(Utc(new DateTime(2026, 10, 30, 3, 0, 0))).ToString(), "Londres");
		c.Check("08:29 ET = Londres (premercado 60 min)", s.De(Utc(new DateTime(2026, 10, 30, 8, 29, 0))).ToString(), "Londres");
		c.Check("08:30 ET = Premercado", s.De(Utc(new DateTime(2026, 10, 30, 8, 30, 0))).ToString(), "Premercado");
		c.Check("09:30 ET = AperturaNY", s.De(Utc(new DateTime(2026, 10, 30, 9, 30, 0))).ToString(), "AperturaNY");
		c.Check("11:00 ET = Mediodia", s.De(Utc(new DateTime(2026, 10, 30, 11, 0, 0))).ToString(), "Mediodia");
		c.Check("15:59 ET = Cierre", s.De(Utc(new DateTime(2026, 10, 30, 15, 59, 0))).ToString(), "Cierre");
		c.Check("16:30 ET = FueraDeHorario", s.De(Utc(new DateTime(2026, 10, 30, 16, 30, 0))).ToString(), "FueraDeHorario");

		// Horario de verano: 09:30 ET = 13:30 UTC en octubre y 14:30 UTC en noviembre (el cambio fue el 1-nov-2026).
		c.Check("Apertura NY en UTC (verano, 30-oct)", Utc(new DateTime(2026, 10, 30, 9, 30, 0)).ToString("HH:mm"), "13:30");
		c.Check("Apertura NY en UTC (invierno, 2-nov)", Utc(new DateTime(2026, 11, 2, 9, 30, 0)).ToString("HH:mm"), "14:30");
		c.Check("14:30 UTC del 2-nov = AperturaNY", s.De(new DateTime(2026, 11, 2, 14, 30, 0, DateTimeKind.Utc)).ToString(), "AperturaNY");

		s.PremercadoMinutos = 30;
		c.Check("premercado 30 min: 08:45 = Londres", s.De(Utc(new DateTime(2026, 10, 30, 8, 45, 0))).ToString(), "Londres");
		c.Check("premercado 30 min: 09:00 = Premercado", s.De(Utc(new DateTime(2026, 10, 30, 9, 0, 0))).ToString(), "Premercado");

		var t = new TablaUmbrales();
		c.Check("umbral NQ Apertura NY", $"{t.De(Sesion.AperturaNY, false).MinEjecutado}/{t.De(Sesion.AperturaNY, false).AltaProbabilidad}/{t.De(Sesion.AperturaNY, false).MinRecargas}", "150/400/5");
		c.Check("umbral NQ Premercado", $"{t.De(Sesion.Premercado, false).MinEjecutado}/{t.De(Sesion.Premercado, false).AltaProbabilidad}/{t.De(Sesion.Premercado, false).MinRecargas}", "100/300/4");
		c.Check("umbral MNQ Apertura NY (NQ x 0,5)", $"{t.De(Sesion.AperturaNY, true).MinEjecutado}/{t.De(Sesion.AperturaNY, true).AltaProbabilidad}", "75/200");
	}

	#endregion

	#region Imbalance

	private static void Imbalance(Comprobador c)
	{
		c.Seccion("StackedImbalancePro: diagonal, stacked, NUEVA -> PROBADA -> ROTA");
		var ev = new RegistroEventos { EnVivo = true };
		var m = new MotorImbalance(ev) { Tick = Tick };
		var t0 = new DateTime(2026, 10, 30, 10, 0, 0);

		// Compras agresivas 60 en 100.25-100.75 contra ventas 10 una fila más abajo: 600 % en 3 niveles seguidos.
		var v = V(t0, 100, 101, 100, 100.75m, 10, (100.00m, 10, 5), (100.25m, 10, 60), (100.50m, 10, 60), (100.75m, 5, 60), (101.00m, 5, 5));
		var (compra, venta) = m.Imbalances(v, true);
		c.Check("imbalances de compra (ticks)", string.Join(",", compra.Select(x => (x * Tick).ToString(CultureInfo.InvariantCulture))), "100.25,100.50,100.75");
		c.Check("sin imbalances de venta", venta.Count.ToString(), "0");

		m.Agregar(v, Sesion.Mediodia, true);
		c.Check("una zona stacked de compra", m.Zonas.Count.ToString(), "1");
		var z = m.Zonas[0];
		c.Check("zona 100.25-100.75, 3 niveles", $"{F(z.Bajo)}-{F(z.Alto)} {z.Niveles}", "100.25-100.75 3");
		c.Check("texto de la zona", z.Evento.Texto, "STACKED IMB COMPRA | 100.25-100.75 | 3 niv | 600% | NUEVA | 10:00:00");
		c.Check("una novedad al nacer", ev.TomarNovedades().Count.ToString(), "1");

		// Sale por arriba, vuelve y respeta: PROBADA (con su alerta).
		m.Agregar(V(t0.AddMinutes(1), 101, 102, 101, 101.5m), Sesion.Mediodia, true);
		c.Check("sigue NUEVA al salir", z.Estado, "NUEVA");
		m.Precio(100.50m, Utc(t0.AddMinutes(2).AddSeconds(5)));
		c.Check("vuelve en vivo: PROBADA", z.Estado, "PROBADA");
		c.Check("alerta al volver a la zona NUEVA", ev.TomarNovedades().Single().Estado, "PROBADA");

		// Cierre 2 ticks por debajo de la zona = ROTA.
		m.Agregar(V(t0.AddMinutes(2), 100.5m, 100.75m, 99.5m, 99.75m), Sesion.Mediodia, true);
		c.Check("cierre 99.75 (2 ticks bajo 100.25): ROTA", z.Estado, "ROTA");
		c.Check("ROTA deja de extenderse", (z.FinUtc is not null).ToString(), "True");

		// Lado débil a cero y volumen mínimo fuera de RTH.
		var cero = V(t0, 200, 201, 200, 201, 0, (200.00m, 0, 0), (200.25m, 0, 20), (200.50m, 0, 20), (200.75m, 0, 20), (201.00m, 0, 0));
		c.Check("débil 0 con IgnoreZeroSide: cuenta (exige el mínimo)", m.Imbalances(cero, true).Compra.Count.ToString(), "3");
		m.P.IgnoreZeroSide = false;
		c.Check("débil 0 sin IgnoreZeroSide: no cuenta", m.Imbalances(cero, true).Compra.Count.ToString(), "0");
		m.P.IgnoreZeroSide = true;

		var flojo = V(t0, 300, 301, 300, 301, 0, (300.00m, 2, 0), (300.25m, 2, 10), (300.50m, 2, 10), (300.75m, 2, 10), (301.00m, 0, 0));
		c.Check("10 contratos: no llega a 15 en RTH", m.Imbalances(flojo, true).Compra.Count.ToString(), "0");
		c.Check("10 contratos: sí fuera de RTH (15 x 0,5)", m.Imbalances(flojo, false).Compra.Count.ToString(), "3");

		// Etiqueta "con iceberg".
		var ev2 = new RegistroEventos { EnVivo = true };
		var m2 = new MotorImbalance(ev2) { Tick = Tick };
		m2.Agregar(v, Sesion.Mediodia, true);
		m2.MarcarIceberg(Lado.Compra, 100.50m, Utc(t0.AddSeconds(70)));
		c.Check("con iceberg dentro de la zona", m2.Zonas[0].Evento.Texto.Contains("| NUEVA | con iceberg |").ToString(), "True");
		m2.MarcarIceberg(Lado.Venta, 100.50m, Utc(t0.AddSeconds(80)));
		c.Check("iceberg del otro lado no marca", ev2.TomarNovedades().Count.ToString(), "2");
	}

	#endregion

	#region Icebergs

	private static MotorIceberg Iceberg(RegistroEventos ev) => new(ev) { Tick = Tick };

	private static void IcebergInferido(Comprobador c)
	{
		c.Seccion("IcebergTrackerPro INFERIDO (sin MBO): ejecutado >> visible en el DOM");
		var ev = new RegistroEventos { EnVivo = true };
		var m = Iceberg(ev);
		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 10));
		m.Dom(new CambioDom(t, 21396.25m, 20, false));

		// 16 ventas de 10 contra el BID de 21396.25 (visible 20, se repone cada vez).
		for (var i = 0; i < 16; i++)
		{
			var ti = t.AddSeconds(2 + i * 0.3);
			m.Trade(Tr(ti, 21396.25m, 10, Lado.Venta));
			m.Dom(new CambioDom(ti.AddMilliseconds(5), 21396.25m, 10, false));
			m.Dom(new CambioDom(ti.AddMilliseconds(10), 21396.25m, 20, false));

			if (i == 13)
				c.Check("140 ejecutados (< 150 de Apertura NY): aún no", m.Icebergs.Count.ToString(), "0");
		}

		c.Check("modo Inferido (sin MBO)", m.TextoModo, "Inferido (sin MBO)");
		c.Check("150 ejecutados x 7,5 visible: iceberg", m.Icebergs.Count.ToString(), "1");
		var i0 = m.Icebergs[0];
		c.Check("lado COMPRA (pasivo en el BID), precio", $"{i0.Lado} {F(i0.Precio)} {i0.Modo}", "Compra 21396.25 Inferido");
		c.Check("ejecutado y ratio", $"{F(i0.Ejecutado)} {F(i0.Ratio)}", "160 8");
		c.Check("texto", i0.Evento.Texto, "ICEBERG COMPRA INFERIDO | 21396.25 | 160c | 16 recargas | 8:1 | ACTIVO | NEUTRO | 09:42:16");

		// El precio sube 4 ticks y deja de recargarse 10 s: AGUANTANDO.
		m.Trade(Tr(t.AddSeconds(8), 21397.25m, 5, Lado.Compra));
		c.Check("sube +4 pero recargó hace < 10 s: sigue ACTIVO", i0.Estado, "ACTIVO");
		m.Tiempo(t.AddSeconds(17));
		c.Check("sin recargas 10 s y +4 ticks: AGUANTANDO", i0.Estado, "AGUANTANDO");
		c.Check("texto AGUANTANDO (+4 ticks)", i0.Evento.Texto.Contains("| AGUANTANDO (+4 ticks) |").ToString(), "True");

		// Atraviesa el nivel 2 ticks: ROTO -> RUPTURA.
		m.Trade(Tr(t.AddSeconds(20), 21395.75m, 5, Lado.Venta));
		c.Check("atraviesa 2 ticks: ROTO", i0.Estado, "ROTO");
		c.Check("sesgo RUPTURA", i0.Sesgo, "RUPTURA");
		c.Check("una alerta por estado (nace, AGUANTANDO, ROTO)", string.Join(",", ev.TomarNovedades().Select(n => n.Estado)), "ACTIVO,AGUANTANDO,ROTO");

		// Desplazamiento: si el precio cede 3 ticks mientras se ejecuta, no es iceberg.
		var ev2 = new RegistroEventos { EnVivo = true };
		var m2 = Iceberg(ev2);
		m2.Dom(new CambioDom(t, 21400m, 10, false));

		for (var i = 0; i < 20; i++)
		{
			m2.Trade(Tr(t.AddSeconds(1 + i), 21400m, 10, Lado.Venta));

			if (i == 2)
				m2.Trade(Tr(t.AddSeconds(1.5 + i), 21399.25m, 1, Lado.Venta));
		}

		c.Check("desplazamiento 3 ticks (> 2): no hay iceberg", m2.Icebergs.Count.ToString(), "0");

		// SoloInferido ignora el MBO.
		var m3 = Iceberg(new RegistroEventos());
		m3.P.ModoMbo = ModoMbo.SoloInferido;
		m3.Mbo(new MensajeMbo(t, TipoMbo.Nueva, 1, 21400m, 5, false, 1));
		c.Check("SoloInferido: el MBO no cambia el modo", m3.TextoModo, "Inferido (sin MBO)");
	}

	private static void IcebergNativo(Comprobador c)
	{
		c.Seccion("IcebergTrackerPro NATIVO (MBO): misma orden, visible restaurado, peor prioridad");
		var ev = new RegistroEventos { EnVivo = true };
		var m = Iceberg(ev);

		// Asia (40 / 120 / 3): clip 5 que se recarga.
		var t = Utc(new DateTime(2026, 10, 29, 20, 0, 0));
		m.Mbo(new MensajeMbo(t, TipoMbo.Nueva, 7, 21450.25m, 5, false, 100));

		for (var i = 1; i <= 9; i++)
		{
			var ti = t.AddSeconds(i);
			m.Trade(Tr(ti, 21450.25m, 5, Lado.Venta, idPasivo: 7));
			m.Mbo(new MensajeMbo(ti.AddMilliseconds(2), TipoMbo.Cambio, 7, 21450.25m, 5, false, 100 + i * 10));
		}

		c.Check("modo MBO", m.TextoModo, "MBO");
		c.Check("iceberg NATIVO", m.Icebergs.Count == 1 ? m.Icebergs[0].Modo.ToString() : "ninguno", "MBO");
		var i0 = m.Icebergs[0];
		c.Check("ejecutado 45, clip 5, 9 recargas, ratio 9", $"{F(i0.Ejecutado)} {F(i0.Clip)} {i0.Recargas} {F(i0.Ratio)}", "45 5 9 9");
		c.Check("texto NATIVO", i0.Evento.Texto, "ICEBERG COMPRA NATIVO | 21450.25 | 45c | 9 recargas | 9:1 | ACTIVO | NEUTRO | 20:00:09");
		c.Check("se confirmó al llegar a 40 ejecutados y 3 recargas (no antes)", i0.Evento.Utc.ToString("ss"), "08");

		// Más tamaño con peor prioridad SIN ejecución: es una modificación, no una recarga.
		var antes = i0.Recargas;
		m.Mbo(new MensajeMbo(t.AddSeconds(30), TipoMbo.Cambio, 7, 21450.25m, 8, false, 999));
		c.Check("modificación sin trade: no suma recarga", (i0.Recargas == antes).ToString(), "True");

		// Borrada sin trade: CANCELADO.
		m.Mbo(new MensajeMbo(t.AddSeconds(40), TipoMbo.Borrada, 7, 21450.25m, 0, false, 999));
		c.Check("borrada sin ejecutarse: CANCELADO", i0.Estado, "CANCELADO");

		// Iceberg de venta en el ASK.
		var m2 = Iceberg(new RegistroEventos());
		m2.Mbo(new MensajeMbo(t, TipoMbo.Nueva, 8, 21460m, 4, true, 1));

		for (var i = 1; i <= 12; i++)
		{
			var ti = t.AddSeconds(i);
			m2.Trade(Tr(ti, 21460m, 4, Lado.Compra));
			m2.Mbo(new MensajeMbo(ti.AddMilliseconds(3), TipoMbo.Cambio, 8, 21460m, 4, true, 1 + i));
		}

		c.Check("iceberg VENTA en el ASK (sin IdPasivo: por las recargas)", m2.Icebergs.Count == 1 ? $"{m2.Icebergs[0].Lado} {F(m2.Icebergs[0].Ejecutado)}" : "ninguno", "Venta 48");
	}

	private static void IcebergSintetico(Comprobador c)
	{
		c.Seccion("IcebergTrackerPro SINTÉTICO (MBO): orden nueva parecida en < 50 ms tras el llenado");
		var ev = new RegistroEventos { EnVivo = true };
		var m = Iceberg(ev);
		var t = Utc(new DateTime(2026, 10, 29, 21, 0, 0));
		long id = 100;
		m.Mbo(new MensajeMbo(t, TipoMbo.Nueva, id, 21418.50m, 10, true, 1));

		for (var i = 1; i <= 4; i++)
		{
			var ti = t.AddSeconds(i);
			m.Trade(Tr(ti, 21418.50m, 10, Lado.Compra));
			m.Mbo(new MensajeMbo(ti.AddMilliseconds(1), TipoMbo.Borrada, id, 21418.50m, 0, true, 1));
			id++;
			m.Mbo(new MensajeMbo(ti.AddMilliseconds(30), TipoMbo.Nueva, id, 21418.50m, i == 2 ? 11 : 10, true, 1 + i));
		}

		c.Check("SINTÉTICO tras 4 llenados (10+10+11+10 = 41c, 4 recargas)", m.Icebergs.Count == 1 ? $"{m.Icebergs[0].Modo} {F(m.Icebergs[0].Ejecutado)} {m.Icebergs[0].Recargas}" : "ninguno", "Sintetico 41 4");
		c.Check("texto SINTÉTICO", m.Icebergs.FirstOrDefault()?.Evento.Texto.StartsWith("ICEBERG VENTA SINTÉTICO | 21418.50 | 41c | 4 recargas | 4:1 | ACTIVO") ?? false, "");

		// Tamaño fuera de tolerancia (30 %) o demasiado tarde (80 ms): no encadena.
		var m2 = Iceberg(new RegistroEventos());
		id = 500;
		m2.Mbo(new MensajeMbo(t, TipoMbo.Nueva, id, 21418.50m, 10, true, 1));

		for (var i = 1; i <= 6; i++)
		{
			var ti = t.AddSeconds(i);
			m2.Trade(Tr(ti, 21418.50m, 10, Lado.Compra));
			m2.Mbo(new MensajeMbo(ti.AddMilliseconds(1), TipoMbo.Borrada, id, 21418.50m, 0, true, 1));
			id++;
			m2.Mbo(new MensajeMbo(ti.AddMilliseconds(i % 2 == 0 ? 80 : 30), TipoMbo.Nueva, id, 21418.50m, i % 2 == 0 ? 10 : 13, true, 1 + i));
		}

		c.Check("tamaño +30 % o 80 ms: no es sintético", m2.Icebergs.Count.ToString(), "0");

		// Orden cancelada (sin trade) en vez de llenada: no cuenta como llenado.
		var m3 = Iceberg(new RegistroEventos());
		id = 900;
		m3.Mbo(new MensajeMbo(t, TipoMbo.Nueva, id, 21418.50m, 10, true, 1));

		for (var i = 1; i <= 6; i++)
		{
			var ti = t.AddSeconds(i);
			m3.Mbo(new MensajeMbo(ti, TipoMbo.Borrada, id, 21418.50m, 0, true, 1));
			id++;
			m3.Mbo(new MensajeMbo(ti.AddMilliseconds(20), TipoMbo.Nueva, id, 21418.50m, 10, true, 1 + i));
		}

		c.Check("cancelar y reponer sin trades (spoofing): no es iceberg", m3.Icebergs.Count.ToString(), "0");
	}

	#endregion

	#region Stops y barridos

	/// <summary>
	/// Día anterior (29-oct) con mínimo 21398.50 (= PDL del 30-oct), overnight en 21408-21412 y RTH del 30-oct de
	/// 09:30 a 09:41 alrededor de 21410.
	/// </summary>
	private static MotorSuite Suite(bool conMaster)
	{
		var s = new MotorSuite(new ConfiguracionSuite(), conMaster);
		s.Configurar(Tick, false);

		foreach (var v in Planas(new DateTime(2026, 10, 29, 9, 30, 0), new DateTime(2026, 10, 29, 10, 31, 0), 21420))
			s.Vela(v);

		s.Vela(V(new DateTime(2026, 10, 29, 10, 31, 0), 21420, 21421, 21398.50m, 21419));

		foreach (var v in Planas(new DateTime(2026, 10, 29, 18, 0, 0), new DateTime(2026, 10, 29, 18, 6, 0), 21410))
			s.Vela(v);

		foreach (var v in Planas(new DateTime(2026, 10, 30, 9, 30, 0), new DateTime(2026, 10, 30, 9, 42, 0), 21410))
			s.Vela(v);

		s.Eventos.EnVivo = true;
		return s;
	}

	private static void StopsYBarridos(Comprobador c)
	{
		c.Seccion("Contexto, StopsTrackerPro y SweepsTrackerPro");
		var s = Suite(false);
		c.Check("PDH / PDL del día anterior", $"{F(s.Contexto.Pdh)} / {F(s.Contexto.Pdl)}", "21422 / 21398.5");
		c.Check("overnight ONH / ONL", $"{F(s.Contexto.Onh)} / {F(s.Contexto.Onl)}", "21412 / 21408");
		c.Check("IB aún sin terminar a las 09:42 (no es zona)", s.Contexto.NivelesLiquidez(false, false).Any(n => n.Nombre.StartsWith("IB")).ToString(), "False");
		c.Check("perfil RTH: POC en 21410", F(s.Contexto.PerfilRth?.PrecioPoc), "21410");
		c.Check("números redondos alrededor del precio", string.Join(" ", s.Contexto.NivelesLiquidez(true, false).Where(n => n.Nombre.EndsWith("R")).Select(n => n.Nombre)), "21350R 21400R 21450R 21500R");

		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 11));
		Cascada(s, t);
		Barrido(s, t.AddMilliseconds(207), 777);
		s.Trade(Tr(t.AddSeconds(1), 21396.75m, 1, Lado.Compra));

		c.Check("una cascada de stops", s.Stops.Cascadas.Count.ToString(), "1");
		var cs = s.Stops.Cascadas[0];
		c.Check("texto cascada (EVALUANDO)", cs.Evento.Texto, "STOPS TOMADOS VENTA | 495c | 21398.50-21396.50 | bajo PDL | EVALUANDO | 09:42:11");
		c.Check("un barrido reconstruido por AggressorExchangeOrderId", s.Barridos.Barridos.Count.ToString(), "1");
		var b = s.Barridos.Barridos[0];
		c.Check("texto barrido (EVALUANDO)", b.Evento.Texto, "BARRIDO VENTA | 8 niveles | 185c | barrió PDL 21398.50 | EVALUANDO | 09:42:11");
		c.Check("modo del barrido: MBO (con id de agresor)", b.Evento.Modo.ToString(), "MBO");

		// Vuelve dentro a los 22 s: LIQUIDEZ y TRAMPA (impulso alcista).
		s.Trade(Tr(t.AddSeconds(22), 21398.75m, 2, Lado.Compra));
		c.Check("barrido -> LIQUIDEZ", b.Evento.Texto, "BARRIDO LIQUIDEZ VENTA | 8 niveles | 185c | barrió PDL 21398.50 | volvió en 22 s | 09:42:11");
		c.Check("stops -> TRAMPA, impulso alcista", cs.Evento.Texto, "STOPS TOMADOS VENTA | 495c | 21398.50-21396.50 | bajo PDL | TRAMPA (volvió en 22 s) | 09:42:11 | IMPULSO ALCISTA");

		// Sin vuelta: RUPTURA y CONTINUACIÓN.
		var s2 = Suite(false);
		Cascada(s2, t);
		Barrido(s2, t.AddMilliseconds(207), 778);
		s2.Trade(Tr(t.AddSeconds(5), 21396m, 1, Lado.Venta));
		s2.Tiempo(t.AddSeconds(31));
		c.Check("31 s sin volver: RUPTURA", s2.Barridos.Barridos.Single().Tipo, "RUPTURA");
		c.Check("31 s: stops aún EVALUANDO (60 s)", s2.Stops.Cascadas.Single().Estado, "EVALUANDO");
		s2.Tiempo(t.AddSeconds(61));
		c.Check("61 s: CONTINUACIÓN, impulso bajista", s2.Stops.Cascadas.Single().Evento.Texto.EndsWith("| CONTINUACIÓN | 09:42:11 | IMPULSO BAJISTA").ToString(), "True");

		// Una cascada lejos de toda zona no cuenta (RequireZone).
		var s3 = Suite(false);
		var lejos = Utc(new DateTime(2026, 10, 30, 9, 50, 0));

		for (var i = 0; i < 5; i++)
			s3.Trade(Tr(lejos.AddMilliseconds(i * 20), 21430m - i * 0.5m, 40, Lado.Venta));

		s3.Tiempo(lejos.AddSeconds(1));
		c.Check("cascada lejos de las zonas: ignorada", s3.Stops.Cascadas.Count.ToString(), "0");
		s3.Cfg.Stops.RequireZone = false;

		for (var i = 0; i < 5; i++)
			s3.Trade(Tr(lejos.AddSeconds(5).AddMilliseconds(i * 20), 21430m - i * 0.5m, 40, Lado.Venta));

		s3.Tiempo(lejos.AddSeconds(6));
		c.Check("sin RequireZone: 'sin zona'", s3.Stops.Cascadas.SingleOrDefault()?.Evento.Texto.Contains("| sin zona |") ?? false, "");

		// Barrido INFERIDO (sin id de agresor: mismo milisegundo) y aviso de zona probable.
		var s4 = Suite(false);
		var ti = Utc(new DateTime(2026, 10, 30, 9, 45, 0));
		s4.Trade(Tr(ti, 21399.25m, 1, Lado.Compra));
		c.Check("aviso: zona probable PDL a 3 ticks (no suena)", s4.Eventos.Todos.Where(e => e.Informativo).Select(e => e.Texto).LastOrDefault() ?? "", "Zona probable de stops PDL 21398.50 a 3 ticks | 09:45:00");
		c.Check("el aviso no es una novedad (sin alerta ni CSV)", s4.Eventos.TomarNovedades().Count.ToString(), "0");

		var ms = ti.AddSeconds(3);

		foreach (var p in new[] { 21399.00m, 21398.75m, 21398.50m, 21398.25m, 21398.00m })
			s4.Trade(Tr(ms, p, 12, Lado.Venta));

		s4.Tiempo(ms.AddSeconds(1));
		c.Check("barrido INFERIDO (5 niveles, 60c, mismo ms)", s4.Barridos.Barridos.SingleOrDefault() is { } bi ? $"{bi.Evento.Modo} {bi.Niveles} {F(bi.Volumen)} {bi.Nivel?.Nombre}" : "ninguno", "Inferido 5 60 PDL");

		// CONTRA ICEBERG: un barrido de venta que se frena en un iceberg de compra vivo.
		var s5 = Suite(false);
		var tc = Utc(new DateTime(2026, 10, 30, 9, 50, 0));
		Absorber(s5, tc, 21396.25m, 16);
		c.Check("iceberg de compra vivo en 21396.25", s5.Icebergs.Icebergs.Count(i => i.Vivo).ToString(), "1");
		Barrido(s5, tc.AddSeconds(8), 999, 21397.50m, 21396.25m, 20);
		s5.Tiempo(tc.AddSeconds(9));
		c.Check("barrido CONTRA ICEBERG", s5.Barridos.Barridos.SingleOrDefault()?.Evento.Texto ?? "", "BARRIDO CONTRA ICEBERG VENTA | 6 niveles | 120c | frenado en 21396.25 | 09:50:08");
	}

	/// <summary>Cascada de ventas 21398.25 -> 21396.50 (310c en 200 ms, sin id de agresor).</summary>
	private static void Cascada(MotorSuite s, DateTime t)
	{
		var precios = new[] { (21398.25m, 60m), (21398.00m, 50m), (21397.75m, 50m), (21397.50m, 50m), (21397.00m, 50m), (21396.50m, 50m) };

		for (var i = 0; i < precios.Length; i++)
			s.Trade(Tr(t.AddMilliseconds(i * 40), precios[i].Item1, precios[i].Item2, Lado.Venta));
	}

	/// <summary>Orden agresiva de venta (un id) que barre de 'desde' a 'hasta' en el mismo milisegundo.</summary>
	private static void Barrido(MotorSuite s, DateTime t, long id, decimal desde = 21398.50m, decimal hasta = 21396.75m, decimal porNivel = 0)
	{
		var precios = new List<decimal>();

		for (var p = desde; p >= hasta; p -= Tick)
			precios.Add(p);

		var volumenes = porNivel > 0 ? precios.Select(_ => porNivel).ToArray() : new[] { 25m, 25, 25, 20, 20, 25, 25, 20 };

		for (var i = 0; i < precios.Count; i++)
			s.Trade(Tr(t, precios[i], volumenes[i], Lado.Venta, idAgresor: id));
	}

	/// <summary>Ventas de 10 contra un BID de 20 que se repone (iceberg inferido de compra).</summary>
	private static void Absorber(MotorSuite s, DateTime t, decimal precio, int veces, double cada = 0.3)
	{
		s.Dom(new CambioDom(t.AddSeconds(-1), precio, 20, false));

		for (var i = 0; i < veces; i++)
		{
			var ti = t.AddSeconds(i * cada);
			s.Trade(Tr(ti, precio, 10, Lado.Venta));
			s.Dom(new CambioDom(ti.AddMilliseconds(5), precio, 10, false));
			s.Dom(new CambioDom(ti.AddMilliseconds(10), precio, 20, false));
		}
	}

	#endregion

	#region AbsorptionMaster

	/// <summary>El caso del documento: barren el mínimo de ayer, un iceberg comprador absorbe y el precio gira (Setup B).</summary>
	private static void HistoriaSetupB(Comprobador c)
	{
		c.Seccion("AbsorptionMaster: la historia del documento (Setup B)");
		var s = Suite(true);
		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 11));

		Cascada(s, t);
		Barrido(s, t.AddMilliseconds(207), 777);

		// 09:42:12 - 09:42:24.6: 43 ventas de 10 absorbidas en 21396.25 (visible 20).
		Absorber(s, t.AddSeconds(1), 21396.25m, 43);
		var iceberg = s.Icebergs.Icebergs.Single();
		c.Check("iceberg de alta probabilidad (*)", iceberg.AltaProbabilidad.ToString(), "True");
		c.Check("sesgo GIRO tras el barrido del PDL", $"{iceberg.Sesgo} | {iceberg.Contexto}", "GIRO | PostSweep PDL");
		c.Check("todavía sin señal (iceberg ACTIVO)", (s.Master.UltimaSenal is null).ToString(), "True");
		c.Check("pendiente: POSIBLE SETUP B LARGO esperando AGUANTANDO",
			s.Master.Pendiente is { } p ? s.Master.TextoPendiente(p) : "ninguno", "POSIBLE SETUP B LARGO | 60/100 | esperando: Iceberg AGUANTANDO (+4 ticks)");

		// 09:42:25 - 09:42:35: compras que suben el precio a +4 ticks.
		var precios = new[] { 21396.50m, 21396.75m, 21397.00m, 21397.25m };

		for (var i = 0; i <= 10; i++)
			s.Trade(Tr(t.AddSeconds(14 + i), precios[Math.Min(i, 3)], 40, Lado.Compra));

		c.Check("AGUANTANDO", iceberg.Estado, "AGUANTANDO");
		var senal = s.Master.UltimaSenal;
		c.Check("señal emitida", (senal is not null).ToString(), "True");

		if (senal is null)
			return;

		var e = s.Master.EventoUltimaSenal!;
		c.Check("texto de la señal", e.Texto, "SEÑAL GIRO LARGO | 75/100 | Setup B | Entrada 21397.25 | SL 21395.50 | TP 21409.00 (VAL) | R:R 6.7 | 09:42:35");
		c.Check("checklist",
			string.Join(" / ", senal.Checklist.Select(l => $"{l.Marca} {l.Texto} +{l.Puntos}")),
			"OK Iceberg válido (* alta prob.) +40 / OK Barrido / stops previos (PDL) +20 / NO Stacked imbalance compra +0 / OK Giro del delta a favor +15 / " +
			"NO En nivel de perfil (LVN/VAH/VAL/POC) +0 / OK Fuera de blackout de noticias +0 / OK R:R >= 1.5 +0 / OK Sin tendencia en contra (o con barrido previo) +0");
		c.Check("una sola señal por iceberg", s.Eventos.Todos.Count(x => x.Tipo == "Signal").ToString(), "1");

		var novedades = s.Eventos.TomarNovedades().Select(n => $"{n.Indicador}:{n.Estado}").ToList();
		c.Check("novedades en orden", string.Join(",", novedades),
			"Sweeps:EVALUANDO,Stops:EVALUANDO,Iceberg:ACTIVO,Iceberg:ACTIVO,Iceberg:AGUANTANDO,Master:EMITIDA");

		// Después vuelve sobre el PDL: LIQUIDEZ y TRAMPA.
		s.Trade(Tr(t.AddSeconds(27), 21398.75m, 5, Lado.Compra));
		c.Check("barrido -> LIQUIDEZ (volvió en 27 s)", s.Barridos.Barridos.Single().Evento.Texto.Contains("BARRIDO LIQUIDEZ VENTA").ToString(), "True");
		c.Check("stops -> TRAMPA", s.Stops.Cascadas.Single().Estado, "TRAMPA");
	}

	private static void SetupDyFiltros(Comprobador c)
	{
		c.Seccion("AbsorptionMaster: Setup D, blackout, R:R mínimo y cooldown");

		// D: el iceberg de compra se rompe y un barrido de venta se mantiene fuera (RUPTURA): CONTINUIDAD CORTO.
		// Aquí solo puntúan el iceberg (30) y el barrido de ruptura (20): se baja el mínimo a 50 para ver la mecánica.
		var s = Suite(true);
		s.Cfg.Master.MinSignalScore = 50;
		var t = Utc(new DateTime(2026, 10, 30, 9, 50, 0));
		Absorber(s, t, 21400.25m, 16);
		c.Check("iceberg de compra en 21400.25", s.Icebergs.Icebergs.Count(i => i.Vivo).ToString(), "1");
		Barrido(s, t.AddSeconds(30), 4242, 21400.25m, 21399.00m, 15);
		s.Tiempo(t.AddSeconds(31));
		var roto = s.Icebergs.Icebergs.Single();
		c.Check("iceberg ROTO por el barrido", $"{roto.Estado} {roto.Sesgo}", "ROTO RUPTURA");
		c.Check("D pendiente (barrido EVALUANDO)", s.Master.Pendiente is { } p ? s.Master.TextoPendiente(p) : "ninguno",
			"POSIBLE SETUP D CORTO | 30/100 | esperando: barrido de RUPTURA");
		s.Trade(Tr(t.AddSeconds(45), 21398.75m, 50, Lado.Venta));
		s.Tiempo(t.AddSeconds(61));
		c.Check("barrido por el nivel del iceberg roto -> RUPTURA", $"{s.Barridos.Barridos.Single().Nivel?.Nombre} {s.Barridos.Barridos.Single().Tipo}", "Iceberg RUPTURA");
		c.Check("Setup D: SEÑAL CONTINUIDAD CORTO", s.Master.EventoUltimaSenal?.Texto.StartsWith("SEÑAL CONTINUIDAD CORTO |") ?? false, "");
		c.Check("D: entrada 4 ticks bajo el nivel roto, stop 3 ticks sobre él",
			s.Master.UltimaSenal is { } d ? $"{F(d.Entrada)} {F(d.Stop)}" : "", "21399.25 21401");

		// Blackout de noticias: 10:01 está a menos de 2 min de las 10:00.
		var m = new MotorConfluencia(new RegistroEventos(), new MotorContexto(), new MotorImbalance(new RegistroEventos()), Iceberg(new RegistroEventos()),
			new MotorStops(new RegistroEventos()), new MotorBarridos(new RegistroEventos()), new MedidorDelta());
		c.Check("blackout 10:01 (noticia 10:00 ± 2)", m.EnBlackout(Utc(new DateTime(2026, 10, 30, 10, 1, 0))).ToString(), "True");
		c.Check("sin blackout 10:03", m.EnBlackout(Utc(new DateTime(2026, 10, 30, 10, 3, 0))).ToString(), "False");
		c.Check("blackout 08:28:30 (noticia 08:30)", m.EnBlackout(Utc(new DateTime(2026, 10, 30, 8, 28, 30))).ToString(), "True");
		m.P.NewsTimes = "";
		c.Check("sin horas de noticias: nunca", m.EnBlackout(Utc(new DateTime(2026, 10, 30, 10, 0, 0))).ToString(), "False");

		// R:R mínimo: con MinRewardRisk 7 la señal de la historia B queda bloqueada (R:R 6,7).
		var s2 = Suite(true);
		s2.Cfg.Master.MinRewardRisk = 7;
		var t2 = Utc(new DateTime(2026, 10, 30, 9, 42, 11));
		Cascada(s2, t2);
		Barrido(s2, t2.AddMilliseconds(207), 777);
		Absorber(s2, t2.AddSeconds(1), 21396.25m, 43);
		var precios = new[] { 21396.50m, 21396.75m, 21397.00m, 21397.25m };

		for (var i = 0; i <= 10; i++)
			s2.Trade(Tr(t2.AddSeconds(14 + i), precios[Math.Min(i, 3)], 40, Lado.Compra));

		c.Check("R:R 6,7 < 7: sin señal", (s2.Master.UltimaSenal is null).ToString(), "True");
		c.Check("pendiente bloqueada por R:R", s2.Master.Pendiente is { } pb ? s2.Master.TextoPendiente(pb) : "ninguno",
			"POSIBLE SETUP B LARGO | 75/100 | bloqueada: R:R 6.7 < 7.0");
	}

	#endregion

	#region Grabación

	private static void Grabacion(Comprobador c)
	{
		c.Seccion("EventRecorder: CSV por día (NY), una fila por publicación, recarga por Id");
		var carpeta = Path.Combine(Path.GetTempPath(), "suite-pruebas-" + Guid.NewGuid().ToString("N"));

		try
		{
			var ev = new RegistroEventos { EnVivo = true };
			var m = Iceberg(ev);
			var t = Utc(new DateTime(2026, 11, 2, 9, 42, 10));
			m.Dom(new CambioDom(t, 21396.25m, 20, false));

			for (var i = 0; i < 16; i++)
				m.Trade(Tr(t.AddSeconds(1 + i * 0.3), 21396.25m, 10, Lado.Venta));

			m.Trade(Tr(t.AddSeconds(30), 21395.50m, 1, Lado.Venta));

			using (var g = new GrabadorEventos(carpeta, "NQ Z6", conTemporizador: false))
			{
				foreach (var n in ev.TomarNovedades())
					g.Encolar(n);
			}

			var ruta = GrabadorEventos.Archivo(carpeta, "NQ Z6", new DateTime(2026, 11, 2));
			c.Check("archivo del día de NY", Path.GetFileName(ruta), "2026-11-02.csv");
			var bytes = File.ReadAllBytes(ruta);
			c.Check("UTF-8 con BOM (Excel)", (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF).ToString(), "True");
			var lineas = File.ReadAllLines(ruta);
			c.Check("cabecera + 2 filas (ACTIVO y ROTO)", lineas.Length.ToString(), "3");
			c.Check("cabecera con las columnas de la especificación", lineas[0].StartsWith("TimestampUTC,TimestampNY,Instrumento,Indicador,Tipo,Lado,Precio,PrecioFin,Volumen,Clip,Recargas,Ratio,Estado,Sesgo,Sesion,Modo,Score,Contexto").ToString(), "True");
			var campos = GrabadorEventos.Campos(lineas[2]);
			c.Check("fila ROTO", string.Join(",", campos.Skip(1).Take(17)), "2026-11-02 09:42:40.000,NQ Z6,Iceberg,Iceberg,Compra,21396.25,,160,20,7,8,ROTO,RUPTURA,AperturaNY,Inferido,,Roto");

			var leidos = GrabadorEventos.Leer(carpeta, "NQ Z6", "Iceberg", t.AddHours(3), 5);
			c.Check("recarga: un evento (última fila por Id)", leidos.Count.ToString(), "1");
			c.Check("recargado: estado, texto y marca Grabado", $"{leidos[0].Estado} {leidos[0].Grabado} {leidos[0].Texto == m.Icebergs[0].Evento.Texto}", "ROTO True True");
			c.Check("filtro por indicador", GrabadorEventos.Leer(carpeta, "NQ Z6", "Sweeps", t, 5).Count.ToString(), "0");

			var ev2 = new RegistroEventos();
			c.Check("Importar no duplica por Id", $"{ev2.Importar(leidos)} {ev2.Importar(GrabadorEventos.Leer(carpeta, "NQ Z6", null, t, 5))}", "1 0");

			c.Check("comillas en el CSV", string.Join("|", GrabadorEventos.Campos("a,\"b,c\",\"d\"\"e\"")), "a|b,c|d\"e");
		}
		finally
		{
			if (Directory.Exists(carpeta))
				Directory.Delete(carpeta, true);
		}
	}

	#endregion
}
