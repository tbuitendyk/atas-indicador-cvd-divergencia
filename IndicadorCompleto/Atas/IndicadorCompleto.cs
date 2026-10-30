namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

using ATAS.Indicators;

using Completo.Nucleo;
using Completo.Ventana;

using Utils.Common.Logging;

using MediaColor = System.Windows.Media.Color;

/// <summary>
/// IndicadorCompleto: herramienta de análisis y asistencia, NO envía órdenes. El maestro (gráfico de 1 minuto)
/// calcula con velas de 1M cerradas y comparte el motor por grupo + instrumento; los visores (1M y 5M) dibujan los
/// mismos resultados. La ventana flotante muestra el reporte. Ver LEEME.md.
/// </summary>
[DisplayName("Indicador Completo")]
[Description("Contexto 1H, barridos de liquidez, modelos M1/M2, mapa LVN, riesgo en micros y reporte flotante. Análisis sin órdenes.")]
public partial class IndicadorCompleto : Indicator
{
	#region Estado

	private readonly object _sync = new();
	private readonly List<DateTime> _tiempos = new();
	private MotorCompleto? _motor;
	private string _clave = "";
	private bool _esMaestro;
	private int _marcoGrafico = 1;
	private string _estadoRol = "Iniciando…";
	private int _ultimaBarra = -1;
	private bool _ventanaAbierta;
	private string? _claveVentana;

	#endregion

	#region Ajustes: rol y presentación

	private RolIndicador _rol = RolIndicador.Automatico;
	private string _grupo = "A";

	[Display(Name = "Rol", GroupName = "0. Rol, grupo y ventana", Order = 1,
		Description = "Automático: maestro si el gráfico es de 1 minuto y no hay otro maestro para el grupo y el instrumento; si no, visor.")]
	public RolIndicador Rol { get => _rol; set => Cambiar(ref _rol, value); }

	[Display(Name = "Grupo compartido", GroupName = "0. Rol, grupo y ventana", Order = 2,
		Description = "Los gráficos con el mismo grupo e instrumento comparten el mismo motor y el mismo mapa LVN.")]
	public string Grupo { get => _grupo; set => Cambiar(ref _grupo, value ?? "A"); }

	[Display(Name = "Mostrar ventana flotante del reporte", GroupName = "0. Rol, grupo y ventana", Order = 3,
		Description = "Solo la abre el maestro. Se puede mover a otro monitor; guarda su posición y tamaño.")]
	public bool MostrarVentana { get; set; } = true;

	[Display(Name = "Ventana siempre visible", GroupName = "0. Rol, grupo y ventana", Order = 4)]
	public bool VentanaSiempreVisible { get; set; } = true;

	[Display(Name = "Zona horaria del reporte", GroupName = "0. Rol, grupo y ventana", Order = 5)]
	public ZonaPresentacion ZonaReporte { get; set; } = ZonaPresentacion.CiudadDeMexico;

	#endregion

	#region Ajustes: riesgo

	private bool _verificados;
	private string _permitidos = "NQ;MNQ";
	private decimal _riesgoUsd = 250m, _valorTick = 0.50m, _comision = 1.50m, _rrMinimo = 1m;
	private int _deslizamiento = 2, _maxContratos = 20;

	[Display(Name = "Valores monetarios verificados", GroupName = "1. Riesgo", Order = 10,
		Description = "Sin esta confirmación no se admite ninguna señal. Revise valor del tick, comisión y deslizamiento de su intermediario.")]
	public bool ValoresMonetariosVerificados { get => _verificados; set => Cambiar(ref _verificados, value); }

	[Display(Name = "Instrumentos permitidos (prefijos ;)", GroupName = "1. Riesgo", Order = 11)]
	public string InstrumentosPermitidos { get => _permitidos; set => Cambiar(ref _permitidos, value ?? ""); }

	[Display(Name = "Riesgo por señal (USD)", GroupName = "1. Riesgo", Order = 12)]
	[Range(0, 1000000)]
	public decimal RiesgoUsd { get => _riesgoUsd; set => Cambiar(ref _riesgoUsd, value); }

