namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// Dibujo de v2.0 (11.5): cajas de barrido por estado (TRAMPA verde/rojo, PREMIUM borde dorado, RUPTURA azul, FALLIDO
/// morado, PROVISIONAL naranja, SIN LIQ gris), zonas de los modelos (MOD3 fucsia, MOD4 azul claro), FVG/BFVG, LVN del
/// Thermal, cajas del gatillo manual, divergencias del CVD sobre el precio y, si se pide, el panel dentro del gráfico.
/// </summary>
public partial class SessionSweepsPro
{
	private readonly SeguidorCvd _cvdPrecio = new();
	private readonly List<(ManualTriggerRequest Solicitud, ManualTriggerResult Resultado)> _cajasManuales = new();

	private sealed class FotoV2
	{
		public List<(EstadoBarrido Estado, int SignoGiro, decimal L, decimal Extremo, DateTime T0, DateTime? Fin, string Nombre, decimal Total, bool Provisional)> Barridos = new();
		public List<(string Modelo, string Etiqueta, int Signo, decimal Bajo, decimal Alto, DateTime Desde, DateTime? Hasta, bool Pendiente, bool Abierta, decimal? Entrada, decimal? Stop, decimal? Tp1, decimal? Tp2)> Modelos = new();
		public List<(bool Alcista, int Marco, decimal Bajo, decimal Alto, DateTime Utc)> Fvgs = new();
		public List<(decimal Bajo, decimal Alto, bool Manual, bool Activo)> Lvns = new();
	}

	/// <summary>Vela cerrada para el CVD del precio (se llama en orden desde la vela 0).</summary>
	private void CvdPrecio(int bar)
	{
		if (bar == 0)
			_cvdPrecio.Reiniciar();

		if (bar != _cvdPrecio.Velas.Count)
			return;

		var c = GetCandle(bar);
		_cvdPrecio.Cerrar(c.Time, c.High, c.Low, c.Delta, c.MaxDelta, c.MinDelta);
		_cvdPrecio.Nuevas(_cfg.V2.CvdMacroPivotBars, _cfg.V2.CvdMicroPivotBars, _cfg.V2.CvdShowMicro, _cfg.V2.CvdMaxLookbackBars);
	}

	/// <summary>Vela del gráfico que contiene 'utc' (búsqueda binaria en las horas de las velas).</summary>
	private int BarDe(DateTime utc)
	{
		int bajo = 0, alto = Math.Max(0, CurrentBar - 1);

		if (CurrentBar <= 0)
			return 0;

		while (bajo < alto)
		{
			var medio = (bajo + alto + 1) / 2;

			if (GetCandle(medio).Time <= utc)
				bajo = medio;
			else
				alto = medio - 1;
		}

		return bajo;
	}

	private static Color ColorDe(ColorPanel c, int alfa = 255) => c switch
	{
		ColorPanel.Verde => Color.FromArgb(alfa, 0, 200, 83),
		ColorPanel.Rojo => Color.FromArgb(alfa, 255, 82, 82),
		ColorPanel.Naranja => Color.FromArgb(alfa, 255, 152, 0),
		ColorPanel.Amarillo => Color.FromArgb(alfa, 255, 235, 59),
		ColorPanel.Gris => Color.FromArgb(alfa, 138, 143, 152),
		ColorPanel.Dorado => Color.FromArgb(alfa, 255, 193, 7),
		ColorPanel.Fucsia => Color.FromArgb(alfa, 255, 0, 200),
		ColorPanel.AzulClaro => Color.FromArgb(alfa, 100, 200, 255),
		ColorPanel.Azul => Color.FromArgb(alfa, 41, 121, 255),
		ColorPanel.Morado => Color.FromArgb(alfa, 171, 71, 188),
		ColorPanel.Titulo => Color.FromArgb(alfa, 159, 179, 200),
		_ => Color.FromArgb(alfa, 235, 235, 235)
	};

