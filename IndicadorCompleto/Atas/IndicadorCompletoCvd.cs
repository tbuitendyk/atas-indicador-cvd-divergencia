namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators;

using Completo.Nucleo;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Subpanel de velas CVD OHLC (1M, o 5M agregadas de cinco velas de 1M completas) con las divergencias de 1M y 5M.
/// Toma los datos del motor del maestro del grupo; sin maestro, calcula el CVD con las velas de este gráfico y lo indica.
/// </summary>
[DisplayName("Indicador Completo CVD")]
[Description("Velas CVD OHLC con reinicio a la hora de NY y divergencias de 1M y 5M del Indicador Completo.")]
public class IndicadorCompletoCvd : Indicator
{
	private readonly object _sync = new();
	private readonly CandleDataSeries _velas = new("VelasCvd", "Velas CVD");
	private readonly ValueDataSeries _divAlcista = new("DivAlcista", "Divergencia alcista") { VisualType = VisualMode.Line, ShowZeroValue = false, Width = 2 };
	private readonly ValueDataSeries _divBajista = new("DivBajista", "Divergencia bajista") { VisualType = VisualMode.Line, ShowZeroValue = false, Width = 2 };
	private readonly List<DateTime> _tiempos = new();
	private readonly RenderFont _fuente = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderStringFormat _izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

	private CalculadoraCvd _propia = new(new TimeSpan(9, 30, 0));
	private int _revision = -1;
	private int _ultimaBarra = -1;
	private int _minutos = 1;
	private string _estado = "";

	public IndicadorCompletoCvd()
		: base(true)
	{
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		_velas.UpCandleColor = MediaColor.FromArgb(255, 0, 230, 118);
		_velas.DownCandleColor = MediaColor.FromArgb(255, 255, 23, 68);
		_divAlcista.Color = MediaColor.FromArgb(255, 0, 200, 83);
		_divBajista.Color = MediaColor.FromArgb(255, 255, 82, 82);

		DataSeries.Add(_velas);
		DataSeries.Add(_divAlcista);
		DataSeries.Add(_divBajista);

		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	[Display(Name = "Grupo compartido", GroupName = "CVD", Order = 1, Description = "El mismo grupo que el Indicador Completo del maestro.")]
	public string Grupo { get; set; } = "A";

	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			lock (_sync)
				Calcular(bar);
		}
		catch (Exception ex)
		{
			this.LogError("IndicadorCompletoCvd: error de cálculo", ex);
		}
	}

	private void Calcular(int bar)
	{
		if (bar == 0)
		{
			_tiempos.Clear();
			_propia = new CalculadoraCvd(new TimeSpan(9, 30, 0));
			_revision = -1;
			_ultimaBarra = -1;
			_minutos = CurrentBar > 1 ? Math.Max(1, (int)Math.Round((GetCandle(1).Time - GetCandle(0).Time).TotalMinutes)) : 1;
			_minutos = _minutos >= 5 ? 5 : 1;
		}
		else if (bar < _ultimaBarra)
		{
			return;
		}

		while (_tiempos.Count <= bar)
			_tiempos.Add(GetCandle(_tiempos.Count).Time);

		var motor = RegistroMotores.Motor(RegistroMotores.Clave(Grupo, InstrumentInfo?.Instrument ?? ""));

		if (motor is null)
		{
			// Sin maestro: CVD propio con las velas de este gráfico; cada vela cerrada se suma una sola vez.
			for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
				PonerPropia(i);

			_estado = $"CVD {_minutos}M · SIN MAESTRO: calculado con las velas de este gráfico";
		}
		else if (motor.Revision != _revision)
		{
			_revision = motor.Revision;
			AplicarMotor(motor);
		}

		_ultimaBarra = bar;
	}

	private void PonerPropia(int i)
	{
		var c = GetCandle(i);
		var vela = new Vela
		{
			Marco = Marco.M1,
			InicioUtc = c.Time,
			FinUtc = c.Time.AddMinutes(_minutos),
			Delta = c.Delta,
			DeltaMaximo = c.MaxDelta,
			DeltaMinimo = c.MinDelta
		};

		_propia.Aplicar(vela);
		_velas[i] = new Candle { Open = vela.CvdApertura, High = vela.CvdMaximo, Low = vela.CvdMinimo, Close = vela.CvdCierre };
	}

	/// <summary>Copia las velas CVD del motor a las velas de este gráfico (por hora de inicio) y las divergencias.</summary>
	private void AplicarMotor(MotorCompleto motor)
	{
		if (_tiempos.Count == 0)
			return;

		var velas = motor.VelasCvd(_minutos, _tiempos[0], _tiempos[^1]);
		var indice = new Dictionary<DateTime, int>();

		for (var i = 0; i < _tiempos.Count; i++)
			indice[_tiempos[i]] = i;

		var parcial = false;

		foreach (var v in velas)
		{
			if (!indice.TryGetValue(v.InicioUtc, out var i))
				continue;

			_velas[i] = new Candle { Open = v.CvdApertura, High = v.CvdMaximo, Low = v.CvdMinimo, Close = v.CvdCierre };
			parcial = v.CvdParcial;
		}

		var divergencias = motor.Leer(m => m.Divergencias.Divergencias.Where(d => d.Marco == (_minutos == 5 ? Marco.M5 : Marco.M1)).ToList());
		var cvd = velas.ToDictionary(v => v.InicioUtc, v => v);

		foreach (var d in divergencias)
		{
			if (!indice.TryGetValue(d.P1.ExtremoUtc, out var b1) || !indice.TryGetValue(d.P2.ExtremoUtc, out var b2) || b2 <= b1)
				continue;

			// Tramo recto entre los valores de CVD de los dos pivotes.
			var serie = d.Alcista ? _divAlcista : _divBajista;

			for (var b = b1; b <= b2; b++)
				serie[b] = d.P1.Cvd + (d.P2.Cvd - d.P1.Cvd) * (b - b1) / (b2 - b1);
		}

		_estado = $"CVD {_minutos}M del maestro · {(parcial ? "BASE PARCIAL" : "BASE COMPLETA")} · reinicio 09:30 NY · divergencias {divergencias.Count}";
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		try
		{
			var region = Container.Region;
			context.DrawString(_estado, _fuente, Color.Silver, new Rectangle(region.X + 6, region.Y + 4, Math.Max(10, region.Width - 12), 14), _izquierda);
		}
		catch (Exception ex)
		{
			this.LogError("IndicadorCompletoCvd: error de dibujo", ex);
		}
	}
}
