using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>Fuentes de la suite a mano para las pruebas.</summary>
internal sealed class FuentesPrueba : IFuentesSuite
{
	public readonly List<EventoSuite> Eventos = new();
	public readonly Dictionary<FuenteSuite, EstadoFuente> Estados = new();

	public EstadoFuente Estado(FuenteSuite fuente) => Estados.TryGetValue(fuente, out var e) ? e : EstadoFuente.Cargado;

	public List<T> Consultar<T>(DateTime desde, DateTime hasta, Func<T, bool>? filtro = null) where T : EventoSuite =>
		Eventos.OfType<T>().Where(e => e.Utc <= hasta && e.CambioUtc >= desde).Where(e => filtro is null || filtro(e)).ToList();
}

/// <summary>
/// Escenario de barrido sobre barras de volumen de 100 contratos. s = +1: barrido del máximo L = 21420 (buyside); s = -1:
/// el espejo exacto (sellside, L = 21380). P(k) = L + s·k·tick.
/// </summary>
internal sealed class Escenario
{
	public readonly MotorBarrasVolumen M = new() { Tick = Tick, ContratosPorBarra = 100 };
	public readonly FuentesPrueba Fuentes = new();
	public readonly ParametrosV2 P = new();
	public readonly int S;
	public readonly decimal L;
	public DateTime T;
	public EvaluacionBarrido E = null!;
	public bool? Pine = true;
	public bool Cvd = true;
	public decimal Pico = 100;

	public Escenario(int s, DateTime inicioEt)
	{
		S = s;
		L = s > 0 ? 21420m : 21380m;
		T = Utc(inicioEt);

		// 30 barras previas (10 precios, 5/5 cada uno, 20 s) lejos del nivel.
		for (var i = 0; i < 30; i++)
			Barra(20, Enumerable.Range(-30, 10).Select(k => (k, 5m, 5m)).ToArray());
	}

	public decimal Pk(int k) => L + S * k * Tick;

	/// <summary>Una barra: niveles (k, agresión a favor de s, agresión en contra) en orden; el volumen suma 100.</summary>
	public int Barra(double seg, params (int K, decimal AFavor, decimal EnContra)[] niveles)
	{
		var dt = TimeSpan.FromSeconds(seg / niveles.Length);

		foreach (var (k, aFavor, enContra) in niveles)
		{
			// "A favor de s" = compra si s = +1 (barrido de un máximo), venta si s = -1.
			if (aFavor > 0)
				M.Agregar(T, Pk(k), aFavor, S > 0);

			if (enContra > 0)
				M.Agregar(T, Pk(k), enContra, S < 0);

			T += dt;
		}

		return M.Barras.Count - 1;
	}

	/// <summary>La llegada, la barra de T0 (extremo en P6 con subasta terminada), F2 de 2 barras y F3 con cierre de vuelta.</summary>
	public void Trampa(bool subastaTerminada = true, double f3Seg = 4, int f2Barras = 2)
	{
		Barra(20, Enumerable.Range(-14, 10).Select(k => (k, 7m, 3m)).ToArray());
		var nivel = new NivelLiquidez { Id = 1, Precio = L, Lado = S > 0 ? Lado.Buyside : Lado.Sellside, Grupo = "dia", Origen = "Dia anterior", CreadoUtc = T.AddHours(-2) };
		var t0 = T + TimeSpan.FromSeconds(6.0 / 11 * 6);
		var b0 = Barra(6, (-4, 8, 0), (-3, 8, 0), (-2, 8, 0), (-1, 8, 0), (0, 8, 0), (1, 6, 5), (2, 6, 5), (3, 6, 6), (4, 5, 6),
			subastaTerminada ? (5, 5, 5) : (5, 5, 4), subastaTerminada ? (6, 5, 0) : (6, 3, 3));
		E = new EvaluacionBarrido { Id = 1, Nivel = nivel, Signo = S, L = L, Extremo = Pk(6), T0Utc = t0, ExtremoUtc = M.Barras[b0].FinUtc, BarraT0 = b0 };

		for (var i = 0; i < f2Barras; i++)
			Barra(20, (3, 13, 20), (4, 14, 20), (5, 13, 20));

		Barra(f3Seg, Enumerable.Range(0, 20).Select(i => (5 - i, 1m, 4m)).ToArray());
	}

