# Plan — Session Sweeps Pro v2.0 (propuesta, pendiente de aprobación)

Fuente: "PROMPT MAESTRO PARA CLAUDE CODE - NQ SESSION SWEEPS PRO v2.0 (INTEGRACION TOTAL)" (2026-11-06).
Pedido del propietario: el indicador modificado se llama **Session Sweeps Pro** y es **otro indicador**. El NQ Session
Sweeps Pro actual (`NQSessionSweepsPro/`, `NQSessionSweepsPro.dll`) **no se toca** y sigue instalado tal cual.

## 0. Lo que ya está comprobado (base del plan)

| Pieza que pide el documento | Ya existe y funciona en ATAS real | Dónde |
|---|---|---|
| Ventana WPF flotante (mover, otro monitor, guarda posición y tamaño, Topmost, letra) | Sí: la ventana del reporte | `IndicadorCompleto/Ventana/VentanaReporte.cs` |
| Ratón sobre el gráfico (clic, arrastrar un recuadro, de píxel a precio y a barra) | Sí: la barra PERFIL / RANGO / ZONA / LVN | `ThermalVolumeProfile.cs` (`ProcessMouseDown/Move/Up`, `PrecioDeY`, `BarraDeX`) |
| MBO, DOM, trades con `AggressorExchangeOrderId` | Sí (compilado contra las DLL reales) | `SuiteOrderFlowStacker` |
| Icebergs, stops, sweeps, stacked, absorción | Sí: los 5 indicadores de la suite | `SuiteOrderFlowStacker/Nucleo` |
| Sesiones, niveles, Pine "Session Sweeps", FVG/IFVG, MSS, H1, proyección, correo | Sí | `NQSessionSweepsPro/Nucleo` (se copia) |
| CSV asíncrono por día + recarga | Sí | `SuiteOrderFlowStacker/Nucleo/Grabacion.cs` |

## a) Archivos

### Ubicación (recomendada): dentro de la DLL de la suite

Session Sweeps Pro sería el **7.º indicador de `SuiteOrderFlowStacker.dll`**. Motivo: el bus de eventos (2.2) es un
estático compartido; dentro de **una sola DLL** todos los indicadores comparten ese estático con seguridad. Entre DLL
distintas depende de cómo ATAS cargue cada DLL (punto de duda del propio documento). Las capas del documento (Common,
Nucleo, Atas, Ui) se mantienen como **carpetas y espacios de nombres**, igual que hicimos con la suite. El instalador
no cambia: `instalar.txt` ya lista `SuiteOrderFlowStacker`.

### Crear (todo bajo `SuiteOrderFlowStacker/`)

| Carpeta / archivo | Contenido |
|---|---|
| `Comun/Eventos.cs` | Tipos compartidos: `NivelLiquidez`, `LvnZone`, `FvgZone`, `IcebergEvent`, `StopsEvent`, `SweepTrackerEvent`, `StackedEvent`, `ImbalanceEvent`, `AbsorptionEvent`, `ManualTriggerRequest/Result`, `RangeReset`. Sin ATAS ni UI. |
| `Comun/SuiteEventBus.cs` | Singleton por instrumento, buffer circular por tipo (`SuiteBufferSize`), cerrojo, consulta por tiempo y precio, latido cada 2 s → CARGADO / NO CARGADO / SIN DATOS, versión. |
| `SessionSweeps/Nucleo/` (sin ATAS) | `VirtualVolumeBarEngine`, `CompositeBarBuilder` (3 contextos), `PhaseDetector` (F1/F2/F3), `LevelQualityScorer`, `SweepScorer` (bloques A-E), `SweepStateMachine` (estados, congelación, transiciones 7.7), `FvgEngine` (+BFVG), `LvnRepository`, `TriggerAnalyzer`, `ModelEngine` (MOD1-MOD4 + BFVG), `ConflictResolver`, `RiskCalculator`, `MicroImpulseManager`, `ContextEngine` (H1/15m/5m, CVD, VWAP), `CsvLogger`. Se **copian y adaptan** de NQ: sesiones, niveles, Pine, FVG/MSS, H1, proyección (gestión de objetivos existente). |
| `SessionSweeps/Atas/` | `SessionSweepsPro.cs` (datos, trades en vivo e historial), `.Dibujo.cs` (cajas, FVG, liquidez), `.Alertas.cs`, `Correo.cs` (copia del de NQ), parámetros generados. |
| `Ui/FloatingWindowHost.cs` | Ventana WPF reutilizable, del patrón comprobado de `VentanaReporte`, + recolocar si queda fuera de pantalla (`SystemParameters.VirtualScreen*`). |
| `Ui/PanelPrincipal.cs`, `Ui/PanelThermal.cs` | Panel Resumen/Detalle (11) y panel Thermal con GATILLO (12). |
| `pruebas/PruebasSessionSweeps*.cs` | T1-T31 (buyside y espejo sellside) + sustitutos para `CumulativeTrade`, teclado y cuenta. |
| `SessionSweeps/LEEME.md` | Manual de usuario del indicador nuevo. |

