// Sustitutos de la API de ATAS para las pruebas de comportamiento: corren en Linux o Windows sin ATAS.
// Tienen las mismas firmas que la API real que usan los indicadores (comprobadas contra AtasPlatform/Indicators)
// más ganchos para alimentar velas, sesiones y volumen por precio, y para registrar lo que se dibuja.
// No se distribuyen: solo los compila pruebas/Pruebas.csproj.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace System.Windows.Media
{
	public struct Color
	{
		public byte A, R, G, B;

		public static Color FromArgb(byte a, byte r, byte g, byte b) => new() { A = a, R = r, G = g, B = b };
	}
}

namespace System.Drawing
{
	public enum FontStyle { Regular = 0, Bold = 1 }

	public enum StringAlignment { Near, Center, Far }
}

namespace OFT.Rendering
{
	public enum StdCursor { Default, Hand }
}

namespace OFT.Rendering.Control
{
	public class RenderControlMouseEventArgs
	{
		public RenderControlMouseEventArgs(int x, int y)
		{
			X = x;
			Y = y;
		}

		public int X { get; }
		public int Y { get; }
		public Point Location => new(X, Y);
	}
}

namespace OFT.Rendering.Tools
{
	public class RenderFont
	{
		public RenderFont(string family, float size) { }
		public RenderFont(string family, float size, FontStyle style) { }
	}

	public class RenderStringFormat
	{
		public StringAlignment Alignment { get; set; }
		public StringAlignment LineAlignment { get; set; }
	}

	public class RenderPen
	{
		public RenderPen(Color color) => Color = color;
		public RenderPen(Color color, float width) => (Color, Width) = (color, width);
		public Color Color { get; }
		public float Width { get; } = 1;
	}
}

namespace OFT.Rendering.Context
{
	using OFT.Rendering.Tools;

	public class RenderContext
	{
		/// <summary>Textos dibujados y flechas de los polígonos (▲ si el vértice está arriba), en orden.</summary>
		public List<string> Log { get; } = new();

		/// <summary>Textos dibujados con su rectángulo.</summary>
		public List<(string Text, Rectangle Rect)> Texts { get; } = new();

		public List<(RenderPen Pen, int Y)> HorizontalLines { get; } = new();

		public void FillRectangle(Color color, Rectangle rect) { }
		public void DrawRectangle(RenderPen pen, Rectangle rect) { }

		public void DrawString(string text, RenderFont font, Color color, Rectangle rect, RenderStringFormat format)
		{
			Log.Add(text);
			Texts.Add((text, rect));
		}

		public void FillPolygon(Color color, Point[] points) => Log.Add(points[0].Y < points[1].Y ? "▲" : "▼");

		public void DrawLine(RenderPen pen, int x1, int y1, int x2, int y2)
		{
			if (y1 == y2)
				HorizontalLines.Add((pen, y1));
		}

		public Size MeasureString(string text, RenderFont font) => new(text.Length * 7, 12);
	}
}

namespace ATAS.Indicators
{
	[Flags]
	public enum DrawingLayouts { None = 0, Historical = 1, LatestBar = 2, Final = 4 }

	public enum VisualMode { Line, Hide, Dots }

	public interface IDataSeries
	{
		bool IsHidden { get; set; }
	}

	public class ValueDataSeries : IDataSeries
	{
		public ValueDataSeries() { }
		public ValueDataSeries(string id, string name) => Name = name;

		public string Name { get; } = "";
		public bool IsHidden { get; set; }
		public VisualMode VisualType { get; set; }
		public bool ShowZeroValue { get; set; } = true;
		public int Width { get; set; }
		public System.Windows.Media.Color Color { get; set; }
		public Dictionary<int, decimal> Values { get; } = new();

		public decimal this[int bar]
		{
			get => Values.TryGetValue(bar, out var v) ? v : 0;
			set => Values[bar] = value;
		}
	}

	public class Candle
	{
		public decimal Open { get; set; }
		public decimal High { get; set; }
		public decimal Low { get; set; }
		public decimal Close { get; set; }
	}

	public class CandleDataSeries : IDataSeries
	{
		public CandleDataSeries(string id) { }
		public CandleDataSeries(string id, string name) { }

		public bool IsHidden { get; set; }
		public System.Windows.Media.Color UpCandleColor { get; set; }
		public System.Windows.Media.Color DownCandleColor { get; set; }
		public Dictionary<int, Candle> Stored { get; } = new();