	private static ColorPanel ColorEstado(EstadoBarrido e, int signoGiro, bool provisional) => e switch
	{
		_ when provisional && e is EstadoBarrido.Pendiente or EstadoBarrido.Indeterminado => ColorPanel.Naranja,
		EstadoBarrido.SinLiquidez => ColorPanel.Gris,
		EstadoBarrido.TrampaB or EstadoBarrido.TrampaA or EstadoBarrido.TrampaPremium => signoGiro > 0 ? ColorPanel.Verde : ColorPanel.Rojo,
		EstadoBarrido.Ruptura => ColorPanel.Azul,
		EstadoBarrido.Fallido or EstadoBarrido.RupturaFallida => ColorPanel.Morado,
		_ => provisional ? ColorPanel.Naranja : ColorPanel.Gris
	};

	private static ColorPanel ColorModelo(string modelo, int signo) => modelo switch
	{
		"MOD3" => ColorPanel.Fucsia,
		"MOD4" => ColorPanel.AzulClaro,
		_ => signo > 0 ? ColorPanel.Verde : ColorPanel.Rojo
	};

	private void DibujarV2(RenderContext context, MotorSweeps motor, Rectangle area, Func<int, int, bool> visible)
	{
		var foto = motor.Leer(m => new FotoV2
		{
			Barridos = m.Evaluaciones.Select(e => (e.Estado, e.SignoGiro, e.L, e.Signo > 0 ? Math.Max(e.Extremo, e.ExtremoPierna) : Math.Min(e.Extremo, e.ExtremoPierna),
				e.T0Utc, e.TimeConfirmUtc, e.Nombre, e.Total, e.Provisional)).ToList(),
			Modelos = m.Setups.Select(s => (s.Modelo, s.Etiqueta, s.Signo, s.Zona.Bajo, s.Zona.Alto, s.ArmadoUtc, s.CierreUtc ?? (s.Pendiente ? (DateTime?)null : s.ExpiraUtc),
				s.Pendiente, s.Abierta, s.Entrada, s.Stop, s.Tp1, s.Tp2)).ToList(),
			Fvgs = m.Fvg2.Fvgs.Where(f => f.Vivo).Select(f => (f.Alcista, f.Marco, f.Bajo, f.Alto, f.Utc)).ToList(),
			Lvns = m.Lvn.Todas.Select(z => (z.Bajo, z.Alto, z.Manual, z.Activo)).ToList()
		});

		var ultima = Math.Max(0, CurrentBar - 1);
		var derecha = X(ultima) + AnchoVela;

		if (_v.ShowLvnLines)
		{
			foreach (var z in foto.Lvns)
			{
				var color = z.Activo ? Color.FromArgb(200, 255, 152, 0) : Color.FromArgb(110, 160, 160, 160);
				var (y1, y2) = (Y(z.Alto), Y(z.Bajo));

				if (y2 - y1 > 2)
					context.FillRectangle(Color.FromArgb(z.Activo ? 40 : 20, color), new Rectangle(area.Left, y1, area.Width, y2 - y1));

				var pluma = new RenderPen(color, 1);
				var y = (y1 + y2) / 2;

				if (z.Activo)
					context.DrawLine(pluma, area.Left, y, area.Right, y);
				else
					Punteada(context, pluma, area.Left, area.Right, y);

				_etiquetas.Add((y, $"LVN {(z.Manual ? "MANUAL" : "AUTO")}{(z.Activo ? "" : " mitigado")}", color));
			}
		}

		if (_v.ShowFvgV2)
		{
			foreach (var f in foto.Fvgs)
			{
				var desde = BarDe(f.Utc);

				if (!visible(desde, ultima))
					continue;

				var color = f.Alcista ? Color.FromArgb(f.Marco == 5 ? 60 : 40, 0, 150, 136) : Color.FromArgb(f.Marco == 5 ? 60 : 40, 244, 67, 54);
				var caja = new Rectangle(X(desde), Y(f.Alto), Math.Max(1, derecha - X(desde)), Math.Max(1, Y(f.Bajo) - Y(f.Alto)));
				context.FillRectangle(color, caja);

				if (f.Marco == 5)
					context.DrawString("FVG 5m", _fuente!, Color.FromArgb(220, color), new Rectangle(caja.X + 2, caja.Y, 60, 13), _izquierda);
			}
		}

		if (_v.ShowSweepBoxes)
		{
			foreach (var b in foto.Barridos)
			{
				var (desde, hasta) = (BarDe(b.T0), b.Fin is { } fin ? BarDe(fin) : ultima);

				if (!visible(desde, hasta))
					continue;

				var colorPanel = ColorEstado(b.Estado, b.SignoGiro, b.Provisional);
				var color = ColorDe(colorPanel);
				var (arriba, abajo) = (Math.Max(b.L, b.Extremo), Math.Min(b.L, b.Extremo));
				var caja = new Rectangle(X(desde), Y(arriba), Math.Max(2, X(hasta) + AnchoVela - X(desde)), Math.Max(2, Y(abajo) - Y(arriba)));
				var alfa = b.Estado switch { EstadoBarrido.TrampaPremium => 110, EstadoBarrido.TrampaA => 85, EstadoBarrido.TrampaB => 60, _ => 45 };
				context.FillRectangle(Color.FromArgb(alfa, color), caja);
				context.DrawRectangle(new RenderPen(b.Estado == EstadoBarrido.TrampaPremium ? ColorDe(ColorPanel.Dorado) : color, b.Estado == EstadoBarrido.TrampaPremium ? 2 : 1), caja);
				var texto = $"{(b.Provisional ? "PROVISIONAL · " : "")}{b.Estado.Texto()} {b.Total:0} · {b.Nombre}";
				var yTexto = b.SignoGiro < 0 ? caja.Top - 14 : caja.Bottom + 1;
				context.DrawString(texto, _fuente!, color, new Rectangle(caja.X, yTexto, 320, 13), _izquierda);
			}
		}

		if (_v.ShowModelBoxes)
		{
			foreach (var s in foto.Modelos)
			{
				var (desde, hasta) = (BarDe(s.Desde), s.Hasta is { } h ? BarDe(h) : ultima);

				if (!visible(desde, hasta) || (!s.Pendiente && !s.Abierta && s.Entrada is null))
					continue;

				var color = ColorDe(ColorModelo(s.Modelo, s.Signo));
				var x2 = X(hasta) + AnchoVela;
				var caja = new Rectangle(X(desde), Y(s.Alto), Math.Max(2, x2 - X(desde)), Math.Max(2, Y(s.Bajo) - Y(s.Alto)));
				context.FillRectangle(Color.FromArgb(s.Pendiente ? 35 : 55, color), caja);

				if (s.Pendiente)
				{
					var pluma = new RenderPen(color, 1);
					Punteada(context, pluma, caja.Left, caja.Right, caja.Top);
					Punteada(context, pluma, caja.Left, caja.Right, caja.Bottom);
				}
				else
				{
					context.DrawRectangle(new RenderPen(color, 1), caja);
				}

				context.DrawString(s.Etiqueta, _fuente!, color, new Rectangle(caja.X + 2, caja.Y - 14, 260, 13), _izquierda);

				void Nivel(decimal? precio, string nombre, Color c)
				{
					if (precio is not { } p)
						return;

					var y = Y(p);
					context.DrawLine(new RenderPen(c, 1), caja.Left, y, caja.Right, y);
					context.DrawString($"{nombre} {motor.Precio(p)}", _fuente!, c, new Rectangle(caja.Right + 3, y - 7, 140, 13), _izquierda);
				}

				Nivel(s.Entrada, "ENTRADA", Color.WhiteSmoke);
				Nivel(s.Stop, "STOP", ColorDe(ColorPanel.Rojo));
				Nivel(s.Tp1, "TP1", ColorDe(ColorPanel.Verde));
				Nivel(s.Tp2, "TP2", ColorDe(ColorPanel.Verde, 170));
			}
		}

		if (_v.ShowManualTriggerBoxes)
			DibujarManuales(context, visible);

		if (_v.ShowCvdDivergences)
			DibujarDivergencias(context, visible);

		if (_v.ShowInChartPanel)
			DibujarPanelEnGrafico(context, area, ContenidoPanelV2(false));
	}

