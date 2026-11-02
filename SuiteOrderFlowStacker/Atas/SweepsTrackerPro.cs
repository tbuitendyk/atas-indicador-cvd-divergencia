namespace ATAS.Indicators.Custom;

using System.ComponentModel;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

/// <summary>
/// SweepsTrackerPro (Suite Order Flow Stacker): la orden agresiva que barre varios niveles, reconstruida por
/// AggressorExchangeOrderId (MBO) o por milisegundo (inferido), y clasificada LIQUIDEZ / RUPTURA / CONTRA ICEBERG.
/// Parámetros y llamadas de ATAS: Generado/SweepsTrackerPro.g.cs. Ver LEEME.md.
/// </summary>
[DisplayName("Sweeps Tracker Pro (Suite)")]
[Description("Suite Order Flow Stacker: barridos de liquidez, de ruptura y contra iceberg, reconstruidos por orden agresiva.")]
public partial class SweepsTrackerPro : Indicator
{
	private void Dibujar(RenderContext context)
	{
		var l = Lienzo();

		if (l.Modo != ModoVisual.Minimal)
			DibujoSuite.Barridos(context, _c, l);

		string titulo;

		lock (_c.Suite.Cerrojo)
			titulo = $"SWEEPS TRACKER · {(_c.Suite.Barridos.ConIdAgresor ? "por orden agresiva (MBO)" : "inferido (mismo ms)")}";

		_c.DibujarPanel(context, l.Area, titulo, _c.LineasRecientes());
	}
}
