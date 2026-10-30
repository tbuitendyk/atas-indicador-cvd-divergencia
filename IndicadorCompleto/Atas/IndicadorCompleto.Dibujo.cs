namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators;

using Completo.Nucleo;

using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StdCursor = OFT.Rendering.StdCursor;
using StringAlignment = System.Drawing.StringAlignment;

public partial class IndicadorCompleto
{
	#region Ajustes de dibujo

	[Display(Name = "Color LVN alta prioridad", GroupName = "9. Dibujo", Order = 100)]
	public MediaColor ColorLvnAlta { get; set; } = MediaColor.FromArgb(255, 255, 239, 0);

	[Display(Name = "Color LVN secundario", GroupName = "9. Dibujo", Order = 101)]
	public MediaColor ColorLvnSecundario { get; set; } = MediaColor.FromArgb(255, 255, 140, 0);

	[Display(Name = "Mostrar LVN menores (gris)", GroupName = "9. Dibujo", Order = 102)]
	public bool MostrarLvnMenores { get; set; }

	[Display(Name = "Mostrar mapas LVN de sesiones anteriores", GroupName = "9. Dibujo", Order = 103)]
	public bool MostrarMapasHistoricos { get; set; }

	[Display(Name = "Perfil de volumen de origen", GroupName = "9. Dibujo", Order = 104,
		Description = "Histograma del perfil con que se calcularon los LVN, para comprobarlos con su propia lectura.")]
	public PerfilVisible PerfilVisible { get; set; } = PerfilVisible.Sesiones20;

	[Display(Name = "Ancho del perfil (píxeles)", GroupName = "9. Dibujo", Order = 105)]
	[Range(20, 1000)]
	public int AnchoPerfil { get; set; } = 160;

	[Display(Name = "Color entrada EXTERNA", GroupName = "9. Dibujo", Order = 106)]
	public MediaColor ColorEntradaExterna { get; set; } = MediaColor.FromArgb(255, 255, 255, 255);

	[Display(Name = "Color entrada INTERNA", GroupName = "9. Dibujo", Order = 107)]
	public MediaColor ColorEntradaInterna { get; set; } = MediaColor.FromArgb(255, 255, 165, 0);

	[Display(Name = "Color EMA", GroupName = "9. Dibujo", Order = 108)]
	public MediaColor ColorEma { get; set; } = MediaColor.FromArgb(255, 0, 229, 255);

	[Display(Name = "Grosor EMA", GroupName = "9. Dibujo", Order = 109)]
	[Range(1, 6)]
	public int GrosorEma { get; set; } = 2;

	[Display(Name = "Mostrar EMA", GroupName = "9. Dibujo", Order = 110)]
	public bool MostrarEma { get; set; } = true;

	[Display(Name = "Mostrar FVG", GroupName = "9. Dibujo", Order = 111)]
	public bool MostrarFvg { get; set; } = true;

	[Display(Name = "Mostrar todos los barridos", GroupName = "9. Dibujo", Order = 112)]
	public bool MostrarBarridos { get; set; } = true;

	[Display(Name = "Mostrar setups bloqueados (gris)", GroupName = "9. Dibujo", Order = 113)]
	public bool MostrarBloqueados { get; set; } = true;

	[Display(Name = "Mostrar divergencias CVD", GroupName = "9. Dibujo", Order = 114)]
	public bool MostrarDivergencias { get; set; } = true;

	[Display(Name = "Mostrar directrices", GroupName = "9. Dibujo", Order = 115)]
	public bool MostrarDirectrices { get; set; } = true;

	[Display(Name = "Posición X del recuadro de estado", GroupName = "9. Dibujo", Order = 116)]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Posición Y del recuadro de estado", GroupName = "9. Dibujo", Order = 117)]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 60;

	#endregion

