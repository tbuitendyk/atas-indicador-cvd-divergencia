#!/usr/bin/env python3
"""Genera Atas/Generado/<Indicador>.g.cs y <Indicador>.Mbo.g.cs para los cinco indicadores de la suite.

Cada parámetro se define UNA vez (bloques de abajo) y cada indicador expone los bloques que usa: así el
AbsorptionMaster, que ejecuta todos los motores, tiene los mismos ajustes que cada indicador por separado.
Uso (desde la raíz del repositorio):  python3 SuiteOrderFlowStacker/herramientas/generar_parametros.py
"""
import os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SALIDA = os.path.join(RAIZ, "Atas", "Generado")

# (propiedad, tipo, destino, nombre en español, descripción, mínimo, máximo, recalcular)
# tipo: int | decimal | bool | string | sonido | enum:<Tipo>
FAMILIAS = [("", "NQ", "NQ"), ("Mnq", "MNQ", "MNQ"), ("Es", "ES", "ES"), ("Mes", "MES", "MES"), ("Ym", "YM", "YM"), ("Mym", "MYM", "MYM")]


def por_familia(nombre, campo, titulo, desc, mn, mx, recalc):
    """Un umbral en contratos para cada familia (v3, 2.4). NQ conserva el nombre de siempre (plantillas guardadas)."""
    filas = []
    for prefijo, raiz, etiqueta in FAMILIAS:
        nota = "" if raiz == "NQ" else " PROVISIONAL: calibrar con el CSV de " + raiz + "."
        filas.append((f"{prefijo}{nombre}", "int", f"_c.Cfg.Contratos.{raiz}.{campo}", f"{etiqueta}: {titulo}", desc + nota, mn, mx, recalc))
    return filas


GENERAL = ("General", [
    ("DisplayMode", "enum:ModoVisual", "_c.General.DisplayMode", "Modo visual",
     "Minimal = solo el dato en texto (recomendado); Markers = dato + marcador pequeño; Full = todo dibujado.", None, None, False),
    ("InfoPanelPosition", "enum:EsquinaPanel", "_c.General.InfoPanelPosition", "Esquina del panel",
     "Esquina del panel de texto. Con varios indicadores de la suite, use esquinas o desplazamientos distintos.", None, None, False),
    ("InfoPanelOffsetX", "int", "_c.General.InfoPanelOffsetX", "Panel: desplazamiento horizontal (px)",
     "Separa el panel del borde (para no tapar el de otro indicador). 0 = pegado a la esquina.", 0, 3000, False),
    ("InfoPanelOffsetY", "int", "_c.General.InfoPanelOffsetY", "Panel: desplazamiento vertical (px)",
     "Separa el panel del borde (para no tapar el de otro indicador).", 0, 3000, False),
    ("InfoPanelMaxLines", "int", "_c.General.InfoPanelMaxLines", "Líneas del panel",
     "Últimos N eventos mostrados (1-20). Recomendado: 5.", 1, 20, False),
    ("FontSize", "int", "_c.General.FontSize", "Tamaño de letra",
     "Tamaño de fuente de etiquetas y panel. Recomendado: 11.", 6, 30, False),
    ("EventLifetimeMin", "int", "_c.General.EventLifetimeMin", "Vida de un evento (min)",
     "Minutos que un evento sigue visible y 'reciente'. Recomendado: 30.", 1, 1440, False),
    ("InstrumentProfile", "enum:PerfilInstrumento", "_c.General.InstrumentProfile", "Perfil de instrumento",
     "Auto = por el símbolo de ATAS (NQ, MNQ, ES, MES, YM, MYM; sin reconocer: NQ). Elige la tabla de umbrales por sesión y los umbrales en contratos de esa familia.", None, None, False),
    ("TrackerAlerts", "bool", "_c.General.EnableAlerts", "Alertas",
     "v4: apagadas en los trackers (tapaban la vista y el panel de órdenes); el Indicador Global Session Sweep solo avisa lo del barrido. Una alerta por evento, solo en vivo.", None, None, False),
    ("ShowOwnPanel", "bool", "_c.General.ShowInfoPanel", "Panel propio en el gráfico",
     "Apagado en los trackers: su último evento se ve en el panel del Indicador Global Session Sweep. Su dibujo en el gráfico no cambia.", None, None, False),
    ("AlertSoundFile", "sonido", "_c.General.AlertSoundFile", "Sonido de la alerta",
     "Sonido de ATAS para las alertas. Por defecto: alert1.", None, None, False),
    ("EnableRecording", "bool", "_c.General.EnableRecording", "Grabar en CSV",
     "Graba cada evento en %APPDATA%\\ATAS\\OrderFlowSuite\\{Instrumento}\\yyyy-MM-dd.csv (para calibrar).", None, None, False),
    ("RealTimeOnly", "bool", "_c.General.RealTimeOnly", "Solo tiempo real",
     "Si se activa, no se reconstruyen eventos con las velas del historial (solo los de ahora en adelante).", None, None, True),
    ("LoadRecordedEvents", "bool", "_c.General.LoadRecordedEvents", "Cargar eventos grabados",
     "Al cargar, lee los CSV de los últimos días y vuelve a dibujar sus eventos (en gris, sin sonar).", None, None, True),
    ("RecordedDaysToLoad", "int", "_c.General.RecordedDaysToLoad", "Días grabados a cargar",
     "Días de CSV que se cargan (hoy incluido). Recomendado: 5.", 1, 60, True),
])

