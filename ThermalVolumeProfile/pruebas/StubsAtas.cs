// Sustitutos de la API de ATAS para las pruebas de comportamiento: corren en Linux o Windows sin ATAS.
// Mismas firmas que la API real que usa el indicador (comprobadas contra AtasPlatform/Indicators) más ganchos para
// alimentar velas con volumen por precio y para registrar lo que se dibuja y las alertas. No se distribuyen.

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
		public RenderControlMouseEventArgs(int x, int y) => (X, Y) = (x, y);

		public int X { get; }
		public int Y { get; }
		public Point Location => new(X, Y);
	}
}

namespace OFT.Rendering.Tools
{
	public class RenderFont
	{
		public RenderFont(string family, float size) => Size = size;
		public RenderFont(string family, float size, FontStyle style) => Size = size;
		public float Size { get; }
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
		public List<(Color Color, Rectangle Rect)> Rects { get; } = new();
		public List<(RenderPen Pen, int X1, int Y1, int X2, int Y2)> Lines { get; } = new();
		public List<(string Text, Rectangle Rect, Color Color)> Texts { get; } = new();

		public void FillRectangle(Color color, Rectangle rect) => Rects.Add((color, rect));
		public void DrawRectangle(RenderPen pen, Rectangle rect) { }
		public void DrawString(string text, RenderFont font, Color color, Rectangle rect, RenderStringFormat format) => Texts.Add((text, rect, color));
		public void DrawLine(RenderPen pen, int x1, int y1, int x2, int y2) => Lines.Add((pen, x1, y1, x2, y2));
		public Size MeasureString(string text, RenderFont font) => new(text.Length * 7, 12);
	}
}

namespace OFT.Attributes
{
	internal static class Vacio { }
}

namespace OFT.Attributes.Editors
{
	[AttributeUsage(AttributeTargets.Property)] public sealed class SoundComboBoxEditorAttribute : Attribute { }

	public enum MaskTypes { Regular, DateTime, DateTimeAdvancingCaret }

	[AttributeUsage(AttributeTargets.Property)] public sealed class MaskAttribute : Attribute { public MaskAttribute(MaskTypes type, string mask) { } }
}

namespace Utils.Common.Logging
{
	public static class Registro
	{
		public static List<string> Mensajes { get; } = new();

		public static void LogError(this object origen, string mensaje, Exception error) => Mensajes.Add("ERROR " + mensaje + ": " + error);
		public static void LogWarn(this object origen, string mensaje) => Mensajes.Add("WARN " + mensaje);
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
		public bool IsHidden { get; set; }
		public VisualMode VisualType { get; set; }
	}

	public static class IndicatorDataProvider
	{
		public const string CandlesPanel = "CandlesPanel";
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
		public decimal Volume { get; set; }
		public decimal Delta { get; set; }

		/// <summary>Gancho de prueba: Bid y Ask negociados en cada precio de la vela.</summary>
		public Dictionary<decimal, (decimal Bid, decimal Ask)> Levels { get; set; } = new();

		/// <summary>Gancho de prueba: veces que se leyó el volumen por precio de esta vela.</summary>
		public int Lecturas { get; private set; }

		public IEnumerable<PriceVolumeInfo> GetAllPriceLevels()
		{
			Lecturas++;
			return Levels.Select(kv => new PriceVolumeInfo { Price = kv.Key, Bid = kv.Value.Bid, Ask = kv.Value.Ask, Volume = kv.Value.Bid + kv.Value.Ask }).ToList();
		}
	}

	public class InstrumentInfo
	{
		public TimeSpan TimeZoneOffset { get; set; }
		public decimal TickSize { get; set; }
		public string Instrument { get; set; } = "NQZ6";
	}

	public class PriceChartContainer
	{
		// double a propósito (en los sustitutos de compilación es decimal): el código debe compilar con cualquiera.
		public double BarsWidth { get; set; } = 6;
		public Rectangle Region { get; set; } = new(0, 0, 1200, 800);
	}

	public class ChartInfo
	{
		public PriceChartContainer PriceChartContainer { get; set; } = new();

		/// <summary>Gancho de prueba: x de cada vela (por defecto 10 píxeles por vela).</summary>
		public Func<int, int> TestXByBar { get; set; } = bar => bar * 10;

		/// <summary>Gancho de prueba: de precio a píxel (por defecto 4 píxeles por punto, 15500 arriba en y = 0).</summary>
		public Func<decimal, int> TestYByPrice { get; set; } = precio => (int)Math.Round((15500 - precio) * 4);

		public int GetXByBar(int bar) => TestXByBar(bar);
		public int GetXByBar(int bar, bool b) => TestXByBar(bar);
		public int GetYByPrice(decimal price, bool b) => TestYByPrice(price);
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
		public void RecalculateValues() => Recalculos++;
		public void RedrawChart() => Redibujos++;
		public void AddAlert(string alertFile, string message) => Alertas.Add($"{alertFile}|{message}");

		protected abstract void OnCalculate(int bar, decimal value);
		protected virtual void OnRender(OFT.Rendering.Context.RenderContext context, DrawingLayouts layout) { }

		public virtual bool ProcessMouseDown(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
		public virtual bool ProcessMouseMove(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
		public virtual bool ProcessMouseUp(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
		public virtual OFT.Rendering.StdCursor GetCursor(OFT.Rendering.Control.RenderControlMouseEventArgs e) => OFT.Rendering.StdCursor.Default;

		// Ganchos de prueba.
		public List<IndicatorCandle> TestCandles { get; set; } = new();
		public List<string> Alertas { get; } = new();
		public int Recalculos { get; private set; }
		public int Redibujos { get; private set; }
		public IndicatorCandle GetCandle(int bar) => TestCandles[bar];
		public void TestCalc(int bar) => OnCalculate(bar, 0);

		/// <summary>Lo que hace ATAS al cargar o recalcular: OnCalculate de la vela 0 a la última.</summary>
		public void TestCalcAll()
		{
			for (var i = 0; i < TestCandles.Count; i++)
				OnCalculate(i, 0);
		}

		public OFT.Rendering.Context.RenderContext TestRender()
		{
			var c = new OFT.Rendering.Context.RenderContext();
			OnRender(c, DrawingLayouts.Final);
			return c;
		}
	}
}