	[Display(Name = "Valor del tick por contrato (USD)", GroupName = "1. Riesgo", Order = 13, Description = "0,50 en MNQ.")]
	[Range(0, 1000)]
	public decimal ValorTick { get => _valorTick; set => Cambiar(ref _valorTick, value); }

	[Display(Name = "Comisión ida y vuelta (USD)", GroupName = "1. Riesgo", Order = 14)]
	[Range(0, 1000)]
	public decimal Comision { get => _comision; set => Cambiar(ref _comision, value); }

	[Display(Name = "Deslizamiento total (ticks)", GroupName = "1. Riesgo", Order = 15)]
	[Range(0, 100)]
	public int DeslizamientoTicks { get => _deslizamiento; set => Cambiar(ref _deslizamiento, value); }

	[Display(Name = "Máximo de contratos", GroupName = "1. Riesgo", Order = 16)]
	[Range(1, 1000)]
	public int MaximoContratos { get => _maxContratos; set => Cambiar(ref _maxContratos, value); }

	[Display(Name = "RR neto mínimo", GroupName = "1. Riesgo", Order = 17)]
	[Range(0, 100)]
	public decimal RrMinimo { get => _rrMinimo; set => Cambiar(ref _rrMinimo, value); }

	#endregion

	#region Ajustes: estructura y liquidez

	private int _fuerza1H = 2, _margenEstructural = 4, _fuerzaInterna = 2;
	private bool _tokio, _londres = true, _nuevaYork = true;
	private string _cierres = "";
	private int _penetracion = 4, _rec1M = 16, _rec5M = 32, _plazo = 10, _vigencia = 30;
	private ReglaRecuperacion _regla = ReglaRecuperacion.PrimeraValida;

	[Display(Name = "Fuerza de pivote 1H", GroupName = "2. Estructura 1H", Order = 20)]
	[Range(1, 10)]
	public int FuerzaPivote1H { get => _fuerza1H; set => Cambiar(ref _fuerza1H, value); }

	[Display(Name = "Margen estructural (ticks)", GroupName = "2. Estructura 1H", Order = 21)]
	[Range(0, 400)]
	public int MargenEstructuralTicks { get => _margenEstructural; set => Cambiar(ref _margenEstructural, value); }

	[Display(Name = "Fuerza de pivote 1M/5M", GroupName = "2. Estructura 1H", Order = 22, Description = "Liquidez interna, directrices y divergencias.")]
	[Range(1, 10)]
	public int FuerzaPivoteInterno { get => _fuerzaInterna; set => Cambiar(ref _fuerzaInterna, value); }

	[Display(Name = "Ventana Tokio (09:00–15:00 Tokio)", GroupName = "3. Liquidez y barridos", Order = 30)]
	public bool VentanaTokio { get => _tokio; set => Cambiar(ref _tokio, value); }

	[Display(Name = "Ventana Londres (08:00–12:00 Londres)", GroupName = "3. Liquidez y barridos", Order = 31)]
	public bool VentanaLondres { get => _londres; set => Cambiar(ref _londres, value); }

	[Display(Name = "Ventana Nueva York (09:30–16:00 NY)", GroupName = "3. Liquidez y barridos", Order = 32)]
	public bool VentanaNuevaYork { get => _nuevaYork; set => Cambiar(ref _nuevaYork, value); }

	[Display(Name = "Cierres anticipados NY", GroupName = "3. Liquidez y barridos", Order = 33, Description = "Formato: 2026-11-27 13:00; 2026-12-24 13:00 (hora de Nueva York).")]
	public string CierresAnticipadosNy { get => _cierres; set => Cambiar(ref _cierres, value ?? ""); }

	[Display(Name = "Penetración mínima (ticks) (propuesto)", GroupName = "3. Liquidez y barridos", Order = 34)]
	[Range(1, 400)]
	public int PenetracionMinimaTicks { get => _penetracion; set => Cambiar(ref _penetracion, value); }

	[Display(Name = "Regla de recuperación", GroupName = "3. Liquidez y barridos", Order = 35)]
	public ReglaRecuperacion ReglaRecuperacion { get => _regla; set => Cambiar(ref _regla, value); }