IMBALANCE = ("Imbalance", [
    ("ImbalanceRatioPercent", "decimal", "_c.Cfg.Imbalance.ImbalanceRatioPercent", "Ratio diagonal mínimo (%)",
     "Ask del nivel N contra Bid del nivel N-1 (compra) o Bid de N contra Ask de N+1 (venta). 150-1000 %. Recomendado: 300.", 150, 1000, True),
    ("MinStackedLevels", "int", "_c.Cfg.Imbalance.MinStackedLevels", "Niveles consecutivos mínimos",
     "Niveles seguidos con imbalance del mismo lado para formar la zona (2-10). Recomendado: 3.", 2, 10, True),
    *por_familia("MinLevelVolumeContracts", "MinLevelVolumeContracts", "volumen mínimo por nivel (contratos)",
                 "Volumen mínimo del lado dominante por nivel en RTH.", 1, 100000, True),
    ("OffHoursFactor", "decimal", "_c.Cfg.Imbalance.OffHoursFactor", "Factor fuera de RTH",
     "Multiplicador del volumen mínimo fuera de 09:30-16:00 ET. Recomendado: 0,5.", 0.1, 2, True),
    ("IgnoreZeroSide", "bool", "_c.Cfg.Imbalance.IgnoreZeroSide", "Lado débil a cero: exigir el mínimo",
     "Activado: un lado débil a 0 cuenta como 1 contrato y exige el volumen mínimo (sin 'infinitos'). Desactivado: ese nivel no cuenta.", None, None, True),
    ("ZoneExtendBars", "int", "_c.Cfg.Imbalance.ZoneExtendBars", "Velas que se extiende la zona",
     "Velas que la zona sigue viva y dibujada. Recomendado: 50.", 1, 2000, True),
    ("MinBreakTicks", "int", "_c.Cfg.Imbalance.MinBreakTicks", "Ticks de cierre más allá -> ROTA",
     "Cierre de vela a estos ticks (o más) fuera de la zona = ROTA. Recomendado: 2.", 1, 50, True),
    ("MaxActiveZones", "int", "_c.Cfg.Imbalance.MaxActiveZones", "Zonas vivas máximas",
     "Las más antiguas dejan de extenderse. Recomendado: 20.", 1, 200, True),
])

ICEBERG = ("Filtros Iceberg", [
    ("MboMode", "enum:ModoMbo", "_c.Cfg.Iceberg.ModoMbo", "Datos MBO",
     "Automático: MBO (nativo + sintético) si el feed lo envía (Rithmic); si no, inferido. Solo inferido: ignora el MBO.", None, None, False),
    ("MinExecVisibleRatio", "decimal", "_c.Cfg.Iceberg.MinExecVisibleRatio", "Ejecutado / visible mínimo",
     "Veces que lo ejecutado supera al tamaño visible (clip). Recomendado: 3.", 1, 50, False),
    ("HighProbExecVisibleRatio", "decimal", "_c.Cfg.Iceberg.HighProbExecVisibleRatio", "Ratio de alta probabilidad",
     "Ratio para la marca * (junto con el volumen de alta probabilidad de la sesión). Recomendado: 5.", 1, 100, False),
    ("MaxDisplacementTicks", "int", "_c.Cfg.Iceberg.MaxDisplacementTicks", "Desplazamiento máximo (ticks)",
     "Ticks que el precio puede atravesar el nivel mientras se ejecuta (1-10). Recomendado: 2.", 1, 10, False),
    ("DetectionWindowSeconds", "int", "_c.Cfg.Iceberg.DetectionWindowSeconds", "Ventana del evento (s)",
     "Sin actividad durante estos segundos, un candidato vuelve a empezar (5-600). Recomendado: 60.", 5, 600, False),
    *por_familia("MinVisibleClipContracts", "MinVisibleClipContracts", "clip mínimo (informativo)",
                 "Informativo, NO es filtro.", 1, 1000, False),
    *por_familia("MaxVisibleClipContracts", "MaxVisibleClipContracts", "clip máximo considerado",
                 "Un tamaño visible mayor no se considera iceberg.", 1, 10000, False),
    ("SyntheticRefillMaxMs", "int", "_c.Cfg.Iceberg.SyntheticRefillMaxMs", "Sintético: tiempo máximo (ms)",
     "Tiempo entre el llenado y la orden nueva parecida. Recomendado: 50.", 1, 2000, False),
    ("SyntheticSizeTolerancePercent", "int", "_c.Cfg.Iceberg.SyntheticSizeTolerancePercent", "Sintético: tolerancia de tamaño (%)",
     "Diferencia de tamaño aceptada entre la orden llenada y la nueva. Recomendado: 20.", 0, 100, False),
    ("DetectSynthetic", "bool", "_c.Cfg.Iceberg.DetectSynthetic", "Detectar sintéticos",
     "Icebergs gestionados por software (orden nueva tras cada llenado). Necesita MBO.", None, None, False),
    ("HoldConfirmTicks", "int", "_c.Cfg.Iceberg.HoldConfirmTicks", "Ticks a favor -> AGUANTANDO",
     "Sin recargar y con el precio a estos ticks a favor, el iceberg pasa a AGUANTANDO. Recomendado: 4.", 1, 50, False),
    ("BreakTicks", "int", "_c.Cfg.Iceberg.BreakTicks", "Ticks atravesados -> ROTO",
     "El precio atraviesa el nivel estos ticks: ROTO (agotado). Recomendado: 2.", 1, 50, False),
    ("ActiveRefillSeconds", "int", "_c.Cfg.Iceberg.ActiveRefillSeconds", "ACTIVO: recarga en los últimos (s)",
     "ACTIVO mientras recarga o se ejecuta en estos segundos. Recomendado: 10.", 1, 300, False),
    ("MaxTrackedOrders", "int", "_c.Cfg.Iceberg.MaxTrackedOrders", "Órdenes MBO seguidas (máximo)",
     "Límite de memoria en la apertura de NY. Recomendado: 20000.", 1000, 500000, False),
])

