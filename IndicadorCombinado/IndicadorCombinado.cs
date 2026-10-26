namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;
using StdCursor = OFT.Rendering.StdCursor;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Los cuatro indicadores en uno: Tendencia Hourly, CVD Trigger Engine, Footprint Predictor y LVN de 90 días.
/// Usa los mismos motores que los indicadores individuales; cada módulo se puede apagar. Las banderas, los puntos, las
/// líneas de LVN y el CVD en las velas se dibujan sobre el gráfico, y los cuatro paneles van apilados en un solo
/// tablero que se arrastra por su barra de título. Las velas del CVD siguen siendo un indicador aparte
/// ("Velas CVD (panel)"), porque necesitan su propio panel.
/// </summary>
[DisplayName("Indicador Combinado (Tendencia, CVD, Footprint y LVN)")]
[Description("Los cuatro indicadores en uno, con un solo tablero movible: tendencia 1H/15M/5M, gatillo CVD, pronóstico del footprint y LVN de hasta 90 días. Cada módulo se puede apagar.")]
public class IndicadorCombinado : Indicator, IGrafico
{
	#region Campos

	private const int AltoTitulo = 22;
	private const int Separacion = 4;

	private readonly ArrastrePanel _arrastre = new();

	private MotorTendencia? _tendencia;
	private MotorCvd? _cvd;
	private MotorFootprint? _footprint;
	private MotorLvn? _lvn;

	private bool _mostrarTendencia = true;
	private bool _mostrarCvd = true;
	private bool _mostrarFootprint = true;
	private bool _mostrarLvn = true;

	private readonly RenderFont _fuenteTitulo = new("Arial", 8.5f, FontStyle.Bold);
	private readonly RenderStringFormat _alinearIzquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderPen _bordeTablero = new(Color.FromArgb(90, 100, 120));

	#endregion

	// Los motores se crean al primer uso: ATAS puede leer los ajustes antes de que termine el constructor.
	private MotorTendencia Tendencia => _tendencia ??= new MotorTendencia(this);
	private MotorCvd Cvd => _cvd ??= new MotorCvd(this);
	private MotorFootprint Footprint => _footprint ??= new MotorFootprint(this);
	private MotorLvn Lvn => _lvn ??= new MotorLvn(this);

	#region Configuración: módulos

	[Display(Name = "Tendencia Hourly", GroupName = "0. Módulos", Order = 1)]
	public bool MostrarTendencia
	{
		get => _mostrarTendencia;
		set
		{
			_mostrarTendencia = value;
			RecalculateValues();
		}
	}

