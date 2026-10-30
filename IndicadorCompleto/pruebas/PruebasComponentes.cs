using System;
using System.Collections.Generic;
using System.Linq;

using Completo.Nucleo;

using static Velas;

/// <summary>Pruebas de las piezas del núcleo por separado.</summary>
internal static class PruebasComponentes
{
	public static void Ejecutar(Comprobador c)
	{
		Tiempo(c);
		Agregacion(c);
		Footprint(c);
		Estructura(c);
		Sesiones(c);
		Cvd(c);
		Lvn(c);
	}

	private static void Tiempo(Comprobador c)
	{
		c.Seccion("Tiempo y sesiones (con cambio de horario)");
		var cal = new Calendario("2026-11-27 13:00");

		// 28/10/2026: Nueva York en horario de verano (UTC-4); Londres ya en invierno (UTC+0, cambió el 25/10).
		var ny = cal.VentanaDelDia(TipoSesion.NuevaYork, new DateTime(2026, 10, 28));
		c.Check("NY 28/10 (EDT): 09:30–16:00 = 13:30–20:00 UTC", $"{ny.InicioUtc:HH:mm}-{ny.FinUtc:HH:mm}", "13:30-20:00");

		var londres = cal.VentanaDelDia(TipoSesion.Londres, new DateTime(2026, 10, 28));
		c.Check("Londres 28/10 (GMT): 08:00–12:00 = 08:00–12:00 UTC", $"{londres.InicioUtc:HH:mm}-{londres.FinUtc:HH:mm}", "08:00-12:00");

		var londresVerano = cal.VentanaDelDia(TipoSesion.Londres, new DateTime(2026, 10, 21));
		c.Check("Londres 21/10 (BST): 08:00–12:00 = 07:00–11:00 UTC", $"{londresVerano.InicioUtc:HH:mm}-{londresVerano.FinUtc:HH:mm}", "07:00-11:00");

		var tokio = cal.VentanaDelDia(TipoSesion.Tokio, new DateTime(2026, 10, 28));
		c.Check("Tokio 28/10: 09:00–15:00 = 00:00–06:00 UTC", $"{tokio.InicioUtc:HH:mm}-{tokio.FinUtc:HH:mm}", "00:00-06:00");

		var anticipado = cal.VentanaDelDia(TipoSesion.NuevaYork, new DateTime(2026, 11, 27));
		c.Check("cierre anticipado NY 27/11 13:00 (EST) = 18:00 UTC", $"{anticipado.FinUtc:HH:mm}", "18:00");

		c.Check("día Globex: 27/10 22:30 UTC (18:30 NY) pertenece al 28/10", $"{Calendario.DiaGlobex(new DateTime(2026, 10, 27, 22, 30, 0)):dd/MM}", "28/10");
		c.Check("sin sesión de NY en sábado", (cal.Ventana(TipoSesion.NuevaYork, new DateTime(2026, 10, 31, 15, 0, 0)) is null).ToString(), "True");
		c.Check("reinicio CVD 09:30 NY del 28/10 = 13:30 UTC", $"{Calendario.ReinicioCvd(new DateTime(2026, 10, 28, 16, 0, 0), new TimeSpan(9, 30, 0)):HH:mm}", "13:30");
		c.Check("presentación en Ciudad de México (UTC-6)", $"{Calendario.APresentacion(new DateTime(2026, 10, 28, 15, 0, 0), ZonaPresentacion.CiudadDeMexico):HH:mm}", "09:00");
	}

	private static Vela V1(DateTime inicio, decimal o, decimal h, decimal l, decimal cierre, decimal volumen = 100) => new()
	{
		Marco = Marco.M1,
		InicioUtc = inicio,
		FinUtc = inicio.AddMinutes(1),
		Apertura = o,
		Maximo = h,
		Minimo = l,
		Cierre = cierre,
		Volumen = volumen,
		Delta = 10,
		DeltaMaximo = 12,
		DeltaMinimo = -3,
		FootprintCompleto = true
	};

