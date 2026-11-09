# Plan v3 — Sessions Sweeps Pro (corrección del 08-11-2026)

Especificación: [PROMPT_V3.md](PROMPT_V3.md) (texto literal del propietario). **El propietario aprobó construirlo
completo sin esperar la aprobación del plan ni de cada subfase** (mensaje del 2026-11-09: "the ai has owner approval.
just make the indicator and the manual"). Rama de trabajo: `sessions-sweeps-pro-v3`; se fusiona a `main` al final.
Base: v2.0 (Session Sweeps Pro (Suite), commit 2031a90), que ya cubre gran parte del clasificador, FVG/BFVG, gatillos,
paneles, CSV y bus. Avance y pendientes: [PROGRESO.md](PROGRESO.md) (sección v3).

## a. Archivos

| Qué | Dónde |
|---|---|
| Nombre visible | `SessionSweepsPro.cs` (DisplayName), `PanelV2.cs`, `VentanaFlotante` títulos, correo, docs |
| Perfiles por instrumento | nuevo `SessionSweeps/Nucleo/Instrumentos.cs` (+ calibración), parámetros generados por instrumento |
| Modelos A / B | `MotorV2.Modelos.cs` (etiquetas y mapa A/B), `Clasificador.cs` (estados REAL / PREMIUM) |
| EDGE en Sessions Sweeps | `Comun/EventosSuite.cs` (`EdgeZone`), `MotorV2.Lvn.cs` (repositorio EDGE), zonas de Modelo B |
| Thermal: perfil fino, LVN entre picos, PIERNA, LVN manual, EDGE | `ThermalVolumeProfile/MotorTermico.cs` (detector nuevo, el heredado queda como opción), `ThermalVolumeProfile.Suite*.cs` |
| Panel principal (cabecera + 17 recuadros) | `PanelV2.cs` → panel v3, `Comun/Panel.cs` (secciones desplegables, minigráfico CVD), `Ui/VentanaFlotante.cs` |
| CVD + Trigger Matrix | `SessionSweeps/Nucleo/Cvd.cs` (divergencias regulares y ocultas, matriz de 4 filtros portada de `Compartido/MotorCvd.cs`) |
| Alertas, correo, Telegram, credenciales | `AlertasV2.cs`, nuevo `Notificaciones.cs`, nuevo `Credenciales.cs` (DPAPI por P/Invoke a crypt32) |
| MT5 | nuevo `SessionSweeps/Nucleo/Mt5.cs` (simulación, órdenes, límites, idempotencia), `SessionSweeps/Atas/Puente*.cs` (TCP localhost), `SessionSweeps/MT5/SessionsSweepsBridge.mq5` |
| Trackers | `CarcasaSuite`/generador: `ShowInfoPanel` (por defecto apagado: el resumen pasa al panel principal); su dibujo no cambia |
| Docs | `LEEME.md`, `OPERATIVA.md`, `PARAMETROS.md`, `DECISIONES.md`, `PROGRESO.md`, nuevo `VALIDACION_TRACKERS.md`, manual en Claude Docs |

## b. Comunicación

El bus estático por DLL (`SuiteEventBus.Para(instrumento)`) ya aísla por instrumento **y contrato** (la clave es el
símbolo completo de ATAS, p. ej. `NQZ6`). Se añaden `EdgeZone`, `InstrumentProfileUpdated` y latido con versión
(`VERSIÓN DISTINTA`). La deduplicación entre fuentes se hace al contar (instrumento, precio, dirección, hora, origen).

## c. APIs de ATAS a verificar (// VERIFICAR)

Estáticos compartidos entre indicadores · ventana WPF · `RequestForCumulativeTrades` · `ProcessKeyDown` ·
`TradingManager.Portfolio.Balance` · conversión de pantalla a precio: ATAS no expone `GetPriceByY` en el código oficial,
así que se **invierte `GetYByPrice`** por búsqueda binaria (respeta la escala real, también la logarítmica) ·
`InstrumentInfo.Instrument` como fuente del símbolo (raíz + vencimiento).

## d. NQ, ES, YM

Raíces NQ, ES, YM, MNQ, MES, MYM con alias configurables. Especificaciones de referencia (CME): NQ 0.25 / 20 USD por
punto / 5 USD por tick; ES 0.25 / 50 / 12.50; YM 1 / 5 / 5; MNQ 0.25 / 2 / 0.50; MES 0.25 / 5 / 1.25; MYM 1 / 0.50 /
0.50. Si el tick de ATAS no coincide: `VALOR DE CONTRATO NO VALIDADO` (sin contratos ni órdenes). Símbolo desconocido:
`INSTRUMENTO NO SOPORTADO — CONFIGURACIÓN MANUAL REQUERIDA` (sin señales).

Los umbrales que dependen de contratos (y los que el prompt lista como "no compartir") son **parámetros propios de cada
raíz**, en grupos separados de ATAS ("Instrumento NQ", "Instrumento ES"…): así se guardan por separado y al abrir ES
solo se usan los de ES. `VolumePerBar` en modo AUTO se deriva de la mediana del volumen de 1 minuto en RTH del propio
instrumento en el historial cargado (con límites); calibración CALIBRADO (>= 20 sesiones) / CALIBRACIÓN INSUFICIENTE
/ CONFIGURACIÓN MANUAL, guardada por raíz en `%APPDATA%\ATAS\OrderFlowSuite\SessionsSweeps\perfiles\`. Los valores
iniciales de ES, YM y micros son PROVISIONALES (no se copian de NQ).

## e. Dibujo original de los trackers

No se toca. Su panel propio pasa a apagado por defecto (`ShowInfoPanel`) y su último evento se ve en el panel principal.
Modo diagnóstico opcional en Sessions Sweeps: marca hueca y distinta en cada evento recibido por el bus.

## f. Paneles

Ventana WPF (ATAS clásico) con cabecera de 3 líneas y 17 recuadros desplegables (estado guardado por instrumento y
temporalidad). Alternativa: el mismo contenido dentro del gráfico (`ShowInChartPanel`). Panel Thermal aparte con
Perfil, Rango, Pierna, Zona, LVN, Borrar LVN, Deshacer, EDGE, Gatillo, Borrar gatillo.

## g. MT5

Apagado por defecto. Modos SIMULACIÓN (dentro del indicador, sin MT5), DEMO y REAL (REAL exige confirmación escrita).
Puente: el indicador escucha en `127.0.0.1:<puerto>` (TCP con HTTP mínimo); un EA de MT5 (`SessionsSweepsBridge.mq5`)
consulta órdenes con `WebRequest` y devuelve informes y la ficha del símbolo (tick, point, digits, volumen mínimo/máximo/
paso, valor por punto). Órdenes por **distancia** (SL/TP en puntos desde el precio de MT5), nunca precios absolutos del
futuro. Idempotencia por EventId. Sin mapeo validado o sin calibración: no hay órdenes. Límites diarios y controles
PAUSAR / CERRAR TODO / DESHABILITAR ÓRDENES. Credenciales: ninguna en el indicador (la cuenta vive en MT5).

## h. Riesgos

Todo lo de c. + `WebRequest` a localhost desde MT5 (alternativa: archivos en la carpeta común de MT5) + Telegram por
HTTPS desde ATAS. Las pruebas usan DLL simuladas: nada se da por probado en ATAS real.

## Decisiones propias (el propietario delegó)

1. Nombre en ATAS **Sessions Sweeps Pro (Suite)** (el documento dice SESSIONS SWEEPS PRO; el mensaje, "(Suits)": se
   escribe como los demás de la suite). Título del panel: SESSIONS SWEEPS PRO. Panel CVD: **Sessions Sweeps CVD
   (Suite)**. Las clases no cambian de nombre, así ATAS conserva las plantillas guardadas (no hace falta migración).
   El NQ Session Sweeps Pro (DLL aparte) no se toca: es otro producto; lo nuevo nunca muestra su nombre.
2. Modelo A = MOD1 + BFVG. Modelo B = MOD2 + MOD4 ("B - RETROCESO TRAS ROMPIMIENTO") + las zonas de la pierna de un
   iceberg roto. El iceberg roto (antes MOD3) es **contexto**, sin señal ni alerta propias.
3. Estados: BARRIDO REAL >= ScoreRealMin (70, el antiguo TRAMPA A); PREMIUM >= ScorePremiumMin (85) + puertas; lo que
   antes era TRAMPA B (55-69) queda INDETERMINADO.
4. Salida: TP a 2R o cierre de 1m al otro lado de la EMA 15, lo primero (por defecto). El SL nunca se amplía.
5. Alertas: solo las 5 de la sección 15. Se retiran las de ruptura en el tick, cuentas atrás e IFVG.
6. CVD: divergencias solo sobre velas de CVD (minigráfico del panel y subpanel), nunca sobre el precio.

## Subfases (orden del prompt: Modelo B, LVN y EDGE antes que Modelo A)

S1 plan · S2 nombre · S3 perfiles e instrumento · S4 parámetros por instrumento y calibración · S5 bus y latidos ·
S6 trackers (panel propio apagado, diagnóstico) · S7 paneles · S8 Thermal perfil fino y LVN entre picos · S9 PIERNA,
LVN manual, rango · S10 EDGE · S11 Modelo A · S12 Modelo B, FVG/BFVG, gatillos · S13 CVD y matriz · S14 alertas,
correo, Telegram · S15 MT5 · S16 CSV, docs, pruebas 1-36, manual, lista para ATAS real.
