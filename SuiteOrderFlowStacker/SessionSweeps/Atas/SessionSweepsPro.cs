namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

using ATAS.Indicators;

using SessionSweeps.Nucleo;

using OFT.Attributes;
using OFT.Attributes.Editors;

using Utils.Common.Logging;

using MediaColor = System.Windows.Media.Color;

/// <summary>
/// NQ Session Sweeps Pro: sesiones (Asia, Londres, premercado, NY AM, NY PM), niveles de liquidez, barridos con la
/// lógica del Pine "Session Sweeps" de LuxAlgo, validación con Order Flow, alertas en el tick, contexto H1, objetivos,
/// FVG/IFVG, LVN y Modelos A y B, con correo. Todo el cálculo está en Nucleo/ (MotorSweeps); aquí se pasan las velas
/// y los trades, se avisa y se dibuja. Ver LEEME.md y DECISIONES.md.
/// </summary>
[DisplayName("Sessions Sweeps Pro (Suite)")]
[Description("Sessions Sweeps Pro v3 (Suite Order Flow Stacker): Modelo A (barrido de liquidez real / premium) y Modelo B (retroceso a LVN / EDGE / FVG) con gatillo de order flow, perfiles NQ / ES / YM, LVN y EDGE del Thermal, panel flotante, alertas, correo, Telegram y puente MT5 opcional.")]
public partial class SessionSweepsPro : Indicator
{
	#region Grupos

	private const string G1 = "1. Sesiones";
	private const string G2 = "2. Tabla";
	private const string G3 = "3. Niveles de liquidez";
	private const string G4 = "4. Barridos (Pine)";
	private const string G5 = "5. Order Flow";
	private const string G6 = "6. Contexto H1";
	private const string G7 = "7. Proyeccion";
	private const string G8 = "8. FVG / IFVG";
	private const string G12 = "12. Alertas";
	private const string G13 = "13. Correo";
	private const string G14 = "14. MSS";
	private const string G15 = "15. Visual";

	#endregion

	#region Parámetros

	private readonly ConfiguracionSweeps _cfg = new();

	private DefinicionSesion S(string id) => _cfg.Sesion(id);

	private void Recalcular() => RecalculateValues();

	// --- 1. Sesiones (generales; las de cada sesión están en SessionSweepsPro.Sesiones.cs) ----------------------

	[Display(Name = "Preset de horarios", GroupName = G1, Order = 1, Description = "PineOriginal: Asia 09-18 Tokio, Londres 08-17 Londres, NY AM 08-13 y NY PM 13-19 ET.")]
	public PresetHorarios Preset { get => _cfg.Preset; set { _cfg.Preset = value; Recalcular(); } }

