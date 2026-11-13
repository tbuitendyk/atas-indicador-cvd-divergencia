# Parámetros de Sessions Sweeps Pro (Suite)

Tabla generada por `herramientas/generar_parametros_v2.py` (nombres, rangos y descripciones del generador; valores por
defecto leídos del código). No se edita a mano. Todos son **valores iniciales a calibrar** con el CSV; las puntuaciones
no son probabilidades. Los grupos 1-15 son los de sesiones, niveles y visual (ver [LEEME.md](LEEME.md)).

## 16. Barras de volumen y horario

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `MedianLookbackBars` | Barras para las medianas | 30 | 5-500 | Medianas de duración, rango y volumen (solo barras de RTH). Recomendado: 30. |
| `PreT0DeltaSec` | Delta previo a T0 (s) | 60 | 5-600 | Segundos antes de la ruptura para el delta previo. Recomendado: 60. |
| `PanelRefreshMs` | Refresco del panel (ms) | 250 | 100-5000 | Cada cuánto se redibuja el panel flotante. Recomendado: 250. |
| `KillzoneStartNY` | Killzone: inicio (NY) | 09:30 |  | Inicio de la ventana de mayor peso (hora de Nueva York). 09:30 = 07:30 CDMX en verano, 08:30 en invierno. |
| `KillzoneEndNY` | Killzone: fin (NY) | 11:30 |  | Fin de la killzone (hora de Nueva York). |
| `NewsBlockMin` | Bloqueo por noticia (min) | 5 | 0-60 | Minutos antes y después de cada hora de noticia en los que no se alerta ni se entra (NOTICIA). |
| `NewsTimes` | Horas de noticias (NY) | 08:30;10:00;14:00 |  | Separadas por ';' en hora de Nueva York, por ejemplo 08:30;10:00;14:00. |

## 17. Fuentes de la suite

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `DedupTicks` | Duplicados: distancia (ticks) | 1 | 0-20 | Dos barridos del mismo nivel a esta distancia o menos son el mismo evento. |
| `DedupSec` | Duplicados: tiempo (s) | 2 | 0-60 | Y dentro de estos segundos. |
| `SuitePreSec` | Eventos previos a T0 (s) | 30 | 0-300 | Los eventos de la suite hasta estos segundos antes de la ruptura también cuentan. |
| `SuitePriceMarginTicks` | Margen de precio (ticks) | 8 | 0-40 | Distancia máxima entre un evento de la suite y el barrido para asociarlos. |
| `IcebergEnabled` | Iceberg: activado | Sí |  | Usar los eventos del Iceberg Tracker Pro. |
| `IcebergWeight` | Iceberg: peso | 1.0 | 0-2 | 0 a 2 sobre los puntos que aporta. 1 = tal cual. |
| `StopsEnabled` | Stops: activado | Sí |  | Usar los eventos del Stops Tracker Pro. |
| `StopsWeight` | Stops: peso | 1.0 | 0-2 | 0 a 2 sobre los puntos que aporta. |
| `SweepsEnabled` | Sweeps: activado | Sí |  | Usar los eventos del Sweeps Tracker Pro. |
| `SweepsWeight` | Sweeps: peso | 1.0 | 0-2 | 0 a 2 sobre los puntos que aporta. |
| `StackedEnabled` | Stacked: activado | Sí |  | Usar las zonas del Stacked Imbalance Pro. |
| `StackedWeight` | Stacked: peso | 1.0 | 0-2 | 0 a 2 sobre los puntos que aporta. |
| `ImbalanceEnabled` | Imbalance: activado | Sí |  | Usar los imbalances diagonales de la suite. |
| `ImbalanceWeight` | Imbalance: peso | 1.0 | 0-2 | 0 a 2 sobre los puntos que aporta. |
| `AbsorptionEnabled` | Absorción: activado | Sí |  | Usar las señales del Absorption Master. |
| `AbsorptionWeight` | Absorción: peso | 1.0 | 0-2 | 0 a 2 sobre los puntos que aporta. |

## 18. Niveles

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `EqualLevelTolTicks` | Iguales: tolerancia (ticks) | 3 | 0-20 | Máximos o mínimos a esta distancia o menos cuentan como iguales. |
| `EqualLevelMinTouches` | Iguales: toques mínimos | 2 | 2-10 | Toques para un nivel igual. |
| `LevelMinAgeMin` | Edad mínima del nivel (min) | 60 | 0-1440 | Un nivel más reciente puntúa menos en el bloque A. |

## 19. Stops

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `FastBarDurationMult` | Barra rápida: × duración mediana | 0.5 | 0.1-1 | Barra de volumen que dura menos de esto = rápida. |
| `StopsEventWindowSec` | Ventana de eventos de stops (s) | 30 | 1-300 | Alrededor de T0. |
| `VolBeyondRelMult` | Volumen más allá: × medio de barridos | 1.5 | 0.5-10 | Respecto a los últimos barridos. |
| `SweepHistoryCount` | Barridos para la media | 20 | 3-200 | Barridos anteriores para el volumen relativo. |

## 20. Absorción

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `IcebergMaxDistTicks` | Iceberg: distancia máxima (ticks) | 2 | 0-20 | Del extremo del barrido. |
| `F2MinBars` | F2: barras mínimas | 2 | 1-20 | Compresión tras el extremo. |
| `F2MaxBars` | F2: barras máximas | 5 | 1-30 |  |
| `F2MaxBarsRetrace` | F2: barras máximas en retroceso | 7 | 1-40 |  |
| `CompressionRangeMult` | Compresión: × rango mediano | 0.5 | 0.1-2 |  |
| `CompressionMaxTicks` | Compresión: máximo (ticks) | 8 | 1-60 |  |
| `ValueAreaPct` | Área de valor (fracción) | 0.70 | 0.5-0.95 | 0,70 = 70 % del volumen. |

