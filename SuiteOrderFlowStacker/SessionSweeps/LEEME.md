# Suite FAST v5 — Manual de usuario

**Tres indicadores independientes, hechos para cargar rápido y sin guardar historiales pesados:**

| Indicador (nombre en ATAS) | Qué hace | Ventana flotante |
|---|---|---|
| **LVN Thermal Profile (FAST)** | Perfil térmico con LVN y EDGE automáticos y manuales; los **guarda** para que el Sweep Pro los siga usando aunque el Thermal esté cerrado o apagado | Herramientas del Thermal |
| **Indicador de Vela Compuesta (FAST)** | Velas volumétricas (**2,300 contratos** de base, por franja horaria), **GATILLO COMP**, **GATILLO VELAS VOLUMÉTRICAS** y **FVG**; umbral **sugerido** con las últimas 20 velas | Botones de gatillos, franja, contratos |
| **Sweep Pro (FAST)** | Barridos de liquidez externa (REAL, REAL PRIME, ROMPIMIENTO, DUDOSO) automáticos y **marcados a mano**, recuadro de **vela compuesta** con flechas, Modelos **M1 / M2 / M3**, líneas de sesión, trackers en tiempo real, CVD, alertas, correo, Telegram, MT5 opcional | El panel de 16 recuadros |

El Sweep Pro (FAST) es el antiguo *Sessions Sweeps Pro* / *Indicador Global Session Sweep* (la misma clase: los
gráficos guardados lo abren). Funciona en NQ, ES y YM (y sus micros) con un perfil propio por instrumento, en
gráficos de **1 minuto o de 5 minutos** (en 5 minutos los datos siguen siendo de 1 minuto: sección 8).

> **Las señales y estadísticas son informativas y deben validarse. No son un registro de operaciones reales salvo
> que provengan de ejecuciones confirmadas.** Las puntuaciones son puntos para ordenar, no probabilidades.

Otros documentos: [OPERATIVA.md](OPERATIVA.md) (cómo operar M1, M2 y M3 y el flujo de confirmación) ·
[PARAMETROS.md](PARAMETROS.md) (los parámetros del barrido, generado) · [DECISIONES.md](DECISIONES.md) ·
[PLAN_V5.md](PLAN_V5.md) (lo que se cambió y por qué) · [PROMPT_V5.md](PROMPT_V5.md) (las correcciones del
propietario) · [PROGRESO.md](PROGRESO.md) (pruebas y lo que hay que comprobar en ATAS real).

---

## 1. Instalar y poner en el gráfico

1. Cierre ATAS y ejecute `C:\homs\bic.cmd` (sincroniza GitHub, compila e instala; el mismo de siempre).
2. Gráfico de **1 minuto** (*TimeFrame M1*) o de **5 minutos** (*M5*) de NQ, ES, YM, MNQ, MES o MYM.
3. Agregue los tres: **LVN Thermal Profile (FAST)**, **Indicador de Vela Compuesta (FAST)** y **Sweep Pro (FAST)**.
   Cada uno funciona solo; juntos se pasan la información (LVN / EDGE, gatillos) por dentro.
4. **Quite** el *Thermal Volume Profile* suelto (de su propia DLL) si lo tenía: no comparte sus LVN con la suite. Los
   trackers (Iceberg, Stops, Sweeps, Stacked / Imbalance) siguen **dentro del Sweep Pro**: no los agregue sueltos.
5. Un gráfico guardado con el *Indicador Global Session Sweep* abre como **Sweep Pro (FAST)**, pero el Thermal ya no
   va dentro: agregue el **LVN Thermal Profile (FAST)** aparte (sus ajustes empiezan con los valores de fábrica).
6. Opcional: **Sessions Sweeps CVD (Suite)** para el CVD en velas en un subpanel (también está, en pequeño, en el
   recuadro 8 del panel).

Al terminar de cargar se abren las ventanas flotantes de cada indicador, **siempre visibles** (solo se ocultan al
minimizarlas; el botón *Fijado* las desfija). Cerrar una ventana no apaga su indicador.

## 2. Por qué ahora carga rápido

