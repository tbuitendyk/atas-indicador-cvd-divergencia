# Sessions Sweeps Pro (Suite) v3 — Manual de usuario

**Sessions Sweeps Pro** vigila la liquidez de las sesiones, clasifica cada barrido en el tick y arma dos modelos de
entrada: **A (barrido)** y **B (retroceso a LVN / EDGE / FVG)**, con gatillo de order flow. Funciona en NQ, ES y YM (y
sus micros) con un perfil propio por instrumento. Vive en `SuiteOrderFlowStacker.dll` junto a los trackers y el
Thermal; el **NQ Session Sweeps Pro** (`NQSessionSweepsPro.dll`) sigue aparte y no cambia.

> **Las señales y estadísticas son informativas y deben validarse. No son un registro de operaciones reales salvo
> que provengan de ejecuciones confirmadas.** Las puntuaciones son puntos para ordenar, no probabilidades.

Otros documentos: [OPERATIVA.md](OPERATIVA.md) (cómo operar A y B, práctica con LVN y EDGE) ·
[PARAMETROS.md](PARAMETROS.md) (todos los parámetros, generado) · [DECISIONES.md](DECISIONES.md) ·
[PLAN_V3.md](PLAN_V3.md) · [PROGRESO.md](PROGRESO.md) (hecho, pruebas y lo que hay que comprobar en ATAS real) ·
[VALIDACION_TRACKERS.md](VALIDACION_TRACKERS.md) · [PROMPT_V3.md](PROMPT_V3.md) (la especificación).

---

## 1. Instalar y poner en el gráfico

1. Cierre ATAS y ejecute `C:\homs\bic.cmd` (sincroniza GitHub, compila e instala la suite).
2. Gráfico de **1 minuto** (*TimeFrame M1*) de NQ, ES, YM, MNQ, MES o MYM. En otra temporalidad el indicador lo dice
   y no calcula.
3. Agregue **Sessions Sweeps Pro (Suite)**.
4. Agregue el **Thermal Volume Profile + LVN (Suite)** del mismo instrumento (en este gráfico o en otro): es la fuente
   de **LVN y EDGE** y tiene las herramientas PIERNA, LVN, EDGE y GATILLO. Sin él: `SIN FUENTE LVN`.
5. Para los bloques B y C del barrido agregue los trackers del **mismo instrumento** en cualquier gráfico: **Iceberg
   Tracker Pro**, **Stops Tracker Pro**, **Sweeps Tracker Pro**, **Stacked Imbalance Pro** y/o **Absorption Master**.
   Conservan su dibujo original; su panel propio viene apagado (*0. General → Panel propio en el gráfico*): todo se lee en el
   panel de Sessions Sweeps.
6. Opcional: **Sessions Sweeps CVD (Suite)** (subpanel con las velas del CVD y sus divergencias).

Al terminar de cargar el historial se abre el panel flotante. Los indicadores se hablan por dentro de la DLL (bus de
eventos por contrato, versión 3.0.0); si uno es de otra versión, el panel dice `VERSIÓN DISTINTA` y sus eventos no se
usan.

## 2. Instrumento y calibración

- El símbolo del gráfico elige el perfil: **NQ, ES, YM, MNQ, MES, MYM** (grupos *31-36. Perfil …*). Si el proveedor
  usa otro nombre, *30. Instrumentos → Alias de símbolos* (`ENQ=NQ;EP=ES`). Sin reconocer: **INSTRUMENTO NO
  SOPORTADO** (sin señales ni alertas).
- El tick de ATAS se compara con el del contrato (NQ/ES/MNQ/MES 0.25; YM/MYM 1). Si no coincide: **VALOR DE CONTRATO
  NO VALIDADO** y no se calculan contratos ni se mandan órdenes.
- **Calibración:** con las velas de 1 minuto de RTH cargadas (por defecto 20 sesiones) calcula la mediana de volumen y
  de |delta| por minuto (también por franja de 15 min) y el **VolumePerBar AUTO** = mediana × 0,33 (con mínimo y
  máximo). Estados: **CALIBRADO**, **CALIBRACIÓN INSUFICIENTE** (valores PROVISIONAL, en naranja) o **CONFIGURACIÓN
  MANUAL**. Se guarda por familia en `%APPDATA%\ATAS\OrderFlowSuite\SessionsSweeps\perfiles`.
- Los umbrales en contratos de ES, YM y micros son **PROVISIONAL** hasta calibrarlos: nunca se copian de NQ.
- Cargue al menos 20 días de historial en el gráfico para que calibre.

## 3. Configuración por grupos (lo que importa)