SESIONES = [("Asia", "Asia", "18:00-02:00"), ("London", "Londres", "02:00-premercado"), ("Premarket", "Premercado", "1 h antes de las 09:30"),
            ("NyOpen", "AperturaNY", "09:30-11:00"), ("Midday", "Mediodia", "11:00-14:00"), ("Close", "Cierre", "14:00-16:00"),
            ("OffHours", "FueraDeHorario", "16:00-18:00")]
NOMBRES = {"Asia": "Asia", "Londres": "Londres", "Premercado": "Premercado NY", "AperturaNY": "Apertura NY", "Mediodia": "Mediodía",
           "Cierre": "Cierre", "FueraDeHorario": "Fuera de horario"}


def umbrales(prefijo, tabla, etiqueta):
    filas = []
    if prefijo == "":
        filas.append(("PremarketMinutes", "int", "_c.Cfg.Sesiones.PremercadoMinutos", "Premercado NY: minutos antes de las 09:30",
                      "Duración de la sesión de premercado (configurable). Recomendado: 60.", 0, 450, False))
    for ingles, sesion, horario in SESIONES:
        n = NOMBRES[sesion]
        filas += [
            (f"{prefijo}{ingles}MinExecutedContracts", "umbral", f"_c.Cfg.Umbrales.{tabla}[Sesion.{sesion}].MinEjecutado",
             f"{n}: ejecutado mínimo (ruido)", f"{etiqueta} {horario} ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR.", 1, 100000, False),
            (f"{prefijo}{ingles}HighProbContracts", "umbral", f"_c.Cfg.Umbrales.{tabla}[Sesion.{sesion}].AltaProbabilidad",
             f"{n}: alta probabilidad", f"{etiqueta} {horario} ET. Contratos para la marca * (con el ratio de alta probabilidad).", 1, 100000, False),
            (f"{prefijo}{ingles}MinRefills", "umbralint", f"_c.Cfg.Umbrales.{tabla}[Sesion.{sesion}].MinRecargas",
             f"{n}: recargas mínimas", f"{etiqueta} {horario} ET. Recargas vistas (nativo y sintético).", 1, 1000, False),
        ]
    return filas


UMBRALES_NQ = ("Umbrales por sesión (NQ)", umbrales("", "Nq", "NQ,"))
UMBRALES_MNQ = ("Umbrales por sesión (MNQ)", umbrales("Mnq", "Mnq", "MNQ (punto de partida: NQ x 0,5),"))
UMBRALES_ES = ("Umbrales por sesión (ES)", umbrales("Es", "Es", "ES PROVISIONAL (punto de partida: NQ x 2,5),"))
UMBRALES_MES = ("Umbrales por sesión (MES)", umbrales("Mes", "Mes", "MES PROVISIONAL (punto de partida: NQ x 1,25),"))
UMBRALES_YM = ("Umbrales por sesión (YM)", umbrales("Ym", "Ym", "YM PROVISIONAL (punto de partida: NQ x 0,4),"))
UMBRALES_MYM = ("Umbrales por sesión (MYM)", umbrales("Mym", "Mym", "MYM PROVISIONAL (punto de partida: NQ x 0,2),"))

