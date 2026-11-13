# Indicador Global Session Sweep (v4) — Manual de usuario

**Un solo indicador** que junta todo: sesiones y sus líneas, barridos de liquidez con absorción, los Modelos **M1**,
**M2** y **M3**, el **Thermal Volume Profile + LVN / EDGE**, los trackers (Iceberg, Stops, Sweeps, Stacked /
Imbalance), el CVD, el panel flotante y las alertas (ATAS, correo, Telegram), con un puente MT5 opcional. Funciona en
NQ, ES y YM (y sus micros) con un perfil propio por instrumento. Es el antiguo *Sessions Sweeps Pro (Suite)* con otro
nombre: los gráficos guardados lo abren sin hacer nada.

> **Las señales y estadísticas son informativas y deben validarse. No son un registro de operaciones reales salvo
> que provengan de ejecuciones confirmadas.** Las puntuaciones son puntos para ordenar, no probabilidades.

Otros documentos: [OPERATIVA.md](OPERATIVA.md) (cómo operar M1, M2 y M3) · [PARAMETROS.md](PARAMETROS.md) (los
parámetros del barrido, generado) · [DECISIONES.md](DECISIONES.md) · [PLAN_V4.md](PLAN_V4.md) (lo que se cambió y
por qué) · [PROMPT_V4.md](PROMPT_V4.md) (las correcciones del propietario) · [PROGRESO.md](PROGRESO.md) (pruebas y
lo que hay que comprobar en ATAS real).

---

## 1. Instalar y poner en el gráfico

1. Cierre ATAS y ejecute `C:\homs\bic.cmd` (sincroniza GitHub, compila e instala).
2. Gráfico de **1 minuto** (*TimeFrame M1*) de NQ, ES, YM, MNQ, MES o MYM.
3. Agregue **Indicador Global Session Sweep**. Es todo lo que hace falta.
4. **Quite del gráfico** el *Thermal Volume Profile + LVN (Suite)*, el *Iceberg / Stops / Sweeps Tracker Pro* y el
   *Stacked Imbalance Pro* sueltos si los tenía: ahora corren dentro (con los dos, los LVN y los eventos llegan
   repetidos).
5. Opcional: **Sessions Sweeps CVD (Suite)** si quiere el CVD en velas en un subpanel (en ATAS un indicador dibuja en
   un solo panel; el CVD también está, en pequeño, en el recuadro 8 del panel flotante).

Al terminar de cargar el historial se abren **dos ventanas flotantes**: la del indicador y la del Thermal (con sus
herramientas). Las dos se quedan **siempre visibles**: ya no se esconden al hacer clic en el gráfico; solo se ocultan
al minimizarlas (el botón *Fijado* las desfija).

## 2. Instrumento y calibración

| Instrumento | Tick | USD por tick | USD por punto | Perfil en ATAS |
|---|---|---|---|---|
| NQ | 0.25 | 5 | 20 | 31. Perfil NQ |
| ES | 0.25 | 12.5 | 50 | 32. Perfil ES |
| YM | 1 | 5 | 5 | 33. Perfil YM |
| MNQ | 0.25 | 0.5 | 2 | 34. Perfil MNQ |
| MES | 0.25 | 1.25 | 5 | 35. Perfil MES |
| MYM | 1 | 0.5 | 0.5 | 36. Perfil MYM |

El símbolo elige el perfil (alias en *30. Instrumentos*). Con el tick de ATAS distinto al del contrato: **VALOR DE
CONTRATO NO VALIDADO**. Con 20 sesiones de 1 minuto cargadas calibra volumen y |delta| por minuto y el VolumePerBar
AUTO: **CALIBRADO**; con menos, **PROVISIONAL** (naranja). ES, YM y micros nunca usan los valores de NQ.

## 3. Ajustes por grupos