| Grupo | Ajuste | Recomendado (NQ, NY) |
|---|---|---|
| 3. Niveles de liquidez | PDH/PDL, días anteriores, PWH/PWL, iguales, máximos/mínimos de sesión; *Ventana de detección* | Todo encendido; ventana **NY** (Premercado, NY AM, NY PM). |
| 16. Barras de volumen y horario | Killzone, *Horas de noticias*, bloqueo por noticia | 09:30-11:30 NY; `08:30;10:00;14:00`; 5 min. |
| 17. Fuentes de la suite | Activado y peso de Iceberg, Stops, Sweeps, Stacked, Imbalance, Absorción | Todos sí, peso 1. |
| 22. Estados y alertas | *Puntos BARRIDO REAL* / *PREMIUM*; *Estado mínimo para alertar* | 70 / 85; REAL. |
| 23. FVG / LVN | FVG 1m y 5m, tamaño mínimo, ventana BFVG, distancia mínima del objetivo | Sí / 4 ticks / 30 min / 8 ticks. |
| 24. Modelos | Modelo A, Modelo B y sus zonas (LVN, EDGE, FVG, EMA, nivel), intentos por zona, TP, salida | Todo sí; 2 intentos; TP 2R; **TP o EMA 15, lo primero**. |
| 25. Riesgo | Monto o porcentaje, riesgo por operación, leer saldo de ATAS | Monto 200 USD (o 0,5 %). |
| 27. CVD | Pivotes macro/micro, reinicio, CVD Trigger Matrix | 10 / 3; 18:00 NY. |
| 28. Panel y dibujo | Panel flotante, panel en el gráfico, cajas, zonas, FVG, LVN, diagnóstico de trackers | Panel flotante sí. |
| 29. CSV | Grabar CSV, restaurar del CSV al recargar | Sí / sí. |
| 30-36. Instrumentos y perfiles | Alias, fracción AUTO, contratos por barra (AUTO / manual), umbrales en contratos y ticks | AUTO; revisar ES/YM tras calibrar. |
| 37. Alertas y canales | ATAS / correo / Telegram por alerta | Ver sección 7. |
| 13. Correo y Telegram | SMTP, destinatarios, contraseña de aplicación, token y chat ID | Ver sección 7. |
| 38. MT5 (opcional) | Modo, mapeo de símbolos, límites | **APAGADO** (ver sección 8). |

Todos los valores con su rango y descripción: [PARAMETROS.md](PARAMETROS.md).

## 4. El panel

Se arrastra a cualquier lugar o monitor y recuerda posición, tamaño, modo, letra, opacidad y **qué recuadros están
plegados** (`%APPDATA%\ATAS\OrderFlowSuite\ventanas`). Botones: Detalle/Resumen, Fijar, A− / A+, Opacidad, Minimizar
y los controles del puente MT5 (**PAUSAR / REANUDAR**, **CERRAR TODO**, **DESHABILITAR / HABILITAR ÓRDENES**). Clic en
el título de un recuadro lo pliega o despliega.

**Cabecera (siempre visible):**

1. **ESTADO:** SIN OPERACIÓN · PENDIENTE DE CUMPLIMIENTO DE BARRIDO DE LIQUIDEZ · BARRIDO DE LIQUIDEZ REAL ·
   BARRIDO DE LIQUIDEZ REAL PREMIUM · RUPTURA / ACEPTACIÓN (contexto).
2. **Gatillo:** GATILLO ALCISTA / BAJISTA CUMPLIDO (solo con vela cerrada y gatillo VÁLIDO) · GATILLO PENDIENTE (y
   qué falta) · `NO SE HA CUMPLIDO — EN CAMINO A LVN 21,412.50 · a 20 ticks / 5 pts`.
3. **Instrumento:** raíz, contrato, CALIBRADO / PROVISIONAL / MANUAL, datos TICKS o APROX, Thermal, trackers
   cargados y estado de MT5.

