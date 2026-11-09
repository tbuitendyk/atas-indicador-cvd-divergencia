# Progreso — Session Sweeps Pro v2.0 (rama `session-sweeps-pro`)

Plan y decisiones: [PLAN.md](PLAN.md). Verificación: `bash SuiteOrderFlowStacker/pruebas/verificar.sh` (0 avisos, todas
las comprobaciones).

| Subfase | Estado | Notas |
|---|---|---|
| S1 Common + SuiteEventBus + publicación de los 5 indicadores + latido | **hecha** | `Comun/EventosSuite.cs`. Los 5 publican sus eventos (también el historial) y laten cada 2 s (CARGADO / SIN DATOS / NO CARGADO). 174 comprobaciones. La recarga del CSV cuenta los días desde la última vela del gráfico. |
| S2 Barras de volumen, trades, medianas, compuesta (3 contextos), fases | **hecha** | Motor del NQ copiado a `SessionSweeps/Nucleo` (espacio de nombres propio) sin su perfil LVN ni los Modelos A/B. `BarrasVolumen.cs` (barras de VolumePerBar, reparto, medianas RTH, velocidad 5 s, APROX desde el footprint), `AlmacenTicks` (historial delante + vivo, mismo resultado al recalcular), `Compuesta.cs` (POC, VA, tercios, subasta, stacked contiguo P11, fases F1/F2/F3 y F3 lenta). 193 comprobaciones. |
| S3 Clasificador de barridos (P1-P12, bloques A-E, estados, anti-repintado) | **hecha** | `Clasificador.cs` (bloques A-E con topes, penalizaciones, 7 puertas, 9 estados, congelación y transiciones 7.7), `MotorV2.Clasificador.cs` (T0 en el tick anclado, extremos, evaluación en cada barra de volumen, hijas tras FALLIDO / RUPTURA FALLIDA, recuperación 1m/5m), `Contexto.cs` (H1, 15m, 5m, CVD, VWAP, EMA 15). Pruebas T1-T13, T15, T17-T19 y espejo sellside: 223 comprobaciones. |
| S4 Integración Thermal (LvnRepository, RangeReset, sin LVN interno) | **hecha** | El Thermal (Suite) publica sus LVN (AUTO y MANUAL), BORRADO, RangeReset y su latido (`ThermalVolumeProfile.Suite.cs`, solo con SUITE: el Thermal por separado no cambia, 123 comprobaciones). `ClearLinesOnNewRange` (por defecto sí) y `ConfirmBeforeClear`. `Lvn.cs`: repositorio, próximo objetivo (5.6), mitigación, aviso de zonas borradas. Los LVN del Thermal son objetivos de la proyección. 236 comprobaciones. |
| S5 FvgEngine + BFVG | **hecha** | `Fvg.cs`: FVG de 3 velas en 1m y 5m (ACTIVO, PARCIAL, MITIGADO, INVALIDADO; máximo por lado), tipo y calidad de zona (FVG+LVN > LVN > FVG > nivel), BFVG para TODOS los FVG de la pierna de recuperación (con o sin LVN; BFVG+LVN con +ConfluenceBonus). T22. 244 comprobaciones. |
| S6 TriggerAnalyzer (+ confluencias del anexo) | **hecha** | `Gatillo.cs`: VÁLIDO por ARRANQUE (F1, F2 absorción/compresión/subasta/delta sin progreso, F3 fuera de la zona) o CONTINUIDAD (sesgo 5m/15m, barra rápida con intención, tercio a favor), PENDIENTE con lo que falta, INVÁLIDO / NO EJECUTABLE (zona atravesada, iceberg en contra, stacked en contra, delta en contra con progreso, F3 lenta, ventana, noticia, killzone, R:R, stop). Anexo: imbalance de intención, micro-tendencia, EMA 15, CVD, velas de arranque. Gatillo manual (vela de volumen / compuesta / auto, dirección auto con [-]). 259 comprobaciones. |
| S7 ModelEngine MOD1-MOD4, ConflictResolver, RiskCalculator, micro-impulso | **hecha** | `MotorV2.Modelos.cs`: MOD1 (solo ARRANQUE), BFVG, MOD2 (sesgo, LVN AUTO/MANUAL, FVG, EMA, nivel roto; "MOD2 tras MOD3"), MOD3 (iceberg o super big trade ROTO + imbalance + stops + aceptación; invalida contrarios), MOD4 (ruptura con aceptación, zonas de la pierna, retroceso ≤ 61.8 %, intentos), matriz de conflictos 8.6, una posición por dirección, gestión simulada (TP1, TP2, stop, MAE/MFE) y micro-impulso. `Riesgo.cs`. T20, T21, T23-T27, T29 por el motor real con trades tick a tick. 276 comprobaciones. |
| S8 Panel flotante principal + opciones del gráfico + CVD | **hecha** | `Comun/Panel.cs` (modelo del panel, colocación T30, fábrica), `Ui/VentanaFlotante.cs` (ventana WPF movible a cualquier monitor: Resumen/Detalle, fijar, A−/A+, opacidad, minimizar, guarda posición en %APPDATA%\\ATAS\\OrderFlowSuite\\ventanas), `PanelV2.cs` (secciones 1-8 del anexo y Detalle, aviso legal), carcasa v2 (`SessionSweepsPro.V2.cs`: almacén de trades entre recálculos, bus, LVN vigentes al entrar en vivo, respuesta al GATILLO, latido, recálculo si una fuente carga después; `Historial.cs`: trades acumulados del día; `DibujoV2.cs`: cajas por estado, modelos, FVG, LVN, gatillo manual, divergencias CVD y panel en el gráfico), 116 parámetros v2 generados (`herramientas/generar_parametros_v2.py`), solo gráfico de 1 minuto, indicador aparte **Session Sweeps CVD (Suite)** (velas CVD, divergencias macro y micro). 306 comprobaciones. |
| S9 Panel flotante Thermal + GATILLO + vela compuesta manual | **hecha** | `ThermalVolumeProfile.SuitePanel.cs` (solo SUITE): botón GATILLO en la barra del Thermal y en su panel flotante (PERFIL, RANGO, ZONA LVN, LÍNEAS LVN, GATILLO, Tipo AUTO/VELA/COMPUESTA, Dirección AUTO/LARGO/CORTO, Fijar, Recalcular, Borrar cajas, FVG / Interna / Externa de Session Sweeps por el bus). El recuadro se manda como ManualTriggerRequest; la respuesta se dibuja con flecha verde/roja o [-] y se lee en el panel como vela compuesta (POC, delta, subasta, entrada, SL, R:R, razones). Recuadros en vivo reevaluados cada ManualRefreshSec (mismo Id) hasta Fijar; ESC cancela (ProcessKeyDown). Línea de estado: LVN AUTO/MANUAL, rango (CDMX) y Session Sweeps CONECTADO / SIN DATOS / NO CARGADO. 312 comprobaciones; el Thermal por separado sigue en 123. |
| S10 Alertas, correo, CSV, documentación, lista para ATAS real | **hecha** | `AlertasV2.cs` (política de la sección 13 y correo de cada toma de liquidez externa válida), `CsvV2.cs` (CSV de barridos y de modelos/gatillos por día, escritura asíncrona), `MotorV2.Csv.cs` (recarga = CSV, T16), carcasa (`SessionSweepsPro.Alertas.cs`: solo en vivo, una vez por evento, nunca con NOTICIA; correo a uniontradingacademy@gmail.com por defecto, SMTP solo en ATAS). Documentos: [LEEME.md](LEEME.md), [OPERATIVA.md](OPERATIVA.md), [PARAMETROS.md](PARAMETROS.md) (generado), [DECISIONES.md](DECISIONES.md). T16, T28, T30 y T31 (NQ 101 y Thermal 123 siguen en verde). 338 comprobaciones. |