		public Candle this[int bar]
		{
			get => Stored[bar];
			set => Stored[bar] = value;
		}
	}

	public static class IndicatorDataProvider
	{
		public const string NewPanel = "NewPanel";
	}

	public class PriceVolumeInfo
	{
		public decimal Price { get; set; }
		public decimal Volume { get; set; }
		public decimal Bid { get; set; }
		public decimal Ask { get; set; }
	}

	public class IndicatorCandle
	{
		public DateTime Time { get; set; }
		public decimal Open { get; set; }
		public decimal High { get; set; }
		public decimal Low { get; set; }
		public decimal Close { get; set; }
		public decimal Delta { get; set; }
		public decimal MaxDelta { get; set; }
		public decimal MinDelta { get; set; }
		public decimal Volume { get; set; }

		/// <summary>Gancho de prueba: Bid y Ask negociados en cada precio de la vela.</summary>
		public Dictionary<decimal, (decimal Bid, decimal Ask)> Levels { get; set; } = new();

		public PriceVolumeInfo? GetPriceVolumeInfo(decimal price) =>
			Levels.TryGetValue(price, out var l) ? new PriceVolumeInfo { Price = price, Bid = l.Bid, Ask = l.Ask, Volume = l.Bid + l.Ask } : null;

		public PriceVolumeInfo? MaxVolumePriceInfo =>
			Levels.Count == 0 ? null : GetPriceVolumeInfo(Levels.OrderByDescending(kv => kv.Value.Bid + kv.Value.Ask).ThenBy(kv => kv.Key).First().Key);
	}

	public class InstrumentInfo
	{
		public TimeSpan TimeZoneOffset { get; set; }
		public decimal TickSize { get; set; }
	}

	public class PriceChartContainer
	{
		// double a propósito (en los sustitutos de compilación es decimal): el código debe compilar con cualquiera.
		public double BarsWidth { get; set; }
		public double BarSpacing { get; set; }
		public Rectangle Region { get; set; } = new(0, 0, 1200, 800);
	}

	public class ChartInfo
	{
		public string ChartType { get; set; } = "TimeFrame";
		public PriceChartContainer PriceChartContainer { get; set; } = new();
		public int GetXByBar(int bar) => bar * 50;
		public int GetYByPrice(decimal price, bool b) => TestYByPrice(price);

		/// <summary>Gancho de prueba: de precio a píxel (por defecto, 100 cae en y = 1000, fuera de un área de 800).</summary>
		public Func<decimal, int> TestYByPrice { get; set; } = price => (int)(2000 - price * 10);
	}

	public abstract class Indicator
	{
		protected Indicator(bool useCandles) { }

		public IList<IDataSeries> DataSeries { get; } = new List<IDataSeries> { new ValueDataSeries() };
		public bool DenyToChangePanel { get; set; }
		public bool EnableCustomDrawing { get; set; }
		public bool DrawAbovePrice { get; set; }
		public string Panel { get; set; } = "";
		public InstrumentInfo? InstrumentInfo { get; set; }
		public ChartInfo? ChartInfo { get; set; }
		public int FirstVisibleBarNumber { get; set; }
		public int LastVisibleBarNumber { get; set; }
		public int CurrentBar => TestCandles.Count;

		protected void SubscribeToDrawingEvents(DrawingLayouts layouts) { }
		public void RecalculateValues() { }
		public void RedrawChart() => Redraws++;
		public int Redraws { get; private set; }

		protected abstract void OnCalculate(int bar, decimal value);
		protected virtual void OnRender(OFT.Rendering.Context.RenderContext context, DrawingLayouts layout) { }

		public virtual bool ProcessMouseDown(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
		public virtual bool ProcessMouseMove(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
		public virtual bool ProcessMouseUp(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
		public virtual OFT.Rendering.StdCursor GetCursor(OFT.Rendering.Control.RenderControlMouseEventArgs e) => OFT.Rendering.StdCursor.Default;

		// Ganchos de prueba.
		public List<IndicatorCandle> TestCandles { get; set; } = new();
		public HashSet<int> TestSessions { get; set; } = new();
		protected bool IsNewSession(int bar) => TestSessions.Contains(bar);
		public IndicatorCandle GetCandle(int bar) => TestCandles[bar];
		public void TestCalc(int bar) => OnCalculate(bar, 0);
		public void TestRender(OFT.Rendering.Context.RenderContext c) => OnRender(c, DrawingLayouts.Final);
	}
}

namespace ATAS.Indicators.Drawing
{
	public class TrendLine { }
}