	private void DibujarManuales(RenderContext context, Func<int, int, bool> visible)
	{
		List<(ManualTriggerRequest Solicitud, ManualTriggerResult Resultado)> cajas;

		lock (_syncV2)
			cajas = _cajasManuales.ToList();

		foreach (var (q, r) in cajas)
		{
			var (desde, hasta) = (BarDe(q.DesdeUtc), BarDe(q.HastaUtc));

			if (!visible(desde, hasta))
				continue;

			var color = ColorDe(r.Direccion > 0 ? ColorPanel.Verde : r.Direccion < 0 ? ColorPanel.Rojo : ColorPanel.Gris);
			var caja = new Rectangle(X(desde), Y(q.Alto), Math.Max(2, X(hasta) + AnchoVela - X(desde)), Math.Max(2, Y(q.Bajo) - Y(q.Alto)));
			context.DrawRectangle(new RenderPen(color, 2), caja);
			Flecha(context, caja.Right + 4, caja.Top + caja.Height / 2, r.Direccion, color);
			context.DrawString($"GATILLO {r.Resultado} {(r.Direccion == 0 ? "[-]" : "")} {r.Score:0}", _fuente!, color, new Rectangle(caja.Right + 16, caja.Top + caja.Height / 2 - 7, 260, 13), _izquierda);
		}
	}

