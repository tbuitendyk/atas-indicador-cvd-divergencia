namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;

using MediaColor = System.Windows.Media.Color;

/// <summary>
/// Velas del CVD (delta acumulado por sesión) en un panel propio. Acompaña al
/// Institutional CVD Trigger &amp; Absorption Engine: un indicador de ATAS dibuja en un único panel y
/// el motor tiene que estar en el gráfico de precio, así que las velas CVD van en este indicador aparte.
/// </summary>
[DisplayName("Velas CVD (panel)")]
[Description("Velas del CVD (delta acumulado) en un panel propio, con reinicio por sesión. Usa el mismo cálculo que el Institutional CVD Trigger & Absorption Engine.")]
public class VelasCvd : Indicator
{
	private readonly CalculadoraCvd _cvd = new();
	private readonly CandleDataSeries _velas = new("VelasCvd", "Velas CVD");

	private int _ultimaBarra = -1;
	private MediaColor _colorAlcista = MediaColor.FromArgb(255, 0, 230, 118);
	private MediaColor _colorBajista = MediaColor.FromArgb(255, 255, 23, 68);
	private bool _reiniciarPorSesion = true;

	#region Configuración

	[Display(Name = "Color vela CVD alcista", GroupName = "Velas CVD", Order = 10)]
	public MediaColor ColorAlcista
	{
		get => _colorAlcista;
		set
		{
			_colorAlcista = value;
			_velas.UpCandleColor = value;
		}
	}

	[Display(Name = "Color vela CVD bajista", GroupName = "Velas CVD", Order = 20)]
	public MediaColor ColorBajista
	{
		get => _colorBajista;
		set
		{
			_colorBajista = value;
			_velas.DownCandleColor = value;
		}
	}

	[Display(Name = "Reiniciar CVD en cada sesión", GroupName = "Velas CVD", Order = 30,
		Description = "Activado: el CVD vuelve a cero al empezar cada sesión del instrumento. Desactivado: acumula todo el histórico cargado.")]
	public bool ReiniciarPorSesion
	{
		get => _reiniciarPorSesion;
		set
		{
			_reiniciarPorSesion = value;
			RecalculateValues();
		}
	}

	#endregion

	public VelasCvd()
		: base(true)
	{
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		_velas.UpCandleColor = _colorAlcista;
		_velas.DownCandleColor = _colorBajista;
		DataSeries.Add(_velas);

		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_cvd.Reiniciar();
			_ultimaBarra = -1;
		}
		else if (bar < _ultimaBarra)
		{
			return;
		}

		// Al pasar a una vela nueva, la anterior se recalcula con sus valores finales.
		for (var i = Math.Max(0, Math.Min(_ultimaBarra, bar)); i <= bar; i++)
		{
			var cvd = _cvd.Calcular(i, GetCandle(i), _reiniciarPorSesion && i > 0 && IsNewSession(i));
			_velas[i] = new Candle { Open = cvd.Apertura, High = cvd.Maximo, Low = cvd.Minimo, Close = cvd.Cierre };
		}

		_ultimaBarra = bar;
	}
}