### Modificar

| Archivo | Cambio |
|---|---|
| `ThermalVolumeProfile/ThermalVolumeProfile.cs` | Solo dentro de `#if SUITE`:<br>- publicar `LvnZone`, `RangeReset` y `ManualTriggerRequest`;<br>- `ClearLinesOnNewRange` / `ConfirmBeforeClear`;<br>- panel flotante con el botón GATILLO.<br>El Thermal por separado (`ThermalVolumeProfile.dll`) queda **igual**. |
| `SuiteOrderFlowStacker/Atas/CarcasaSuite.cs` | Los 5 indicadores publican sus eventos y su latido en el bus. **Su lógica no cambia.** |
| `herramientas/generar_parametros.py` | Bloques de parámetros del indicador nuevo (sección 15). |
| Documentación de la suite | `PARAMETROS.md` (generado), `DECISIONES.md`, `OPERATIVA.md` (MOD1-MOD4, BFVG, gatillo), `PROGRESO.md`, `LEEME.md`. |

### Eliminar

- **Nada de `NQSessionSweepsPro/`**: sigue igual.
- **En la copia nueva** no entran:
  - el módulo propio de Volume Profile + LVN (`Perfil.cs` / `MotorPerfil`): lo sustituye el Thermal (5.1);
  - la decisión de barrido actual: la clasificación por W1/W3/W5 y el veto del Pine.
- W1/W3/W5 quedan solo como datos (P9) y el Pine como confluencia (P7).

## b) APIs de ATAS

| API | Estado | Si falla |
|---|---|---|
| `OnCalculate`, `GetCandle`, `GetAllPriceLevels` | Comprobada (ATAS real) | — |
| `OnNewTrade(MarketDataArg)` con `AggressorExchangeOrderId` / `ExchangeOrderId` | Comprobada (ATAS real) | — |
| `MarketDepthChanged`, `OnMarketByOrdersChanged`, `SubscribeMarketByOrderData` | Comprobada (ATAS real) | Sin MBO: iceberg PROBABLE (1.3) |
| **Trades del historial**: `RequestForCumulativeTrades(new CumulativeTradesRequest(desde))` → `OnCumulativeTradesResponse(request, IEnumerable<CumulativeTrade>)`; en vivo `OnCumulativeTrade` / `OnUpdateCumulativeTrade`; campos `FirstPrice`, `Lastprice`, `Time`, `Volume`, `Direction`, `Ticks` | Firmas del código oficial (`TapePattern.cs`, `OIAnalyzer.cs`). **// VERIFICAR** cuántos días devuelve el feed y el nombre del precio de cada `Ticks[i]`. | Footprint de 1 min → barras aproximadas con marca **APROX** (4.2) |
| Ratón: `ProcessMouseDown/Move/Up` + de píxel a precio y barra | Comprobada (barra del Thermal) | — |
| Tecla ESC: `ProcessKeyDown(System.Windows.Input.KeyEventArgs)` | Firma del código oficial (`OHLCPlus.cs`). **// VERIFICAR** en ATAS real. | Botón "Cancelar" en el panel y clic derecho |
| Ventanas WPF flotantes desde un indicador | Comprobada (`VentanaReporte`) | `ShowInChartPanel`: panel arrastrable dentro del gráfico |
| Saldo de la cuenta: `TradingManager?.Portfolio` → `Balance` | Del código oficial (`AccountInfoDisplay.cs`). **// VERIFICAR** sin conexión de trading. | `AccountSize` manual (por defecto) |
| Estáticos compartidos entre indicadores de la misma DLL (bus) | **// VERIFICAR**. Esperado: sí. El latido lo demuestra: si no, el panel dice NO CARGADO. | `MemoryMappedFile` con nombre por instrumento; o lectura incremental de los CSV de cada indicador |
| `AddAlert`, correo SMTP | Comprobadas | — |

