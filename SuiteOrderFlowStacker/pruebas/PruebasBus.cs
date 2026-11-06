using System;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

using OrderFlowSuite.Comun;

using static Datos;

/// <summary>S1: bus de eventos de la suite (publicar, actualizar por Id, consultar, latidos) y publicación de los indicadores.</summary>
internal static class PruebasBus
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S1 SuiteEventBus: publicar, actualizar por Id, consultar por tiempo y precio, latidos");
		SuiteEventBus.Reiniciar();
		var canal = SuiteEventBus.Para("NQZ6");
		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 0));
		var recibidos = 0;
		Action<EventoSuite> contar = _ => recibidos++;
		canal.Suscribir(contar);

		canal.Publicar(new IcebergEvent { Id = 1, Fuente = FuenteSuite.Iceberg, Utc = t, CambioUtc = t, Version = 1, Precio = 21396.25m, Compra = true, Estado = "ACTIVO" });
		canal.Publicar(new IcebergEvent { Id = 1, Fuente = FuenteSuite.Iceberg, Utc = t, CambioUtc = t.AddSeconds(20), Version = 2, Precio = 21396.25m, Compra = true, Estado = "AGUANTANDO" });
		canal.Publicar(new IcebergEvent { Id = 2, Fuente = FuenteSuite.Iceberg, Utc = t.AddMinutes(10), CambioUtc = t.AddMinutes(10), Version = 1, Precio = 21450m, Compra = false, Estado = "ACTIVO" });

		c.Check("actualizar por Id (2 eventos, el 1 AGUANTANDO)", $"{canal.Todos<IcebergEvent>().Count} {canal.Todos<IcebergEvent>().First(e => e.Id == 1).Estado}", "2 AGUANTANDO");
		c.Check("suscriptores avisados en cada publicación", recibidos.ToString(), "3");
		c.Check("consulta por tiempo", canal.Consultar<IcebergEvent>(t.AddSeconds(-30), t.AddMinutes(1)).Select(e => e.Id).Single().ToString(), "1");
		c.Check("consulta por precio", canal.Consultar<IcebergEvent>(t, t.AddHours(1), e => e.Precio > 21400).Single().Id.ToString(), "2");
		c.Check("otro instrumento: canal aparte", SuiteEventBus.Para("MNQZ6").Todos<IcebergEvent>().Count.ToString(), "0");
		canal.Desuscribir(contar);

		c.Check("sin latido: NO CARGADO", canal.Estado(FuenteSuite.Stops, t).ToString(), "NoCargado");
		canal.Latido(FuenteSuite.Stops, false, t);
		c.Check("latido sin datos: SIN DATOS", canal.Estado(FuenteSuite.Stops, t.AddSeconds(1)).ToString(), "SinDatos");
		canal.Latido(FuenteSuite.Stops, true, t.AddSeconds(2));
		c.Check("latido con datos: CARGADO", canal.Estado(FuenteSuite.Stops, t.AddSeconds(3)).ToString(), "Cargado");
		c.Check("sin latido 7 s: NO CARGADO", canal.Estado(FuenteSuite.Stops, t.AddSeconds(10)).ToString(), "NoCargado");
		c.Check("versión del latido", canal.VersionDe(FuenteSuite.Stops) ?? "", SuiteEventBus.Version);

		c.Seccion("S1 Los indicadores de la suite publican SUS eventos en el bus (su lógica no cambia)");
		SuiteEventBus.Reiniciar();
		CarcasaSuitePruebas.Historia(out var velas);
		var iceberg = Crear<IcebergTrackerPro>(velas);
		var imbalance = Crear<StackedImbalancePro>(velas);
		var nq = SuiteEventBus.Para("NQZ6");
		c.Check("latidos al terminar la carga: SIN DATOS (aún sin trades)", $"{nq.Estado(FuenteSuite.Iceberg, DateTime.UtcNow)} {nq.Estado(FuenteSuite.Stacked, DateTime.UtcNow)} {nq.Estado(FuenteSuite.Imbalance, DateTime.UtcNow)}", "SinDatos SinDatos SinDatos");
		c.Check("Stops Tracker no cargado", nq.Estado(FuenteSuite.Stops, DateTime.UtcNow).ToString(), "NoCargado");

		var t2 = Utc(new DateTime(2026, 10, 30, 9, 42, 12));
		iceberg.TestDepth(new MarketDataArg { Price = 21396.25m, Volume = 20, DataType = MarketDataType.Bid });

		for (var i = 0; i < 16; i++)
		{
			iceberg.TestTrade(new MarketDataArg { Time = t2.AddSeconds(i * 0.3), Price = 21396.25m, Volume = 10, Direction = TradeDirection.Sell });
			iceberg.TestDepth(new MarketDataArg { Price = 21396.25m, Volume = 20, DataType = MarketDataType.Bid });
		}

		var ib = nq.Todos<IcebergEvent>();
		c.Check("IcebergEvent en el bus (INFERIDO = Mbo false)", ib.Count == 1 ? $"{ib[0].Precio} {ib[0].Compra} {ib[0].Estado} {ib[0].Mbo} {ib[0].Ejecutado}" : $"{ib.Count}", "21396.25 True ACTIVO False 150");
		c.Check("con trades: Iceberg CARGADO", nq.Estado(FuenteSuite.Iceberg, DateTime.UtcNow).ToString(), "Cargado");
		c.Check("el Stacked Imbalance no publica icebergs", nq.Todos<IcebergEvent>().All(e => e.Fuente == FuenteSuite.Iceberg).ToString(), "True");

		iceberg.TestDispose();
		imbalance.TestDispose();
		SuiteEventBus.Reiniciar();
	}

	private static T Crear<T>(System.Collections.Generic.List<IndicatorCandle> velas) where T : Indicator, new()
	{
		var ind = new T
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick, Instrument = "NQZ6" },
			ChartInfo = new ChartInfo(),
			TestCandles = velas,
			LastVisibleBarNumber = velas.Count - 1
		};

		var p = typeof(T).GetProperty("EnableRecording");
		p?.SetValue(ind, false);
		ind.TestInitialize();
		ind.TestCalcAll();
		return ind;
	}
}

/// <summary>Historial con un stacked imbalance de compra en la última vela cerrada.</summary>
internal static class CarcasaSuitePruebas
{
	public static void Historia(out System.Collections.Generic.List<IndicatorCandle> velas)
	{
		velas = new System.Collections.Generic.List<IndicatorCandle>();

		foreach (var v in Planas(new DateTime(2026, 10, 30, 9, 30, 0), new DateTime(2026, 10, 30, 9, 41, 0), 21410))
			velas.Add(Vela(v));

		velas.Add(Vela(V(new DateTime(2026, 10, 30, 9, 41, 0), 21400, 21401, 21400, 21400.75m, 10,
			(21400.00m, 10, 5), (21400.25m, 10, 60), (21400.50m, 10, 60), (21400.75m, 5, 60), (21401.00m, 5, 5))));
		velas.Add(Vela(V(new DateTime(2026, 10, 30, 9, 42, 0), 21401, 21402, 21401, 21401.5m)));
	}

	private static IndicatorCandle Vela(OrderFlowSuite.Nucleo.VelaFlujo v)
	{
		var c = new IndicatorCandle { Time = v.InicioUtc, Open = v.Open, High = v.High, Low = v.Low, Close = v.Close, Volume = v.Volume, Delta = v.Delta };

		foreach (var (t, n) in v.Niveles)
			c.Levels[t * Tick] = (n.Bid, n.Ask);

		return c;
	}
}
