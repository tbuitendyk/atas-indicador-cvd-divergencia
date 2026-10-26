namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Control;

using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;
using StdCursor = OFT.Rendering.StdCursor;

/// <summary>
/// Nodos bajos de volumen (LVN) del perfil de hasta 90 días alrededor del precio, clasificados por impacto, con
/// panel del objetivo de retest. La lógica está en <see cref="MotorLvn"/>, que comparte con el Indicador Combinado.
/// </summary>
[DisplayName("Institutional 90-Day Deep LVN Profile Engine")]
[Description("Motor institucional de Nodos Bajos de Volumen (LVN) con soporte de hasta 3 meses de datos Rithmic, scoring de impacto y retest tras barrido.")]
public class InstitutionalDeepLvnEngine : Indicator, IGrafico
{
	private readonly ArrastrePanel _arrastre = new();
	private MotorLvn? _motor;

	// Se crea al primer uso: ATAS puede leer los ajustes antes de que termine el constructor.
	private MotorLvn Motor => _motor ??= new MotorLvn(this);

	#region Configuración

	[Display(Name = "Modo de Selección de Rango", GroupName = "1. Horizonte Temporal (Rithmic)", Order = 10,
		Description = "Automático: 3 días si el precio está en el extremo del rango de los últimos 3 días (descubrimiento), 60 días si cotiza dentro de una estructura previa (balance). Manual: los días indicados abajo.")]
	public ModoHorizonte SelectionMode
	{
		get => Motor.Horizonte;
		set
		{
			Motor.Horizonte = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Días de Historial (Modo Manual)", GroupName = "1. Horizonte Temporal (Rithmic)", Order = 20,
		Description = "Soporta hasta 90 días (3 meses de datos de Rithmic). El gráfico tiene que tener cargados esos días.")]
	[Range(1, 90)]
	public int ManualHistoryDays
	{
		get => Motor.DiasManual;
		set
		{
			Motor.DiasManual = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Franja de Búsqueda Alrededor del Precio (Pts)", GroupName = "1. Horizonte Temporal (Rithmic)", Order = 30,
		Description = "Rango de puntos ± alrededor del precio actual para filtrar el perfil histórico en NQ.")]
	[Range(20, 1000)]
	public decimal SearchBandPoints
	{
		get => Motor.FranjaPuntos;
		set
		{
			Motor.FranjaPuntos = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Sensibilidad de Valle (Ticks)", GroupName = "2. Calibración Cuantitativa LVN", Order = 40,
		Description = "Un LVN tiene menos volumen que todos los precios a esta cantidad de ticks a cada lado.")]
	[Range(2, 25)]
	public int SensitivityTicks
	{
		get => Motor.SensibilidadTicks;
		set
		{
			Motor.SensibilidadTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Umbral Valle Alto Impacto (Ratio)", GroupName = "2. Calibración Cuantitativa LVN", Order = 50,
		Description = "Relación de volumen del LVN respecto al pico adyacente (0.30 = 30%).")]
	[Range(0, 1)]
	public decimal HighImpactThresholdRatio
	{
		get => Motor.UmbralAltoImpacto;
		set
		{
			Motor.UmbralAltoImpacto = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuente de volumen", GroupName = "2. Calibración Cuantitativa LVN", Order = 52,
		Description = "Real: el volumen negociado en cada precio (datos de clúster de ATAS); las velas sin esos datos se reparten en su rango. Repartido: el volumen de cada vela se reparte por igual en su rango.")]
	public FuenteVolumen VolumeSource
	{
		get => Motor.Fuente;
		set
		{
			Motor.Fuente = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Tipo de rechazo", GroupName = "2. Calibración Cuantitativa LVN", Order = 54,
		Description = "Extremo: el mínimo (o máximo) de la vela toca el nivel y la vela cierra lejos de él. Mecha: el nivel queda dentro de la mecha de la vela.")]
	public TipoRechazo RejectionMode
	{
		get => Motor.Rechazo;
		set
		{
			Motor.Rechazo = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Tolerancia de toque (Ticks)", GroupName = "2. Calibración Cuantitativa LVN", Order = 56,
		Description = "Solo tipo Extremo: distancia máxima entre el mínimo (o máximo) de la vela y el nivel.")]
	[Range(0, 50)]
	public int TouchToleranceTicks
	{
		get => Motor.ToleranciaTicks;
		set
		{
			Motor.ToleranciaTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Distancia mínima del cierre (Ticks)", GroupName = "2. Calibración Cuantitativa LVN", Order = 58,
		Description = "Solo tipo Extremo: ticks entre el cierre y el nivel para contar como rechazo.")]
	[Range(1, 200)]
	public int CloseDistanceTicks
	{
		get => Motor.DistanciaCierreTicks;
		set
		{
			Motor.DistanciaCierreTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Color LVN Alto Impacto (Default)", GroupName = "3. Visualización de Niveles", Order = 60)]
	public MediaColor HighImpactColor
	{
		get => Motor.ColorAlto;
		set => Motor.ColorAlto = value;
	}

	[Display(Name = "Color LVN Mediano Impacto", GroupName = "3. Visualización de Niveles", Order = 70)]
	public MediaColor MediumImpactColor
	{
		get => Motor.ColorMediano;
		set => Motor.ColorMediano = value;
	}

	[Display(Name = "Color LVN Bajo Impacto", GroupName = "3. Visualización de Niveles", Order = 80)]
	public MediaColor LowImpactColor
	{
		get => Motor.ColorBajo;
		set => Motor.ColorBajo = value;
	}

	[Display(Name = "Grosor Línea Alto Impacto", GroupName = "3. Visualización de Niveles", Order = 90)]
	[Range(1, 6)]
	public int HighImpactLineWidth
	{
		get => Motor.GrosorAlto;
		set => Motor.GrosorAlto = value;
	}

	[Display(Name = "Extender Niveles al Borde Derecho", GroupName = "3. Visualización de Niveles", Order = 100)]
	public bool ExtendLinesRight
	{
		get => Motor.ExtenderDerecha;
		set => Motor.ExtenderDerecha = value;
	}

	[Display(Name = "Mostrar LVN de bajo impacto", GroupName = "3. Visualización de Niveles", Order = 105,
		Description = "Muescas locales del perfil (línea gris fina). Desactivado por defecto para no saturar el gráfico.")]
	public bool ShowLowImpact
	{
		get => Motor.MostrarBajoImpacto;
		set => Motor.MostrarBajoImpacto = value;
	}

	[Display(Name = "Posición X HUD", GroupName = "4. Panel Informativo HUD", Order = 110,
		Description = "También se puede mover arrastrando el panel con el ratón por su barra de título.")]
	[Range(0, 10000)]
	public int HudX { get; set; } = 25;

	[Display(Name = "Posición Y HUD", GroupName = "4. Panel Informativo HUD", Order = 120,
		Description = "Por defecto queda debajo de los paneles de los otros tres indicadores.")]
	[Range(0, 10000)]
	public int HudY { get; set; } = 490;

	#endregion

	public InstitutionalDeepLvnEngine()
		: base(true)
	{
		// Solo dibuja (líneas y panel): se oculta la serie por defecto para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = false;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	protected override void OnCalculate(int bar, decimal value) => Motor.Calcular(bar);

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		Motor.DibujarEnGrafico(context);

		var panel = _arrastre.Ubicar(HudX, HudY, MotorLvn.AnchoMinimo, MotorLvn.AltoPanel, MotorLvn.AltoTitulo, ((IGrafico)this).Area);
		Motor.DibujarPanel(context, panel.X, panel.Y, panel.Width);
	}

	#region Arrastre con el ratón por la barra de título

	public override bool ProcessMouseDown(RenderControlMouseEventArgs e) =>
		_arrastre.Presionar(e.Location) || base.ProcessMouseDown(e);

	public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
	{
		if (_arrastre.Mover(e.Location, ((IGrafico)this).Area) is not { } posicion)
			return base.ProcessMouseMove(e);

		HudX = posicion.X;
		HudY = posicion.Y;
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