SESGO = ("Sesgo", [
    ("EventContextSeconds", "int", "_c.Cfg.Iceberg.EventContextSeconds", "Ventana con barrido / stops (s)",
     "Relaciona un iceberg con un barrido o toma de stops (GIRO) y define los setups B y D. Recomendado: 30.", 5, 600, False),
    ("BiasLevelToleranceTicks", "int", "_c.Cfg.Iceberg.LevelToleranceTicks", "Tolerancia a VAH/VAL/LVN/POC/VWAP (ticks)",
     "Distancia para decir que el iceberg está 'en' un extremo o retroceso. Recomendado: 4.", 0, 50, False),
    ("TrendLengthBars", "int", "_c.Cfg.TrendLengthBars", "Longitud del filtro de tendencia (velas)",
     "Velas de la EMA (si la fuente es EMA). Recomendado: 50.", 2, 1000, True),
    ("TrendSource", "enum:FuenteTendencia", "_c.Cfg.TrendSource", "Fuente de tendencia",
     "VWAP (precio sobre/bajo la VWAP de la sesión) o EMA (pendiente).", None, None, True),
])

STOPS = ("Stops", [
    *por_familia("MinCascadeContractsRth", "MinCascadeContractsRth", "cascada mínima en RTH (contratos)",
                 "Volumen mínimo de la ráfaga en 09:30-16:00 ET.", 1, 100000, False),
    *por_familia("MinCascadeContractsEth", "MinCascadeContractsEth", "cascada mínima fuera de RTH (contratos)",
                 "Volumen mínimo fuera de RTH.", 1, 100000, False),
    ("CascadeMaxMs", "int", "_c.Cfg.Stops.CascadeMaxMs", "Duración máxima de la ráfaga (ms)",
     "Ráfaga de órdenes a mercado en la misma dirección. Recomendado: 300.", 10, 10000, False),
    ("MinCascadeLevels", "int", "_c.Cfg.Stops.MinCascadeLevels", "Niveles mínimos atravesados",
     "Ticks que debe atravesar la ráfaga. Recomendado: 3.", 2, 100, False),
    ("SwingLengthBars", "int", "_c.Cfg.SwingLengthBars", "Velas a cada lado de un swing",
     "Un swing es el máximo/mínimo de 2N+1 velas. Recomendado: 20.", 2, 500, True),
    ("RoundNumberStep", "int", "_c.Cfg.RoundNumberStep", "Paso de números redondos (puntos)",
     "Zonas en múltiplos de este paso. NQ: 50. 0 = sin números redondos.", 0, 100000, True),
    ("ShowProbableZones", "bool", "_c.Cfg.Stops.ShowProbableZones", "Mostrar zonas probables",
     "Dibuja las zonas probables (modo Full) y avisa en el panel al acercarse el precio.", None, None, False),
    ("ZoneToleranceTicks", "int", "_c.Cfg.Stops.ZoneToleranceTicks", "Distancia a la zona (ticks)",
     "Distancia para asociar una cascada a una zona. Recomendado: 4.", 0, 100, False),
    ("StopReclaimSeconds", "int", "_c.Cfg.Stops.StopReclaimSeconds", "Vuelve dentro en (s) -> TRAMPA",
     "Si el precio vuelve dentro en este tiempo: TRAMPA; si no: CONTINUACIÓN. Recomendado: 60.", 1, 3600, False),
    ("IbMinutes", "int", "_c.Cfg.IbMinutes", "Duración del Initial Balance (min)",
     "IBH/IBL desde las 09:30 ET. Recomendado: 60.", 5, 390, True),
    ("RequireZone", "bool", "_c.Cfg.Stops.RequireZone", "Solo cascadas en una zona",
     "Activado: solo cascadas junto a una zona probable. Desactivado: también 'sin zona'.", None, None, False),
])