## // VERIFICAR pendientes (solo en ATAS real)

- Estáticos compartidos entre indicadores de la misma DLL (el bus). El latido lo muestra: si no, el panel dirá NO CARGADO.
- MessageBox de WPF desde el ratón del gráfico (ConfirmBeforeClear del Thermal).
- Ventana flotante: se abre en el hilo de la interfaz de ATAS (si no, en su propio hilo STA). Comprobar que aparece al
  terminar de cargar el historial y que recuerda posición y monitor.
- `RequestForCumulativeTrades` (como TapePattern / DomStrength oficiales): que la respuesta llegue y que el panel pase de
  "APROX" a "TICKS" tras el recálculo único. Sin respuesta, todo sigue funcionando con APROX.
- `TradingManager?.Portfolio.Balance` (como AccountInfoDisplay oficial) para "Leer el saldo de ATAS".
- ESC en el gráfico (`ProcessKeyDown(System.Windows.Input.KeyEventArgs)`, firma WPF de OHLCPlus oficial).
- `CandleDataSeries` / `MaxDelta` / `MinDelta` en el panel CVD (los usa el Indicador Completo CVD, ya compilado en ATAS).

## v3 — Sessions Sweeps Pro (corrección del 08-11-2026)

Plan: [PLAN_V3.md](PLAN_V3.md) · especificación: [PROMPT_V3.md](PROMPT_V3.md) · rama `sessions-sweeps-pro-v3`.