	[Display(Name = "Recuperación 1M (ticks) (propuesto)", GroupName = "3. Liquidez y barridos", Order = 36)]
	[Range(0, 400)]
	public int RecuperacionTicks1M { get => _rec1M; set => Cambiar(ref _rec1M, value); }

	[Display(Name = "Recuperación 5M (ticks) (propuesto)", GroupName = "3. Liquidez y barridos", Order = 37)]
	[Range(0, 400)]
	public int RecuperacionTicks5M { get => _rec5M; set => Cambiar(ref _rec5M, value); }

	[Display(Name = "Plazo de recuperación (min) (propuesto)", GroupName = "3. Liquidez y barridos", Order = 38)]
	[Range(1, 240)]
	public int PlazoRecuperacionMinutos { get => _plazo; set => Cambiar(ref _plazo, value); }

	[Display(Name = "Vigencia de oportunidad (min) (propuesto)", GroupName = "3. Liquidez y barridos", Order = 39)]
	[Range(1, 600)]
	public int VigenciaOportunidadMinutos { get => _vigencia; set => Cambiar(ref _vigencia, value); }

	#endregion

	#region Ajustes: absorción, modelos y gatillo

	private ModoFiltro _absorcion = ModoFiltro.Informativo, _directriz = ModoFiltro.Informativo;
	private decimal _volClimatico = 1.5m, _agresion = 10m, _posGatillo = 0.60m, _expansion = 1.2m;
	private int _avance = 24, _velasCambio = 2, _margenRuptura = 2, _cuerpo = 4, _fvgMin = 2, _fvgCerca = 8, _bufferSl = 12, _anticipacion = 2;
	private bool _m1 = true, _m2 = true, _independientes, _exigirFvg;
	private AnclajeSlFvg _anclaje = AnclajeSlFvg.Rechazo;

	[Display(Name = "Absorción", GroupName = "4. Absorción", Order = 40, Description = "Volumen climático, agresión contraria, escaso avance y cambio de delta.")]
	public ModoFiltro ModoAbsorcion { get => _absorcion; set => Cambiar(ref _absorcion, value); }

	[Display(Name = "Volumen climático (× media 20) (propuesto)", GroupName = "4. Absorción", Order = 41)]
	[Range(0, 100)]
	public decimal MultiplicadorVolumenClimatico { get => _volClimatico; set => Cambiar(ref _volClimatico, value); }

	[Display(Name = "Agresión (% del volumen) (propuesto)", GroupName = "4. Absorción", Order = 42)]
	[Range(0, 100)]
	public decimal AgresionPct { get => _agresion; set => Cambiar(ref _agresion, value); }

	[Display(Name = "Avance máximo (ticks) (propuesto)", GroupName = "4. Absorción", Order = 43)]
	[Range(0, 1000)]
	public int AvanceMaximoTicks { get => _avance; set => Cambiar(ref _avance, value); }

	[Display(Name = "Velas para el cambio de delta", GroupName = "4. Absorción", Order = 44)]
	[Range(0, 20)]
	public int VelasCambioDelta { get => _velasCambio; set => Cambiar(ref _velasCambio, value); }

	[Display(Name = "Activar M1 (recuperación directa)", GroupName = "5. Modelos y gatillo", Order = 50)]
	public bool ActivarM1 { get => _m1; set => Cambiar(ref _m1, value); }

	[Display(Name = "Activar M2 (ruptura de LVN y retroceso)", GroupName = "5. Modelos y gatillo", Order = 51)]
	public bool ActivarM2 { get => _m2; set => Cambiar(ref _m2, value); }

	[Display(Name = "Modelos independientes", GroupName = "5. Modelos y gatillo", Order = 52,
		Description = "Desactivado: un barrido produce como mucho una señal. Activado: M1 y M2 pueden señalar el mismo evento (comparación independiente).")]
	public bool ModelosIndependientes { get => _independientes; set => Cambiar(ref _independientes, value); }

	[Display(Name = "Posición POC/VA del gatillo (propuesto)", GroupName = "5. Modelos y gatillo", Order = 53, Description = "Largos: POC y centro VA ≤ valor; cortos: ≥ 1 - valor.")]
	[Range(0, 1)]
	public decimal PosicionGatillo { get => _posGatillo; set => Cambiar(ref _posGatillo, value); }