	[Display(Name = "CVD Trigger Engine", GroupName = "0. Módulos", Order = 2)]
	public bool MostrarCvd
	{
		get => _mostrarCvd;
		set
		{
			_mostrarCvd = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Footprint Predictor", GroupName = "0. Módulos", Order = 3)]
	public bool MostrarFootprint
	{
		get => _mostrarFootprint;
		set
		{
			_mostrarFootprint = value;

			foreach (var serie in Footprint.Series)
				serie.IsHidden = !value;

			RecalculateValues();
		}
	}

	[Display(Name = "LVN (perfil de volumen)", GroupName = "0. Módulos", Order = 4)]
	public bool MostrarLvn
	{
		get => _mostrarLvn;
		set
		{
			_mostrarLvn = value;
			RecalculateValues();
		}
	}

	#endregion

	#region Configuración: Tendencia Hourly

	[Display(Name = "Confirmar al cierre", GroupName = "1. Tendencia Hourly", Order = 10,
		Description = "Activado: evalúa solo al cerrar cada vela de 1H/15M/5M y mantiene el estado todo el período siguiente. Desactivado: cambia en tiempo real.")]
	public bool ConfirmarAlCierre
	{
		get => Tendencia.ConfirmarAlCierre;
		set
		{
			Tendencia.ConfirmarAlCierre = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Color alcista (▲)", GroupName = "1. Tendencia Hourly", Order = 11)]
	public MediaColor ColorAlcista
	{
		get => Tendencia.ColorAlcista;
		set => Tendencia.ColorAlcista = value;
	}

	[Display(Name = "Color bajista (▼)", GroupName = "1. Tendencia Hourly", Order = 12)]
	public MediaColor ColorBajista
	{
		get => Tendencia.ColorBajista;
		set => Tendencia.ColorBajista = value;
	}

	[Display(Name = "Color rango (-)", GroupName = "1. Tendencia Hourly", Order = 13)]
	public MediaColor ColorRango
	{
		get => Tendencia.ColorRango;
		set => Tendencia.ColorRango = value;
	}

	[Display(Name = "Fondo de la pastilla", GroupName = "1. Tendencia Hourly", Order = 14)]
	public MediaColor ColorFondoPastilla
	{
		get => Tendencia.ColorFondoPastilla;
		set => Tendencia.ColorFondoPastilla = value;
	}

	#endregion

	#region Configuración: CVD Trigger Engine

	[Display(Name = "Color CVD alcista", GroupName = "2. CVD Trigger Engine", Order = 20)]
	public MediaColor ColorCvdAlcista
	{
		get => Cvd.ColorCvdAlcista;
		set => Cvd.ColorCvdAlcista = value;
	}

	[Display(Name = "Color CVD bajista", GroupName = "2. CVD Trigger Engine", Order = 21)]
	public MediaColor ColorCvdBajista
	{
		get => Cvd.ColorCvdBajista;
		set => Cvd.ColorCvdBajista = value;
	}

	[Display(Name = "Mostrar CVD en cada vela (Arial 10)", GroupName = "2. CVD Trigger Engine", Order = 22,
		Description = "Solo aparece cuando las velas son lo bastante anchas para que los textos no se superpongan.")]
	public bool MostrarTextoEnVela
	{
		get => Cvd.MostrarTextoEnVela;
		set => Cvd.MostrarTextoEnVela = value;
	}

	[Display(Name = "Reiniciar CVD en cada sesión", GroupName = "2. CVD Trigger Engine", Order = 23)]
	public bool ReiniciarPorSesion
	{
		get => Cvd.ReiniciarPorSesion;
		set
		{
			Cvd.ReiniciarPorSesion = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuerza macro-divergencia (barras)", GroupName = "2. CVD Trigger Engine", Order = 24)]
	[Range(1, 200)]
	public int FuerzaMacro
	{
		get => Cvd.FuerzaMacro;
		set
		{
			Cvd.FuerzaMacro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuerza micro-divergencia (barras)", GroupName = "2. CVD Trigger Engine", Order = 25)]
	[Range(1, 50)]
	public int FuerzaMicro
	{
		get => Cvd.FuerzaMicro;
		set
		{
			Cvd.FuerzaMicro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia de las divergencias", GroupName = "2. CVD Trigger Engine", Order = 26)]
	public ModoVigencia Vigencia
	{
		get => Cvd.Vigencia;
		set
		{
			Cvd.Vigencia = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia macro (barras)", GroupName = "2. CVD Trigger Engine", Order = 27)]
	[Range(1, 100000)]
	public int VigenciaMacro
	{
		get => Cvd.VigenciaMacro;
		set
		{
			Cvd.VigenciaMacro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia micro (barras)", GroupName = "2. CVD Trigger Engine", Order = 28)]
	[Range(1, 10000)]
	public int VigenciaMicro
	{
		get => Cvd.VigenciaMicro;
		set
		{
			Cvd.VigenciaMicro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Umbral delta gatillo (contratos)", GroupName = "2. CVD Trigger Engine", Order = 29)]
	[Range(0, 10000000)]
	public decimal UmbralDelta
	{
		get => Cvd.UmbralDelta;
		set
		{
			Cvd.UmbralDelta = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mínimo mecha (% del rango)", GroupName = "2. CVD Trigger Engine", Order = 30)]
	[Range(0, 100)]
	public decimal MinMechaPct
	{
		get => Cvd.MinMechaPct;
		set
		{
			Cvd.MinMechaPct = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mínimo volumen en la mecha (% de la vela)", GroupName = "2. CVD Trigger Engine", Order = 31)]
	[Range(0, 100)]
	public decimal MinVolumenMechaPct
	{
		get => Cvd.MinVolumenMechaPct;
		set
		{
			Cvd.MinVolumenMechaPct = value;
			RecalculateValues();
		}
	}

	#endregion

	#region Configuración: Footprint Predictor

	[Display(Name = "Imbalance Ratio (%)", GroupName = "3. Footprint Predictor", Order = 40,
		Description = "Porcentaje mínimo para considerar desequilibrio diagonal (300 = 3:1)")]
	[Range(100, 1000)]
	public decimal ImbalanceRatio
	{
		get => Footprint.ImbalanceRatio;
		set
		{
			Footprint.ImbalanceRatio = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Stacked Levels", GroupName = "3. Footprint Predictor", Order = 41)]
	[Range(2, 10)]
	public int StackedLevels
	{
		get => Footprint.StackedLevels;
		set
		{
			Footprint.StackedLevels = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Pivot Window (Minutos)", GroupName = "3. Footprint Predictor", Order = 42,
		Description = "Un swing es liquidez externa si es el extremo de estos minutos anteriores a él.")]
	[Range(1, 1440)]
	public int PivotWindowMinutes
	{
		get => Footprint.PivotWindowMinutes;
		set
		{
			Footprint.PivotWindowMinutes = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Min Penetration (Ticks)", GroupName = "3. Footprint Predictor", Order = 43)]
	[Range(1, 100)]
	public int MinPenetrationTicks
	{
		get => Footprint.MinPenetrationTicks;
		set
		{
			Footprint.MinPenetrationTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Reclaim Buffer (Ticks)", GroupName = "3. Footprint Predictor", Order = 44)]
	[Range(0, 50)]
	public int ReclaimBufferTicks
	{
		get => Footprint.ReclaimBufferTicks;
		set
		{
			Footprint.ReclaimBufferTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Climatic Volume Multiplier", GroupName = "3. Footprint Predictor", Order = 45)]
	[Range(0, 100)]
	public decimal ClimaticVolumeMultiplier
	{
		get => Footprint.ClimaticVolumeMultiplier;
		set
		{
			Footprint.ClimaticVolumeMultiplier = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mostrar Panel de Contexto", GroupName = "3. Footprint Predictor", Order = 46)]
	public bool ShowContextPanel
	{
		get => Footprint.MostrarPanel;
		set => Footprint.MostrarPanel = value;
	}

	#endregion

	#region Configuración: LVN

	[Display(Name = "Modo de Selección de Rango", GroupName = "4. LVN (perfil de volumen)", Order = 50)]
	public ModoHorizonte SelectionMode
	{
		get => Lvn.Horizonte;
		set
		{
			Lvn.Horizonte = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Días de Historial (Modo Manual)", GroupName = "4. LVN (perfil de volumen)", Order = 51)]
	[Range(1, 90)]
	public int ManualHistoryDays
	{
		get => Lvn.DiasManual;
		set
		{
			Lvn.DiasManual = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Franja de Búsqueda Alrededor del Precio (Pts)", GroupName = "4. LVN (perfil de volumen)", Order = 52)]
	[Range(20, 1000)]
	public decimal SearchBandPoints
	{
		get => Lvn.FranjaPuntos;
		set
		{
			Lvn.FranjaPuntos = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Sensibilidad de Valle (Ticks)", GroupName = "4. LVN (perfil de volumen)", Order = 53)]
	[Range(2, 25)]
	public int SensitivityTicks
	{
		get => Lvn.SensibilidadTicks;
		set
		{
			Lvn.SensibilidadTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Umbral Valle Alto Impacto (Ratio)", GroupName = "4. LVN (perfil de volumen)", Order = 54)]
	[Range(0, 1)]
	public decimal HighImpactThresholdRatio
	{
		get => Lvn.UmbralAltoImpacto;
		set
		{
			Lvn.UmbralAltoImpacto = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuente de volumen", GroupName = "4. LVN (perfil de volumen)", Order = 55)]
	public FuenteVolumen VolumeSource
	{
		get => Lvn.Fuente;
		set
		{
			Lvn.Fuente = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Tipo de rechazo", GroupName = "4. LVN (perfil de volumen)", Order = 56)]
	public TipoRechazo RejectionMode
	{
		get => Lvn.Rechazo;
		set
		{
			Lvn.Rechazo = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Tolerancia de toque (Ticks)", GroupName = "4. LVN (perfil de volumen)", Order = 57)]
	[Range(0, 50)]
	public int TouchToleranceTicks
	{
		get => Lvn.ToleranciaTicks;
		set
		{
			Lvn.ToleranciaTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Distancia mínima del cierre (Ticks)", GroupName = "4. LVN (perfil de volumen)", Order = 58)]
	[Range(1, 200)]
	public int CloseDistanceTicks
	{
		get => Lvn.DistanciaCierreTicks;
		set
		{
			Lvn.DistanciaCierreTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Color LVN Alto Impacto", GroupName = "4. LVN (perfil de volumen)", Order = 59)]
	public MediaColor HighImpactColor
	{
		get => Lvn.ColorAlto;
		set => Lvn.ColorAlto = value;
	}

	[Display(Name = "Color LVN Mediano Impacto", GroupName = "4. LVN (perfil de volumen)", Order = 60)]
	public MediaColor MediumImpactColor
	{
		get => Lvn.ColorMediano;
		set => Lvn.ColorMediano = value;
	}

	[Display(Name = "Color LVN Bajo Impacto", GroupName = "4. LVN (perfil de volumen)", Order = 61)]
	public MediaColor LowImpactColor
	{
		get => Lvn.ColorBajo;
		set => Lvn.ColorBajo = value;
	}

	[Display(Name = "Grosor Línea Alto Impacto", GroupName = "4. LVN (perfil de volumen)", Order = 62)]
	[Range(1, 6)]
	public int HighImpactLineWidth
	{
		get => Lvn.GrosorAlto;
		set => Lvn.GrosorAlto = value;
	}

	[Display(Name = "Extender Niveles al Borde Derecho", GroupName = "4. LVN (perfil de volumen)", Order = 63)]
	public bool ExtendLinesRight
	{
		get => Lvn.ExtenderDerecha;
		set => Lvn.ExtenderDerecha = value;
	}

	[Display(Name = "Mostrar LVN de bajo impacto", GroupName = "4. LVN (perfil de volumen)", Order = 64)]
	public bool ShowLowImpact
	{
		get => Lvn.MostrarBajoImpacto;
		set => Lvn.MostrarBajoImpacto = value;
	}

	#endregion

	#region Configuración: tablero

	[Display(Name = "Posición X", GroupName = "5. Tablero", Order = 70,
		Description = "También se puede mover arrastrando el tablero con el ratón por su barra de título.")]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "5. Tablero", Order = 71)]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 60;

	#endregion

	public IndicadorCombinado()
		: base(true)
	{
		// La serie por defecto no se usa: se oculta para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		foreach (var serie in Footprint.Series)
			DataSeries.Add(serie);

		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		// Solo calculan los módulos encendidos.
		if (_mostrarTendencia)
			Tendencia.Calcular(bar);

		if (_mostrarCvd)
			Cvd.Calcular(bar);

		if (_mostrarFootprint)
			Footprint.Calcular(bar);

		if (_mostrarLvn)
			Lvn.Calcular(bar);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		// Primero lo que va sobre el gráfico (las líneas de LVN debajo de todo), después el tablero encima.
		if (_mostrarLvn)
			Lvn.DibujarEnGrafico(context);

		if (_mostrarCvd)
			Cvd.DibujarEnGrafico(context);

		DibujarTablero(context);
	}

	/// <summary>Un solo tablero con los paneles de los módulos encendidos, apilados y con el mismo ancho.</summary>
	private void DibujarTablero(RenderContext context)
	{
		var secciones = new List<(int Ancho, int Alto, Action<int, int, int> Dibujar)>();

		if (_mostrarTendencia)
			secciones.Add((MotorTendencia.AnchoMinimo, Tendencia.AltoPanel, (x, y, ancho) => Tendencia.DibujarPanel(context, x, y, ancho)));

		if (_mostrarCvd)
			secciones.Add((MotorCvd.AnchoMinimo, MotorCvd.AltoPanel, (x, y, ancho) => Cvd.DibujarPanel(context, x, y, ancho)));

		if (_mostrarFootprint && Footprint.MostrarPanel)
			secciones.Add((MotorFootprint.AnchoMinimo, MotorFootprint.AltoPanel, (x, y, ancho) => Footprint.DibujarPanel(context, x, y, ancho)));

		if (_mostrarLvn)
			secciones.Add((MotorLvn.AnchoMinimo, MotorLvn.AltoPanel, (x, y, ancho) => Lvn.DibujarPanel(context, x, y, ancho)));

		if (secciones.Count == 0)
		{
			_arrastre.Ocultar();
			return;
		}

		var anchoTablero = secciones.Max(s => s.Ancho);
		var altoTablero = AltoTitulo + secciones.Sum(s => Separacion + s.Alto) + Separacion;
		var tablero = _arrastre.Ubicar(PanelX, PanelY, anchoTablero, altoTablero, AltoTitulo, ((IGrafico)this).Area);

		context.FillRectangle(Color.FromArgb(220, 12, 14, 18), tablero);
		context.FillRectangle(Color.FromArgb(245, 40, 46, 58), new Rectangle(tablero.X, tablero.Y, anchoTablero, AltoTitulo));
		context.DrawRectangle(_bordeTablero, tablero);
		context.DrawString("PANEL DE INDICADORES", _fuenteTitulo, Color.FromArgb(220, 228, 240),
			new Rectangle(tablero.X + 10, tablero.Y, anchoTablero - 20, AltoTitulo), _alinearIzquierda);

		var y = tablero.Y + AltoTitulo;

		foreach (var (_, alto, dibujar) in secciones)
		{
			y += Separacion;
			dibujar(tablero.X, y, anchoTablero);
			y += alto;
		}
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

	#region Datos para los motores (IGrafico)

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
