namespace OrderFlowSuite.Atas;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using OrderFlowSuite.Nucleo;

/// <summary>Lo que el indicador sabe del gráfico al dibujar (sin tipos de ATAS: el indicador lo rellena).</summary>
public sealed class Lienzo
{
	public Func<int, int> X { get; init; } = _ => 0;
	public Func<decimal, int> Y { get; init; } = _ => 0;
	public Rectangle Area { get; init; }
	public int AnchoVela { get; init; } = 1;
	public int Primera { get; init; }
	public int Ultima { get; init; }
	public int UltimaBarra { get; init; }
	public decimal Tick { get; init; } = 0.25m;
	public ModoVisual Modo { get; init; }

	public bool Visible(int desde, int hasta) => hasta >= Primera && desde <= Ultima;

	public bool Completo => Modo == ModoVisual.Full;
}

/// <summary>
/// Marcadores de cada motor en el gráfico (modos Markers y Full). Los datos se copian bajo el cerrojo de la suite y
/// se dibujan fuera de él; las plumas se reutilizan (nada de "new" en los bucles de dibujo).
/// </summary>
public static class DibujoSuite
{
	private static readonly Dictionary<(int Argb, float Ancho), RenderPen> Plumas = new();

	public static RenderPen Pluma(Color color, float ancho = 1)
	{
		lock (Plumas)
		{
			if (!Plumas.TryGetValue((color.ToArgb(), ancho), out var p))
				Plumas[(color.ToArgb(), ancho)] = p = new RenderPen(color, ancho);

			return p;
		}
	}

	private static string N(decimal v) => v.ToString("0", CultureInfo.InvariantCulture);

	/// <summary>Barra de una hora; si el evento es anterior al gráfico, la primera barra (si termina dentro).</summary>
	private static int Barra(CarcasaSuite c, DateTime utc) => Math.Max(0, c.Barra(utc));

	#region Imbalance

	public static void Zonas(RenderContext context, CarcasaSuite c, Lienzo l)
	{
		List<(DateTime Desde, DateTime? Hasta, int Velas, decimal Bajo, decimal Alto, Lado Lado, string Etiqueta, bool Gris)> zonas;

		lock (c.Suite.Cerrojo)
		{
			zonas = c.Suite.Imbalance.Zonas.Where(z => l.Completo || z.Activa)
				.Select(z => (z.InicioUtc, z.FinUtc, 0, z.Bajo, z.Alto, z.Lado, $"IMB {z.Niveles} niv {z.Estado}{(z.ConIceberg ? " +ICE" : "")}", z.Estado == "ROTA"))
				.ToList();

			if (l.Completo)
			{
				zonas.AddRange(c.Suite.Eventos.Todos.Where(e => e.Grabado && e.Tipo == "StackedImb")
					.Select(e => (e.Utc, (DateTime?)null, c.Cfg.Imbalance.ZoneExtendBars, e.Precio, e.PrecioFin ?? e.Precio, e.Lado, "IMB (CSV)", true)));
			}
		}

		var medio = l.Tick / 2;

		foreach (var z in zonas)
		{
			var b1 = Barra(c, z.Desde);
			var b2 = z.Velas > 0 ? Math.Min(l.UltimaBarra, b1 + z.Velas) : z.Hasta is { } h ? Barra(c, h) : l.UltimaBarra;

			if (!l.Visible(b1, b2))
				continue;

			var color = z.Gris ? CarcasaSuite.Gris : CarcasaSuite.ColorLado(z.Lado);
			var y1 = l.Y(z.Alto + medio);
			var y2 = l.Y(z.Bajo - medio);
			var caja = new Rectangle(l.X(b1), Math.Min(y1, y2), Math.Max(2, l.X(b2) + l.AnchoVela - l.X(b1)), Math.Max(2, Math.Abs(y2 - y1)));

			if (l.Completo)
				context.FillRectangle(CarcasaSuite.Transparente(color, 45), caja);

			context.DrawRectangle(Pluma(CarcasaSuite.Transparente(color, 200)), caja);

			if (l.Completo)
				c.Etiqueta(context, z.Etiqueta, caja.Right + 3, caja.Top + caja.Height / 2, color);
		}
	}

	#endregion

	#region Icebergs