| Qué | Antes (v4) | Ahora (v5) | Dónde se cambia |
|---|---|---|---|
| Indicadores | Uno solo con Thermal, trackers y barrido | Tres independientes | — |
| Barrido: footprint, barras de volumen, clasificador, modelos | Con **todo** el historial | Solo los **últimos 2 días operativos** (hoy y la sesión anterior) | *16. Barras de volumen y horario → Días con detalle (carga rápida)* |
| Días anteriores | Igual de pesados | Solo lo barato: máximos y mínimos de sesiones y días, H1, calibración | — |
| Trackers dentro del Sweep Pro | Reconstruían el historial | **Solo en tiempo real** | *40. Trackers · General → Solo tiempo real (carga rápida)* |
| Indicador de Vela Compuesta | — | Velas volumétricas solo del **último día**; los anteriores solo cuentan para el sugerido | *1. Gatillos → Días con velas volumétricas* |

Con historial sintético de 15 días el barrido pasó de 2.7 s a 0.26 s (10 veces menos). Más días con detalle = más
barridos del pasado en el gráfico, pero carga más lenta.

## 3. Instrumento y calibración (Sweep Pro)

| Instrumento | Tick | USD por tick | USD por punto | Perfil en ATAS |
|---|---|---|---|---|
| NQ | 0.25 | 5 | 20 | 31. Perfil NQ |
| ES | 0.25 | 12.5 | 50 | 32. Perfil ES |
| YM | 1 | 5 | 5 | 33. Perfil YM |
| MNQ | 0.25 | 0.5 | 2 | 34. Perfil MNQ |
| MES | 0.25 | 1.25 | 5 | 35. Perfil MES |
| MYM | 1 | 0.5 | 0.5 | 36. Perfil MYM |

El símbolo elige el perfil (alias en *30. Instrumentos*). Con el tick de ATAS distinto al del contrato: **VALOR DE
CONTRATO NO VALIDADO**. Con 20 sesiones cargadas calibra volumen y |delta| por minuto y el VolumePerBar AUTO:
**CALIBRADO**; con menos, **PROVISIONAL** (naranja). ES, YM y micros nunca usan los valores de NQ.

## 4. LVN Thermal Profile (FAST)

Su panel flotante tiene **PERFIL, RANGO, PIERNA, ZONA, LVN, BORRAR LVN, DESHACER, EDGE, GATILLO, BORRAR GATILLO**,
Tipo, Dirección, Fijar, Recalcular y FVG / Interna / Externa. LVN = valle entre picos del perfil fino; EDGE = borde de
un HVN. Sus ajustes van en los grupos *1. Rango del perfil* … *11. Barra de herramientas*.

**LVN y EDGE persistentes:** cada vez que cambian (automáticos o manuales) se guardan en
`%APPDATA%\ATAS\OrderFlowSuite\Thermal\{instrumento}.json`. El Sweep Pro los lee al cargar y los **sigue usando y
dibujando** (objetivos, Modelo 2, validación de gatillos) aunque cierre el panel del Thermal o lo apague. El panel del
Sweep Pro lo dice: *LVN Thermal Profile cerrado: se usan sus LVN / EDGE guardados*. Los borrados no se guardan.

## 5. Indicador de Vela Compuesta (FAST)

Arma **velas volumétricas**: una vela nueva cada N contratos (2,300 de base), con el N de la franja horaria en curso.

**Panel flotante — botones:**

| Botón | Qué hace |
|---|---|
| **GATILLO COMP: SÍ / NO** | Enciende o apaga el gatillo de vela compuesta |
| **GATILLO VELAS VOLUMÉTRICAS: SÍ / NO** | Enciende o apaga el gatillo de una sola vela volumétrica |
| **FVG: SÍ / NO** | Muestra u oculta los FVG |
| **FRANJA ▸ …** | El desplegable: cada clic pasa a la franja siguiente (Asia → Londres → Premercado → NY apertura → NY mañana → NY mediodía → NY tarde) |
| **− 100 / + 100** | Baja o sube los contratos de la franja elegida |
| **USAR SUGERIDO** | Pone en la franja elegida el sugerido (promedio de volumen de sus últimas 20 velas de 1 minuto) |