BARRIDOS = ("Sweeps", [
    ("MinSweepLevels", "int", "_c.Cfg.Barridos.MinSweepLevels", "Niveles mínimos",
     "Niveles barridos por una sola orden agresiva (4 ticks = 1 punto en NQ). Recomendado: 4.", 2, 200, False),
    *por_familia("MinSweepContracts", "MinSweepContracts", "volumen mínimo de la orden (contratos)",
                 "Volumen de la orden reconstruida.", 1, 100000, False),
    ("SweepReclaimSeconds", "int", "_c.Cfg.Barridos.SweepReclaimSeconds", "Vuelve dentro en (s) -> LIQUIDEZ",
     "Si vuelve dentro del rango en este tiempo: LIQUIDEZ (trampa); si no: RUPTURA. Recomendado: 30.", 1, 3600, False),
    ("LevelToleranceTicks", "int", "_c.Cfg.Barridos.LevelToleranceTicks", "Distancia al nivel de referencia (ticks)",
     "El barrido debe empezar a esta distancia del nivel (o antes) y cruzarlo. Recomendado: 4.", 0, 100, False),
    ("RefPdhPdl", "bool", "_c.Cfg.Barridos.RefPdhPdl", "Niveles: PDH / PDL", "Máximo y mínimo del día anterior.", None, None, False),
    ("RefIbhIbl", "bool", "_c.Cfg.Barridos.RefIbhIbl", "Niveles: IBH / IBL", "Initial Balance (cuando termina).", None, None, False),
    ("RefOnhOnl", "bool", "_c.Cfg.Barridos.RefOnhOnl", "Niveles: ONH / ONL", "Máximo y mínimo overnight.", None, None, False),
    ("RefSwings", "bool", "_c.Cfg.Barridos.RefSwings", "Niveles: swings", "Swings sin superar.", None, None, False),
    ("RefVahVal", "bool", "_c.Cfg.Barridos.RefVahVal", "Niveles: VAH / VAL", "Área de valor de la sesión RTH.", None, None, False),
    ("OnlyAtReferenceLevels", "bool", "_c.Cfg.Barridos.OnlyAtReferenceLevels", "Solo barridos en un nivel",
     "Activado: solo barridos en un nivel de referencia o contra un iceberg. Desactivado: también 'sin nivel'.", None, None, False),
])

PUNTUACION = ("Puntuación", [
    ("WeightIceberg", "int", "_c.Cfg.Master.WeightIceberg", "Peso: iceberg válido", "Recomendado: 30.", 0, 100, False),
    ("WeightHighProbBonus", "int", "_c.Cfg.Master.WeightHighProbBonus", "Peso extra: iceberg de alta probabilidad (*)", "Recomendado: 10.", 0, 100, False),
    ("WeightProfileLevel", "int", "_c.Cfg.Master.WeightProfileLevel", "Peso: en LVN / VAH / VAL / POC", "Recomendado: 20.", 0, 100, False),
    ("WeightSweepOrStops", "int", "_c.Cfg.Master.WeightSweepOrStops", "Peso: barrido o stops previos", "Recomendado: 20.", 0, 100, False),
    ("WeightImbalance", "int", "_c.Cfg.Master.WeightImbalance", "Peso: stacked imbalance del mismo lado", "Recomendado: 15.", 0, 100, False),
    ("WeightDeltaShift", "int", "_c.Cfg.Master.WeightDeltaShift", "Peso: giro del delta a favor", "Recomendado: 15.", 0, 100, False),
    ("MinSignalScore", "int", "_c.Cfg.Master.MinSignalScore", "Puntuación mínima para la señal",
     "Ponderación de reglas (NO es una probabilidad). Recomendado: 65.", 0, 100, False),
    ("ProfileTolTicks", "int", "_c.Cfg.Master.ProfileTolTicks", "Tolerancia 'en el nivel' (ticks)", "Recomendado: 4.", 0, 50, False),
])

SETUPS = ("Setups", [
    ("EnableSetupA", "bool", "_c.Cfg.Master.EnableSetupA", "Setup A: absorción en nivel", "Iceberg AGUANTANDO en LVN / VAH / VAL -> GIRO.", None, None, False),
    ("EnableSetupB", "bool", "_c.Cfg.Master.EnableSetupB", "Setup B: barrido + iceberg", "Stops o barrido de liquidez + iceberg que absorbe -> GIRO (máxima calidad).", None, None, False),
    ("EnableSetupC", "bool", "_c.Cfg.Master.EnableSetupC", "Setup C: rechazo de LVN", "Precio en LVN + stacked imbalance + giro del delta -> GIRO.", None, None, False),
    ("EnableSetupD", "bool", "_c.Cfg.Master.EnableSetupD", "Setup D: iceberg roto", "Iceberg ROTO + barrido de ruptura -> CONTINUIDAD.", None, None, False),
    ("SetupCZoneMaxAgeSeconds", "int", "_c.Cfg.Master.SetupCZoneMaxAgeSeconds", "Setup C: antigüedad máxima de la zona (s)",
     "La zona de stacked imbalance en el LVN debe ser reciente. Recomendado: 300.", 10, 7200, False),
])