	public static void Icebergs(RenderContext context, CarcasaSuite c, Lienzo l)
	{
		List<(DateTime Desde, DateTime Hasta, bool Vivo, decimal Precio, Lado Lado, string Etiqueta, bool Gris)> lista;

		lock (c.Suite.Cerrojo)
		{
			lista = c.Suite.Icebergs.Icebergs.Select(i => (i.InicioUtc, i.UltimaActividadUtc, i.Vivo, i.Precio, i.Lado,
					l.Completo
						? $"ICEBERG{(i.AltaProbabilidad ? "*" : "")} {N(i.Ejecutado)}c {i.Recargas}r {i.Estado} {i.Sesgo}"
						: $"I{(i.AltaProbabilidad ? "*" : "")} {N(i.Ejecutado)}c",
					!i.Vivo))
				.ToList();

			lista.AddRange(c.Suite.Eventos.Todos.Where(e => e.Grabado && e.Tipo == "Iceberg")
				.Select(e => (e.Utc, e.CambioUtc, false, e.Precio, e.Lado, $"I{(e.AltaProbabilidad ? "*" : "")} {N(e.Volumen)}c (CSV)", true)));
		}

		foreach (var i in lista)
		{
			var b1 = Barra(c, i.Desde);
			var b2 = i.Vivo ? l.UltimaBarra : Barra(c, i.Hasta);

			if (!l.Visible(b1, b2))
				continue;

			var color = i.Gris ? CarcasaSuite.Gris : CarcasaSuite.ColorLado(i.Lado);
			var y = l.Y(i.Precio);
			var x2 = Math.Max(l.X(b1) + 6, l.X(b2) + l.AnchoVela);
			context.DrawLine(Pluma(color, l.Completo ? 3 : 2), l.X(b1), y, x2, y);
			c.Etiqueta(context, i.Etiqueta, x2 + 4, y, color);
		}
	}

	#endregion

	#region Stops

	public static void Stops(RenderContext context, CarcasaSuite c, Lienzo l)
	{
		List<(DateTime Utc, decimal Extremo, Lado Lado, string Etiqueta, bool Gris)> cascadas;
		List<NivelReferencia> zonas = new();

		lock (c.Suite.Cerrojo)
		{
			cascadas = c.Suite.Stops.Cascadas.Select(s => (s.Utc, s.Lado == Lado.Venta ? s.Min : s.Max, s.Lado,
					l.Completo ? $"STOPS {N(s.Volumen)}c {s.Zona?.Nombre ?? ""} {s.Estado}" : $"S {N(s.Volumen)}c", false))
				.ToList();

			cascadas.AddRange(c.Suite.Eventos.Todos.Where(e => e.Grabado && e.Tipo == "StopRun")
				.Select(e => (e.Utc, e.PrecioFin ?? e.Precio, e.Lado, $"S {N(e.Volumen)}c (CSV)", true)));

			if (l.Completo && c.Cfg.Stops.ShowProbableZones && c.Suite.Stops.Zonas?.Invoke() is { } z)
				zonas = z.ToList();
		}

		// Zonas probables (modo Full): líneas discontinuas con su nombre a la derecha.
		foreach (var z in zonas)
		{
			var y = l.Y(z.Precio);

			if (y < l.Area.Top || y > l.Area.Bottom)
				continue;

			CarcasaSuite.Discontinua(context, Pluma(CarcasaSuite.Transparente(CarcasaSuite.Oro, 150)), l.Area.Left, l.Area.Right - 70, y);
			c.Etiqueta(context, z.Nombre.EndsWith("R", StringComparison.Ordinal) ? z.Nombre : $"{z.Nombre} {Textos.Precio(z.Precio, l.Tick)}", l.Area.Right - 160, y, CarcasaSuite.Oro);
		}

		foreach (var s in cascadas)
		{
			var b = Barra(c, s.Utc);

			if (!l.Visible(b, b))
				continue;

			var color = s.Gris ? CarcasaSuite.Gris : CarcasaSuite.ColorLado(s.Lado);
			var x = l.X(b) + l.AnchoVela / 2;
			var y = l.Y(s.Extremo);

			// Ventas: triángulo hacia abajo bajo el mínimo de la cascada; compras: hacia arriba sobre el máximo.
			CarcasaSuite.Flecha(context, x, s.Lado == Lado.Venta ? y + 4 : y - 4, s.Lado == Lado.Compra, color, 5);
			c.Etiqueta(context, s.Etiqueta, x + 8, s.Lado == Lado.Venta ? y + 12 : y - 12, color);
		}
	}

