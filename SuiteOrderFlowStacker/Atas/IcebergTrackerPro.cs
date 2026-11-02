namespace ATAS.Indicators.Custom;

using System.ComponentModel;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

/// <summary>
/// IcebergTrackerPro (Suite Order Flow Stacker, el principal): icebergs NATIVOS y SINTÉTICOS con MBO, o INFERIDOS con
/// el DOM y los trades si no hay MBO; estados ACTIVO / AGUANTANDO / ROTO / CANCELADO, sesgo GIRO / CONTINUIDAD /
/// RUPTURA / NEUTRO, umbrales por sesión (NQ y MNQ) y "*" de alta probabilidad. Parámetros y llamadas de ATAS:
/// Generado/IcebergTrackerPro.g.cs. Ver LEEME.md.
/// </summary>
[DisplayName("Iceberg Tracker Pro (Suite)")]
[Description("Suite Order Flow Stacker: icebergs nativos, sintéticos (MBO) o inferidos (DOM), con estado, sesgo y umbrales por sesión para NQ y MNQ.")]
public partial class IcebergTrackerPro : Indicator
{
	private void Dibujar(RenderContext context)
	{
		var l = Lienzo();

		if (l.Modo != ModoVisual.Minimal)
			DibujoSuite.Icebergs(context, _c, l);

		string titulo;

		lock (_c.Suite.Cerrojo)
			titulo = $"ICEBERG TRACKER · Modo: {_c.Suite.Icebergs.TextoModo} · Sesión: {_c.Cfg.Sesiones.De(_c.Ahora).Texto()}";

		_c.DibujarPanel(context, l.Area, titulo, _c.LineasRecientes());
	}
}
