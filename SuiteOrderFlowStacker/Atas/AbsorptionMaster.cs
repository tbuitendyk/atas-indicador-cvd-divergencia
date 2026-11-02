namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

/// <summary>
/// AbsorptionMaster (Suite Order Flow Stacker, la señal única): ejecuta dentro TODOS los motores (perfil, LVN,
/// imbalance, icebergs, stops, barridos) y solo muestra una flecha y una línea cuando hay confluencia (setups A-D),
/// con su puntuación, la gestión sugerida y el checklist. No depende de que otro indicador esté cargado.
/// Parámetros y llamadas de ATAS: Generado/AbsorptionMaster.g.cs. Ver LEEME.md y OPERATIVA.md.
/// </summary>
[DisplayName("Absorption Master (Suite)")]
[Description("Suite Order Flow Stacker: la señal única por confluencia (setups A-D) con puntuación, entrada, stop, objetivo y checklist.")]
public partial class AbsorptionMaster : Indicator
{
	private void Dibujar(RenderContext context)
	{
		var l = Lienzo();

		// Full: todo lo de los demás motores; Minimal y Markers: solo la señal (la flecha sale siempre).
		if (l.Completo)
		{
			DibujoSuite.Zonas(context, _c, l);
			DibujoSuite.Icebergs(context, _c, l);
			DibujoSuite.Stops(context, _c, l);
			DibujoSuite.Barridos(context, _c, l);
		}

		DibujoSuite.Senales(context, _c, l);
		_c.DibujarPanel(context, l.Area, Titulo(), Lineas());
	}

	private string Titulo()
	{
		lock (_c.Suite.Cerrojo)
			return $"ABSORPTION MASTER · Modo: {_c.Suite.Icebergs.TextoModo}";
	}

	/// <summary>Bloque de la última señal (si es reciente) con su checklist, el setup en espera y las señales anteriores.</summary>
	private List<(string Texto, Color Color)> Lineas()
	{
		var r = new List<(string Texto, Color Color)>();

		lock (_c.Suite.Cerrojo)
		{
			var m = _c.Suite.Master;
			var ahora = _c.Ahora;
			var vida = TimeSpan.FromMinutes(Math.Max(1, _c.General.EventLifetimeMin));
			var tick = _c.Suite.Tick;

			if (m.UltimaSenal is { } s && m.EventoUltimaSenal is { } e && ahora - e.Utc <= vida)
			{
				var riesgo = Math.Abs(s.Entrada - s.Stop);
				var beneficio = Math.Abs(s.Objetivo - s.Entrada);

				r.Add(($"SEÑAL {s.Sesgo} {s.Direccion} | {s.Score}/100 | Setup {s.Setup}", CarcasaSuite.Oro));
				r.Add(($"Entrada {Textos.Precio(s.Entrada, tick)} | SL {Textos.Precio(s.Stop, tick)} | TP {Textos.Precio(s.Objetivo, tick)} ({s.NombreObjetivo})", CarcasaSuite.Oro));
				r.Add(($"Riesgo {Pts(riesgo)} pts | Beneficio {Pts(beneficio)} pts | R:R {s.RewardRisk.ToString("0.0", CultureInfo.InvariantCulture)} | {Textos.Hora(e.Utc)}", CarcasaSuite.Oro));
				r.AddRange(Checklist(s));
			}

			if (m.Pendiente is { } p)
			{
				r.Add((m.TextoPendiente(p), CarcasaSuite.Azul));

				if (r.Count == 1)
					r.AddRange(Checklist(p));
			}

			// Señales anteriores (también las del CSV), sin repetir la última.
			var anteriores = _c.Suite.Eventos.Recientes("Master", ahora, _c.General.EventLifetimeMin, Math.Clamp(_c.General.InfoPanelMaxLines, 1, 20) + 1)
				.Where(x => x.Id != m.EventoUltimaSenal?.Id)
				.Take(Math.Clamp(_c.General.InfoPanelMaxLines, 1, 20) - 1);

			r.AddRange(anteriores.Select(x => ((x.Grabado ? "(CSV) " : "") + x.Texto, CarcasaSuite.ColorEvento(x))));
		}

		return r;
	}

	private static IEnumerable<(string Texto, Color Color)> Checklist(Candidata c) =>
		c.Checklist.Select(x => ($"  {x.Marca}  {x.Texto}{(x.Puntos > 0 ? $"  +{x.Puntos}" : "")}",
			x.Marca == "OK" ? CarcasaSuite.Verde : x.Marca == "NO" ? CarcasaSuite.Rojo : CarcasaSuite.Gris));

	private static string Pts(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}