## 21. Recuperación

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `ReclaimBufferTicks` | Recuperación: margen (ticks) | 1 | 0-20 | Cierre de vuelta al otro lado del nivel. |
| `ReclaimMaxBars` | Recuperación: barras máximas | 5 | 1-30 |  |
| `CompositeMaxBars` | Vela compuesta: barras máximas | 7 | 2-30 |  |
| `ZoneMaxBars` | Zona: barras máximas | 10 | 2-60 |  |
| `F3RangeMult` | F3: × rango mediano | 1.5 | 0.5-10 | Barra de desplazamiento. |
| `DeltaFlipMin` | Giro de delta mínimo | 0.20 | 0-1 |  |
| `ConsolidationMinCandles` | Consolidación: velas mínimas (1 m) | 2 | 1-30 | Velas de 1 minuto en el extremo, sin recuperar, con volumen: cuentan como absorción. |
| `ConsolidationMaxCandles` | Consolidación: velas máximas (1 m) | 14 | 2-60 | El barrido se sigue evaluando al menos estas velas de 1 minuto (10-14 es normal en NY) antes de vencer o llamarse RUPTURA. |
| `ConsolidationVolumeMult` | Consolidación: volumen × mediana por minuto | 1.0 | 0-10 | Volumen por minuto de la consolidación respecto a la mediana de 1 minuto del instrumento (calibración). |

## 22. Estados y alertas

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `ScoreRealMin` | Puntos BARRIDO REAL | 70 | 0-100 | Mínimo para BARRIDO DE LIQUIDEZ REAL (con toma de stops, absorción y recuperación). Puntuación, no probabilidad. |
| `ScorePremiumMin` | Puntos BARRIDO REAL PREMIUM | 85 | 0-100 | Además de todas las puertas premium. |
| `PremiumMinLevelScore` | PREMIUM: bloque A mínimo | 12 | 0-20 |  |
| `PremiumMaxReclaimBars` | PREMIUM: recuperación máxima (barras) | 3 | 1-20 |  |
| `PremiumUpgradeMaxBars` | UPGRADE PREMIUM: barras máximas | 10 | 1-60 |  |
| `RuptureMinConditions` | RUPTURA: condiciones mínimas | 3 | 1-6 |  |
| `AcceptanceCloses` | Aceptación: cierres | 2 | 1-10 | Cierres más allá del nivel. |
| `RuptureMaxAbsorptionScore` | RUPTURA: absorción máxima | 8 | 0-25 | Bloque C máximo para una ruptura. |
| `FailedBreakTicks` | RUPTURA FALLIDA: ticks | 4 | 1-40 |  |
| `FailedBreakBars` | RUPTURA FALLIDA: barras | 5 | 1-30 |  |
| `MaxEvaluationBars` | Evaluación: barras máximas | 15 | 3-100 |  |
| `MaxEvaluationSec` | Evaluación: segundos máximos | 300 | 30-3600 |  |
| `AlertMinState` | Estado mínimo para alertar | Real |  | REAL: suenan REAL y PREMIUM; PREMIUM: solo PREMIUM (el resto se ve en el panel). |

## 23. FVG / LVN

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `FvgMinTicks` | FVG: tamaño mínimo (ticks) | 4 | 1-100 |  |
| `FvgMaxActive` | FVG: máximo activos | 10 | 1-100 | Por marco y lado. |
| `Fvg1m` | FVG de 1m | Sí |  |  |
| `Fvg5m` | FVG de 5m | Sí |  |  |
| `BfvgWindowMin` | BFVG: ventana (min) | 30 | 1-240 | Tras el extremo del barrido. |
| `ConfluenceBonus` | Bonificación FVG + LVN | 10 | 0-30 |  |
| `TargetMinDistTicks` | Objetivo LVN: distancia mínima (ticks) | 8 | 0-200 |  |

## 24. Modelos

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `EnableModelA` | Modelo 1 (M1): barrido de liquidez | Sí |  | Barrido real (o premium) y recuperación; incluye los BFVG. |
| `EnableModelB` | Modelo 2 (M2): retroceso a LVN / EDGE / FVG | Sí |  | Retroceso a una zona con gatillo confirmado (las líneas LVN manuales valen). Incluye M2 - RETROCESO TRAS ROMPIMIENTO. |
| `EnableModelM3` | Modelo 3 (M3): iceberg rebasado con continuidad | Sí |  | Un iceberg (o super big trade) rebasado con imbalance, stops y aceptación: señal propia y retrocesos M3 - ROMPIMIENTO CONTINUIDAD. |
| `M3InvalidaTodos` | M3 invalida todos los M1 y M2 pendientes | Sí |  | Encendido (como pide el propietario): al activarse M3 se invalidan los M1 y M2 pendientes. Apagado: solo los contrarios. |
| `ModelBEnableLvn` | M2: LVN del Thermal | Sí |  |  |
| `ModelBEnableEdge` | M2: EDGE del Thermal | Sí |  |  |
| `ModelBEnableFvg` | M2: FVG | Sí |  | También FVG+LVN y FVG+EDGE. |
| `ModelBEnableEma` | M2: EMA 15 | Sí |  |  |
| `ModelBEnableNivel` | M2: nivel roto | Sí |  |  |
| `MaxAttemptsPerZone` | Intentos máximos por zona | 2 | 1-20 | Entradas en la misma zona y dirección. |
| `TpRMultiple` | TP (R múltiplos) | 2.0 | 0.5-20 | Objetivo a este múltiplo del riesgo (2R). |
| `TargetMode` | Objetivo | RMultiplo |  | R múltiplos, o el próximo LVN / EDGE (si no hay, R). |
| `ExitMode` | Salida | TpOEma15 |  | TP o cierre de 1 minuto al otro lado de la EMA 15, lo primero (por defecto). El SL nunca se amplía. |
| `Mod4AllowInternal` | M2 tras rompimiento en liquidez interna | No |  | Por defecto solo externa. |
| `Mod4MaxWaitMin` | M2 tras rompimiento: espera máxima (min) | 45 | 1-480 |  |
| `Mod4MaxRetracePct` | M2 tras rompimiento: retroceso máximo | 0.618 | 0.1-1 | Fracción de la pierna (0,618). |
| `Mod4MaxAttempts` | M2 tras rompimiento: intentos máximos | 2 | 1-10 | Entradas por ruptura. |
| `MinRR` | R:R mínimo | 1.5 | 0.5-10 |  |
| `StopMode` | Modo del stop | ZonaEstructura |  |  |

