namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Control;

using Rectangle = System.Drawing.Rectangle;
using StdCursor = OFT.Rendering.StdCursor;

/// <summary>
/// Pronóstico de la siguiente vela con barridos de liquidez, volumen climático y desequilibrios del footprint.
/// La lógica está en <see cref="MotorFootprint"/>, que comparte con el Indicador Combinado.
/// </summary>
[DisplayName("Footprint Predictor Pro (Sweeps & Context)")]
[Description("Barridos de liquidez externa e interna con volumen climático, desequilibrios apilados del footprint y cierre respecto al POC: pronóstico de la siguiente vela.")]
public class FootprintPredictorPro : Indicator, IGrafico
{
	private readonly ArrastrePanel _arrastre = new();
	private MotorFootprint? _motor;

	// Se crea al primer uso: ATAS puede leer los ajustes antes de que termine el constructor.
	private MotorFootprint Motor => _motor ??= new MotorFootprint(this);

	#region Configuración

	[Display(Name = "Imbalance Ratio (%)", GroupName = "1. Footprint Imbalance", Order = 10,
		Description = "Porcentaje mínimo para considerar desequilibrio diagonal (300 = 3:1)")]
	[Range(100, 1000)]
	public decimal ImbalanceRatio
	{
		get => Motor.ImbalanceRatio;
		set
		{
			Motor.ImbalanceRatio = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Stacked Levels", GroupName = "1. Footprint Imbalance", Order = 20,
		Description = "Niveles consecutivos mínimos de desequilibrio")]
	[Range(2, 10)]
	public int StackedLevels
	{
		get => Motor.StackedLevels;
		set
		{
			Motor.StackedLevels = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Pivot Window (Minutos)", GroupName = "2. Swings de Liquidez", Order = 30,
		Description = "Un swing es liquidez externa si es el mínimo (o máximo) más extremo de estos minutos anteriores a él; si no, es liquidez interna.")]
	[Range(1, 1440)]
	public int PivotWindowMinutes
	{
		get => Motor.PivotWindowMinutes;
		set
		{
			Motor.PivotWindowMinutes = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Min Penetration (Ticks)", GroupName = "2. Swings de Liquidez", Order = 40,
		Description = "Ticks mínimos que el precio debe penetrar el pivot para validar el barrido")]
	[Range(1, 100)]
	public int MinPenetrationTicks
	{
		get => Motor.MinPenetrationTicks;
		set
		{
			Motor.MinPenetrationTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Reclaim Buffer (Ticks)", GroupName = "2. Swings de Liquidez", Order = 50,
		Description = "Ticks por dentro del nivel roto requeridos en el cierre para confirmar el rechazo. Si una vela cierra más allá del nivel sin recuperarlo, el nivel deja de ser liquidez.")]
	[Range(0, 50)]
	public int ReclaimBufferTicks
	{
		get => Motor.ReclaimBufferTicks;
		set
		{
			Motor.ReclaimBufferTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Climatic Volume Multiplier", GroupName = "3. Volumen Climático", Order = 60,
		Description = "Multiplicador sobre la media de volumen de las 20 velas anteriores para catalogar volumen climático")]
	[Range(0, 100)]
	public decimal ClimaticVolumeMultiplier
	{
		get => Motor.ClimaticVolumeMultiplier;
		set
		{
			Motor.ClimaticVolumeMultiplier = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mostrar Panel de Contexto", GroupName = "4. Interfaz Visual", Order = 70)]
	public bool ShowContextPanel
	{
		get => Motor.MostrarPanel;
		set => Motor.MostrarPanel = value;
	}

	[Display(Name = "Posición X", GroupName = "4. Interfaz Visual", Order = 80,
		Description = "También se puede mover arrastrando el panel con el ratón por su barra de título.")]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "4. Interfaz Visual", Order = 90,
		Description = "Por defecto queda debajo de los paneles del Indicador de Tendencia Hourly y del CVD Trigger Engine.")]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 410;

	#endregion

	public FootprintPredictorPro()
		: base(true)
	{
		// La serie por defecto no se usa: se oculta para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		foreach (var serie in Motor.Series)
			DataSeries.Add(serie);

		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	protected override void OnCalculate(int bar, decimal value) => Motor.Calcular(bar);

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		if (!Motor.MostrarPanel)
		{
			_arrastre.Ocultar();
			return;
		}

		var panel = _arrastre.Ubicar(PanelX, PanelY, MotorFootprint.AnchoMinimo, MotorFootprint.AltoPanel, MotorFootprint.AltoTitulo, ((IGrafico)this).Area);
		Motor.DibujarPanel(context, panel.X, panel.Y, panel.Width);
	}

	#region Arrastre con el ratón por la barra de título

	public override bool ProcessMouseDown(RenderControlMouseEventArgs e) =>
		_arrastre.Presionar(e.Location) || base.ProcessMouseDown(e);

	public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
	{
		if (_arrastre.Mover(e.Location, ((IGrafico)this).Area) is not { } posicion)
			return base.ProcessMouseMove(e);

		PanelX = posicion.X;
		PanelY = posicion.Y;
		RedrawChart();
		return true;
	}

	public override bool ProcessMouseUp(RenderControlMouseEventArgs e) =>
		_arrastre.Soltar() || base.ProcessMouseUp(e);

	public override StdCursor GetCursor(RenderControlMouseEventArgs e) =>
		_arrastre.Arrastrando || _arrastre.SobreTitulo(e.Location) ? StdCursor.Hand : base.GetCursor(e);

	#endregion

	#region Datos para el motor (IGrafico)

	IndicatorCandle IGrafico.Vela(int bar) => GetCandle(bar);
	int IGrafico.CantidadVelas => CurrentBar;
	bool IGrafico.EsNuevaSesion(int bar) => IsNewSession(bar);
	decimal IGrafico.TamanoTick => InstrumentInfo?.TickSize ?? 0m;
	TimeSpan IGrafico.ZonaHoraria => InstrumentInfo?.TimeZoneOffset ?? TimeSpan.Zero;
	string? IGrafico.TipoGrafico => ChartInfo?.ChartType;
	Rectangle IGrafico.Area => ChartInfo?.PriceChartContainer.Region ?? Rectangle.Empty;
	int IGrafico.PrimeraVisible => FirstVisibleBarNumber;
	int IGrafico.UltimaVisible => LastVisibleBarNumber;
	int IGrafico.XVela(int bar) => ChartInfo.GetXByBar(bar);
	int IGrafico.YPrecio(decimal precio) => ChartInfo.GetYByPrice(precio, false);
	int IGrafico.AnchoVela => (int)ChartInfo.PriceChartContainer.BarsWidth;
	int IGrafico.EspacioVela => (int)(ChartInfo.PriceChartContainer.BarSpacing + ChartInfo.PriceChartContainer.BarsWidth);

	#endregion
}
