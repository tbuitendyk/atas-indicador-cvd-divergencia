using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>
/// El motor real con trades tick a tick: historial (PDH 21420 del 29-oct) y minutos del 30-oct en los que cada trade pasa
/// por Trade() y al cierre del minuto la vela por Procesar() (como hace el indicador).
/// </summary>
internal sealed class SimMotor
{
	public readonly FuentesPrueba Fuentes = new();
	public readonly MotorSweeps M;

	public SimMotor(bool sinBarridos = false, Action<ConfiguracionSweeps>? ajustar = null, List<RegistroCsvBarrido>? csv = null)
	{
		var cfg = new ConfiguracionSweeps { TamanoTick = Tick, MinutosGrafico = 1 };

		// Ventana de barridos fuera de la hora de la prueba: solo se prueba MOD3 (sin evaluaciones de los niveles).
		if (sinBarridos)
			(cfg.SweepDetectionWindow, cfg.VentanaInicio, cfg.VentanaFin) = (VentanaBarridos.Custom, new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));

		cfg.V2.VolumePerBar = 100;
		cfg.V2.Fvg5m = false;
		ajustar?.Invoke(cfg);
		M = new MotorSweeps(cfg) { Almacen = new AlmacenTicks(), Fuentes = Fuentes };

		if (csv is not null)
			M.RestaurarDesde.AddRange(csv);

		for (var t = new DateTime(2026, 10, 29, 9, 30, 0); t < new DateTime(2026, 10, 29, 10, 30, 0); t = t.AddMinutes(1))
			M.Procesar(PruebasV2Fvg.E(t, 21400, t.Minute == 0 ? 21420 : 21402, 21400, 21401));

		for (var t = new DateTime(2026, 10, 29, 18, 0, 0); t < new DateTime(2026, 10, 30, 9, 41, 0); t = t.AddMinutes(1))
			M.Procesar(PruebasV2Fvg.E(t, 21400, 21402, 21400, 21401));
	}

	/// <summary>Un minuto: trades (precio, compra, venta) repartidos en el minuto; al final, la vela.</summary>
	public void Minuto(DateTime et, IEnumerable<(decimal P, decimal Compra, decimal Venta)> trades)
	{
		var lista = trades.ToList();
		var inicio = Utc(et);
		decimal o = lista[0].P, h = decimal.MinValue, l = decimal.MaxValue, cierre = 0, vol = 0, delta = 0;

		for (var i = 0; i < lista.Count; i++)
		{
			var (p, compra, venta) = lista[i];
			var t = inicio.AddSeconds(i * 59.0 / lista.Count);

			if (compra > 0)
				M.Trade(t, p, compra, true, o, inicio);

			if (venta > 0)
				M.Trade(t, p, venta, false, o, inicio);

			(h, l, cierre, vol, delta) = (Math.Max(h, p), Math.Min(l, p), p, vol + compra + venta, delta + compra - venta);
		}

		M.Procesar(new VelaEntrada { InicioUtc = inicio, Open = o, High = h, Low = l, Close = cierre, Volume = vol, Delta = delta });
	}

	/// <summary>Un minuto con la hora de cada trade (segundos desde el inicio del minuto).</summary>
	public void MinutoT(DateTime et, IEnumerable<(decimal P, decimal Compra, decimal Venta, double Seg)> trades)
	{
		var lista = trades.OrderBy(x => x.Seg).ToList();
		var inicio = Utc(et);
		decimal o = lista[0].P, h = decimal.MinValue, l = decimal.MaxValue, cierre = 0, vol = 0, delta = 0;

		foreach (var (p, compra, venta, seg) in lista)
		{
			var t = inicio.AddSeconds(seg);

			if (compra > 0)
				M.Trade(t, p, compra, true, o, inicio);

			if (venta > 0)
				M.Trade(t, p, venta, false, o, inicio);

			(h, l, cierre, vol, delta) = (Math.Max(h, p), Math.Min(l, p), p, vol + compra + venta, delta + compra - venta);
		}

		M.Procesar(new VelaEntrada { InicioUtc = inicio, Open = o, High = h, Low = l, Close = cierre, Volume = vol, Delta = delta });
	}

	/// <summary>Un camino de precios entre dos segundos del minuto.</summary>
	public static IEnumerable<(decimal, decimal, decimal, double)> CaminoT(decimal desde, decimal hasta, decimal compra, decimal venta, double seg0, double seg1)
	{
		var lista = Camino(desde, hasta, compra, venta).ToList();

		for (var i = 0; i < lista.Count; i++)
			yield return (lista[i].Item1, lista[i].Item2, lista[i].Item3, seg0 + (seg1 - seg0) * i / Math.Max(1, lista.Count - 1));
	}

	public static IEnumerable<(decimal, decimal, decimal)> Camino(decimal desde, decimal hasta, decimal compra, decimal venta)
	{
		var paso = hasta >= desde ? Tick : -Tick;

		for (var p = desde; paso > 0 ? p <= hasta : p >= hasta; p += paso)
			yield return (p, compra, venta);
	}

	/// <summary>Ruptura del PDH (21420) con aceptación: 09:41 sube a 21416, 09:42 a 21428, 09:43 a 21436, 09:44 baja a 21431 y sube a 21440.</summary>
	public void Ruptura()
	{
		Minuto(new DateTime(2026, 10, 30, 9, 41, 0), Camino(21402, 21416, 8, 2));
		Minuto(new DateTime(2026, 10, 30, 9, 42, 0), Camino(21416, 21428, 8, 2));
		Minuto(new DateTime(2026, 10, 30, 9, 43, 0), Camino(21428, 21436, 8, 2));
		Minuto(new DateTime(2026, 10, 30, 9, 44, 0), Camino(21436, 21431, 3, 3).Concat(Camino(21431, 21440, 8, 2)));
	}

	public EvaluacionBarrido? Pdh => M.Leer(m => m.Evaluaciones.FirstOrDefault(e => e.Nivel.Grupo == "dia"));

	public List<SetupModelo> Setups => M.Leer(m => m.Setups.ToList());
}

