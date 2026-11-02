namespace ATAS.Indicators.Custom;

using System.ComponentModel;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

/// <summary>
/// StackedImbalancePro (Suite Order Flow Stacker): imbalance diagonal en varios niveles seguidos, con el estado de la
/// zona (NUEVA / PROBADA / ROTA), alerta al volver a una zona NUEVA y la etiqueta "con iceberg". Independiente del
/// Stacked Imbalance de ATAS. Parámetros y llamadas de ATAS: Generado/StackedImbalancePro.g.cs. Ver LEEME.md.
/// </summary>
[DisplayName("Stacked Imbalance Pro (Suite)")]
[Description("Suite Order Flow Stacker: stacked imbalance diagonal con estado de la zona (NUEVA, PROBADA, ROTA), alerta al volver y etiqueta 'con iceberg'.")]
public partial class StackedImbalancePro : Indicator
{
	private void Dibujar(RenderContext context)
	{
		var l = Lienzo();

		if (l.Modo != ModoVisual.Minimal)
			DibujoSuite.Zonas(context, _c, l);

		_c.DibujarPanel(context, l.Area, "STACKED IMBALANCE", _c.LineasRecientes());
	}
}