	public ContextoClasificador Ctx() => new()
	{
		Barras = M.Barras,
		P = P,
		Tick = Tick,
		ContratosPorBarra = 100,
		Fuentes = Fuentes,
		Ahora = T,
		Medianas = _ => (20, 9),
		PicoVelocidad = (_, _) => Pico,
		MedianaVelocidad = () => 20,
		MedianaVolBeyond = () => null,
		H1 = () => Tendencia.Rango,
		M15 = () => Tendencia.Rango,
		DivergenciaCvd = _ => Cvd,
		Vwap = _ => L,
		Pine = _ => Pine,
		ToquesNivel = _ => 1
	};

	/// <summary>Evalúa barra a barra (como el motor) y devuelve las transiciones.</summary>
	public List<string> Evaluar(int desde = 0)
	{
		var r = new List<string>();
		var barras = M.Barras.ToList();

		for (var n = Math.Max(E.BarraT0 + 1, desde); n <= barras.Count; n++)
		{
			var c = Ctx();
			c.Barras = barras.Take(n).ToList();
			c.Ahora = c.Barras[^1].FinUtc;

			if (Clasificador.Evaluar(E, c) is { } t)
				r.Add(t);
		}

		return r;
	}

	public void Iceberg(bool mbo, string estado = "AGUANTANDO", int k = 6) => Fuentes.Eventos.Add(new IcebergEvent
	{
		Id = 77, Fuente = FuenteSuite.Iceberg, Utc = E.T0Utc.AddSeconds(2), CambioUtc = E.T0Utc.AddSeconds(30), Precio = Pk(k), Compra = S < 0, Estado = estado, Mbo = mbo, Ejecutado = 300
	});

	public void SuiteBasica()
	{
		Fuentes.Eventos.Add(new StopsEvent { Id = 1, Fuente = FuenteSuite.Stops, Utc = E.T0Utc.AddSeconds(1), CambioUtc = E.T0Utc.AddSeconds(1), Desde = Pk(1), Extremo = Pk(4), Compra = S > 0, Contratos = 300, Estado = "TRAMPA", Zona = "PDH" });
		Fuentes.Eventos.Add(new StackedEvent { Id = 2, Fuente = FuenteSuite.Stacked, Utc = E.T0Utc.AddSeconds(10), CambioUtc = E.T0Utc.AddSeconds(10), Bajo = Math.Min(Pk(3), Pk(5)), Alto = Math.Max(Pk(3), Pk(5)), Compra = S < 0, Niveles = 3 });
		Fuentes.Eventos.Add(new StackedEvent { Id = 3, Fuente = FuenteSuite.Stacked, Utc = E.T0Utc.AddSeconds(46), CambioUtc = E.T0Utc.AddSeconds(46), Bajo = Math.Min(Pk(-8), Pk(-6)), Alto = Math.Max(Pk(-8), Pk(-6)), Compra = S < 0, Niveles = 3 });
	}
}