| Recuadro | Qué dice |
|---|---|
| 1. Instrumento y calibración | Contrato, tick, USD por tick y por punto, VolumePerBar, medianas en contratos, perfil y sesiones. |
| 2. Sesión | Sesión, hora CDMX y NY, killzone, noticias. |
| 3. Contexto | H1, 15m y 5m con flecha; el rompimiento con iceberg roto (contexto, no entrada). |
| 4. Barrido | Nivel, hora del T0 (CDMX), nueva dirección, clasificación y puntos, penetración, volumen más allá en contratos, stops, validación, recuperación 1m/5m (ticks y puntos), lo que falta. |
| 5-8. Iceberg · Stacked / Imbalance · Stops · Sweeps | Estado de cada tracker y su último evento (con Detalle: eventos del día). |
| 9. CVD | CVD y delta en contratos, **minigráfico de velas de CVD con sus divergencias**, CVD Trigger Matrix (macro, micro, absorción, giro) y su último disparo. |
| 10. Modelo A | El setup del barrido y los BFVG. |
| 11. Modelo B | Zona (LVN, EDGE, FVG…), estado, distancia en ticks/puntos, gatillo. |
| 12. LVN / EDGE | Próximo objetivo LVN o EDGE en la dirección, con origen, estado y distancia. |
| 13. Footprint / gatillo | VÁLIDO (vela de arranque o continuidad) · PENDIENTE (lo que falta) · NO EJECUTABLE (motivo). |
| 14. Riesgo | Modo, contratos, entrada, SL, TP, R:R y salida (TP o EMA 15). |
| 15. Alertas | Correo y Telegram configurados y el último envío de cada canal. |
| 16. MT5 | Modo, mapeo, prueba de conversión, operaciones y pérdida del día, registro. |
| 17. Calidad y trazabilidad | Versión, eventos del día, CSV, TICKS/APROX, MBO/INFERIDO y cada fuente. |

**Detalle** añade los bloques A-E con sus topes, penalizaciones, las 7 puertas premium, el desglose punto a punto,
las transiciones y los setups.

## 5. Thermal Volume Profile + LVN (Suite)

Panel flotante del Thermal: **PERFIL**, **RANGO**, **PIERNA** (congela el perfil de una pierna: no suma volumen nuevo;
DESCONGELAR, BORRAR PIERNA), **ZONA**, **LVN** (punto o zona arrastrando), **BORRAR LVN**, **DESHACER**, **EDGE**,
**GATILLO**, **BORRAR GATILLO**, Tipo, Dirección, Fijar, Recalcular y FVG / Interna / Externa de Sessions Sweeps.

- **LVN:** valle entre dos picos del **perfil fino** (1-2 ticks por fila), zona completa a media profundidad,
  puntuación 0-100. PROVISIONAL hasta confirmarse con cierres; luego ACTIVO, PROBADO o ATRAVESADO (con histéresis).
  Sessions Sweeps nunca usa un PROVISIONAL. Un LVN manual se ajusta al valle cercano (se ve el precio original).
- **EDGE:** borde de un HVN (TECHO HVN / PISO HVN) en ticks, con estados y toques. El **dibujo** se configura aparte
  del cálculo (*7. EDGE → Mostrar líneas EDGE*, línea / franja, grosor, discontinua, solo los N más cercanos).
- **Rango nuevo** → borra las líneas LVN (configurable) y avisa a Sessions Sweeps, que cancela los setups que
  dependían solo de ellas.

## 6. Modelos y gatillo

- **Modelo A — barrido:** solo con BARRIDO REAL o PREMIUM. Zona del barrido o BFVG de la pierna de recuperación
  (A - BFVG, A - BFVG+LVN, A - BFVG+EDGE). Stop más allá del extremo con margen.
- **Modelo B — retroceso (mayor prioridad):** a favor del sesgo, a FVG+LVN > FVG+EDGE > LVN > EDGE > FVG > EMA 15 /
  nivel roto; también **B - RETROCESO TRAS ROMPIMIENTO** (retroceso ≤ 61,8 % tras una ruptura con aceptación).
- **Gatillo:** VÁLIDO por **vela de arranque** o **de continuidad** (vela de volumen o compuesta), PENDIENTE con lo que
  falta, NO EJECUTABLE con el motivo. Nunca se da por cumplido antes del cierre de la vela.
- **Salida:** TP a 2R (o al próximo LVN/EDGE) o cierre de 1 m al otro lado de la EMA 15, lo primero. El SL nunca se
  amplía. Detalle en [OPERATIVA.md](OPERATIVA.md).
- **GATILLO manual:** en el Thermal pulse GATILLO, arrastre un recuadro sobre las velas (ESC cancela). Respuesta:
  GATILLO POR VELA DE ARRANQUE / DE CONTINUIDAD, NO EJECUTABLE o [-]. Se graba en el CSV con Origen MANUAL y no alerta.

## 7. Alertas, correo y Telegram

Solo cinco alertas, solo en vivo, una vez por evento y nunca con NOTICIA:

| Alerta | ATAS | Correo | Telegram |
|---|---|---|---|
| BARRIDO DE LIQUIDEZ REAL | sí | no | no |
| BARRIDO REAL PREMIUM (y UPGRADE PREMIUM, sin repetir) | sí | sí | sí |
| RUPTURA / ACEPTACIÓN (contexto) | sí | no | no |
| GATILLO VÁLIDO — Modelo A | sí | sí | sí |
| GATILLO VÁLIDO — Modelo B | sí | sí | sí |

(Valores por defecto de *37. Alertas y canales*; cada casilla se cambia.) El mensaje lleva instrumento, contrato,
dirección, hora CDMX y NY, clasificación, zona, gatillo, entrada, SL, TP, trackers y estado de MT5.

**Correo** (*13. Correo y Telegram*): servidor `smtp.gmail.com`, puerto 587, TLS, *Usuario SMTP* y *Contraseña de
aplicación* (Gmail: 16 letras, con verificación en dos pasos). Destinatarios separados por `;`. **Telegram:** cree un
bot con @BotFather, pegue el token en *Telegram: token del bot* y su *chat ID*. La contraseña y el token se guardan
**cifrados con DPAPI de Windows** (no en los ajustes de ATAS ni en archivos en claro; para quitar el token escriba `BORRAR`).
*Enviar correo de prueba* y *Telegram: enviar prueba* comprueban todo; el resultado sale en el recuadro 15. Los envíos
van por una cola: ATAS nunca espera.

## 8. MT5 (opcional, APAGADO por defecto)