	[Display(Name = "M2: margen de ruptura (ticks)", GroupName = "5. Modelos y gatillo", Order = 54)]
	[Range(0, 100)]
	public int MargenRupturaLvnTicks { get => _margenRuptura; set => Cambiar(ref _margenRuptura, value); }

	[Display(Name = "M2: cuerpo mínimo de ruptura (ticks)", GroupName = "5. Modelos y gatillo", Order = 55)]
	[Range(0, 400)]
	public int CuerpoMinimoRupturaTicks { get => _cuerpo; set => Cambiar(ref _cuerpo, value); }

	[Display(Name = "M2: expansión mínima (× rango medio 20) (propuesto)", GroupName = "5. Modelos y gatillo", Order = 56)]
	[Range(0, 20)]
	public decimal ExpansionMinimaRuptura { get => _expansion; set => Cambiar(ref _expansion, value); }

	[Display(Name = "M2: exigir FVG", GroupName = "5. Modelos y gatillo", Order = 57)]
	public bool ExigirFvg { get => _exigirFvg; set => Cambiar(ref _exigirFvg, value); }

	[Display(Name = "FVG: tamaño mínimo (ticks)", GroupName = "5. Modelos y gatillo", Order = 58)]
	[Range(1, 400)]
	public int FvgMinimoTicks { get => _fvgMin; set => Cambiar(ref _fvgMin, value); }

	[Display(Name = "FVG: cercanía al LVN (ticks)", GroupName = "5. Modelos y gatillo", Order = 59)]
	[Range(0, 400)]
	public int FvgCercaniaTicks { get => _fvgCerca; set => Cambiar(ref _fvgCerca, value); }

	[Display(Name = "M2 con FVG: anclaje del SL", GroupName = "5. Modelos y gatillo", Order = 60)]
	public AnclajeSlFvg AnclajeSlFvg { get => _anclaje; set => Cambiar(ref _anclaje, value); }

	[Display(Name = "Buffer del SL (ticks) (propuesto)", GroupName = "5. Modelos y gatillo", Order = 61)]
	[Range(0, 400)]
	public int BufferSlTicks { get => _bufferSl; set => Cambiar(ref _bufferSl, value); }

	[Display(Name = "Anticipación del TP (ticks)", GroupName = "5. Modelos y gatillo", Order = 62)]
	[Range(0, 400)]
	public int AnticipacionTpTicks { get => _anticipacion; set => Cambiar(ref _anticipacion, value); }

	[Display(Name = "Directriz del retroceso", GroupName = "5. Modelos y gatillo", Order = 63)]
	public ModoFiltro ModoDirectriz { get => _directriz; set => Cambiar(ref _directriz, value); }

	#endregion

	#region Ajustes: LVN

	private decimal _banda = 400m, _umbralValle = 0.60m, _separacion = 10m;
	private int _hombro = 24, _suavizado = 2, _altas = 3, _secundarias = 5, _alejamiento = 40, _plazoReaccion = 15, _adversa = 20;

	[Display(Name = "Banda de búsqueda (± puntos)", GroupName = "6. LVN", Order = 70)]
	[Range(10, 5000)]
	public decimal BandaLvnPuntos { get => _banda; set => Cambiar(ref _banda, value); }

	[Display(Name = "Anchura de hombros (ticks)", GroupName = "6. LVN", Order = 71)]
	[Range(2, 400)]
	public int HombroLvnTicks { get => _hombro; set => Cambiar(ref _hombro, value); }

	[Display(Name = "Umbral de valle (volumen / hombro)", GroupName = "6. LVN", Order = 72)]
	[Range(0, 1)]
	public decimal UmbralValle { get => _umbralValle; set => Cambiar(ref _umbralValle, value); }

	[Display(Name = "Suavizado del perfil (ticks)", GroupName = "6. LVN", Order = 73)]
	[Range(0, 20)]
	public int SuavizadoLvnTicks { get => _suavizado; set => Cambiar(ref _suavizado, value); }

