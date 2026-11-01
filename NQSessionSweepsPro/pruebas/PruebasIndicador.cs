using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

using NqSweeps.Nucleo;

using static Datos;
using static PruebasBarridos;

/// <summary>El indicador real (Atas/) contra los sustitutos de ATAS: carga sin avisos, ruptura en vivo, correo, temporalidad.</summary>
internal static class PruebasIndicador
{
	private static IndicatorCandle Vela(VelaEntrada e)
	{
		var c = new IndicatorCandle { Time = e.InicioUtc, Open = e.Open, High = e.High, Low = e.Low, Close = e.Close, Volume = e.Volume, Delta = e.Delta, Ask = e.Ask, Bid = e.Bid };

		foreach (var n in e.Niveles)
			c.Levels[n.Precio] = (n.Bid, n.Ask);

		return c;
	}

	/// <summary>Lunes 18:00 a martes 09:35 ET: barrido del máximo de Londres a las 08:45 (histórico) y vela abierta a las 09:35.</summary>
	private static List<IndicatorCandle> Historia()
	{
		var velas = new List<IndicatorCandle>();

		foreach (var v in Planas(Martes.AddHours(-6), Martes.AddHours(9) + TimeSpan.FromMinutes(35), 20000))
		{
			var et = Zonas.AEt(v.InicioUtc);

			if (et.Hour == 20 && et.Minute == 0)
				(v.High, v.Low) = (20030, 19970);
			else if (et.Hour == 5 && et.Minute == 0)
				v.High = 20010;
			else if (et.Hour == 6 && et.Minute == 0)
				v.Low = 19990;

			velas.Add(Vela(v));
		}

		var indice = velas.FindIndex(c => Zonas.AEt(c.Time) == Martes.AddHours(8.75));
		velas[indice] = Vela(VelaBarridoMax(Martes.AddHours(8.75)));

		for (var i = 1; i <= 6; i++)
			velas[indice + i] = Vela(V(Martes.AddHours(8.75).AddMinutes(i), 20004, 20005, 19998, 19999));

		// Vela abierta de las 09:35 (abre por encima del mínimo de Londres, 19990).
		velas.Add(Vela(V(Martes.AddHours(9).AddMinutes(35), 19994, 19995, 19993, 19994)));
		return velas;
	}

