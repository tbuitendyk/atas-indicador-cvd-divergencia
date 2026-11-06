namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ATAS.Indicators;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// Historial de trades para las barras de volumen (2.1): al terminar de cargar, se piden a ATAS los trades acumulados desde
/// el inicio del día de futuros (18:00 ET) — RequestForCumulativeTrades, como TapePattern y DomStrength oficiales —, se
/// guardan delante de los trades en vivo y se recalcula UNA vez: las velas del día dejan de ser APROX. Sin respuesta, el
/// motor sigue con la reconstrucción APROX desde el footprint.
/// Este archivo no importa ATAS.DataFeedsCore: TradeDirection solo se compara con ToString.
/// </summary>
public partial class SessionSweepsPro
{
	private string _historialPedido = "";
	private bool _historialEsperando;

	private void ReiniciarHistorialV2() => (_historialPedido, _historialEsperando) = ("", false);

	private void PedirHistorialV2()
	{
		try
		{
			if (CurrentBar <= 0 || _historialEsperando || string.IsNullOrEmpty(_instrumentoV2))
				return;

			// Una vez por instrumento y día de futuros.
			var dia = Zonas.DiaOperativo(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			var clave = _instrumentoV2 + "|" + dia;

			if (_historialPedido == clave)
				return;

			var desde = Zonas.AUtc(Zonas.DiaOperativo(DateTime.UtcNow).AddDays(-1).Add(new TimeSpan(18, 0, 0)), ZonaHoraria.NuevaYork);
			var primera = GetCandle(0).Time;

			if (primera > desde)
				desde = primera;

			_historialPedido = clave;
			_historialEsperando = true;
			RequestForCumulativeTrades(new CumulativeTradesRequest(desde));
		}
		catch (Exception ex)
		{
			_historialEsperando = false;
			this.LogError("SessionSweepsPro: no se pudo pedir el historial de trades", ex);
		}
	}

	protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
	{
		try
		{
			_historialEsperando = false;
			var ticks = new List<TickV2>();

			foreach (var t in cumulativeTrades)
			{
				if (t.Ticks is { Count: > 0 } impresiones)
				{
					foreach (var i in impresiones)
						ticks.Add(new TickV2(i.Time, i.Price, i.Volume, Compra(i.Direction.ToString())));
				}
				else
				{
					ticks.Add(new TickV2(t.Time, t.Lastprice, t.Volume, Compra(t.Direction.ToString())));
				}
			}

			if (_almacen.AgregarHistorial(ticks) > 0)
				RecalculateValues();
		}
		catch (Exception ex)
		{
			this.LogError("SessionSweepsPro: error con el historial de trades", ex);
		}
	}

	private static bool? Compra(string direccion) => direccion == "Buy" ? true : direccion == "Sell" ? false : null;

	/// <summary>Saldo del portafolio seleccionado en ATAS (AccountInfoDisplay oficial: TradingManager?.Portfolio, Balance).</summary>
	private decimal? SaldoAtas()
	{
		try
		{
			var portafolio = TradingManager?.Portfolio;

			if (portafolio is null)
				return null;

			object? saldo = portafolio.Balance;
			return saldo is null ? null : Convert.ToDecimal(saldo, CultureInfo.InvariantCulture);
		}
		catch (Exception ex)
		{
			this.LogWarn("SessionSweepsPro: no se pudo leer el saldo de ATAS: " + ex.Message);
			return null;
		}
	}
}