GESTION = ("Gestión", [
    ("EntryConfirmTicks", "int", "_c.Cfg.Master.EntryConfirmTicks", "Entrada tras (ticks a favor)", "Recomendado: 4 (4-6).", 0, 50, False),
    ("StopBeyondTicks", "int", "_c.Cfg.Master.StopBeyondTicks", "Stop más allá del iceberg/extremo (ticks)", "Recomendado: 3 (2-4).", 0, 50, False),
    ("TargetMode", "enum:ModoObjetivo", "_c.Cfg.Master.TargetMode", "Objetivo",
     "Siguiente POC / VAH / VAL / LVN; si no hay, R:R fijo.", None, None, False),
    ("TargetRewardRisk", "decimal", "_c.Cfg.Master.TargetRewardRisk", "R:R fijo del objetivo",
     "Objetivo = entrada + R:R x riesgo (modo R:R fijo o si no hay siguiente nivel). Recomendado: 2.", 0.5, 20, False),
    ("MinRewardRisk", "decimal", "_c.Cfg.Master.MinRewardRisk", "R:R mínimo", "Si el objetivo no da este R:R, no hay señal. Recomendado: 1,5.", 0.1, 20, False),
])

FILTROS = ("Filtros", [
    ("NewsBlackoutMinutes", "int", "_c.Cfg.Master.NewsBlackoutMinutes", "Blackout de noticias (± min)", "Sin señales ±N min alrededor de las noticias. Recomendado: 2.", 0, 120, False),
    ("NewsTimes", "string", "_c.Cfg.Master.NewsTimes", "Horas de noticias (NY)", "Separadas por ';' (HH:mm, hora de Nueva York). Por defecto: 08:30;10:00;14:00.", None, None, False),
    ("CooldownSeconds", "int", "_c.Cfg.Master.CooldownSeconds", "Tiempo entre señales del mismo lado (s)", "Recomendado: 60.", 0, 3600, False),
    ("BlockAgainstTrend", "bool", "_c.Cfg.Master.BlockAgainstTrend", "Sin GIRO contra tendencia sin barrido",
     "No da GIRO con el iceberg contra la tendencia si no hubo barrido o stops previos.", None, None, False),
])

INDICADORES = [
    # clase, indicador (eventos), con Master, esquina, desplazamiento Y, bloques
    ("StackedImbalancePro", "Imbalance", False, "TopRight", 150, [GENERAL, IMBALANCE]),
    ("IcebergTrackerPro", "Iceberg", False, "TopRight", 0, [GENERAL, ICEBERG, UMBRALES_NQ, UMBRALES_MNQ, UMBRALES_ES, UMBRALES_MES, UMBRALES_YM, UMBRALES_MYM, SESGO]),
    ("StopsTrackerPro", "Stops", False, "BottomRight", 0, [GENERAL, STOPS]),
    ("SweepsTrackerPro", "Sweeps", False, "BottomRight", 150, [GENERAL, BARRIDOS]),
    ("AbsorptionMaster", "Master", True, "TopLeft", 0,
     [GENERAL, PUNTUACION, SETUPS, GESTION, FILTROS, ICEBERG, UMBRALES_NQ, UMBRALES_MNQ, UMBRALES_ES, UMBRALES_MES, UMBRALES_YM,
      UMBRALES_MYM, SESGO, IMBALANCE, STOPS, BARRIDOS]),
]


def numero(v, decimal_):
    if decimal_:
        return f"{v}m"
    return str(int(v))


def propiedad(p, grupo, orden):
    nombre, tipo, destino, titulo, descripcion, minimo, maximo, recalcular = p
    desc = descripcion.replace("\\", "\\\\").replace('"', '\\"')
    tit = titulo.replace('"', '\\"')
    attrs = [f'\t[Display(Name = "{tit}", GroupName = "{grupo}", Order = {orden}, Description = "{desc}")]']
    cambio = f"Cambio({'true' if recalcular else 'false'});"
    if tipo == "int":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = f"public int {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {minimo}, {maximo}); {cambio} }} }}"
    elif tipo == "decimal":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = (f"public decimal {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {numero(minimo, True)}, "
                  f"{numero(maximo, True)}); {cambio} }} }}")
    elif tipo == "umbral":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = f"public int {nombre} {{ get => (int){destino}; set {{ {destino} = Math.Clamp(value, {minimo}, {maximo}); {cambio} }} }}"
    elif tipo == "umbralint":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = f"public int {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {minimo}, {maximo}); {cambio} }} }}"
    elif tipo == "bool":
        cuerpo = f"public bool {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    elif tipo == "string":
        cuerpo = f'public string {nombre} {{ get => {destino}; set {{ {destino} = value ?? ""; {cambio} }} }}'
    elif tipo == "sonido":
        attrs.append("\t[SoundComboBoxEditor]")
        cuerpo = f'public string {nombre} {{ get => {destino}; set {{ {destino} = string.IsNullOrWhiteSpace(value) ? "alert1" : value; {cambio} }} }}'
    elif tipo.startswith("enum:"):
        enum = tipo[5:]
        cuerpo = f"public {enum} {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    else:
        raise ValueError(tipo)
    return "\n".join(attrs) + "\n\t" + cuerpo + "\n"


