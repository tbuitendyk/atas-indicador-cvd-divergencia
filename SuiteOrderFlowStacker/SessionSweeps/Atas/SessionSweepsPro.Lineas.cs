namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using SessionSweeps.Nucleo;

using Color = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;

public enum ModoLineasSesion
{
	[Display(Name = "Blancas sólidas (v4)")]
	Blancas,

	[Display(Name = "Original (punteadas del color de la sesión)")]
	Original,

	[Display(Name = "Apagadas")]
	Apagadas
}

/// <summary>
/// v4: líneas de máximos y mínimos de sesión en blanco sólido (configurables), la de NY desde el último máximo o mínimo
/// reciente, todas se detienen al romperse; BOS de sesión; aviso arriba a la derecha al romperse una línea.
/// </summary>
public partial class SessionSweepsPro
{
	private (string Texto, DateTime Hasta) _aviso;

	[Display(Name = "Líneas de máximos y mínimos", GroupName = G1, Order = 300,
		Description = "Blancas sólidas: se detienen al romperse (no se extienden a la derecha después de la ruptura). Original: las punteadas del color de cada sesión.")]
	public ModoLineasSesion SessionLinesMode { get; set; } = ModoLineasSesion.Blancas;

	[Display(Name = "Líneas: color", GroupName = G1, Order = 310)]
	public MediaColor SessionLinesColor { get; set; } = MediaColor.FromArgb(255, 255, 255, 255);

	[Display(Name = "Líneas: discontinuas", GroupName = G1, Order = 320)]
	public bool SessionLinesDashed { get; set; }

	[Display(Name = "Líneas: grosor", GroupName = G1, Order = 330)]
	[Range(1, 6)]
	public int SessionLinesWidth { get; set; } = 1;

	[Display(Name = "NY: línea desde el último máximo / mínimo reciente", GroupName = G1, Order = 340,
		Description = "Con NY abierta, una línea desde el último máximo y el último mínimo recientes (pivotes de 1 minuto) hacia la derecha, hasta que se rompe.")]
	public bool NyRecentLines { get; set; } = true;

	[Display(Name = "Marcar BOS: Asia", GroupName = G1, Order = 350, Description = "BOS = cierre de 1 minuto más allá del máximo o mínimo de la sesión.")]
	public bool SessionBosAsia { get; set; } = true;

	[Display(Name = "Marcar BOS: Londres", GroupName = G1, Order = 360)]
	public bool SessionBosLondres { get; set; } = true;

	[Display(Name = "Marcar BOS: Premercado", GroupName = G1, Order = 370)]
	public bool SessionBosPremercado { get; set; } = true;

	[Display(Name = "Marcar BOS: NY", GroupName = G1, Order = 380)]
	public bool SessionBosNy { get; set; } = true;

	private bool BosDe(string sesionId) => sesionId switch
	{
		ConfiguracionSweeps.Asia => SessionBosAsia,
		ConfiguracionSweeps.Londres => SessionBosLondres,
		ConfiguracionSweeps.Premercado => SessionBosPremercado,
		ConfiguracionSweeps.NyAm or ConfiguracionSweeps.NyPm => SessionBosNy,
		_ => false
	};

	/// <summary>Una alerta de ruptura de línea: el aviso queda arriba a la derecha BreakBannerSec segundos.</summary>
	private void AvisoLinea(string texto)
	{
		if (_v.BreakBannerSec > 0)
			_aviso = (texto, DateTime.UtcNow.AddSeconds(_v.BreakBannerSec));
	}

	private void DibujarLineasSesion(RenderContext context, MotorSweeps motor, Rectangle area, Func<int, int, bool> visible)
	{
		if (SessionLinesMode != ModoLineasSesion.Blancas)
			return;

		var lineas = motor.Leer(m => m.LineasSesion.Lineas.Select(l => (l.SesionId, l.Sesion, l.Alto, l.Precio, l.DesdeBar, l.RotaBar, l.BosBar, l.Reciente)).ToList());
		var pluma = new RenderPen(C(SessionLinesColor), Math.Max(1, SessionLinesWidth));

		foreach (var l in lineas)
		{
			if (l.Reciente && !NyRecentLines)
				continue;

			var fin = l.RotaBar ?? LastVisibleBarNumber;

			if (!visible(l.DesdeBar, fin))
				continue;

			var y = Y(l.Precio);
			var x1 = X(l.DesdeBar);
			var x2 = l.RotaBar is { } r ? X(r) + AnchoVela / 2 : area.Right;

			if (SessionLinesDashed)
				Punteada(context, pluma, x1, x2, y);
			else
				context.DrawLine(pluma, x1, y, x2, y);

			var nombre = $"{(l.Alto ? "MÁX" : "MÍN")} {l.Sesion}{(l.Reciente ? " rec." : "")}";
			context.DrawString(nombre, _fuente!, C(SessionLinesColor, 200), new Rectangle(x1 + 2, l.Alto ? y - 14 : y + 1, 140, 13), _izquierda);

			if (l.BosBar is { } b && BosDe(l.SesionId) && visible(b, b))
			{
				var color = l.Alto ? Color.FromArgb(255, 38, 166, 154) : Color.FromArgb(255, 239, 83, 80);
				context.DrawString("BOS", _fuente!, color, new Rectangle(X(b) - 12, l.Alto ? y - 28 : y + 14, 40, 13), _izquierda);
				Flecha(context, X(b) - 6, l.Alto ? y - 34 : y + 34, l.Alto ? 1 : -1, color);
			}
		}
	}

	/// <summary>Aviso de ruptura de línea arriba a la derecha del gráfico.</summary>
	private void DibujarAviso(RenderContext context, Rectangle area)
	{
		var (texto, hasta) = _aviso;

		if (string.IsNullOrEmpty(texto) || DateTime.UtcNow > hasta)
			return;

		var ancho = (int)Math.Min(area.Width - 20, context.MeasureString(texto, _fuente!).Width + 24);
		var caja = new Rectangle(area.Right - ancho - 10, area.Top + 10, ancho, LabelFontSize + 14);
		context.FillRectangle(Color.FromArgb(235, 120, 20, 20), caja);
		context.DrawRectangle(new RenderPen(Color.FromArgb(255, 255, 193, 7), 2), caja);
		context.DrawString(texto, _fuente!, Color.White, new Rectangle(caja.X + 12, caja.Y, caja.Width - 16, caja.Height), _izquierda);
	}
}
