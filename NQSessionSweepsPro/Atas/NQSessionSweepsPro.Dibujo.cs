namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

using NqSweeps.Nucleo;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

public partial class NQSessionSweepsPro
{
	private readonly RenderStringFormat _izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderStringFormat _centro = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	private readonly List<(int Y, string Texto, Color Color)> _etiquetas = new();
	private RenderFont? _fuente, _fuenteTabla;
	private int _tamanoFuente, _tamanoTabla;

	/// <summary>Lo que se dibuja, copiado del motor bajo su cerrojo (el motor puede estar calculando en otro hilo).</summary>
	private sealed class Foto
	{
		public List<InstanciaSesion> Sesiones = new();
		public List<NivelLiquidez> Niveles = new();
		public List<ZonaBarrido> Zonas = new();
		public List<ZonaFvg> Fvgs = new();
		public List<LineaChoch> Choch = new();
		public List<LvnNivel> Lvns = new();
		public List<Senal> Senales = new();
		public SecuenciaB? Secuencia;
		public ZonaBarrido? Ultima;
		public string Panel = "";
		public string H1 = "";
		public int UltimaVela;
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		try
		{
			Dibujar(context);
		}
		catch (Exception ex)
		{
			this.LogError("NQSessionSweepsPro: error de dibujo", ex);
		}
	}

	private Rectangle Area => ChartInfo?.PriceChartContainer.Region ?? Rectangle.Empty;

	private int X(int bar) => ChartInfo!.GetXByBar(bar);

	private int Y(decimal precio) => ChartInfo!.GetYByPrice(precio, false);

	private int AnchoVela => Math.Max(1, (int)ChartInfo!.PriceChartContainer.BarsWidth);

	private static Color C(MediaColor c, int alfa = -1) => Color.FromArgb(alfa < 0 ? c.A : alfa, c.R, c.G, c.B);

	private void Dibujar(RenderContext context)
	{
		if (ChartInfo is null)
			return;

		var area = Area;

		if (_tamanoFuente != LabelFontSize || _fuente is null)
			(_fuente, _tamanoFuente) = (new RenderFont("Arial", LabelFontSize), LabelFontSize);

		var tamanoTabla = TableTextSize switch { TamanoTexto.Grande => 11, TamanoTexto.Normal => 9, _ => 8 };

		if (_tamanoTabla != tamanoTabla || _fuenteTabla is null)
			(_fuenteTabla, _tamanoTabla) = (new RenderFont("Arial", tamanoTabla), tamanoTabla);

		if (_motor is not { } motor)
		{
			if (_estado.Length > 0)
				context.DrawString(_estado, _fuente, Color.Orange, new Rectangle(area.Right - 420, area.Bottom - 30, 410, 20), _izquierda);

			return;
		}

		var foto = motor.Leer(m => new Foto
		{
			Sesiones = m.Sesiones.Instancias.ToList(),
			Niveles = m.Niveles.Niveles.ToList(),
			Zonas = m.ZonasBarrido.ToList(),
			Fvgs = m.Fvg.Fvgs.ToList(),
			Choch = m.Mss.Lineas.ToList(),
			Lvns = m.PerfilLvn?.Lvns.ToList() ?? new List<LvnNivel>(),
			Senales = m.Senales.ToList(),
			Secuencia = m.SecuenciasB.LastOrDefault(),
			Ultima = m.ZonasBarrido.LastOrDefault(),
			Panel = m.ZonasBarrido.LastOrDefault()?.Panel(m.Precio) ?? "",
			H1 = "H1 " + m.H1.Valor.Texto(),
			UltimaVela = m.Velas.Count - 1
		});

		var desde = Math.Max(0, FirstVisibleBarNumber);
		var hasta = Math.Max(desde, LastVisibleBarNumber);
		bool Visible(int a, int b) => b >= desde && a <= hasta;
		_etiquetas.Clear();

		DibujarSesiones(context, foto, Visible);
		DibujarNiveles(context, foto, Visible, area, motor);

		if (ShowFvg)
			DibujarFvg(context, foto, Visible);

		DibujarZonas(context, foto, Visible);
		DibujarLvn(context, foto, area, motor);

		if (ShowTargets && foto.Ultima?.Proyeccion is { } p)
			DibujarObjetivos(context, foto.Ultima, p, area, motor);

		DibujarSenales(context, foto, Visible);
		DibujarChoch(context, foto, Visible);

		if (ShowLevelLabels)
			DibujarEtiquetas(context, area);

		if (ShowTable)
			DibujarTabla(context, area, foto.H1);

		if (ShowSweepPanel && foto.Panel.Length > 0)
			Recuadro(context, PanelX, PanelY, foto.Panel, Color.Gainsboro);

		if (ShowModelBStatus && foto.Secuencia is { } b)
		{
			var texto = $"Modelo B ({b.Zona.Nombre} {motor.Precio(b.Zona.L)}): {b.Etapa.Texto()}"
				+ (b.Nivel is { } n ? $"\n{b.NombreNivel} {motor.Precio(n)}" : "")
				+ (b.Motivo.Length > 0 ? $"\n{b.Motivo}" : "");
			Recuadro(context, EstadoBX, EstadoBY, texto, b.Etapa == EtapaModeloB.Entrada ? Color.LightGreen : Color.Gainsboro);
		}
	}