| Grupo | Qué se ajusta |
|---|---|
| 1. Sesiones | Sesiones; **líneas de máximos y mínimos** (blancas sólidas, color, discontinuas, grosor, NY reciente) y **BOS** por sesión |
| 2. Tabla | La tabla de la esquina (apagada: su información está en el panel) |
| 3. Niveles de liquidez | PDH/PDL, días anteriores, PWH/PWL, iguales, de sesión; ventana de detección (NY) |
| 4. Barridos (Pine) | El detector heredado; sus cajas, apagadas (ahora dicen ROMPIMIENTO en lugar de FALSO) |
| 13. Correo y Telegram | SMTP, destinatarios, contraseña de aplicación, token y chat ID (cifrados) |
| 16-29 | Barras de volumen y horario, fuentes, niveles, stops, absorción, **recuperación y consolidación (21)**, estados, FVG / LVN, **modelos (24)**, riesgo, gatillo manual, CVD, panel y dibujo (28), CSV |
| 30-36 | Instrumentos y perfiles NQ / ES / YM / micros |
| 37. Alertas y canales | ATAS, correo, Telegram por alerta; rupturas de línea por sesión |
| 38. MT5 (opcional) | APAGADO por defecto |
| **39. Indicador global** | Trackers dentro, Thermal dentro, capas de dibujo de los trackers |
| **40-51. Trackers · …** | Los ajustes de Iceberg, Stops, Sweeps y Stacked / Imbalance (los de siempre, con el prefijo *Trk*) |
| **Thermal · 1-12** | Los ajustes del Thermal Volume Profile + LVN / EDGE |

Valores clave (NQ en NY): consolidación **2-14 velas**, recuperación 16 ticks, penetración mínima 2 ticks, barrido
máximo 24 ticks, REAL ≥ 70, PREMIUM ≥ 85, TP 2R o EMA 15, riesgo 200 USD.

## 4. El panel

**Cabecera (siempre visible):**

1. Estado del indicador y activo: `CALIBRADO · ACTIVO · NQ (NQZ6)`, datos TICKS / APROX, Thermal, trackers, MT5.
2. Resumen del contexto: `CONTEXTO: H1 ALCISTA · 15m ▲ · 5m (-)` (flecha verde alcista, roja bajista, (-) en rango),
   sesión y killzone.
3. `ESTADO:` SIN OPERACIÓN · PENDIENTE DE CUMPLIMIENTO · BARRIDO DE LIQUIDEZ REAL / PREMIUM · ROMPIMIENTO.
4. Gatillo: GATILLO ALCISTA / BAJISTA CUMPLIDO · GATILLO PENDIENTE · `NO SE HA CUMPLIDO — EN CAMINO A LVN 21,412.50`.

**Recuadros** (clic en el título para plegar; se recuerdan):

| Recuadro | Qué dice |
|---|---|
| 1. Contexto y estructura | Estructura 1H, alineación 15m / 5m, cada sesión (estado, máximo, mínimo, líneas rotas), hora, killzone, noticias |
| 2. Barrido de liquidez externa y recuperación | Nivel con **fecha y hora exactas CDMX**, nueva dirección, el nivel con su precio y hora, clasificación y puntos, compras / ventas del rango de ruptura, consolidación y CVD, estructura (BOS, FVG, LVN), stops tomados y el Stops Tracker, validación por iceberg, recuperación 1m y 5m |
| 3. Resumen de absorción y modelos | Confluencias (absorción, BOS, FVG, LVN), **recuadro de Iceberg, Stops, Sweeps, Stacked e Imbalance** y la **sumatoria: BARRIDO DE LIQUIDEZ REAL / NO COMPLETADO / ROTO**; M1, M2, M3 y el recuadro fucsia de M3 |
| 4. Próximo LVN objetivo y/o FVG | El próximo LVN / EDGE y el próximo FVG con distancia; "Esperar operación ALCISTA / BAJISTA" |
| 5. Footprint gatillo | VÁLIDO CUMPLIDO (texto blanco con flecha) · PENDIENTE (naranja) · INVÁLIDO (rojo) |
| 6. Plan de riesgo | Monto o porcentaje, entrada, SL, TP, R:R, contratos, riesgo en USD y ticks |
| 7. Gestión de micro-impulso | EMA 15 de 1 minuto con su precio; activación por cierre cruzado o vela de cambio de dirección |
| 8. CVD | Minigráfico de velas de CVD con divergencias, CVD Trigger Matrix |
| 9. Instrumento y calibración | Contrato, valores, VolumePerBar, medianas, perfil |
| 10. Alertas · 11. MT5 · 12. Calidad y trazabilidad | Canales y último envío; estado del puente; versión, CSV, datos y fuentes |
| 13-16. Iceberg · Stacked / Imbalance · Stops · Sweeps | El último evento de cada tracker (al final, como pidió el propietario) |