	#endregion

	#region Barridos

	public static void Barridos(RenderContext context, CarcasaSuite c, Lienzo l)
	{
		List<(DateTime Utc, decimal Desde, decimal Hasta, Lado Lado, string Etiqueta, decimal? Nivel, bool Gris)> barridos;

		lock (c.Suite.Cerrojo)
		{
			barridos = c.Suite.Barridos.Barridos.Select(b => (b.Utc, b.Desde, b.Hasta, b.Lado,
					l.Completo ? $"BARRIDO {b.Tipo} {N(b.Volumen)}c" : $"B {N(b.Volumen)}c",
					b.ContraIceberg?.Precio ?? b.Nivel?.Precio, false))
				.ToList();

			barridos.AddRange(c.Suite.Eventos.Todos.Where(e => e.Grabado && e.Tipo == "Sweep")
				.Select(e => (e.Utc, e.Precio, e.PrecioFin ?? e.Precio, e.Lado, $"B {N(e.Volumen)}c (CSV)", (decimal?)null, true)));
		}

		foreach (var b in barridos)
		{
			var barra = Barra(c, b.Utc);

			if (!l.Visible(barra, barra))
				continue;

			var color = b.Gris ? CarcasaSuite.Gris : CarcasaSuite.ColorLado(b.Lado);
			var x = l.X(barra) + l.AnchoVela / 2;
			context.DrawLine(Pluma(color, 3), x, l.Y(b.Desde), x, l.Y(b.Hasta));
			c.Etiqueta(context, b.Etiqueta, x + 8, l.Y(b.Hasta), color);

			// Full: el nivel barrido (o el iceberg que lo frenó), 20 velas.
			if (l.Completo && b.Nivel is { } nivel)
				CarcasaSuite.Discontinua(context, Pluma(color), x, l.X(Math.Min(l.UltimaBarra, barra + 20)), l.Y(nivel));
		}
	}

	#endregion

	#region Señales del Absorption Master

	public static void Senales(RenderContext context, CarcasaSuite c, Lienzo l)
	{
		List<EventoFlujo> senales;
		EventoFlujo? ultima;

		lock (c.Suite.Cerrojo)
		{
			senales = c.Suite.Eventos.Todos.Where(e => e.Tipo == "Signal").Select(RegistroEventos.Copia).ToList();
			ultima = c.Suite.Master.EventoUltimaSenal is { } u ? RegistroEventos.Copia(u) : null;
		}

		// La flecha sale en todos los modos (también en Minimal).
		foreach (var s in senales)
		{
			var b = Barra(c, s.Utc);

			if (!l.Visible(b, b))
				continue;

			var color = s.Grabado ? CarcasaSuite.Gris : CarcasaSuite.ColorLado(s.Lado);
			var x = l.X(b) + l.AnchoVela / 2;
			var y = l.Y(s.StopLoss ?? s.Precio);
			var largo = s.Lado == Lado.Compra;
			CarcasaSuite.Flecha(context, x, largo ? y + 6 : y - 6, largo, color, 7);
			c.Etiqueta(context, $"*{s.Score}", x + 10, largo ? y + 18 : y - 18, color);
		}

		// Markers y Full: entrada, stop y objetivo de la última señal (30 velas).
		if (l.Modo == ModoVisual.Minimal || ultima is null)
			return;

		var b1 = Barra(c, ultima.Utc);
		var b2 = Math.Min(l.UltimaBarra + 5, b1 + 30);

		if (!l.Visible(b1, b2))
			return;

		var x1 = l.X(b1);
		var x2 = l.X(b2);

		void Linea(decimal? precio, Color color, string texto)
		{
			if (precio is not { } p)
				return;

			var y = l.Y(p);
			context.DrawLine(Pluma(color, 2), x1, y, x2, y);
			c.Etiqueta(context, $"{texto} {Textos.Precio(p, l.Tick)}", x2 + 4, y, color);
		}

		Linea(ultima.Precio, CarcasaSuite.Oro, "Entrada");
		Linea(ultima.StopLoss, CarcasaSuite.Rojo, "SL");
		Linea(ultima.PrecioFin, CarcasaSuite.Verde, "TP");
	}

	#endregion
}