	private void Punteada(RenderContext context, RenderPen pluma, int x1, int x2, int y)
	{
		for (var x = x1; x < x2; x += 6)
			context.DrawLine(pluma, x, y, Math.Min(x + 3, x2), y);
	}

	private void DibujarSesiones(RenderContext context, Foto foto, Func<int, int, bool> visible)
	{
		foreach (var s in foto.Sesiones)
		{
			var def = _cfg.Sesiones.FirstOrDefault(d => d.Id == s.Id);

			if (def is null)
				continue;

			var color = ColorSesion(s.Id);
			var pluma = new RenderPen(C(color, 255), SessionLineWidth);
			var finAlto = s.Abierta ? s.HastaBar : Math.Min(s.FinExtension ?? foto.UltimaVela, ExtendUntilSwept && s.AltoBarridoEn is { } a ? a : foto.UltimaVela);
			var finBajo = s.Abierta ? s.HastaBar : Math.Min(s.FinExtension ?? foto.UltimaVela, ExtendUntilSwept && s.BajoBarridoEn is { } b ? b : foto.UltimaVela);

			if (!visible(s.DesdeBar, Math.Max(finAlto, finBajo)))
				continue;

			var x1 = X(s.DesdeBar);
			var x2 = X(s.HastaBar) + AnchoVela;
			var yAlto = Y(s.Alto);
			var yBajo = Y(s.Bajo);

			if (def.Sombrear)
				context.FillRectangle(C(color, 255 * (100 - SessionFillTransparency) / 100), new Rectangle(x1, yAlto, Math.Max(1, x2 - x1), Math.Max(1, yBajo - yAlto)));

			if (SessionExtremeLines)
			{
				context.DrawLine(pluma, x1, yAlto, x2, yAlto);
				context.DrawLine(pluma, x1, yBajo, x2, yBajo);
			}

			if (def.MostrarNombre)
				context.DrawString(s.Nombre, _fuente!, C(color, 190), new Rectangle(x1 + 2, yAlto - 14, 140, 13), _izquierda);

			if (s.Abierta)
				continue;

			// Al cerrar: máximo, mínimo y línea media punteados hasta que se barren o la sesión vuelve a empezar.
			if (def.ExtenderMaxMin)
			{
				Punteada(context, pluma, x2, X(finAlto) + AnchoVela, yAlto);
				Punteada(context, pluma, x2, X(finBajo) + AnchoVela, yBajo);
			}

			if (def.ExtenderMedia)
				Punteada(context, pluma, x2, X(s.FinExtension ?? foto.UltimaVela) + AnchoVela, Y(s.Medio));
		}
	}

	private void DibujarNiveles(RenderContext context, Foto foto, Func<int, int, bool> visible, Rectangle area, MotorSweeps motor)
	{
		var pluma = new RenderPen(Color.FromArgb(170, 180, 180, 180), 1);

		foreach (var n in foto.Niveles.Where(n => n.Activo && motor.Niveles.Vigilado(n)))
		{
			// Los de sesión ya tienen sus líneas; los diarios, semanales e iguales se dibujan aquí.
			if (!n.Grupo.StartsWith("sesion") && visible(n.CreadoBar, foto.UltimaVela))
				Punteada(context, pluma, X(n.CreadoBar), area.Right, Y(n.Precio));

			_etiquetas.Add((Y(n.Precio), $"{n.Nombre} {motor.Precio(n.Precio)}", Color.FromArgb(220, 200, 200, 200)));
		}
	}

