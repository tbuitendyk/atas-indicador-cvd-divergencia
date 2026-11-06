namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

public enum ReinicioCvd
{
	[Display(Name = "Día de futuros (18:00 NY)")]
	DiaFuturos,

	[Display(Name = "Apertura RTH (09:30 NY)")]
	Rth
}

/// <summary>
/// Session Sweeps CVD (anexo): el delta acumulado en velas (apertura = cierre anterior; máximo y mínimo con el delta
/// máximo y mínimo de cada vela) y sus divergencias con el precio, de máximo a máximo (bajista) y de mínimo a mínimo
/// (alcista). MACRO con pivotes de N velas a cada lado; MICRO, más sensible, solo dentro del tramo de una macro, para
/// afinar el punto de giro. Cada divergencia es un tramo recto entre sus dos pivotes: sin líneas verticales.
/// </summary>
[DisplayName("Session Sweeps CVD (Suite)")]
[Description("CVD en velas con divergencias macro y micro (máximo a máximo, mínimo a mínimo) para Session Sweeps Pro. Valores iniciales para el NQ en la sesión de Nueva York.")]
public class SessionSweepsCvd : Indicator
{
	private const string G = "CVD";

	private readonly object _sync = new();
	private readonly SeguidorCvd _cvd = new();
	private readonly CandleDataSeries _velas = new("VelasCvd", "Velas CVD");
	private readonly ValueDataSeries _macroAlcista = new("MacroAlcista", "Divergencia macro alcista") { VisualType = VisualMode.Line, ShowZeroValue = false, Width = 2 };
	private readonly ValueDataSeries _macroBajista = new("MacroBajista", "Divergencia macro bajista") { VisualType = VisualMode.Line, ShowZeroValue = false, Width = 2 };
	private readonly ValueDataSeries _microAlcista = new("MicroAlcista", "Divergencia micro alcista") { VisualType = VisualMode.Line, ShowZeroValue = false, Width = 1 };
	private readonly ValueDataSeries _microBajista = new("MicroBajista", "Divergencia micro bajista") { VisualType = VisualMode.Line, ShowZeroValue = false, Width = 1 };
	private readonly RenderFont _fuente = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderStringFormat _izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

	private ReinicioCvd _reinicio = ReinicioCvd.DiaFuturos;
	private int _macro = 10;
	private int _micro = 3;
	private bool _conMicro = true;
	private int _maximo = 120;

	public SessionSweepsCvd()
		: base(true)
	{
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		_velas.UpCandleColor = MediaColor.FromArgb(255, 0, 230, 118);
		_velas.DownCandleColor = MediaColor.FromArgb(255, 255, 23, 68);
		_macroAlcista.Color = MediaColor.FromArgb(255, 0, 200, 83);
		_macroBajista.Color = MediaColor.FromArgb(255, 255, 82, 82);
		_microAlcista.Color = MediaColor.FromArgb(200, 105, 240, 174);
		_microBajista.Color = MediaColor.FromArgb(200, 255, 138, 128);

		DataSeries.Add(_velas);
		DataSeries.Add(_macroAlcista);
		DataSeries.Add(_macroBajista);
		DataSeries.Add(_microAlcista);
		DataSeries.Add(_microBajista);

		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	[Display(Name = "Reinicio del CVD", GroupName = G, Order = 10, Description = "Hora de Nueva York a la que el delta acumulado vuelve a cero.")]
	public ReinicioCvd Reinicio { get => _reinicio; set { _reinicio = value; RecalculateValues(); } }

	[Display(Name = "Divergencia macro: velas por lado", GroupName = G, Order = 20, Description = "Pivote de máximo o mínimo con N velas a cada lado. Más = menos sensible. NY NQ: 10.")]
	[Range(2, 100)]
	public int MacroPivotBars { get => _macro; set { _macro = Math.Clamp(value, 2, 100); RecalculateValues(); } }

	[Display(Name = "Divergencia micro: velas por lado", GroupName = G, Order = 30, Description = "Dentro de una macro, para afinar el punto exacto de giro. NY NQ: 3.")]
	[Range(1, 30)]
	public int MicroPivotBars { get => _micro; set { _micro = Math.Clamp(value, 1, 30); RecalculateValues(); } }

	[Display(Name = "Mostrar micro-divergencias", GroupName = G, Order = 40)]
	public bool ShowMicro { get => _conMicro; set { _conMicro = value; RecalculateValues(); } }

	[Display(Name = "Velas que se revisan", GroupName = G, Order = 50, Description = "Ventana en la que se buscan los pivotes de cada divergencia.")]
	[Range(20, 5000)]
	public int MaxLookbackBars { get => _maximo; set { _maximo = Math.Clamp(value, 20, 5000); RecalculateValues(); } }

	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			lock (_sync)
				Calcular(bar);
		}
		catch (Exception ex)
		{
			this.LogError("SessionSweepsCvd: error de cálculo", ex);
		}
	}

	private void Calcular(int bar)
	{
		if (bar == 0)
		{
			_cvd.Reiniciar();
			_cvd.Reinicio = _reinicio == ReinicioCvd.Rth ? new TimeSpan(9, 30, 0) : new TimeSpan(18, 0, 0);
		}

		// Las velas anteriores a 'bar' ya cerraron: cada una entra una sola vez.
		while (_cvd.Velas.Count < bar)
		{
			var i = _cvd.Velas.Count;
			var c = GetCandle(i);
			Poner(i, _cvd.Cerrar(c.Time, c.High, c.Low, c.Delta, c.MaxDelta, c.MinDelta));

			foreach (var d in _cvd.Nuevas(_macro, _micro, _conMicro, _maximo))
				Tramo(d);
		}

		var actual = GetCandle(bar);
		Poner(bar, _cvd.Abierta(actual.Time, actual.High, actual.Low, actual.Delta, actual.MaxDelta, actual.MinDelta));
	}

	private void Poner(int i, VelaCvd v) => _velas[i] = new Candle { Open = v.Open, High = v.High, Low = v.Low, Close = v.Close };

	/// <summary>Tramo recto entre los dos pivotes (las divergencias seguidas del mismo tipo comparten pivote: sin saltos).</summary>
	private void Tramo(DivergenciaCvd d)
	{
		if (d.I2 <= d.I1)
			return;

		var serie = d.Micro ? (d.Alcista ? _microAlcista : _microBajista) : (d.Alcista ? _macroAlcista : _macroBajista);

		for (var b = d.I1; b <= d.I2; b++)
			serie[b] = d.Cvd1 + (d.Cvd2 - d.Cvd1) * (b - d.I1) / (d.I2 - d.I1);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		try
		{
			int macro, micro;

			lock (_sync)
				(macro, micro) = (_cvd.Todas.Count(d => !d.Micro), _cvd.Todas.Count(d => d.Micro));

			var region = Container.Region;
			var texto = $"CVD · reinicio {(_reinicio == ReinicioCvd.Rth ? "09:30" : "18:00")} NY · divergencias macro {macro} · micro {micro}";
			context.DrawString(texto, _fuente, Color.Silver, new Rectangle(region.X + 6, region.Y + 4, Math.Max(10, region.Width - 12), 14), _izquierda);
		}
		catch (Exception ex)
		{
			this.LogError("SessionSweepsCvd: error de dibujo", ex);
		}
	}
}