internal static class PruebasV2Modelos
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S7 T23: MOD4 — ruptura del PDH sin iceberg, retroceso a FVG+LVN con ARRANQUE = entrada MOD4");
		var s = new SimMotor();
		s.M.LvnDesdeSuite(PruebasV2Lvn.Z(1, 1, 21429.5m, 21430m, manual: false));
		s.Ruptura();
		c.Check("T23 el PDH queda en RUPTURA", s.Pdh?.Estado.ToString() ?? "sin evaluación", "Ruptura");
		var mod4 = s.Setups.Where(x => x.Modelo == "MOD4").ToList();
		c.Check("T23 MOD4 armado: retest de L y la zona FVG+LVN de la pierna (el LVN de dentro, una sola zona)",
			string.Join(" ", mod4.Where(x => x.Pendiente).Select(x => x.Zona.Tipo).OrderBy(x => x)), "FVG+LVN NIVEL");

		// Retroceso a 21430 (61.8 % de 20 puntos = 12.36: llega a 10), freno y arranque.
		// 0-30 s retroceso equilibrado (60 % ventas), 30-44 s freno en 21430.25-21430.50, 45-47 s arranque de 28 ticks.
		s.MinutoT(new DateTime(2026, 10, 30, 9, 45, 0), SimMotor.CaminoT(21440, 21430, 4, 6, 0, 30)
			.Concat(Enumerable.Range(0, 8).Select(i => (i % 2 == 0 ? 21430.25m : 21430.50m, 25m, 25m, 31.0 + i * 1.5)))
			.Concat(SimMotor.CaminoT(21430, 21437, 4.5m, 0.5m, 45, 47)));
		var entrada = s.Setups.FirstOrDefault(x => x.Modelo == "MOD4" && x.Zona.Tipo == "FVG+LVN");
		c.Check("T23 entrada MOD4 en la zona FVG+LVN con gatillo de ARRANQUE",
			entrada is null ? "sin setup" : $"{entrada.TextoEstado} {entrada.Gatillo?.Tipo} {entrada.Etiqueta}", "ENTRADA Arranque MOD4-C RUPTURA-RETROCESO");
		c.Check("T23 riesgo calculado (NQ)", entrada?.Riesgo?.Contratos > 0 ? "ok" : $"{entrada?.Riesgo}", "ok");

		c.Seccion("S7 T24: la misma ruptura con un iceberg ROTO = manda MOD3, sin duplicar MOD4");
		var s24 = new SimMotor();
		s24.M.LvnDesdeSuite(PruebasV2Lvn.Z(1, 1, 21429.5m, 21430m, manual: false));
		var t0 = Utc(new DateTime(2026, 10, 30, 9, 42, 10));
		s24.Fuentes.Eventos.Add(new IcebergEvent { Id = 50, Fuente = FuenteSuite.Iceberg, Utc = t0, CambioUtc = t0.AddSeconds(5), Precio = 21420.25m, Compra = false, Estado = "ROTO", Mbo = true, Ejecutado = 250 });
		s24.Ruptura();
		var modelos24 = s24.Setups;
		c.Check("T24 RUPTURA con iceberg ROTO: MOD3 activado, ningún MOD4",
			$"{s24.Pdh?.IcebergEstado} {modelos24.Count(x => x.Modelo == "MOD3")} {modelos24.Count(x => x.Modelo == "MOD4")}", "ROTO 1 0");
		c.Check("T24 las zonas de la pierna se operan como 'MOD2 tras MOD3'", modelos24.Any(x => x.Etiqueta == "MOD2 tras MOD3" && x.Zona.Tipo == "FVG+LVN").ToString(), "True");

		c.Seccion("S7 T25: MOD4 cancelado por RUPTURA FALLIDA");
		var s25 = new SimMotor();
		s25.Minuto(new DateTime(2026, 10, 30, 9, 41, 0), SimMotor.Camino(21402, 21416, 8, 2));
		s25.Minuto(new DateTime(2026, 10, 30, 9, 42, 0), SimMotor.Camino(21416, 21428, 8, 2));
		c.Check("T25 primero RUPTURA y MOD4 armado", $"{s25.Pdh?.Estado} {s25.Setups.Count(x => x.Modelo == "MOD4" && x.Pendiente)}", "Ruptura 1");
		s25.Minuto(new DateTime(2026, 10, 30, 9, 43, 0), new[] { (21427m, 0m, 50m), (21417m, 0m, 100m), (21416.5m, 0m, 50m) });
		var fallida = s25.Setups.Where(x => x.Modelo == "MOD4").ToList();
		c.Check("T25 vuelve 3 puntos bajo L: RUPTURA FALLIDA invalida el MOD4",
			$"{s25.M.Leer(m => m.Evaluaciones.Any(e => e.Estado == EstadoBarrido.RupturaFallida))} {string.Join(",", fallida.Select(x => $"{x.TextoEstado}:{x.Motivo}"))}",
			"True INVALIDADO:RUPTURA FALLIDA");

		c.Seccion("S7 T26: MOD3 activo invalida MOD1 y MOD2 contrarios y deja MOD2 a favor");
		var s26 = new SimMotor(sinBarridos: true);
		var lejos = new ZonaOperable { Bajo = 21300, Alto = 21301 };
		var futuro = Utc(new DateTime(2026, 10, 30, 11, 0, 0));
		s26.M.Leer(m =>
		{
			m.Setups.Add(new SetupModelo { Id = 901, Modelo = "MOD1", Etiqueta = "MOD1 (Modelo A)", Signo = -1, Zona = new ZonaOperable { Bajo = 21300, Alto = 21301, Signo = -1 }, ExpiraUtc = futuro });
			m.Setups.Add(new SetupModelo { Id = 902, Modelo = "MOD2", Etiqueta = "MOD2 (Modelo B)", Signo = -1, Zona = new ZonaOperable { Bajo = 21300, Alto = 21301, Signo = -1 }, ExpiraUtc = futuro });
			m.Setups.Add(new SetupModelo { Id = 903, Modelo = "MOD2", Etiqueta = "MOD2 (Modelo B)", Signo = 1, Zona = new ZonaOperable { Bajo = 21300, Alto = 21301, Signo = 1 }, ExpiraUtc = futuro });
			return 0;
		});
		var tr = Utc(new DateTime(2026, 10, 30, 9, 41, 5));
		s26.Fuentes.Eventos.Add(new IcebergEvent { Id = 60, Fuente = FuenteSuite.Iceberg, Utc = tr, CambioUtc = tr, Precio = 21403m, Compra = false, Estado = "ROTO", Mbo = true, Ejecutado = 300 });
		s26.Fuentes.Eventos.Add(new StackedEvent { Id = 61, Fuente = FuenteSuite.Stacked, Utc = tr, CambioUtc = tr, Bajo = 21403, Alto = 21404, Compra = true, Niveles = 4 });
		s26.Fuentes.Eventos.Add(new StopsEvent { Id = 62, Fuente = FuenteSuite.Stops, Utc = tr, CambioUtc = tr, Desde = 21403, Extremo = 21405, Compra = true, Contratos = 200 });
		s26.Minuto(new DateTime(2026, 10, 30, 9, 41, 0), SimMotor.Camino(21402, 21412, 8, 2));
		var st = s26.Setups.ToDictionary(x => x.Id);
		c.Check("T26 MOD3 activado (largo)", $"{s26.M.Leer(m => m.Mod3Activo?.Signo)} {s26.Setups.Count(x => x.Modelo == "MOD3")}", "1 1");
		c.Check("T26 MOD1 y MOD2 cortos: INVALIDADO POR MOD3; MOD2 largo sigue ARMADO",
			$"{st[901].TextoEstado}:{st[901].Motivo} {st[902].TextoEstado}:{st[902].Motivo} {st[903].TextoEstado}",
			"INVALIDADO:INVALIDADO POR MOD3 INVALIDADO:INVALIDADO POR MOD3 ARMADO");

		c.Seccion("S7 T27 y T20: MOD1 no acepta continuidad; un LVN MANUAL del botón LVN arma MOD2");
		var s27 = new SimMotor();
		var nivel = new NivelLiquidez { Id = 77, Precio = 21420, Lado = Lado.Buyside, Grupo = "dia", Origen = "Dia anterior" };
		var trampa = new EvaluacionBarrido { Id = 70, Nivel = nivel, Signo = 1, L = 21420, Extremo = 21422, Estado = EstadoBarrido.Real, Congelada = true, BarraT0 = 0 };
		s27.M.Leer(m => { m.ModelosAlEstado(trampa, "CONFIRMADO", Utc(new DateTime(2026, 10, 30, 9, 41, 0))); return 0; });
		var mod1 = s27.Setups.Single(x => x.Modelo == "MOD1");
		c.Check("T27 la TRAMPA arma MOD1 corto que exige ARRANQUE (sin continuidad)", $"{mod1.Signo} {mod1.PermiteContinuidad} {mod1.TextoEstado}", "-1 False ARMADO");

		var s20 = new SimMotor();
		s20.M.Leer(m => { m.SesgoForzado = 1; return 0; });
		s20.M.LvnDesdeSuite(PruebasV2Lvn.Z(5, 1, 21405m, 21405m, manual: true));
		s20.Minuto(new DateTime(2026, 10, 30, 9, 41, 0), SimMotor.Camino(21402, 21410, 8, 2));
		s20.Minuto(new DateTime(2026, 10, 30, 9, 42, 0), SimMotor.Camino(21410, 21402.5m, 2, 8));
		var mod2 = s20.Setups.FirstOrDefault(x => x.Modelo == "MOD2");
		c.Check("T20 retroceso al LVN MANUAL a favor del sesgo: MOD2 armado sobre esa zona",
			mod2 is null ? "sin MOD2" : $"{mod2.Zona.Tipo} {mod2.Zona.LvnOrigen} {mod2.Signo}", "LVN MANUAL 1");

		c.Seccion("S7 T21: rango nuevo con ClearLinesOnNewRange — pendientes cancelados, ejecutados intactos");
		var s21 = new SimMotor();
		s21.M.LvnDesdeSuite(PruebasV2Lvn.Z(8, 1, 21390m, 21391m, manual: true));
		s21.M.Leer(m =>
		{
			m.Setups.Add(new SetupModelo { Id = 801, Modelo = "MOD2", Signo = 1, DependeSoloDeLvn = true, Zona = new ZonaOperable { Tipo = "LVN", LvnId = 8, Bajo = 21390, Alto = 21391 }, ExpiraUtc = futuro });
			m.Setups.Add(new SetupModelo { Id = 802, Modelo = "MOD2", Signo = 1, DependeSoloDeLvn = true, Estado = EstadoModelo.Entrada, Zona = new ZonaOperable { Tipo = "LVN", LvnId = 8, Bajo = 21390, Alto = 21391 }, ExpiraUtc = futuro });
			return 0;
		});
		s21.M.LvnDesdeSuite(new RangeReset { Id = 3, Fuente = FuenteSuite.Thermal, Utc = DateTime.UtcNow, CambioUtc = DateTime.UtcNow, RangoAnteriorId = 1, RangoNuevoId = 2 });
		var s21s = s21.Setups.ToDictionary(x => x.Id);
		c.Check("T21 el pendiente que dependía del LVN se cancela; el ejecutado sigue",
			$"{s21s[801].TextoEstado}:{s21s[801].Motivo} {s21s[802].TextoEstado}", "INVALIDADO:rango nuevo: LVN borrado ENTRADA");

		c.Seccion("S7 T29: RiskCalculator y micro-impulso");
		var p = new ParametrosV2 { RiskMode = ModoRiesgo.Monto, RiskAmountUSD = 200 };
		var nq = CalculadoraRiesgo.Calcular(p, 21400, 21395, Tick, Instrumentos.Referencia[RaizInstrumento.NQ].ValorTick);
		var mnq = CalculadoraRiesgo.Calcular(p, 21400, 21395, Tick, Instrumentos.Referencia[RaizInstrumento.MNQ].ValorTick);
		c.Check("T29 200 USD y stop de 20 ticks: 2 NQ / 20 MNQ", $"{nq.StopTicks} {nq.Contratos} {mnq.Contratos}", "20 2 20");
		var poco = CalculadoraRiesgo.Calcular(new ParametrosV2 { RiskAmountUSD = 50 }, 21400, 21395, Tick, Instrumentos.Referencia[RaizInstrumento.NQ].ValorTick);
		c.Check("50 USD con stop de 20 ticks en NQ: RIESGO INSUFICIENTE: usar MNQ", poco.Texto(RaizInstrumento.NQ).StartsWith("RIESGO INSUFICIENTE: usar MNQ").ToString(), "True");
		var pct = CalculadoraRiesgo.Riesgo(new ParametrosV2 { RiskMode = ModoRiesgo.Porcentaje, RiskPct = 0.5m, AccountSize = 50000 });
		c.Check("modo porcentaje: 0,5 % de 50.000 = 250 USD", F(pct), "250");

		var micro = new MicroImpulso();
		var v1 = new Vela { InicioUtc = Utc(new DateTime(2026, 11, 2, 10, 0, 0)), FinUtc = Utc(new DateTime(2026, 11, 2, 10, 1, 0)), Open = 21410, High = 21411, Low = 21404, Close = 21404.5m, Volume = 100, Delta = -10 };
		c.Check("micro-impulso: cierre que cruza la EMA 15 contra un largo = ACTIVA", $"{micro.Vela(v1, 21406, 21409, 21405, +1, new ParametrosV2())} {micro.Texto(x => F(x))}",
			"True ACTIVA desde 09:01 CDMX en 21404.5 (cierre que cruza la EMA)");
	}
}