	[Display(Name = "Apertura de NY (NYOpen)", GroupName = G1, Order = 2)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan NYOpen { get => _cfg.NYOpen; set { _cfg.NYOpen = value; Recalcular(); } }

	[Display(Name = "Premercado: minutos antes de la apertura", GroupName = G1, Order = 3)]
	[Range(0, 600)]
	public int PremarketMinutesBeforeOpen { get => _cfg.PremarketMinutesBeforeOpen; set { _cfg.PremarketMinutesBeforeOpen = value; Recalcular(); } }

	[Display(Name = "Ajuste manual (horas)", GroupName = G1, Order = 4, Description = "Solo si se ve un desfase: el horario de verano ya se aplica solo.")]
	[Range(-12, 12)]
	public decimal AjusteManualHoras { get => (decimal)_cfg.AjusteManualHoras; set { _cfg.AjusteManualHoras = (double)value; Recalcular(); } }

	[Display(Name = "Extender hasta que se barra", GroupName = G1, Order = 5)]
	public bool ExtendUntilSwept { get => _cfg.ExtendUntilSwept; set => _cfg.ExtendUntilSwept = value; }

	[Display(Name = "Grosor de las líneas de sesión", GroupName = G1, Order = 6)]
	[Range(1, 5)]
	public int SessionLineWidth { get; set; } = 1;

	[Display(Name = "Líneas de máximo y mínimo", GroupName = G1, Order = 7)]
	public bool SessionExtremeLines { get; set; } = true;

	[Display(Name = "Transparencia del sombreado (0-100)", GroupName = G1, Order = 8)]
	[Range(0, 100)]
	public int SessionFillTransparency { get; set; } = 95;

	// --- 2. Tabla --------------------------------------------------------------------------------------------------

	[Display(Name = "Mostrar la tabla de sesiones", GroupName = G2, Order = 10)]
	public bool ShowTable { get; set; } = true;

	[Display(Name = "Tamaño del texto", GroupName = G2, Order = 20)]
	public TamanoTexto TableTextSize { get; set; } = TamanoTexto.Pequeno;

	[Display(Name = "Posición X (vacío = arriba a la derecha)", GroupName = G2, Order = 30)]
	[Range(-1, 10000)]
	public int TableX { get; set; } = -1;

	[Display(Name = "Posición Y", GroupName = G2, Order = 40)]
	[Range(0, 10000)]
	public int TableY { get; set; } = 10;

	// --- 3. Niveles de liquidez ------------------------------------------------------------------------------------

	[Display(Name = "Máximo y mínimo de cada sesión", GroupName = G3, Order = 10)]
	public bool NivelesSesion { get => _cfg.NivelesSesion; set { _cfg.NivelesSesion = value; Recalcular(); } }

	[Display(Name = "PDH / PDL (día 18:00-17:00 ET)", GroupName = G3, Order = 20)]
	public bool NivelesPdhPdl { get => _cfg.NivelesPdhPdl; set { _cfg.NivelesPdhPdl = value; Recalcular(); } }

	[Display(Name = "Días anteriores sin barrer", GroupName = G3, Order = 30)]
	public bool NivelesDiasAnteriores { get => _cfg.NivelesDiasAnteriores; set { _cfg.NivelesDiasAnteriores = value; Recalcular(); } }

	[Display(Name = "Días anteriores (N)", GroupName = G3, Order = 40)]
	[Range(1, 20)]
	public int DiasAnteriores { get => _cfg.DiasAnteriores; set { _cfg.DiasAnteriores = value; Recalcular(); } }

	[Display(Name = "PWH / PWL (semana anterior)", GroupName = G3, Order = 50)]
	public bool NivelesSemanaAnterior { get => _cfg.NivelesSemanaAnterior; set { _cfg.NivelesSemanaAnterior = value; Recalcular(); } }

	[Display(Name = "Equal highs / equal lows", GroupName = G3, Order = 60)]
	public bool NivelesIguales { get => _cfg.NivelesIguales; set { _cfg.NivelesIguales = value; Recalcular(); } }

	[Display(Name = "Tolerancia equal (ticks)", GroupName = G3, Order = 70)]
	[Range(0, 100)]
	public int EqualTolTicks { get => _cfg.EqualTolTicks; set { _cfg.EqualTolTicks = value; Recalcular(); } }

	[Display(Name = "Ventana de detección de barridos", GroupName = G3, Order = 80)]
	public VentanaBarridos SweepDetectionWindow { get => _cfg.SweepDetectionWindow; set { _cfg.SweepDetectionWindow = value; Recalcular(); } }

	[Display(Name = "Ventana personalizada: inicio (ET)", GroupName = G3, Order = 90)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan VentanaInicio { get => _cfg.VentanaInicio; set { _cfg.VentanaInicio = value; Recalcular(); } }

	[Display(Name = "Ventana personalizada: fin (ET)", GroupName = G3, Order = 100)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan VentanaFin { get => _cfg.VentanaFin; set { _cfg.VentanaFin = value; Recalcular(); } }

	[Display(Name = "Modo compatible con el Pine", GroupName = G3, Order = 110, Description = "Solo el par de la última sesión cerrada y el de Londres durante NY AM, como el Pine original.")]
	public bool PineCompatibilityMode { get => _cfg.PineCompatibilityMode; set { _cfg.PineCompatibilityMode = value; Recalcular(); } }

	// --- 4. Barridos (Pine) ----------------------------------------------------------------------------------------

	[Display(Name = "Zonas buyside", GroupName = G4, Order = 10)]
	public bool BuysideZones { get => _cfg.BuysideZones; set { _cfg.BuysideZones = value; Recalcular(); } }

	[Display(Name = "Color buyside", GroupName = G4, Order = 20)]
	public MediaColor BuysideColor { get; set; } = MediaColor.FromArgb(160, 255, 152, 0);

	[Display(Name = "Margen buyside (× ATR21)", GroupName = G4, Order = 30)]
	[Range(0.5, 10)]
	public decimal BuysideMargin { get => (decimal)_cfg.BuysideMargin; set { _cfg.BuysideMargin = (double)value; Recalcular(); } }

	[Display(Name = "Zonas sellside", GroupName = G4, Order = 40)]
	public bool SellsideZones { get => _cfg.SellsideZones; set { _cfg.SellsideZones = value; Recalcular(); } }

	[Display(Name = "Color sellside", GroupName = G4, Order = 50)]
	public MediaColor SellsideColor { get; set; } = MediaColor.FromArgb(160, 33, 150, 243);

	[Display(Name = "Margen sellside (× ATR21)", GroupName = G4, Order = 60)]
	[Range(0.5, 10)]
	public decimal SellsideMargin { get => (decimal)_cfg.SellsideMargin; set { _cfg.SellsideMargin = (double)value; Recalcular(); } }

	[Display(Name = "Longitud del margen (velas)", GroupName = G4, Order = 70)]
	[Range(2, 10)]
	public int SweepMarginLength { get => _cfg.SweepMarginLength; set { _cfg.SweepMarginLength = value; Recalcular(); } }

	[Display(Name = "Detectar una vez por sesión", GroupName = G4, Order = 80)]
	public bool DetectOncePerSession { get => _cfg.DetectOncePerSession; set { _cfg.DetectOncePerSession = value; Recalcular(); } }

	[Display(Name = "Ocultar zonas falsas", GroupName = G4, Order = 90)]
	public bool HideFakeSweepZones { get; set; }

	[Display(Name = "Color falso", GroupName = G4, Order = 100)]
	public MediaColor FakeColor { get; set; } = MediaColor.FromArgb(69, 120, 123, 134);

	// --- 5. Order Flow ---------------------------------------------------------------------------------------------

	[Display(Name = "Validar con Order Flow", GroupName = G5, Order = 10)]
	public bool OrderFlowValidation { get => _cfg.OrderFlowValidation; set { _cfg.OrderFlowValidation = value; Recalcular(); } }

	[Display(Name = "Contratos mínimos más allá (MinVolBeyond)", GroupName = G5, Order = 20, Description = "150 en NQ. En MNQ, recalibrar (negocia más contratos).")]
	[Range(0, 1000000)]
	public decimal MinVolBeyond { get => _cfg.MinVolBeyond; set { _cfg.MinVolBeyond = value; Recalcular(); } }

	[Display(Name = "Absorción: volumen (× medio por tick)", GroupName = G5, Order = 30)]
	[Range(0, 100)]
	public decimal AbsVolMult { get => (decimal)_cfg.AbsVolMult; set { _cfg.AbsVolMult = (double)value; Recalcular(); } }

	[Display(Name = "Absorción: avance máximo (ticks)", GroupName = G5, Order = 40)]
	[Range(0, 1000)]
	public int AbsMaxTicks { get => _cfg.AbsMaxTicks; set { _cfg.AbsMaxTicks = value; Recalcular(); } }

	[Display(Name = "Panel clásico del último barrido", GroupName = G5, Order = 50, Description = "Panel heredado de texto simple. El panel principal está en 28. Panel y dibujo.")]
	public bool ShowSweepPanel { get; set; }

	[Display(Name = "Panel: posición X", GroupName = G5, Order = 60)]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Panel: posición Y", GroupName = G5, Order = 70)]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 200;

	// --- 6. Contexto H1 --------------------------------------------------------------------------------------------

	[Display(Name = "Longitud de pivote H1", GroupName = G6, Order = 10)]
	[Range(1, 20)]
	public int H1PivotLen { get => _cfg.H1PivotLen; set { _cfg.H1PivotLen = value; Recalcular(); } }

	[Display(Name = "Solo entradas alineadas con H1", GroupName = G6, Order = 20, Description = "Salvo barridos del PDH/PDL.")]
	public bool RequireH1Alignment { get => _cfg.RequireH1Alignment; set { _cfg.RequireH1Alignment = value; Recalcular(); } }

	// --- 7. Proyección ---------------------------------------------------------------------------------------------

	[Display(Name = "Distancia mínima de T1 (ticks)", GroupName = G7, Order = 10)]
	[Range(0, 10000)]
	public int MinTargetTicks { get => _cfg.MinTargetTicks; set { _cfg.MinTargetTicks = value; Recalcular(); } }

	[Display(Name = "Proyección: margen del stop (ticks)", GroupName = G7, Order = 20, Description = "Solo para T1/T2/T3 de la proyección del barrido. El stop de los modelos v2 está en 21. Recuperación.")]
	[Range(0, 1000)]
	public int ProjectionStopBufferTicks { get => _cfg.StopBufferTicks; set { _cfg.StopBufferTicks = value; Recalcular(); } }

	[Display(Name = "Valor del punto", GroupName = G7, Order = 30)]
	public ContratoValor Contrato { get => _cfg.Contrato; set { _cfg.Contrato = value; Recalcular(); } }

	[Display(Name = "Mostrar T1 / T2 / T3", GroupName = G7, Order = 40)]
	public bool ShowTargets { get; set; } = true;

	// --- 8. FVG / IFVG ---------------------------------------------------------------------------------------------

	[Display(Name = "Mostrar FVG / IFVG", GroupName = G8, Order = 10)]
	public bool ShowFvg { get; set; } = true;

	[Display(Name = "Filtro de anchura (× ATR144)", GroupName = G8, Order = 20)]
	[Range(0, 10)]
	public decimal FVGWidthFilter { get => (decimal)_cfg.FVGWidthFilter; set { _cfg.FVGWidthFilter = (double)value; Recalcular(); } }

	[Display(Name = "Velas tras el barrido (FVG post sweep)", GroupName = G8, Order = 30)]
	[Range(1, 500)]
	public int PostSweepFVGWindow { get => _cfg.PostSweepFVGWindow; set { _cfg.PostSweepFVGWindow = value; Recalcular(); } }

	[Display(Name = "Color FVG alcista", GroupName = G8, Order = 40)]
	public MediaColor FvgBullColor { get; set; } = MediaColor.FromArgb(51, 0, 150, 136);

	[Display(Name = "Color FVG bajista", GroupName = G8, Order = 50)]
	public MediaColor FvgBearColor { get; set; } = MediaColor.FromArgb(51, 244, 67, 54);

	[Display(Name = "Color IFVG", GroupName = G8, Order = 60)]
	public MediaColor IfvgColor { get; set; } = MediaColor.FromArgb(90, 139, 0, 0);

	// --- 12. Alertas -----------------------------------------------------------------------------------------------

	[Display(Name = "Alerta de ruptura (en el tick)", GroupName = G12, Order = 10)]
	public bool AlertaRuptura { get; set; } = true;

	[Display(Name = "Alertas de cuenta atrás (1m y 5m)", GroupName = G12, Order = 20)]
	public bool AlertaCuentaAtras { get; set; } = true;

	[Display(Name = "Segundos antes del cierre", GroupName = G12, Order = 30)]
	[Range(1, 59)]
	public int TriggerCountdownSec { get => _cfg.TriggerCountdownSec; set { _cfg.TriggerCountdownSec = value; Recalcular(); } }

	[Display(Name = "Alerta TRAMPA / UPGRADE PREMIUM", GroupName = G12, Order = 40, Description = "Al congelar una TRAMPA igual o mayor que el estado mínimo (22. Estados y alertas) y en cada UPGRADE PREMIUM.")]
	public bool AlertaClasificacion { get; set; } = true;

	[Display(Name = "Alerta de gatillo VÁLIDO", GroupName = G12, Order = 50, Description = "Entrada de MOD1, BFVG, MOD2, MOD3 o MOD4.")]
	public bool AlertaEntrada { get; set; } = true;

	[Display(Name = "Alerta MOD3 / RUPTURA (arma MOD4)", GroupName = G12, Order = 52)]
	public bool AlertaModelos { get; set; } = true;

	[Display(Name = "Alerta FALLIDO / RUPTURA FALLIDA", GroupName = G12, Order = 54)]
	public bool AlertaFallidos { get; set; } = true;

	[Display(Name = "Alerta de IFVG", GroupName = G12, Order = 60)]
	public bool AlertaIfvg { get; set; }

	[Display(Name = "Sonido", GroupName = G12, Order = 70)]
	[SoundComboBoxEditor]
	public string AlertSoundFile { get; set; } = "alert1";

	// --- 13. Correo ------------------------------------------------------------------------------------------------

	[Display(Name = "Correo activado", GroupName = G13, Order = 10)]
	public bool EmailEnabled { get; set; } = true;

	[Display(Name = "Para (EmailTo)", GroupName = G13, Order = 20, Description = "Destinatario del correo de cada toma de liquidez externa válida.")]
	public string EmailTo { get; set; } = "uniontradingacademy@gmail.com";

	[Display(Name = "Servidor SMTP", GroupName = G13, Order = 30)]
	public string SmtpServer { get; set; } = "smtp.gmail.com";

	[Display(Name = "Puerto", GroupName = G13, Order = 40)]
	[Range(1, 65535)]
	public int SmtpPort { get; set; } = 587;

	[Display(Name = "SSL", GroupName = G13, Order = 50)]
	public bool UseSSL { get; set; } = true;

	[Display(Name = "Usuario SMTP", GroupName = G13, Order = 60)]
	public string SmtpUser { get; set; } = "";

	[Display(Name = "Contraseña de aplicación", GroupName = G13, Order = 70, Description = "Gmail: contraseña de aplicación de 16 letras (verificación en 2 pasos).")]
	public SecureString? SmtpPassword { get; set; }

	[Display(Name = "Correo en cada toma de liquidez externa válida", GroupName = G13, Order = 75,
		Description = "Gatillo VÁLIDO sobre un barrido de liquidez externa (PDH/PDL, días y semana anteriores) con entrada, stop y objetivos. Solo en vivo, nunca con NOTICIA.")]
	public bool EmailOnValidEntry { get; set; } = true;

	[Display(Name = "Correo al barrer (en t0, aviso informativo)", GroupName = G13, Order = 80)]
	public bool EmailOnSweep { get; set; }

	[Display(Name = "Correo de actualización (al cierre de W5)", GroupName = G13, Order = 85)]
	public bool EmailOnUpdate { get; set; }

	[Display(Name = "Zona horaria del correo", GroupName = G13, Order = 120, Description = "Por ejemplo Eastern Standard Time o Central Standard Time (Mexico).")]
	public string EmailTimeZone { get; set; } = "Eastern Standard Time";

	[Display(Name = "Enviar correo de prueba", GroupName = G13, Order = 130, Description = "Al marcarlo se envía un correo de prueba y se desmarca solo.")]
	public bool SendTestEmail
	{
		get => false;
		set
		{
			if (value)
				EnviarPrueba();
		}
	}

	// --- 14. MSS ---------------------------------------------------------------------------------------------------

	[Display(Name = "Mostrar MSS / CHoCH", GroupName = G14, Order = 10)]
	public bool Mss { get => _cfg.Mss; set { _cfg.Mss = value; Recalcular(); } }

	[Display(Name = "Longitud de detección", GroupName = G14, Order = 20)]
	[Range(1, 100)]
	public int MSSLength { get => _cfg.MSSLength; set { _cfg.MSSLength = value; Recalcular(); } }

	[Display(Name = "Color CHoCH alcista", GroupName = G14, Order = 30)]
	public MediaColor MssBullColor { get; set; } = MediaColor.FromArgb(255, 0, 150, 136);

	[Display(Name = "Color CHoCH bajista", GroupName = G14, Order = 40)]
	public MediaColor MssBearColor { get; set; } = MediaColor.FromArgb(255, 244, 67, 54);

	// --- 15. Visual ------------------------------------------------------------------------------------------------

	[Display(Name = "Máximo de cajas de barrido", GroupName = G15, Order = 10)]
	[Range(10, 5000)]
	public int MaxSweepBoxes { get => _cfg.MaxSweepBoxes; set { _cfg.MaxSweepBoxes = value; Recalcular(); } }

	[Display(Name = "Máximo de líneas", GroupName = G15, Order = 20)]
	[Range(10, 5000)]
	public int MaxLines { get => _cfg.MaxLines; set { _cfg.MaxLines = value; Recalcular(); } }

	[Display(Name = "Mostrar etiquetas de niveles", GroupName = G15, Order = 30)]
	public bool ShowLevelLabels { get; set; } = true;

	[Display(Name = "Tamaño de las etiquetas", GroupName = G15, Order = 40)]
	[Range(6, 20)]
	public int LabelFontSize { get; set; } = 8;

	#endregion

	#region Estado

	private readonly object _sync = new();
	private readonly object _syncAvisos = new();
	private MotorSweeps? _motor;
	private int _ultimaBarra = -1;
	private bool _enVivo;
	private long _avisoVisto;
	private int _minutos;
	private string _estado = "";
	private Correo? _correo;
	private bool _avisoSinSmtp;

	#endregion

	public SessionSweepsPro()
		: base(true)
	{
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		OrderFlowSuite.Ui.VentanaFlotante.Registrar();
		Panel = IndicatorDataProvider.CandlesPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	#region Cálculo

	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			lock (_sync)
				Calcular(bar);
		}
		catch (Exception ex)
		{
			_estado = "ERROR DE CALCULO: " + ex.Message;
			this.LogError("SessionSweepsPro: error de cálculo", ex);
		}
	}

	private void Calcular(int bar)
	{
		if (bar == 0)
			Reiniciar();

		if (_motor is null || bar <= _ultimaBarra)
			return;

		// Las velas anteriores a 'bar' ya cerraron: cada una se procesa una sola vez, con sus valores finales.
		for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
		{
			_motor.Procesar(Entrada(i));
		}

		_ultimaBarra = bar;

		// Al llegar a la última vela termina la carga del historial: desde aquí los avisos suenan y se envían.
		if (!_enVivo && bar >= CurrentBar - 1)
		{
			_enVivo = true;
			_avisoVisto = _motor.UltimoAviso;
			AlEntrarEnVivoV2(_motor);
		}

		RevisarAvisos();
		ProcesarNovedadesV2();
		LatidoV2();
	}

	/// <summary>ATAS vuelve a calcular desde la vela 0 al cambiar de instrumento, temporalidad o un ajuste.</summary>
	private void Reiniciar()
	{
		_ultimaBarra = -1;
		_enVivo = false;
		_avisoVisto = 0;
		_motor = null;
		_minutos = Minutos();

		// Decisión 4: Session Sweeps Pro v2.0 trabaja sobre el gráfico de 1 minuto (las barras de volumen se arman por minuto).
		if (_minutos != 1)
		{
			_estado = "Sessions Sweeps Pro funciona en graficos de 1 minuto (TimeFrame M1)";
			return;
		}

		_cfg.TamanoTick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		_cfg.MinutosGrafico = _minutos;
		_motor = new MotorSweeps(_cfg) { Simbolo = Simbolo(InstrumentInfo?.Instrument), ZonaCorreo = Zonas.PorNombre(EmailTimeZone) };
		PrepararV2(_motor);
		_estado = "";
	}

	/// <summary>Minutos por vela: por el tipo de gráfico de ATAS ("TimeFrame" con "M1".."M5") o la menor separación entre velas.</summary>
	private int Minutos()
	{
		if (ChartInfo?.ChartType is { } tipo && tipo != "TimeFrame")
			return 0;

		if (ChartInfo?.TimeFrame is { } tf && Regex.Match(tf, @"^M(\d+)$") is { Success: true } m)
			return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);

		var menor = double.MaxValue;

		for (var i = 0; i + 1 < Math.Min(CurrentBar, 40); i++)
		{
			var minutos = (GetCandle(i + 1).Time - GetCandle(i).Time).TotalMinutes;

			if (minutos > 0)
				menor = Math.Min(menor, minutos);
		}

		return menor == double.MaxValue ? 1 : (int)Math.Round(menor);
	}

	/// <summary>"NQZ6" → "NQ", "MNQH7" → "MNQ".</summary>
	private static string Simbolo(string? instrumento)
	{
		if (string.IsNullOrWhiteSpace(instrumento))
			return "NQ";

		var m = Regex.Match(instrumento.Trim(), @"^([A-Za-z]+?)[FGHJKMNQUVXZ]\d{1,2}$");
		return (m.Success ? m.Groups[1].Value : instrumento.Trim()).ToUpperInvariant();
	}

	private VelaEntrada Entrada(int bar)
	{
		var c = GetCandle(bar);
		var e = new VelaEntrada
		{
			InicioUtc = c.Time, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume, Delta = c.Delta, Ask = c.Ask, Bid = c.Bid,
			MaxDelta = c.MaxDelta, MinDelta = c.MinDelta
		};
		var niveles = c.GetAllPriceLevels();

		if (niveles is not null)
		{
			foreach (var n in niveles)
				e.Niveles.Add(new NivelVela(n.Price, n.Bid, n.Ask));
		}

		return e;
	}

	/// <summary>En vivo: cada trade (ruptura en el tick, velocidad y cuentas atrás).</summary>
	protected override void OnNewTrade(MarketDataArg trade)
	{
		try
		{
			bool? compra = trade.Direction == TradeDirection.Buy ? true : trade.Direction == TradeDirection.Sell ? false : null;

			if (!_enVivo || _motor is not { } motor || CurrentBar == 0)
			{
				// Mientras se recalcula, el trade se guarda: el motor nuevo lo usará al llegar a su vela.
				GuardarTradeV2(trade.Time, trade.Price, trade.Volume, compra);
				return;
			}

			var vela = GetCandle(CurrentBar - 1);
			motor.Trade(trade.Time, trade.Price, trade.Volume, compra, vela.Open, vela.Time);
			RevisarAvisos();
			ProcesarNovedadesV2();
			LatidoV2();
		}
		catch (Exception ex)
		{
			this.LogError("SessionSweepsPro: error en OnNewTrade", ex);
		}
	}

	protected override void OnDispose()
	{
		CerrarV2();
		_grabador?.Dispose();
		_grabador = null;
		_correo?.Dispose();
		_correo = null;
	}

	private static List<decimal> LeerPrecios(string texto) =>
		texto.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(t => decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : (decimal?)null)
			.Where(p => p is > 0)
			.Select(p => p!.Value)
			.ToList();

	#endregion

	#region Alertas y correo

	/// <summary>Avisos nuevos del motor: alerta de ATAS y/o correo según el tipo y los ajustes. Solo en vivo.</summary>
	private void RevisarAvisos()
	{
		if (!_enVivo || _motor is not { } motor)
			return;

		lock (_syncAvisos)
		{
			if (motor.UltimoAviso == _avisoVisto)
				return;

			foreach (var a in motor.AvisosDespuesDe(_avisoVisto))
			{
				_avisoVisto = a.Numero;

				// De los avisos del motor del NQ quedan los del tick (ruptura, cuentas atrás) e IFVG; la clasificación y las
				// entradas son las de v2.0 (ProcesarNovedadesV2).
				var alerta = a.Tipo switch
				{
					TipoAviso.Ruptura => AlertaRuptura,
					TipoAviso.CuentaAtras1m or TipoAviso.CuentaAtras5m => AlertaCuentaAtras,
					TipoAviso.Ifvg => AlertaIfvg,
					_ => false
				};

				if (alerta)
					AddAlert(string.IsNullOrWhiteSpace(AlertSoundFile) ? "alert1" : AlertSoundFile, a.Texto);

				var correo = a.Asunto is not null && a.Tipo switch
				{
					TipoAviso.Ruptura => EmailOnSweep,
					TipoAviso.ActualizacionW5 => EmailOnUpdate,
					_ => false
				};

				if (correo)
					Enviar(a.Clave, a.Asunto!, a.Cuerpo ?? a.Texto);
			}
		}
	}

	private void Enviar(string clave, string asunto, string cuerpo)
	{
		if (!EmailEnabled || string.IsNullOrWhiteSpace(EmailTo))
			return;

		// Sin usuario o contraseña SMTP no se intenta (se avisa una vez en el registro de ATAS).
		if (string.IsNullOrWhiteSpace(SmtpUser) || SmtpPassword is not { Length: > 0 })
		{
			if (!_avisoSinSmtp)
				this.LogWarn("Sessions Sweeps Pro: correo sin configurar (usuario y contraseña SMTP en 13. Correo); no se envía.");

			_avisoSinSmtp = true;
			return;
		}

		_correo ??= new Correo((mensaje, error) => this.LogError(mensaje, error));
		_correo.Encolar(clave, new ConfiguracionCorreo(SmtpServer, SmtpPort, UseSSL, SmtpUser, SmtpPassword, EmailTo), asunto, cuerpo);
	}

	private void EnviarPrueba()
	{
		var hora = Zonas.PorNombre(EmailTimeZone);
		var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, hora).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		Enviar("prueba|" + Guid.NewGuid(), "Sessions Sweeps Pro - correo de prueba", $"Correo de prueba enviado a las {ahora}.\nSi lo recibe, el correo esta bien configurado.");
	}

	#endregion
}