CABECERA = """// <auto-generated>
// Generado por SuiteOrderFlowStacker/herramientas/generar_parametros.py: no editar a mano (cambiar el generador y volver
// a ejecutarlo). Parámetros de este indicador y las llamadas de ATAS, iguales en los cinco indicadores de la suite.
// </auto-generated>
"""


def generar(clase, indicador, master, esquina, offy, bloques):
    props = []
    for i, (titulo, params) in enumerate(bloques):
        grupo = f"{i}. {titulo}"
        props.append(f"\t#region {grupo}\n")
        for j, p in enumerate(params):
            props.append(propiedad(p, grupo, (j + 1) * 10))
        props.append("\t#endregion\n")
    cuerpo_props = "\n".join(props)

    codigo = f"""{CABECERA}#nullable enable
namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;

using OFT.Attributes.Editors;
using OFT.Rendering.Context;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

using Utils.Common.Logging;

public partial class {clase}
{{
	private readonly CarcasaSuite _c = new("{indicador}", {'true' if master else 'false'});

	public {clase}()
		: base(true)
	{{
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		Panel = IndicatorDataProvider.CandlesPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		_c.Alerta = (sonido, texto) => AddAlert(sonido, texto);
		_c.Redibujar = () => RedrawChart();
		_c.Error = (mensaje, error) =>
		{{
			// LogError de ATAS exige la excepción (no nula); sin excepción, un aviso.
			if (error is null)
				this.LogWarn(mensaje);
			else
				this.LogError(mensaje, error);
		}};
		_c.General.InfoPanelPosition = EsquinaPanel.{esquina};
		_c.General.InfoPanelOffsetY = {offy};
		_c.General.ShowInfoPanel = {"true" if master else "false"};

		// v4: solo el Absorption Master avisa por defecto; los trackers no (sus alertas tapaban la vista).
		_c.General.EnableAlerts = {"true" if master else "false"};
	}}

	/// <summary>Cambio de un parámetro: recalcula las velas solo si hace falta (los eventos en vivo se conservan).</summary>
	private void Cambio(bool recalcular)
	{{
		if (recalcular)
			RecalculateValues();
		else
			_c.Aplicar();
	}}

{cuerpo_props}
	#region ATAS

	/// <summary>Suscripción al MBO (nivel 3). Si el feed no lo ofrece, no llegan mensajes y se usa el modo inferido.</summary>
	protected override async void OnInitialize()
	{{
		try
		{{
			if (_c.Cfg.Iceberg.ModoMbo != ModoMbo.SoloInferido)
				await SubscribeMarketByOrderData();
		}}
		catch (Exception ex)
		{{
			this.LogError("{clase}: sin suscripción MBO (se usa el modo inferido)", ex);
		}}
	}}

	protected override void OnCalculate(int bar, decimal value)
	{{
		try
		{{
			_c.Calcular(bar, CurrentBar, Vela, i => GetCandle(i).Time, InstrumentInfo?.Instrument, InstrumentInfo?.TickSize ?? 0);
		}}
		catch (Exception ex)
		{{
			this.LogError("{clase}: error de cálculo", ex);
		}}
	}}

	/// <summary>Vela de ATAS a VelaFlujo (volumen por precio con GetAllPriceLevels; sin niveles, el núcleo lo reparte).</summary>
	private VelaFlujo Vela(int bar)
	{{
		var c = GetCandle(bar);
		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		var v = new VelaFlujo {{ InicioUtc = c.Time, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume, Delta = c.Delta }};

		if (c.GetAllPriceLevels() is {{ }} niveles)
		{{
			foreach (var n in niveles)
			{{
				var t = PrecioMath.Tick(n.Price, tick);
				var previo = v.Niveles.TryGetValue(t, out var p) ? p : (0m, 0m);
				v.Niveles[t] = (previo.Item1 + n.Bid, previo.Item2 + n.Ask);
			}}
		}}

		return v;
	}}

	/// <summary>Trade en vivo (Direction y los identificadores de orden; estos solo llegan con MBO).</summary>
	protected override void OnNewTrade(MarketDataArg trade)
	{{
		try
		{{
			_c.Trade(trade.Price, trade.Volume, trade.Time, trade.Direction.ToString(), trade.AggressorExchangeOrderId, trade.ExchangeOrderId);
		}}
		catch (Exception ex)
		{{
			this.LogError("{clase}: error en OnNewTrade", ex);
		}}
	}}

	/// <summary>DOM (nivel 2): volumen visible por precio. DataType es el MarketDataType de ATAS.Indicators (Ask / Bid).</summary>
	protected override void MarketDepthChanged(MarketDataArg depth)
	{{
		try
		{{
			_c.Dom(depth.Price, depth.Volume, depth.DataType.ToString());
		}}
		catch (Exception ex)
		{{
			this.LogError("{clase}: error en MarketDepthChanged", ex);
		}}
	}}

	protected override void OnDispose() => _c.Dispose();

	/// <summary>Geometría del gráfico para dibujar (coordenadas por barra y precio, área y velas visibles).</summary>
	private Lienzo Lienzo() => new()
	{{
		X = bar => ChartInfo!.GetXByBar(bar),
		Y = precio => ChartInfo!.GetYByPrice(precio, false),
		Area = ChartInfo!.PriceChartContainer.Region,
		AnchoVela = Math.Max(1, (int)ChartInfo.PriceChartContainer.BarsWidth),
		Primera = FirstVisibleBarNumber,
		Ultima = LastVisibleBarNumber,
		UltimaBarra = CurrentBar - 1,
		Tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m,
		Modo = _c.General.DisplayMode
	}};

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{{
		try
		{{
			if (ChartInfo is null || InstrumentInfo is null)
				return;

			Dibujar(context);
		}}
		catch (Exception ex)
		{{
			this.LogError("{clase}: error de dibujo", ex);
		}}
	}}

	#endregion
}}
"""

    mbo = f"""{CABECERA}#nullable enable
namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using Utils.Common.Logging;

// Archivo aparte a propósito: es el único que importa ATAS.DataFeedsCore (MarketByOrder). TradeDirection y
// MarketDataType existen en los dos espacios de nombres; aquí no se nombran (se comparan con ToString).
public partial class {clase}
{{
	/// <summary>MBO (nivel 3): Type New/Change/Delete/Snapshot, Side Bid/Ask, ExchangeOrderId y Priority.</summary>
	protected override void OnMarketByOrdersChanged(IEnumerable<MarketByOrder> orders)
	{{
		try
		{{
			foreach (var o in orders)
				_c.Mbo(o.Type.ToString(), o.Side.ToString(), o.Price, o.Volume, Convert.ToInt64(o.ExchangeOrderId), Convert.ToInt64(o.Priority));
		}}
		catch (Exception ex)
		{{
			this.LogError("{clase}: error en OnMarketByOrdersChanged", ex);
		}}
	}}
}}
"""
    for nombre, texto in ((f"{clase}.g.cs", codigo), (f"{clase}.Mbo.g.cs", mbo)):
        with open(os.path.join(SALIDA, nombre), "w", encoding="utf-8", newline="\n") as f:
            f.write(texto)


