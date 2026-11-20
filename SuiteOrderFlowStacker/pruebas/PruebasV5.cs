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
