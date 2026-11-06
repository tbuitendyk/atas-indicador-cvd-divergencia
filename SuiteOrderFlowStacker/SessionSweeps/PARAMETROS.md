# Parámetros de Session Sweeps Pro v2.0 (Suite)

Tabla generada por `herramientas/generar_parametros_v2.py` (nombres, rangos y descripciones del generador; valores por
defecto leídos del código). No se edita a mano. Todos son **valores iniciales a calibrar** con el CSV; las puntuaciones
no son probabilidades. Los grupos 1-15 son los del NQ Session Sweeps Pro (ver [LEEME.md](LEEME.md#ajustes-heredados-del-nq)).

## 16. Barras de volumen y horario

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `VolumePerBar` | Contratos por barra (NQ) | 500 | 10-100000 | Cada barra de volumen cierra al llegar a estos contratos. NQ: 500 (calibrar con el CSV). |
| `VolumePerBarMnq` | Contratos por barra (MNQ) | 1000 | 10-100000 | El MNQ negocia más contratos: 1000 como punto de partida. |
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
| `MinPenetrationTicks` | Penetración mínima (ticks) | 2 | 1-40 | Ticks más allá del nivel para que cuente como barrido. |
| `MaxSweepTicks` | Barrido máximo (ticks) | 24 | 4-200 | Más allá de esto deja de ser un barrido y se vigila la ruptura. |
| `BurstSpeedMult` | Ráfaga: × velocidad mediana | 2.5 | 1-10 | Velocidad de 5 s respecto a la mediana para una ráfaga de stops. |
| `FastBarDurationMult` | Barra rápida: × duración mediana | 0.5 | 0.1-1 | Barra de volumen que dura menos de esto = rápida. |
| `AggressorShareMin` | Agresor mínimo (fracción) | 0.65 | 0.5-1 | Parte del volumen más allá del nivel del lado agresor (0,65 = 65 %). |
| `StopsEventWindowSec` | Ventana de eventos de stops (s) | 30 | 1-300 | Alrededor de T0. |
| `VolBeyondRelMult` | Volumen más allá: × medio de barridos | 1.5 | 0.5-10 | Respecto a los últimos barridos. |
| `SweepHistoryCount` | Barridos para la media | 20 | 3-200 | Barridos anteriores para el volumen relativo. |
| `MinVolBeyondFloor` | Volumen más allá: piso (contratos) | 50 | 0-100000 | Mínimo absoluto. |

## 20. Absorción

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `DivergenceDeltaPctMin` | Divergencia de delta mínima | 0.15 | 0-1 | Delta % de la barra contra el movimiento (0,15 = 15 %). |
| `IcebergMaxDistTicks` | Iceberg: distancia máxima (ticks) | 2 | 0-20 | Del extremo del barrido. |
| `F2MinBars` | F2: barras mínimas | 2 | 1-20 | Compresión tras el extremo. |
| `F2MaxBars` | F2: barras máximas | 5 | 1-30 |  |
| `F2MaxBarsRetrace` | F2: barras máximas en retroceso | 7 | 1-40 |  |
| `CompressionRangeMult` | Compresión: × rango mediano | 0.5 | 0.1-2 |  |
| `CompressionMaxTicks` | Compresión: máximo (ticks) | 8 | 1-60 |  |
| `ValueAreaPct` | Área de valor (fracción) | 0.70 | 0.5-0.95 | 0,70 = 70 % del volumen. |
| `FinishedAuctionMaxContracts` | Subasta terminada: contratos máximos | 2 | 0-50 | En el extremo. |
| `StackedMinLevels` | Stacked: niveles mínimos | 3 | 2-10 | Contiguos. |
| `ImbalanceRatio` | Imbalance: ratio | 3.0 | 1.5-10 | Diagonal (3 = 300 %). |
| `ImbalanceMinVolume` | Imbalance: volumen mínimo | 5 | 1-1000 | Del lado dominante. |

## 21. Recuperación

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `ReclaimBufferTicks` | Recuperación: margen (ticks) | 1 | 0-20 | Cierre de vuelta al otro lado del nivel. |
| `ReclaimMaxBars` | Recuperación: barras máximas | 5 | 1-30 |  |
| `CompositeMaxBars` | Vela compuesta: barras máximas | 7 | 2-30 |  |
| `ZoneMaxBars` | Zona: barras máximas | 10 | 2-60 |  |
| `F3RangeMult` | F3: × rango mediano | 1.5 | 0.5-10 | Barra de desplazamiento. |
| `F3DeltaPctMin` | F3: delta % mínimo | 0.25 | 0-1 |  |
| `ContDeltaPctMin` | Continuación: delta % mínimo | 0.30 | 0-1 |  |
| `DeltaFlipMin` | Giro de delta mínimo | 0.20 | 0-1 |  |
| `RecoveryTicks` | Recuperación 1m/5m (ticks) | 16 | 1-200 | Recuperación que valida el barrido en el panel (anexo: 16). |
| `StopBufferTicks` | Margen del stop (ticks) | 3 | 0-40 | Más allá de la zona, estructura o POC; también para FALLIDO. |

## 22. Estados y alertas

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `ScoreTrapB` | Puntos TRAMPA B | 55 | 0-100 | Puntuación (no probabilidad). |
| `ScoreTrapA` | Puntos TRAMPA A | 70 | 0-100 |  |
| `ScorePremium` | Puntos TRAMPA PREMIUM | 85 | 0-100 | Además de las 7 puertas. |
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
| `AlertMinState` | Estado mínimo para alertar | TrampaA |  | Las trampas por debajo se ven en el panel pero no suenan. |

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
| `Mod3InvalidatesAllMod1Mod2` | MOD3 invalida todos los MOD1/MOD2 | No |  | Desactivado: solo los contrarios a la dirección del MOD3. |
| `Mod4AllowInternal` | MOD4 en liquidez interna | No |  | Por defecto solo externa. |
| `Mod4MaxWaitMin` | MOD4: espera máxima (min) | 45 | 1-480 |  |
| `Mod4MaxRetracePct` | MOD4: retroceso máximo | 0.618 | 0.1-1 | Fracción de la pierna (0,618). |
| `Mod4MaxAttempts` | MOD4: intentos máximos | 2 | 1-10 | Entradas por ruptura. |
| `MinRR` | R:R mínimo | 1.5 | 0.5-10 |  |
| `MaxStopTicks` | Stop máximo (ticks) | 40 | 4-400 |  |
| `SuperBigTradeMinContracts` | MOD3: super big trade (contratos) | 150 | 10-100000 | El documento no fija el valor: calibrar con el CSV. |
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
| `MicroImpulseEmaLen` | Micro-impulso: EMA | 15 | 2-200 | EMA de 1m (anexo: 15). |
| `MicroImpulseFromEntry` | Micro-impulso desde la entrada | No |  | Si no, desde el armado. |

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

## 28. Panel y dibujo

| Parámetro | En ATAS | Defecto | Rango | Descripción |
|---|---|---|---|---|
| `MostrarPanel` | Panel flotante | Sí |  | Ventana movible a cualquier lugar o monitor (Resumen / Detalle). Guarda su posición. |
| `ShowInChartPanel` | Panel dentro del gráfico | No |  | El mismo contenido dibujado sobre el gráfico (si no se quiere la ventana). |
| `InChartPanelX` | Panel en el gráfico: X | 25 | 0-5000 |  |
| `InChartPanelY` | Panel en el gráfico: Y | 60 | 0-5000 |  |
| `ShowSweepBoxes` | Cajas de barrido (v2) | Sí |  | Coloreadas por estado (TRAMPA, PREMIUM, RUPTURA, FALLIDO…). |
| `ShowModelBoxes` | Zonas de los modelos | Sí |  | MOD1-MOD4 y BFVG con entrada, stop y objetivos. |
| `ShowFvgV2` | FVG / BFVG (v2) | Sí |  |  |
| `ShowInternalLiquidity` | Liquidez interna | Sí |  | Niveles de sesión e iguales. |
| `ShowExternalLiquidity` | Liquidez externa | Sí |  | PDH/PDL y semana anterior. |
| `ShowLvnLines` | LVN del Thermal | Sí |  | Las zonas que llegan del Thermal (activas y mitigadas). |
| `ShowManualTriggerBoxes` | Cajas del gatillo manual | No |  | Las dibuja el Thermal que las pidió; actívelo solo si el Thermal está en otro gráfico. |
| `ShowCvdDivergences` | Divergencias del CVD en el precio | Sí |  | Tramos entre pivotes, sin líneas verticales. |