# v4: el Indicador Global Session Sweep (clase SessionSweepsPro) corre los cuatro trackers por dentro (AnfitrionTrackers)
# y expone sus ajustes con el prefijo "Trk" en los grupos 40+ (sin panel propio ni alertas: no se incluyen).
GLOBAL_GENERAL = ("General", [p for p in GENERAL[1] if p[0] in (
    "DisplayMode", "FontSize", "EventLifetimeMin", "InstrumentProfile", "EnableRecording", "RealTimeOnly", "LoadRecordedEvents", "RecordedDaysToLoad")])
GLOBAL_BLOQUES = [GLOBAL_GENERAL, IMBALANCE, ICEBERG, UMBRALES_NQ, UMBRALES_MNQ, UMBRALES_ES, UMBRALES_MES, UMBRALES_YM, UMBRALES_MYM, SESGO, STOPS, BARRIDOS]
SALIDA_GLOBAL = os.path.join(RAIZ, "SessionSweeps", "Atas", "Generado")


def generar_global():
    props = []
    for i, (titulo, params) in enumerate(GLOBAL_BLOQUES):
        grupo = f"{40 + i}. Trackers · {titulo}"
        props.append(f"\t#region {grupo}\n")
        for j, p in enumerate(params):
            nombre, tipo, destino, tit, desc, mn, mx, recalc = p
            q = ("Trk" + nombre, tipo, destino.replace("_c.", "_trk.C."), tit, desc, mn, mx, recalc)
            props.append(propiedad(q, grupo, (j + 1) * 10).replace("Cambio(", "CambioTrackers("))
        props.append("\t#endregion\n")
    codigo = f"""{CABECERA}#nullable enable
namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes.Editors;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

/// <summary>Ajustes de los trackers que corren dentro del Indicador Global Session Sweep (v4).</summary>
public partial class SessionSweepsPro
{{
{chr(10).join(props)}}}
"""
    os.makedirs(SALIDA_GLOBAL, exist_ok=True)
    with open(os.path.join(SALIDA_GLOBAL, "SessionSweepsPro.Trackers.g.cs"), "w", encoding="utf-8", newline="\n") as f:
        f.write(codigo)


def main():
    os.makedirs(SALIDA, exist_ok=True)
    for ind in INDICADORES:
        generar(*ind)
        print("generado:", ind[0])
    generar_global()
    print("generado: SessionSweepsPro.Trackers (Indicador Global)")


if __name__ == "__main__":
    main()