## c) Comunicación entre indicadores (2.2)

1. Cada indicador de la suite, al publicar un evento, lo copia al bus: `SuiteEventBus.Para(instrumento).Publicar(e)`.
   Cada 2 s envía su latido.
2. Session Sweeps Pro consulta el bus por **ventana anclada al evento** (3.5):
   - tiempo [T0 − `SuitePreSec`, fin de la compuesta];
   - precio [L − margen, extremo + margen].
3. Aplica `Enabled` / `Weight` por fuente (3.3) y deduplica (3.4: ± `DedupTicks`, ± `DedupSec`). El mismo iceberg del
   Iceberg Tracker y del Absorption Master cuenta una vez (T18).
4. El Thermal publica `LvnZone` (AUTO y MANUAL), `RangeReset` y `ManualTriggerRequest`. Session Sweeps Pro responde con
   `ManualTriggerResult`, que el panel Thermal muestra. Si no hay respuesta: "MOTOR NO CARGADO".
5. Si el mismo instrumento está en dos gráficos, sus indicadores publican dos veces el mismo hecho. La deduplicación de
   3.4 lo cuenta una vez.

## d) Riesgos de compatibilidad

1. **"Al recargar el historial debe coincidir con el CSV" (7.7, T16).** Icebergs, stops y sweeps de la suite solo
   existen en vivo (ATAS no guarda MBO en el historial). Recalcular desde el historial no puede reproducir los
   bloques B y C.
   - **Solución propuesta:** los barridos **congelados** del día se restauran desde el CSV (el CSV manda). Solo se
     recalcula lo no congelado.
2. **Coste de cargar trades del historial** (NQ: cientos de miles por día). Solo desde el inicio de la sesión actual,
   en segundo plano, sin bloquear la UI (1.10). Si no llegan: modo APROX.
3. **Barras de volumen vs. velas del gráfico.** El clasificador ya no depende de la temporalidad. Los FVG de 1m/5m y el
   contexto 15m/H1 se construyen por dentro. Se recomienda un gráfico de 1 min, como el NQ actual.
4. **Thermal compartido.** Lo nuevo va en `#if SUITE`; el Thermal por separado y sus 123 pruebas no cambian.
5. **Pruebas.** Las 101 del NQ siguen intactas (no se toca). Las del indicador nuevo se migran
   (Falso → Ruptura, Dudoso → Indeterminado, Real → TrampaB/A/Premium; T31).
6. **Huecos del documento que decidiría yo** (y quedan en DECISIONES.md):
   - "Super big trade" (MOD3) no tiene umbral: `SuperBigTradeMinContracts`, a calibrar.
   - No hay lista de noticias: se reusa `NewsTimes` = 08:30;10:00;14:00 NY con `NewsBlockMin` = 5.
   - `VolumePerBar` de MNQ: parámetro aparte (`VolumePerBarMnq`).
   - `Lado`: el NQ usa Buyside/Sellside (lado de la liquidez) y la suite Compra/Venta (lado de la orden). Se unifican
     en `Comun` con `Signo()` (1.5).
7. **Tamaño.** Es más grande que el NQ actual (~4.500 líneas): se estima 8.000-10.000 líneas más las pruebas. Por eso
   va por subfases S1-S10 (sección 18), cada una compilada, con todas las pruebas en verde y PROGRESO.md al día.

## Preguntas antes de empezar

1. Los tres documentos que menciona (Prompt_Clasificador_Barridos_Premium, Prompt_Integrado_Panel_Modelos_Fase10,
   Prompt_Fase10_BarrasVolumen_VelaCompuesta) **no los tengo**. ¿Trabajo solo con este documento maestro, o los envía?
   Si los envía y no coinciden con el maestro, ¿cuál manda?
2. ¿Session Sweeps Pro **dentro de la DLL de la suite** (recomendado) o en una DLL aparte (`SessionSweepsPro.dll`)? En
   una DLL aparte el bus necesita la alternativa de `MemoryMappedFile`.
3. Ritmo:
   - **(a)** una subfase por mensaje, esperando su aprobación en cada una (lo que pide el documento); o
   - **(b)** aprobar el plan una vez y construir S1-S10 seguidas, cada una compilada, probada y subida a GitHub.