El panel muestra la franja actual con sus contratos y el **sugerido** (promedio de las últimas 20 velas), la **tabla
de contratos por franja** con el sugerido de cada una (▸ = la del desplegable) y los **últimos gatillos** con su hora
CDMX y sus razones.

| Franja (hora de Nueva York) | Contratos por defecto |
|---|---|
| Asia 18:00-02:00 · Londres 02:00-08:00 · Premercado 08:00-09:30 | 2,300 |
| NY apertura 09:30-10:30 · NY mañana 10:30-12:00 · NY mediodía 12:00-14:00 · NY tarde 14:00-18:00 | 2,300 |

Los contratos de cada franja también están en *2. Contratos por franja* (se guardan con el gráfico).

**Los gatillos** (flecha verde ▲ alcista, roja ▼ bajista, con la etiqueta COMP o VOL en el gráfico):

- **GATILLO COMP (vela compuesta):** las últimas *Velas de la vela compuesta* (4) juntas: POC y área de valor en el
  tercio del extremo (absorción), delta de las anteriores en contra y la última a favor (≥ *Delta % mínimo*, 25 %), y
  cierre en el tercio contrario = arranque. Con stacked imbalance a favor se anota.
- **GATILLO VELAS VOLUMÉTRICAS:** una sola vela de desplazamiento: rango ≥ 1.5 × la mediana, delta ≥ 25 % a favor,
  duración ≤ 0.5 × la mediana (rápida) y cierre en el tercio a favor.

Cada gatillo va al Sweep Pro: lo muestra en el recuadro 5 y, si va a favor de un setup y cerca de su zona, **lo
cuenta como confluencia** del gatillo de entrada.

## 6. Sweep Pro (FAST)

### 6.1 El panel

**Cabecera:** estado y activo (`CALIBRADO · ACTIVO · NQ (NQZ6)`), contexto (`H1 ALCISTA · 15m ▲ · 5m (-)`), `ESTADO:`
(SIN OPERACIÓN · PENDIENTE · BARRIDO DE LIQUIDEZ REAL / **REAL PRIME** · ROMPIMIENTO) y gatillo (CUMPLIDO · PENDIENTE ·
`NO SE HA CUMPLIDO — EN CAMINO A LVN 21,412.50`).

| Recuadro | Qué dice |
|---|---|
| 1. Contexto y estructura | Estructura 1H, alineación 15m / 5m, cada sesión, hora, killzone |
| **2. Barrido de liquidez externa y recuperación** | Los **botones de marcado manual** (6.2), el barrido activo (automático o MANUAL) con fecha y hora CDMX, clasificación y puntos, compras / ventas, consolidación y CVD, estructura, stops, iceberg, recuperación; y la lista de **recuadros manuales** y de **vela compuesta** con su ruptura (▲ / ▼) |
| 3. Resumen de absorción y modelos | Confluencias, trackers y la sumatoria REAL / NO COMPLETADO / ROTO; M1, M2, M3 |
| 4. Próximo LVN objetivo y/o FVG | Próximo LVN / EDGE (vivos o guardados) y FVG con distancia |
| **5. Footprint gatillo** | VÁLIDO CUMPLIDO · PENDIENTE · INVÁLIDO; y el **último gatillo del Indicador de Vela Compuesta** (30 min), con *confirma …* si va a favor de un setup en su zona |
| 6. Plan de riesgo | Monto o porcentaje, entrada, SL, TP, R:R, contratos |
| 7. Micro-impulso · 8. CVD · 9. Instrumento | EMA 15, CVD con divergencias, calibración |
| 10. Alertas · 11. MT5 · 12. Calidad | Canales, puente, versión, datos (y "gráfico de 5 minutos, cálculo con velas de 1 minuto") |
| 13-16. Iceberg · Stacked / Imbalance · Stops · Sweeps | El último evento de cada tracker |

### 6.2 Marcar un barrido a mano (recuadro 2)

