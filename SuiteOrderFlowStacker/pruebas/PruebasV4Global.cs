using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Comun;
using OrderFlowSuite.Nucleo;

using static Datos;

/// <summary>
/// Indicador de prueba que aloja los trackers como el Indicador Global Session Sweep (la carcasa real usa WPF y no se
/// compila en las pruebas): mismas llamadas a AnfitrionTrackers desde OnCalculate, OnNewTrade, el DOM y el dibujo.
/// </summary>
internal sealed class GlobalPrueba : Indicator
{
	public readonly AnfitrionTrackers Trk = new();

	public GlobalPrueba()
		: base(true) => Trk.C.General.EnableRecording = false;

	protected override void OnCalculate(int bar, decimal value) =>
		Trk.Calcular(bar, CurrentBar, i =>
		{
			var c = GetCandle(i);
			var v = new VelaFlujo { InicioUtc = c.Time, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume, Delta = c.Delta };

			foreach (var n in c.GetAllPriceLevels() ?? Enumerable.Empty<PriceVolumeInfo>())
				v.Niveles[PrecioMath.Tick(n.Price, Tick)] = (n.Bid, n.Ask);

			return v;
		}, i => GetCandle(i).Time, InstrumentInfo?.Instrument, InstrumentInfo?.TickSize ?? 0);

	protected override void OnNewTrade(MarketDataArg trade) =>
		Trk.Trade(trade.Price, trade.Volume, trade.Time, trade.Direction.ToString(), trade.AggressorExchangeOrderId, trade.ExchangeOrderId);

	protected override void MarketDepthChanged(MarketDataArg depth) => Trk.Dom(depth.Price, depth.Volume, depth.DataType.ToString());

	protected override void OnDispose() => Trk.Dispose();

	protected override void OnRender(RenderContext context, DrawingLayouts layout) =>
		Trk.Dibujar(context, new Lienzo
		{
			X = bar => bar * 10, Y = precio => (int)Math.Round((21430 - precio) * 20), Area = new System.Drawing.Rectangle(0, 0, 2000, 1000), AnchoVela = 8,
			Primera = 0, Ultima = CurrentBar - 1, UltimaBarra = CurrentBar - 1, Tick = Tick, Modo = Trk.C.General.DisplayMode
		});
}

/// <summary>v4 G8: los trackers corren dentro del Indicador Global y publican como los sueltos, sin alertas.</summary>
internal static class PruebasV4Global
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v4 G8: Indicador Global: los cuatro trackers por dentro publican en el bus del instrumento, sin alertas ni panel propio");
		SuiteEventBus.Reiniciar();
		CarcasaSuitePruebas.Historia(out var velas);
		var g = new GlobalPrueba
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick, Instrument = "NQZ6" },
			ChartInfo = new ChartInfo(),
			TestCandles = velas,
			LastVisibleBarNumber = velas.Count - 1
		};
		g.TestInitialize();
		g.TestCalcAll();
		var nq = SuiteEventBus.Para("NQZ6");
		var ahora = DateTime.UtcNow;
		c.Check("latidos de las cinco fuentes de los trackers (sin trades aún: SIN DATOS)",
			string.Join(" ", new[] { FuenteSuite.Stacked, FuenteSuite.Imbalance, FuenteSuite.Iceberg, FuenteSuite.Stops, FuenteSuite.Sweeps }.Select(f => nq.Estado(f, ahora))),
			"SinDatos SinDatos SinDatos SinDatos SinDatos");
		c.Check("el Absorption Master no se representa", nq.Estado(FuenteSuite.Absorption, ahora).ToString(), "NoCargado");
		c.Check("el stacked imbalance del historial llega al bus", (nq.Todos<StackedEvent>().Count > 0).ToString(), "True");

		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 12));
		g.TestDepth(new MarketDataArg { Price = 21396.25m, Volume = 20, DataType = MarketDataType.Bid });

		for (var i = 0; i < 16; i++)
		{
			g.TestTrade(new MarketDataArg { Time = t.AddSeconds(i * 0.3), Price = 21396.25m, Volume = 10, Direction = TradeDirection.Sell });
			g.TestDepth(new MarketDataArg { Price = 21396.25m, Volume = 20, DataType = MarketDataType.Bid });
		}

		var ib = nq.Todos<IcebergEvent>();
		c.Check("el iceberg inferido de dentro llega al bus como el del Iceberg Tracker suelto",
			ib.Count == 1 ? $"{ib[0].Fuente} {ib[0].Precio} {ib[0].Compra} {ib[0].Estado} {ib[0].Mbo} {ib[0].Ejecutado}" : $"{ib.Count}", "Iceberg 21396.25 True ACTIVO False 150");
		c.Check("con trades: las fuentes CARGADO", $"{nq.Estado(FuenteSuite.Iceberg, DateTime.UtcNow)} {nq.Estado(FuenteSuite.Stops, DateTime.UtcNow)}", "Cargado Cargado");
		c.Check("sin alertas de icebergs, stops ni imbalances (el propietario: tapaban la vista)", g.Alertas.Count.ToString(), "0");
		g.Trk.C.General.DisplayMode = ModoVisual.Full;
		var r = g.TestRender();
		c.Check("dibuja el dibujo original de los trackers (iceberg y zona stacked en modo Full)", $"{r.Texts.Any(x => x.Text.Contains("ICEBERG", StringComparison.Ordinal))} {r.Rects.Count > 0}", "True True");
		g.Trk.DibujarIcebergs = false;
		c.Check("cada capa se apaga por separado (sin icebergs)", g.TestRender().Texts.Any(x => x.Text.Contains("ICEBERG", StringComparison.Ordinal)).ToString(), "False");
		g.Trk.DibujarIcebergs = true;

		g.Trk.Activo = false;
		SuiteEventBus.Reiniciar();
		g.TestTrade(new MarketDataArg { Time = t.AddSeconds(30), Price = 21396.25m, Volume = 10, Direction = TradeDirection.Sell });
		c.Check("con 'Trackers dentro' apagado no corre ni publica", SuiteEventBus.Para("NQZ6").Todos<IcebergEvent>().Count.ToString(), "0");
		g.TestDispose();
		SuiteEventBus.Reiniciar();
	}
}