internal static class PruebasV2Clasificador
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S3 Clasificador: T1 TRAMPA PREMIUM completa (buyside) y su espejo sellside");

		foreach (var s in new[] { +1, -1 })
		{
			var x = new Escenario(s, new DateTime(2026, 11, 2, 9, 31, 0));
			x.Trampa();
			x.SuiteBasica();
			x.Iceberg(mbo: true);
			var t = x.Evaluar();
			var lado = s > 0 ? "buyside" : "sellside";
			c.Check($"T1 {lado}: TRAMPA PREMIUM al cierre de vuelta", $"{string.Join(",", t)} | {x.E.Estado}", "BARRIDO REAL PREMIUM | Premium");
			c.Check($"T1 {lado}: bloques A-E, total y puertas", $"{F(x.E.ScoreA)} {F(x.E.ScoreB)} {F(x.E.ScoreC)} {F(x.E.ScoreD)} {F(x.E.ScoreE)} = {F(x.E.Total)} | {x.E.PremiumGates}", "15 16 25 25 10 = 91 | 1111111");
			c.Check($"T1 {lado}: congelado, ReclaimBars 3, recuperación 20 ticks", $"{x.E.Congelada} {x.E.ReclaimBars} {x.E.RecoveryTicks}", "True 3 20");
			c.Check($"T9 {lado}: el cierre de vuelta suma una sola vez", x.E.Desglose.Count(d => d.Texto.StartsWith("Cierre de vuelta")).ToString(), "1");
			c.Check($"1.6 {lado}: el stacked del extremo (C) y el de la nueva dirección (D) son zonas distintas", $"{x.E.StackedAgainst} {x.E.StackedNewDir}", "True True");
		}

		c.Seccion("v4 G3: consolidación de 10 velas de 1 minuto en el extremo con absorción y CVD que gira → BARRIDO REAL");

		foreach (var s in new[] { +1, -1 })
		{
			Escenario Consolidacion(int maxVelas)
			{
				// Como Trampa(), pero con 10 minutos de consolidación entre el extremo y el F3.
				var x = new Escenario(s, new DateTime(2026, 11, 2, 9, 31, 0));
				x.P.ConsolidationMaxCandles = maxVelas;
				x.Barra(20, Enumerable.Range(-14, 10).Select(k => (k, 7m, 3m)).ToArray());
				var nivel = new NivelLiquidez { Id = 9, Precio = x.L, Lado = s > 0 ? Lado.Buyside : Lado.Sellside, Grupo = "dia", Origen = "Dia anterior", CreadoUtc = x.T.AddHours(-2) };
				var t0 = x.T + TimeSpan.FromSeconds(6.0 / 11 * 6);
				var b0 = x.Barra(6, (-4, 8, 0), (-3, 8, 0), (-2, 8, 0), (-1, 8, 0), (0, 8, 0), (1, 6, 5), (2, 6, 5), (3, 6, 6), (4, 5, 6), (5, 5, 5), (6, 5, 0));
				x.E = new EvaluacionBarrido { Id = 9, Nivel = nivel, Signo = s, L = x.L, Extremo = x.Pk(6), T0Utc = t0, ExtremoUtc = x.M.Barras[b0].FinUtc, BarraT0 = b0 };
				x.SuiteBasica();

				// 10 minutos consolidando en el extremo: volumen alto con los vendedores (en un máximo) absorbiendo.
				for (var i = 0; i < 10; i++)
					x.Barra(60, (3, 13, 20), (4, 14, 20), (5, 13, 20));

				x.Barra(4, Enumerable.Range(0, 20).Select(i => (5 - i, 1m, 4m)).ToArray());
				x.Evaluar();
				return x;
			}

			var lado = s > 0 ? "buyside" : "sellside";
			var tc = Consolidacion(14);
			c.Check($"v4 {lado}: consolida 10 velas sin llamarse RUPTURA y gira: BARRIDO REAL (no FALSO ni INDETERMINADO)", tc.E.Estado.EsReal() ? "REAL" : tc.E.Estado.ToString(), "REAL");
			c.Check($"v4 {lado}: absorción en consolidación con compras y ventas y el CVD que gira",
				$"{tc.E.AbsorcionConsolidacion} {tc.E.ConsolidacionVelas >= 10} {tc.E.CvdGiro} {tc.E.VolumenVenta > tc.E.VolumenCompra == (s > 0)}", "True True True True");
			c.Check($"v4 {lado}: cierre de vuelta tras consolidar suma (+4) aunque pase de ReclaimMaxBars",
				tc.E.Desglose.Any(d => d.Texto.StartsWith("Cierre de vuelta tras consolidar", StringComparison.Ordinal)).ToString(), "True");
			var corta = Consolidacion(1);
			c.Check($"v4 {lado}: con la ventana de antes (5 minutos) la misma consolidación vencía sin ser REAL", corta.E.Estado.EsReal() ? "REAL" : "vencida", "vencida");
		}

		c.Seccion("S3 T2-T4: sin iceberg ni F2 >= 3; iceberg inferido; sin liquidez");
		var t2 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t2.Trampa();
		t2.SuiteBasica();
		t2.Evaluar();
		c.Check("T2 sin iceberg ni F2 >= 3 (puerta 3): TRAMPA A", $"{t2.E.Estado} {F(t2.E.Total)} {t2.E.PremiumGates}", "Real 85 1101111");
		c.Check("v4: recuperación inmediata: la consolidación no suma (cada hecho una vez)", t2.E.AbsorcionConsolidacion.ToString(), "False");

		var t3 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t3.Trampa();
		t3.SuiteBasica();
		t3.Iceberg(mbo: false);
		t3.Evaluar();
		c.Check("T3 iceberg INFERIDO suma +3 (no +6)", F(t3.E.Desglose.Single(d => d.Texto.StartsWith("Iceberg")).Puntos), "3");
		c.Check("T3 modo del iceberg INFERIDO (el panel dirá 'Probable', nunca 'Certificada')", t3.E.IcebergModo, "INFERIDO");

		var t4 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0)) { Pico = 20 };
		t4.Barra(20, Enumerable.Range(-12, 10).Select(k => (k, 5m, 5m)).ToArray());
		var nivel4 = new NivelLiquidez { Id = 4, Precio = t4.L, Lado = Lado.Buyside, Grupo = "dia", Origen = "Dia anterior", CreadoUtc = t4.T.AddHours(-2) };
		var b4 = t4.Barra(20, Enumerable.Range(-7, 10).Select(k => (k, 5m, 5m)).ToArray());
		t4.E = new EvaluacionBarrido { Id = 4, Nivel = nivel4, Signo = 1, L = t4.L, Extremo = t4.Pk(2), T0Utc = t4.M.Barras[b4].FinUtc.AddSeconds(-1), BarraT0 = b4 };
		t4.Barra(20, Enumerable.Range(0, 10).Select(i => (1 - i, 5m, 5m)).ToArray());
		t4.Evaluar();
		c.Check("T4 penetración 2 ticks, sin ráfaga ni stops: SIN LIQ", t4.E.Estado.ToString(), "SinLiquidez");

		c.Seccion("S3 T5-T7: RUPTURA, FALLIDO y RUPTURA FALLIDA");
		var t5 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t5.Barra(20, Enumerable.Range(-12, 10).Select(k => (k, 7m, 3m)).ToArray());
		var nivel5 = new NivelLiquidez { Id = 5, Precio = t5.L, Lado = Lado.Buyside, Grupo = "dia", Origen = "Dia anterior", CreadoUtc = t5.T.AddHours(-2) };
		var b5 = t5.Barra(20, Enumerable.Range(-2, 10).Select(k => (k, 8m, 2m)).ToArray());
		t5.E = new EvaluacionBarrido { Id = 5, Nivel = nivel5, Signo = 1, L = t5.L, Extremo = t5.Pk(7), T0Utc = t5.M.Barras[b5].InicioUtc.AddSeconds(8), BarraT0 = b5 };
		t5.Barra(20, Enumerable.Range(6, 10).Select(k => (k, 7m, 3m)).ToArray());
		t5.Barra(20, Enumerable.Range(14, 10).Select(k => (k, 7m, 3m)).ToArray());
		t5.Evaluar();
		c.Check("v4: cierra 23 ticks más allá (dentro del barrido máximo, 24) y a los 2 minutos: aún no es RUPTURA", t5.E.Estado.ToString(), "Pendiente");
		t5.Barra(20, Enumerable.Range(24, 10).Select(k => (k, 7m, 3m)).ToArray());
		t5.Evaluar();
		c.Check("T5 aceptación, POC más allá, delta sostenido y sin absorción, y el precio se aleja más del barrido máximo: RUPTURA",
			$"{t5.E.Estado} {Clasificador.CondicionesRuptura(t5.E, t5.Ctx())}", "Ruptura 4");

		// T7: la ruptura vuelve 4 ticks bajo el nivel en la barra siguiente.
		var antes = t5.M.Barras.Count;
		t5.Barra(4, Enumerable.Range(0, 10).Select(i => (20 - i * 3, 2m, 8m)).ToArray());
		var t7 = t5.Evaluar(antes + 1);
		c.Check("T7 ruptura que vuelve en 1 barra: RUPTURA FALLIDA", $"{string.Join(",", t7)} {t5.E.Estado}", "RUPTURA FALLIDA RupturaFallida");

		// T6: trampa congelada y cierre 3 ticks más allá del extremo.
		var t6 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t6.Trampa();
		t6.SuiteBasica();
		t6.Iceberg(mbo: true);
		t6.Evaluar();
		var n6 = t6.M.Barras.Count;
		t6.Barra(10, Enumerable.Range(0, 10).Select(i => (-6 + i * 2, 8m, 2m)).ToArray());
		var tr6 = t6.Evaluar(n6 + 1);
		c.Check("T6 trampa que cierra más allá del extremo + 3 ticks: FALLIDO", $"{string.Join(",", tr6)} {t6.E.Estado}", "FALLIDO Fallido");

		c.Seccion("S3 T10-T13: subasta sin terminar, F3 lenta, conflicto Pine, noticia");
		var t10 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t10.Trampa(subastaTerminada: false);
		t10.SuiteBasica();
		t10.Iceberg(mbo: true);
		t10.Evaluar();
		c.Check("T10 subasta sin terminar: -4 y no premium (puerta 4)", $"{F(t10.E.Penalties)} {t10.E.Estado} {t10.E.PremiumGates[3]}", "4 Real 0");

		var t11 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t11.Trampa(f3Seg: 30);
		t11.SuiteBasica();
		t11.Iceberg(mbo: true);
		t11.Evaluar();
		c.Check("T11 F3 lenta: -3, sin F3 rápida (trampa por giro de delta, no premium)",
			$"{F(t11.E.Penalties)} {t11.E.F3Rapida} {t11.E.Estado}", "3 False Real");

		var t12 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t12.Pine = false;
		t12.Trampa();
		t12.SuiteBasica();
		t12.Iceberg(mbo: true);
		t12.Evaluar();
		c.Check("T12 Pine en contra: el estado lo decide el Order Flow (sigue PREMIUM) y se marca CONFLICTO PINE", $"{t12.E.Estado} {t12.E.ConflictoPine} {F(t12.E.ScoreE)}", "Premium True 8");

		var t13 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 47, 0));
		t13.Trampa();
		t13.SuiteBasica();
		t13.Iceberg(mbo: true);
		t13.Evaluar();
		c.Check("T13 noticia a 3 min (10:00): el contexto no suma, se marca NOTICIA y se registra igual",
			$"{t13.E.Noticia} {F(t13.E.ScoreE)} {t13.E.Congelada}", "True 0 True");

		c.Seccion("S3 T15, T18, T19: upgrade premium, deduplicación y fuentes apagadas");
		var t15 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t15.Trampa();
		t15.SuiteBasica();
		t15.Evaluar();
		c.Check("T15 primero TRAMPA A", t15.E.Estado.ToString(), "Real");
		t15.Iceberg(mbo: true);
		var n15 = t15.M.Barras.Count;
		t15.Barra(20, Enumerable.Range(-14, 4).Select(k => (k, 12.5m, 12.5m)).ToArray());
		var tr15 = t15.Evaluar(n15 + 1);
		c.Check("T15 llega el iceberg: UPGRADE PREMIUM (sin repetir la confirmación)", $"{string.Join(",", tr15)} {t15.E.Estado} {t15.E.UpgradePremium}", "UPGRADE PREMIUM Premium True");

		var t18 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t18.Trampa();
		t18.SuiteBasica();
		t18.Iceberg(mbo: true);
		t18.Fuentes.Eventos.Add(new AbsorptionEvent { Id = 9, Fuente = FuenteSuite.Absorption, Utc = t18.E.T0Utc.AddSeconds(31), CambioUtc = t18.E.T0Utc.AddSeconds(31), Precio = t18.Pk(6), Compra = false, Score = 80, Setup = "SetupB" });
		t18.Evaluar();
		c.Check("T18 mismo iceberg del Iceberg Tracker y del Absorption Master: cuenta una vez",
			$"{t18.E.Desglose.Count(d => d.Texto.StartsWith("Iceberg"))} {t18.E.Desglose.Count(d => d.Texto.StartsWith("Absorption"))}", "1 0");

		var t19 = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		t19.Trampa();
		t19.SuiteBasica();
		t19.Iceberg(mbo: true);
		t19.P.Iceberg.Enabled = false;
		t19.Fuentes.Estados[FuenteSuite.Stops] = EstadoFuente.NoCargado;
		t19.Evaluar();
		c.Check("T19 iceberg APAGADO y Stops NO CARGADO: 0 puntos y fuera de las cargadas",
			$"{t19.E.Desglose.Any(d => d.Texto.StartsWith("Iceberg") || d.Texto.StartsWith("Stops"))} {t19.E.SourcesLoaded.Contains("Stops")}", "False False");

		c.Seccion("S3 T17: horario de verano en Nueva York y hora de CDMX");
		c.Check("T17 30-oct 09:30 ET (verano) = 07:30 CDMX", TiempoV2.HoraCdmx(Utc(new DateTime(2026, 10, 30, 9, 30, 0))), "07:30 CDMX");
		c.Check("T17 2-nov 09:57 ET (invierno) = 08:57 CDMX (el ejemplo del documento)", TiempoV2.HoraCdmx(Utc(new DateTime(2026, 11, 2, 9, 57, 0))), "08:57 CDMX");

		c.Seccion("S3 T8: el motor real detecta T0 en el tick (:50 del minuto) y ancla la ventana a T0");
		var cfg = new ConfiguracionSweeps { TamanoTick = Tick, MinutosGrafico = 1 };
		var motor = new MotorSweeps(cfg) { Almacen = new AlmacenTicks() };

		Vela V(DateTime et, decimal o, decimal h, decimal l, decimal cierre) => new()
		{
			InicioUtc = Utc(et), FinUtc = Utc(et).AddMinutes(1), Open = o, High = h, Low = l, Close = cierre, Volume = 100
		};

		VelaEntrada E(Vela v) => new() { InicioUtc = v.InicioUtc, Open = v.Open, High = v.High, Low = v.Low, Close = v.Close, Volume = v.Volume };

		for (var t = new DateTime(2026, 10, 29, 9, 30, 0); t < new DateTime(2026, 10, 29, 10, 30, 0); t = t.AddMinutes(1))
			motor.Procesar(E(V(t, 21400, t.Minute == 0 ? 21420 : 21405, 21395, 21400)));

		for (var t = new DateTime(2026, 10, 29, 18, 0, 0); t < new DateTime(2026, 10, 30, 9, 41, 0); t = t.AddMinutes(1))
			motor.Procesar(E(V(t, 21400, 21405, 21395, 21400)));

		var t0 = Utc(new DateTime(2026, 10, 30, 9, 41, 50));
		motor.Trade(t0.AddSeconds(-5), 21419.75m, 5, true, 21400, Utc(new DateTime(2026, 10, 30, 9, 41, 0)));
		motor.Trade(t0, 21420.50m, 5, true, 21400, Utc(new DateTime(2026, 10, 30, 9, 41, 0)));
		var ev = motor.Leer(m => m.Evaluaciones.Where(e => e.Nivel.Grupo == "dia").ToList());
		c.Check("T8 una evaluación del PDH con T0 = 09:41:50 ET (no la apertura del minuto)",
			ev.Count == 1 ? $"{ev[0].Nombre} {TiempoV2.HoraEt(ev[0].T0Utc)}" : $"{ev.Count}", "MAX Dia anterior 09:41:50 ET");
		c.Check("T8 los máximos de sesión (21405) también se evaluaron, cada uno con su T0", motor.Leer(m => m.Evaluaciones.Count(e => e.Nivel.Grupo != "dia" && e.T0Utc == t0.AddSeconds(-5))).ToString(), "3");
		c.Check("T8 aviso 'rompe' en el tick", motor.TomarNovedadesV2().Select(n => n.Tipo).FirstOrDefault() ?? "", "ROMPE NIVEL");
	}
}