Botones dentro del recuadro 2 (en la ventana flotante): **MARCAR BARRIDO REAL**, **MARCAR BARRIDO REAL PRIME**,
**MARCAR ROMPIMIENTO**, **RECUADRO VELA COMPUESTA** y **BORRAR RECUADROS**.

1. Pulse el botón (queda marcado: *arrastre en el gráfico*).
2. Arrastre un recuadro sobre las velas del barrido, del máximo al mínimo que quiera, y suelte.
3. **Barrido Real / Real Prime:** la dirección sale del precio en ese momento (por debajo de la mitad del recuadro =
   se barrió un máximo, giro bajista; por encima = se barrió un mínimo, giro alcista). Queda como **barrido MANUAL**
   con ese estado, pasa a ser la **referencia activa** del panel y **arranca la verificación de los modelos 1, 2 y
   3** como un barrido detectado.
4. **Rompimiento:** la dirección es la del precio respecto al recuadro; arma el M2 tras rompimiento.
5. **Recuadro vela compuesta:** el rango es el máximo y el mínimo de las velas del recuadro. La primera ruptura dibuja
   una **flecha verde** (rompe arriba) o **roja** (rompe abajo) y lo anota en el panel.

Los recuadros se guardan con el gráfico (vuelven al recargar, con su dirección de entonces). **BORRAR RECUADROS** los
quita y cancela los modelos que dependían de un barrido manual.

### 6.3 Automático

Sigue clasificando solo: **BARRIDO REAL**, **BARRIDO REAL PRIME** (antes "Premium"), **ROMPIMIENTO** y **DUDOSO**
(antes "indeterminado"). Detalle en la sección 9.

### 6.4 En el gráfico

- **Zonas de sesión** (Asia, Londres, Premercado, NY mañana y tarde) y sus **líneas** blancas de máximos y mínimos que
  se detienen al romperse; **BOS** con flecha; aviso arriba a la derecha al romperse una línea.
- Cajas de barrido y de los modelos; FVG solo tras un barrido validado (opción).
- **LVN y EDGE** del LVN Thermal Profile (vivos o guardados). EDGE se apaga en *28 → EDGE del Thermal*.
- Recuadros manuales (verde / rojo; PRIME con borde dorado; rompimiento azul) y de vela compuesta (gris) con su flecha.
- Los trackers con su dibujo (cada capa en *39. Trackers dentro*), sin alertas.

## 7. Modelos

- **M1 — barrido:** con BARRIDO REAL o REAL PRIME (automático o MANUAL); zona del barrido o BFVG de la recuperación.
- **M2 — retroceso a LVN / EDGE / FVG:** a favor del sesgo o tras un rompimiento con aceptación (también marcado a
  mano).
- **M3 — iceberg rebasado con continuidad:** invalida los M1 y M2 pendientes.

Entrada solo con gatillo VÁLIDO al cierre de la vela; TP a 2R o EMA 15, lo primero. Detalle y el **flujo de
confirmación** (Thermal + ruptura del rango compuesto + gatillos del Indicador de Vela Compuesta) en
[OPERATIVA.md](OPERATIVA.md).

## 8. Gráfico de 5 minutos

En **M5** el Sweep Pro (y el Indicador de Vela Compuesta) siguen calculando con **velas de 1 minuto**:

- Las velas del **día** salen de los trades (el historial de trades que se pide a ATAS al cargar y los trades en
  vivo): son **exactas**, como en el gráfico de 1 minuto.
- Las de **días anteriores** se aproximan partiendo cada vela de 5 minutos en 5 de 1 minuto (con su footprint en los
  días con detalle). Los máximos y mínimos de sesión son los mismos que con 1 minuto.
- En vivo, cada minuto se cierra con el primer trade del siguiente.
- Se dibujan en el gráfico de 5 minutos, cada cosa en su hora: **zonas de NY mañana y tarde, Asiático, Londres,
  Premercado, los barridos de liquidez, los gatillos**, los modelos y los recuadros.

Recién cargado (antes de que llegue el historial de trades) el día también se aproxima; al llegar, el indicador se
recalcula una vez con todo exacto. Funciona en gráficos de 1 a 15 minutos; los de ticks o volumen no.