1. Copie `SessionSweeps/MT5/SessionsSweepsBridge.mq5` a MetaEditor, compílelo y póngalo en un gráfico de MT5. En MT5:
   *Herramientas → Opciones → Asesores expertos → Permitir WebRequest para las URL*: `http://127.0.0.1:5005` (el *Puerto local del puente*; // VERIFICAR).
2. En *38. MT5*: modo **SIMULACIÓN** primero (no necesita MT5), luego **DEMO**. **REAL** exige escribir la frase
   `CONFIRMO OPERAR EN REAL`.
3. Mapee cada instrumento (`NQ → USTEC`, etc.), mire la **prueba de conversión** en el recuadro 16 y solo entonces
   marque *mapeo validado*. SL y TP viajan como **distancias** (factor por instrumento).
4. Nunca manda órdenes en el historial o la recarga, sin mapeo validado, con calibración insuficiente, con NOTICIA,
   pausado, con órdenes deshabilitadas, dos veces el mismo evento, con posición abierta en esa dirección o la contraria,
   por debajo del volumen mínimo del bróker, o pasados los límites del día.
5. Las credenciales de MT5 **no** se piden: la cuenta vive en MT5.

## 9. Qué toma en cuenta el barrido de liquidez

1. **Nivel vigilado** (grupo 3): PDH/PDL, días anteriores sin barrer, PWH/PWL, máximos y mínimos iguales, máximos y
   mínimos de sesión (Asia, Londres, Premercado), dentro de la ventana de detección.
2. **T0 en el tick:** el primer trade que pasa el nivel por *Penetración mínima* (NQ 2 ticks). Desde ahí se mide en
   **barras de volumen** (VolumePerBar del instrumento) hasta congelar o vencer (15 barras o 300 s).
3. **Puntos por bloques**, con tope cada uno (total 0-100):

| Bloque | Tope | Qué suma |
|---|---|---|
| A Nivel | 20 | Externo 10 · sesión 6 · swing interno 2; iguales +5; intacto con antigüedad +3; killzone +2. |
| B Toma de stops | 20 | Ráfaga propia o del Sweeps Tracker +6; barra del cruce rápida +4; agresor ≥ 65 % +4; Stops Tracker +4; penetración en rango +2. |
| C Absorción | 25 | Divergencia de delta más allá +6 (+3 con volumen relativo bajo); iceberg AGUANTANDO MBO +6 / inferido +3; Absorption Master +3; F2 freno +4; POC y VA en el tercio del extremo +4; subasta terminada +3; stacked contrario +2. |
| D Recuperación | 25 | Cierre de vuelta +6; F3 rápida +6; giro de delta +5; stacked en la nueva dirección +4; recuperación ≥ 16 ticks sin tocar el extremo +4. |
| E Contexto | 10 | 15m y H1 no en contra +3; divergencia de CVD +3; vuelta al otro lado de la VWAP +2; coincide el Pine +2. Con NOTICIA no suma. |

Penalizaciones (después de los topes): subasta sin terminar −4; barra de arranque lenta −3.

4. **Estados:**
   - **BARRIDO REAL:** recuperó el nivel + hay absorción + arranque (F3 rápida o giro de delta) + toma de stops + ≥ 70.
   - **BARRIDO REAL PREMIUM:** REAL con ≥ 85 y las **7 puertas**: (1) bloque A ≥ 12; (2) ráfaga o stops; (3) iceberg
     aguantando o F2 ≥ 3 barras con POC en el tercio; (4) subasta terminada; (5) recuperación en ≤ 3 barras con F3 y
     giro de delta; (6) recuperación ≥ 16 ticks sin tocar el extremo; (7) killzone, sin noticia y sin 15m en contra.
   - **RUPTURA / ACEPTACIÓN:** no recupera y cumple ≥ 3 de 5: cierres de aceptación, POC más allá, delta sostenido, poca
     absorción (o iceberg roto), retest que aguanta.
   - **SIN LIQ:** penetración ≤ 2 ticks sin toma de stops. **INDETERMINADO:** vence sin cumplir.
5. **Validación:** "Certificada como real por iceberg (MBO)" solo con MBO y el iceberg AGUANTANDO; sin MBO, "Probable:
   iceberg inferido".
6. **Congelación y transiciones:** al congelar, el estado no se repinta. Después solo: REAL → **UPGRADE PREMIUM**
   (≤ 10 barras); REAL/PREMIUM → **FALLIDO** (cierre más allá del extremo + margen); RUPTURA → **RUPTURA FALLIDA**
   (cierre 4 ticks de vuelta en ≤ 5 barras, y nace una evaluación nueva).
7. **Unidades:** volumen y delta en contratos del instrumento; ticks del instrumento. Con NQ los valores de arriba;
   ES y YM usan los suyos.

## 10. CSV y recarga del historial

En `%APPDATA%\ATAS\OrderFlowSuite\SessionSweeps\{Instrumento}\`:

- `yyyy-MM-dd_barridos_v3.csv` — una fila al congelar y en cada transición: medidas, bloques A-E, estado, puertas,
  fuentes, transiciones, y la auditoría v3 (hora ET y CDMX, raíz, contrato, timeframe, tick, USD por tick y punto,
  calibración, versión del perfil, fuente REAL/MBO/APROX, canales, MT5, volumen y delta en contratos, DeltaPct).
- `yyyy-MM-dd_modelos_v3.csv` — entradas, cierres e invalidaciones de A y B (zona, centro, LVN/EDGE/FVG, gatillo,
  entrada, SL, TP, R:R, contratos, riesgo, resultado, MAE/MFE, salida) y los gatillos manuales.

Nunca llevan credenciales. Al recargar, los barridos ya congelados del día **toman su estado del CSV** y nada suena.

## 11. Problemas frecuentes

| Ve | Causa y arreglo |
|---|---|
| `funciona en graficos de 1 minuto` | Cambie el gráfico a M1. |
| `INSTRUMENTO NO SOPORTADO` | Agregue el alias en *30. Instrumentos*. |
| `VALOR DE CONTRATO NO VALIDADO` | El tick de ATAS no es el del contrato: revise el instrumento. |
| `CALIBRACIÓN INSUFICIENTE` / PROVISIONAL | Cargue 20 días o más de historial de 1 minuto. |
| Fuentes `NO CARGADO` / `VERSIÓN DISTINTA` | Falta ese tracker en un gráfico del mismo contrato, o es de otra compilación: reinstale con bic.cmd. |
| `SIN FUENTE LVN` | Agregue el Thermal (Suite) del mismo instrumento. |
| `Datos: APROX` todo el día | El proveedor no dio los trades del historial; lo nuevo en vivo es TICKS. |
| Correo / Telegram `SIN CONFIGURAR` o ERROR | Usuario y contraseña de aplicación, token y chat ID; use las pruebas; mire el recuadro 15 y el registro de ATAS. |
| MT5 `SIN VALIDAR` / `EA SIN CONTACTO` | Revise la prueba de conversión y marque el mapeo; EA en el gráfico de MT5 y WebRequest permitido. |
| No se ve la ventana | Otro monitor o minimizada; o active *Panel dentro del gráfico*. |

## 12. Pruebas

`bash SuiteOrderFlowStacker/pruebas/verificar.sh` compila la DLL contra sustitutos de ATAS con 0 avisos y ejecuta las
comprobaciones (T1-T31 de v2 y las pruebas 1-36 de v3). **No sustituye la prueba en ATAS real** con el feed que se use:
la lista de `// VERIFICAR` está en [PROGRESO.md](PROGRESO.md). Parámetros: `python3
SuiteOrderFlowStacker/herramientas/generar_parametros_v2.py` (también escribe [PARAMETROS.md](PARAMETROS.md)).