	[Display(Name = "Separación mínima entre LVN (puntos)", GroupName = "6. LVN", Order = 74, Description = "De dos LVN más cercanos que esto queda el de mayor puntuación.")]
	[Range(0, 1000)]
	public decimal SeparacionMinimaLvnPuntos { get => _separacion; set => Cambiar(ref _separacion, value); }

	[Display(Name = "LVN de alta prioridad (amarillos)", GroupName = "6. LVN", Order = 75)]
	[Range(0, 50)]
	public int CantidadLvnAltaPrioridad { get => _altas; set => Cambiar(ref _altas, value); }

	[Display(Name = "LVN secundarios (naranjas)", GroupName = "6. LVN", Order = 76)]
	[Range(0, 50)]
	public int CantidadLvnSecundarios { get => _secundarias; set => Cambiar(ref _secundarias, value); }

	[Display(Name = "Reacción: alejamiento (ticks) (propuesto)", GroupName = "6. LVN", Order = 77)]
	[Range(1, 1000)]
	public int ReaccionAlejamientoTicks { get => _alejamiento; set => Cambiar(ref _alejamiento, value); }

	[Display(Name = "Reacción: plazo (min)", GroupName = "6. LVN", Order = 78)]
	[Range(1, 600)]
	public int ReaccionPlazoMinutos { get => _plazoReaccion; set => Cambiar(ref _plazoReaccion, value); }

	[Display(Name = "Reacción: excursión adversa máxima (ticks)", GroupName = "6. LVN", Order = 79)]
	[Range(0, 1000)]
	public int ReaccionExcursionAdversaTicks { get => _adversa; set => Cambiar(ref _adversa, value); }

	#endregion

	#region Ajustes: EMA, gestión simulada y CVD

	private int _periodoEma = 15, _vigCandidata = 5, _margenSalida = 2, _bufferPoc = 4;
	private MarcoEma _marcoEma = MarcoEma.M1;
	private bool _salidaEma = true, _simular = true, _exigirEma, _proteccionPoc;
	private decimal _agotamiento = 0.70m, _volExtremo = 5m, _iniciativa = 10m;
	private ReglaSubasta _subasta = ReglaSubasta.SinAgresionEnExtremo;
	private string _reinicioCvd = "09:30";

	[Display(Name = "EMA: periodo", GroupName = "7. EMA y gestión simulada", Order = 80)]
	[Range(1, 500)]
	public int PeriodoEma { get => _periodoEma; set => Cambiar(ref _periodoEma, value); }

	[Display(Name = "EMA: temporalidad", GroupName = "7. EMA y gestión simulada", Order = 81)]
	public MarcoEma MarcoEma { get => _marcoEma; set => Cambiar(ref _marcoEma, value); }

	[Display(Name = "Cerrar al cierre de la vela cruzada por la EMA", GroupName = "7. EMA y gestión simulada", Order = 82,
		Description = "Activado: la operación simulada se cierra al cierre de la vela que la EMA cruza en contra. Desactivado: solo advertencia.")]
	public bool SalidaPorEma { get => _salidaEma; set => Cambiar(ref _salidaEma, value); }

	[Display(Name = "Simular resultados (estadística)", GroupName = "7. EMA y gestión simulada", Order = 83)]
	public bool SimularResultados { get => _simular; set => Cambiar(ref _simular, value); }

	[Display(Name = "Agotamiento: POC/VA relativo (propuesto)", GroupName = "7. EMA y gestión simulada", Order = 84)]
	[Range(0, 1)]
	public decimal AgotamientoPosicion { get => _agotamiento; set => Cambiar(ref _agotamiento, value); }

	[Display(Name = "Agotamiento: regla de subasta terminada", GroupName = "7. EMA y gestión simulada", Order = 85)]
	public ReglaSubasta ReglaSubasta { get => _subasta; set => Cambiar(ref _subasta, value); }

	[Display(Name = "Agotamiento: volumen mínimo en el extremo", GroupName = "7. EMA y gestión simulada", Order = 86)]
	[Range(0, 100000)]
	public decimal VolumenMinimoExtremo { get => _volExtremo; set => Cambiar(ref _volExtremo, value); }

