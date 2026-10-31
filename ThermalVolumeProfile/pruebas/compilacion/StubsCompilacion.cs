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
        public bool IsHidden { get; set; } public VisualMode VisualType { get; set; }
    }
    // HRanges.cs: Dictionary<int, IEnumerable<PriceVolumeInfo>> ... = GetCandle(i).GetAllPriceLevels(); .Price, .Volume
    public class PriceVolumeInfo { public decimal Price { get; set; } public decimal Volume { get; set; } public decimal Bid { get; set; } public decimal Ask { get; set; } }
    public class IndicatorCandle
    {
        public DateTime Time { get; set; }
        public decimal Open { get; set; } public decimal High { get; set; } public decimal Low { get; set; } public decimal Close { get; set; }
        public decimal Delta { get; set; } public decimal Volume { get; set; }
        public IEnumerable<PriceVolumeInfo> GetAllPriceLevels() => null;
    }
    public static class IndicatorDataProvider { public const string CandlesPanel = "CandlesPanel"; }
    public class InstrumentInfo { public TimeSpan TimeZoneOffset { get; set; } public decimal TickSize { get; set; } public string Instrument { get; set; } }
    public class PriceChartContainer { public decimal BarsWidth { get; set; } public Rectangle Region { get; set; } }
    public class ChartInfo
    {
        public PriceChartContainer PriceChartContainer { get; set; }
        public int GetXByBar(int bar) => 0; public int GetXByBar(int bar, bool b) => 0;
        public int GetYByPrice(decimal price, bool b) => 0;
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
        public IndicatorCandle GetCandle(int bar) => new IndicatorCandle();
        public void RecalculateValues() { }
        protected abstract void OnCalculate(int bar, decimal value);
        public virtual bool ProcessMouseDown(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
        public virtual bool ProcessMouseMove(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
        public virtual bool ProcessMouseUp(OFT.Rendering.Control.RenderControlMouseEventArgs e) => false;
        public virtual OFT.Rendering.StdCursor GetCursor(OFT.Rendering.Control.RenderControlMouseEventArgs e) => OFT.Rendering.StdCursor.Default;
        public void RedrawChart() { }
        protected virtual void OnRender(OFT.Rendering.Context.RenderContext context, DrawingLayouts layout) { }
        // UnfinishedAuction.cs: AddAlert(AlertFile, alertText)
        public void AddAlert(string alertFile, string message) { }
    }
}
namespace OFT.Rendering.Context
{
    using OFT.Rendering.Tools;
    public class RenderContext
    {
        public void FillRectangle(Color color, Rectangle rect) { }
        public void DrawRectangle(RenderPen pen, Rectangle rect) { }
        public void DrawString(string text, RenderFont font, Color color, Rectangle rect, RenderStringFormat format) { }
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
    public class RenderControlMouseEventArgs { public int X { get; } public int Y { get; } public System.Drawing.Point Location => new(X, Y); }
}
// BarTimer.cs: [OFT.Attributes.Editors.SoundComboBoxEditor]; Pivots.cs: [Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm:ss")];
// SampleProperties.cs: [Mask(MaskTypes.DateTime, "dd.MM.yyyy")]. Pivots.cs importa OFT.Attributes y OFT.Attributes.Editors.
namespace OFT.Attributes { internal static class Vacio { } }
namespace OFT.Attributes.Editors
{
    [AttributeUsage(AttributeTargets.Property)] public sealed class SoundComboBoxEditorAttribute : Attribute { }
    public enum MaskTypes { Regular, DateTime, DateTimeAdvancingCaret }
    [AttributeUsage(AttributeTargets.Property)] public sealed class MaskAttribute : Attribute { public MaskAttribute(MaskTypes type, string mask) { } }
}
// Pivots.cs: this.LogError(...); un indicador oficial: this.LogWarn(texto)
namespace Utils.Common.Logging
{
    public static class Registro
    {
        public static void LogError(this object origen, string mensaje, Exception error) { }
        public static void LogWarn(this object origen, string mensaje) { }
    }
}