	private static void Agregacion(Comprobador c)
	{
		c.Seccion("Agregación 1M → 5M (solo periodos completos)");
		var cfg = new ConfiguracionCompleto();
		var a5 = new Agregador(Marco.M5, false, cfg);
		var t0 = new DateTime(2026, 10, 28, 14, 0, 0);
		var cerradas = new List<Vela>();

		for (var i = 0; i < 10; i++)
		{
			if (a5.Agregar(V1(t0.AddMinutes(i), 100 + i, 105 + i, 95 + i, 101 + i)) is { } v)
				cerradas.Add(v);
		}

		c.Check("10 velas de 1M → 2 velas de 5M", cerradas.Count.ToString(), "2");
		c.Check("5M #1: O/H/L/C y volumen", $"{cerradas[0].Apertura}/{cerradas[0].Maximo}/{cerradas[0].Minimo}/{cerradas[0].Cierre} v{cerradas[0].Volumen}", "100/109/95/105 v500");
		c.Check("5M #1 se conoce al cierre de 14:05", $"{cerradas[0].FinUtc:HH:mm}", "14:05");
		c.Check("delta máximo intravela acumulado (4×10 + 12)", cerradas[0].DeltaMaximo.ToString(), "52");

		// Falta la vela de 14:12: el periodo 14:10–14:15 es incompleto y se descarta.
		var falta = new Agregador(Marco.M5, false, cfg);
		Vela? resultado = null;

		foreach (var i in new[] { 10, 11, 13, 14 })
			resultado = falta.Agregar(V1(t0.AddMinutes(i), 100, 101, 99, 100)) ?? resultado;

		c.Check("periodo con una vela de 1M ausente: descartado", $"{(resultado is null ? "ninguna" : "emitida")} descartadas={falta.Descartadas}", "ninguna descartadas=1");
	}

	private static void Footprint(Comprobador c)
	{
		c.Seccion("Footprint: POC, área de valor y posición relativa");
		var cfg = new ConfiguracionCompleto();
		var niveles = new SortedDictionary<long, Nivel>();

		// 100.00–102.00; volumen máximo en 100.50 (POC), poco arriba.
		var volumenes = new Dictionary<decimal, decimal> { [100m] = 10, [100.25m] = 30, [100.5m] = 50, [100.75m] = 20, [101m] = 10, [101.25m] = 5, [101.5m] = 3, [101.75m] = 2, [102m] = 1 };

		foreach (var (precio, volumen) in volumenes)
			niveles[cfg.ATick(precio)] = new Nivel(volumen / 2, volumen / 2);

		var h = Huella.Calcular(niveles, cfg, 102m, 100m)!;
		c.Check("POC = precio de mayor volumen", h.Poc.ToString("0.00"), "100.50");
		c.Check("área de valor 70 %: VAL / VAH", $"{h.Val:0.00}/{h.Vah:0.00}", "100.25/100.75");
		c.Check("POC relativo = (100.50 - 100) / 2", h.PosPoc.ToString("0.00"), "0.25");
		c.Check("centro VA relativo", h.PosVa.ToString("0.00"), "0.25");
	}

	private static List<Vela> Velas1H(decimal[] h, decimal[] l, decimal[] cierre)
	{
		var t0 = new DateTime(2026, 10, 20, 0, 0, 0);
		return Enumerable.Range(0, h.Length).Select(i => new Vela
		{
			Marco = Marco.H1,
			Indice = i,
			InicioUtc = t0.AddHours(i),
			FinUtc = t0.AddHours(i + 1),
			Apertura = l[i],
			Maximo = h[i],
			Minimo = l[i],
			Cierre = cierre[i]
		}).ToList();
	}