## 5. En el gráfico

- **Líneas de sesión** blancas y sólidas en el máximo y el mínimo de Asia, Londres, Premercado y NY; la de NY sale del
  **último máximo o mínimo reciente**. Todas **se detienen al romperse**. *1. Sesiones* permite otro color, discontinuas,
  grosor, el modo "Original" (punteadas del color de la sesión) o apagarlas.
- **BOS** (cierre de 1 minuto más allá de una línea de sesión) marcado con una flecha, por sesión.
- Al **romperse una línea**: sonido y un **aviso arriba a la derecha** del gráfico (60 s; *28. Panel y dibujo*).
- **FVG solo después de un barrido validado** (REAL o PREMIUM) y a favor de su giro; los demás se ocultan (opción).
- El perfil térmico con sus LVN y EDGE, y las cajas de barrido y de los modelos por encima de las velas.
- Los trackers con su dibujo original (cada capa en *39. Indicador global*), **sin alertas** ni paneles que tapen la
  vista o el panel de órdenes.

## 6. Thermal (dentro)

Su panel flotante tiene **PERFIL, RANGO, PIERNA, ZONA, LVN, BORRAR LVN, DESHACER, EDGE, GATILLO, BORRAR GATILLO**,
Tipo, Dirección, Fijar, Recalcular y FVG / Interna / Externa. LVN = valle entre picos del perfil fino; EDGE = borde de
un HVN. Las líneas trazadas con el botón LVN valen para el **M2**. Al elegir un **rango nuevo** se borran las líneas
LVN (configurable) y se cancelan los M2 que dependían solo de ellas.

## 7. Modelos

- **M1 — barrido:** con BARRIDO REAL o PREMIUM; zona del barrido o BFVG de la recuperación.
- **M2 — retroceso a LVN / EDGE / FVG:** a favor del sesgo (FVG+LVN > FVG+EDGE > LVN > EDGE > FVG > EMA / nivel) o
  tras un rompimiento con aceptación.
- **M3 — iceberg rebasado con continuidad:** al romperse un iceberg con imbalance, stops y aceptación se activa M3
  (señal propia y aviso **M3 ACTIVADO**); sus retrocesos son "M3 - ROMPIMIENTO CONTINUIDAD". **Invalida los M1 y M2
  pendientes** (o solo los contrarios, con *M3 invalida todos* apagado).

Entrada solo con gatillo VÁLIDO al cierre de la vela; TP a 2R o EMA 15, lo primero. Detalle en [OPERATIVA.md](OPERATIVA.md).

## 8. Alertas, correo y Telegram

Solo en vivo, una vez por evento y nunca con NOTICIA:

| Alerta | ATAS | Correo | Telegram |
|---|---|---|---|
| BARRIDO DE LIQUIDEZ REAL | sí | no | no |
| BARRIDO REAL PREMIUM (y UPGRADE) | sí | sí | sí |
| ROMPIMIENTO / ACEPTACIÓN CONFIRMADO (contexto) | sí | no | no |
| GATILLO VÁLIDO M1 / M2 / M3 | sí | sí | sí |
| M3 ACTIVADO | sí | no | no |
| RUPTURA DE LÍNEA DE SESIÓN (con aviso arriba a la derecha) | sí | no | no |