	[Display(Name = "Candidata: vigencia (velas)", GroupName = "7. EMA y gestión simulada", Order = 87)]
	[Range(1, 100)]
	public int VigenciaCandidataVelas { get => _vigCandidata; set => Cambiar(ref _vigCandidata, value); }

	[Display(Name = "Arranque contrario: margen (ticks)", GroupName = "7. EMA y gestión simulada", Order = 88)]
	[Range(0, 100)]
	public int MargenSalidaTicks { get => _margenSalida; set => Cambiar(ref _margenSalida, value); }

	[Display(Name = "Arranque contrario: delta relativo (%)", GroupName = "7. EMA y gestión simulada", Order = 89)]
	[Range(0, 100)]
	public decimal IniciativaDeltaPct { get => _iniciativa; set => Cambiar(ref _iniciativa, value); }

	[Display(Name = "Exigir cruce de EMA para salir", GroupName = "7. EMA y gestión simulada", Order = 90)]
	public bool ExigirEmaParaSalir { get => _exigirEma; set => Cambiar(ref _exigirEma, value); }

	[Display(Name = "Protección POC del stop", GroupName = "7. EMA y gestión simulada", Order = 91)]
	public bool ProteccionPoc { get => _proteccionPoc; set => Cambiar(ref _proteccionPoc, value); }

	[Display(Name = "Protección POC: buffer (ticks)", GroupName = "7. EMA y gestión simulada", Order = 92)]
	[Range(0, 100)]
	public int BufferPocTicks { get => _bufferPoc; set => Cambiar(ref _bufferPoc, value); }

	[Display(Name = "CVD: hora de reinicio (Nueva York, hh:mm)", GroupName = "8. CVD", Order = 95)]
	public string ReinicioCvd { get => _reinicioCvd; set => Cambiar(ref _reinicioCvd, value ?? "09:30"); }

	#endregion

