namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// v4 Indicador Global Session Sweep: los trackers (Stacked / Imbalance, Iceberg, Stops, Sweeps) corren dentro de este
/// indicador con su motor de siempre (AnfitrionTrackers) y publican en el bus del instrumento; no hace falta agregarlos
/// al gráfico. Sus ajustes están en los grupos 40+ (Generado/SessionSweepsPro.Trackers.g.cs).
/// </summary>
public partial class SessionSweepsPro
{
	private const string G39 = "39. Indicador global";

	private readonly AnfitrionTrackers _trk = new();

	[Display(Name = "Trackers dentro del indicador", GroupName = G39, Order = 10,
		Description = "Corre Iceberg, Stops, Sweeps y Stacked / Imbalance por dentro (no hace falta agregarlos al gráfico). Apagado: usa los sueltos que haya.")]
	public bool HostTrackers { get => _trk.Activo; set { _trk.Activo = value; RecalculateValues(); } }

	private bool _hostThermal = true;

	[Display(Name = "Thermal Volume Profile + LVN dentro del indicador", GroupName = G39, Order = 15,
		Description = "El perfil térmico, sus LVN / EDGE, PIERNA, el GATILLO manual y su panel flotante corren dentro (ajustes en los grupos 'Thermal · …'). Apagado: usa un Thermal (Suite) suelto.")]
	public bool HostThermal { get => _hostThermal; set { _hostThermal = value; RecalculateValues(); } }

	[Display(Name = "Dibujar: Stacked / Imbalance", GroupName = G39, Order = 20)]
	public bool ShowTrackerStacked { get => _trk.DibujarStacked; set { _trk.DibujarStacked = value; RedrawChart(); } }

	[Display(Name = "Dibujar: Icebergs", GroupName = G39, Order = 30)]
	public bool ShowTrackerIcebergs { get => _trk.DibujarIcebergs; set { _trk.DibujarIcebergs = value; RedrawChart(); } }

	[Display(Name = "Dibujar: Stops", GroupName = G39, Order = 40)]
	public bool ShowTrackerStops { get => _trk.DibujarStops; set { _trk.DibujarStops = value; RedrawChart(); } }

	[Display(Name = "Dibujar: Sweeps", GroupName = G39, Order = 50)]
	public bool ShowTrackerSweeps { get => _trk.DibujarSweeps; set { _trk.DibujarSweeps = value; RedrawChart(); } }

	private void ConfigurarTrackers()
	{
		_trk.C.Redibujar = () => RedrawChart();
		_trk.C.Error = (mensaje, error) =>
		{
			if (error is null)
				this.LogWarn(mensaje);
			else
				this.LogError(mensaje, error);
		};
	}

	/// <summary>Cambio de un ajuste de los trackers: recalcula solo si hace falta (los eventos en vivo se conservan).</summary>
	private void CambioTrackers(bool recalcular)
	{
		if (recalcular)
			RecalculateValues();
		else
			_trk.C.Aplicar();
	}

	/// <summary>Suscripción al MBO para los icebergs (si el feed no lo ofrece, se usa el modo inferido).</summary>
	protected override async void OnInitialize()
	{
		try
		{
			if (_trk.Activo && _trk.C.Cfg.Iceberg.ModoMbo != ModoMbo.SoloInferido)
				await SubscribeMarketByOrderData();
		}
		catch (Exception ex)
		{
			this.LogError("Indicador Global Session Sweep: sin suscripción MBO (los icebergs usan el modo inferido)", ex);
		}
	}

	private void CalcularTrackers(int bar)
	{
		try
		{
			_trk.Calcular(bar, CurrentBar, VelaFlujoDe, i => GetCandle(i).Time, InstrumentInfo?.Instrument, InstrumentInfo?.TickSize ?? 0);
		}
		catch (Exception ex)
		{
			this.LogError("Indicador Global Session Sweep: error de cálculo de los trackers", ex);
		}
	}

	/// <summary>Vela de ATAS a VelaFlujo (volumen por precio con GetAllPriceLevels), como en los trackers sueltos.</summary>
	private VelaFlujo VelaFlujoDe(int bar)
	{
		var c = GetCandle(bar);
		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		var v = new VelaFlujo { InicioUtc = c.Time, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume, Delta = c.Delta };

		if (c.GetAllPriceLevels() is { } niveles)
		{
			foreach (var n in niveles)
			{
				var t = PrecioMath.Tick(n.Price, tick);
				var previo = v.Niveles.TryGetValue(t, out var p) ? p : (0m, 0m);
				v.Niveles[t] = (previo.Item1 + n.Bid, previo.Item2 + n.Ask);
			}
		}

		return v;
	}

	private void TradeTrackers(MarketDataArg trade)
	{
		try
		{
			_trk.Trade(trade.Price, trade.Volume, trade.Time, trade.Direction.ToString(), trade.AggressorExchangeOrderId, trade.ExchangeOrderId);
		}
		catch (Exception ex)
		{
			this.LogError("Indicador Global Session Sweep: error de los trackers en OnNewTrade", ex);
		}
	}

	/// <summary>DOM (nivel 2) para los icebergs inferidos.</summary>
	protected override void MarketDepthChanged(MarketDataArg depth)
	{
		try
		{
			_trk.Dom(depth.Price, depth.Volume, depth.DataType.ToString());
		}
		catch (Exception ex)
		{
			this.LogError("Indicador Global Session Sweep: error en MarketDepthChanged", ex);
		}
	}

	/// <summary>El dibujo original de los trackers (debajo de lo del barrido).</summary>
	private void DibujarTrackers(RenderContext context)
	{
		if (!_trk.Activo || ChartInfo is null)
			return;

		_trk.Dibujar(context, new Lienzo
		{
			X = bar => ChartInfo!.GetXByBar(bar),
			Y = precio => ChartInfo!.GetYByPrice(precio, false),
			Area = ChartInfo!.PriceChartContainer.Region,
			AnchoVela = Math.Max(1, (int)ChartInfo.PriceChartContainer.BarsWidth),
			Primera = FirstVisibleBarNumber,
			Ultima = LastVisibleBarNumber,
			UltimaBarra = CurrentBar - 1,
			Tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m,
			Modo = _trk.C.General.DisplayMode
		});
	}
}