	private readonly RenderFont _fuente = new("Arial", 8f, FontStyle.Regular);
	private readonly RenderFont _fuenteNegrita = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderStringFormat _izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderStringFormat _centro = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	private readonly ArrastrePanel _arrastre = new();

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		try
		{
			Dibujar(context);
		}
		catch (Exception ex)
		{
			this.LogError("IndicadorCompleto: error de dibujo", ex);
		}
	}

	private static Color ADibujo(MediaColor c, int alfa = -1) => Color.FromArgb(alfa < 0 ? c.A : alfa, c.R, c.G, c.B);

	private Rectangle Area => ChartInfo?.PriceChartContainer.Region ?? Rectangle.Empty;

	private int Centro(int bar) => ChartInfo!.GetXByBar(bar) + (int)(ChartInfo.PriceChartContainer.BarsWidth / 2);

	private int Y(decimal precio) => ChartInfo!.GetYByPrice(precio, false);

	/// <summary>Vela de este gráfico que contiene el instante: la última cuyo inicio es anterior o igual.</summary>
	private int? Barra(DateTime utc)
	{
		lock (_sync)
		{
			if (_tiempos.Count == 0 || utc < _tiempos[0])
				return null;

			int bajo = 0, alto = _tiempos.Count - 1;

			while (bajo < alto)
			{
				var medio = (bajo + alto + 1) / 2;

				if (_tiempos[medio] <= utc)
					bajo = medio;
				else
					alto = medio - 1;
			}

			return bajo;
		}
	}

	private int? X(DateTime utc) => Barra(utc) is { } bar ? Centro(bar) : null;

	private void Dibujar(RenderContext context)
	{
		if (ChartInfo is null)
			return;

		var motor = MotorActivo();
		DibujarEstado(context, motor);

		if (motor is null)
			return;

		DateTime desde, hasta;
		int primera, ultima;

		lock (_sync)
		{
			if (_tiempos.Count == 0)
				return;

			primera = Math.Clamp(FirstVisibleBarNumber, 0, _tiempos.Count - 1);
			ultima = Math.Clamp(LastVisibleBarNumber, 0, _tiempos.Count - 1);
			desde = _tiempos[primera];
			hasta = _tiempos[ultima].AddMinutes(Math.Max(1, _marcoGrafico));
		}

		var datos = motor.Dibujo(desde, hasta, PerfilVisible, MostrarMapasHistoricos);

		if (MostrarFvg)
			DibujarFvg(context, datos);

		DibujarLvn(context, datos);
		DibujarPerfil(context, datos);

		if (MostrarEma)
			DibujarEma(context, datos, primera, ultima);

		if (MostrarDirectrices)
			DibujarDirectrices(context, datos);

		if (MostrarDivergencias)
			DibujarDivergencias(context, datos);

		if (MostrarBarridos)
			DibujarBarridos(context, datos);

		if (MostrarBloqueados)
			DibujarBloqueados(context, datos);

		DibujarSenales(context, datos);
	}

	private void DibujarLvn(RenderContext context, DatosDibujo datos)
	{
		var area = Area;
		var etiquetas = new List<(int Y, int XFin, string Texto, Color Color, decimal Orden)>();

		foreach (var mapa in datos.Mapas)
		{
			var x1 = Math.Max(area.Left, X(mapa.CreadoUtc) ?? area.Left);
			var x2 = mapa.FinUtc is { } fin ? X(fin) ?? area.Right : area.Right;

			if (x2 <= x1)
				continue;

			var vigente = mapa == datos.Mapas[^1];

			// Primero las menores y las secundarias; las de alta prioridad quedan encima.
			foreach (var z in mapa.Visibles(MostrarLvnMenores).OrderByDescending(z => z.Prioridad))
			{
				var y1 = Y(z.Superior);
				var y2 = Y(z.Inferior);

				if (y2 < area.Top || y1 > area.Bottom)
					continue;

				var (color, alfa, grosor) = z.Prioridad switch
				{
					PrioridadLvn.Alta => (ColorLvnAlta, 70, 2),
					PrioridadLvn.Secundaria => (ColorLvnSecundario, 45, 1),
					_ => (MediaColor.FromArgb(255, 140, 140, 140), 25, 1)
				};

				context.FillRectangle(ADibujo(color, alfa), new Rectangle(x1, y1, x2 - x1, Math.Max(1, y2 - y1)));

				var pluma = new RenderPen(ADibujo(color), grosor);
				context.DrawLine(pluma, x1, y1, x2, y1);
				context.DrawLine(pluma, x1, y2, x2, y2);

				if (vigente && z.Prioridad != PrioridadLvn.Menor)
					etiquetas.Add(((y1 + y2) / 2, x2, z.Etiqueta, ADibujo(color), (z.Prioridad == PrioridadLvn.Alta ? 1000 : 0) + z.Puntuacion));
			}
		}

		// Etiquetas sin solaparse: primero las de alta prioridad; una etiqueta demasiado cerca de otra se omite.
		var colocadas = new List<int>();

		foreach (var e in etiquetas.OrderByDescending(e => e.Orden))
		{
			if (colocadas.Any(y => Math.Abs(y - e.Y) < 13))
				continue;

			colocadas.Add(e.Y);
			var ancho = context.MeasureString(e.Texto, _fuenteNegrita).Width + 8;
			var caja = new Rectangle(e.XFin - ancho - AnchoPerfil - 14, e.Y - 7, ancho, 14);
			context.FillRectangle(Color.FromArgb(170, 15, 15, 20), caja);
			context.DrawString(e.Texto, _fuenteNegrita, e.Color, caja, _centro);
		}
	}

	private void DibujarPerfil(RenderContext context, DatosDibujo datos)
	{
		if (datos.Perfil is not { Count: > 0 } perfil || datos.Mapas.Count == 0)
			return;

		var area = Area;
		var mapa = datos.Mapas[^1];
		var maximo = perfil.Values.Max();
		var derecha = area.Right - 6;
		var tick = datos.Tick;
		var alto = Math.Max(1, Math.Abs(Y(0m) - Y(tick)));
		var zonas = mapa.Visibles(false).ToList();

		foreach (var (t, volumen) in perfil)
		{
			var precio = t * tick;
			var y = Y(precio);

			if (y < area.Top || y > area.Bottom || maximo <= 0)
				continue;

			var largo = (int)(volumen / maximo * AnchoPerfil);
			var zona = zonas.FirstOrDefault(z => precio >= z.Inferior && precio < z.Superior);
			var color = zona?.Prioridad switch
			{
				PrioridadLvn.Alta => ADibujo(ColorLvnAlta, 200),
				PrioridadLvn.Secundaria => ADibujo(ColorLvnSecundario, 190),
				_ => Color.FromArgb(110, 170, 170, 170)
			};

			context.FillRectangle(color, new Rectangle(derecha - largo, y - alto / 2, Math.Max(1, largo), alto));
		}

		var nombre = PerfilVisible switch
		{
			PerfilVisible.NyAnterior => "última sesión NY",
			PerfilVisible.Sesiones5 => "últimas 5 sesiones",
			PerfilVisible.Sesiones10 => "últimas 10 sesiones",
			PerfilVisible.Sesiones15 => "últimas 15 sesiones",
			_ => "últimas 20 sesiones"
		};

		context.DrawString($"Perfil de origen: {nombre} · mapa v{mapa.Version}", _fuente, Color.Silver,
			new Rectangle(derecha - AnchoPerfil, area.Top + 4, AnchoPerfil, 14), _izquierda);
	}

	private void DibujarFvg(RenderContext context, DatosDibujo datos)
	{
		var area = Area;

		foreach (var f in datos.Fvgs)
		{
			if (X(f.C1.InicioUtc) is not { } x1)
				continue;

			var x2 = f.InvalidadoUtc is { } fin ? X(fin) ?? area.Right : area.Right;
			var color = f.Direccion == Direccion.Larga ? Color.FromArgb(40, 0, 188, 212) : Color.FromArgb(40, 186, 104, 200);
			var y1 = Y(f.Superior);
			context.FillRectangle(color, new Rectangle(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, Y(f.Inferior) - y1)));
		}
	}

	private void DibujarEma(RenderContext context, DatosDibujo datos, int primera, int ultima)
	{
		if (datos.Ema.Count == 0)
			return;

		var tiempos = datos.Ema.Select(kv => kv.Key).ToList();
		var pluma = new RenderPen(ADibujo(ColorEma), GrosorEma);
		Point? anterior = null;

		for (var bar = primera; bar <= ultima; bar++)
		{
			DateTime inicio;

			lock (_sync)
				inicio = _tiempos[bar];

			// Valor de la EMA al cierre de esta vela del gráfico (la última calculada dentro de ella).
			var limite = inicio.AddMinutes(Math.Max(1, _marcoGrafico)).AddMinutes(-(int)datos.MarcoEma);
			var i = tiempos.BinarySearch(limite);
			i = i >= 0 ? i : ~i - 1;

			if (i < 0)
			{
				anterior = null;
				continue;
			}

			var punto = new Point(Centro(bar), Y(datos.Ema[i].Value));

			if (anterior is { } a)
				context.DrawLine(pluma, a.X, a.Y, punto.X, punto.Y);

			anterior = punto;
		}
	}

	private void DibujarDirectrices(RenderContext context, DatosDibujo datos)
	{
		var pluma = new RenderPen(Color.FromArgb(200, 120, 180, 255), 1);

		foreach (var linea in datos.Directrices)
		{
			var fin = linea.RotaUtc ?? DateTime.UtcNow;

			if (X(linea.P1.ExtremoUtc) is not { } x1)
				continue;

			var x2 = X(fin) ?? Area.Right;
			context.DrawLine(pluma, x1, Y(linea.P1.Precio), x2, Y(linea.ValorEn(fin)));
		}
	}

	private void DibujarDivergencias(RenderContext context, DatosDibujo datos)
	{
		foreach (var d in datos.Divergencias)
		{
			if (X(d.P1.ExtremoUtc) is not { } x1 || X(d.P2.ExtremoUtc) is not { } x2)
				continue;

			var color = d.Alcista ? Color.FromArgb(220, 0, 200, 83) : Color.FromArgb(220, 255, 82, 82);
			context.DrawLine(new RenderPen(color, 1), x1, Y(d.P1.Precio), x2, Y(d.P2.Precio));
			context.DrawString($"div {d.Marco.Texto()}", _fuente, color, new Rectangle(x2 - 30, Y(d.P2.Precio) + (d.Alcista ? 4 : -18), 60, 14), _centro);
		}
	}

	private void DibujarBarridos(RenderContext context, DatosDibujo datos)
	{
		foreach (var b in datos.Barridos)
		{
			if (X(b.VelaBarrido.InicioUtc) is not { } x)
				continue;

			var y = Y(b.Extremo);
			var color = b.Estado == EstadoBarrido.Bloqueado ? Color.FromArgb(150, 130, 130, 130) : Color.FromArgb(220, 210, 210, 210);
			var pluma = new RenderPen(color, 1);
			context.DrawLine(pluma, x - 4, y - 4, x + 4, y + 4);
			context.DrawLine(pluma, x - 4, y + 4, x + 4, y - 4);

			var abajo = b.Direccion == Direccion.Larga;
			context.DrawString($"B{b.Id} {(b.Tipo == TipoLiquidez.Externa ? "EXT" : "INT")}", _fuente, color,
				new Rectangle(x - 30, abajo ? y + 6 : y - 20, 60, 14), _centro);
		}
	}

	private void DibujarBloqueados(RenderContext context, DatosDibujo datos)
	{
		var color = Color.FromArgb(170, 150, 150, 150);

		foreach (var s in datos.Bloqueados)
		{
			if (X(s.Vela.FinUtc.AddSeconds(-1)) is not { } x)
				continue;

			var larga = s.Direccion == Direccion.Larga;
			var y = larga ? Y(s.Vela.Minimo) + 26 : Y(s.Vela.Maximo) - 26;
			Flecha(context, x, y, larga, color);

			var motivo = s.Motivo.Length > 34 ? s.Motivo[..34] + "…" : s.Motivo;
			context.DrawString($"{s.Modelo} bloqueada: {motivo}", _fuente, color, new Rectangle(x - 110, larga ? y + 14 : y - 28, 220, 14), _centro);
		}
	}

	private void DibujarSenales(RenderContext context, DatosDibujo datos)
	{
		foreach (var s in datos.Senales)
		{
			if (X(s.ConfirmadaUtc.AddSeconds(-1)) is not { } x)
				continue;

			var color = ADibujo(s.Tipo == TipoLiquidez.Externa ? ColorEntradaExterna : ColorEntradaInterna);
			var larga = s.Direccion == Direccion.Larga;
			var y = larga ? Y(s.VelaGatillo.Minimo) + 8 : Y(s.VelaGatillo.Maximo) - 8;
			Flecha(context, x, y, larga, color);

			var texto = $"{s.Titulo} #{s.Id} · {s.Plan.Cantidad} micros";
			context.DrawString(texto, _fuenteNegrita, color, new Rectangle(x - 100, larga ? y + 16 : y - 30, 200, 14), _centro);

			// SL y TP propuestos: tramos cortos a la derecha de la entrada.
			var x2 = x + (int)Math.Max(60, 10 * (ChartInfo!.PriceChartContainer.BarSpacing + ChartInfo.PriceChartContainer.BarsWidth));
			var ySl = Y(s.Plan.Sl);
			var yTp = Y(s.Plan.Tp);
			context.DrawLine(new RenderPen(Color.FromArgb(230, 255, 70, 70), 1), x, ySl, x2, ySl);
			context.DrawLine(new RenderPen(Color.FromArgb(230, 0, 230, 118), 1), x, yTp, x2, yTp);
			context.DrawString($"SL {s.Plan.Sl}", _fuente, Color.FromArgb(230, 255, 70, 70), new Rectangle(x2 + 2, ySl - 7, 90, 14), _izquierda);
			context.DrawString($"TP {s.Plan.Tp}", _fuente, Color.FromArgb(230, 0, 230, 118), new Rectangle(x2 + 2, yTp - 7, 90, 14), _izquierda);
		}
	}

	/// <summary>Flecha dibujada con formas: hacia arriba (largos, bajo la vela) o hacia abajo (cortos, sobre la vela).</summary>
	private static void Flecha(RenderContext context, int x, int y, bool arriba, Color color)
	{
		var puntos = arriba
			? new[] { new Point(x, y), new Point(x - 7, y + 12), new Point(x + 7, y + 12) }
			: new[] { new Point(x, y), new Point(x - 7, y - 12), new Point(x + 7, y - 12) };

		context.FillPolygon(color, puntos);
	}

	/// <summary>Recuadro de estado (arrastrable): rol, grupo, si hay maestro e interruptores de las alertas.</summary>
	private void DibujarEstado(RenderContext context, MotorCompleto? motor)
	{
		const int ancho = 360;
		const int alto = 60;

		var panel = _arrastre.Ubicar(PanelX, PanelY, ancho, alto, 18, Area);
		var estado = motor is null
			? $"SIN MAESTRO para la clave {_clave}: añada el indicador a un gráfico de 1 minuto del mismo instrumento"
			: $"motor: {motor.VelasProcesadas} velas 1M · rev. {motor.Revision}";

		context.FillRectangle(Color.FromArgb(215, 18, 20, 26), panel);
		context.FillRectangle(Color.FromArgb(235, 30, 34, 44), new Rectangle(panel.X, panel.Y, ancho, 18));
		context.DrawString($"INDICADOR COMPLETO · {_estadoRol}", _fuenteNegrita, Color.Gainsboro, new Rectangle(panel.X + 6, panel.Y, ancho - 12, 18), _izquierda);
		context.DrawString(estado, _fuente, motor is null ? Color.Orange : Color.Silver, new Rectangle(panel.X + 6, panel.Y + 19, ancho - 12, 18), _izquierda);
		DibujarIconosAlerta(context, panel.X, panel.Y + 39, ancho, motor is not null);
	}

	public override bool ProcessMouseDown(RenderControlMouseEventArgs e) =>
		ClicEnAlerta(e.Location) || _arrastre.Presionar(e.Location) || base.ProcessMouseDown(e);

	public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
	{
		if (_arrastre.Mover(e.Location, Area) is not { } posicion)
			return base.ProcessMouseMove(e);

		PanelX = posicion.X;
		PanelY = posicion.Y;
		RedrawChart();
		return true;
	}

	public override bool ProcessMouseUp(RenderControlMouseEventArgs e) =>
		_arrastre.Soltar() || base.ProcessMouseUp(e);

	public override StdCursor GetCursor(RenderControlMouseEventArgs e) =>
		_arrastre.Arrastrando || _arrastre.SobreTitulo(e.Location) || IconoAlerta(e.Location) >= 0 ? StdCursor.Hand : base.GetCursor(e);
}