	public IndicadorCompleto()
		: base(true)
	{
		// Solo dibuja: la serie por defecto se oculta para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	private void Cambiar<T>(ref T campo, T valor)
	{
		campo = valor;
		RecalculateValues();
	}

	private ConfiguracionCompleto Configuracion() => new()
	{
		TamanoTick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m,
		Instrumento = InstrumentInfo?.Instrument ?? "",
		InstrumentosPermitidos = _permitidos,
		ValoresMonetariosVerificados = _verificados,
		RiesgoUsd = _riesgoUsd,
		ValorTick = _valorTick,
		ComisionIdaVuelta = _comision,
		DeslizamientoTicks = _deslizamiento,
		MaximoContratos = _maxContratos,
		RrNetoMinimo = _rrMinimo,
		FuerzaPivote1H = _fuerza1H,
		MargenEstructuralTicks = _margenEstructural,
		FuerzaPivoteInterno = _fuerzaInterna,
		VentanaTokio = _tokio,
		VentanaLondres = _londres,
		VentanaNuevaYork = _nuevaYork,
		CierresAnticipadosNy = _cierres,
		PenetracionMinimaTicks = _penetracion,
		ReglaRecuperacion = _regla,
		RecuperacionTicks1M = _rec1M,
		RecuperacionTicks5M = _rec5M,
		PlazoRecuperacionMinutos = _plazo,
		VigenciaOportunidadMinutos = _vigencia,
		ModoAbsorcion = _absorcion,
		MultiplicadorVolumenClimatico = _volClimatico,
		AgresionPct = _agresion,
		AvanceMaximoTicks = _avance,
		VelasCambioDelta = _velasCambio,
		ActivarM1 = _m1,
		ActivarM2 = _m2,
		ModelosIndependientes = _independientes,
		PosicionGatillo = _posGatillo,
		MargenRupturaLvnTicks = _margenRuptura,
		CuerpoMinimoRupturaTicks = _cuerpo,
		ExpansionMinimaRuptura = _expansion,
		ExigirFvg = _exigirFvg,
		FvgMinimoTicks = _fvgMin,
		FvgCercaniaTicks = _fvgCerca,
		AnclajeSlFvg = _anclaje,
		BufferSlTicks = _bufferSl,
		AnticipacionTpTicks = _anticipacion,
		ModoDirectriz = _directriz,
		BandaLvnPuntos = _banda,
		HombroLvnTicks = _hombro,
		UmbralValle = _umbralValle,
		SuavizadoLvnTicks = _suavizado,
		SeparacionMinimaLvnPuntos = _separacion,
		CantidadLvnAltaPrioridad = _altas,
		CantidadLvnSecundarios = _secundarias,
		ReaccionAlejamientoTicks = _alejamiento,
		ReaccionPlazoMinutos = _plazoReaccion,
		ReaccionExcursionAdversaTicks = _adversa,
		PeriodoEma = _periodoEma,
		MarcoEma = _marcoEma,
		SalidaPorEma = _salidaEma,
		SimularResultados = _simular,
		AgotamientoPosicion = _agotamiento,
		ReglaSubasta = _subasta,
		VolumenMinimoExtremo = _volExtremo,
		VigenciaCandidataVelas = _vigCandidata,
		MargenSalidaTicks = _margenSalida,
		IniciativaDeltaPct = _iniciativa,
		ExigirEmaParaSalir = _exigirEma,
		ProteccionPoc = _proteccionPoc,
		BufferPocTicks = _bufferPoc,
		ReinicioCvdNy = TimeSpan.TryParseExact(_reinicioCvd, @"hh\:mm", CultureInfo.InvariantCulture, out var hora) ? hora : new TimeSpan(9, 30, 0)
	};

	#region Cálculo

	/// <summary>
	/// Minutos por vela del gráfico: por el tipo de gráfico de ATAS ("TimeFrame" con "M1" o "M5") o, si no se
	/// reconoce, por la menor separación entre las primeras velas cargadas.
	/// </summary>
	private int DetectarMarco()
	{
		if (ChartInfo?.ChartType == "TimeFrame")
		{
			switch (ChartInfo.TimeFrame)
			{
				case "M1":
					return 1;
				case "M5":
					return 5;
			}
		}

		var menor = double.MaxValue;

		for (var i = 0; i + 1 < Math.Min(CurrentBar, 40); i++)
		{
			var minutos = (GetCandle(i + 1).Time - GetCandle(i).Time).TotalMinutes;

			if (minutos > 0)
				menor = Math.Min(menor, minutos);
		}

		return menor == double.MaxValue ? 0 : (int)Math.Round(menor);
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			lock (_sync)
				Calcular(bar);
		}
		catch (Exception ex)
		{
			_estadoRol = "ERROR DE CÁLCULO: " + ex.Message;
			this.LogError("IndicadorCompleto: error de cálculo", ex);
		}
	}

	private void Calcular(int bar)
	{
		if (bar == 0)
			Reiniciar();
		else if (bar < _ultimaBarra)
			return;

		// Índice de tiempos de este gráfico (para dibujar en 1M o 5M lo que calculó el maestro).
		while (_tiempos.Count <= bar)
			_tiempos.Add(GetCandle(_tiempos.Count).Time);

		_tiempos[bar] = GetCandle(bar).Time;

		if (!_esMaestro)
		{
			_ultimaBarra = bar;
			return;
		}

		// Las velas anteriores a 'bar' ya cerraron: cada una se envía una sola vez, con sus valores finales.
		for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
			_motor!.Procesar(Entrada(i));

		_ultimaBarra = bar;
		_motor!.UltimoDatoUtc = DateTime.UtcNow;
		RevisarAlertas(bar);

		// Abre la ventana o, si ya estaba abierta (recálculo), le conecta el motor nuevo.
		if (!_ventanaAbierta && MostrarVentana)
		{
			_ventanaAbierta = true;
			_claveVentana = _clave;
			var motor = _motor;
			var clave = _clave;
			VentanaReporte.Abrir(clave, $"INDICADOR COMPLETO — {InstrumentInfo?.Instrument}", VentanaSiempreVisible,
				() => motor.Instantanea(DateTime.UtcNow, ZonaReporte, "Maestro 1M (velas de 1M cerradas)", RegistroMotores.Graficos(clave)),
				() => motor.ExportarJson(DateTime.UtcNow),
				() => motor.ExportarCsv(ZonaReporte),
				() => motor.Revision);
		}
	}

