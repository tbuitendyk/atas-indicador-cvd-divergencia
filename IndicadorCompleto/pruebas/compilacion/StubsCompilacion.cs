// Sustitutos de la API de ATAS SOLO para comprobar que la solución compila sin ATAS (pruebas/verificar.sh).
// Cada miembro reproduce una llamada comprobada en el código oficial de AtasPlatform/Indicators. No se distribuyen.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ATAS.Indicators
{
    [Flags] public enum DrawingLayouts { None = 0, Historical = 1, LatestBar = 2, Final = 4 }
    public enum VisualMode { Line, Hide, Dots }
    public interface IDataSeries { bool IsHidden { get; set; } }
    public class ValueDataSeries : IDataSeries
    {
        public ValueDataSeries() { } public ValueDataSeries(string id, string name) { }
        public bool IsHidden { get; set; } public VisualMode VisualType { get; set; } public bool ShowZeroValue { get; set; } public int Width { get; set; }
        public System.Windows.Media.Color Color { get; set; }
        public decimal this[int bar] { get => 0; set { } }
    }
    public class PriceVolumeInfo { public decimal Price { get; set; } public decimal Volume { get; set; } public decimal Bid { get; set; } public decimal Ask { get; set; } }
    public class IndicatorCandle
    {
        public DateTime Time { get; set; } public DateTime LastTime { get; set; }
        public decimal Open { get; set; } public decimal High { get; set; } public decimal Low { get; set; } public decimal Close { get; set; }
        public decimal Delta { get; set; } public decimal MaxDelta { get; set; } public decimal MinDelta { get; set; } public decimal Volume { get; set; }
        public PriceVolumeInfo GetPriceVolumeInfo(decimal price) => null;
        public PriceVolumeInfo MaxVolumePriceInfo { get; set; }
    }
    public class Candle { public decimal Open { get; set; } public decimal High { get; set; } public decimal Low { get; set; } public decimal Close { get; set; } }
    public class CandleDataSeries : IDataSeries
    {
        public CandleDataSeries(string id) { } public CandleDataSeries(string id, string name) { }
        public bool IsHidden { get; set; }
        public System.Windows.Media.Color UpCandleColor { get; set; } public System.Windows.Media.Color DownCandleColor { get; set; }
        public Candle this[int bar] { get => null; set { } }
    }
    public class IndicatorContainer { public Rectangle Region { get; set; } }
    public static class IndicatorDataProvider { public const string NewPanel = "NewPanel"; public const string CandlesPanel = "CandlesPanel"; }
    public class InstrumentInfo { public TimeSpan TimeZoneOffset { get; set; } public decimal TickSize { get; set; } public string Instrument { get; set; } }
    public class PriceChartContainer { public decimal BarsWidth { get; set; } public decimal BarSpacing { get; set; } public Rectangle Region { get; set; } }
    public class ChartInfo
    {
        public string ChartType { get; set; }
        public string TimeFrame { get; set; }
        public PriceChartContainer PriceChartContainer { get; set; }
        public int GetXByBar(int bar) => 0; public int GetXByBar(int bar, bool b) => 0;
        public int GetYByPrice(decimal price) => 0; public int GetYByPrice(decimal price, bool b) => 0;
    }
    public abstract class Indicator
    {
        protected Indicator(bool useCandles) { }
        public IList<IDataSeries> DataSeries { get; } = new List<IDataSeries> { new ValueDataSeries() };
        public bool DenyToChangePanel { get; set; }
        public bool EnableCustomDrawing { get; set; }
        public bool DrawAbovePrice { get; set; }
        public string Panel { get; set; }
        public InstrumentInfo InstrumentInfo { get; set; }
        public ChartInfo ChartInfo { get; set; }
        public int FirstVisibleBarNumber { get; set; }
        public int LastVisibleBarNumber { get; set; }
        public int CurrentBar { get; set; }
        protected void SubscribeToDrawingEvents(DrawingLayouts layouts) { }
        protected bool IsNewSession(int bar) => false;
        public IndicatorCandle GetCandle(int bar) => new IndicatorCandle();
        public void RecalculateValues() { }
        protected abstract void OnCalculate(int bar, decimal value);
        protected virtual void OnDispose() { }
        public IndicatorContainer Container { get; set; }
        public virtual bool ProcessMouseDown(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
        public virtual bool ProcessMouseMove(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
        public virtual bool ProcessMouseUp(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
        public virtual OFT.Rendering.StdCursor GetCursor(OFT.Rendering.Control.RenderControlMouseEventArgs e) => OFT.Rendering.StdCursor.Default;
        public int Redraws { get; private set; }
        public void RedrawChart() => Redraws++;
        protected virtual void OnRender(OFT.Rendering.Context.RenderContext context, DrawingLayouts layout) { }
    }
}
namespace ATAS.Indicators.Drawing { public class TrendLine { } }
namespace OFT.Rendering.Context
{
    using OFT.Rendering.Tools;
    public class RenderContext
    {
        public void FillRectangle(Color color, Rectangle rect) { }
        public void DrawRectangle(RenderPen pen, Rectangle rect) { }
        public void DrawString(string text, RenderFont font, Color color, Rectangle rect, RenderStringFormat format) { }
        public void DrawString(string text, RenderFont font, Color color, Rectangle rect) { }
        public void DrawString(string text, RenderFont font, Color color, int x, int y) { }
        public void FillPolygon(Color color, Point[] points) { }
        public void DrawLine(RenderPen pen, int x1, int y1, int x2, int y2) { }
        public Size MeasureString(string text, RenderFont font) => default;
    }
}
namespace OFT.Rendering.Tools
{
    public class RenderFont { public RenderFont(string family, float size) { } public RenderFont(string family, float size, FontStyle style) { } }
    public class RenderStringFormat { public StringAlignment Alignment { get; set; } public StringAlignment LineAlignment { get; set; } }
    public class RenderPen { public RenderPen(Color color) { } public RenderPen(Color color, float width) { } }
}

namespace OFT.Rendering { public enum StdCursor { Default, Hand } }
namespace OFT.Rendering.Control
{
    public class RenderControlMouseEventArgs
    {
        public RenderControlMouseEventArgs(int x, int y) { X = x; Y = y; }
        public int X { get; } public int Y { get; }
        public System.Drawing.Point Location => new(X, Y);
    }
}

namespace Utils.Common.Logging { public static class Registro { public static void LogError(this object origen, string mensaje, System.Exception error) { } } }