Correo (Gmail: contraseña de aplicación), Telegram (token del bot y chat ID) en *13. Correo y Telegram*, cifrados con
DPAPI. Las alertas de los trackers (icebergs, stops, imbalances) ya no suenan.

## 9. Qué toma en cuenta el barrido de liquidez

1. **Nivel vigilado** (grupo 3) y **T0 en el tick** (penetración mínima; NQ 2 ticks).
2. **Ventana de consolidación:** el barrido se sigue evaluando **al menos 14 velas de 1 minuto** (*21. Recuperación → Consolidación*).
   En NY el precio puede consolidar 10-12 velas en el extremo antes de girar: eso ya no se descarta como falso.
3. **Vela compuesta del rango de ruptura:** compras contra ventas (contratos), POC y área de valor, subasta.
4. **Absorción por CVD:** con mucho volumen en el extremo (respecto a la mediana de 1 minuto del instrumento), el
   delta acumulado desde T0 **gira** (de positivo a negativo en máximos, de negativo a positivo en mínimos) o
   **diverge** → "Absorción en consolidación" (+5) y, al recuperar el nivel, "Cierre de vuelta tras consolidar" (+4).
5. **Puntos por bloques** A Nivel (20), B Toma de stops (20), C Absorción (25), D Recuperación (25), E Contexto (10);
   penalizaciones; **REAL ≥ 70**; **PREMIUM ≥ 85** con las 7 puertas.
6. **ROMPIMIENTO** (antes "falso"): solo si el precio **se aleja más que el barrido máximo** (NQ 24 ticks) o termina la
   ventana sin recuperar, con aceptación. Mientras consolida cerca del nivel no se llama ruptura.
7. **Confirmación estructural:** BOS a favor del giro + FVG a favor + un LVN como objetivo → **ESTRUCTURA
   CONFIRMADA**.
8. **Sumatoria** en el panel: BARRIDO DE LIQUIDEZ REAL / NO COMPLETADO / ROTO.

## 10. CSV

`%APPDATA%\ATAS\OrderFlowSuite\SessionSweeps\{Instrumento}\yyyy-MM-dd_barridos_v3.csv` y `_modelos_v3.csv` (auditoría
v3) con 8 columnas nuevas en barridos: velas de consolidación, compras, ventas, giro del CVD, BOS, FVG a favor, LVN
objetivo y estructura confirmada. El modelo sale como M1, M2 o M3. Nunca credenciales.

## 11. Problemas frecuentes

| Ve | Causa y arreglo |
|---|---|
| LVN o eventos repetidos | Quite los Thermal / trackers sueltos: ya corren dentro |
| `funciona en graficos de 1 minuto` | Gráfico M1 |
| PROVISIONAL | Cargue 20 días de historial de 1 minuto |
| `INSTRUMENTO NO SOPORTADO` / `VALOR DE CONTRATO NO VALIDADO` | Alias en 30 / revise el instrumento |
| El perfil tapa las velas | Baje *Thermal · 4. Térmico → Opacidad del perfil* (110 por defecto) |
| No quiere las líneas blancas | *1. Sesiones → Líneas de máximos y mínimos → Original* o *Apagadas* |
| Demasiados FVG | *28 → FVG solo después de un barrido validado* (encendido) |
| La ventana no se ve | Está minimizada: restáurela; o active *Panel dentro del gráfico* |

## 12. Pruebas

`bash SuiteOrderFlowStacker/pruebas/verificar.sh` compila la DLL contra sustitutos de ATAS con 0 avisos y ejecuta las
comprobaciones. **No sustituye la prueba en ATAS real**: lista en [PROGRESO.md](PROGRESO.md).
