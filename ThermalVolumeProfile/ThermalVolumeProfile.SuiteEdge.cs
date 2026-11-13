// Solo dentro de SuiteOrderFlowStacker (símbolo SUITE): en el ThermalVolumeProfile por separado este archivo queda vacío.
#if SUITE
namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using OrderFlowSuite.Comun;

using Termico;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// EDGE v3 (8): calculados en el perfil fino con parámetros en ticks, dibujo separado del cálculo (línea, franja o las
/// dos; grosor, discontinua, opacidad, solo los N más cercanos), TECHO HVN / PISO HVN, estados con primer toque, toques
/// repetidos y ruptura como eventos distintos, EDGE manuales con el botón EDGE y publicación EdgeZone para Sessions Sweeps.
/// </summary>
public partial class ThermalVolumeProfileLVN
{
	private const string GEdge = Pref + "7. EDGE";

	private readonly SeguidorLvn _seguidorEdge = new();
	private readonly Dictionary<long, EdgeZone> _edgesPublicados = new();
	private string _manualEdges = "";

	[Display(Name = "Detectar en el perfil fino (ticks)", GroupName = GEdge, Order = 200,
		Description = "Con los parámetros en ticks del instrumento: el resultado no cambia con el agrupamiento del dibujo.")]
	public bool EdgeEnPerfilFino { get => _p.EdgeEnPerfilFino; set { _p.EdgeEnPerfilFino = value; Recalcular(); } }

	[Display(Name = "Ventana W (ticks)", GroupName = GEdge, Order = 210)]
	[Range(1, 200)]
	public int EdgeWindowTicks { get => _p.EdgeWindowTicks; set { _p.EdgeWindowTicks = Math.Clamp(value, 1, 200); Recalcular(); } }

	[Display(Name = "Confirmación K (ticks)", GroupName = GEdge, Order = 220)]
	[Range(1, 200)]
	public int EdgeConfirmTicks { get => _p.EdgeConfirmTicks; set { _p.EdgeConfirmTicks = Math.Clamp(value, 1, 200); Recalcular(); } }

	[Display(Name = "Suavizado (sigma, ticks)", GroupName = GEdge, Order = 230)]
	[Range(0, 50)]
	public decimal EdgeSmoothSigmaTicks { get => (decimal)_p.EdgeSmoothSigmaTicks; set { _p.EdgeSmoothSigmaTicks = (double)Math.Clamp(value, 0, 50); Recalcular(); } }

	[Display(Name = "Mostrar líneas EDGE", GroupName = GEdge, Order = 240, Description = "Solo el dibujo: el cálculo y la publicación siguen aunque esté apagado.")]
	public bool ShowEdgeLines { get; set; } = true;

	[Display(Name = "Dibujo del EDGE", GroupName = GEdge, Order = 250)]
	public DibujoEdge EdgeDisplay { get; set; } = DibujoEdge.LineaYFranja;

	[Display(Name = "Grosor de la línea", GroupName = GEdge, Order = 260)]
	[Range(1, 10)]
	public int EdgeLineWidth { get; set; } = 2;

	[Display(Name = "Línea discontinua", GroupName = GEdge, Order = 270)]
	public bool EdgeDashed { get; set; }

	[Display(Name = "Franja (ticks)", GroupName = GEdge, Order = 280, Description = "Ancho de la franja y de la zona del EDGE, en ticks del instrumento.")]
	[Range(0, 200)]
	public int EdgeBandTicks { get; set; } = 4;

	[Display(Name = "Opacidad de la franja (%)", GroupName = GEdge, Order = 290)]
	[Range(0, 100)]
	public int EdgeBandOpacity { get; set; } = 45;

	[Display(Name = "Solo los N más cercanos (0 = todos)", GroupName = GEdge, Order = 300)]
	[Range(0, 50)]
	public int EdgeNearestOnly { get; set; }

	[Display(Name = "EDGE manuales", GroupName = GEdge, Order = 310, Description = "Precio|Lado|Instrumento|RangeId|HoraUtc; se marcan con el botón EDGE.")]
	public string ManualEdges { get => _manualEdges; set { _manualEdges = value ?? ""; Recalcular(); } }