	private void Reiniciar()
	{
		_tiempos.Clear();
		_ultimaBarra = -1;
		_ventanaAbierta = false;
		_enVivo = false;
		_avisoSonado = 0;
		_marcoGrafico = DetectarMarco();

		var instrumento = InstrumentInfo?.Instrument ?? "";
		_clave = RegistroMotores.Clave(_grupo, instrumento);

		// Ventana de una clave anterior (cambió el grupo) o de cuando era maestro: se cierra.
		if (_claveVentana is { } anterior && (anterior != _clave || !MostrarVentana))
		{
			VentanaReporte.Cerrar(anterior);
			_claveVentana = null;
		}

		var esUnMinuto = _marcoGrafico == 1;
		var quiereMaestro = _rol == RolIndicador.Maestro1M || (_rol == RolIndicador.Automatico && esUnMinuto && !RegistroMotores.HayOtroMaestro(_clave, this));

		_esMaestro = false;
		_motor = null;

		if (quiereMaestro && !esUnMinuto)
		{
			_estadoRol = "EL MAESTRO EXIGE UN GRÁFICO DE 1 MINUTO: no calcula";
		}
		else if (quiereMaestro)
		{
			var motor = new MotorCompleto(Configuracion()) { Descripcion = _clave };

			if (RegistroMotores.RegistrarMaestro(_clave, this, motor))
			{
				_esMaestro = true;
				_motor = motor;
				_estadoRol = $"MAESTRO 1M · grupo {_grupo} · {instrumento}";
			}
			else
			{
				_estadoRol = "YA EXISTE UN MAESTRO PARA ESTE GRUPO E INSTRUMENTO: actúa como visor";
			}
		}

		if (!_esMaestro)
		{
			var marco = _rol switch
			{
				RolIndicador.Visor5M => 5,
				RolIndicador.Visor1M => 1,
				_ => _marcoGrafico
			};

			_estadoRol = _estadoRol.StartsWith("EL MAESTRO") || _estadoRol.StartsWith("YA EXISTE") ? _estadoRol : $"VISOR {marco}M · grupo {_grupo} · {instrumento}";
			_marcoGrafico = marco;
			RegistroMotores.Visor(_clave, this, $"{marco}M");

			// Un visor no tiene ventana propia.
			if (_claveVentana is { } suya)
			{
				VentanaReporte.Cerrar(suya);
				_claveVentana = null;
			}
		}
	}

	/// <summary>Vela de ATAS a la entrada del motor, con el volumen por precio (Bid = ventas, Ask = compras agresivas).</summary>
	private EntradaVela1M Entrada(int bar)
	{
		var vela = GetCandle(bar);
		var entrada = new EntradaVela1M
		{
			InicioUtc = vela.Time,
			Apertura = vela.Open,
			Maximo = vela.High,
			Minimo = vela.Low,
			Cierre = vela.Close,
			Volumen = vela.Volume,
			Delta = vela.Delta,
			DeltaMaximo = vela.MaxDelta,
			DeltaMinimo = vela.MinDelta
		};

		var tick = InstrumentInfo?.TickSize ?? 0m;

		if (tick <= 0)
			return entrada;

		var niveles = 0;

		for (var precio = vela.Low; precio <= vela.High && niveles < 10000; precio += tick, niveles++)
		{
			if (vela.GetPriceVolumeInfo(precio) is { } info)
				entrada.Niveles.Add(new NivelEntrada(precio, info.Bid, info.Ask));
		}

		return entrada;
	}

	protected override void OnDispose()
	{
		try
		{
			if (_claveVentana is { } clave)
				VentanaReporte.Cerrar(clave);

			RegistroMotores.Quitar(this);
		}
		catch (Exception ex)
		{
			this.LogError("IndicadorCompleto: error al cerrar", ex);
		}

		base.OnDispose();
	}

	/// <summary>Motor que dibuja este gráfico: el propio si es maestro; si no, el del maestro del grupo.</summary>
	private MotorCompleto? MotorActivo() => _esMaestro ? _motor : RegistroMotores.Motor(_clave);

	#endregion
}
