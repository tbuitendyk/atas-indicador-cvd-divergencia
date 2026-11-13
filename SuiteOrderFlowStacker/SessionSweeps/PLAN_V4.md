# Plan v4 — Indicador Global Session Sweep (correcciones del 13-11-2026)

Especificación: [PROMPT_V4.md](PROMPT_V4.md) (texto del propietario, sin cambios). Base: v3 (`main` 8eddad3). Rama
`global-session-sweep-v4`. El propietario delegó las decisiones (como en la v3); las de abajo quedan registradas.

## Lo que se encontró en el código antes de empezar

- El **"Falso" a las pocas velas** no viene del clasificador v3: viene del detector heredado del NQ (cajas del Pine,
  grupo 4), que cierra la zona a las `SweepMarginLength` (5) velas y marca FALSO cuando el precio cierra más allá. Ese
  detector se seguía dibujando junto al de la v3.
- El clasificador v3 evalúa en **barras de volumen** (NQ ≈ 500 contratos, unas 3 por minuto): `MaxEvaluationBars` 15
  y `MaxEvaluationSec` 300 dan unos 5 minutos, y `ReclaimMaxBars` 5 / `CompositeMaxBars` 7 / `F2MaxBarsRetrace` 7
  barras. Una consolidación de 10-12 velas de 1 minuto no cabe: el barrido vence (INDETERMINADO) o sale RUPTURA antes.
- El **panel de la esquina superior derecha** ("H1 … / Asia / Londres / Premercado / Nueva York") es la tabla de
  sesiones heredada (grupo 2, `ShowTable`, encendida por defecto).
- La **ventana flotante desaparece** al hacer clic en el gráfico porque es una ventana independiente sin dueño: al
  activarse ATAS queda detrás. "Fijar" (siempre visible) venía apagado.

## Decisiones

1. **Un solo indicador: "Indicador Global Session Sweep".** La clase `SessionSweepsPro` pasa a llamarse así en ATAS
   (las plantillas guardadas siguen abriendo) y corre por dentro: sesiones y niveles, clasificador y modelos, los
   trackers (Iceberg, Stops, Sweeps, Stacked/Imbalance, Absorción) con su motor de la suite, el **Thermal Volume
   Profile + LVN/EDGE** con sus herramientas y su panel flotante, y el CVD (minigráfico del panel). Cada capa se
   enciende o apaga en sus ajustes. Los indicadores sueltos siguen en la DLL para quien los use; con el Global no
   hacen falta (si se cargan también, sus eventos se deduplican).
2. **El CVD en velas** no puede ir en un subpanel del mismo indicador (en ATAS un indicador dibuja en un solo panel):
   queda en el recuadro CVD del panel flotante. El subpanel "Sessions Sweeps CVD" sigue disponible. // VERIFICAR con
   el propietario si le basta.
3. **Tres modelos** (el propietario los acepta como prioritarios): **M1** barrido (antes A: barrido + BFVG), **M2**
   retroceso a LVN / EDGE / FVG (antes B, también tras rompimiento; las líneas LVN manuales valen para M2), **M3**
   iceberg rebasado con continuidad (antes contexto): señal y gatillo propios; al activarse **invalida los M1 y M2
   pendientes** (configurable: solo los contrarios).
4. **Barrido con consolidación:** ventana de validación de **2 a 14 velas de 1 minuto** (configurable) y hasta 15
   minutos; la consolidación en el extremo se evalúa como **vela compuesta** (volumen total, compras contra ventas,
   POC); **absorción por CVD**: mucho volumen en el extremo con el delta acumulado que gira (de positivo a negativo en
   máximos, de negativo a positivo en mínimos) o diverge. RUPTURA solo si el precio se aleja más del barrido máximo o
   termina la ventana sin recuperar (nunca antes por consolidar). **Confirmación estructural**: BOS a favor del giro +
   FVG a favor + reacción o búsqueda de un LVN → "ESTRUCTURA CONFIRMADA" en el panel.
5. **"Falso" → "ROMPIMIENTO":** en el detector heredado, lo que antes era FALSO (aceptación más allá) se llama
   ROMPIMIENTO, y sus cajas se ocultan por defecto (el Pine sigue sumando en el bloque E).
6. **Líneas de sesión** blancas y sólidas (color, estilo y grosor configurables; opción "original" punteada del
   color de la sesión; se pueden apagar). En NY: desde el último máximo o mínimo reciente hacia la derecha, y se
   detiene al romperse. Al romper una línea de sesión: sonido y aviso arriba a la derecha del gráfico (configurable
   por sesión; NY por defecto).
7. **BOS** de máximos y mínimos de Asia, Londres, Premercado y NY: opción para marcarlos en el gráfico.
8. **FVG solo después de un barrido validado:** opción (encendida por defecto, para quitar ruido).
9. **Sin alertas de trackers** que tapan la vista y el panel de órdenes: el Global no suena ni muestra avisos de
   icebergs, stops o imbalances; los trackers sueltos traen sus alertas apagadas. Solo las alertas del barrido.
10. **Panel**: el orden del documento (cabecera con estado del indicador, activo NQ/MNQ y resumen ejecutivo del
    contexto; contexto 1H/15m/5m; barrido externo con fecha y hora CDMX y el detalle del nivel con precio y hora;
    recuperación; resumen de absorción con el veredicto BARRIDO DE LIQUIDEZ REAL / NO COMPLETADO / ROTO; modelos;
    próximo LVN y/o FVG; gatillo; riesgo; micro-impulso EMA 15; calidad; aviso legal). Los recuadros de los trackers
    pasan al final. La tabla de la esquina se apaga y su información (H1 y estado de cada sesión) entra al panel.
11. **La ventana no desaparece:** queda "Fijada" (siempre visible) por defecto, también en las posiciones ya guardadas
    (una vez); solo se oculta al minimizarla. Se puede desfijar.
12. **"Que los trackers aparezcan como los nativos de ATAS"**: no se sabe qué aspecto concreto pide. No se cambia su
    dibujo hasta que lo aclare (con una captura del indicador nativo que quiere imitar). // PREGUNTAR.
13. **Aviso legal:** se conserva el de la v3 (incluye la frase de la v2 y es el que pidió la especificación v3).

## Subfases

| | Subfase |
|---|---|
| G1 | Especificación y plan (este archivo). |
| G2 | Ventana fija por defecto; tabla de la esquina apagada; ROMPIMIENTO en lugar de FALSO; cajas del Pine ocultas. |
| G3 | Barrido con consolidación (2-14 velas), compuesta con compras/ventas, absorción por CVD, RUPTURA sin prisas, confirmación estructural BOS + FVG + LVN. |
| G4 | Líneas de sesión blancas, línea de NY que se detiene al romperse, alerta de ruptura de línea, BOS de sesión, FVG solo tras barrido validado. |
| G5 | Modelos M1, M2, M3 y sus alertas. |
| G6 | Panel en el orden del documento. |
| G7 | Alertas de trackers fuera (Global y sueltos). |
| G8 | Global: los trackers corren dentro (motor de la suite), con sus ajustes y su dibujo opcional. |
| G9 | Global: el Thermal corre dentro (perfil, LVN, EDGE, PIERNA, herramientas, panel flotante, ratón y teclado). |
| G10 | Nombre "Indicador Global Session Sweep", fuentes duplicadas, CVD. |
| G11 | Documentación, manual en español, pruebas, fusión a `main`. |
