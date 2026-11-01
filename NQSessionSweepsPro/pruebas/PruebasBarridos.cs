using System;
using System.Collections.Generic;
using System.Linq;

using NqSweeps.Nucleo;

using static Datos;

/// <summary>F2/F3: barridos con la lógica del Pine, validación de Order Flow, clasificación, panel, FVG/IFVG y MSS.</summary>
internal static class PruebasBarridos
{
	public static readonly DateTime Martes = new(2026, 11, 3);

	/// <summary>
	/// Lunes 18:00 a martes 'hasta' ET con velas planas en 20000 (rango 4, ATR 4). Asia: máximo 20030 y mínimo 19970
	/// (a las 20:00). Londres: máximo 20010 (05:00) y mínimo 19990 (06:00).
	/// </summary>
	public static MotorSweeps Escenario(DateTime hastaEt, Action<ConfiguracionSweeps>? ajustes = null)
	{
		var cfg = new ConfiguracionSweeps();
		ajustes?.Invoke(cfg);
		var m = new MotorSweeps(cfg);

		foreach (var v in Planas(Martes.AddHours(-6), hastaEt, 20000))
		{
			var et = Zonas.AEt(v.InicioUtc);

			if (et.Hour == 20 && et.Minute == 0)
				(v.High, v.Low) = (20030, 19970);
			else if (et.Hour == 5 && et.Minute == 0)
				v.High = 20010;
			else if (et.Hour == 6 && et.Minute == 0)
				v.Low = 19990;

			m.Procesar(v);
		}

		return m;
	}

	/// <summary>Vela de barrido del máximo de Londres (20010) a las 08:45: compras agresivas fuertes más allá y cierre de vuelta.</summary>
	public static VelaEntrada VelaBarridoMax(DateTime et) =>
		V(et, 20006, 20012, 20005, 20007, 100, 0, (20011m, 50, 250), (20011.5m, 40, 200), (20012m, 30, 100));

	public static void Ejecutar(Comprobador c)
	{
		BarridoReal(c);
		Falsos(c);
		VentanaYEspejo(c);
		FvgYMss(c);
	}