## 25. Riesgo

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `RiskMode` | Modo de riesgo | Monto |  |  |
| `RiskAmountUSD` | Riesgo por operación (USD) | 200 | 1-1000000 |  |
| `RiskPct` | Riesgo por operación (%) | 0.5 | 0.01-10 |  |
| `AccountSize` | Tamaño de la cuenta (USD) | 50000 | 100-100000000 | Si no se lee de ATAS. |
| `ReadAtasAccount` | Leer el saldo de ATAS | No |  | Del portafolio seleccionado en ATAS. |
| `MaxOpenRiskPct` | Riesgo abierto máximo (%) | 1.0 | 0.1-20 |  |
| `MicroImpulseEmaLen` | EMA de salida (Ema15Length) | 15 | 2-200 | EMA de 1 minuto para la salida y el contexto (15). |

## 26. Gatillo manual

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `ManualZoneTicks` | Zona manual (ticks) | 4 | 0-40 | Margen de la caja dibujada. |
| `ManualLookbackBars` | Barras de contexto | 10 | 1-100 |  |
| `MaxManualBoxes` | Cajas manuales máximas | 5 | 1-50 |  |

## 27. CVD

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `CvdMacroPivotBars` | Divergencia macro: velas por lado | 10 | 2-100 | Pivote de máximo a máximo o de mínimo a mínimo. Más = menos sensible. NY NQ: 10. |
| `CvdMicroPivotBars` | Divergencia micro: velas por lado | 3 | 1-30 | Dentro de una macro, para afinar el punto de giro. NY NQ: 3. |
| `CvdShowMicro` | Mostrar micro-divergencias | Sí |  |  |
| `CvdMaxLookbackBars` | Velas que se revisan | 120 | 20-5000 |  |
| `CvdReset` | Reinicio del CVD | DiaFuturos |  | Día de futuros (18:00 NY) o apertura RTH (09:30 NY). |
| `CvdMatrixMacroStrength` | CVD Matrix: fuerza macro (velas) | 12 | 2-100 | Filtro 1: swing amplio. |
| `CvdMatrixMicroStrength` | CVD Matrix: fuerza micro (velas) | 3 | 1-30 | Filtro 2: swing corto. |
| `CvdMatrixMacroValidity` | CVD Matrix: vigencia macro (velas) | 60 | 1-1000 |  |
| `CvdMatrixMicroValidity` | CVD Matrix: vigencia micro (velas) | 7 | 1-200 |  |
| `CvdMatrixMinWickPct` | CVD Matrix: mecha mínima (% del rango) | 40 | 0-100 | Filtro 3: absorción en la vela del swing. |
| `CvdMatrixMinWickVolumePct` | CVD Matrix: volumen en la mecha (%) | 30 | 0-100 | Filtro 3. |

## 28. Panel y dibujo

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `MostrarPanel` | Panel flotante | Sí |  | Ventana movible a cualquier lugar o monitor (Resumen / Detalle). Guarda su posición. |
| `ShowInChartPanel` | Panel dentro del gráfico | No |  | El mismo contenido dibujado sobre el gráfico (si no se quiere la ventana). |
| `InChartPanelX` | Panel en el gráfico: X | 25 | 0-5000 |  |
| `InChartPanelY` | Panel en el gráfico: Y | 60 | 0-5000 |  |
| `ShowSweepBoxes` | Cajas de barrido | Sí |  | Coloreadas por estado (REAL, PREMIUM, RUPTURA / ACEPTACIÓN, FALLIDO…). |
| `ShowModelBoxes` | Zonas de los modelos | Sí |  | M1 (barrido, BFVG), M2 (LVN / EDGE / FVG) y M3 (iceberg rebasado) con entrada, stop y objetivo. |
| `ShowFvgV2` | FVG / BFVG | Sí |  |  |
| `FvgOnlyAfterSweep` | FVG solo después de un barrido validado | Sí |  | Dibuja solo los FVG nacidos después de un BARRIDO REAL o PREMIUM y a favor de su giro (oculta los demás). |
| `BreakBannerSec` | Aviso de ruptura de línea: segundos | 60 | 0-600 | Cuánto queda el aviso arriba a la derecha del gráfico (0 = sin aviso). |
| `ShowInternalLiquidity` | Liquidez interna | Sí |  | Niveles de sesión e iguales. |
| `ShowExternalLiquidity` | Liquidez externa | Sí |  | PDH/PDL y semana anterior. |
| `ShowLvnLines` | LVN del Thermal | Sí |  | Las zonas que llegan del Thermal (activas y mitigadas). |
| `ShowManualTriggerBoxes` | Cajas del gatillo manual | No |  | Las dibuja el Thermal que las pidió; actívelo solo si el Thermal está en otro gráfico. |
| `ShowTrackerDiagnostics` | Diagnóstico de trackers | No |  | Marca hueca fucsia (SSP ICE, STOP, SWEEP, STK, IMB) en cada evento que Sessions Sweeps recibe del bus, para compararlo con el dibujo original del tracker. |

