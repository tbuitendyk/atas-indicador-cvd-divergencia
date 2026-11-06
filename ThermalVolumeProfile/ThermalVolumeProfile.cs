namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators;

using OFT.Attributes;
using OFT.Attributes.Editors;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using Termico;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StdCursor = OFT.Rendering.StdCursor;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Volume Profile térmico (amarillo claro = poco volumen, naranja = medio, rojo oscuro = mucho) con POC, VAH y VAL, y
/// detección de CLÚSTERES de espigas (zona de rebote entre 2 líneas), EDGES, SPIKES y LVN dentro de una zona de
/// búsqueda. Los LVN marcados a mano (amarillos) cuentan como LVN. Funciona en cualquier temporalidad y tipo de
/// gráfico porque solo usa la hora de cada vela. Todo el cálculo está en MotorTermico.cs (sin tipos de ATAS); aquí se
/// eligen las velas, se dibuja y se avisa. Ver LEEME.md y DECISIONES.md.
/// </summary>
// El mismo código se compila también dentro de SuiteOrderFlowStacker (símbolo SUITE) con otro nombre de clase, para que
// ATAS no vea dos indicadores con el mismo nombre de tipo cuando están instaladas las dos DLL.
#if SUITE
[DisplayName("Thermal Volume Profile + LVN (Suite)")]
[Description("Suite Order Flow Stacker: perfil de volumen con color térmico, POC/VA, clústeres de espigas, EDGE, SPIKE y LVN (también manuales).")]
public partial class ThermalVolumeProfileLVN : Indicator
#else
[DisplayName("Thermal Volume Profile (perfil térmico)")]
[Description("Perfil de volumen con color térmico, POC/VA, clústeres de espigas, EDGE, SPIKE y LVN (también manuales) en una zona de búsqueda.")]
public partial class ThermalVolumeProfile : Indicator
#endif
{
	#region Parámetros

	private const string G1 = "1. Rango del perfil";
	private const string G2 = "2. Tipo de perfil";
	private const string G3 = "3. Área de Valor";
	private const string G4 = "4. Térmico";
	private const string G5 = "5. Zona de búsqueda";
	private const string G6 = "6. CLÚSTER";
	private const string G7 = "7. EDGE";
	private const string G8 = "8. SPIKE";
	private const string G9 = "9. LVN";
	private const string G10 = "10. Etiquetas y alertas";
	private const string G11 = "11. Barra de herramientas";

	// --- 1. Rango del perfil ---------------------------------------------------------------------------------------

	private ModosRango _rangeMode = ModosRango.CustomDateTime;
	private DateTime _startDateTime = DateTime.Today.AddDays(-20);
	private DateTime _endDateTime = DateTime.Today.AddDays(1);
	private int _lastNBars = 200;
	private TimeSpan _sessionStart = new(9, 30, 0);
	private TimeSpan _sessionEnd = new(16, 0, 0);
	private ZonasHorariasSesion _sessionTimeZone = ZonasHorariasSesion.NewYork;
	private bool _useCurrentBarAsEnd = true;

	[Display(Name = "Modo del rango", GroupName = G1, Order = 10,
		Description = "Fecha y hora, últimas N velas, rango visible, sesión, día, semana o mes. Con la herramienta RANGO de la barra se elige arrastrando sobre el gráfico.")]
	public ModosRango RangeMode { get => _rangeMode; set => Cambiar(ref _rangeMode, value); }

	[Display(Name = "Desde (hora del gráfico)", GroupName = G1, Order = 20)]
	[Mask(MaskTypes.DateTime, "dd.MM.yyyy HH:mm")]
	public DateTime StartDateTime { get => _startDateTime; set => Cambiar(ref _startDateTime, value); }

	[Display(Name = "Hasta (hora del gráfico)", GroupName = G1, Order = 30, Description = "Se ignora si «Hasta la vela actual» está activo.")]
	[Mask(MaskTypes.DateTime, "dd.MM.yyyy HH:mm")]
	public DateTime EndDateTime { get => _endDateTime; set => Cambiar(ref _endDateTime, value); }

	[Display(Name = "Últimas N velas", GroupName = G1, Order = 40)]
	[Range(1, 1000000)]
	public int LastNBars { get => _lastNBars; set => Cambiar(ref _lastNBars, Math.Max(1, value)); }

	[Display(Name = "Inicio de sesión", GroupName = G1, Order = 50)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm:ss")]
	public TimeSpan SessionStart { get => _sessionStart; set => Cambiar(ref _sessionStart, value); }

	[Display(Name = "Fin de sesión", GroupName = G1, Order = 60)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm:ss")]
	public TimeSpan SessionEnd { get => _sessionEnd; set => Cambiar(ref _sessionEnd, value); }

	[Display(Name = "Horario de sesión en", GroupName = G1, Order = 70, Description = "09:30–16:00 es el horario de Nueva York (con su cambio de horario).")]
	public ZonasHorariasSesion SessionTimeZone { get => _sessionTimeZone; set => Cambiar(ref _sessionTimeZone, value); }

	[Display(Name = "Hasta la vela actual (en vivo)", GroupName = G1, Order = 80)]
	public bool UseCurrentBarAsEnd { get => _useCurrentBarAsEnd; set => Cambiar(ref _useCurrentBarAsEnd, value); }

	// --- 2. Tipo de perfil -----------------------------------------------------------------------------------------

	private TiposPerfil _profileType = TiposPerfil.Volume;
	private int _tpoPeriodMinutes = 30;
	private int _rowSizeTicks = 1;
	private bool _autoRowSize = true;
	private int _maxRows = 300;

	[Display(Name = "Tipo de perfil", GroupName = G2, Order = 10)]
	public TiposPerfil ProfileType { get => _profileType; set => Cambiar(ref _profileType, value); }

	[Display(Name = "Periodo TPO (minutos)", GroupName = G2, Order = 20)]
	[Range(1, 1440)]
	public int TPOPeriodMinutes { get => _tpoPeriodMinutes; set => Cambiar(ref _tpoPeriodMinutes, Math.Max(1, value)); }

	[Display(Name = "Ticks por fila", GroupName = G2, Order = 30)]
	[Range(1, 10000)]
	public int RowSizeTicks { get => _rowSizeTicks; set => Cambiar(ref _rowSizeTicks, Math.Max(1, value)); }

	[Display(Name = "Tamaño de fila automático", GroupName = G2, Order = 40, Description = "Agranda las filas para no pasar del máximo de filas.")]
	public bool AutoRowSize { get => _autoRowSize; set => Cambiar(ref _autoRowSize, value); }

	[Display(Name = "Máximo de filas", GroupName = G2, Order = 50)]
	[Range(10, 5000)]
	public int MaxRows { get => _maxRows; set => Cambiar(ref _maxRows, Math.Max(10, value)); }

	// --- 3. Área de Valor ------------------------------------------------------------------------------------------

	private decimal _valueAreaPercent = 70;

	// No se llama ValueAreaPercent: la clase Indicator de ATAS ya tiene un miembro con ese nombre (aviso CS0108 en la
	// compilación real del 2026-11-02) y ocultarlo puede confundir a la ventana de ajustes de ATAS.
	[Display(Name = "Área de Valor (%)", GroupName = G3, Order = 10)]
	[Range(50, 95)]
	public decimal ProfileValueAreaPercent { get => _valueAreaPercent; set => Cambiar(ref _valueAreaPercent, Math.Clamp(value, 50, 95)); }

	[Display(Name = "Mostrar POC", GroupName = G3, Order = 20)]
	public bool ShowPOC { get; set; } = true;

	[Display(Name = "Mostrar VAH", GroupName = G3, Order = 30)]
	public bool ShowVAH { get; set; } = true;

	[Display(Name = "Mostrar VAL", GroupName = G3, Order = 40)]
	public bool ShowVAL { get; set; } = true;

	[Display(Name = "Color POC", GroupName = G3, Order = 50)]
	public MediaColor POCColor { get; set; } = MediaColor.FromArgb(255, 255, 230, 0);

	[Display(Name = "Color VAH / VAL", GroupName = G3, Order = 60)]
	public MediaColor VAColor { get; set; } = MediaColor.FromArgb(255, 255, 255, 255);

	[Display(Name = "Extender las líneas hasta el borde derecho", GroupName = G3, Order = 70)]
	public bool ExtendLevelsRight { get; set; } = true;

	[Display(Name = "Etiquetas POC / VAH / VAL", GroupName = G3, Order = 80)]
	public bool VALabels { get; set; } = true;

	// --- 4. Térmico ------------------------------------------------------------------------------------------------

	private bool _profileInFront;

	[Display(Name = "Mostrar el perfil", GroupName = G4, Order = 5, Description = "Apagado: solo se ven las líneas de los niveles.")]
	public bool ShowProfile { get; set; } = true;

	[Display(Name = "Perfil delante de las velas", GroupName = G4, Order = 6, Description = "Apagado (por defecto): el perfil queda detrás del precio.")]
	public bool ProfileInFront
	{
		get => _profileInFront;
		set
		{
			// Sin RedrawChart: ATAS asigna los ajustes al cargar, antes de que haya gráfico, y redibuja él solo.
			_profileInFront = value;
			DrawAbovePrice = value;
		}
	}

	[Display(Name = "Paleta", GroupName = G4, Order = 10)]
	public PaletasTermicas ThermalPalette { get; set; } = PaletasTermicas.YellowRed;

	[Display(Name = "Personalizada: color bajo", GroupName = G4, Order = 20)]
	public MediaColor ColorLow { get; set; } = MediaColor.FromArgb(255, 255, 250, 160);

	[Display(Name = "Personalizada: color medio 1", GroupName = G4, Order = 21)]
	public MediaColor ColorMid1 { get; set; } = MediaColor.FromArgb(255, 255, 205, 0);

	[Display(Name = "Personalizada: color medio 2", GroupName = G4, Order = 22)]
	public MediaColor ColorMid2 { get; set; } = MediaColor.FromArgb(255, 255, 120, 0);

	[Display(Name = "Personalizada: color alto", GroupName = G4, Order = 23)]
	public MediaColor ColorHigh { get; set; } = MediaColor.FromArgb(255, 150, 0, 0);

	[Display(Name = "Escala térmica", GroupName = G4, Order = 30)]
	public EscalasTermicas ThermalScale { get; set; } = EscalasTermicas.SquareRoot;

	[Display(Name = "Opacidad del perfil (0-255)", GroupName = G4, Order = 40)]
	[Range(0, 255)]
	public int ProfileOpacity { get; set; } = 180;

	[Display(Name = "Ancho máximo del perfil (% del rango)", GroupName = G4, Order = 50)]
	[Range(1, 100)]
	public int ProfileWidthPercent { get; set; } = 35;

	[Display(Name = "Anclaje del perfil", GroupName = G4, Order = 60)]
	public AnclajesPerfil ProfileAnchor { get; set; } = AnclajesPerfil.LeftOfRange;

	[Display(Name = "Dirección de las barras", GroupName = G4, Order = 70, Description = "Automática: desde el anclaje hacia dentro. También de izquierda a derecha o de derecha a izquierda.")]
	public DireccionesPerfil ProfileDirection { get; set; } = DireccionesPerfil.Auto;

	// --- 5. Zona de búsqueda ---------------------------------------------------------------------------------------

	private bool _useSearchZone;
	private decimal _searchZoneHigh;
	private decimal _searchZoneLow;
	private bool _useTimeFilter;
	private DateTime _searchStartTime = DateTime.Today.AddDays(-5);
	private DateTime _searchEndTime = DateTime.Today.AddDays(1);

	[Display(Name = "Usar zona de búsqueda", GroupName = G5, Order = 10, Description = "También con la herramienta ZONA de la barra: arrastre un rectángulo sobre el gráfico.")]
	public bool UseSearchZone { get => _useSearchZone; set => Cambiar(ref _useSearchZone, value); }

	[Display(Name = "Precio superior", GroupName = G5, Order = 20, Description = "Los dos precios se aceptan en cualquier orden.")]
	public decimal SearchZoneHigh { get => _searchZoneHigh; set => Cambiar(ref _searchZoneHigh, value); }

	[Display(Name = "Precio inferior", GroupName = G5, Order = 30)]
	public decimal SearchZoneLow { get => _searchZoneLow; set => Cambiar(ref _searchZoneLow, value); }

	[Display(Name = "Filtrar también por tiempo", GroupName = G5, Order = 40, Description = "Los detectores usan un perfil hecho solo con las velas entre las dos fechas.")]
	public bool UseTimeFilter { get => _useTimeFilter; set => Cambiar(ref _useTimeFilter, value); }

	[Display(Name = "Desde (hora del gráfico)", GroupName = G5, Order = 50)]
	[Mask(MaskTypes.DateTime, "dd.MM.yyyy HH:mm")]
	public DateTime SearchStartTime { get => _searchStartTime; set => Cambiar(ref _searchStartTime, value); }

	[Display(Name = "Hasta (hora del gráfico)", GroupName = G5, Order = 60)]
	[Mask(MaskTypes.DateTime, "dd.MM.yyyy HH:mm")]
	public DateTime SearchEndTime { get => _searchEndTime; set => Cambiar(ref _searchEndTime, value); }

	[Display(Name = "Dibujar la zona (rectángulo gris)", GroupName = G5, Order = 70)]
	public bool ShowSearchZone { get; set; } = true;

	// --- 6. CLÚSTER ------------------------------------------------------------------------------------------------

	private readonly ParametrosTermicos _p = new();

	[Display(Name = "Activar clústeres", GroupName = G6, Order = 10)]
	public bool EnableCluster { get => _p.EnableCluster; set { _p.EnableCluster = value; Recalcular(); } }

	[Display(Name = "Usar la zona de búsqueda", GroupName = G6, Order = 15)]
	public bool ClusterUseZone { get => _p.ClusterUseZone; set { _p.ClusterUseZone = value; Recalcular(); } }

	[Display(Name = "n mínimo de una espiga", GroupName = G6, Order = 20, Description = "Volumen de la fila / volumen del POC.")]
	[Range(0, 1)]
	public decimal ClusterSpikeMinN { get => (decimal)_p.ClusterSpikeMinN; set { _p.ClusterSpikeMinN = (double)value; Recalcular(); } }

	[Display(Name = "Veces la media de sus 5 + 5 vecinas", GroupName = G6, Order = 30)]
	[Range(1, 100)]
	public decimal ClusterSpikeRatio { get => (decimal)_p.ClusterSpikeRatio; set { _p.ClusterSpikeRatio = (double)value; Recalcular(); } }

	[Display(Name = "Distancia máxima entre espigas (ticks)", GroupName = G6, Order = 40)]
	[Range(1, 100000)]
	public int ClusterGapTicks { get => _p.ClusterGapTicks; set { _p.ClusterGapTicks = value; Recalcular(); } }

	[Display(Name = "Mínimo de espigas", GroupName = G6, Order = 50)]
	[Range(2, 1000)]
	public int MinSpikesInCluster { get => _p.MinSpikesInCluster; set { _p.MinSpikesInCluster = value; Recalcular(); } }

	[Display(Name = "Altura mínima (ticks)", GroupName = G6, Order = 60)]
	[Range(0, 100000)]
	public int MinClusterHeightTicks { get => _p.MinClusterHeightTicks; set { _p.MinClusterHeightTicks = value; Recalcular(); } }

	[Display(Name = "Ajustar al EDGE a (filas)", GroupName = G6, Order = 70)]
	[Range(0, 1000)]
	public int EdgeSnapRows { get => _p.EdgeSnapRows; set { _p.EdgeSnapRows = value; Recalcular(); } }

	[Display(Name = "Máximo de clústeres", GroupName = G6, Order = 80)]
	[Range(0, 100)]
	public int MaxClusters { get => _p.MaxClusters; set { _p.MaxClusters = value; Recalcular(); } }

	[Display(Name = "Color de las 2 líneas", GroupName = G6, Order = 90)]
	public MediaColor ClusterLineColor { get; set; } = MediaColor.FromArgb(255, 230, 30, 30);

	[Display(Name = "Grosor de las líneas", GroupName = G6, Order = 100)]
	[Range(1, 10)]
	public int ClusterLineWidth { get; set; } = 2;

	[Display(Name = "Rectángulo entre las líneas", GroupName = G6, Order = 110)]
	public bool ShowClusterBox { get; set; } = true;

	[Display(Name = "Opacidad del rectángulo (0-255)", GroupName = G6, Order = 120)]
	[Range(0, 255)]
	public int ClusterBoxOpacity { get; set; } = 40;

	[Display(Name = "Mostrar cPOC (línea verde)", GroupName = G6, Order = 130)]
	public bool ShowClusterPOC { get; set; } = true;

	[Display(Name = "Color cPOC", GroupName = G6, Order = 140)]
	public MediaColor ClusterPOCColor { get; set; } = MediaColor.FromArgb(255, 0, 200, 83);

	[Display(Name = "Etiquetas de clúster", GroupName = G6, Order = 150)]
	public bool ClusterLabels { get; set; } = true;

	// --- 7. EDGE ---------------------------------------------------------------------------------------------------

	[Display(Name = "Activar EDGE", GroupName = G7, Order = 10)]
	public bool EnableEdge { get => _p.EnableEdge; set { _p.EnableEdge = value; Recalcular(); } }

	[Display(Name = "Usar la zona de búsqueda", GroupName = G7, Order = 15)]
	public bool EdgeUseZone { get => _p.EdgeUseZone; set { _p.EdgeUseZone = value; Recalcular(); } }

	[Display(Name = "n mínimo de un HVN", GroupName = G7, Order = 20)]
	[Range(0, 1)]
	public decimal EdgeHvnThreshold { get => (decimal)_p.EdgeHvnThreshold; set { _p.EdgeHvnThreshold = (double)value; Recalcular(); } }

	[Display(Name = "Unir HVN separados por menos de (ticks)", GroupName = G7, Order = 30)]
	[Range(0, 100000)]
	public int EdgeMergeGapTicks { get => _p.EdgeMergeGapTicks; set { _p.EdgeMergeGapTicks = value; Recalcular(); } }

	[Display(Name = "Altura mínima del HVN (ticks)", GroupName = G7, Order = 40)]
	[Range(0, 100000)]
	public int EdgeMinHvnHeightTicks { get => _p.EdgeMinHvnHeightTicks; set { _p.EdgeMinHvnHeightTicks = value; Recalcular(); } }

	[Display(Name = "Ventana W (filas)", GroupName = G7, Order = 50)]
	[Range(1, 100)]
	public int EdgeWindowRows { get => _p.EdgeWindowRows; set { _p.EdgeWindowRows = value; Recalcular(); } }

	[Display(Name = "Caída mínima (fracción del POC)", GroupName = G7, Order = 60)]
	[Range(0, 1)]
	public decimal EdgeMinStep { get => (decimal)_p.EdgeMinStep; set { _p.EdgeMinStep = (double)value; Recalcular(); } }

	[Display(Name = "Caída mínima por fila", GroupName = G7, Order = 70, Description = "Cuanto más alta, más horizontal es el escalón.")]
	[Range(0, 1)]
	public decimal EdgeMinSlope { get => (decimal)_p.EdgeMinSlope; set { _p.EdgeMinSlope = (double)value; Recalcular(); } }

	[Display(Name = "Filas de confirmación K", GroupName = G7, Order = 80)]
	[Range(1, 100)]
	public int EdgeConfirmRows { get => _p.EdgeConfirmRows; set { _p.EdgeConfirmRows = value; Recalcular(); } }

	[Display(Name = "n medio máximo después del borde", GroupName = G7, Order = 90)]
	[Range(0, 1)]
	public decimal EdgeLowAfter { get => (decimal)_p.EdgeLowAfter; set { _p.EdgeLowAfter = (double)value; Recalcular(); } }

	[Display(Name = "Suavizado (sigma, filas)", GroupName = G7, Order = 100, Description = "Ligero. Uno fuerte convierte el escalón en rampa.")]
	[Range(0, 20)]
	public decimal EdgeSmoothSigma { get => (decimal)_p.EdgeSmoothSigma; set { _p.EdgeSmoothSigma = (double)value; Recalcular(); } }

	[Display(Name = "Máximo de EDGE", GroupName = G7, Order = 110)]
	[Range(0, 100)]
	public int MaxEdges { get => _p.MaxEdges; set { _p.MaxEdges = value; Recalcular(); } }

	[Display(Name = "Color", GroupName = G7, Order = 120)]
	public MediaColor EdgeColor { get; set; } = MediaColor.FromArgb(255, 255, 140, 0);

	[Display(Name = "Etiquetas de EDGE", GroupName = G7, Order = 130)]
	public bool EdgeLabels { get; set; } = true;

	// --- 8. SPIKE --------------------------------------------------------------------------------------------------

	[Display(Name = "Activar SPIKE", GroupName = G8, Order = 10)]
	public bool EnableSpike { get => _p.EnableSpike; set { _p.EnableSpike = value; Recalcular(); } }

	[Display(Name = "Usar la zona de búsqueda", GroupName = G8, Order = 15)]
	public bool SpikeUseZone { get => _p.SpikeUseZone; set { _p.SpikeUseZone = value; Recalcular(); } }

	[Display(Name = "n mínimo", GroupName = G8, Order = 20)]
	[Range(0, 1)]
	public decimal SpikeMinN { get => (decimal)_p.SpikeMinN; set { _p.SpikeMinN = (double)value; Recalcular(); } }

	[Display(Name = "Veces la media de sus vecinas", GroupName = G8, Order = 30)]
	[Range(1, 100)]
	public decimal SpikeRatio { get => (decimal)_p.SpikeRatio; set { _p.SpikeRatio = (double)value; Recalcular(); } }

	[Display(Name = "Ancho máximo (filas)", GroupName = G8, Order = 40)]
	[Range(1, 50)]
	public int SpikeMaxWidthRows { get => _p.SpikeMaxWidthRows; set { _p.SpikeMaxWidthRows = value; Recalcular(); } }

	[Display(Name = "Ocultar las espigas de un clúster", GroupName = G8, Order = 50)]
	public bool HideSpikesInCluster { get => _p.HideSpikesInCluster; set { _p.HideSpikesInCluster = value; Recalcular(); } }

	[Display(Name = "Máximo de espigas", GroupName = G8, Order = 60)]
	[Range(0, 100)]
	public int MaxSpikes { get => _p.MaxSpikes; set { _p.MaxSpikes = value; Recalcular(); } }

	[Display(Name = "Color (línea punteada)", GroupName = G8, Order = 70)]
	public MediaColor SpikeColor { get; set; } = MediaColor.FromArgb(255, 255, 0, 255);

	[Display(Name = "Etiquetas de SPIKE", GroupName = G8, Order = 80)]
	public bool SpikeLabels { get; set; } = true;

	// --- 9. LVN ----------------------------------------------------------------------------------------------------

	private string _manualLvnPrices = "";

	[Display(Name = "Activar LVN automáticos", GroupName = G9, Order = 10)]
	public bool EnableLVN { get => _p.EnableLVN; set { _p.EnableLVN = value; Recalcular(); } }

	[Display(Name = "Usar la zona de búsqueda", GroupName = G9, Order = 15)]
	public bool LVNUseZone { get => _p.LVNUseZone; set { _p.LVNUseZone = value; Recalcular(); } }

	[Display(Name = "Suavizado (ticks)", GroupName = G9, Order = 20, Description = "Gaussiano con sigma = valor / 2.")]
	[Range(0, 1000)]
	public decimal LVNSmoothTicks { get => (decimal)_p.LVNSmoothTicks; set { _p.LVNSmoothTicks = (double)value; Recalcular(); } }

	[Display(Name = "Prominencia mínima (%)", GroupName = G9, Order = 30)]
	[Range(0, 100)]
	public decimal LVNMinProminence { get => (decimal)_p.LVNMinProminence; set { _p.LVNMinProminence = (double)value; Recalcular(); } }

	[Display(Name = "Valle máximo (% del POC)", GroupName = G9, Order = 40)]
	[Range(0, 100)]
	public decimal LVNMaxPercentOfPOC { get => (decimal)_p.LVNMaxPercentOfPOC; set { _p.LVNMaxPercentOfPOC = (double)value; Recalcular(); } }

	[Display(Name = "Separación mínima (ticks)", GroupName = G9, Order = 50)]
	[Range(0, 100000)]
	public int LVNMinDistanceTicks { get => _p.LVNMinDistanceTicks; set { _p.LVNMinDistanceTicks = value; Recalcular(); } }

	[Display(Name = "Dibujo", GroupName = G9, Order = 60)]
	public DibujosLvn LVNDisplay { get; set; } = DibujosLvn.Line;

	[Display(Name = "Máximo de LVN automáticos", GroupName = G9, Order = 70)]
	[Range(0, 100)]
	public int MaxLVN { get => _p.MaxLVN; set { _p.MaxLVN = value; Recalcular(); } }

	[Display(Name = "Color", GroupName = G9, Order = 80)]
	public MediaColor LVNColor { get; set; } = MediaColor.FromArgb(255, 0, 229, 255);

	[Display(Name = "Etiquetas de LVN", GroupName = G9, Order = 90)]
	public bool LVNLabels { get; set; } = true;

	[Display(Name = "Mostrar LVN manuales (amarillos)", GroupName = G9, Order = 100, Description = "Se marcan con la herramienta LVN de la barra: un clic añade una línea, un clic sobre ella la quita.")]
	public bool ShowManualLVN { get => _p.ShowManualLVN; set { _p.ShowManualLVN = value; Recalcular(); } }

	[Display(Name = "LVN manuales (precios separados por ;)", GroupName = G9, Order = 110)]
	public string ManualLVNPrices { get => _manualLvnPrices; set => Cambiar(ref _manualLvnPrices, value ?? ""); }

	[Display(Name = "Color LVN manual", GroupName = G9, Order = 120)]
	public MediaColor ManualLVNColor { get; set; } = MediaColor.FromArgb(255, 255, 239, 0);

	// --- 10. Etiquetas y alertas -----------------------------------------------------------------------------------

	[Display(Name = "Mostrar etiquetas (borde derecho)", GroupName = G10, Order = 10)]
	public bool ShowLabels { get; set; } = true;

	[Display(Name = "Tamaño de la letra", GroupName = G10, Order = 20)]
	[Range(6, 30)]
	public int LabelFontSize { get; set; } = 10;

	[Display(Name = "Alerta al tocar un CLÚSTER", GroupName = G10, Order = 30)]
	public bool AlertOnCluster { get; set; }

	[Display(Name = "Alerta al tocar un EDGE", GroupName = G10, Order = 40)]
	public bool AlertOnEdge { get; set; }

	[Display(Name = "Alerta al tocar un SPIKE", GroupName = G10, Order = 50)]
	public bool AlertOnSpike { get; set; }

	[Display(Name = "Alerta al tocar un LVN (también manual)", GroupName = G10, Order = 60)]
	public bool AlertOnLVN { get; set; }

	[Display(Name = "Alerta al tocar el POC", GroupName = G10, Order = 70)]
	public bool AlertOnPOC { get; set; }

	[Display(Name = "Alerta al tocar VAH / VAL", GroupName = G10, Order = 80)]
	public bool AlertOnVA { get; set; }

	[Display(Name = "Sonido de la alerta", GroupName = G10, Order = 90)]
	[SoundComboBoxEditor]
	public string AlertSoundFile { get; set; } = "alert1";

	// --- 11. Barra de herramientas ---------------------------------------------------------------------------------

	[Display(Name = "Mostrar la barra (PERFIL, RANGO, ZONA, LVN)", GroupName = G11, Order = 10)]
	public bool ShowToolbar { get; set; } = true;

	[Display(Name = "Posición X", GroupName = G11, Order = 20)]
	[Range(0, 10000)]
	public int ToolbarX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = G11, Order = 30)]
	[Range(0, 10000)]
	public int ToolbarY { get; set; } = 130;

	#endregion

	#region Estado

	private readonly object _sync = new();
	private readonly PerfilIncremental _principal = new();
	private readonly PerfilIncremental _filtro = new();
	private readonly HashSet<string> _alertadas = new();
	private ResultadoTermico? _resultado;
	private (int Desde, int Hasta) _rango = (-1, -1);
	private int _ultimaBarra = -1;
	private int _barraAlertas = -1;
	private bool _enVivo;
	private bool _avisoSinNiveles;
	private DateTime _ultimoAnalisis = DateTime.MinValue;
	private (int Primera, int Ultima) _visibles = (-1, -1);
	private string _estado = "Calculando...";

	#endregion

#if SUITE
	public ThermalVolumeProfileLVN()
#else
	public ThermalVolumeProfile()
#endif
		: base(true)
	{
		// Solo dibuja: la serie por defecto se oculta para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		Panel = IndicatorDataProvider.CandlesPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = false;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	private void Cambiar<T>(ref T campo, T valor)
	{
		campo = valor;
		Recalcular();
	}

	private void Recalcular()
	{
		lock (_sync)
			_principal.Sucio = _filtro.Sucio = true;

		RecalculateValues();
	}

	#region Perfil

	/// <summary>Acumulado de las velas cerradas de un rango; en vivo solo se rehace la vela abierta.</summary>
	private sealed class PerfilIncremental
	{
		public AcumuladorTicks? Base;
		public int Desde = -1;
		public int IncluidasHasta = -1;
		public bool Sucio = true;

		public void Limpiar()
		{
			Base = null;
			Desde = IncluidasHasta = -1;
			Sucio = true;
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			lock (_sync)
			{
				if (bar == 0)
					Reiniciar();

				// Durante la carga del historial no se calcula nada: todo se hace una sola vez en la última vela.
				if (bar < CurrentBar - 1)
					return;

#if SUITE
				SuiteLatido();
#endif

				Actualizar(bar, false);
			}
		}
		catch (Exception ex)
		{
			_estado = "ERROR DE CALCULO: " + ex.Message;
			this.LogError("ThermalVolumeProfile: error de cálculo", ex);
		}
	}

	/// <summary>ATAS vuelve a calcular desde la vela 0 al cambiar de instrumento, de temporalidad o un ajuste.</summary>
	private void Reiniciar()
	{
#if SUITE
		SuiteAlReiniciar();
#endif
		_principal.Limpiar();
		_filtro.Limpiar();
		_resultado = null;
		_rango = (-1, -1);
		_ultimaBarra = -1;
		_enVivo = false;
		_alertadas.Clear();
		_barraAlertas = -1;
		_ultimoAnalisis = DateTime.MinValue;
	}

	/// <summary>Calcula el perfil y los niveles con la última vela 'ultima' (forzar: desde el dibujo, rango visible).</summary>
	private void Actualizar(int ultima, bool forzar)
	{
		var nuevaBarra = ultima != _ultimaBarra;

		// En vivo, con la misma vela abierta, se recalcula como mucho 4 veces por segundo.
		if (!nuevaBarra && !forzar && !_principal.Sucio && (DateTime.UtcNow - _ultimoAnalisis).TotalMilliseconds < 250)
			return;

		_ultimaBarra = ultima;

		if (InstrumentInfo is not { TickSize: > 0 } instrumento)
		{
			_resultado = null;
			_estado = "SIN DATOS DEL INSTRUMENTO (tick)";
			return;
		}

		var total = ultima + 1;
		var rango = Rangos.Resolver(ParametrosDeRango(instrumento.TimeZoneOffset), Tiempo, total);

		if (rango is not { } r)
		{
			_resultado = null;
			_rango = (-1, -1);
			_estado = "RANGO VACIO: no hay velas en el rango elegido";
			return;
		}

		_rango = r;
		var perfil = Construir(_principal, r, ultima, instrumento.TickSize);

		if (perfil is null)
		{
			_resultado = null;
			_estado = "RANGO SIN VOLUMEN";
			return;
		}

		// Perfil secundario de los detectores: solo con las velas del filtro de tiempo.
		Perfil? detectores = null;
		var conFiltro = _useSearchZone && _useTimeFilter;

		if (conFiltro)
		{
			var desde = Rangos.PrimeraDesde(Tiempo, total, _searchStartTime - instrumento.TimeZoneOffset);
			var hasta = Math.Min(ultima, Rangos.UltimaHasta(Tiempo, total, _searchEndTime - instrumento.TimeZoneOffset));
			detectores = desde <= hasta ? Construir(_filtro, (desde, hasta), ultima, instrumento.TickSize) : null;
		}

		ZonaBusqueda? zona = _useSearchZone && (_searchZoneHigh != 0 || _searchZoneLow != 0)
			? ZonaBusqueda.Crear(_searchZoneLow, _searchZoneHigh)
			: null;

		_p.ValueAreaPercent = (double)_valueAreaPercent;
		var resultado = conFiltro && detectores is null
			? MotorTermico.Analizar(perfil, new Perfil { Tick = perfil.Tick, TicksPorFila = perfil.TicksPorFila }, _p, zona, Utilidades.LeerPrecios(_manualLvnPrices))
			: MotorTermico.Analizar(perfil, detectores, _p, zona, Utilidades.LeerPrecios(_manualLvnPrices));

		_resultado = resultado;
		_ultimoAnalisis = DateTime.UtcNow;
#if SUITE
		SuitePublicarLvn(resultado);
#endif
		_estado = $"{perfil.Velas} velas  {perfil.Filas} filas de {perfil.TicksPorFila} ticks{(conFiltro && detectores is null ? "  FILTRO DE TIEMPO SIN VELAS" : "")}";

		// Alertas: solo en vivo; lo que la vela actual ya tocaba al terminar la carga no suena.
		if (!_enVivo)
		{
			RevisarAlertas(ultima, false);
			_enVivo = true;
		}
		else
		{
			RevisarAlertas(ultima, true);
		}
	}

	/// <summary>Perfil de [desde, hasta]: las velas cerradas se suman una vez; la abierta se rehace en cada actualización.</summary>
	private Perfil? Construir(PerfilIncremental p, (int Desde, int Hasta) r, int ultima, decimal tick)
	{
		var cerradasHasta = Math.Min(r.Hasta, ultima - 1);

		if (p.Sucio || p.Base is null || p.Desde != r.Desde || cerradasHasta < p.IncluidasHasta)
		{
			p.Base = new AcumuladorTicks(tick, _tpoPeriodMinutes);
			p.Desde = r.Desde;
			p.IncluidasHasta = r.Desde - 1;
			p.Sucio = false;
		}

		// Velas que se cerraron desde la última vez: se leen con sus valores finales.
		for (var i = p.IncluidasHasta + 1; i <= cerradasHasta; i++)
		{
			if (!p.Base.Agregar(Vela(i)))
				AvisarSinNiveles();
		}

		p.IncluidasHasta = Math.Max(p.IncluidasHasta, cerradasHasta);

		Dictionary<long, AcumuladorTicks.Celda>? abierta = null;

		if (r.Hasta == ultima)
		{
			abierta = p.Base.Aportacion(Vela(ultima), out var conNiveles);

			if (!conNiveles)
				AvisarSinNiveles();
		}

		return Perfil.Construir(p.Base, abierta, _profileType, _rowSizeTicks, _autoRowSize, _maxRows);
	}

	private VelaPerfil Vela(int bar)
	{
		var c = GetCandle(bar);
		var v = new VelaPerfil { Time = c.Time, High = c.High, Low = c.Low, Volume = c.Volume, Delta = c.Delta };
		var niveles = c.GetAllPriceLevels();

		if (niveles is not null)
		{
			foreach (var n in niveles)
				v.Niveles.Add(new NivelPrecio(n.Price, n.Volume, n.Bid, n.Ask));
		}

		return v;
	}

	private DateTime Tiempo(int bar) => GetCandle(bar).Time;

	private ParametrosRango ParametrosDeRango(TimeSpan offset) => new()
	{
		Modo = _rangeMode,
		Desde = _startDateTime,
		Hasta = _endDateTime,
		LastNBars = _lastNBars,
		SessionStart = _sessionStart,
		SessionEnd = _sessionEnd,
		ZonaSesion = _sessionTimeZone,
		OffsetGrafico = offset,
		UseCurrentBarAsEnd = _useCurrentBarAsEnd,
		PrimeraVisible = FirstVisibleBarNumber,
		UltimaVisible = LastVisibleBarNumber
	};

	private void AvisarSinNiveles()
	{
		if (_avisoSinNiveles)
			return;

		_avisoSinNiveles = true;
		this.LogWarn("ThermalVolumeProfile: hay velas sin volumen por precio; su volumen se reparte por igual entre su High y su Low.");
	}

	#endregion

	#region Render

	private readonly RenderStringFormat _centro = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	private readonly RenderStringFormat _izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderFont _fuenteBarra = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteEstado = new("Arial", 8f, FontStyle.Regular);
	private readonly Arrastre _arrastre = new();
	private readonly List<Etiqueta> _etiquetas = new();
	private Estilo? _estilo;
	private int _firmaEstilo;

	/// <summary>Plumas, fuente y colores térmicos: se crean una vez y solo se rehacen si cambian los ajustes.</summary>
	private sealed class Estilo
	{
		public Color[] Termico = Array.Empty<Color>();
		public RenderFont Fuente = null!;
		public RenderPen Poc = null!, Va = null!, Cluster = null!, ClusterPoc = null!, Edge = null!, Spike = null!, Lvn = null!, LvnManual = null!, Zona = null!;
		public Color CajaCluster, ZonaRelleno, FranjaLvn, FondoEtiqueta;
	}

	private readonly record struct Etiqueta(int Y, string Texto, Color Color);

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		try
		{
			Dibujar(context);
		}
		catch (Exception ex)
		{
			this.LogError("ThermalVolumeProfile: error de dibujo", ex);
		}
	}

	private Rectangle Area => ChartInfo?.PriceChartContainer.Region ?? Rectangle.Empty;

	private int Y(decimal precio) => ChartInfo!.GetYByPrice(precio, false);

	private int AnchoBarra => Math.Max(1, (int)ChartInfo!.PriceChartContainer.BarsWidth);

	private void Dibujar(RenderContext context)
	{
		if (ChartInfo is null || InstrumentInfo is not { TickSize: > 0 })
			return;

		RevisarRangoVisible();

		var area = Area;
		var estilo = Estilos();
		ResultadoTermico? r;
		(int Desde, int Hasta) rango;

		lock (_sync)
			(r, rango) = (_resultado, _rango);

		_etiquetas.Clear();

		if (r is not null && rango.Desde >= 0)
		{
			var xDesde = ChartInfo.GetXByBar(rango.Desde);
			var xHasta = ChartInfo.GetXByBar(rango.Hasta) + AnchoBarra;
			var xFinLineas = ExtendLevelsRight ? area.Right : xHasta;

			DibujarZona(context, estilo, area, xDesde, xFinLineas);

			if (ShowProfile)
				DibujarPerfil(context, r.Perfil, estilo, area, xDesde, xHasta);

			DibujarNiveles(context, r, estilo, Math.Max(area.Left, xDesde), xFinLineas);

			if (ShowLabels)
				DibujarEtiquetas(context, estilo, area);
		}

		DibujarSeleccion(context, area);

		if (ShowToolbar)
			DibujarBarra(context, area);
		else
			_arrastre.Ocultar();
	}

	/// <summary>En el modo «rango visible» el perfil sigue a la pantalla: se recalcula al desplazar el gráfico.</summary>
	private void RevisarRangoVisible()
	{
		if (_rangeMode != ModosRango.VisibleRange)
			return;

		var visibles = (FirstVisibleBarNumber, LastVisibleBarNumber);

		lock (_sync)
		{
			if (visibles == _visibles || _ultimaBarra < 0)
				return;

			_visibles = visibles;
			_principal.Sucio = true;
			Actualizar(_ultimaBarra, true);
		}
	}

	private Estilo Estilos()
	{
		var h = new HashCode();
		h.Add(ThermalPalette);
		h.Add(ThermalScale);
		h.Add(ProfileOpacity);
		h.Add(ColorLow);
		h.Add(ColorMid1);
		h.Add(ColorMid2);
		h.Add(ColorHigh);
		h.Add(LabelFontSize);
		h.Add(POCColor);
		h.Add(VAColor);
		h.Add(ClusterLineColor);
		h.Add(ClusterLineWidth);
		h.Add(ClusterBoxOpacity);
		h.Add(ClusterPOCColor);
		h.Add(EdgeColor);
		h.Add(SpikeColor);
		h.Add(LVNColor);
		h.Add(ManualLVNColor);
		var firma = h.ToHashCode();

		if (_estilo is not null && firma == _firmaEstilo)
			return _estilo;

		var personalizada = new[] { ColorLow, ColorMid1, ColorMid2, ColorHigh }.Select(c => new ColorArgb(c.A, c.R, c.G, c.B)).ToArray();
		var cuatro = PaletaTermica.Colores(ThermalPalette, personalizada);

		_estilo = new Estilo
		{
			Termico = PaletaTermica.Cache(cuatro, ThermalScale, (byte)Math.Clamp(ProfileOpacity, 0, 255)).Select(c => Color.FromArgb(c.A, c.R, c.G, c.B)).ToArray(),
			Fuente = new RenderFont("Arial", Math.Clamp(LabelFontSize, 6, 30)),
			Poc = new RenderPen(ADibujo(POCColor), 2),
			Va = new RenderPen(ADibujo(VAColor), 1),
			Cluster = new RenderPen(ADibujo(ClusterLineColor), Math.Clamp(ClusterLineWidth, 1, 10)),
			ClusterPoc = new RenderPen(ADibujo(ClusterPOCColor), 1),
			Edge = new RenderPen(ADibujo(EdgeColor), 2),
			Spike = new RenderPen(ADibujo(SpikeColor), 1),
			Lvn = new RenderPen(ADibujo(LVNColor), 1),
			LvnManual = new RenderPen(ADibujo(ManualLVNColor), 2),
			Zona = new RenderPen(Color.FromArgb(150, 160, 160, 160), 1),
			CajaCluster = ADibujo(ClusterLineColor, Math.Clamp(ClusterBoxOpacity, 0, 255)),
			ZonaRelleno = Color.FromArgb(45, 150, 150, 150),
			FranjaLvn = ADibujo(LVNColor, 60),
			FondoEtiqueta = Color.FromArgb(200, 18, 20, 26)
		};
		_firmaEstilo = firma;
		return _estilo;
	}

	private static Color ADibujo(MediaColor c, int alfa = -1) => Color.FromArgb(alfa < 0 ? c.A : alfa, c.R, c.G, c.B);

	private void DibujarZona(RenderContext context, Estilo estilo, Rectangle area, int xDesde, int xHasta)
	{
		if (!ShowSearchZone || !_useSearchZone || (_searchZoneHigh == 0 && _searchZoneLow == 0))
			return;

		var zona = ZonaBusqueda.Crear(_searchZoneLow, _searchZoneHigh);
		var yArriba = Y(zona.Alta);
		var yAbajo = Y(zona.Baja);

		// Con filtro de tiempo, el rectángulo cubre solo esas velas.
		int x1 = area.Left, x2 = area.Right;

		if (_useTimeFilter && InstrumentInfo is { } info && CurrentBar > 0)
		{
			var total = CurrentBar;
			var desde = Rangos.PrimeraDesde(Tiempo, total, _searchStartTime - info.TimeZoneOffset);
			var hasta = Rangos.UltimaHasta(Tiempo, total, _searchEndTime - info.TimeZoneOffset);

			if (desde <= hasta && desde < total && hasta >= 0)
				(x1, x2) = (ChartInfo!.GetXByBar(desde), ChartInfo!.GetXByBar(hasta) + AnchoBarra);
		}

		var caja = new Rectangle(x1, Math.Min(yArriba, yAbajo), Math.Max(1, x2 - x1), Math.Max(1, Math.Abs(yAbajo - yArriba)));
		context.FillRectangle(estilo.ZonaRelleno, caja);
		context.DrawRectangle(estilo.Zona, caja);
	}

	/// <summary>Una barra por fila, solo las filas visibles, con el color térmico de la caché.</summary>
	private void DibujarPerfil(RenderContext context, Perfil p, Estilo estilo, Rectangle area, int xDesde, int xHasta)
	{
		if (p.Filas == 0)
			return;

		var maximo = p.Valor.Max();

		if (maximo <= 0)
			return;

		var (x0, dir, anchoMaximo) = Geometria(area, xDesde, xHasta);
		var altoTick = Math.Max(1, Math.Abs(Y(p.PrecioFila(0)) - Y(p.PrecioFila(0) + p.Tick)));

		// Filas visibles: de la que está en el borde inferior a la del superior.
		var primera = Math.Max(0, p.FilaDePrecio(PrecioDeY(area.Bottom)) - 1);
		var ultima = Math.Min(p.Filas - 1, p.FilaDePrecio(PrecioDeY(area.Top)) + 1);

		for (var i = primera; i <= ultima; i++)
		{
			var n = p.Valor[i] / maximo;
			var largo = (int)(n * anchoMaximo);

			if (largo < 1)
				continue;

			var yArriba = Y(p.PrecioFila(i) + (p.TicksPorFila - 1) * p.Tick) - altoTick / 2;
			var yAbajo = Y(p.PrecioFila(i)) + (altoTick + 1) / 2;
			var rect = new Rectangle(dir > 0 ? x0 : x0 - largo, yArriba, largo, Math.Max(1, yAbajo - yArriba));
			context.FillRectangle(estilo.Termico[PaletaTermica.Indice(n)], rect);

			// Perfil Delta: franja con el signo (verde = más compras, rojo = más ventas) junto al anclaje.
			if (p.Tipo == TiposPerfil.Delta && p.DeltaFila[i] != 0)
			{
				var signo = p.DeltaFila[i] > 0 ? Color.FromArgb(220, 0, 200, 83) : Color.FromArgb(220, 255, 60, 60);
				context.FillRectangle(signo, new Rectangle(dir > 0 ? x0 : x0 - 3, rect.Y, 3, rect.Height));
			}
		}
	}

	/// <summary>Origen de las barras, sentido (+1 hacia la derecha) y largo máximo en píxeles.</summary>
	private (int X0, int Dir, int AnchoMaximo) Geometria(Rectangle area, int xDesde, int xHasta)
	{
		var enPantalla = ProfileAnchor is AnclajesPerfil.LeftOfScreen or AnclajesPerfil.RightOfScreen;
		var base100 = enPantalla ? area.Width : Math.Max(30, xHasta - xDesde);
		var anchoMaximo = Math.Max(10, base100 * Math.Clamp(ProfileWidthPercent, 1, 100) / 100);

		var x0 = ProfileAnchor switch
		{
			AnclajesPerfil.RightOfRange => xHasta,
			AnclajesPerfil.LeftOfScreen => area.Left,
			AnclajesPerfil.RightOfScreen => area.Right,
			_ => xDesde
		};

		var dir = ProfileDirection switch
		{
			DireccionesPerfil.LeftToRight => 1,
			DireccionesPerfil.RightToLeft => -1,
			_ => ProfileAnchor is AnclajesPerfil.RightOfRange or AnclajesPerfil.RightOfScreen ? -1 : 1
		};

		return (x0, dir, anchoMaximo);
	}

	private void DibujarNiveles(RenderContext context, ResultadoTermico r, Estilo estilo, int x1, int x2)
	{
		var f = Formato;

		// Clústeres: rectángulo, 2 líneas gruesas y el cPOC.
		foreach (var k in r.Clusters)
		{
			var yArriba = Y(k.Arriba);
			var yAbajo = Y(k.Abajo);

			if (ShowClusterBox)
				context.FillRectangle(estilo.CajaCluster, new Rectangle(x1, Math.Min(yArriba, yAbajo), Math.Max(1, x2 - x1), Math.Max(1, Math.Abs(yAbajo - yArriba))));

			Linea(context, estilo.Cluster, x1, x2, yArriba);
			Linea(context, estilo.Cluster, x1, x2, yAbajo);
			Nota(ClusterLabels, yArriba, $"CLU ARRIBA {f(k.Arriba)}", ADibujo(ClusterLineColor));
			Nota(ClusterLabels, yAbajo, $"CLU ABAJO {f(k.Abajo)}", ADibujo(ClusterLineColor));

			if (ShowClusterPOC)
			{
				var yPoc = Y(k.Poc);
				Linea(context, estilo.ClusterPoc, x1, x2, yPoc);
				Nota(ClusterLabels, yPoc, $"cPOC {f(k.Poc)}", ADibujo(ClusterPOCColor));
			}
		}

		foreach (var e in r.Edges)
		{
			var y = Y(e.Precio);
			Linea(context, estilo.Edge, x1, x2, y);
			Nota(EdgeLabels, y, e.Etiqueta(f), ADibujo(EdgeColor));
		}

		// Espigas: línea punteada (tramos de 4 píxeles cada 8).
		foreach (var s in r.Spikes)
		{
			var y = Y(s.Precio);

			for (var x = x1; x < x2; x += 8)
				context.DrawLine(estilo.Spike, x, y, Math.Min(x + 4, x2), y);

			Nota(SpikeLabels, y, $"SPK {f(s.Precio)}", ADibujo(SpikeColor));
		}

		foreach (var l in r.Lvns)
		{
			var y = Y(l.Precio);

			if (l.Manual)
			{
				Linea(context, estilo.LvnManual, x1, x2, y);
				Nota(LVNLabels, y, $"LVN MANUAL {f(l.Precio)}", ADibujo(ManualLVNColor));
				continue;
			}

			if (LVNDisplay == DibujosLvn.Band)
			{
				var p = r.Detectores;
				var yArriba = Y(p.PrecioFila(l.Superior) + (p.TicksPorFila - 1) * p.Tick);
				var yAbajo = Y(p.PrecioFila(l.Inferior));
				context.FillRectangle(estilo.FranjaLvn, new Rectangle(x1, Math.Min(yArriba, yAbajo), Math.Max(1, x2 - x1), Math.Max(2, Math.Abs(yAbajo - yArriba))));
			}
			else
			{
				Linea(context, estilo.Lvn, x1, x2, y);
			}

			Nota(LVNLabels, y, $"LVN {l.Ranking} {f(l.Precio)}", ADibujo(LVNColor));
		}

		if (ShowVAH)
		{
			var y = Y(r.PrecioVah);
			Linea(context, estilo.Va, x1, x2, y);
			Nota(VALabels, y, $"VAH {f(r.PrecioVah)}", ADibujo(VAColor));
		}

		if (ShowVAL)
		{
			var y = Y(r.PrecioVal);
			Linea(context, estilo.Va, x1, x2, y);
			Nota(VALabels, y, $"VAL {f(r.PrecioVal)}", ADibujo(VAColor));
		}

		if (ShowPOC)
		{
			var y = Y(r.PrecioPoc);
			Linea(context, estilo.Poc, x1, x2, y);
			Nota(VALabels, y, $"POC {f(r.PrecioPoc)}", ADibujo(POCColor));
		}
	}

	private static void Linea(RenderContext context, RenderPen pluma, int x1, int x2, int y) => context.DrawLine(pluma, x1, y, x2, y);

	private void Nota(bool mostrar, int y, string texto, Color color)
	{
		if (mostrar)
			_etiquetas.Add(new Etiqueta(y, texto, color));
	}

	/// <summary>Etiquetas en el borde derecho; si dos se solapan, la segunda baja (o sube si ya no cabe abajo).</summary>
	private void DibujarEtiquetas(RenderContext context, Estilo estilo, Rectangle area)
	{
		if (_etiquetas.Count == 0)
			return;

		var alto = Math.Clamp(LabelFontSize, 6, 30) + 6;
		var orden = _etiquetas.Where(e => e.Y >= area.Top - alto && e.Y <= area.Bottom + alto).OrderBy(e => e.Y).ToList();
		var ys = new int[orden.Count];

		for (var i = 0; i < orden.Count; i++)
			ys[i] = i == 0 ? orden[i].Y : Math.Max(orden[i].Y, ys[i - 1] + alto);

		// Las que se salen por abajo suben, empujando a las de encima.
		for (var i = orden.Count - 1; i >= 0; i--)
		{
			var limite = i == orden.Count - 1 ? area.Bottom - alto / 2 : ys[i + 1] - alto;
			ys[i] = Math.Min(ys[i], limite);
		}

		for (var i = 0; i < orden.Count; i++)
		{
			var ancho = context.MeasureString(orden[i].Texto, estilo.Fuente).Width + 10;
			var caja = new Rectangle(area.Right - ancho - 4, ys[i] - alto / 2, ancho, alto);
			context.FillRectangle(estilo.FondoEtiqueta, caja);
			context.DrawString(orden[i].Texto, estilo.Fuente, orden[i].Color, caja, _centro);
		}
	}

	#endregion

	#region Alertas

	/// <summary>
	/// Alerta cuando la vela actual toca un nivel: una sola vez por nivel y por vela. 'sonar' = false marca como ya
	/// avisados los toques que existían al terminar la carga (para no soltar una ráfaga de alertas al abrir el gráfico).
	/// </summary>
	private void RevisarAlertas(int bar, bool sonar)
	{
		if (_resultado is not { } r)
			return;

		if (bar != _barraAlertas)
		{
			_alertadas.Clear();
			_barraAlertas = bar;
		}

		var vela = GetCandle(bar);

		foreach (var (activa, precio, texto) in NivelesConAlerta(r))
		{
			if (!activa || vela.Low > precio || vela.High < precio || !_alertadas.Add(texto))
				continue;

			if (sonar)
				AddAlert(string.IsNullOrWhiteSpace(AlertSoundFile) ? "alert1" : AlertSoundFile, $"Precio toca {texto}");
		}
	}

	private IEnumerable<(bool Activa, decimal Precio, string Texto)> NivelesConAlerta(ResultadoTermico r)
	{
		var f = Formato;

		foreach (var k in r.Clusters)
		{
			yield return (AlertOnCluster, k.Arriba, $"CLU ARRIBA {f(k.Arriba)}");
			yield return (AlertOnCluster, k.Abajo, $"CLU ABAJO {f(k.Abajo)}");
			yield return (AlertOnCluster && ShowClusterPOC, k.Poc, $"cPOC {f(k.Poc)}");
		}

		foreach (var e in r.Edges)
			yield return (AlertOnEdge, e.Precio, e.Etiqueta(f));

		foreach (var s in r.Spikes)
			yield return (AlertOnSpike, s.Precio, $"SPK {f(s.Precio)}");

		foreach (var l in r.Lvns)
			yield return (AlertOnLVN, l.Precio, l.Manual ? $"LVN MANUAL {f(l.Precio)}" : $"LVN {l.Ranking} {f(l.Precio)}");

		yield return (AlertOnPOC, r.PrecioPoc, $"POC {f(r.PrecioPoc)}");
		yield return (AlertOnVA, r.PrecioVah, $"VAH {f(r.PrecioVah)}");
		yield return (AlertOnVA, r.PrecioVal, $"VAL {f(r.PrecioVal)}");
	}

	#endregion

	#region Barra de herramientas (ratón)

	private enum Herramienta
	{
		Ninguna,
		Rango,
		Zona,
		Lvn
	}

	private static readonly string[] Botones = { "PERFIL", "RANGO", "ZONA", "LVN" };
	private readonly Rectangle[] _botones = new Rectangle[4];
	private Herramienta _herramienta;
	private Point? _inicioSeleccion, _finSeleccion;

	private void DibujarBarra(RenderContext context, Rectangle area)
	{
		const int ancho = 260;
		const int alto = 56;
		var panel = _arrastre.Ubicar(ToolbarX, ToolbarY, ancho, alto, 16, area);

		context.FillRectangle(Color.FromArgb(215, 18, 20, 26), panel);
		context.FillRectangle(Color.FromArgb(235, 30, 34, 44), new Rectangle(panel.X, panel.Y, ancho, 16));
		context.DrawString("THERMAL VOLUME PROFILE", _fuenteBarra, Color.Gainsboro, new Rectangle(panel.X + 6, panel.Y, ancho - 12, 16), _izquierda);

		var celda = (ancho - 12) / 4;

		for (var i = 0; i < 4; i++)
		{
			var caja = new Rectangle(panel.X + 6 + i * celda, panel.Y + 19, celda - 4, 17);
			_botones[i] = caja;
			var activo = i == 0 ? ShowProfile : (int)_herramienta == i;
			context.FillRectangle(activo ? Color.FromArgb(230, 200, 90, 0) : Color.FromArgb(220, 50, 54, 64), caja);
			context.DrawString(Botones[i], _fuenteBarra, activo ? Color.White : Color.Silver, caja, _centro);
		}

		var ayuda = _herramienta switch
		{
			Herramienta.Rango => "RANGO: arrastre sobre el grafico de inicio a fin",
			Herramienta.Zona => "ZONA: arrastre un rectangulo de precios",
			Herramienta.Lvn => "LVN: clic = nueva linea, clic sobre una = quitar",
			_ => _estado
		};

		context.DrawString(ayuda, _fuenteEstado, _herramienta == Herramienta.Ninguna ? Color.Silver : Color.Orange,
			new Rectangle(panel.X + 6, panel.Y + 38, ancho - 12, 16), _izquierda);
	}

	private void DibujarSeleccion(RenderContext context, Rectangle area)
	{
		if (_inicioSeleccion is not { } a || _finSeleccion is not { } b)
			return;

		var pluma = new RenderPen(Color.Orange, 1);
		var caja = _herramienta == Herramienta.Rango
			? new Rectangle(Math.Min(a.X, b.X), area.Top, Math.Abs(b.X - a.X), area.Height)
			: new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

		context.FillRectangle(Color.FromArgb(40, 255, 165, 0), caja);
		context.DrawRectangle(pluma, caja);
	}

	private int BotonEn(Point p)
	{
		if (!ShowToolbar)
			return -1;

		for (var i = 0; i < 4; i++)
		{
			if (_botones[i].Width > 0 && _botones[i].Contains(p))
				return i;
		}

		return -1;
	}

	public override bool ProcessMouseDown(RenderControlMouseEventArgs e)
	{
		try
		{
			var p = e.Location;
			var boton = BotonEn(p);

			if (boton == 0)
			{
				ShowProfile = !ShowProfile;
				RedrawChart();
				return true;
			}

			if (boton > 0)
			{
				var elegida = (Herramienta)boton;
				_herramienta = _herramienta == elegida ? Herramienta.Ninguna : elegida;
				RedrawChart();
				return true;
			}

			if (ShowToolbar && _arrastre.Presionar(p))
				return true;

			switch (_herramienta)
			{
				case Herramienta.Rango:
				case Herramienta.Zona:
					_inicioSeleccion = _finSeleccion = p;
					return true;

				case Herramienta.Lvn:
					AlternarLvnManual(p);
					return true;
			}
		}
		catch (Exception ex)
		{
			this.LogError("ThermalVolumeProfile: error del ratón", ex);
		}

		return base.ProcessMouseDown(e);
	}

	public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
	{
		if (_arrastre.Mover(e.Location, Area) is { } posicion)
		{
			(ToolbarX, ToolbarY) = (posicion.X, posicion.Y);
			RedrawChart();
			return true;
		}

		if (_inicioSeleccion is not null)
		{
			_finSeleccion = e.Location;
			RedrawChart();
			return true;
		}

		return base.ProcessMouseMove(e);
	}

	public override bool ProcessMouseUp(RenderControlMouseEventArgs e)
	{
		if (_arrastre.Soltar())
			return true;

		if (_inicioSeleccion is { } a)
		{
			_inicioSeleccion = _finSeleccion = null;

			try
			{
				AplicarSeleccion(a, e.Location);
			}
			catch (Exception ex)
			{
				this.LogError("ThermalVolumeProfile: error de la selección", ex);
			}

			_herramienta = Herramienta.Ninguna;
			RedrawChart();
			return true;
		}

		return base.ProcessMouseUp(e);
	}

	public override StdCursor GetCursor(RenderControlMouseEventArgs e) =>
		_herramienta != Herramienta.Ninguna || _arrastre.Arrastrando || _arrastre.SobreTitulo(e.Location) || BotonEn(e.Location) >= 0
			? StdCursor.Hand
			: base.GetCursor(e);

	/// <summary>RANGO fija las fechas del perfil; ZONA, la zona de búsqueda (precios y, para el filtro, fechas).</summary>
	private void AplicarSeleccion(Point a, Point b)
	{
		if ((Math.Abs(b.X - a.X) < 4 && Math.Abs(b.Y - a.Y) < 4) || InstrumentInfo is not { TickSize: > 0 } info || CurrentBar == 0)
			return;

		var barA = BarraDeX(Math.Min(a.X, b.X));
		var barB = BarraDeX(Math.Max(a.X, b.X));
		var horaA = GetCandle(barA).Time + info.TimeZoneOffset;
		var horaB = GetCandle(barB).Time + info.TimeZoneOffset;

		if (_herramienta == Herramienta.Rango)
		{
#if SUITE
			SuiteAntesDeNuevoRango();
#endif
			_rangeMode = ModosRango.CustomDateTime;
			_startDateTime = horaA;
			_endDateTime = horaB;
			_useCurrentBarAsEnd = barB >= CurrentBar - 1;
		}
		else if (_herramienta == Herramienta.Zona)
		{
			_searchZoneHigh = PrecioDeY(Math.Min(a.Y, b.Y));
			_searchZoneLow = PrecioDeY(Math.Max(a.Y, b.Y));
			_searchStartTime = horaA;
			_searchEndTime = horaB;
			_useSearchZone = true;
		}

		Recalcular();
	}

	/// <summary>Herramienta LVN: un clic añade una línea amarilla en ese precio; un clic sobre una existente la quita.</summary>
	private void AlternarLvnManual(Point p)
	{
		if (InstrumentInfo is not { TickSize: > 0 })
			return;

		var precios = Utilidades.LeerPrecios(_manualLvnPrices);
		var cercana = precios.Where(x => Math.Abs(Y(x) - p.Y) <= 5).OrderBy(x => Math.Abs(Y(x) - p.Y)).Cast<decimal?>().FirstOrDefault();

		if (cercana is { } quitar)
			precios.Remove(quitar);
		else
			precios.Add(PrecioDeY(p.Y));

		_manualLvnPrices = Utilidades.EscribirPrecios(precios);
		Recalcular();
	}

	/// <summary>Vela bajo una coordenada X (la más cercana de las visibles).</summary>
	private int BarraDeX(int x)
	{
		var primera = Math.Max(0, FirstVisibleBarNumber);
		var ultima = Math.Min(CurrentBar - 1, Math.Max(primera, LastVisibleBarNumber));
		var mejor = ultima;
		var distancia = int.MaxValue;

		for (var bar = primera; bar <= ultima; bar++)
		{
			var d = Math.Abs(ChartInfo!.GetXByBar(bar) + AnchoBarra / 2 - x);

			if (d < distancia)
				(distancia, mejor) = (d, bar);
		}

		return mejor;
	}

	#endregion

	#region Utilidades

	private string Formato(decimal precio) => Utilidades.Formato(precio, InstrumentInfo?.TickSize ?? 0.25m);

	/// <summary>Precio de una coordenada Y (la escala de precios es lineal), redondeado al tick.</summary>
	private decimal PrecioDeY(int y)
	{
		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		var referencia = _resultado?.PrecioPoc ?? (CurrentBar > 0 ? GetCandle(CurrentBar - 1).Close : 0m);
		var otro = referencia + 100 * tick;
		var y1 = Y(referencia);
		var y2 = Y(otro);

		if (y1 == y2)
			return referencia;

		var precio = referencia + (y - y1) * (otro - referencia) / (y2 - y1);
		return Math.Round(precio / tick, MidpointRounding.AwayFromZero) * tick;
	}

	/// <summary>Arrastre de la barra por su título, dentro del área del gráfico (posición guardada en los ajustes).</summary>
	private sealed class Arrastre
	{
		private readonly object _sync = new();
		private Rectangle _panel;
		private int _altoTitulo;
		private Point? _agarre;

		public bool Arrastrando
		{
			get
			{
				lock (_sync)
					return _agarre is not null;
			}
		}

		public Rectangle Ubicar(int x, int y, int ancho, int alto, int altoTitulo, Rectangle area)
		{
			lock (_sync)
			{
				_panel = new Rectangle(Ajustar(x, area.Left, area.Right - ancho), Ajustar(y, area.Top, area.Bottom - alto), ancho, alto);
				_altoTitulo = altoTitulo;
				return _panel;
			}
		}

		public void Ocultar()
		{
			lock (_sync)
				(_panel, _agarre) = (Rectangle.Empty, null);
		}

		public bool SobreTitulo(Point raton)
		{
			lock (_sync)
				return new Rectangle(_panel.X, _panel.Y, _panel.Width, _altoTitulo).Contains(raton);
		}

		public bool Presionar(Point raton)
		{
			if (!SobreTitulo(raton))
				return false;

			lock (_sync)
				_agarre = new Point(raton.X - _panel.X, raton.Y - _panel.Y);

			return true;
		}

		public Point? Mover(Point raton, Rectangle area)
		{
			lock (_sync)
			{
				if (_agarre is not { } agarre)
					return null;

				return new Point(Ajustar(raton.X - agarre.X, area.Left, area.Right - _panel.Width),
					Ajustar(raton.Y - agarre.Y, area.Top, area.Bottom - _panel.Height));
			}
		}

		public bool Soltar()
		{
			lock (_sync)
			{
				var arrastraba = _agarre is not null;
				_agarre = null;
				return arrastraba;
			}
		}

		private static int Ajustar(int valor, int minimo, int maximo) => Math.Max(minimo, Math.Min(valor, maximo));
	}

	#endregion
}