	private void DibujarDivergencias(RenderContext context, Func<int, int, bool> visible)
	{
		foreach (var d in _cvdPrecio.Todas.ToList())
		{
			if (!visible(d.I1, d.I2))
				continue;

			var color = d.Alcista ? ColorDe(ColorPanel.Verde, d.Micro ? 170 : 255) : ColorDe(ColorPanel.Rojo, d.Micro ? 170 : 255);
			var (x1, x2) = (X(d.I1) + AnchoVela / 2, X(d.I2) + AnchoVela / 2);
			var (y1, y2) = (Y(d.Precio1), Y(d.Precio2));

			if (d.Micro)
			{
				// Punteada a lo largo del tramo (sin líneas verticales).
				var pasos = Math.Max(1, Math.Abs(x2 - x1) / 6);

				for (var k = 0; k < pasos; k += 2)
					context.DrawLine(new RenderPen(color, 1), x1 + (x2 - x1) * k / pasos, y1 + (y2 - y1) * k / pasos, x1 + (x2 - x1) * (k + 1) / pasos, y1 + (y2 - y1) * (k + 1) / pasos);
			}
			else
			{
				context.DrawLine(new RenderPen(color, 2), x1, y1, x2, y2);
			}
		}
	}

	private static void Flecha(RenderContext context, int x, int y, int direccion, Color color)
	{
		const int t = 6;

		if (direccion == 0)
			return;

		var puntos = direccion > 0
			? new[] { new Point(x, y + t), new Point(x + t, y - t), new Point(x + 2 * t, y + t) }
			: new[] { new Point(x, y - t), new Point(x + 2 * t, y - t), new Point(x + t, y + t) };

		context.FillPolygon(color, puntos);
	}

	/// <summary>El contenido del panel flotante dibujado sobre el gráfico (colores y flechas vectoriales).</summary>
	private void DibujarPanelEnGrafico(RenderContext context, Rectangle area, ContenidoPanel c)
	{
		var alto = LabelFontSize + 6;
		var lineas = c.Lineas.Where(l => !l.Separador).ToList();
		var ancho = Math.Max(300, lineas.Select(l => context.MeasureString(l.Texto, _fuente!).Width + 16 * (l.Sangria + 1) + 20).DefaultIfEmpty(300).Max());
		var caja = new Rectangle(area.Left + _v.InChartPanelX, area.Top + _v.InChartPanelY, ancho, alto * (lineas.Count + 2) + 8);
		context.FillRectangle(Color.FromArgb(220, 18, 20, 26), caja);
		var y = caja.Top + 4;

		foreach (var l in lineas)
		{
			var x = caja.Left + 6 + 12 * l.Sangria;

			foreach (var s in l.Segmentos)
			{
				if (s.Flecha != FlechaPanel.Ninguna)
				{
					Flecha(context, x, y + alto / 2, s.Flecha == FlechaPanel.Arriba ? 1 : -1, ColorDe(s.ColorFlecha ?? s.Color));
					x += 16;
				}

				var w = context.MeasureString(s.Texto, _fuente!).Width;
				context.DrawString(s.Texto, _fuente!, ColorDe(s.Color), new Rectangle(x, y, w + 4, alto), _izquierda);
				x += w;
			}

			if (l.Recuadro is { } borde)
				context.DrawRectangle(new RenderPen(ColorDe(borde), 1), new Rectangle(caja.Left + 3, y, caja.Width - 6, alto));

			y += alto;
		}

		context.DrawString(c.Pie, _fuente!, ColorDe(ColorPanel.Gris), new Rectangle(caja.Left + 6, y + 4, caja.Width - 12, alto), _izquierda);
	}
}