## 37. Alertas y canales

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `AlertRealAtas` | BARRIDO REAL: ATAS (sonido y ventana) | Sí |  |  |
| `AlertRealEmail` | BARRIDO REAL: correo | No |  |  |
| `AlertRealTelegram` | BARRIDO REAL: Telegram | No |  |  |
| `AlertPremiumAtas` | BARRIDO REAL PREMIUM: ATAS (sonido y ventana) | Sí |  |  |
| `AlertPremiumEmail` | BARRIDO REAL PREMIUM: correo | Sí |  |  |
| `AlertPremiumTelegram` | BARRIDO REAL PREMIUM: Telegram | Sí |  |  |
| `AlertRupturaAtas` | RUPTURA / ACEPTACIÓN: ATAS (sonido y ventana) | Sí |  |  |
| `AlertRupturaEmail` | RUPTURA / ACEPTACIÓN: correo | No |  |  |
| `AlertRupturaTelegram` | RUPTURA / ACEPTACIÓN: Telegram | No |  |  |
| `AlertEntradaAAtas` | GATILLO VÁLIDO M1: ATAS (sonido y ventana) | Sí |  |  |
| `AlertEntradaAEmail` | GATILLO VÁLIDO M1: correo | Sí |  |  |
| `AlertEntradaATelegram` | GATILLO VÁLIDO M1: Telegram | Sí |  |  |
| `AlertEntradaBAtas` | GATILLO VÁLIDO M2: ATAS (sonido y ventana) | Sí |  |  |
| `AlertEntradaBEmail` | GATILLO VÁLIDO M2: correo | Sí |  |  |
| `AlertEntradaBTelegram` | GATILLO VÁLIDO M2: Telegram | Sí |  |  |
| `AlertEntradaM3Atas` | GATILLO VÁLIDO M3: ATAS (sonido y ventana) | Sí |  |  |
| `AlertEntradaM3Email` | GATILLO VÁLIDO M3: correo | Sí |  |  |
| `AlertEntradaM3Telegram` | GATILLO VÁLIDO M3: Telegram | Sí |  |  |
| `AlertM3ActivadoAtas` | M3 ACTIVADO (iceberg rebasado): ATAS (sonido y ventana) | Sí |  |  |
| `AlertM3ActivadoEmail` | M3 ACTIVADO: correo | No |  |  |
| `AlertM3ActivadoTelegram` | M3 ACTIVADO: Telegram | No |  |  |
| `AlertLineaAtas` | RUPTURA DE LÍNEA DE SESIÓN: ATAS (sonido y aviso) | Sí |  | Sonido y aviso arriba a la derecha del gráfico al romperse la línea de un máximo o mínimo de sesión. |
| `AlertLineaEmail` | RUPTURA DE LÍNEA DE SESIÓN: correo | No |  |  |
| `AlertLineaTelegram` | RUPTURA DE LÍNEA DE SESIÓN: Telegram | No |  |  |
| `LineaAlertaAsia` | Ruptura de línea: Asia | Sí |  |  |
| `LineaAlertaLondres` | Ruptura de línea: Londres | Sí |  |  |
| `LineaAlertaPremercado` | Ruptura de línea: Premercado | Sí |  |  |
| `LineaAlertaNy` | Ruptura de línea: NY | Sí |  | Incluye el último máximo y mínimo recientes de NY. |

## 30. Instrumentos

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `AliasSimbolos` | Alias de símbolos |  |  | Si el proveedor usa otro nombre: ENQ=NQ;EP=ES. Sin reconocer: INSTRUMENTO NO SOPORTADO (sin señales). |
| `VolumePerBarFraction` | AUTO: fracción del minuto típico | 0.33 | 0.05-2 | Contratos por barra AUTO = mediana del volumen de 1 minuto en RTH × esta fracción (normalizada: igual para todos). |