	private static void Estructura(Comprobador c)
	{
		c.Seccion("Estructura 1H: pivotes, BOS, swing protegido, MSS");
		var cfg = new ConfiguracionCompleto { MargenEstructuralTicks = 4 };
		var est = new Estructura1H(cfg);

		decimal[] h = { 10, 12, 15, 12, 11, 9, 10, 13, 17, 18, 16, 12, 9 };
		decimal[] l = { 8, 10, 12, 9, 7, 5, 7, 9, 13, 15, 12, 6, 3 };
		decimal[] cierre = { 9, 11, 14, 10, 8, 6, 9, 12, 16, 17, 13, 7, 3.5m };
		var todas = Velas1H(h, l, cierre);
		var lista = new List<Vela>();
		var historial = new List<string>();

		foreach (var vela in todas)
		{
			lista.Add(vela);
			est.Procesar(lista);
			historial.Add(est.Sesgo.Texto());
		}

		c.Check("sin ruptura todavía: NO DETERMINADO (vela 8 cierra 16, no supera 15 + 1)", historial[8], "NO DETERMINADO");
		c.Check("vela 9 cierra 17 > máximo confirmado 15 + margen: BOS inicial ALCISTA", historial[9], "ALCISTA");
		c.Check("evento registrado", $"{est.Eventos[0].Tipo} nivel {est.Eventos[0].NivelRoto}", "BOS inicial nivel 15");
		c.Check("swing protegido = último mínimo confirmado (5)", est.Eventos[0].SwingProtegido?.ToString() ?? "-", "5");
		c.Check("cierre 7 no pierde el swing protegido (5 - 1)", historial[11], "ALCISTA");
		c.Check("cierre 3.5 < 5 - 1: MSS → BAJISTA", $"{historial[12]} {est.Eventos[^1].Tipo}", "BAJISTA MSS");
		c.Check("condición de cambio escrita con el precio", est.CondicionCambio(p => p.ToString("0.##")).StartsWith("Cierre 1H > ").ToString(), "True");
	}

	private static void Sesiones(Comprobador c)
	{
		c.Seccion("Referencias de liquidez: sesiones completas");
		var cfg = new ConfiguracionCompleto();
		var cal = new Calendario("");
		var completa = new GestorLiquidez(cfg, cal);
		var desde = new DateTime(2026, 10, 28, 7, 50, 0);

		// Londres 28/10: 08:00–12:00 UTC. Mínimo 19980 a las 09:00, máximo 20040 a las 11:00.
		for (var i = 0; i < 260; i++)
		{
			var t = desde.AddMinutes(i);
			var minimo = t == new DateTime(2026, 10, 28, 9, 0, 0) ? 19980m : 20000m;
			var maximo = t == new DateTime(2026, 10, 28, 11, 0, 0) ? 20040m : 20010m;
			completa.ProcesarVela1M(new Vela { Marco = Marco.M1, InicioUtc = t, FinUtc = t.AddMinutes(1), Apertura = 20005, Maximo = maximo, Minimo = minimo, Cierre = 20005 });
		}

		var londres = completa.Referencias.Where(r => r.Origen == OrigenReferencia.Londres).OrderBy(r => r.EsMaximo).ToList();
		c.Check("Londres completa: mínimo y máximo publicados", string.Join(" | ", londres.Select(r => $"{r.Etiqueta} {r.Precio}")), "Mínimo de Londres 28/10 19980 | Máximo de Londres 28/10 20040");
		c.Check("disponibles al cierre de la ventana (12:00 UTC)", $"{londres[0].DisponibleUtc:HH:mm}", "12:00");

		var parcial = new GestorLiquidez(cfg, cal);

		for (var i = 30; i < 260; i++)
		{
			var t = desde.AddMinutes(i + 10);
			parcial.ProcesarVela1M(new Vela { Marco = Marco.M1, InicioUtc = t, FinUtc = t.AddMinutes(1), Apertura = 20005, Maximo = 20010, Minimo = 20000, Cierre = 20005 });
		}

		c.Check("Londres cargada a medias (desde 08:30): no se publica", $"{parcial.Referencias.Count(r => r.Origen == OrigenReferencia.Londres)} incompletas={parcial.SesionesIncompletas}", "0 incompletas=1");
	}