	private static NQSessionSweepsPro Crear(List<IndicatorCandle> velas, Action<NQSessionSweepsPro>? ajustes = null)
	{
		var ind = new NQSessionSweepsPro
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick, Instrument = "NQZ6" },
			ChartInfo = new ChartInfo { TestYByPrice = p => (int)Math.Round((20040 - p) * 10) },
			TestCandles = velas,
			FirstVisibleBarNumber = velas.Count - 120,
			LastVisibleBarNumber = velas.Count - 1,
			EmailTo = "owner@example.com"
		};

		ajustes?.Invoke(ind);
		ind.TestCalcAll();
		return ind;
	}

	public static void Ejecutar(Comprobador c)
	{
		var correos = new List<string>();
		Correo.EsperaReintento = TimeSpan.FromMilliseconds(10);
		Correo.Enviador = (cfg, asunto, cuerpo) =>
		{
			lock (correos)
				correos.Add($"{cfg.Para}|{asunto}");
		};

		CargaYVivo(c, correos);
		Temporalidad(c);
		CorreoDePrueba(c, correos);
		Correo.Enviador = null;
	}

	private static bool Esperar(Func<bool> condicion)
	{
		var reloj = Stopwatch.StartNew();

		while (!condicion() && reloj.ElapsedMilliseconds < 3000)
			Thread.Sleep(20);

		return condicion();
	}

	private static void CargaYVivo(Comprobador c, List<string> correos)
	{
		c.Seccion("Indicador: carga del historial sin avisos, dibujo, ruptura en el tick en vivo, alerta y correo");

		var velas = Historia();
		var ind = Crear(velas);
		c.Check("el historial tiene un barrido (08:45) pero no suena ninguna alerta ni sale ningún correo",
			$"alertas {ind.Alertas.Count} correos {correos.Count}", "alertas 0 correos 0");

		var r = ind.TestRender();
		var textos = r.Texts.Select(t => t.Text).ToList();
		c.Check("dibujo: nombres de sesión, etiqueta del barrido y panel de datos",
			$"{textos.Contains("Londres")} {textos.Contains("SWEEP REAL 9/10")} {textos.Contains("MAX Londres 20010.00 | 08:45:00 ET")}", "True True True");
		c.Check("tabla de sesiones con el contexto H1 y la hora ET", $"{textos.Contains("H1 Rango")} {textos.Any(t => t.Contains(" ET  Abre en") || t.Contains(" ET  Cierra en") || t.Contains(" ET  Fin de semana"))}", "True True");
		c.Check("etiquetas de niveles a la derecha (el MIN de Londres sigue vigilado)", textos.Contains("MIN Londres 19990.00").ToString(), "True");
		c.Check("todo el texto del gráfico en ASCII", textos.All(t => t.All(ch => ch < 128)).ToString(), "True");

		ind.TestTrade(new MarketDataArg { Time = Utc(Martes.AddHours(9).AddMinutes(35).AddSeconds(20)), Price = 19989.75m, Volume = 15, Direction = TradeDirection.Sell });
		c.Check("trade en vivo que cruza el mínimo: alerta en ese segundo, con el sonido elegido", string.Join(" | ", ind.Alertas), "alert1|NQ rompe MIN Londres 19990.00 a las 09:35:20 ET");
		c.Check("y el correo de barrido sale en segundo plano", Esperar(() => correos.Count == 1) ? correos[0] : "ninguno",
			"owner@example.com|NQ BARRIDO SELLSIDE - MIN Londres 19990.00 - 09:35");

		// La vela de las 09:35 cierra con ventas fuertes más allá y vuelve: clasificación, sin repetir la ruptura.
		velas[^1] = Vela(V(Martes.AddHours(9).AddMinutes(35), 19994, 19995, 19988, 19993, 100, 0, (19989m, 250, 50), (19988.5m, 200, 40)));
		velas.Add(Vela(V(Martes.AddHours(9).AddMinutes(36), 19993, 19994, 19992, 19993)));
		ind.TestCalc(velas.Count - 1);
		// En vivo hay un punto más que en el histórico: la velocidad de venta medida con los trades cae más de un 50 %
		// antes del cierre del minuto (agotamiento).
		c.Check("al cerrar la vela: clasificación (10/10 con el agotamiento medido en vivo), sin otra alerta de ruptura",
			string.Join(" | ", ind.Alertas.Skip(1)), "alert1|SWEEP REAL 10/10 - MIN Londres 19990.00 (1m)");
	}

	private static void Temporalidad(Comprobador c)
	{
		c.Seccion("Indicador: solo gráficos de 1 a 5 minutos");

		var m15 = Crear(Historia(), i => i.ChartInfo!.TimeFrame = "M15");
		c.Check("15 minutos: aviso y sin cálculo", m15.TestRender().Texts.Select(t => t.Text).FirstOrDefault() ?? "", "Session Sweeps funciona en graficos de 1 a 5 min");

		var ticks = Crear(Historia(), i => i.ChartInfo!.ChartType = "Tick");
		c.Check("gráfico de ticks: el mismo aviso", ticks.TestRender().Texts.Select(t => t.Text).FirstOrDefault() ?? "", "Session Sweeps funciona en graficos de 1 a 5 min");

		var m5 = Crear(Historia(), i => i.ChartInfo!.TimeFrame = "M5");
		c.Check("5 minutos: calcula", m5.TestRender().Texts.Any(t => t.Text == "Londres").ToString(), "True");
	}

	private static void CorreoDePrueba(Comprobador c, List<string> correos)
	{
		c.Seccion("Indicador: correo de prueba, anti-duplicados y errores sin bloquear");

		lock (correos)
			correos.Clear();

		var ind = Crear(Historia());
		ind.SendTestEmail = true;
		c.Check("SendTestEmail envía un correo de prueba y se desmarca solo",
			$"{(Esperar(() => correos.Count == 1) ? correos[0] : "ninguno")} / {ind.SendTestEmail}", "owner@example.com|NQ Session Sweeps Pro - correo de prueba / False");

		var sinDestino = Crear(Historia(), i => i.EmailTo = "");
		sinDestino.SendTestEmail = true;
		Thread.Sleep(100);
		c.Check("sin EmailTo no se envía nada", correos.Count.ToString(), "1");

		var errores = 0;
		var cola = new Correo((_, _) => Interlocked.Increment(ref errores));
		Correo.Enviador = (_, _, _) => throw new InvalidOperationException("SMTP caído");
		var reloj = Stopwatch.StartNew();
		var primera = cola.Encolar("clave", new ConfiguracionCorreo("smtp", 587, true, "u", null, "x@y.z"), "a", "b");
		var segunda = cola.Encolar("clave", new ConfiguracionCorreo("smtp", 587, true, "u", null, "x@y.z"), "a", "b");
		var bloqueo = reloj.ElapsedMilliseconds;
		c.Check("encolar no bloquea aunque el SMTP falle; la misma clave solo una vez", $"{bloqueo < 50} {primera} {segunda}", "True True False");
		c.Check("tras 2 reintentos, un error al log", Esperar(() => errores == 1).ToString(), "True");
		cola.Dispose();
	}
}