	private static void BarridoReal(Comprobador c)
	{
		c.Seccion("F2/F3. Barrido del máximo de Londres a las 08:45 ET: caja del Pine, Order Flow y SWEEP REAL");

		var m = Escenario(Martes.AddHours(8.75));
		c.Check("ATR21 de velas planas de rango 4", m.Atr21?.ToString("0.00") ?? "-", "4.00");

		m.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));
		var z = m.ZonasBarrido.Single();
		var s = z.BarraBarrido;
		c.Check("caja Pine: de bar-1 a bar+1, base L y techo min(L + 1,3 × ATR, High)", $"{z.Nombre} {z.Izquierda - s}..+{z.Derecha - s} {z.L}-{z.Extremo}", "MAX Londres -1..+1 20010-20012");
		c.Check("aviso de ruptura con la hora del barrido (en histórico, al cierre de la vela)", m.Avisos.First(a => a.Tipo == TipoAviso.Ruptura).Texto, "NQ rompe MAX Londres 20010.00 a las 08:45:00 ET");

		var w1 = z.W1!;
		c.Check("W1: contratos, más allá del nivel, delta y avance", $"{w1.VolTotal:0} | beyond {w1.VolBeyond:0} | ticks {w1.TicksBeyond}", "770 | beyond 698 | ticks 8");
		c.Check("absorción W1: volumen, divergencia, cierre de vuelta, POC, VA y eficiencia (sin agotamiento)",
			$"{w1.Absorcion}/10 [{string.Join("; ", w1.Puntos)}]",
			"9/10 [Volumen mas alla +2; Divergencia de delta +2; Cierre de vuelta +2; POC en el extremo +1; VA en el extremo +1; Eficiencia alta +1]");
		c.Check("clasificación al cierre de W1", z.Etiqueta, "SWEEP REAL 9/10");
		c.Check("aviso de clasificación", m.Avisos.Last(a => a.Tipo == TipoAviso.Clasificacion).Texto, "SWEEP REAL 9/10 - MAX Londres 20010.00 (1m)");

		// Cinco velas de caída: fuera del margen por abajo; a las 5 velas del borde derecho, la zona se cierra (válida).
		for (var i = 1; i <= 6; i++)
			m.Procesar(V(Martes.AddHours(8.75).AddMinutes(i), 20004, 20005, 19998, 19999));

		c.Check("ventanas de 3 y 5 min calculadas", $"{z.W3?.Minutos} {z.W5?.Minutos}", "3 5");
		c.Check("zona cerrada como válida a las 5 velas; el nivel queda Barrido y deja de vigilarse",
			$"activa {z.Activa} falso {z.FalsoPine} nivel {z.Nivel.Estado} borde +{z.Derecha - s}", "activa False falso False nivel Barrido borde +1");
		c.Check("panel de datos (ASCII)", z.Panel(m.Precio).Split('\n')[0] + " / " + z.Panel(m.Precio).Split('\n')[3] + " / " + z.Panel(m.Precio).Split('\n')[5],
			"MAX Londres 20010.00 | 08:45:00 ET / Absorcion 1m 9/10 3m 9/10 5m 9/10 / Estado: SWEEP REAL");
		c.Check("velocidad aproximada en histórico (compras de W1 más allá / 60 s)", z.Panel(m.Precio).Split('\n')[4],
			$"Vel compra {w1.AskBeyond / 60:0} c/s (x{z.RatioVelocidad.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}) aprox");
		c.Check("la línea de la sesión barrida se corta en el barrido", (m.Sesiones.Instancias.First(i => i.Id == "Londres").AltoBarridoEn == s).ToString(), "True");
		c.Check("todos los textos en ASCII", m.Avisos.All(a => (a.Texto + a.Asunto + a.Cuerpo).All(ch => ch < 128)).ToString(), "True");
	}

	private static void Falsos(Comprobador c)
	{
		c.Seccion("F2. Regla del Pine: barrido FALSO (aceptación) y regla de 5 velas");

		var m = Escenario(Martes.AddHours(8.75));
		m.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));
		m.Procesar(V(Martes.AddHours(8.75).AddMinutes(1), 20012, 20021, 20011, 20020));
		var z = m.ZonasBarrido.Single();
		c.Check("cierre por encima de L + 1,5 × margen: FALSO, borde derecho = esa vela, nivel roto",
			$"{z.Clasificacion.Texto()} falsoPine {z.FalsoPine} borde +{z.Derecha - z.BarraBarrido} nivel {z.Nivel.Estado}", "FALSO falsoPine True borde +1 nivel Roto");
		c.Check("aviso de falso rompimiento", m.Avisos.Last().Texto, "FALSO ROMPIMIENTO - MAX Londres 20010.00 (aceptacion)");

		var cinco = Escenario(Martes.AddHours(8.75));
		cinco.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));

		for (var i = 1; i <= 7; i++)
			cinco.Procesar(V(Martes.AddHours(8.75).AddMinutes(i), 20012, 20017, 20011, 20014));

		var z5 = cinco.ZonasBarrido.Single();
		c.Check("5 velas por encima de L + margen sin cierre lejano: FALSO al cumplirse la longitud, borde = bar + 1",
			$"{z5.FalsoPine} activa {z5.Activa} borde +{z5.Derecha - z5.BarraBarrido}", "True activa False borde +7");

		var pine = Escenario(Martes.AddHours(8.75), cfg => cfg.PineCompatibilityMode = true);
		var antes = pine.ZonasBarrido.Count;
		pine.Procesar(V(Martes.AddHours(9.5), 20000, 20004, 19999, 20003));
		c.Check("PineCompatibilityMode: solo se vigila el par de la última sesión cerrada (premercado) y el de Londres",
			string.Join(" | ", pine.Niveles.Activos.Where(pine.Niveles.Vigilado).Select(n => $"{n.Nombre} {n.Precio}")),
			"MAX Londres 20010 | MIN Londres 19990 | MAX Premercado 20002 | MIN Premercado 19998");
		c.Check("y el máximo del premercado se barre", $"{pine.ZonasBarrido.Count - antes} {pine.ZonasBarrido.Last().Nombre}", "1 MAX Premercado");
	}

	private static void VentanaYEspejo(Comprobador c)
	{
		c.Seccion("F2. Ventana de detección (NY por defecto), sellside en espejo");

		var ny = Escenario(Martes.AddHours(5.5));
		ny.Procesar(V(Martes.AddHours(5.5), 20028, 20032, 20027, 20029));
		c.Check("05:30 ET (Londres) con la ventana NY: el máximo de Asia no se barre", $"{ny.ZonasBarrido.Count} {ny.Niveles.Niveles.First(n => n.Nombre == "MAX Asia").Estado}", "0 Activo");

		var todas = Escenario(Martes.AddHours(5.5), cfg => cfg.SweepDetectionWindow = VentanaBarridos.Todas);
		todas.Procesar(V(Martes.AddHours(5.5), 20028, 20032, 20027, 20029));
		c.Check("con la ventana Todas, sí", todas.ZonasBarrido.Select(z => z.Nombre).FirstOrDefault() ?? "ninguno", "MAX Asia");

		var abajo = Escenario(Martes.AddHours(9) + TimeSpan.FromMinutes(35));
		abajo.Procesar(V(Martes.AddHours(9).AddMinutes(35), 19994, 19995, 19988, 19993, 100, 0, (19989m, 250, 50), (19988.5m, 200, 40)));
		var z = abajo.ZonasBarrido.Single();
		c.Check("sellside: caja desde L hacia abajo hasta max(L - margen, Low)", $"{z.Nombre} {z.L} {z.Extremo}", "MIN Londres 19990 19988");
		c.Check("ventas agresivas más allá y cierre de vuelta: SWEEP REAL", z.Etiqueta, "SWEEP REAL 9/10");
	}

	private static void FvgYMss(Comprobador c)
	{
		c.Seccion("F2/F5. FVG del Pine (lista), mitigación, IFVG y MSS (CHoCH)");

		var m = Escenario(Martes.AddHours(10));
		var pequeno = Escenario(Martes.AddHours(10));
		pequeno.Procesar(V(Martes.AddHours(10), 20000, 20001, 19998, 20000));
		pequeno.Procesar(V(Martes.AddHours(10).AddMinutes(1), 20000, 20007, 19999, 20006));
		pequeno.Procesar(V(Martes.AddHours(10).AddMinutes(2), 20005, 20008, 20003, 20007));
		c.Check("hueco de 2 puntos con ATR144 de unos 4: el filtro de anchura del Pine lo descarta", pequeno.Fvg.Fvgs.Count.ToString(), "0");

		m.Procesar(V(Martes.AddHours(10), 20000, 20001, 19998, 20000));
		m.Procesar(V(Martes.AddHours(10).AddMinutes(1), 20000, 20015, 19999, 20014));
		m.Procesar(V(Martes.AddHours(10).AddMinutes(2), 20013, 20016, 20008, 20015));
		var f = m.Fvg.Fvgs.Last();
		c.Check("FVG alcista: Low (20008) - High[2] (20001) > ATR144, cierre[1] por encima, sin huecos entre velas", $"{f.Alcista} {f.Abajo}-{f.Arriba}", "True 20001-20008");

		m.Procesar(V(Martes.AddHours(10).AddMinutes(3), 20012, 20013, 20000.5m, 20002));
		c.Check("el precio entra y cruza su base: mitigado (deja de extenderse)", $"{f.Mitigado} {f.Ifvg}", "True False");

		m.Procesar(V(Martes.AddHours(10).AddMinutes(4), 20002, 20002, 19998, 19999));
		c.Check("cierre por debajo de la base: IFVG (pasa a bajista) y aviso", $"{f.Ifvg} {f.AlcistaAhora} | {m.Avisos.Last().Texto}", "True False | IFVG BAJISTA 20001.00-20008.00");

		var mss = new MotorSweeps(new ConfiguracionSweeps { Mss = true, MSSLength = 2 });
		var precios = new[] { 100m, 101, 105, 101, 100, 98, 95, 97, 99, 96, 94, 97, 106, 104 };
		var t = Martes.AddHours(10);

		foreach (var p in precios)
		{
			mss.Procesar(V(t, p, p + 0.5m, p - 0.5m, p));
			t = t.AddMinutes(1);
		}

		c.Check("CHoCH: primero un mínimo roto (shift -1), luego un cierre sobre el último pivote alto",
			string.Join(" | ", mss.Mss.Lineas.Select(l => $"{(l.Alcista ? "alcista" : "bajista")} {l.Precio}")), "alcista 99.5");
	}
}