	private void DibujarZonas(RenderContext context, Foto foto, Func<int, int, bool> visible)
	{
		foreach (var z in foto.Zonas.Where(z => visible(z.Izquierda, z.Derecha)))
		{
			var falso = z.Clasificacion == Clasificacion.Falso || z.FalsoPine;

			if (falso && HideFakeSweepZones)
				continue;

			var lado = z.Lado == Lado.Buyside ? BuysideColor : SellsideColor;
			var color = falso ? C(FakeColor)
				: z.Clasificacion == Clasificacion.SinLiquidez ? C(lado, 51)
				: z.Clasificacion == Clasificacion.Real ? C(lado, 200)
				: C(lado);
			var y1 = Y(Math.Max(z.L, z.Extremo));
			var y2 = Y(Math.Min(z.L, z.Extremo));
			var x1 = X(z.Izquierda);
			var x2 = X(z.Derecha);
			context.FillRectangle(color, new Rectangle(x1, y1, Math.Max(1, x2 - x1), Math.Max(2, y2 - y1)));
			context.DrawString(z.Etiqueta, _fuente!, Color.White, new Rectangle(x2 + 3, (z.Lado == Lado.Buyside ? y1 : y2) - 7, 150, 14), _izquierda);
		}
	}

	private void DibujarFvg(RenderContext context, Foto foto, Func<int, int, bool> visible)
	{
		foreach (var f in foto.Fvgs.Where(f => visible(f.Izquierda, f.Derecha)))
		{
			var color = f.Ifvg ? C(IfvgColor) : f.Alcista ? C(FvgBullColor) : C(FvgBearColor);
			var caja = new Rectangle(X(f.Izquierda), Y(f.Arriba), Math.Max(1, X(f.Derecha) + AnchoVela - X(f.Izquierda)), Math.Max(1, Y(f.Abajo) - Y(f.Arriba)));
			context.FillRectangle(color, caja);

			if (f.PostSweep)
				context.DrawRectangle(new RenderPen(Color.FromArgb(255, color), 2), caja);

			var texto = f.Ifvg ? "IFVG" : f.PostSweep ? "FVG post sweep" : "";

			if (texto.Length > 0)
				context.DrawString(texto, _fuente!, Color.FromArgb(255, color), new Rectangle(caja.X + 2, caja.Y, 120, 13), _izquierda);
		}
	}

	private void DibujarLvn(RenderContext context, Foto foto, Rectangle area, MotorSweeps motor)
	{
		var pluma = new RenderPen(C(LvnColor), 1);

		foreach (var l in foto.Lvns)
		{
			var y = Y(l.Precio);
			context.DrawLine(pluma, area.Left, y, area.Right, y);
			_etiquetas.Add((y, $"LVN {motor.Precio(l.Precio)}", C(LvnColor)));
		}
	}

	private void DibujarObjetivos(RenderContext context, ZonaBarrido z, Proyeccion p, Rectangle area, MotorSweeps motor)
	{
		var pluma = new RenderPen(Color.FromArgb(200, 230, 230, 230), 1);

		for (var i = 0; i < p.Objetivos.Count; i++)
		{
			var o = p.Objetivos[i];
			var y = Y(o.Precio);
			Punteada(context, pluma, X(z.BarraBarrido), area.Right, y);
			_etiquetas.Add((y, $"T{i + 1} {motor.Precio(o.Precio)} ({o.Puntos:0.##} pts){(o.FueraDeRango ? " fuera de rango" : "")}", Color.WhiteSmoke));
		}
	}

	private void DibujarSenales(RenderContext context, Foto foto, Func<int, int, bool> visible)
	{
		foreach (var s in foto.Senales.Where(s => visible(s.Barra, s.Barra)))
		{
			if (s.Barra >= CurrentBar)
				continue;

			var vela = GetCandle(s.Barra);
			var x = X(s.Barra) + AnchoVela / 2;
			var color = s.Corto ? Color.FromArgb(255, 239, 83, 80) : Color.FromArgb(255, 38, 166, 154);
			var y = s.Corto ? Y(vela.High) - 8 : Y(vela.Low) + 8;
			var puntos = s.Corto
				? new[] { new Point(x, y), new Point(x - 7, y - 12), new Point(x + 7, y - 12) }
				: new[] { new Point(x, y), new Point(x - 7, y + 12), new Point(x + 7, y + 12) };
			context.FillPolygon(color, puntos);
			context.DrawString(s.Etiqueta, _fuente!, color, new Rectangle(x - 40, s.Corto ? y - 28 : y + 14, 80, 13), _centro);
		}

		// Directriz del retroceso del Modelo B.
		if (foto.Secuencia?.Directriz is { } d)
		{
			var fin = foto.Secuencia.Senal?.Barra ?? foto.UltimaVela;
			context.DrawLine(new RenderPen(Color.FromArgb(200, 255, 255, 255), 1), X(d.B1), Y(d.P1), X(fin), Y(Modelos.ValorDirectriz(d, fin)));
		}
	}

