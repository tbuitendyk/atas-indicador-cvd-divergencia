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
/// Panel con la tendencia de 1 hora, 15 minutos y 5 minutos, calculada a partir de las velas del gráfico actual.
/// La lógica está en <see cref="MotorTendencia"/>, que comparte con el Indicador Combinado.
/// </summary>
[DisplayName("Indicador de Tendencia Hourly")]
[Description("Muestra la tendencia de 1 hora, 15 minutos y 5 minutos en un panel sobre el gráfico: ▲ alcista, ▼ bajista, (-) en rango.")]
public class IndicadorTendenciaHourly : Indicator, IGrafico
{
	private readonly ArrastrePanel _arrastre = new();
	private MotorTendencia? _motor;

	// Se crea al primer uso: ATAS puede leer los ajustes antes de que termine el constructor.
	private MotorTendencia Motor => _motor ??= new MotorTendencia(this);

	#region Configuración

	[Display(Name = "Confirmar al cierre", GroupName = "Cálculo", Order = 10,
		Description = "Activado (por defecto): evalúa solo al cerrar cada vela de 1H/15M/5M y mantiene el estado durante todo el período siguiente. "
			+ "Desactivado: compara el precio actual con la vela anterior de cada marco temporal y cambia en tiempo real.")]
	public bool ConfirmarAlCierre
	{
		get => Motor.ConfirmarAlCierre;
		set
		{
			Motor.ConfirmarAlCierre = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Posición X", GroupName = "Ubicación del panel", Order = 20,
		Description = "También se puede mover arrastrando el panel con el ratón por su barra de título.")]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "Ubicación del panel", Order = 30)]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 60;

	[Display(Name = "Color alcista (▲)", GroupName = "Colores", Order = 40)]
	public MediaColor ColorAlcista
	{
		get => Motor.ColorAlcista;
		set => Motor.ColorAlcista = value;
	}

	[Display(Name = "Color bajista (▼)", GroupName = "Colores", Order = 50)]
	public MediaColor ColorBajista
	{
		get => Motor.ColorBajista;
		set => Motor.ColorBajista = value;
	}

	[Display(Name = "Color rango (-)", GroupName = "Colores", Order = 60)]
	public MediaColor ColorRango
	{
		get => Motor.ColorRango;
		set => Motor.ColorRango = value;
	}

	[Display(Name = "Fondo de la pastilla", GroupName = "Colores", Order = 70)]
	public MediaColor ColorFondoPastilla
	{
		get => Motor.ColorFondoPastilla;
		set => Motor.ColorFondoPastilla = value;
	}

	#endregion

	public IndicadorTendenciaHourly()
		: base(true)
	{
		// El indicador solo dibuja el panel: se oculta la serie por defecto para que no trace una línea en 0
		// que deforme la escala de precios.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

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

		var panel = _arrastre.Ubicar(PanelX, PanelY, MotorTendencia.AnchoMinimo, Motor.AltoPanel, MotorTendencia.AltoTitulo, ((IGrafico)this).Area);
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