## 31. Perfil NQ

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `NQ_VolumePerBarMode` | NQ: Contratos por barra: modo | AutoPorInstrumento |  | AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo. |
| `NQ_VolumePerBar` | NQ: Contratos por barra (manual / provisional) | 500 | 10-100000 | En modo AUTO solo se usa si no hay datos de RTH. |
| `NQ_VolumePerBarMin` | NQ: AUTO: mínimo de contratos por barra | 50 | 10-100000 | Límite de seguridad del valor AUTO. |
| `NQ_VolumePerBarMax` | NQ: AUTO: máximo de contratos por barra | 5000 | 10-100000 | Límite de seguridad del valor AUTO. |
| `NQ_CalibrationLookbackSessions` | NQ: Calibración: sesiones a usar | 20 | 1-250 | Últimas sesiones con RTH del historial cargado. |
| `NQ_MinimumCalibrationSessions` | NQ: Calibración: sesiones mínimas | 20 | 1-250 | Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL). |
| `NQ_TimeOfDayBucketMinutes` | NQ: Calibración: franja horaria (min) | 15 | 5-120 | Medianas por franja para el volumen relativo. |
| `NQ_MinVolBeyondFloor` | NQ: Volumen más allá: piso (contratos) | 50 | 0-100000 | Mínimo absoluto de contratos más allá del nivel. |
| `NQ_SuperBigTradeMinContracts` | NQ: Super big trade (contratos) | 150 | 1-100000 | Orden agresiva grande para el contexto de iceberg roto. |
| `NQ_ImbalanceMinVolume` | NQ: Imbalance: volumen mínimo (contratos) | 5 | 1-100000 | Del lado dominante. |
| `NQ_FinishedAuctionMaxContracts` | NQ: Subasta terminada: contratos máximos | 2 | 0-1000 | En el extremo. |
| `NQ_ImbalanceRatio` | NQ: Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). |
| `NQ_StackedMinLevels` | NQ: Stacked: niveles mínimos | 3 | 2-10 | Contiguos. |
| `NQ_AggressorShareMin` | NQ: Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del lado agresor. |
| `NQ_BurstSpeedMult` | NQ: Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana de este instrumento. |
| `NQ_F3DeltaPctMin` | NQ: F3: delta % mínimo | 0.25 | 0-1 | Delta / volumen de la barra de desplazamiento. |
| `NQ_ContDeltaPctMin` | NQ: Continuación: delta % mínimo | 0.30 | 0-1 |  |
| `NQ_DivergenceDeltaPctMin` | NQ: Divergencia de delta mínima | 0.15 | 0-1 | Delta % contra el movimiento. |
| `NQ_MinPenetrationTicks` | NQ: Penetración mínima (ticks) | 2 | 1-200 | Ticks de ESTE instrumento más allá del nivel. |
| `NQ_MaxSweepTicks` | NQ: Barrido máximo (ticks) | 24 | 2-1000 | Más allá: se vigila la ruptura. |
| `NQ_RecoveryTicks` | NQ: Recuperación 1m/5m (ticks) | 16 | 1-1000 | Recuperación que valida el barrido. |
| `NQ_StopBufferTicks` | NQ: Margen del stop (ticks) | 3 | 0-200 | Más allá de la zona, estructura o extremo. |
| `NQ_MaxStopTicks` | NQ: Stop máximo (ticks) | 40 | 2-2000 | Con más: NO EJECUTABLE. |
| `NQ_CvdMatrixDeltaFlip` | NQ: CVD Matrix: delta del giro (contratos) | 400 | 1-100000 | Filtro 4: delta de la vela de giro tras el swing. |

## 32. Perfil ES

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `ES_VolumePerBarMode` | ES: Contratos por barra: modo | AutoPorInstrumento |  | AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo. Valor inicial PROVISIONAL: calibrar. |
| `ES_VolumePerBar` | ES: Contratos por barra (manual / provisional) | 1500 | 10-100000 | En modo AUTO solo se usa si no hay datos de RTH. Valor inicial PROVISIONAL: calibrar. |
| `ES_VolumePerBarMin` | ES: AUTO: mínimo de contratos por barra | 50 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `ES_VolumePerBarMax` | ES: AUTO: máximo de contratos por barra | 5000 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `ES_CalibrationLookbackSessions` | ES: Calibración: sesiones a usar | 20 | 1-250 | Últimas sesiones con RTH del historial cargado. Valor inicial PROVISIONAL: calibrar. |
| `ES_MinimumCalibrationSessions` | ES: Calibración: sesiones mínimas | 20 | 1-250 | Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL). Valor inicial PROVISIONAL: calibrar. |
| `ES_TimeOfDayBucketMinutes` | ES: Calibración: franja horaria (min) | 15 | 5-120 | Medianas por franja para el volumen relativo. Valor inicial PROVISIONAL: calibrar. |
| `ES_MinVolBeyondFloor` | ES: Volumen más allá: piso (contratos) | 150 | 0-100000 | Mínimo absoluto de contratos más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `ES_SuperBigTradeMinContracts` | ES: Super big trade (contratos) | 400 | 1-100000 | Orden agresiva grande para el contexto de iceberg roto. Valor inicial PROVISIONAL: calibrar. |
| `ES_ImbalanceMinVolume` | ES: Imbalance: volumen mínimo (contratos) | 15 | 1-100000 | Del lado dominante. Valor inicial PROVISIONAL: calibrar. |
| `ES_FinishedAuctionMaxContracts` | ES: Subasta terminada: contratos máximos | 5 | 0-1000 | En el extremo. Valor inicial PROVISIONAL: calibrar. |
| `ES_ImbalanceRatio` | ES: Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). Valor inicial PROVISIONAL: calibrar. |
| `ES_StackedMinLevels` | ES: Stacked: niveles mínimos | 3 | 2-10 | Contiguos. Valor inicial PROVISIONAL: calibrar. |
| `ES_AggressorShareMin` | ES: Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del lado agresor. Valor inicial PROVISIONAL: calibrar. |
| `ES_BurstSpeedMult` | ES: Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana de este instrumento. Valor inicial PROVISIONAL: calibrar. |
| `ES_F3DeltaPctMin` | ES: F3: delta % mínimo | 0.25 | 0-1 | Delta / volumen de la barra de desplazamiento. Valor inicial PROVISIONAL: calibrar. |
| `ES_ContDeltaPctMin` | ES: Continuación: delta % mínimo | 0.30 | 0-1 | Valor inicial PROVISIONAL: calibrar. |
| `ES_DivergenceDeltaPctMin` | ES: Divergencia de delta mínima | 0.15 | 0-1 | Delta % contra el movimiento. Valor inicial PROVISIONAL: calibrar. |
| `ES_MinPenetrationTicks` | ES: Penetración mínima (ticks) | 1 | 1-200 | Ticks de ESTE instrumento más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `ES_MaxSweepTicks` | ES: Barrido máximo (ticks) | 8 | 2-1000 | Más allá: se vigila la ruptura. Valor inicial PROVISIONAL: calibrar. |
| `ES_RecoveryTicks` | ES: Recuperación 1m/5m (ticks) | 6 | 1-1000 | Recuperación que valida el barrido. Valor inicial PROVISIONAL: calibrar. |
| `ES_StopBufferTicks` | ES: Margen del stop (ticks) | 2 | 0-200 | Más allá de la zona, estructura o extremo. Valor inicial PROVISIONAL: calibrar. |
| `ES_MaxStopTicks` | ES: Stop máximo (ticks) | 16 | 2-2000 | Con más: NO EJECUTABLE. Valor inicial PROVISIONAL: calibrar. |
| `ES_CvdMatrixDeltaFlip` | ES: CVD Matrix: delta del giro (contratos) | 1000 | 1-100000 | Filtro 4: delta de la vela de giro tras el swing. Valor inicial PROVISIONAL: calibrar. |

