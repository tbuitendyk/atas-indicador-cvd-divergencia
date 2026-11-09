#!/usr/bin/env python3
"""Genera SessionSweeps/Atas/Generado/SessionSweepsPro.V2.g.cs: los parámetros de Sessions Sweeps Pro.

Sección 15 del documento maestro (ParametrosV2 en SessionSweeps/Nucleo) más el panel y el dibujo del anexo. Cada
parámetro se define UNA vez aquí; PARAMETROS.md se genera de este mismo archivo (pruebas, ACTUALIZAR_DOCS=1).
Uso (desde la raíz del repositorio):  python3 SuiteOrderFlowStacker/herramientas/generar_parametros_v2.py
"""
import os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SALIDA = os.path.join(RAIZ, "SessionSweeps", "Atas", "Generado", "SessionSweepsPro.V2.g.cs")
DOC = os.path.join(RAIZ, "SessionSweeps", "PARAMETROS.md")

P = "_cfg.V2."

# (propiedad, tipo, destino, nombre en español, descripción, mínimo, máximo, recalcular)
# tipo: int | decimal | bool | string | hora | enum:<Tipo>
GRUPOS = [
    ("16. Barras de volumen y horario", [
        ("VolumePerBar", "int", P + "VolumePerBar", "Contratos por barra (NQ)",
         "Cada barra de volumen cierra al llegar a estos contratos. NQ: 500 (calibrar con el CSV).", 10, 100000, True),
        ("VolumePerBarMnq", "int", P + "VolumePerBarMnq", "Contratos por barra (MNQ)",
         "El MNQ negocia más contratos: 1000 como punto de partida.", 10, 100000, True),
        ("MedianLookbackBars", "int", P + "MedianLookbackBars", "Barras para las medianas",
         "Medianas de duración, rango y volumen (solo barras de RTH). Recomendado: 30.", 5, 500, True),
        ("PreT0DeltaSec", "int", P + "PreT0DeltaSec", "Delta previo a T0 (s)",
         "Segundos antes de la ruptura para el delta previo. Recomendado: 60.", 5, 600, True),
        ("PanelRefreshMs", "int", P + "PanelRefreshMs", "Refresco del panel (ms)",
         "Cada cuánto se redibuja el panel flotante. Recomendado: 250.", 100, 5000, False),
        ("KillzoneStartNY", "hora", P + "KillzoneStartNY", "Killzone: inicio (NY)",
         "Inicio de la ventana de mayor peso (hora de Nueva York). 09:30 = 07:30 CDMX en verano, 08:30 en invierno.", None, None, True),
        ("KillzoneEndNY", "hora", P + "KillzoneEndNY", "Killzone: fin (NY)",
         "Fin de la killzone (hora de Nueva York).", None, None, True),
        ("NewsBlockMin", "int", P + "NewsBlockMin", "Bloqueo por noticia (min)",
         "Minutos antes y después de cada hora de noticia en los que no se alerta ni se entra (NOTICIA).", 0, 60, True),
        ("NewsTimes", "string", P + "NewsTimes", "Horas de noticias (NY)",
         "Separadas por ';' en hora de Nueva York, por ejemplo 08:30;10:00;14:00.", None, None, True),
    ]),
    ("17. Fuentes de la suite", [
        ("DedupTicks", "int", P + "DedupTicks", "Duplicados: distancia (ticks)",
         "Dos barridos del mismo nivel a esta distancia o menos son el mismo evento.", 0, 20, True),
        ("DedupSec", "int", P + "DedupSec", "Duplicados: tiempo (s)", "Y dentro de estos segundos.", 0, 60, True),
        ("SuitePreSec", "int", P + "SuitePreSec", "Eventos previos a T0 (s)",
         "Los eventos de la suite hasta estos segundos antes de la ruptura también cuentan.", 0, 300, True),
        ("SuitePriceMarginTicks", "int", P + "SuitePriceMarginTicks", "Margen de precio (ticks)",
         "Distancia máxima entre un evento de la suite y el barrido para asociarlos.", 0, 40, True),
        ("IcebergEnabled", "bool", P + "Iceberg.Enabled", "Iceberg: activado", "Usar los eventos del Iceberg Tracker Pro.", None, None, True),
        ("IcebergWeight", "decimal", P + "Iceberg.Weight", "Iceberg: peso", "0 a 2 sobre los puntos que aporta. 1 = tal cual.", 0, 2, True),
        ("StopsEnabled", "bool", P + "Stops.Enabled", "Stops: activado", "Usar los eventos del Stops Tracker Pro.", None, None, True),
        ("StopsWeight", "decimal", P + "Stops.Weight", "Stops: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("SweepsEnabled", "bool", P + "Sweeps.Enabled", "Sweeps: activado", "Usar los eventos del Sweeps Tracker Pro.", None, None, True),
        ("SweepsWeight", "decimal", P + "Sweeps.Weight", "Sweeps: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("StackedEnabled", "bool", P + "Stacked.Enabled", "Stacked: activado", "Usar las zonas del Stacked Imbalance Pro.", None, None, True),
        ("StackedWeight", "decimal", P + "Stacked.Weight", "Stacked: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("ImbalanceEnabled", "bool", P + "Imbalance.Enabled", "Imbalance: activado", "Usar los imbalances diagonales de la suite.", None, None, True),
        ("ImbalanceWeight", "decimal", P + "Imbalance.Weight", "Imbalance: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("AbsorptionEnabled", "bool", P + "Absorption.Enabled", "Absorción: activado", "Usar las señales del Absorption Master.", None, None, True),
        ("AbsorptionWeight", "decimal", P + "Absorption.Weight", "Absorción: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
    ]),
    ("18. Niveles", [
        ("EqualLevelTolTicks", "int", P + "EqualLevelTolTicks", "Iguales: tolerancia (ticks)",
         "Máximos o mínimos a esta distancia o menos cuentan como iguales.", 0, 20, True),
        ("EqualLevelMinTouches", "int", P + "EqualLevelMinTouches", "Iguales: toques mínimos", "Toques para un nivel igual.", 2, 10, True),
        ("LevelMinAgeMin", "int", P + "LevelMinAgeMin", "Edad mínima del nivel (min)",
         "Un nivel más reciente puntúa menos en el bloque A.", 0, 1440, True),
    ]),
    ("19. Stops", [
        ("MinPenetrationTicks", "int", P + "MinPenetrationTicks", "Penetración mínima (ticks)", "Ticks más allá del nivel para que cuente como barrido.", 1, 40, True),
        ("MaxSweepTicks", "int", P + "MaxSweepTicks", "Barrido máximo (ticks)", "Más allá de esto deja de ser un barrido y se vigila la ruptura.", 4, 200, True),
        ("BurstSpeedMult", "decimal", P + "BurstSpeedMult", "Ráfaga: × velocidad mediana", "Velocidad de 5 s respecto a la mediana para una ráfaga de stops.", 1, 10, True),
        ("FastBarDurationMult", "decimal", P + "FastBarDurationMult", "Barra rápida: × duración mediana", "Barra de volumen que dura menos de esto = rápida.", 0.1, 1, True),
        ("AggressorShareMin", "decimal", P + "AggressorShareMin", "Agresor mínimo (fracción)", "Parte del volumen más allá del nivel del lado agresor (0,65 = 65 %).", 0.5, 1, True),
        ("StopsEventWindowSec", "int", P + "StopsEventWindowSec", "Ventana de eventos de stops (s)", "Alrededor de T0.", 1, 300, True),
        ("VolBeyondRelMult", "decimal", P + "VolBeyondRelMult", "Volumen más allá: × medio de barridos", "Respecto a los últimos barridos.", 0.5, 10, True),
        ("SweepHistoryCount", "int", P + "SweepHistoryCount", "Barridos para la media", "Barridos anteriores para el volumen relativo.", 3, 200, True),
        ("MinVolBeyondFloor", "int", P + "MinVolBeyondFloor", "Volumen más allá: piso (contratos)", "Mínimo absoluto.", 0, 100000, True),
    ]),
    ("20. Absorción", [
        ("DivergenceDeltaPctMin", "decimal", P + "DivergenceDeltaPctMin", "Divergencia de delta mínima", "Delta % de la barra contra el movimiento (0,15 = 15 %).", 0, 1, True),
        ("IcebergMaxDistTicks", "int", P + "IcebergMaxDistTicks", "Iceberg: distancia máxima (ticks)", "Del extremo del barrido.", 0, 20, True),
        ("F2MinBars", "int", P + "F2MinBars", "F2: barras mínimas", "Compresión tras el extremo.", 1, 20, True),
        ("F2MaxBars", "int", P + "F2MaxBars", "F2: barras máximas", "", 1, 30, True),
        ("F2MaxBarsRetrace", "int", P + "F2MaxBarsRetrace", "F2: barras máximas en retroceso", "", 1, 40, True),
        ("CompressionRangeMult", "decimal", P + "CompressionRangeMult", "Compresión: × rango mediano", "", 0.1, 2, True),
        ("CompressionMaxTicks", "int", P + "CompressionMaxTicks", "Compresión: máximo (ticks)", "", 1, 60, True),
        ("ValueAreaPct", "decimal", P + "ValueAreaPct", "Área de valor (fracción)", "0,70 = 70 % del volumen.", 0.5, 0.95, True),
        ("FinishedAuctionMaxContracts", "int", P + "FinishedAuctionMaxContracts", "Subasta terminada: contratos máximos", "En el extremo.", 0, 50, True),
        ("StackedMinLevels", "int", P + "StackedMinLevels", "Stacked: niveles mínimos", "Contiguos.", 2, 10, True),
        ("ImbalanceRatio", "decimal", P + "ImbalanceRatio", "Imbalance: ratio", "Diagonal (3 = 300 %).", 1.5, 10, True),
        ("ImbalanceMinVolume", "int", P + "ImbalanceMinVolume", "Imbalance: volumen mínimo", "Del lado dominante.", 1, 1000, True),
    ]),
    ("21. Recuperación", [
        ("ReclaimBufferTicks", "int", P + "ReclaimBufferTicks", "Recuperación: margen (ticks)", "Cierre de vuelta al otro lado del nivel.", 0, 20, True),
        ("ReclaimMaxBars", "int", P + "ReclaimMaxBars", "Recuperación: barras máximas", "", 1, 30, True),
        ("CompositeMaxBars", "int", P + "CompositeMaxBars", "Vela compuesta: barras máximas", "", 2, 30, True),
        ("ZoneMaxBars", "int", P + "ZoneMaxBars", "Zona: barras máximas", "", 2, 60, True),
        ("F3RangeMult", "decimal", P + "F3RangeMult", "F3: × rango mediano", "Barra de desplazamiento.", 0.5, 10, True),
        ("F3DeltaPctMin", "decimal", P + "F3DeltaPctMin", "F3: delta % mínimo", "", 0, 1, True),
        ("ContDeltaPctMin", "decimal", P + "ContDeltaPctMin", "Continuación: delta % mínimo", "", 0, 1, True),
        ("DeltaFlipMin", "decimal", P + "DeltaFlipMin", "Giro de delta mínimo", "", 0, 1, True),
        ("RecoveryTicks", "int", P + "RecoveryTicks", "Recuperación 1m/5m (ticks)", "Recuperación que valida el barrido en el panel (anexo: 16).", 1, 200, True),
        ("StopBufferTicks", "int", P + "StopBufferTicks", "Margen del stop (ticks)", "Más allá de la zona, estructura o POC; también para FALLIDO.", 0, 40, True),
    ]),
    ("22. Estados y alertas", [
        ("ScoreRealMin", "int", P + "ScoreRealMin", "Puntos BARRIDO REAL", "Mínimo para BARRIDO DE LIQUIDEZ REAL (con toma de stops, absorción y recuperación). Puntuación, no probabilidad.", 0, 100, True),
        ("ScorePremiumMin", "int", P + "ScorePremiumMin", "Puntos BARRIDO REAL PREMIUM", "Además de todas las puertas premium.", 0, 100, True),
        ("PremiumMinLevelScore", "int", P + "PremiumMinLevelScore", "PREMIUM: bloque A mínimo", "", 0, 20, True),
        ("PremiumMaxReclaimBars", "int", P + "PremiumMaxReclaimBars", "PREMIUM: recuperación máxima (barras)", "", 1, 20, True),
        ("PremiumUpgradeMaxBars", "int", P + "PremiumUpgradeMaxBars", "UPGRADE PREMIUM: barras máximas", "", 1, 60, True),
        ("RuptureMinConditions", "int", P + "RuptureMinConditions", "RUPTURA: condiciones mínimas", "", 1, 6, True),
        ("AcceptanceCloses", "int", P + "AcceptanceCloses", "Aceptación: cierres", "Cierres más allá del nivel.", 1, 10, True),
        ("RuptureMaxAbsorptionScore", "int", P + "RuptureMaxAbsorptionScore", "RUPTURA: absorción máxima", "Bloque C máximo para una ruptura.", 0, 25, True),
        ("FailedBreakTicks", "int", P + "FailedBreakTicks", "RUPTURA FALLIDA: ticks", "", 1, 40, True),
        ("FailedBreakBars", "int", P + "FailedBreakBars", "RUPTURA FALLIDA: barras", "", 1, 30, True),
        ("MaxEvaluationBars", "int", P + "MaxEvaluationBars", "Evaluación: barras máximas", "", 3, 100, True),
        ("MaxEvaluationSec", "int", P + "MaxEvaluationSec", "Evaluación: segundos máximos", "", 30, 3600, True),
        ("AlertMinState", "enum:EstadoMinimoAlerta", P + "AlertMinState", "Estado mínimo para alertar",
         "REAL: suenan REAL y PREMIUM; PREMIUM: solo PREMIUM (el resto se ve en el panel).", None, None, False),
    ]),
    ("23. FVG / LVN", [
        ("FvgMinTicks", "int", P + "FvgMinTicks", "FVG: tamaño mínimo (ticks)", "", 1, 100, True),
        ("FvgMaxActive", "int", P + "FvgMaxActive", "FVG: máximo activos", "Por marco y lado.", 1, 100, True),
        ("Fvg1m", "bool", P + "Fvg1m", "FVG de 1m", "", None, None, True),
        ("Fvg5m", "bool", P + "Fvg5m", "FVG de 5m", "", None, None, True),
        ("BfvgWindowMin", "int", P + "BfvgWindowMin", "BFVG: ventana (min)", "Tras el extremo del barrido.", 1, 240, True),
        ("ConfluenceBonus", "int", P + "ConfluenceBonus", "Bonificación FVG + LVN", "", 0, 30, True),
        ("TargetMinDistTicks", "int", P + "TargetMinDistTicks", "Objetivo LVN: distancia mínima (ticks)", "", 0, 200, True),
    ]),
    ("24. Modelos", [
        ("EnableModelA", "bool", P + "EnableModelA", "Modelo A: barrido de liquidez", "Prioridad A: barrido real (o premium) y recuperación; incluye los BFVG.", None, None, True),
        ("EnableModelB", "bool", P + "EnableModelB", "Modelo B: retroceso a LVN / EDGE / FVG", "Prioridad B (la de mayor interés): retroceso a una zona con gatillo confirmado. Incluye B - RETROCESO TRAS ROMPIMIENTO.", None, None, True),
        ("ModelBEnableLvn", "bool", P + "ModelBEnableLvn", "Modelo B: LVN del Thermal", "", None, None, True),
        ("ModelBEnableEdge", "bool", P + "ModelBEnableEdge", "Modelo B: EDGE del Thermal", "", None, None, True),
        ("ModelBEnableFvg", "bool", P + "ModelBEnableFvg", "Modelo B: FVG", "También FVG+LVN y FVG+EDGE.", None, None, True),
        ("ModelBEnableEma", "bool", P + "ModelBEnableEma", "Modelo B: EMA 15", "", None, None, True),
        ("ModelBEnableNivel", "bool", P + "ModelBEnableNivel", "Modelo B: nivel roto", "", None, None, True),
        ("MaxAttemptsPerZone", "int", P + "MaxAttemptsPerZone", "Intentos máximos por zona", "Entradas en la misma zona y dirección.", 1, 20, True),
        ("TpRMultiple", "decimal", P + "TpRMultiple", "TP (R múltiplos)", "Objetivo a este múltiplo del riesgo (2R).", 0.5, 20, True),
        ("TargetMode", "enum:ModoObjetivo", P + "TargetMode", "Objetivo", "R múltiplos, o el próximo LVN / EDGE (si no hay, R).", None, None, True),
        ("ExitMode", "enum:ModoSalida", P + "ExitMode", "Salida", "TP o cierre de 1 minuto al otro lado de la EMA 15, lo primero (por defecto). El SL nunca se amplía.", None, None, True),
        ("Mod3InvalidatesAllMod1Mod2", "bool", P + "Mod3InvalidatesAllMod1Mod2", "Iceberg roto anula todos los A y B",
         "Desactivado: solo los contrarios a la dirección del rompimiento (contexto).", None, None, True),
        ("Mod4AllowInternal", "bool", P + "Mod4AllowInternal", "B tras rompimiento en liquidez interna", "Por defecto solo externa.", None, None, True),
        ("Mod4MaxWaitMin", "int", P + "Mod4MaxWaitMin", "B tras rompimiento: espera máxima (min)", "", 1, 480, True),
        ("Mod4MaxRetracePct", "decimal", P + "Mod4MaxRetracePct", "B tras rompimiento: retroceso máximo", "Fracción de la pierna (0,618).", 0.1, 1, True),
        ("Mod4MaxAttempts", "int", P + "Mod4MaxAttempts", "B tras rompimiento: intentos máximos", "Entradas por ruptura.", 1, 10, True),
        ("MinRR", "decimal", P + "MinRR", "R:R mínimo", "", 0.5, 10, True),
        ("MaxStopTicks", "int", P + "MaxStopTicks", "Stop máximo (ticks)", "", 4, 400, True),
        ("SuperBigTradeMinContracts", "int", P + "SuperBigTradeMinContracts", "Super big trade (contratos)",
         "El documento no fija el valor: calibrar con el CSV.", 10, 100000, True),
        ("StopMode", "enum:ModoStop", P + "StopMode", "Modo del stop", "", None, None, True),
    ]),
    ("25. Riesgo", [
        ("RiskMode", "enum:ModoRiesgo", P + "RiskMode", "Modo de riesgo", "", None, None, False),
        ("RiskAmountUSD", "decimal", P + "RiskAmountUSD", "Riesgo por operación (USD)", "", 1, 1000000, False),
        ("RiskPct", "decimal", P + "RiskPct", "Riesgo por operación (%)", "", 0.01, 10, False),
        ("AccountSize", "decimal", P + "AccountSize", "Tamaño de la cuenta (USD)", "Si no se lee de ATAS.", 100, 100000000, False),
        ("ReadAtasAccount", "bool", P + "ReadAtasAccount", "Leer el saldo de ATAS", "Del portafolio seleccionado en ATAS.", None, None, False),
        ("MaxOpenRiskPct", "decimal", P + "MaxOpenRiskPct", "Riesgo abierto máximo (%)", "", 0.1, 20, True),
        ("MicroImpulseEmaLen", "int", P + "MicroImpulseEmaLen", "EMA de salida (Ema15Length)", "EMA de 1 minuto para la salida y el contexto (15).", 2, 200, True),
    ]),
    ("26. Gatillo manual", [
        ("ManualZoneTicks", "int", P + "ManualZoneTicks", "Zona manual (ticks)", "Margen de la caja dibujada.", 0, 40, False),
        ("ManualLookbackBars", "int", P + "ManualLookbackBars", "Barras de contexto", "", 1, 100, False),
        ("MaxManualBoxes", "int", P + "MaxManualBoxes", "Cajas manuales máximas", "", 1, 50, False),
    ]),
    ("27. CVD", [
        ("CvdMacroPivotBars", "int", P + "CvdMacroPivotBars", "Divergencia macro: velas por lado",
         "Pivote de máximo a máximo o de mínimo a mínimo. Más = menos sensible. NY NQ: 10.", 2, 100, False),
        ("CvdMicroPivotBars", "int", P + "CvdMicroPivotBars", "Divergencia micro: velas por lado",
         "Dentro de una macro, para afinar el punto de giro. NY NQ: 3.", 1, 30, False),
        ("CvdShowMicro", "bool", P + "CvdShowMicro", "Mostrar micro-divergencias", "", None, None, False),
        ("CvdMaxLookbackBars", "int", P + "CvdMaxLookbackBars", "Velas que se revisan", "", 20, 5000, False),
        ("CvdReset", "enum:ReinicioCvd", P + "CvdReset", "Reinicio del CVD", "Día de futuros (18:00 NY) o apertura RTH (09:30 NY).", None, None, True),
        ("CvdMatrixMacroStrength", "int", P + "CvdMatrixMacroStrength", "CVD Matrix: fuerza macro (velas)", "Filtro 1: swing amplio.", 2, 100, True),
        ("CvdMatrixMicroStrength", "int", P + "CvdMatrixMicroStrength", "CVD Matrix: fuerza micro (velas)", "Filtro 2: swing corto.", 1, 30, True),
        ("CvdMatrixMacroValidity", "int", P + "CvdMatrixMacroValidity", "CVD Matrix: vigencia macro (velas)", "", 1, 1000, True),
        ("CvdMatrixMicroValidity", "int", P + "CvdMatrixMicroValidity", "CVD Matrix: vigencia micro (velas)", "", 1, 200, True),
        ("CvdMatrixMinWickPct", "decimal", P + "CvdMatrixMinWickPct", "CVD Matrix: mecha mínima (% del rango)", "Filtro 3: absorción en la vela del swing.", 0, 100, True),
        ("CvdMatrixMinWickVolumePct", "decimal", P + "CvdMatrixMinWickVolumePct", "CVD Matrix: volumen en la mecha (%)", "Filtro 3.", 0, 100, True),
    ]),
    ("28. Panel y dibujo", [
        ("MostrarPanel", "bool", "_v.MostrarPanel", "Panel flotante",
         "Ventana movible a cualquier lugar o monitor (Resumen / Detalle). Guarda su posición.", None, None, False),
        ("ShowInChartPanel", "bool", "_v.ShowInChartPanel", "Panel dentro del gráfico",
         "El mismo contenido dibujado sobre el gráfico (si no se quiere la ventana).", None, None, False),
        ("InChartPanelX", "int", "_v.InChartPanelX", "Panel en el gráfico: X", "", 0, 5000, False),
        ("InChartPanelY", "int", "_v.InChartPanelY", "Panel en el gráfico: Y", "", 0, 5000, False),
        ("ShowSweepBoxes", "bool", "_v.ShowSweepBoxes", "Cajas de barrido (v2)", "Coloreadas por estado (TRAMPA, PREMIUM, RUPTURA, FALLIDO…).", None, None, False),
        ("ShowModelBoxes", "bool", "_v.ShowModelBoxes", "Zonas de los modelos", "MOD1-MOD4 y BFVG con entrada, stop y objetivos.", None, None, False),
        ("ShowFvgV2", "bool", "_v.ShowFvgV2", "FVG / BFVG (v2)", "", None, None, False),
        ("ShowInternalLiquidity", "bool", "_v.ShowInternalLiquidity", "Liquidez interna", "Niveles de sesión e iguales.", None, None, False),
        ("ShowExternalLiquidity", "bool", "_v.ShowExternalLiquidity", "Liquidez externa", "PDH/PDL y semana anterior.", None, None, False),
        ("ShowLvnLines", "bool", "_v.ShowLvnLines", "LVN del Thermal", "Las zonas que llegan del Thermal (activas y mitigadas).", None, None, False),
        ("ShowManualTriggerBoxes", "bool", "_v.ShowManualTriggerBoxes", "Cajas del gatillo manual",
         "Las dibuja el Thermal que las pidió; actívelo solo si el Thermal está en otro gráfico.", None, None, False),
        ("ShowTrackerDiagnostics", "bool", "_v.ShowTrackerDiagnostics", "Diagnóstico de trackers",
         "Marca hueca fucsia (SSP ICE, STOP, SWEEP, STK, IMB) en cada evento que Sessions Sweeps recibe del bus, para compararlo con el dibujo original del tracker.", None, None, False),
    ]),
]


# --- Perfiles por instrumento (2.4): estos umbrales NO son generales; cada familia tiene los suyos -----------------------
POR_INSTRUMENTO = {"VolumePerBar", "VolumePerBarMnq", "MinVolBeyondFloor", "SuperBigTradeMinContracts", "ImbalanceMinVolume",
                   "ImbalanceRatio", "StackedMinLevels", "AggressorShareMin", "BurstSpeedMult", "F3DeltaPctMin", "ContDeltaPctMin",
                   "DivergenceDeltaPctMin", "FinishedAuctionMaxContracts", "StopBufferTicks", "MaxStopTicks", "MinPenetrationTicks",
                   "MaxSweepTicks", "RecoveryTicks"}
GRUPOS = [(g, [x for x in ps if x[0] not in POR_INSTRUMENTO]) for g, ps in GRUPOS]

RAICES = ["NQ", "ES", "YM", "MNQ", "MES", "MYM"]

# (campo, tipo, nombre, descripción, mínimo, máximo)
CAMPOS = [
    ("VolumePerBarMode", "enum:ModoVolumenBarra", "Contratos por barra: modo",
     "AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo.", None, None),
    ("VolumePerBar", "int", "Contratos por barra (manual / provisional)", "En modo AUTO solo se usa si no hay datos de RTH.", 10, 100000),
    ("VolumePerBarMin", "int", "AUTO: mínimo de contratos por barra", "Límite de seguridad del valor AUTO.", 10, 100000),
    ("VolumePerBarMax", "int", "AUTO: máximo de contratos por barra", "Límite de seguridad del valor AUTO.", 10, 100000),
    ("CalibrationLookbackSessions", "int", "Calibración: sesiones a usar", "Últimas sesiones con RTH del historial cargado.", 1, 250),
    ("MinimumCalibrationSessions", "int", "Calibración: sesiones mínimas", "Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL).", 1, 250),
    ("TimeOfDayBucketMinutes", "int", "Calibración: franja horaria (min)", "Medianas por franja para el volumen relativo.", 5, 120),
    ("MinVolBeyondFloor", "int", "Volumen más allá: piso (contratos)", "Mínimo absoluto de contratos más allá del nivel.", 0, 100000),
    ("SuperBigTradeMinContracts", "int", "Super big trade (contratos)", "Orden agresiva grande para el contexto de iceberg roto.", 1, 100000),
    ("ImbalanceMinVolume", "int", "Imbalance: volumen mínimo (contratos)", "Del lado dominante.", 1, 100000),
    ("FinishedAuctionMaxContracts", "int", "Subasta terminada: contratos máximos", "En el extremo.", 0, 1000),
    ("ImbalanceRatio", "decimal", "Imbalance: ratio", "Diagonal (3 = 300 %).", 1.5, 10),
    ("StackedMinLevels", "int", "Stacked: niveles mínimos", "Contiguos.", 2, 10),
    ("AggressorShareMin", "decimal", "Agresor mínimo (fracción)", "Parte del volumen más allá del lado agresor.", 0.5, 1),
    ("BurstSpeedMult", "decimal", "Ráfaga: × velocidad mediana", "Velocidad de 5 s respecto a la mediana de este instrumento.", 1, 10),
    ("F3DeltaPctMin", "decimal", "F3: delta % mínimo", "Delta / volumen de la barra de desplazamiento.", 0, 1),
    ("ContDeltaPctMin", "decimal", "Continuación: delta % mínimo", "", 0, 1),
    ("DivergenceDeltaPctMin", "decimal", "Divergencia de delta mínima", "Delta % contra el movimiento.", 0, 1),
    ("MinPenetrationTicks", "int", "Penetración mínima (ticks)", "Ticks de ESTE instrumento más allá del nivel.", 1, 200),
    ("MaxSweepTicks", "int", "Barrido máximo (ticks)", "Más allá: se vigila la ruptura.", 2, 1000),
    ("RecoveryTicks", "int", "Recuperación 1m/5m (ticks)", "Recuperación que valida el barrido.", 1, 1000),
    ("StopBufferTicks", "int", "Margen del stop (ticks)", "Más allá de la zona, estructura o extremo.", 0, 200),
    ("MaxStopTicks", "int", "Stop máximo (ticks)", "Con más: NO EJECUTABLE.", 2, 2000),
    ("CvdMatrixDeltaFlip", "int", "CVD Matrix: delta del giro (contratos)", "Filtro 4: delta de la vela de giro tras el swing.", 1, 100000),
]

# Valores por defecto: espejo de PerfilInstrumento.PorDefecto (una prueba los compara con el código).
BASE = {"VolumePerBarMode": "ModoVolumenBarra.AutoPorInstrumento", "VolumePerBar": "500", "VolumePerBarMin": "50", "VolumePerBarMax": "5000",
        "CalibrationLookbackSessions": "20", "MinimumCalibrationSessions": "20", "TimeOfDayBucketMinutes": "15", "MinVolBeyondFloor": "50",
        "SuperBigTradeMinContracts": "150", "ImbalanceMinVolume": "5", "FinishedAuctionMaxContracts": "2", "ImbalanceRatio": "3.0",
        "StackedMinLevels": "3", "AggressorShareMin": "0.65", "BurstSpeedMult": "2.5", "F3DeltaPctMin": "0.25", "ContDeltaPctMin": "0.30",
        "DivergenceDeltaPctMin": "0.15", "MinPenetrationTicks": "2", "MaxSweepTicks": "24", "RecoveryTicks": "16", "StopBufferTicks": "3",
        "MaxStopTicks": "40", "CvdMatrixDeltaFlip": "400"}
ES_ = {"VolumePerBar": "1500", "MinVolBeyondFloor": "150", "SuperBigTradeMinContracts": "400", "ImbalanceMinVolume": "15",
       "FinishedAuctionMaxContracts": "5", "MinPenetrationTicks": "1", "MaxSweepTicks": "8", "RecoveryTicks": "6", "StopBufferTicks": "2", "MaxStopTicks": "16", "CvdMatrixDeltaFlip": "1000"}
YM_ = {"VolumePerBar": "150", "MinVolBeyondFloor": "15", "SuperBigTradeMinContracts": "40", "ImbalanceMinVolume": "2",
       "FinishedAuctionMaxContracts": "1", "MinPenetrationTicks": "1", "MaxSweepTicks": "12", "RecoveryTicks": "8", "StopBufferTicks": "2", "MaxStopTicks": "25", "CvdMatrixDeltaFlip": "100"}
MNQ_ = {"VolumePerBar": "1000", "MinVolBeyondFloor": "100", "SuperBigTradeMinContracts": "300", "ImbalanceMinVolume": "10", "FinishedAuctionMaxContracts": "4", "CvdMatrixDeltaFlip": "800"}
DEFECTOS_PERFIL = {r: {**BASE, **extra} for r, extra in
                   [("NQ", {}), ("ES", ES_), ("YM", YM_), ("MNQ", MNQ_), ("MES", ES_), ("MYM", YM_)]}

GRUPOS.append(("37. Alertas y canales", [
    ("AlertRealAtas", "bool", P + "AlertRealAtas", "BARRIDO REAL: ATAS (sonido y ventana)", "", None, None, False),
    ("AlertRealEmail", "bool", P + "AlertRealEmail", "BARRIDO REAL: correo", "", None, None, False),
    ("AlertRealTelegram", "bool", P + "AlertRealTelegram", "BARRIDO REAL: Telegram", "", None, None, False),
    ("AlertPremiumAtas", "bool", P + "AlertPremiumAtas", "BARRIDO REAL PREMIUM: ATAS (sonido y ventana)", "", None, None, False),
    ("AlertPremiumEmail", "bool", P + "AlertPremiumEmail", "BARRIDO REAL PREMIUM: correo", "", None, None, False),
    ("AlertPremiumTelegram", "bool", P + "AlertPremiumTelegram", "BARRIDO REAL PREMIUM: Telegram", "", None, None, False),
    ("AlertRupturaAtas", "bool", P + "AlertRupturaAtas", "RUPTURA / ACEPTACIÓN: ATAS (sonido y ventana)", "", None, None, False),
    ("AlertRupturaEmail", "bool", P + "AlertRupturaEmail", "RUPTURA / ACEPTACIÓN: correo", "", None, None, False),
    ("AlertRupturaTelegram", "bool", P + "AlertRupturaTelegram", "RUPTURA / ACEPTACIÓN: Telegram", "", None, None, False),
    ("AlertEntradaAAtas", "bool", P + "AlertEntradaAAtas", "GATILLO VÁLIDO A: ATAS (sonido y ventana)", "", None, None, False),
    ("AlertEntradaAEmail", "bool", P + "AlertEntradaAEmail", "GATILLO VÁLIDO A: correo", "", None, None, False),
    ("AlertEntradaATelegram", "bool", P + "AlertEntradaATelegram", "GATILLO VÁLIDO A: Telegram", "", None, None, False),
    ("AlertEntradaBAtas", "bool", P + "AlertEntradaBAtas", "GATILLO VÁLIDO B: ATAS (sonido y ventana)", "", None, None, False),
    ("AlertEntradaBEmail", "bool", P + "AlertEntradaBEmail", "GATILLO VÁLIDO B: correo", "", None, None, False),
    ("AlertEntradaBTelegram", "bool", P + "AlertEntradaBTelegram", "GATILLO VÁLIDO B: Telegram", "", None, None, False),
]))

GRUPOS.append(("30. Instrumentos", [
    ("AliasSimbolos", "string", P + "AliasSimbolos", "Alias de símbolos",
     "Si el proveedor usa otro nombre: ENQ=NQ;EP=ES. Sin reconocer: INSTRUMENTO NO SOPORTADO (sin señales).", None, None, True),
    ("VolumePerBarFraction", "decimal", P + "VolumePerBarFraction", "AUTO: fracción del minuto típico",
     "Contratos por barra AUTO = mediana del volumen de 1 minuto en RTH × esta fracción (normalizada: igual para todos).", 0.05, 2, True),
]))

for i, r in enumerate(RAICES):
    nota = "" if r == "NQ" else " Valor inicial PROVISIONAL: calibrar."
    GRUPOS.append((f"{31 + i}. Perfil {r}", [
        (f"{r}_{campo}", tipo, f"_cfg.Perfiles.{r}.{campo}", f"{r}: {nombre}", (desc + nota).strip(), mn, mx, True)
        for campo, tipo, nombre, desc, mn, mx in CAMPOS]))


def numero(v, decimal_):
    if decimal_:
        return f"{v}m"
    return str(v)


def propiedad(p, grupo, orden):
    nombre, tipo, destino, titulo, descripcion, minimo, maximo, recalcular = p
    desc = descripcion.replace("\\", "\\\\").replace('"', '\\"')
    tit = titulo.replace('"', '\\"')
    attrs = [f'\t[Display(Name = "{tit}", GroupName = "{grupo}", Order = {orden}, Description = "{desc}")]']
    cambio = "Recalcular();" if recalcular else "RedrawChart();"
    if tipo == "int":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = f"public int {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {minimo}, {maximo}); {cambio} }} }}"
    elif tipo == "decimal":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = (f"public decimal {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {numero(minimo, True)}, "
                  f"{numero(maximo, True)}); {cambio} }} }}")
    elif tipo == "bool":
        cuerpo = f"public bool {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    elif tipo == "string":
        cuerpo = f'public string {nombre} {{ get => {destino}; set {{ {destino} = value ?? ""; {cambio} }} }}'
    elif tipo == "hora":
        attrs.append('\t[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]')
        cuerpo = f"public TimeSpan {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    elif tipo.startswith("enum:"):
        cuerpo = f"public {tipo[5:]} {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    else:
        raise ValueError(tipo)
    return "\n".join(attrs) + "\n\t" + cuerpo + "\n"


def defectos():
    """Valores por defecto leídos del código (ParametrosV2, PesoFuente y VisualV2): la tabla no se desfasa."""
    import re
    d = {}
    with open(os.path.join(RAIZ, "SessionSweeps", "Nucleo", "ParametrosV2.cs"), encoding="utf-8") as f:
        texto = f.read()
    for nombre, valor in re.findall(r"public\s+[\w<>?]+\s+(\w+)\s*\{\s*get;\s*set;\s*\}\s*=\s*(\"[^\"]*\"|[^;]+);", texto):
        d[P + nombre] = valor.strip()
    for nombre in re.findall(r"public\s+bool\s+(\w+)\s*\{\s*get;\s*set;\s*\}(?!\s*=)", texto):
        d.setdefault(P + nombre, "false")
    for fuente in ("Iceberg", "Stops", "Sweeps", "Stacked", "Imbalance", "Absorption"):
        d[P + fuente + ".Enabled"] = "true"
        d[P + fuente + ".Weight"] = "1.0m"
    for r, valores in DEFECTOS_PERFIL.items():
        for campo, valor in valores.items():
            d[f"_cfg.Perfiles.{r}.{campo}"] = valor
    with open(os.path.join(RAIZ, "SessionSweeps", "Atas", "SessionSweepsPro.V2.cs"), encoding="utf-8") as f:
        texto = f.read()
    bloque = texto[texto.index("class VisualV2"):texto.index("private const int MaximoRecalculosPorFuentes")]
    for nombre, valor in re.findall(r"public\s+\w+\s+(\w+)(?:\s*=\s*([^;]+))?;", bloque):
        d["_v." + nombre] = (valor or "false").strip()
    return d


def bonito(valor, tipo):
    import re
    v = valor.replace("m", "") if re.fullmatch(r"[0-9.]+m", valor) else valor
    if tipo == "bool":
        return "Sí" if v == "true" else "No"
    if tipo == "hora":
        m = re.match(r"new\((\d+),\s*(\d+),", v)
        return f"{int(m.group(1)):02d}:{int(m.group(2)):02d}" if m else v
    if tipo.startswith("enum:"):
        return v.split(".")[-1]
    return v.strip('"')


def documentar():
    d = defectos()
    filas = ["# Parámetros de Sessions Sweeps Pro (Suite)", "",
             "Tabla generada por `herramientas/generar_parametros_v2.py` (nombres, rangos y descripciones del generador; valores por",
             "defecto leídos del código). No se edita a mano. Todos son **valores iniciales a calibrar** con el CSV; las puntuaciones",
             "no son probabilidades. Los grupos 1-15 son los de sesiones, niveles y visual (ver [LEEME.md](LEEME.md)).", ""]
    for grupo, params in GRUPOS:
        filas += [f"## {grupo}", "", "| Parámetro | En ATAS | Defecto | Rango | Descripción |", "|---|---|---|---|---|"]
        for nombre, tipo, destino, titulo, descripcion, minimo, maximo, _ in params:
            defecto = bonito(d.get(destino, "?"), tipo)
            rango = f"{minimo}-{maximo}" if minimo is not None else ""
            filas.append(f"| `{nombre}` | {titulo} | {defecto} | {rango} | {descripcion.replace('|', '/')} |")
        filas.append("")
    with open(DOC, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(filas))
    faltan = [p[2] for _, ps in GRUPOS for p in ps if p[2] not in d]
    if faltan:
        raise SystemExit("sin valor por defecto: " + ", ".join(faltan))


def main():
    partes = []
    for grupo, params in GRUPOS:
        partes.append(f"\t// --- {grupo} " + "-" * max(3, 100 - len(grupo)) + "\n")
        for i, p in enumerate(params):
            partes.append(propiedad(p, grupo, (i + 1) * 10))
    cuerpo = "\n".join(partes)
    texto = f"""// <auto-generated>
// Generado por SuiteOrderFlowStacker/herramientas/generar_parametros_v2.py: no editar a mano (cambiar el generador y
// volver a ejecutarlo). Parámetros de Sessions Sweeps Pro (Suite).
// </auto-generated>
#nullable enable
namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes.Editors;

using SessionSweeps.Nucleo;

public partial class SessionSweepsPro
{{
{cuerpo}}}
"""
    os.makedirs(os.path.dirname(SALIDA), exist_ok=True)
    with open(SALIDA, "w", encoding="utf-8", newline="\n") as f:
        f.write(texto)
    documentar()
    print("generado:", os.path.relpath(SALIDA, RAIZ), "y", os.path.relpath(DOC, RAIZ), sum(len(g[1]) for g in GRUPOS), "parámetros")


if __name__ == "__main__":
    main()
