namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using Utils.Common.Logging;

// Archivo aparte a propósito (como en los trackers): es el único que importa ATAS.DataFeedsCore (MarketByOrder).
public partial class SessionSweepsPro
{
	/// <summary>MBO (nivel 3) para los icebergs de los trackers que corren dentro.</summary>
	protected override void OnMarketByOrdersChanged(IEnumerable<MarketByOrder> orders)
	{
		try
		{
			foreach (var o in orders)
				_trk.Mbo(o.Type.ToString(), o.Side.ToString(), o.Price, o.Volume, Convert.ToInt64(o.ExchangeOrderId), Convert.ToInt64(o.Priority));
		}
		catch (Exception ex)
		{
			this.LogError("Indicador Global Session Sweep: error en OnMarketByOrdersChanged", ex);
		}
	}
}