## 33. Perfil YM

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `YM_VolumePerBarMode` | YM: Contratos por barra: modo | AutoPorInstrumento |  | AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo. Valor inicial PROVISIONAL: calibrar. |
| `YM_VolumePerBar` | YM: Contratos por barra (manual / provisional) | 150 | 10-100000 | En modo AUTO solo se usa si no hay datos de RTH. Valor inicial PROVISIONAL: calibrar. |
| `YM_VolumePerBarMin` | YM: AUTO: mínimo de contratos por barra | 50 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `YM_VolumePerBarMax` | YM: AUTO: máximo de contratos por barra | 5000 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `YM_CalibrationLookbackSessions` | YM: Calibración: sesiones a usar | 20 | 1-250 | Últimas sesiones con RTH del historial cargado. Valor inicial PROVISIONAL: calibrar. |
| `YM_MinimumCalibrationSessions` | YM: Calibración: sesiones mínimas | 20 | 1-250 | Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL). Valor inicial PROVISIONAL: calibrar. |
| `YM_TimeOfDayBucketMinutes` | YM: Calibración: franja horaria (min) | 15 | 5-120 | Medianas por franja para el volumen relativo. Valor inicial PROVISIONAL: calibrar. |
| `YM_MinVolBeyondFloor` | YM: Volumen más allá: piso (contratos) | 15 | 0-100000 | Mínimo absoluto de contratos más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `YM_SuperBigTradeMinContracts` | YM: Super big trade (contratos) | 40 | 1-100000 | Orden agresiva grande para el contexto de iceberg roto. Valor inicial PROVISIONAL: calibrar. |
| `YM_ImbalanceMinVolume` | YM: Imbalance: volumen mínimo (contratos) | 2 | 1-100000 | Del lado dominante. Valor inicial PROVISIONAL: calibrar. |
| `YM_FinishedAuctionMaxContracts` | YM: Subasta terminada: contratos máximos | 1 | 0-1000 | En el extremo. Valor inicial PROVISIONAL: calibrar. |
| `YM_ImbalanceRatio` | YM: Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). Valor inicial PROVISIONAL: calibrar. |
| `YM_StackedMinLevels` | YM: Stacked: niveles mínimos | 3 | 2-10 | Contiguos. Valor inicial PROVISIONAL: calibrar. |
| `YM_AggressorShareMin` | YM: Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del lado agresor. Valor inicial PROVISIONAL: calibrar. |
| `YM_BurstSpeedMult` | YM: Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana de este instrumento. Valor inicial PROVISIONAL: calibrar. |
| `YM_F3DeltaPctMin` | YM: F3: delta % mínimo | 0.25 | 0-1 | Delta / volumen de la barra de desplazamiento. Valor inicial PROVISIONAL: calibrar. |
| `YM_ContDeltaPctMin` | YM: Continuación: delta % mínimo | 0.30 | 0-1 | Valor inicial PROVISIONAL: calibrar. |
| `YM_DivergenceDeltaPctMin` | YM: Divergencia de delta mínima | 0.15 | 0-1 | Delta % contra el movimiento. Valor inicial PROVISIONAL: calibrar. |
| `YM_MinPenetrationTicks` | YM: Penetración mínima (ticks) | 1 | 1-200 | Ticks de ESTE instrumento más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `YM_MaxSweepTicks` | YM: Barrido máximo (ticks) | 12 | 2-1000 | Más allá: se vigila la ruptura. Valor inicial PROVISIONAL: calibrar. |
| `YM_RecoveryTicks` | YM: Recuperación 1m/5m (ticks) | 8 | 1-1000 | Recuperación que valida el barrido. Valor inicial PROVISIONAL: calibrar. |
| `YM_StopBufferTicks` | YM: Margen del stop (ticks) | 2 | 0-200 | Más allá de la zona, estructura o extremo. Valor inicial PROVISIONAL: calibrar. |
| `YM_MaxStopTicks` | YM: Stop máximo (ticks) | 25 | 2-2000 | Con más: NO EJECUTABLE. Valor inicial PROVISIONAL: calibrar. |
| `YM_CvdMatrixDeltaFlip` | YM: CVD Matrix: delta del giro (contratos) | 100 | 1-100000 | Filtro 4: delta de la vela de giro tras el swing. Valor inicial PROVISIONAL: calibrar. |