	private void DibujarChoch(RenderContext context, Foto foto, Func<int, int, bool> visible)
	{
		foreach (var l in foto.Choch.Where(l => visible(l.Desde, l.Hasta)))
		{
			var color = l.Alcista ? C(MssBullColor) : C(MssBearColor);
			var y = Y(l.Precio);
			context.DrawLine(new RenderPen(color, 1), X(l.Desde), y, X(l.Hasta), y);
			context.DrawString("CHoCH", _fuente!, color, new Rectangle(X(l.Desde), l.Alcista ? y - 14 : y + 1, 60, 13), _izquierda);
		}
	}

	/// <summary>Etiquetas de los niveles en el borde derecho; si chocan, se desplazan.</summary>
	private void DibujarEtiquetas(RenderContext context, Rectangle area)
	{
		var alto = LabelFontSize + 6;
		var orden = _etiquetas.Where(e => e.Y >= area.Top && e.Y <= area.Bottom).OrderBy(e => e.Y).ToList();
		var ultima = int.MinValue;

		foreach (var (y, texto, color) in orden)
		{
			var yy = Math.Max(y, ultima + alto);
			ultima = yy;
			var ancho = context.MeasureString(texto, _fuente!).Width + 8;
			var caja = new Rectangle(area.Right - ancho - 4, yy - alto / 2, ancho, alto);
			context.FillRectangle(Color.FromArgb(190, 18, 20, 26), caja);
			context.DrawString(texto, _fuente!, color, caja, _centro);
		}
	}

	/// <summary>Tabla de sesiones (como "Sessions Tabular View" del Pine): color, nombre, fecha y hora ET y estado.</summary>
	private void DibujarTabla(RenderContext context, Rectangle area, string h1)
	{
		var motorSesiones = new MotorSesiones(_cfg);
		var ahora = DateTime.UtcNow;
		var filas = _cfg.Sesiones.Where(s => _cfg.Horario(s).Activa).Select(s => (s, motorSesiones.Tabla(s, ahora))).ToList();
		var alto = _tamanoTabla + 8;
		const int ancho = 400;
		var x = TableX < 0 ? area.Right - ancho - 90 : TableX;
		var y = area.Top + TableY;

		context.FillRectangle(Color.FromArgb(200, 18, 20, 26), new Rectangle(x, y, ancho, alto * (filas.Count + 1) + 4));
		context.DrawString(h1, _fuenteTabla!, Color.Gainsboro, new Rectangle(x + 6, y + 2, ancho - 12, alto), _izquierda);

		for (var i = 0; i < filas.Count; i++)
		{
			var (s, (abierta, fecha, estado)) = filas[i];
			var fy = y + 2 + alto * (i + 1);
			var color = ColorSesion(s.Id);
			context.FillRectangle(C(color, 255), new Rectangle(x + 6, fy + 3, alto - 6, alto - 6));
			context.DrawString(s.Nombre, _fuenteTabla!, C(color, 255), new Rectangle(x + alto + 4, fy, 90, alto), _izquierda);
			context.DrawString($"{fecha}  {estado}", _fuenteTabla!, abierta ? Color.FromArgb(255, 38, 166, 154) : Color.FromArgb(255, 239, 83, 80),
				new Rectangle(x + alto + 96, fy, ancho - alto - 100, alto), _izquierda);
		}
	}

	private void Recuadro(RenderContext context, int x, int y, string texto, Color color)
	{
		var lineas = texto.Split('\n');
		var alto = LabelFontSize + 6;
		var ancho = lineas.Max(l => context.MeasureString(l, _fuente!).Width) + 14;
		var area = Area;
		var caja = new Rectangle(area.Left + x, area.Top + y, ancho, alto * lineas.Length + 6);
		context.FillRectangle(Color.FromArgb(215, 18, 20, 26), caja);

		for (var i = 0; i < lineas.Length; i++)
			context.DrawString(lineas[i], _fuente!, color, new Rectangle(caja.X + 6, caja.Y + 3 + i * alto, ancho - 8, alto), _izquierda);
	}
}
