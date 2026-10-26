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
/// Gatillo sobre el CVD con cuatro filtros en cascada, banderas de compra/venta y panel de auditoría.
/// La lógica está en <see cref="MotorCvd"/>, que comparte con el Indicador Combinado.
/// </summary>
[DisplayName("Institutional CVD Trigger & Absorption Engine")]
[Description("Motor institucional autosuficiente: Macro y Micro Divergencias CVD, Absorción cuantitativa, Delta Shift y Banderas Gatillo.")]
public class InstitutionalCvdTriggerEngine : Indicator, IGrafico
{
	private readonly ArrastrePanel _arrastre = new();
	private MotorCvd? _motor;

	// Se crea al primer uso: ATAS puede leer los ajustes antes de que termine el constructor.
	private MotorCvd Motor => _motor ??= new MotorCvd(this);

	#region Configuración

	[Display(Name = "Color CVD alcista", GroupName = "1. Velas CVD", Order = 10)]
	public MediaColor ColorCvdAlcista
	{
		get => Motor.ColorCvdAlcista;
		set => Motor.ColorCvdAlcista = value;
	}

	[Display(Name = "Color CVD bajista", GroupName = "1. Velas CVD", Order = 20)]
	public MediaColor ColorCvdBajista
	{
		get => Motor.ColorCvdBajista;
		set => Motor.ColorCvdBajista = value;
	}

	[Display(Name = "Mostrar CVD en cada vela (Arial 10)", GroupName = "1. Velas CVD", Order = 30,
		Description = "Escribe el CVD de la sesión en el centro de cada vela. Solo aparece cuando las velas son lo bastante anchas para que los textos no se superpongan: acercar el gráfico.")]
	public bool MostrarTextoEnVela
	{
		get => Motor.MostrarTextoEnVela;
		set => Motor.MostrarTextoEnVela = value;
	}

	[Display(Name = "Reiniciar CVD en cada sesión", GroupName = "1. Velas CVD", Order = 40,
		Description = "Activado: el CVD vuelve a cero al empezar cada sesión del instrumento. Desactivado: acumula todo el histórico cargado.")]
	public bool ReiniciarPorSesion
	{
		get => Motor.ReiniciarPorSesion;
		set
		{
			Motor.ReiniciarPorSesion = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuerza macro-divergencia (barras)", GroupName = "2. Divergencias", Order = 50,
		Description = "Velas a cada lado necesarias para confirmar un swing macro. Con 12 en un gráfico de 1 minuto, cada swing abarca unos 25 minutos.")]
	[Range(1, 200)]
	public int FuerzaMacro
	{
		get => Motor.FuerzaMacro;
		set
		{
			Motor.FuerzaMacro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuerza micro-divergencia (barras)", GroupName = "2. Divergencias", Order = 60,
		Description = "Velas a cada lado necesarias para confirmar un swing micro (el fractal del gatillo).")]
	[Range(1, 50)]
	public int FuerzaMicro
	{
		get => Motor.FuerzaMicro;
		set
		{
			Motor.FuerzaMicro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia de las divergencias", GroupName = "2. Divergencias", Order = 70,
		Description = "Por barras: cada divergencia vence tras las barras indicadas abajo, contadas desde que se confirma. "
			+ "Hasta divergencia opuesta: sigue activa hasta que aparece una en sentido contrario.")]
	public ModoVigencia Vigencia
	{
		get => Motor.Vigencia;
		set
		{
			Motor.Vigencia = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia macro (barras)", GroupName = "2. Divergencias", Order = 80,
		Description = "Solo en modo Por barras. 60 = una hora en un gráfico de 1 minuto.")]
	[Range(1, 100000)]
	public int VigenciaMacro
	{
		get => Motor.VigenciaMacro;
		set
		{
			Motor.VigenciaMacro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia micro (barras)", GroupName = "2. Divergencias", Order = 90,
		Description = "Solo en modo Por barras. Barras disponibles para que llegue el giro de delta tras confirmarse la micro-divergencia.")]
	[Range(1, 10000)]
	public int VigenciaMicro
	{
		get => Motor.VigenciaMicro;
		set
		{
			Motor.VigenciaMicro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Umbral delta gatillo (contratos)", GroupName = "3. Filtros de gatillo", Order = 100,
		Description = "Delta mínimo, en valor absoluto, de una vela posterior al swing para validar el giro agresivo (positivo para compra, negativo para venta).")]
	[Range(0, 10000000)]
	public decimal UmbralDelta
	{
		get => Motor.UmbralDelta;
		set
		{
			Motor.UmbralDelta = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mínimo mecha (% del rango)", GroupName = "3. Filtros de gatillo", Order = 110,
		Description = "Mecha del lado del swing (inferior en compras, superior en ventas) respecto al rango total de la vela del swing.")]
	[Range(0, 100)]
	public decimal MinMechaPct
	{
		get => Motor.MinMechaPct;
		set
		{
			Motor.MinMechaPct = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mínimo volumen en la mecha (% de la vela)", GroupName = "3. Filtros de gatillo", Order = 120,
		Description = "Volumen negociado en los precios de esa mecha respecto al volumen total de la vela del swing (datos por nivel de precio de ATAS).")]
	[Range(0, 100)]
	public decimal MinVolumenMechaPct
	{
		get => Motor.MinVolumenMechaPct;
		set
		{
			Motor.MinVolumenMechaPct = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Posición X", GroupName = "4. Panel HUD", Order = 130,
		Description = "También se puede mover arrastrando el panel con el ratón por su barra de título.")]
	[Range(0, 10000)]
	public int HudX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "4. Panel HUD", Order = 140,
		Description = "Por defecto queda debajo del panel del Indicador de Tendencia Hourly.")]
	[Range(0, 10000)]
	public int HudY { get; set; } = 230;

	#endregion

	public InstitutionalCvdTriggerEngine()
		: base(true)
	{
		// Solo dibuja (texto, banderas y panel): se oculta la serie por defecto para que no deforme la escala.
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

		Motor.DibujarEnGrafico(context);

		var panel = _arrastre.Ubicar(HudX, HudY, MotorCvd.AnchoMinimo, MotorCvd.AltoPanel, MotorCvd.AltoTitulo, ((IGrafico)this).Area);
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
