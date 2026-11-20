using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>v5 (tres indicadores FAST): el Indicador de Vela Compuesta (FAST).</summary>
internal static class PruebasV5
{
	public static void Ejecutar(Comprobador c)
	{
		VelaCompuestaFast(c);
		ConfluenciaVolumetrica(c);
		GraficoCincoMinutos(c);
	}

	/// <summary>v5: gráfico de 5 minutos con datos de 1 minuto (partir velas, minutos desde trades, armado en vivo).</summary>
	private static void GraficoCincoMinutos(Comprobador c)
	{
		c.Seccion("v5 Sweep Pro en 5 minutos: velas de 1 minuto partiendo la vela del gráfico (sin footprint)");
		var t0 = Zonas.AUtc(new DateTime(2026, 11, 18, 9, 30, 0), ZonaHoraria.NuevaYork);
		var cinco = new VelaEntrada { InicioUtc = t0, Open = 21400m, High = 21410m, Low = 21395m, Close = 21408m, Volume = 5000, Delta = 600, Ask = 2800, Bid = 2200 };
		var p = MinutosV5.Partir(cinco, 5, Tick);
		c.Check("5 velas, una por minuto", string.Join(" ", p.Select(v => TiempoV2.HoraEt(v.InicioUtc, "HH:mm"))), "09:30 ET 09:31 ET 09:32 ET 09:33 ET 09:34 ET");
		c.Check("apertura, cierre y extremos exactos", $"{p[0].Open} {p[^1].Close} {p.Max(v => v.High)} {p.Min(v => v.Low)}", "21400 21408 21410 21395");
		c.Check("alcista: primero el mínimo, después el máximo", (p.FindIndex(v => v.Low == 21395m) < p.FindIndex(v => v.High == 21410m)).ToString(), "True");
		c.Check("volumen y delta repartidos (suman lo mismo)", $"{p.Sum(v => v.Volume)} {p.Sum(v => v.Delta)} {p[2].Volume}", "5000 600 1000");
		c.Check("cada minuto empieza donde terminó el anterior", p.Zip(p.Skip(1)).All(x => x.First.Close == x.Second.Open).ToString(), "True");
		c.Check("precios en ticks", p.All(v => new[] { v.Open, v.High, v.Low, v.Close }.All(x => x % Tick == 0)).ToString(), "True");

		c.Check("minutos del gráfico de ATAS", $"{MinutosV5.DeTimeFrame("TimeFrame", "M5")} {MinutosV5.DeTimeFrame("TimeFrame", "M1")} {MinutosV5.DeTimeFrame("Tick", "100")} {MinutosV5.DeTimeFrame(null, "H1")}", "5 1 0 -1");

		c.Seccion("v5 Sweep Pro en 5 minutos: con footprint (días con detalle)");
		var conHuella = new VelaEntrada { InicioUtc = t0, Open = 21405m, High = 21406m, Low = 21400m, Close = 21401m };

		for (var precio = 21400m; precio <= 21406m; precio += Tick)
			conHuella.Niveles.Add(new NivelVela(precio, 30, 20));

		(conHuella.Volume, conHuella.Delta) = (conHuella.Niveles.Sum(n => n.Bid + n.Ask), conHuella.Niveles.Sum(n => n.Ask - n.Bid));
		var ph = MinutosV5.Partir(conHuella, 5, Tick);
		c.Check("bajista: primero el máximo, después el mínimo", (ph.FindIndex(v => v.High == 21406m) <= ph.FindIndex(v => v.Low == 21400m)).ToString(), "True");
		c.Check("el footprint va a los minutos cuyo rango contiene el precio, y los totales cuadran",
			$"{ph.All(v => v.Niveles.All(n => n.Precio >= v.Low && n.Precio <= v.High))} {ph.Sum(v => v.Volume) == conHuella.Volume} {ph.Sum(v => v.Delta) == conHuella.Delta}", "True True True");

		c.Seccion("v5 Sweep Pro en 5 minutos: minutos exactos desde los trades del día; armado en vivo");
		var ticks = new List<TickV2>
		{
			new(t0.AddSeconds(5), 21400m, 10, true), new(t0.AddSeconds(30), 21402m, 5, false), new(t0.AddSeconds(50), 21399m, 7, true),
			new(t0.AddMinutes(2).AddSeconds(1), 21403m, 4, true), new(t0.AddMinutes(2).AddSeconds(40), 21401m, 6, false)
		};
		var dt = MinutosV5.DesdeTicks(ticks, t0, t0.AddMinutes(5));
		c.Check("dos minutos con trades (el vacío no tiene vela, como en 1 minuto)", string.Join(" ", dt.Select(v => TiempoV2.HoraEt(v.InicioUtc, "HH:mm"))), "09:30 ET 09:32 ET");
		c.Check("OHLC, volumen, delta y delta máximo del primer minuto", $"{dt[0].Open} {dt[0].High} {dt[0].Low} {dt[0].Close} {dt[0].Volume} {dt[0].Delta} {dt[0].MaxDelta} {dt[0].MinDelta}",
			"21400 21402 21399 21399 22 12 12 0");
		c.Check("footprint por precio (bid / ask)", string.Join(" ", dt[0].Niveles.Select(n => $"{n.Precio}:{n.Bid}/{n.Ask}")), "21399:0/7 21400:0/10 21402:5/0");

		var vivo = new ConstructorMinuto();
		var cerradas = ticks.Select(t => vivo.Trade(t.Utc, t.Precio, t.Volumen, t.Compra)).Where(v => v is not null).ToList();
		c.Check("en vivo, el minuto se entrega al llegar el primer trade del siguiente", $"{cerradas.Count} {TiempoV2.HoraEt(cerradas[0]!.InicioUtc, "HH:mm")} {cerradas[0]!.Volume}", "1 09:30 ET 22");
		c.Check("el minuto en curso hace de vela para la ruptura en el tick", $"{TiempoV2.HoraEt(vivo.Inicio, "HH:mm")} {vivo.Apertura}", "09:32 ET 21403");
		c.Check("al cerrar la vela de 5 minutos se cierra el minuto que quedaba", $"{vivo.CerrarSiTermino(t0.AddMinutes(2).AddSeconds(30)) is null} {vivo.CerrarSiTermino(t0.AddMinutes(5))?.Volume}", "True 10");
		c.Check("un trade atrasado no reabre un minuto pasado", (vivo.Trade(t0.AddMinutes(4), 21400m, 1, true) is null && vivo.Trade(t0.AddMinutes(1), 21400m, 1, true) is null && vivo.Inicio == t0.AddMinutes(4)).ToString(), "True");

		c.Seccion("v5 Sweep Pro en 5 minutos: las sesiones calculadas con velas partidas son las mismas que con velas de 1 minuto");
		var rnd = new Random(11);
		var precioActual = 21400m;
		var unMinuto = new List<VelaEntrada>();
		var inicioEt = new DateTime(2026, 11, 16, 18, 0, 0);

		for (var t = inicioEt; t < inicioEt.AddHours(23); t = t.AddMinutes(1))
		{
			var o = precioActual;
			precioActual += (rnd.Next(0, 9) - 4) * Tick;
			var (h, l) = (Math.Max(o, precioActual) + Tick, Math.Min(o, precioActual) - Tick);
			unMinuto.Add(new VelaEntrada { InicioUtc = Zonas.AUtc(t, ZonaHoraria.NuevaYork), Open = o, High = h, Low = l, Close = precioActual, Volume = 100, Delta = 10 });
		}

		var deCinco = unMinuto.Chunk(5).Select(g => new VelaEntrada
		{
			InicioUtc = g[0].InicioUtc, Open = g[0].Open, High = g.Max(v => v.High), Low = g.Min(v => v.Low), Close = g[^1].Close, Volume = g.Sum(v => v.Volume), Delta = g.Sum(v => v.Delta)
		}).ToList();

		List<string> Sesiones(IEnumerable<VelaEntrada> velas)
		{
			var m = new MotorSweeps(new ConfiguracionSweeps { TamanoTick = Tick, MinutosGrafico = 1 }) { DetalleDesdeUtc = DateTime.MaxValue };

			foreach (var v in velas)
				m.Procesar(v);

			return m.Leer(x => x.Sesiones.Instancias.Select(s => $"{s.Id} {s.Alto} {s.Bajo}").ToList());
		}

		var reales = Sesiones(unMinuto);
		var partidas = Sesiones(deCinco.SelectMany(v => MinutosV5.Partir(v, 5, Tick)));
		c.Check($"{reales.Count} sesiones (Asia, Londres, NY…) con el mismo máximo y mínimo", (reales.Count > 3 && reales.SequenceEqual(partidas)).ToString(), "True");
	}

