namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

/// <summary>
/// StopsTrackerPro (Suite Order Flow Stacker): zonas PROBABLES de stops (PDH/PDL, IB, overnight, swings, números
/// redondos) y cascadas de stops ya disparadas, con la zona tomada y TRAMPA / CONTINUACIÓN. Los stops no se ven antes
/// de dispararse. Parámetros y llamadas de ATAS: Generado/StopsTrackerPro.g.cs. Ver LEEME.md.
/// </summary>
[DisplayName("Stops Tracker Pro (Suite)")]
[Description("Suite Order Flow Stacker: zonas probables de stops y cascadas disparadas, con la zona tomada y TRAMPA o CONTINUACIÓN.")]
public partial class StopsTrackerPro : Indicator
{
	private void Dibujar(RenderContext context)
	{
		var l = Lienzo();

		if (l.Modo != ModoVisual.Minimal)
			DibujoSuite.Stops(context, _c, l);

		var lineas = _c.LineasRecientes();

		lock (_c.Suite.Cerrojo)
		{
			if (_c.Cfg.Stops.ShowProbableZones)
			{
				lineas.Insert(0, (_c.Suite.Stops.TextoZonas(5), CarcasaSuite.Oro));

				// Último aviso de cercanía a una zona (2 minutos).
				var aviso = _c.Suite.Eventos.Todos.LastOrDefault(e => e.Tipo == "StopZone" && _c.Ahora - e.Utc <= TimeSpan.FromMinutes(2));

				if (aviso is not null)
					lineas.Insert(1, (aviso.Texto, CarcasaSuite.Azul));
			}
		}

		_c.DibujarPanel(context, l.Area, "STOPS TRACKER", lineas);
	}
}