## 9. Alertas, correo y Telegram

Solo en vivo, una vez por evento y nunca con NOTICIA:

| Alerta | ATAS | Correo | Telegram |
|---|---|---|---|
| BARRIDO DE LIQUIDEZ REAL | sí | no | no |
| BARRIDO DE LIQUIDEZ REAL PRIME (y UPGRADE PRIME) | sí | sí | sí |
| ROMPIMIENTO / ACEPTACIÓN CONFIRMADO (contexto) | sí | no | no |
| GATILLO VÁLIDO M1 / M2 / M3 | sí | sí | sí |
| M3 ACTIVADO | sí | no | no |
| RUPTURA DE LÍNEA DE SESIÓN (con aviso arriba a la derecha) | sí | no | no |

Marcar un barrido a mano no suena (lo hizo usted); sí suenan los gatillos de los modelos que arma. Correo (Gmail:
contraseña de aplicación) y Telegram (token del bot y chat ID) en *13. Correo y Telegram*, cifrados con DPAPI.

## 10. Qué toma en cuenta el barrido de liquidez

1. **Nivel vigilado** (grupo 3) y **T0 en el tick** (penetración mínima; NQ 2 ticks).
2. **Ventana de consolidación:** al menos 14 velas de 1 minuto (*21. Recuperación → Consolidación*).
3. **Vela compuesta del rango de ruptura:** compras contra ventas, POC y área de valor, subasta.
4. **Absorción por CVD:** con mucho volumen en el extremo, el delta desde T0 gira o diverge → "Absorción en
   consolidación" (+5) y "Cierre de vuelta tras consolidar" (+4).
5. **Puntos por bloques** A Nivel (20), B Toma de stops (20), C Absorción (25), D Recuperación (25), E Contexto (10);
   **REAL ≥ 70**; **REAL PRIME ≥ 85** con las 7 puertas; por debajo, **DUDOSO**.
6. **ROMPIMIENTO:** solo si el precio se aleja más que el barrido máximo (NQ 24 ticks) o termina la ventana sin
   recuperar, con aceptación.
7. **Confirmación estructural:** BOS + FVG a favor + LVN objetivo → **ESTRUCTURA CONFIRMADA**.
8. **Sumatoria** en el panel: BARRIDO DE LIQUIDEZ REAL / NO COMPLETADO / ROTO.

## 11. CSV

`%APPDATA%\ATAS\OrderFlowSuite\SessionSweeps\{Instrumento}\yyyy-MM-dd_barridos_v3.csv` y `_modelos_v3.csv`. Los
barridos manuales salen con origen MANUAL. Nunca credenciales.

## 12. Problemas frecuentes

| Ve | Causa y arreglo |
|---|---|
| Tarda en cargar | Baje *16 → Días con detalle* a 1; deje *Solo tiempo real* encendido en los trackers |
| No hay barridos de hace una semana | Es la carga rápida: suba *Días con detalle* si los necesita |
| `funciona en graficos de 1 a 15 minutos` | Use un gráfico de tiempo (M1 o M5), no de ticks o volumen |
| No hay LVN / EDGE en el Sweep Pro | Abra una vez el LVN Thermal Profile (FAST) en ese instrumento: guarda sus zonas |
| LVN o eventos repetidos | Quite el Thermal suelto y los trackers sueltos |
| Los botones del recuadro 2 no aparecen | Están en la ventana flotante (el panel dentro del gráfico solo muestra el texto) |
| El recuadro manual eligió la dirección contraria | Bórrelo y márquelo de nuevo cuando el precio ya esté del lado del giro |
| PROVISIONAL | Cargue 20 días de historial |
| La ventana no se ve | Está minimizada: restáurela; o active *Panel dentro del gráfico* |

## 13. Pruebas

`bash SuiteOrderFlowStacker/pruebas/verificar.sh` compila la DLL contra sustitutos de ATAS con 0 avisos y ejecuta las
comprobaciones (584). **No sustituye la prueba en ATAS real**: lista en [PROGRESO.md](PROGRESO.md).