	/// <summary>El gatillo del Indicador de Vela Compuesta a favor y cerca de la zona cuenta en el análisis del gatillo del Sweep Pro.</summary>
	private static void ConfluenciaVolumetrica(Comprobador c)
	{
		c.Seccion("v5 Sweep Pro: el GATILLO COMP del Indicador de Vela Compuesta a favor confirma la zona");

		AnalisisGatillo Analizar(bool conGatillo, int direccion)
		{
			var x = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
			var z = new ZonaOperable { Id = 1, Tipo = "LVN", Bajo = x.Pk(-4), Alto = x.Pk(0), Signo = 1 };
			var toque = x.Barra(20, Enumerable.Range(0, 10).Select(i => (8 - i, 3m, 7m)).ToArray());
			x.Barra(20, (-2, 13, 20), (-3, 14, 20), (-4, 13, 20));
			x.Barra(20, (-2, 13, 20), (-3, 14, 20), (-4, 13, 20));

			if (conGatillo)
			{
				x.Fuentes.Eventos.Add(new VolumetricTriggerEvent
				{
					Id = 1, Fuente = FuenteSuite.VelaCompuesta, Utc = x.T, CambioUtc = x.T, Version = 1, Tipo = "COMPUESTA", Direccion = direccion,
					Precio = x.Pk(-2), Bajo = x.Pk(-5), Alto = x.Pk(-1), Razones = "prueba"
				});
			}

			x.Barra(4, Enumerable.Range(0, 20).Select(i => (-3 + i, 4m, 1m)).ToArray());
			var ctx = new ContextoGatillo
			{
				Barras = x.M.Barras.ToList(), P = x.P, Tick = Tick, ContratosPorBarra = 100, Fuentes = x.Fuentes, Medianas = _ => (20, 9),
				M5 = Tendencia.Rango, M15 = Tendencia.Rango, Objetivo = (_, _) => null, Ahora = x.T
			};
			return AnalizadorGatillo.Analizar(z, toque, ctx, continuidad: false);
		}

		var sin = Analizar(false, 1);
		var con = Analizar(true, 1);
		var contra = Analizar(true, -1);
		c.Check("con el GATILLO COMP alcista en la zona larga: hallazgo y razón de intención", 
			$"{con.Hallazgos.Contains("GATILLO COMP a favor")} {con.Razones.Any(r => r.Contains("Indicador de Vela Compuesta", StringComparison.Ordinal))}", "True True");
		c.Check("sin gatillo o con uno en contra: no cuenta", $"{sin.Hallazgos.Any(h => h.StartsWith("GATILLO", StringComparison.Ordinal))} {contra.Hallazgos.Any(h => h.StartsWith("GATILLO", StringComparison.Ordinal))}",
			"False False");
		c.Check("el resultado del gatillo no empeora con la confluencia", $"{sin.Resultado} {con.Resultado}", $"{sin.Resultado} {sin.Resultado}");
	}