	private sealed record EdgeManual(decimal Precio, string Lado, string Instrumento, long RangoId, DateTime HoraUtc);

	private List<EdgeManual> EdgesManuales()
	{
		var r = new List<EdgeManual>();

		foreach (var parte in _manualEdges.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var c = parte.Split('|');

			if (c.Length >= 5 && decimal.TryParse(c[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var p) &&
			    long.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rango))
			{
				var hora = DateTime.TryParse(c[4], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var h) ? h : DateTime.UtcNow;
				r.Add(new EdgeManual(p, c[1] == "PISO_HVN" ? "PISO_HVN" : "TECHO_HVN", c[2], rango, DateTime.SpecifyKind(hora, DateTimeKind.Utc)));
			}
		}

		return r;
	}

	private void GuardarEdges(IEnumerable<EdgeManual> edges)
	{
		_manualEdges = string.Join(";", edges.Select(e => string.Join("|", e.Precio.ToString(CultureInfo.InvariantCulture), e.Lado, e.Instrumento,
			e.RangoId.ToString(CultureInfo.InvariantCulture), e.HoraUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))));
		Recalcular();
	}

	/// <summary>Botón EDGE: clic = EDGE manual (lado por el perfil fino); clic sobre uno visible = quitarlo.</summary>
	private void SuiteEdgeManual(Point p)
	{
		if (InstrumentInfo is not { TickSize: > 0 })
			return;

		var todos = EdgesManuales();
		var area = Area;
		var cercano = todos.Where(e => e.Instrumento == Instrumento).Select(e => (e, y: Y(e.Precio)))
			.Where(x => x.y >= area.Top && x.y <= area.Bottom && Math.Abs(x.y - p.Y) <= 5).OrderBy(x => Math.Abs(x.y - p.Y)).Select(x => x.e).FirstOrDefault();

		if (cercano is not null)
		{
			todos.Remove(cercano);
		}
		else
		{
			var precio = PrecioDeY(p.Y);
			Perfil? fino;

			lock (_sync)
				fino = _resultado?.Fino;

			todos.Add(new EdgeManual(precio, fino is null ? "TECHO_HVN" : MotorTermico.LadoEdge(fino, precio, _p.EdgeWindowTicks), Instrumento, _rangoId, DateTime.UtcNow));
		}

		GuardarEdges(todos);
	}

	/// <summary>EDGE automáticos (perfil fino si lo hay) y manuales como zonas para el estabilizador.</summary>
	private List<LvnTermico> EdgesComoZonas(ResultadoTermico r)
	{
		var mitad = EdgeBandTicks * (InstrumentInfo?.TickSize ?? 0.25m) / 2;
		var auto = (r.EdgesFino.Count > 0 || _p.EdgeEnPerfilFino ? r.EdgesFino : r.Edges)
			.Select(e => new LvnTermico { Precio = e.Precio, Bajo = e.Precio - mitad, Alto = e.Precio + mitad, Score100 = Math.Clamp((int)Math.Round(e.Score * 100), 0, 100), Metodo = e.Lado });
		var manuales = EdgesManuales().Where(e => e.Instrumento == Instrumento)
			.Select(e => new LvnTermico { Precio = e.Precio, Bajo = e.Precio - mitad, Alto = e.Precio + mitad, Manual = true, Metodo = e.Lado });
		return auto.Concat(manuales).ToList();
	}

	private void SuitePublicarEdges(ResultadoTermico r, bool cerrada, DateTime ahora, int confirmaciones)
	{
		_seguidorEdge.Actualizar(_p.EnableEdge ? EdgesComoZonas(r) : new List<LvnTermico>(), cerrada, ahora, confirmaciones, LvnHysteresisPct);
		var actuales = new Dictionary<long, EdgeZone>();

		foreach (var z in _seguidorEdge.Zonas)
		{
			var id = (_rangoId * 1_000_003 + z.Id * 2 + (z.Manual ? 1 : 0)) & long.MaxValue;
			var previo = _edgesPublicados.GetValueOrDefault(id);
			var igual = previo is not null && previo.Precio == z.Centro && previo.Estado == z.Estado && previo.Toques == z.Toques && previo.Provisional == z.Provisional;

			actuales[id] = igual
				? previo!
				: new EdgeZone
				{
					Id = id, Fuente = FuenteSuite.Thermal, Utc = previo?.Utc ?? ahora, CambioUtc = ahora, Version = (previo?.Version ?? 0) + 1, RangoId = _rangoId,
					Precio = z.Centro, Bajo = z.Bajo, Alto = z.Alto, Lado = z.LadoEdge.Length > 0 ? z.LadoEdge : EdgeZone.TechoHvn, Estado = z.Estado, Score = z.Score100,
					Manual = z.Manual, Toques = z.Toques, Provisional = z.Provisional
				};
		}

		var canal = SuiteEventBus.Para(Instrumento);

		foreach (var (id, z) in actuales)
		{
			if (!_edgesPublicados.TryGetValue(id, out var previo) || !ReferenceEquals(previo, z))
				canal.Publicar(z);
		}

		foreach (var (id, z) in _edgesPublicados)
		{
			if (!actuales.ContainsKey(id))
			{
				canal.Publicar(new EdgeZone
				{
					Id = z.Id, Fuente = FuenteSuite.Thermal, Utc = z.Utc, CambioUtc = ahora, Version = z.Version + 1, RangoId = z.RangoId, Precio = z.Precio,
					Bajo = z.Bajo, Alto = z.Alto, Lado = z.Lado, Estado = "BORRADO", Score = z.Score, Manual = z.Manual, Toques = z.Toques
				});
			}
		}

		_edgesPublicados.Clear();

		foreach (var (id, z) in actuales)
			_edgesPublicados[id] = z;
	}

	/// <summary>Dibujo de los EDGE (separado del cálculo): línea, franja o las dos; solo los N más cercanos si se pide.</summary>
	private void SuiteDibujarEdges(RenderContext context, int x1, int x2, Func<decimal, string> f)
	{
		if (!ShowEdgeLines || CurrentBar == 0)
			return;

		var cierre = GetCandle(CurrentBar - 1).Close;
		var zonas = _seguidorEdge.Zonas.ToList();

		if (EdgeNearestOnly > 0)
			zonas = zonas.OrderBy(z => Math.Abs(z.Centro - cierre)).Take(EdgeNearestOnly).ToList();

		var color = Color.FromArgb(255, EdgeColor.R, EdgeColor.G, EdgeColor.B);
		var pluma = new RenderPen(color, EdgeLineWidth);

		foreach (var z in zonas)
		{
			var y = Y(z.Centro);

			if (EdgeDisplay is DibujoEdge.Franja or DibujoEdge.LineaYFranja && EdgeBandTicks > 0)
			{
				var (ya, yb) = (Y(z.Alto), Y(z.Bajo));
				context.FillRectangle(Color.FromArgb(EdgeBandOpacity * 255 / 100, color), new Rectangle(x1, Math.Min(ya, yb), Math.Max(1, x2 - x1), Math.Max(1, Math.Abs(yb - ya))));
			}

			if (EdgeDisplay is DibujoEdge.Linea or DibujoEdge.LineaYFranja)
			{
				if (EdgeDashed)
				{
					for (var x = x1; x < x2; x += 10)
						context.DrawLine(pluma, x, y, Math.Min(x + 6, x2), y);
				}
				else
				{
					context.DrawLine(pluma, x1, y, x2, y);
				}
			}

			var lado = z.LadoEdge == "PISO_HVN" ? "PISO HVN" : "TECHO HVN";
			var estado = z.Provisional ? " PROVISIONAL" : z.Estado == "ACTIVO" ? "" : " " + z.Estado;
			Nota(EdgeLabels, y, $"EDGE {lado} {f(z.Centro)}{(z.Manual ? " MANUAL" : "")}{estado}", color);
		}
	}
}
#endif
