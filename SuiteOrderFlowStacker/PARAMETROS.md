# Parámetros de la Suite Order Flow Stacker

Tabla generada automáticamente de los indicadores (atributos y valores por defecto del código): no se edita a mano.
Regenerar: `ACTUALIZAR_DOCS=1 bash SuiteOrderFlowStacker/pruebas/verificar.sh`. El Thermal Volume Profile + LVN tiene sus
parámetros en [../ThermalVolumeProfile/LEEME.md](../ThermalVolumeProfile/LEEME.md).

El nombre en inglés es el de la especificación (así se llama la propiedad); en ATAS se ve el nombre en español.
Las recomendaciones para NQ y MNQ están en la descripción; los umbrales de icebergs son valores iniciales: **calibrar**
con 1-2 semanas de CSV ([OPERATIVA.md](OPERATIVA.md#8-calibración-con-los-csv)).

## Qué grupos tiene cada indicador

| Indicador | Grupos |
|---|---|
| **Stacked Imbalance Pro (Suite)** (`StackedImbalancePro`) | 0. General · 1. Imbalance |
| **Iceberg Tracker Pro (Suite)** (`IcebergTrackerPro`) | 0. General · 1. Filtros Iceberg · 2. Umbrales por sesión (NQ) · 3. Umbrales por sesión (MNQ) · 4. Sesgo |
| **Stops Tracker Pro (Suite)** (`StopsTrackerPro`) | 0. General · 1. Stops |
| **Sweeps Tracker Pro (Suite)** (`SweepsTrackerPro`) | 0. General · 1. Sweeps |
| **Absorption Master (Suite)** (`AbsorptionMaster`) | 0. General · 1. Puntuación · 2. Setups · 3. Gestión · 4. Filtros · 5. Filtros Iceberg · 6. Umbrales por sesión (NQ) · 7. Umbrales por sesión (MNQ) · 8. Sesgo · 9. Imbalance · 10. Stops · 11. Sweeps |

## General

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `DisplayMode` | Modo visual | lista | Mínimo (solo el dato en texto) |  | Minimal = solo el dato en texto (recomendado); Markers = dato + marcador pequeño; Full = todo dibujado. |
| `InfoPanelPosition` | Esquina del panel | lista | según el indicador (abajo) |  | Esquina del panel de texto. Con varios indicadores de la suite, use esquinas o desplazamientos distintos. |
| `InfoPanelOffsetX` | Panel: desplazamiento horizontal (px) | entero | 0 | 0-3000 | Separa el panel del borde (para no tapar el de otro indicador). 0 = pegado a la esquina. |
| `InfoPanelOffsetY` | Panel: desplazamiento vertical (px) | entero | según el indicador (abajo) | 0-3000 | Separa el panel del borde (para no tapar el de otro indicador). |
| `InfoPanelMaxLines` | Líneas del panel | entero | 5 | 1-20 | Últimos N eventos mostrados (1-20). Recomendado: 5. |
| `FontSize` | Tamaño de letra | entero | 11 | 6-30 | Tamaño de fuente de etiquetas y panel. Recomendado: 11. |
| `EventLifetimeMin` | Vida de un evento (min) | entero | 30 | 1-1440 | Minutos que un evento sigue visible y 'reciente'. Recomendado: 30. |
| `InstrumentProfile` | Perfil de instrumento | lista | Automático (por el nombre) |  | Auto = MNQ si el instrumento empieza por MNQ, si no NQ. Elige la tabla de umbrales por sesión. |
| `EnableAlerts` | Alertas | sí/no | Sí |  | Una alerta por evento y por cambio de estado, solo en vivo (nunca al recalcular el historial). |
| `AlertSoundFile` | Sonido de la alerta | texto | alert1 |  | Sonido de ATAS para las alertas. Por defecto: alert1. |
| `EnableRecording` | Grabar en CSV | sí/no | Sí |  | Graba cada evento en %APPDATA%\ATAS\OrderFlowSuite\{Instrumento}\yyyy-MM-dd.csv (para calibrar). |
| `RealTimeOnly` | Solo tiempo real | sí/no | No |  | Si se activa, no se reconstruyen eventos con las velas del historial (solo los de ahora en adelante). |
| `LoadRecordedEvents` | Cargar eventos grabados | sí/no | Sí |  | Al cargar, lee los CSV de los últimos días y vuelve a dibujar sus eventos (en gris, sin sonar). |
| `RecordedDaysToLoad` | Días grabados a cargar | entero | 5 | 1-60 | Días de CSV que se cargan (hoy incluido). Recomendado: 5. |

## Puntuación

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `WeightIceberg` | Peso: iceberg válido | entero | 30 | 0-100 | Recomendado: 30. |
| `WeightHighProbBonus` | Peso extra: iceberg de alta probabilidad (*) | entero | 10 | 0-100 | Recomendado: 10. |
| `WeightProfileLevel` | Peso: en LVN / VAH / VAL / POC | entero | 20 | 0-100 | Recomendado: 20. |
| `WeightSweepOrStops` | Peso: barrido o stops previos | entero | 20 | 0-100 | Recomendado: 20. |
| `WeightImbalance` | Peso: stacked imbalance del mismo lado | entero | 15 | 0-100 | Recomendado: 15. |
| `WeightDeltaShift` | Peso: giro del delta a favor | entero | 15 | 0-100 | Recomendado: 15. |
| `MinSignalScore` | Puntuación mínima para la señal | entero | 65 | 0-100 | Ponderación de reglas (NO es una probabilidad). Recomendado: 65. |
| `ProfileTolTicks` | Tolerancia 'en el nivel' (ticks) | entero | 4 | 0-50 | Recomendado: 4. |

## Setups

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `EnableSetupA` | Setup A: absorción en nivel | sí/no | Sí |  | Iceberg AGUANTANDO en LVN / VAH / VAL -> GIRO. |
| `EnableSetupB` | Setup B: barrido + iceberg | sí/no | Sí |  | Stops o barrido de liquidez + iceberg que absorbe -> GIRO (máxima calidad). |
| `EnableSetupC` | Setup C: rechazo de LVN | sí/no | Sí |  | Precio en LVN + stacked imbalance + giro del delta -> GIRO. |
| `EnableSetupD` | Setup D: iceberg roto | sí/no | Sí |  | Iceberg ROTO + barrido de ruptura -> CONTINUIDAD. |
| `SetupCZoneMaxAgeSeconds` | Setup C: antigüedad máxima de la zona (s) | entero | 300 | 10-7200 | La zona de stacked imbalance en el LVN debe ser reciente. Recomendado: 300. |

## Gestión

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `EntryConfirmTicks` | Entrada tras (ticks a favor) | entero | 4 | 0-50 | Recomendado: 4 (4-6). |
| `StopBeyondTicks` | Stop más allá del iceberg/extremo (ticks) | entero | 3 | 0-50 | Recomendado: 3 (2-4). |
| `TargetMode` | Objetivo | lista | Siguiente nivel (POC / VAH / VAL / LVN) |  | Siguiente POC / VAH / VAL / LVN; si no hay, R:R fijo. |
| `TargetRewardRisk` | R:R fijo del objetivo | decimal | 2 | 0.5-20 | Objetivo = entrada + R:R x riesgo (modo R:R fijo o si no hay siguiente nivel). Recomendado: 2. |
| `MinRewardRisk` | R:R mínimo | decimal | 1.5 | 0.1-20 | Si el objetivo no da este R:R, no hay señal. Recomendado: 1,5. |

## Filtros

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `NewsBlackoutMinutes` | Blackout de noticias (± min) | entero | 2 | 0-120 | Sin señales ±N min alrededor de las noticias. Recomendado: 2. |
| `NewsTimes` | Horas de noticias (NY) | texto | 08:30;10:00;14:00 |  | Separadas por ';' (HH:mm, hora de Nueva York). Por defecto: 08:30;10:00;14:00. |
| `CooldownSeconds` | Tiempo entre señales del mismo lado (s) | entero | 60 | 0-3600 | Recomendado: 60. |
| `BlockAgainstTrend` | Sin GIRO contra tendencia sin barrido | sí/no | Sí |  | No da GIRO con el iceberg contra la tendencia si no hubo barrido o stops previos. |

## Filtros Iceberg

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `MboMode` | Datos MBO | lista | Automático (MBO si llega; si no, inferido) |  | Automático: MBO (nativo + sintético) si el feed lo envía (Rithmic); si no, inferido. Solo inferido: ignora el MBO. |
| `MinExecVisibleRatio` | Ejecutado / visible mínimo | decimal | 3 | 1-50 | Veces que lo ejecutado supera al tamaño visible (clip). Recomendado: 3. |
| `HighProbExecVisibleRatio` | Ratio de alta probabilidad | decimal | 5 | 1-100 | Ratio para la marca * (junto con el volumen de alta probabilidad de la sesión). Recomendado: 5. |
| `MaxDisplacementTicks` | Desplazamiento máximo (ticks) | entero | 2 | 1-10 | Ticks que el precio puede atravesar el nivel mientras se ejecuta (1-10). Recomendado: 2. |
| `DetectionWindowSeconds` | Ventana del evento (s) | entero | 60 | 5-600 | Sin actividad durante estos segundos, un candidato vuelve a empezar (5-600). Recomendado: 60. |
| `MinVisibleClipContracts` | Clip mínimo (informativo) | entero | 1 | 1-1000 | Informativo, NO es filtro: en NQ los clips suelen ser de 1-10 contratos. |
| `MaxVisibleClipContracts` | Clip máximo considerado | entero | 50 | 1-10000 | Un tamaño visible mayor no se considera iceberg. Recomendado: 50. |
| `SyntheticRefillMaxMs` | Sintético: tiempo máximo (ms) | entero | 50 | 1-2000 | Tiempo entre el llenado y la orden nueva parecida. Recomendado: 50. |
| `SyntheticSizeTolerancePercent` | Sintético: tolerancia de tamaño (%) | entero | 20 | 0-100 | Diferencia de tamaño aceptada entre la orden llenada y la nueva. Recomendado: 20. |
| `DetectSynthetic` | Detectar sintéticos | sí/no | Sí |  | Icebergs gestionados por software (orden nueva tras cada llenado). Necesita MBO. |
| `HoldConfirmTicks` | Ticks a favor -> AGUANTANDO | entero | 4 | 1-50 | Sin recargar y con el precio a estos ticks a favor, el iceberg pasa a AGUANTANDO. Recomendado: 4. |
| `BreakTicks` | Ticks atravesados -> ROTO | entero | 2 | 1-50 | El precio atraviesa el nivel estos ticks: ROTO (agotado). Recomendado: 2. |
| `ActiveRefillSeconds` | ACTIVO: recarga en los últimos (s) | entero | 10 | 1-300 | ACTIVO mientras recarga o se ejecuta en estos segundos. Recomendado: 10. |
| `MaxTrackedOrders` | Órdenes MBO seguidas (máximo) | entero | 20000 | 1000-500000 | Límite de memoria en la apertura de NY. Recomendado: 20000. |

## Umbrales por sesión (NQ)

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `PremarketMinutes` | Premercado NY: minutos antes de las 09:30 | entero | 60 | 0-450 | Duración de la sesión de premercado (configurable). Recomendado: 60. |
| `AsiaMinExecutedContracts` | Asia: ejecutado mínimo (ruido) | entero | 40 | 1-100000 | NQ, 18:00-02:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `AsiaHighProbContracts` | Asia: alta probabilidad | entero | 120 | 1-100000 | NQ, 18:00-02:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `AsiaMinRefills` | Asia: recargas mínimas | entero | 3 | 1-1000 | NQ, 18:00-02:00 ET. Recargas vistas (nativo y sintético). |
| `LondonMinExecutedContracts` | Londres: ejecutado mínimo (ruido) | entero | 80 | 1-100000 | NQ, 02:00-premercado ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `LondonHighProbContracts` | Londres: alta probabilidad | entero | 250 | 1-100000 | NQ, 02:00-premercado ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `LondonMinRefills` | Londres: recargas mínimas | entero | 4 | 1-1000 | NQ, 02:00-premercado ET. Recargas vistas (nativo y sintético). |
| `PremarketMinExecutedContracts` | Premercado NY: ejecutado mínimo (ruido) | entero | 100 | 1-100000 | NQ, 1 h antes de las 09:30 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `PremarketHighProbContracts` | Premercado NY: alta probabilidad | entero | 300 | 1-100000 | NQ, 1 h antes de las 09:30 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `PremarketMinRefills` | Premercado NY: recargas mínimas | entero | 4 | 1-1000 | NQ, 1 h antes de las 09:30 ET. Recargas vistas (nativo y sintético). |
| `NyOpenMinExecutedContracts` | Apertura NY: ejecutado mínimo (ruido) | entero | 150 | 1-100000 | NQ, 09:30-11:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `NyOpenHighProbContracts` | Apertura NY: alta probabilidad | entero | 400 | 1-100000 | NQ, 09:30-11:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `NyOpenMinRefills` | Apertura NY: recargas mínimas | entero | 5 | 1-1000 | NQ, 09:30-11:00 ET. Recargas vistas (nativo y sintético). |
| `MiddayMinExecutedContracts` | Mediodía: ejecutado mínimo (ruido) | entero | 100 | 1-100000 | NQ, 11:00-14:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MiddayHighProbContracts` | Mediodía: alta probabilidad | entero | 300 | 1-100000 | NQ, 11:00-14:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MiddayMinRefills` | Mediodía: recargas mínimas | entero | 4 | 1-1000 | NQ, 11:00-14:00 ET. Recargas vistas (nativo y sintético). |
| `CloseMinExecutedContracts` | Cierre: ejecutado mínimo (ruido) | entero | 150 | 1-100000 | NQ, 14:00-16:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `CloseHighProbContracts` | Cierre: alta probabilidad | entero | 400 | 1-100000 | NQ, 14:00-16:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `CloseMinRefills` | Cierre: recargas mínimas | entero | 5 | 1-1000 | NQ, 14:00-16:00 ET. Recargas vistas (nativo y sintético). |
| `OffHoursMinExecutedContracts` | Fuera de horario: ejecutado mínimo (ruido) | entero | 40 | 1-100000 | NQ, 16:00-18:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `OffHoursHighProbContracts` | Fuera de horario: alta probabilidad | entero | 120 | 1-100000 | NQ, 16:00-18:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `OffHoursMinRefills` | Fuera de horario: recargas mínimas | entero | 3 | 1-1000 | NQ, 16:00-18:00 ET. Recargas vistas (nativo y sintético). |

## Umbrales por sesión (MNQ)

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `MnqAsiaMinExecutedContracts` | Asia: ejecutado mínimo (ruido) | entero | 20 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 18:00-02:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqAsiaHighProbContracts` | Asia: alta probabilidad | entero | 60 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 18:00-02:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqAsiaMinRefills` | Asia: recargas mínimas | entero | 3 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 18:00-02:00 ET. Recargas vistas (nativo y sintético). |
| `MnqLondonMinExecutedContracts` | Londres: ejecutado mínimo (ruido) | entero | 40 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 02:00-premercado ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqLondonHighProbContracts` | Londres: alta probabilidad | entero | 125 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 02:00-premercado ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqLondonMinRefills` | Londres: recargas mínimas | entero | 4 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 02:00-premercado ET. Recargas vistas (nativo y sintético). |
| `MnqPremarketMinExecutedContracts` | Premercado NY: ejecutado mínimo (ruido) | entero | 50 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 1 h antes de las 09:30 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqPremarketHighProbContracts` | Premercado NY: alta probabilidad | entero | 150 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 1 h antes de las 09:30 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqPremarketMinRefills` | Premercado NY: recargas mínimas | entero | 4 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 1 h antes de las 09:30 ET. Recargas vistas (nativo y sintético). |
| `MnqNyOpenMinExecutedContracts` | Apertura NY: ejecutado mínimo (ruido) | entero | 75 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 09:30-11:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqNyOpenHighProbContracts` | Apertura NY: alta probabilidad | entero | 200 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 09:30-11:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqNyOpenMinRefills` | Apertura NY: recargas mínimas | entero | 5 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 09:30-11:00 ET. Recargas vistas (nativo y sintético). |
| `MnqMiddayMinExecutedContracts` | Mediodía: ejecutado mínimo (ruido) | entero | 50 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 11:00-14:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqMiddayHighProbContracts` | Mediodía: alta probabilidad | entero | 150 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 11:00-14:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqMiddayMinRefills` | Mediodía: recargas mínimas | entero | 4 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 11:00-14:00 ET. Recargas vistas (nativo y sintético). |
| `MnqCloseMinExecutedContracts` | Cierre: ejecutado mínimo (ruido) | entero | 75 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 14:00-16:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqCloseHighProbContracts` | Cierre: alta probabilidad | entero | 200 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 14:00-16:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqCloseMinRefills` | Cierre: recargas mínimas | entero | 5 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 14:00-16:00 ET. Recargas vistas (nativo y sintético). |
| `MnqOffHoursMinExecutedContracts` | Fuera de horario: ejecutado mínimo (ruido) | entero | 20 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 16:00-18:00 ET. Contratos ejecutados mínimos para considerar iceberg. CALIBRAR. |
| `MnqOffHoursHighProbContracts` | Fuera de horario: alta probabilidad | entero | 60 | 1-100000 | MNQ (punto de partida: NQ x 0,5), 16:00-18:00 ET. Contratos para la marca * (con el ratio de alta probabilidad). |
| `MnqOffHoursMinRefills` | Fuera de horario: recargas mínimas | entero | 3 | 1-1000 | MNQ (punto de partida: NQ x 0,5), 16:00-18:00 ET. Recargas vistas (nativo y sintético). |

## Sesgo

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `EventContextSeconds` | Ventana con barrido / stops (s) | entero | 30 | 5-600 | Relaciona un iceberg con un barrido o toma de stops (GIRO) y define los setups B y D. Recomendado: 30. |
| `BiasLevelToleranceTicks` | Tolerancia a VAH/VAL/LVN/POC/VWAP (ticks) | entero | 4 | 0-50 | Distancia para decir que el iceberg está 'en' un extremo o retroceso. Recomendado: 4. |
| `TrendLengthBars` | Longitud del filtro de tendencia (velas) | entero | 50 | 2-1000 | Velas de la EMA (si la fuente es EMA). Recomendado: 50. |
| `TrendSource` | Fuente de tendencia | lista | VWAP |  | VWAP (precio sobre/bajo la VWAP de la sesión) o EMA (pendiente). |

## Imbalance

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `ImbalanceRatioPercent` | Ratio diagonal mínimo (%) | decimal | 300 | 150-1000 | Ask del nivel N contra Bid del nivel N-1 (compra) o Bid de N contra Ask de N+1 (venta). 150-1000 %. Recomendado: 300. |
| `MinStackedLevels` | Niveles consecutivos mínimos | entero | 3 | 2-10 | Niveles seguidos con imbalance del mismo lado para formar la zona (2-10). Recomendado: 3. |
| `MinLevelVolumeContracts` | Volumen mínimo por nivel (contratos) | entero | 15 | 1-100000 | Volumen mínimo del lado dominante por nivel en RTH. NQ: 15; MNQ: 30-50. |
| `OffHoursFactor` | Factor fuera de RTH | decimal | 0.5 | 0.1-2 | Multiplicador del volumen mínimo fuera de 09:30-16:00 ET. Recomendado: 0,5. |
| `IgnoreZeroSide` | Lado débil a cero: exigir el mínimo | sí/no | Sí |  | Activado: un lado débil a 0 cuenta como 1 contrato y exige el volumen mínimo (sin 'infinitos'). Desactivado: ese nivel no cuenta. |
| `ZoneExtendBars` | Velas que se extiende la zona | entero | 50 | 1-2000 | Velas que la zona sigue viva y dibujada. Recomendado: 50. |
| `MinBreakTicks` | Ticks de cierre más allá -> ROTA | entero | 2 | 1-50 | Cierre de vela a estos ticks (o más) fuera de la zona = ROTA. Recomendado: 2. |
| `MaxActiveZones` | Zonas vivas máximas | entero | 20 | 1-200 | Las más antiguas dejan de extenderse. Recomendado: 20. |

## Stops

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `MinCascadeContractsRth` | Cascada mínima en RTH (contratos) | entero | 100 | 1-100000 | Volumen mínimo de la ráfaga en 09:30-16:00 ET. NQ: 100; MNQ: 200. |
| `MinCascadeContractsEth` | Cascada mínima fuera de RTH (contratos) | entero | 50 | 1-100000 | Volumen mínimo fuera de RTH. NQ: 50; MNQ: 100. |
| `CascadeMaxMs` | Duración máxima de la ráfaga (ms) | entero | 300 | 10-10000 | Ráfaga de órdenes a mercado en la misma dirección. Recomendado: 300. |
| `MinCascadeLevels` | Niveles mínimos atravesados | entero | 3 | 2-100 | Ticks que debe atravesar la ráfaga. Recomendado: 3. |
| `SwingLengthBars` | Velas a cada lado de un swing | entero | 20 | 2-500 | Un swing es el máximo/mínimo de 2N+1 velas. Recomendado: 20. |
| `RoundNumberStep` | Paso de números redondos (puntos) | entero | 50 | 0-100000 | Zonas en múltiplos de este paso. NQ: 50. 0 = sin números redondos. |
| `ShowProbableZones` | Mostrar zonas probables | sí/no | Sí |  | Dibuja las zonas probables (modo Full) y avisa en el panel al acercarse el precio. |
| `ZoneToleranceTicks` | Distancia a la zona (ticks) | entero | 4 | 0-100 | Distancia para asociar una cascada a una zona. Recomendado: 4. |
| `StopReclaimSeconds` | Vuelve dentro en (s) -> TRAMPA | entero | 60 | 1-3600 | Si el precio vuelve dentro en este tiempo: TRAMPA; si no: CONTINUACIÓN. Recomendado: 60. |
| `IbMinutes` | Duración del Initial Balance (min) | entero | 60 | 5-390 | IBH/IBL desde las 09:30 ET. Recomendado: 60. |
| `RequireZone` | Solo cascadas en una zona | sí/no | Sí |  | Activado: solo cascadas junto a una zona probable. Desactivado: también 'sin zona'. |

## Sweeps

| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |
|---|---|---|---|---|---|
| `MinSweepLevels` | Niveles mínimos | entero | 4 | 2-200 | Niveles barridos por una sola orden agresiva (4 ticks = 1 punto en NQ). Recomendado: 4. |
| `MinSweepContracts` | Volumen mínimo de la orden (contratos) | entero | 50 | 1-100000 | Volumen de la orden reconstruida. NQ: 50; MNQ: 100. |
| `SweepReclaimSeconds` | Vuelve dentro en (s) -> LIQUIDEZ | entero | 30 | 1-3600 | Si vuelve dentro del rango en este tiempo: LIQUIDEZ (trampa); si no: RUPTURA. Recomendado: 30. |
| `LevelToleranceTicks` | Distancia al nivel de referencia (ticks) | entero | 4 | 0-100 | El barrido debe empezar a esta distancia del nivel (o antes) y cruzarlo. Recomendado: 4. |
| `RefPdhPdl` | Niveles: PDH / PDL | sí/no | Sí |  | Máximo y mínimo del día anterior. |
| `RefIbhIbl` | Niveles: IBH / IBL | sí/no | Sí |  | Initial Balance (cuando termina). |
| `RefOnhOnl` | Niveles: ONH / ONL | sí/no | Sí |  | Máximo y mínimo overnight. |
| `RefSwings` | Niveles: swings | sí/no | Sí |  | Swings sin superar. |
| `RefVahVal` | Niveles: VAH / VAL | sí/no | Sí |  | Área de valor de la sesión RTH. |
| `OnlyAtReferenceLevels` | Solo barridos en un nivel | sí/no | Sí |  | Activado: solo barridos en un nivel de referencia o contra un iceberg. Desactivado: también 'sin nivel'. |

## Posición del panel por defecto

| Indicador | Esquina | Desplazamiento vertical |
|---|---|---|
| `StackedImbalancePro` | Arriba a la derecha | 150 px |
| `IcebergTrackerPro` | Arriba a la derecha | 0 px |
| `StopsTrackerPro` | Abajo a la derecha | 0 px |
| `SweepsTrackerPro` | Abajo a la derecha | 150 px |
| `AbsorptionMaster` | Arriba a la izquierda | 0 px |