| Subfase | Estado | Notas |
|---|---|---|
| S1 Plan | **hecha** | PLAN_V3.md |
| S2 Nombre visible | **hecha** | En ATAS: **Sessions Sweeps Pro (Suite)** y **Sessions Sweeps CVD (Suite)**; panel SESSIONS SWEEPS PRO; correo, ventana, Thermal y mensajes. Las clases no cambian de nombre: las plantillas guardadas de ATAS siguen funcionando (sin migración). |
| S3 Perfiles e instrumento | **hecha** | `Nucleo/Instrumentos.cs`: NQ, ES, YM, MNQ, MES, MYM (alias configurables, la raíz más larga gana), especificación CME y validación del tick de ATAS (`VALOR DE CONTRATO NO VALIDADO`: sin contratos), `INSTRUMENTO NO SOPORTADO`: setups invalidados y sin alertas. Riesgo con el valor del tick del instrumento ("usar MES/MYM/MNQ"). |
| S4 Parámetros por instrumento y calibración | **hecha** | Grupos 31-36 "Perfil NQ/ES/YM/MNQ/MES/MYM" (23 valores cada uno; ES, YM y micros PROVISIONAL, nunca copiados de NQ). Calibración con las velas de 1 min RTH cargadas: medianas de volumen y \|delta\| (también por franja), VolumePerBar AUTO = mediana × 0,33 con límites; CALIBRADO / CALIBRACIÓN INSUFICIENTE / CONFIGURACIÓN MANUAL; guardada por familia en `%APPDATA%\\ATAS\\OrderFlowSuite\\SessionsSweeps\\perfiles`. Pruebas 1-7, 9, 10, 30, 34. |
| S5 Bus, latidos, EdgeZone | **hecha** | Bus 3.0.0: canal por contrato (ya lo era), `EdgeZone`, `InstrumentProfileUpdated`, LvnZone con puntuación, profundidad, ancho, toque, PROVISIONAL y precio original; latido con versión (`VERSIÓN DISTINTA`: sus eventos no se usan); deduplicación por tipo, precio, dirección y segundo (prueba 27). |
| S6 Trackers: panel propio apagado, diagnóstico, VALIDACION_TRACKERS.md | **hecha** | `ShowInfoPanel` (apagado en Stacked, Iceberg, Stops y Sweeps; encendido en el Master); su dibujo y su algoritmo no cambian. Umbrales en contratos por familia en los trackers (NQ, MNQ, ES, MES, YM, MYM; tablas de icebergs por sesión para ES, MES, YM, MYM; PROVISIONAL), Auto por el símbolo. Diagnóstico de trackers en Sessions Sweeps (rombo hueco fucsia "SSP …"). [VALIDACION_TRACKERS.md](VALIDACION_TRACKERS.md). |
| S7 Paneles (cabecera + 17 recuadros, Thermal) | pendiente | |
| S8 Thermal: perfil fino y LVN entre picos | pendiente | |
| S9 Thermal: PIERNA, LVN manual, rango | pendiente | |
| S10 EDGE | pendiente | |
| S11 Modelo A | pendiente | |
| S12 Modelo B, FVG/BFVG, gatillos | pendiente | |
| S13 CVD y Trigger Matrix | pendiente | |
| S14 Alertas, correo, Telegram | pendiente | |
| S15 MT5 | pendiente | |
| S16 CSV, documentación, pruebas, manual | pendiente | |