	private static void Cvd(Comprobador c)
	{
		c.Seccion("CVD: reinicio 09:30 NY, base parcial");
		var cvd = new CalculadoraCvd(new TimeSpan(9, 30, 0));
		var t0 = new DateTime(2026, 10, 28, 13, 25, 0);
		var velas = new List<Vela>();

		foreach (var i in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 10 })
		{
			var v = new Vela { Marco = Marco.M1, InicioUtc = t0.AddMinutes(i), FinUtc = t0.AddMinutes(i + 1), Delta = 10, DeltaMaximo = 15, DeltaMinimo = -5 };
			cvd.Aplicar(v);
			velas.Add(v);
		}

		c.Check("carga iniciada a las 09:25 NY: base PARCIAL", $"{velas[0].SesionCvd} {velas[0].CvdParcial}", "1 True");
		c.Check("09:30 NY reinicia: sesión nueva, base COMPLETA, CVD vuelve a 0 + delta", $"{velas[5].SesionCvd} {velas[5].CvdParcial} {velas[5].CvdApertura}/{velas[5].CvdCierre}", "2 False 0/10");
		c.Check("vela CVD: máximo = base + delta máximo intravela", $"{velas[6].CvdApertura}/{velas[6].CvdMaximo}/{velas[6].CvdMinimo}/{velas[6].CvdCierre}", "10/25/5/20");
		c.Check("faltan velas en la sesión: la base pasa a PARCIAL", $"{velas[^1].CvdParcial} {cvd.Estado}", "True BASE PARCIAL");
	}

	/// <summary>Perfil sintético: volumen alto con valles en 20030 (suave), 20075 (profundo), 20081 (cerca del profundo) y 20120.</summary>
	private static decimal Forma(decimal p) =>
		p is >= 20073m and <= 20077m ? 0.4m
		: p is >= 20080.5m and <= 20081.5m ? 3m
		: p is >= 20029.5m and <= 20031m ? 5m
		: p is >= 20119.5m and <= 20121.5m ? 2.5m
		: 10m;

	private static Vela VelaPerfil(DateTime inicio, ConfiguracionCompleto cfg)
	{
		var v = new Vela { Marco = Marco.M1, InicioUtc = inicio, FinUtc = inicio.AddMinutes(1), Apertura = 20050, Maximo = 20200, Minimo = 19900, Cierre = 20050, FootprintCompleto = true, Niveles = new SortedDictionary<long, Nivel>() };

		for (var p = 19900m; p <= 20200m; p += cfg.TamanoTick)
		{
			var volumen = Forma(p);
			v.Niveles[cfg.ATick(p)] = new Nivel(volumen / 2, volumen / 2);
			v.Volumen += volumen;
		}

		return v;
	}

	private static void Lvn(Comprobador c)
	{
		c.Seccion("LVN: perfiles reales, zonas, separación, ranking amarillo/naranja, evidencia");
		var cfg = new ConfiguracionCompleto { CantidadLvnAltaPrioridad = 1, CantidadLvnSecundarios = 5 };
		var lvn = new MotorLvn(cfg);
		var cal = new Calendario("");

		// 7 días de Globex con 4 velas cada uno (la primera de cada día abre el perfil; el primer día cargado no cuenta).
		var primerDia = new DateTime(2026, 10, 19, 23, 0, 0);

		for (var d = 0; d < 7; d++)
		{
			foreach (var hora in new[] { 0, 6, 12, 18 })
			{
				var t = primerDia.AddDays(d).AddHours(hora);
				var vela = VelaPerfil(t, cfg);
				lvn.AgregarVela(vela, Calendario.DiaGlobex(t), cal.Ventana(TipoSesion.NuevaYork, t));
			}
		}

		var ahora = new DateTime(2026, 10, 27, 13, 30, 0);
		var mapa = lvn.CrearMapa(ahora, 20050m)!;
		var porPuesto = mapa.Zonas.OrderBy(z => z.Puesto).ToList();

		// 7 días: el primero empezó a medias y el último sigue abierto → 5 sesiones completas y cerradas.
		c.Check("mapa creado solo con sesiones completas y cerradas (5 de 7)", $"v{mapa.Version} sesiones={mapa.SesionesValidas}", "v1 sesiones=5");
		c.Check("el valle profundo (20073–20077) es el primero: ALTA PRIORIDAD", $"{porPuesto[0].Centro:0.##} {porPuesto[0].Prioridad.Texto()}", "20075 ALTA PRIORIDAD");
		c.Check("el resto son SECUNDARIOS (1 amarillo configurado)", string.Join(",", porPuesto.Skip(1).Select(z => z.Prioridad.Texto()).Distinct()), "SECUNDARIO");
		c.Check("valle a 6 puntos del profundo (20081): eliminado por la separación mínima de 10 puntos",
			mapa.Zonas.Any(z => Math.Abs(z.Centro - 20081m) < 1).ToString(), "False");
		c.Check("separación mínima respetada entre todas las zonas",
			mapa.Zonas.Zip(mapa.Zonas.Skip(1)).All(par => par.Second.Centro - par.First.Centro >= 10m).ToString(), "True");
		c.Check("valles suaves detectados (20030 y 20120)",
			$"{mapa.Zonas.Any(z => Math.Abs(z.Centro - 20030m) <= 1)} {mapa.Zonas.Any(z => Math.Abs(z.Centro - 20120.5m) <= 1.5m)}", "True True");
		c.Check("perfil de origen guardado para dibujarlo (5 sesiones)", mapa.Perfiles.ContainsKey(PerfilVisible.Sesiones5).ToString(), "True");
		c.Check("objetivo LVN amarillo por encima de 20000 = zona 20075",
			lvn.SiguientePrioritario(Direccion.Larga, 20000m, ahora)?.Centro.ToString("0.##") ?? "ninguno", "20075");
		c.Check("sin objetivo amarillo por encima de 20100", (lvn.SiguientePrioritario(Direccion.Larga, 20100m, ahora) is null).ToString(), "True");

		// Reacción de soporte en la zona 20075 (desde que fue reconocida): toca, cierra encima y se aleja 40 ticks.
		var profunda = porPuesto[0];
		var t1 = ahora.AddMinutes(10);
		var toque = new Vela { Marco = Marco.M1, InicioUtc = t1, FinUtc = t1.AddMinutes(1), Apertura = 20080, Maximo = 20080, Minimo = 20076, Cierre = 20079 };
		var salida = new Vela { Marco = Marco.M1, InicioUtc = t1.AddMinutes(1), FinUtc = t1.AddMinutes(2), Apertura = 20079, Maximo = 20090, Minimo = 20078, Cierre = 20089 };
		lvn.SeguirReacciones(toque, Calendario.DiaGlobex(t1));
		lvn.SeguirReacciones(salida, Calendario.DiaGlobex(t1));

		c.Check("el mapa vigente no cambia a mitad de sesión (congelado)", profunda.Reacciones.ToString(), "0");

		var siguiente = lvn.CrearMapa(ahora.AddDays(1), 20050m)!;
		var misma = siguiente.Zonas.First(z => Math.Abs(z.Centro - 20075m) < 2);
		c.Check("mapa siguiente: misma zona, mismo ID, con la reacción acumulada",
			$"id {misma.Id == profunda.Id} reacciones {misma.Reacciones} soporte {misma.ReaccionesSoporte} sesiones {misma.SesionesConReaccion}", "id True reacciones 1 soporte 1 sesiones 1");
		c.Check("la evidencia sube la puntuación", (misma.Puntuacion > profunda.Puntuacion).ToString(), "True");
		c.Check("el mapa anterior queda cerrado al crearse el nuevo", (mapa.FinUtc == ahora.AddDays(1)).ToString(), "True");
	}
}