## 34. Perfil MNQ

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `MNQ_VolumePerBarMode` | MNQ: Contratos por barra: modo | AutoPorInstrumento |  | AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_VolumePerBar` | MNQ: Contratos por barra (manual / provisional) | 1000 | 10-100000 | En modo AUTO solo se usa si no hay datos de RTH. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_VolumePerBarMin` | MNQ: AUTO: mínimo de contratos por barra | 50 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_VolumePerBarMax` | MNQ: AUTO: máximo de contratos por barra | 5000 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_CalibrationLookbackSessions` | MNQ: Calibración: sesiones a usar | 20 | 1-250 | Últimas sesiones con RTH del historial cargado. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_MinimumCalibrationSessions` | MNQ: Calibración: sesiones mínimas | 20 | 1-250 | Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL). Valor inicial PROVISIONAL: calibrar. |
| `MNQ_TimeOfDayBucketMinutes` | MNQ: Calibración: franja horaria (min) | 15 | 5-120 | Medianas por franja para el volumen relativo. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_MinVolBeyondFloor` | MNQ: Volumen más allá: piso (contratos) | 100 | 0-100000 | Mínimo absoluto de contratos más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_SuperBigTradeMinContracts` | MNQ: Super big trade (contratos) | 300 | 1-100000 | Orden agresiva grande para el contexto de iceberg roto. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_ImbalanceMinVolume` | MNQ: Imbalance: volumen mínimo (contratos) | 10 | 1-100000 | Del lado dominante. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_FinishedAuctionMaxContracts` | MNQ: Subasta terminada: contratos máximos | 4 | 0-1000 | En el extremo. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_ImbalanceRatio` | MNQ: Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). Valor inicial PROVISIONAL: calibrar. |
| `MNQ_StackedMinLevels` | MNQ: Stacked: niveles mínimos | 3 | 2-10 | Contiguos. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_AggressorShareMin` | MNQ: Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del lado agresor. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_BurstSpeedMult` | MNQ: Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana de este instrumento. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_F3DeltaPctMin` | MNQ: F3: delta % mínimo | 0.25 | 0-1 | Delta / volumen de la barra de desplazamiento. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_ContDeltaPctMin` | MNQ: Continuación: delta % mínimo | 0.30 | 0-1 | Valor inicial PROVISIONAL: calibrar. |
| `MNQ_DivergenceDeltaPctMin` | MNQ: Divergencia de delta mínima | 0.15 | 0-1 | Delta % contra el movimiento. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_MinPenetrationTicks` | MNQ: Penetración mínima (ticks) | 2 | 1-200 | Ticks de ESTE instrumento más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_MaxSweepTicks` | MNQ: Barrido máximo (ticks) | 24 | 2-1000 | Más allá: se vigila la ruptura. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_RecoveryTicks` | MNQ: Recuperación 1m/5m (ticks) | 16 | 1-1000 | Recuperación que valida el barrido. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_StopBufferTicks` | MNQ: Margen del stop (ticks) | 3 | 0-200 | Más allá de la zona, estructura o extremo. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_MaxStopTicks` | MNQ: Stop máximo (ticks) | 40 | 2-2000 | Con más: NO EJECUTABLE. Valor inicial PROVISIONAL: calibrar. |
| `MNQ_CvdMatrixDeltaFlip` | MNQ: CVD Matrix: delta del giro (contratos) | 800 | 1-100000 | Filtro 4: delta de la vela de giro tras el swing. Valor inicial PROVISIONAL: calibrar. |

## 35. Perfil MES

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `MES_VolumePerBarMode` | MES: Contratos por barra: modo | AutoPorInstrumento |  | AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo. Valor inicial PROVISIONAL: calibrar. |
| `MES_VolumePerBar` | MES: Contratos por barra (manual / provisional) | 1500 | 10-100000 | En modo AUTO solo se usa si no hay datos de RTH. Valor inicial PROVISIONAL: calibrar. |
| `MES_VolumePerBarMin` | MES: AUTO: mínimo de contratos por barra | 50 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `MES_VolumePerBarMax` | MES: AUTO: máximo de contratos por barra | 5000 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `MES_CalibrationLookbackSessions` | MES: Calibración: sesiones a usar | 20 | 1-250 | Últimas sesiones con RTH del historial cargado. Valor inicial PROVISIONAL: calibrar. |
| `MES_MinimumCalibrationSessions` | MES: Calibración: sesiones mínimas | 20 | 1-250 | Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL). Valor inicial PROVISIONAL: calibrar. |
| `MES_TimeOfDayBucketMinutes` | MES: Calibración: franja horaria (min) | 15 | 5-120 | Medianas por franja para el volumen relativo. Valor inicial PROVISIONAL: calibrar. |
| `MES_MinVolBeyondFloor` | MES: Volumen más allá: piso (contratos) | 150 | 0-100000 | Mínimo absoluto de contratos más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `MES_SuperBigTradeMinContracts` | MES: Super big trade (contratos) | 400 | 1-100000 | Orden agresiva grande para el contexto de iceberg roto. Valor inicial PROVISIONAL: calibrar. |
| `MES_ImbalanceMinVolume` | MES: Imbalance: volumen mínimo (contratos) | 15 | 1-100000 | Del lado dominante. Valor inicial PROVISIONAL: calibrar. |
| `MES_FinishedAuctionMaxContracts` | MES: Subasta terminada: contratos máximos | 5 | 0-1000 | En el extremo. Valor inicial PROVISIONAL: calibrar. |
| `MES_ImbalanceRatio` | MES: Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). Valor inicial PROVISIONAL: calibrar. |
| `MES_StackedMinLevels` | MES: Stacked: niveles mínimos | 3 | 2-10 | Contiguos. Valor inicial PROVISIONAL: calibrar. |
| `MES_AggressorShareMin` | MES: Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del lado agresor. Valor inicial PROVISIONAL: calibrar. |
| `MES_BurstSpeedMult` | MES: Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana de este instrumento. Valor inicial PROVISIONAL: calibrar. |
| `MES_F3DeltaPctMin` | MES: F3: delta % mínimo | 0.25 | 0-1 | Delta / volumen de la barra de desplazamiento. Valor inicial PROVISIONAL: calibrar. |
| `MES_ContDeltaPctMin` | MES: Continuación: delta % mínimo | 0.30 | 0-1 | Valor inicial PROVISIONAL: calibrar. |
| `MES_DivergenceDeltaPctMin` | MES: Divergencia de delta mínima | 0.15 | 0-1 | Delta % contra el movimiento. Valor inicial PROVISIONAL: calibrar. |
| `MES_MinPenetrationTicks` | MES: Penetración mínima (ticks) | 1 | 1-200 | Ticks de ESTE instrumento más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `MES_MaxSweepTicks` | MES: Barrido máximo (ticks) | 8 | 2-1000 | Más allá: se vigila la ruptura. Valor inicial PROVISIONAL: calibrar. |
| `MES_RecoveryTicks` | MES: Recuperación 1m/5m (ticks) | 6 | 1-1000 | Recuperación que valida el barrido. Valor inicial PROVISIONAL: calibrar. |
| `MES_StopBufferTicks` | MES: Margen del stop (ticks) | 2 | 0-200 | Más allá de la zona, estructura o extremo. Valor inicial PROVISIONAL: calibrar. |
| `MES_MaxStopTicks` | MES: Stop máximo (ticks) | 16 | 2-2000 | Con más: NO EJECUTABLE. Valor inicial PROVISIONAL: calibrar. |
| `MES_CvdMatrixDeltaFlip` | MES: CVD Matrix: delta del giro (contratos) | 1000 | 1-100000 | Filtro 4: delta de la vela de giro tras el swing. Valor inicial PROVISIONAL: calibrar. |

## 36. Perfil MYM

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `MYM_VolumePerBarMode` | MYM: Contratos por barra: modo | AutoPorInstrumento |  | AUTO: de la mediana del volumen de 1 minuto en RTH de ESTE instrumento. Manual: el valor de abajo. Valor inicial PROVISIONAL: calibrar. |
| `MYM_VolumePerBar` | MYM: Contratos por barra (manual / provisional) | 150 | 10-100000 | En modo AUTO solo se usa si no hay datos de RTH. Valor inicial PROVISIONAL: calibrar. |
| `MYM_VolumePerBarMin` | MYM: AUTO: mínimo de contratos por barra | 50 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `MYM_VolumePerBarMax` | MYM: AUTO: máximo de contratos por barra | 5000 | 10-100000 | Límite de seguridad del valor AUTO. Valor inicial PROVISIONAL: calibrar. |
| `MYM_CalibrationLookbackSessions` | MYM: Calibración: sesiones a usar | 20 | 1-250 | Últimas sesiones con RTH del historial cargado. Valor inicial PROVISIONAL: calibrar. |
| `MYM_MinimumCalibrationSessions` | MYM: Calibración: sesiones mínimas | 20 | 1-250 | Con menos: CALIBRACIÓN INSUFICIENTE (valores PROVISIONAL). Valor inicial PROVISIONAL: calibrar. |
| `MYM_TimeOfDayBucketMinutes` | MYM: Calibración: franja horaria (min) | 15 | 5-120 | Medianas por franja para el volumen relativo. Valor inicial PROVISIONAL: calibrar. |
| `MYM_MinVolBeyondFloor` | MYM: Volumen más allá: piso (contratos) | 15 | 0-100000 | Mínimo absoluto de contratos más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `MYM_SuperBigTradeMinContracts` | MYM: Super big trade (contratos) | 40 | 1-100000 | Orden agresiva grande para el contexto de iceberg roto. Valor inicial PROVISIONAL: calibrar. |
| `MYM_ImbalanceMinVolume` | MYM: Imbalance: volumen mínimo (contratos) | 2 | 1-100000 | Del lado dominante. Valor inicial PROVISIONAL: calibrar. |
| `MYM_FinishedAuctionMaxContracts` | MYM: Subasta terminada: contratos máximos | 1 | 0-1000 | En el extremo. Valor inicial PROVISIONAL: calibrar. |
| `MYM_ImbalanceRatio` | MYM: Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). Valor inicial PROVISIONAL: calibrar. |
| `MYM_StackedMinLevels` | MYM: Stacked: niveles mínimos | 3 | 2-10 | Contiguos. Valor inicial PROVISIONAL: calibrar. |
| `MYM_AggressorShareMin` | MYM: Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del lado agresor. Valor inicial PROVISIONAL: calibrar. |
| `MYM_BurstSpeedMult` | MYM: Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana de este instrumento. Valor inicial PROVISIONAL: calibrar. |
| `MYM_F3DeltaPctMin` | MYM: F3: delta % mínimo | 0.25 | 0-1 | Delta / volumen de la barra de desplazamiento. Valor inicial PROVISIONAL: calibrar. |
| `MYM_ContDeltaPctMin` | MYM: Continuación: delta % mínimo | 0.30 | 0-1 | Valor inicial PROVISIONAL: calibrar. |
| `MYM_DivergenceDeltaPctMin` | MYM: Divergencia de delta mínima | 0.15 | 0-1 | Delta % contra el movimiento. Valor inicial PROVISIONAL: calibrar. |
| `MYM_MinPenetrationTicks` | MYM: Penetración mínima (ticks) | 1 | 1-200 | Ticks de ESTE instrumento más allá del nivel. Valor inicial PROVISIONAL: calibrar. |
| `MYM_MaxSweepTicks` | MYM: Barrido máximo (ticks) | 12 | 2-1000 | Más allá: se vigila la ruptura. Valor inicial PROVISIONAL: calibrar. |
| `MYM_RecoveryTicks` | MYM: Recuperación 1m/5m (ticks) | 8 | 1-1000 | Recuperación que valida el barrido. Valor inicial PROVISIONAL: calibrar. |
| `MYM_StopBufferTicks` | MYM: Margen del stop (ticks) | 2 | 0-200 | Más allá de la zona, estructura o extremo. Valor inicial PROVISIONAL: calibrar. |
| `MYM_MaxStopTicks` | MYM: Stop máximo (ticks) | 25 | 2-2000 | Con más: NO EJECUTABLE. Valor inicial PROVISIONAL: calibrar. |
| `MYM_CvdMatrixDeltaFlip` | MYM: CVD Matrix: delta del giro (contratos) | 100 | 1-100000 | Filtro 4: delta de la vela de giro tras el swing. Valor inicial PROVISIONAL: calibrar. |