	/// <summary>Una vela volumétrica con 'n' trades de 100 contratos entre dos precios (fracción de compras), en 'seg' segundos.</summary>
	private static DateTime Vela(MotorVelaCompuesta m, DateTime t, decimal desde, decimal hasta, double compras, double seg, int n = 23)
	{
		for (var i = 0; i < n; i++)
		{
			var p = desde + Math.Round((hasta - desde) * i / Math.Max(1, n - 1) / Tick) * Tick;
			m.Trade(t.AddSeconds(seg * i / n), p, 100, i < n * compras);
		}

		return t.AddSeconds(seg);
	}

	private static void VelaCompuestaFast(Comprobador c)
	{
		c.Seccion("v5 F6: Indicador de Vela Compuesta (FAST): franjas, velas volumétricas de 2,300 contratos, sugerido");
		c.Check("franjas (hora NY): 19:00 Asia, 03:00 Londres, 09:00 Premercado, 09:45 NY apertura, 11:00 NY mañana, 13:00 mediodía, 15:00 tarde",
			string.Join(" ", new[] { 19, 3, 9, 9.75, 11, 13, 15 }.Select(h => Franjas.De(Utc(new DateTime(2026, 11, 19).AddHours(h))))),
			"Asia Londres Premercado NyApertura NyManana NyMediodia NyTarde");

		var m = new MotorVelaCompuesta(Tick);
		var t = Utc(new DateTime(2026, 11, 19, 9, 45, 0));
		t = Vela(m, t, 21400, 21402, 0.5, 20, 50);
		c.Check("5,000 contratos con el margen base de 2,300: dos velas volumétricas cerradas", m.Leer(x => $"{x.Barras.Barras.Count} {x.Barras.Barras.All(b => b.Volumen == 2300)}"), "2 True");

		var m2 = new MotorVelaCompuesta(Tick);
		m2.P.Contratos[FranjaHoraria.NyApertura] = 1000;
		Vela(m2, t, 21400, 21402, 0.5, 20, 50);
		c.Check("con 1,000 en la franja NY apertura: velas de 1,000", m2.Leer(x => $"{x.Barras.Barras.Count} {x.Barras.Barras[0].Volumen}"), "5 1000");

		var m3 = new MotorVelaCompuesta(Tick);
		var inicio = new DateTime(2026, 11, 19, 9, 31, 0);

		for (var i = 0; i < 25; i++)
			m3.Vela(new VelaEntrada { InicioUtc = Utc(inicio.AddMinutes(i)), Open = 21400, High = 21401, Low = 21399, Close = 21400, Volume = 2000 + i * 20, Delta = 0 }, Tick);

		c.Check("sugerido: promedio de las últimas 20 velas (2,100 a 2,480 → 2,290 → 2,300 en centenas)", m3.Sugerido().ToString(), "2300");
		c.Check("sugerido de la franja (las de NY apertura)", m3.Sugerido(FranjaHoraria.NyApertura).ToString(), "2300");
		c.Check("sin velas de esa franja: sin sugerido", m3.Sugerido(FranjaHoraria.Asia).ToString(), "0");

		c.Seccion("v5 F6: GATILLO VELAS VOLUMÉTRICAS (una vela de desplazamiento) y GATILLO COMP (vela compuesta)");
		var v = new MotorVelaCompuesta(Tick);
		var senales = new List<SenalVolumetrica>();
		v.AlSenal = senales.Add;
		var tv = Utc(new DateTime(2026, 11, 19, 10, 0, 0));

		// 30 velas normales: 20 s, 3 ticks, equilibradas.
		for (var i = 0; i < 30; i++)
			tv = Vela(v, tv, 21400, 21400.75m, 0.5, 20);

		var antes = senales.Count;
		tv = Vela(v, tv, 21401, 21405, 0.85, 3);
		var vol = senales.Skip(antes).FirstOrDefault(s => s.Tipo == "VOLUMETRICA");
		c.Check("una vela de 16 ticks en 3 s con 70 % de delta comprador que cierra arriba: GATILLO VOLUMÉTRICO ALCISTA",
			vol is null ? "nada" : $"{vol.Tipo} {vol.Direccion} {F(vol.Precio)}", "VOLUMETRICA 1 21405");

		var comp = new MotorVelaCompuesta(Tick);
		var sc = new List<SenalVolumetrica>();
		comp.AlSenal = sc.Add;
		var tc = Utc(new DateTime(2026, 11, 19, 10, 30, 0));

		for (var i = 0; i < 10; i++)
			tc = Vela(comp, tc, 21390, 21390.75m, 0.5, 20);

		// Tres velas absorbiendo ventas abajo (21400-21401) y una que arranca a 21410 con compras.
		tc = Vela(comp, tc, 21400, 21401, 0.3, 20);
		tc = Vela(comp, tc, 21401, 21400, 0.3, 20);
		tc = Vela(comp, tc, 21400, 21401, 0.35, 20);
		var antesComp = sc.Count;
		Vela(comp, tc, 21402, 21410, 0.75, 15);
		var compuesta = sc.Skip(antesComp).FirstOrDefault(s => s.Tipo == "COMPUESTA");
		c.Check("POC abajo, ventas absorbidas y la última vira a compra y cierra en el tercio superior: GATILLO COMP ALCISTA",
			compuesta is null ? "nada" : $"{compuesta.Tipo} {compuesta.Direccion} {F(compuesta.Bajo)}-{F(compuesta.Alto)}", "COMPUESTA 1 21400-21410");
		var sinComp = new MotorVelaCompuesta(Tick);
		sinComp.P.GatilloComp = false;
		sinComp.P.GatilloVolumetrico = false;
		var ninguna = new List<SenalVolumetrica>();
		sinComp.AlSenal = ninguna.Add;
		var tn = Utc(new DateTime(2026, 11, 19, 10, 30, 0));

		for (var i = 0; i < 10; i++)
			tn = Vela(sinComp, tn, 21390, 21390.75m, 0.5, 20);

		tn = Vela(sinComp, tn, 21400, 21401, 0.3, 20);
		tn = Vela(sinComp, tn, 21401, 21400, 0.3, 20);
		tn = Vela(sinComp, tn, 21400, 21401, 0.35, 20);
		Vela(sinComp, tn, 21402, 21410, 0.75, 3);
		c.Check("con los dos botones apagados no hay gatillos", ninguna.Count.ToString(), "0");

		var f = new MotorVelaCompuesta(Tick);
		var tf = new DateTime(2026, 11, 19, 11, 0, 0);
		f.Vela(new VelaEntrada { InicioUtc = Utc(tf), Open = 21400, High = 21402, Low = 21398, Close = 21401, Volume = 900 }, Tick);
		f.Vela(new VelaEntrada { InicioUtc = Utc(tf.AddMinutes(1)), Open = 21401, High = 21410, Low = 21400, Close = 21409, Volume = 900 }, Tick);
		f.Vela(new VelaEntrada { InicioUtc = Utc(tf.AddMinutes(2)), Open = 21409, High = 21412, Low = 21405, Close = 21411, Volume = 900 }, Tick);
		c.Check("FVG de las velas de 1 minuto (el botón FVG los muestra u oculta)", f.Leer(x => string.Join(" ", x.Fvg.Fvgs.Select(g => $"{(g.Alcista ? "+" : "-")}{F(g.Bajo)}-{F(g.Alto)}"))), "+21402-21405");
	}
}
