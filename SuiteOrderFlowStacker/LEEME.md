# Suite Order Flow Stacker — Manual de usuario

Los indicadores de order flow para ATAS en **una sola DLL** (`SuiteOrderFlowStacker.dll`), pensados para NQ y MNQ:

| # | En ATAS se llama | Qué hace (en una línea) |
|---|---|---|
| 0 | **Thermal Volume Profile + LVN (Suite)** | El perfil térmico con POC/VA, clústeres, EDGE, SPIKE y LVN (también manuales). Es **el mismo** Thermal Volume Profile que ya usa: [ThermalVolumeProfile/LEEME.md](../ThermalVolumeProfile/LEEME.md). |
| 1 | **Stacked Imbalance Pro (Suite)** | Imbalances diagonales en varios niveles seguidos, con el estado de la zona: NUEVA → PROBADA → ROTA. |
| 2 | **Iceberg Tracker Pro (Suite)** | **El principal.** Icebergs nativos o sintéticos (con MBO) o inferidos (sin MBO), con estado y sesgo. |
| 3 | **Stops Tracker Pro (Suite)** | Zonas probables de stops y cascadas de stops ya disparadas: ¿TRAMPA o CONTINUACIÓN? |
| 4 | **Sweeps Tracker Pro (Suite)** | Barridos (una orden agresiva que se come varios niveles): LIQUIDEZ, RUPTURA o CONTRA ICEBERG. |
| 5 | **Absorption Master (Suite)** | **La señal única.** Ejecuta todo lo anterior por dentro y solo avisa cuando hay confluencia (setups A-D). |
| 6 | **Indicador Global Session Sweep** | v4: **un solo indicador** con todo (sesiones y líneas de sesión, barridos con consolidación y CVD, Modelos M1 / M2 / M3, el Thermal + LVN / EDGE y los trackers por dentro, CVD, panel, alertas, Telegram, MT5 opcional). Manual: [SessionSweeps/LEEME.md](SessionSweeps/LEEME.md). |
| 6b | **Sessions Sweeps CVD (Suite)** | Subpanel: CVD en velas con divergencias macro y micro. |

**Principio:** mucho cálculo por dentro, **resultado simple por fuera**. Por defecto cada indicador muestra solo un
panel pequeño con los últimos eventos, **una línea por evento** (el "dato"), y el Absorption Master una flecha.

Otros documentos:

- **[OPERATIVA.md](OPERATIVA.md)** — manual de operativa: cómo operar los setups A-D, qué es absorción, iceberg y
  spoofing, herramientas de ATAS como complemento, valores por temporalidad para NQ y calibración con los CSV.
- **[PARAMETROS.md](PARAMETROS.md)** — tabla de TODOS los parámetros (nombre, tipo, defecto, rango, descripción).
- **[DECISIONES.md](DECISIONES.md)** — las decisiones que tomó la IA donde el documento dejaba algo abierto.
- **[PROGRESO.md](PROGRESO.md)** — lista de lo hecho y lo que hay que comprobar en ATAS.

Contenido: [1. Instalar](#1-instalar) · [2. Antes de empezar: los datos](#2-antes-de-empezar-los-datos) ·
[3. Poner los indicadores](#3-poner-los-indicadores-en-el-gráfico) · [4. Cómo leer el dato](#4-cómo-leer-el-dato) ·
[5. Cada indicador](#5-cada-indicador) · [6. Modos visuales](#6-modos-visuales) · [7. Alertas](#7-alertas) ·
[8. Grabación y eventos de días anteriores](#8-grabación-csv-y-eventos-de-días-anteriores) ·
[9. Problemas frecuentes](#9-problemas-frecuentes) · [10. Lo que NO puede hacer](#10-lo-que-no-puede-hacer) ·
[11. Compilar a mano y pruebas](#11-compilar-a-mano-y-pruebas)

---

## 1. Instalar

**Con el acceso directo del escritorio** (lo normal): cierre ATAS y haga doble clic en el acceso directo a
`C:\homs\bic.cmd`. Sincroniza GitHub, compila lo que cambió e instala las DLL de `instalar.txt`, que ya incluye
`SuiteOrderFlowStacker`. Al terminar debe decir:

```
LISTO: ThermalVolumeProfile, NQSessionSweepsPro, SuiteOrderFlowStacker instalado(s) en ...\ATAS\Indicators
```

Abra ATAS. La DLL nueva es `%APPDATA%\ATAS\Indicators\SuiteOrderFlowStacker.dll` (la del instalador; ver
[homs/LEEME.md](../homs/LEEME.md)).

> El Thermal Volume Profile **por separado** (`ThermalVolumeProfile.dll`) puede seguir instalado: en ATAS son dos
> indicadores distintos ("Thermal Volume Profile (perfil térmico)" y "Thermal Volume Profile + LVN (Suite)") con el
> mismo código. Un cambio en uno se ve en los dos (el instalador compila los dos).

## 2. Antes de empezar: los datos

| Dato | Lo usan | Si falta |
|---|---|---|
| **Velas con volumen por precio** (footprint) | Imbalance, perfil, LVN, contexto | Sin footprint no hay imbalances; el perfil reparte el volumen de cada vela entre su máximo y su mínimo. |
| **Trades en vivo** (lado agresor) | Icebergs, stops, barridos, delta, Master | Sin conexión en vivo no hay icebergs, stops, barridos ni señales (solo los grabados de días anteriores). |
| **DOM** (nivel 2) | Icebergs **inferidos** | Sin DOM y sin MBO no se pueden detectar icebergs. |
| **MBO** (nivel 3, *market by order*) | Icebergs **nativos** y **sintéticos**; barridos por orden agresiva | Se usa el modo **inferido** (sin MBO). El panel lo dice: `Modo: Inferido (sin MBO)`. |

El MBO lo dan feeds como **Rithmic** (Market by Order activado). Con MBO el panel del Iceberg Tracker dice `Modo: MBO`.
Para forzar el modo inferido: parámetro **Datos MBO = Solo inferido**.

> **Importante:** ATAS no guarda MBO ni trades en el historial. Los icebergs, stops, barridos y señales se detectan
> **desde que el indicador está cargado y conectado**. Lo de días anteriores solo se ve si se grabó (sección 8).

## 3. Poner los indicadores en el gráfico

1. Gráfico de NQ o MNQ (cualquier temporalidad: 1 min es lo más cómodo; ver la tabla de
   [OPERATIVA.md](OPERATIVA.md#6-valores-recomendados-por-temporalidad-para-nq)).
2. **Indicadores** (Ctrl+I) → busque **"Suite"** → añada los que quiera. Cada uno funciona **solo**; el Absorption
   Master **no necesita** que los demás estén cargados.
3. Cada panel sale en una esquina distinta para que no se tapen:

   | Indicador | Panel por defecto |
   |---|---|
   | Iceberg Tracker Pro | arriba a la derecha |
   | Stacked Imbalance Pro | arriba a la derecha, 150 px más abajo |
   | Stops Tracker Pro | abajo a la derecha |
   | Sweeps Tracker Pro | abajo a la derecha, 150 px más arriba |
   | Absorption Master | arriba a la izquierda |

   Para moverlos: grupo **0. General** → *Esquina del panel* y *Panel: desplazamiento horizontal / vertical (px)*.

**Combinación recomendada para empezar:** Thermal Volume Profile + LVN (Suite) + Iceberg Tracker Pro + Absorption
Master. Con eso ve el perfil, los icebergs y la señal; los otros tres añaden detalle.

## 4. Cómo leer el dato

Todos los eventos usan el mismo formato de **una línea**:

```
[EVENTO] [LADO] | precio | volumen | métricas | ESTADO | SESGO | hora (Nueva York)
```

- **COMPRA / VENTA** en un iceberg = el lado de la orden **pasiva**: *ICEBERG COMPRA* es una compra escondida en el
  BID que absorbe ventas agresivas; *ICEBERG VENTA* está en el ASK y absorbe compras.
- En stops y barridos, el lado es el del **agresor**: *STOPS TOMADOS VENTA* = ventas a mercado en cascada.
- **`c`** = contratos. **Hora** = hora de Nueva York (con su cambio de horario).
- Un **`*`** al principio = alta probabilidad (supera los umbrales "HighProb" de la sesión). En el documento original
  es una estrella; ATAS dibuja el texto con Arial, que no tiene estrellas ni emojis, así que se usa `*`.
- **`(CSV)`** al principio = evento leído de la grabación (no sucedió en esta sesión de ATAS).
- Color: verde = compra, rojo = venta, dorado = señal, gris = terminado (ROTO, CANCELADO, ROTA) o del CSV.

Ejemplos del formato (de las pruebas, que comprueban el texto de cada dato):

```
* ICEBERG COMPRA INFERIDO | 21396.25 | 430c | 43 recargas | 22:1 | AGUANTANDO (+4 ticks) | GIRO | 09:42:24
STOPS TOMADOS VENTA | 495c | 21398.50-21396.50 | bajo PDL | TRAMPA (volvió en 27 s) | 09:42:11 | IMPULSO ALCISTA
BARRIDO LIQUIDEZ VENTA | 8 niveles | 185c | barrió PDL 21398.50 | volvió en 27 s | 09:42:11
STACKED IMB COMPRA | 100.25-100.75 | 3 niv | 600% | NUEVA | con iceberg | 10:00:00
SEÑAL GIRO LARGO | 75/100 | Setup B | Entrada 21397.25 | SL 21395.50 | TP 21409.00 (VAL) | R:R 6.7 | 09:42:35
```

## 5. Cada indicador

### 5.1 Stacked Imbalance Pro

**Imbalance diagonal** (igual que el footprint de ATAS, pero sin depender de él):

- **Compra:** el Ask de un nivel contra el Bid del nivel de abajo (≥ 300 % por defecto).
- **Venta:** el Bid de un nivel contra el Ask del nivel de arriba.
- **Stacked:** 3 o más niveles seguidos del mismo lado forman una **zona**.

Se calcula con cada **vela cerrada** (no repinta).

| Estado | Significa |
|---|---|
| **NUEVA** | Recién formada, el precio aún no volvió. |
| **PROBADA** | El precio salió, **volvió a la zona y la respetó**. Suena una alerta en el momento en que vuelve (en el tick, sin esperar al cierre). |
| **ROTA** | Una vela **cerró** a 2 ticks o más al otro lado de la zona (MinBreakTicks). |

**"con iceberg"**: el Iceberg Tracker (que corre dentro) detectó un iceberg del mismo lado dentro de la zona: la zona
tiene una orden grande escondida defendiéndola.

### 5.2 Iceberg Tracker Pro (el principal)

Un **iceberg** es una orden límite que enseña poco (el *clip*) y se recarga cada vez que se ejecuta: en ese precio
se **ejecuta mucho más de lo que se ve**, y el precio casi no se mueve.

| Modo | Cómo se detecta | Confianza |
|---|---|---|
| **NATIVO** (MBO) | La **misma orden** (mismo identificador) reaparece con el tamaño visible repuesto y **peor prioridad** después de ejecutarse. | Confirmado |
| **SINTÉTICO** (MBO) | Tras **llenarse del todo** una orden, aparece **otra nueva** en el mismo precio y lado, de tamaño parecido (±20 %), en **menos de 50 ms**. Lo hacen los algoritmos que esconden órdenes grandes. | Probable |
| **INFERIDO** (sin MBO) | El volumen agresivo ejecutado contra un nivel supera **3 veces** el máximo visible en el DOM en ese nivel, sin que el precio se desplace más de 2 ticks. Las recargas se estiman. | Estimado |

**Umbrales por sesión** (NQ; MNQ = la mitad, editable): un iceberg cuenta si en esa sesión llega al ejecutado
mínimo y a las recargas mínimas:

| Sesión (hora NY) | Ejecutado mínimo | Alta probabilidad (`*`) | Recargas mínimas |
|---|---|---|---|
| Asia 18:00-02:00 | 40 | 120 | 3 |
| Londres 02:00-08:30 | 80 | 250 | 4 |
| **Premercado NY** 08:30-09:30 (configurable) | 100 | 300 | 4 |
| Apertura NY 09:30-11:00 | 150 | 400 | 5 |
| Mediodía 11:00-14:00 | 100 | 300 | 4 |
| Cierre 14:00-16:00 | 150 | 400 | 5 |
| Fuera de horario 16:00-18:00 | 40 | 120 | 3 |

El `*` exige además un ratio ejecutado/visible de 5:1 o más.

| Estado | Significa |
|---|---|
| **ACTIVO** | Se está recargando ahora (recarga o ejecución en los últimos 10 s). |
| **AGUANTANDO (+N ticks)** | Dejó de recargarse **y** el precio se alejó 4 ticks o más a su favor: la orden ganó. |
| **ROTO** | El precio atravesó el nivel 2 ticks: la orden se agotó. |
| **CANCELADO** | La orden desapareció sin terminar de ejecutarse (la retiraron). |

| Sesgo | Cuándo |
|---|---|
| **GIRO** | El iceberg está en un extremo (VAL o VAH, un LVN, el mínimo o máximo de la sesión) **o** absorbe justo después (30 s) de un barrido o una toma de stops del lado contrario. Se confirma con AGUANTANDO. |
| **CONTINUIDAD** | A favor de la tendencia (VWAP o EMA) en un retroceso a POC, VWAP o una zona de stacked imbalance del mismo lado. |
| **RUPTURA** | El iceberg está ROTO: continuación probable hacia donde iba el agresor. |
| **NEUTRO** | Ninguno de los anteriores. |

El título del panel dice el modo y la sesión: `ICEBERG TRACKER · Modo: MBO · Sesión: Apertura NY`.

### 5.3 Stops Tracker Pro

Los stops **no se ven** antes de dispararse. El indicador hace dos cosas:

1. **Mapa de zonas PROBABLES** (la primera línea del panel), las más cercanas al precio:
   - PDH / PDL: máximo y mínimo del día anterior; el día va de 18:00 a 17:00 de Nueva York.
   - IBH / IBL: Initial Balance, la primera hora desde las 09:30, cuando termina.
   - ONH / ONL: sesión overnight, de 18:00 a 09:30.
   - Swings sin superar.
   - Números redondos cada 50 puntos.

   Cuando el precio se acerca a una zona, el panel lo avisa en azul: `Zona probable de stops PDL 21398.50 a 3 ticks`.
   Este aviso no suena ni se graba.
2. **Cascada de stops**: una ráfaga de órdenes a mercado en la misma dirección, en menos de 300 ms, que atraviesa
   3 niveles o más y suma 100 contratos o más en RTH (50 fuera), junto a una zona. Dice qué zona se tomó
   ("bajo PDL", "sobre ONH") y, después:
   - **TRAMPA**: el precio vuelve dentro en 60 s o menos. El nuevo impulso va en contra de la cascada
     (`IMPULSO ALCISTA` tras stops de venta).
   - **CONTINUACIÓN**: sigue fuera pasados los 60 s. El impulso va a favor de la cascada.
   - **EVALUANDO**: mientras no se sabe.

### 5.4 Sweeps Tracker Pro

Un **barrido** es UNA orden agresiva grande que el exchange parte en muchos trozos y que se come varios niveles de
golpe (4 niveles = 1 punto de NQ, y 50 contratos o más).

- **Con MBO:** los trozos se juntan por el identificador de la orden agresiva.
- **Sin MBO (inferido):** se juntan los trades del mismo lado en el mismo milisegundo.

| Tipo | Significa |
|---|---|
| **LIQUIDEZ** | Rompió un máximo o mínimo relevante (PDH/PDL, IBH/IBL, ONH/ONL, swing, VAH/VAL) y **volvió dentro** en 30 s o menos: trampa, fue a buscar liquidez. |
| **RUPTURA** | Se mantiene fuera pasados los 30 s. |
| **CONTRA ICEBERG** | Chocó con un iceberg vivo del lado contrario y no avanzó más de 2 ticks: **setup de giro de máxima calidad**. |
| **EVALUANDO** | Mientras no se sabe si es LIQUIDEZ o RUPTURA. |

### 5.5 Absorption Master (la señal única)

Corre por dentro todos los motores (perfil, LVN, imbalance, icebergs, stops, barridos y el delta) y solo da una
**flecha** y **una línea** cuando hay confluencia:

| Setup | Condiciones | Señal |
|---|---|---|
| **A** Absorción en nivel | Iceberg AGUANTANDO en LVN / VAH / VAL | GIRO |
| **B** Barrido + iceberg | Toma de stops o barrido de liquidez + iceberg que absorbe en 30 s o menos | GIRO (máxima calidad) |
| **C** Rechazo de LVN | Precio en un LVN + stacked imbalance a favor del giro + giro del delta | GIRO |
| **D** Iceberg roto | Iceberg ROTO + barrido de ruptura | CONTINUIDAD |

**Puntuación 0-100** (pesos editables; **no es una probabilidad**, es una ponderación de reglas):

| Condición | Puntos |
|---|---|
| Iceberg válido | 30 (+10 si tiene `*`) |
| Precio en LVN / VAH / VAL / POC (±4 ticks) | 20 |
| Barrido de liquidez o stops tomados antes | 20 |
| Stacked imbalance del mismo lado | 15 |
| Giro del delta a favor | 15 |

La señal sale con **65 o más** puntos.

**Gestión sugerida:**

- **Entrada**: 4 ticks a favor del iceberg o nivel.
- **Stop**: 3 ticks más allá del iceberg o del extremo del barrido.
- **Objetivo**: el siguiente POC, VAH, VAL o LVN.
- **Sin señal** en estos casos:
  - El R:R no llega a 1,5.
  - Estamos a ±2 min de una noticia (08:30, 10:00 y 14:00 NY, editables).
  - Han pasado menos de 60 s desde otra señal del mismo lado.
  - Es un GIRO contra la tendencia sin barrido previo.

El panel muestra:

```
ABSORPTION MASTER · Modo: MBO
SEÑAL GIRO LARGO | 75/100 | Setup B
Entrada 21397.25 | SL 21395.50 | TP 21409.00 (VAL)
Riesgo 1.75 pts | Beneficio 11.75 pts | R:R 6.7 | 09:42:35
  OK  Iceberg válido (* alta prob.)  +40
  OK  Barrido / stops previos (PDL)  +20
  NO  Stacked imbalance compra
  OK  Giro del delta a favor  +15
  NO  En nivel de perfil (LVN/VAH/VAL/POC)
  OK  Fuera de blackout de noticias
  OK  R:R >= 1.5
  OK  Sin tendencia en contra (o con barrido previo)
POSIBLE SETUP A CORTO | 50/100 | esperando: Iceberg AGUANTANDO (+4 ticks)
```

- **OK** = se cumple; **NO** = no se cumple; **..** = pendiente. El documento usa ✔ ✖ ⏳, que Arial no tiene.
- La línea azul **POSIBLE SETUP** es lo que se está formando y qué falta, o por qué está bloqueado
  (`bloqueada: R:R 1.2 < 1.5`).

## 6. Modos visuales

Parámetro **Modo visual** (grupo 0. General), en cada indicador:

| Modo | Qué se ve |
|---|---|
| **Mínimo** (por defecto) | Solo el panel de texto. El Absorption Master añade su flecha. |
| **Marcadores** | Además un marcador pequeño de cada evento en su vela y precio: línea del iceberg (`I* 520c`), recuadro de la zona de imbalance, triángulo de la cascada (`S 495c`), barra vertical del barrido (`B 185c`), y entrada / SL / TP de la última señal. |
| **Completo** | Todo dibujado con etiquetas largas: zonas de imbalance rellenas (también las ROTAS), zonas probables de stops (líneas discontinuas), nivel barrido. El Absorption Master dibuja además lo de todos los motores. |

Los eventos se guardan por **hora y precio** (no por número de vela), así que se ven bien en cualquier temporalidad o
tipo de gráfico.

## 7. Alertas

- Usan las alertas de ATAS con el sonido del parámetro **Sonido de la alerta** (por defecto `alert1`).
- **Una sola alerta por evento y por cada cambio de estado**, con el mismo texto del dato. Por ejemplo, un iceberg
  suena al nacer, al pasar a AGUANTANDO, al ganar el `*` o cambiar de sesgo, y al romperse.
- **Nunca al cargar o recalcular el historial**: solo en vivo.
- Cada indicador suena **solo con lo suyo**: el Absorption Master solo con sus señales.
- Para apagarlas: **Alertas = No** en el indicador.

## 8. Grabación (CSV) y eventos de días anteriores

Con **Grabar en CSV = Sí** (por defecto) cada evento y cada cambio de estado se escriben en:

```
%APPDATA%\ATAS\OrderFlowSuite\{Instrumento}\yyyy-MM-dd.csv     (fecha de Nueva York)
```

- Columnas: `TimestampUTC, TimestampNY, Instrumento, Indicador, Tipo, Lado, Precio, PrecioFin, Volumen, Clip,
  Recargas, Ratio, Estado, Sesgo, Sesion, Modo, Score, Contexto`, más `InicioUTC, StopLoss, Id, Version, Texto` para
  volver a cargarlos.
- La escritura no frena la plataforma: se escribe cada 5 s y al quitar el indicador.
- Si el archivo está abierto en Excel, las filas esperan y se reintenta. Mejor abra una **copia** del archivo.

**Cargar eventos grabados = Sí** (por defecto) lee los CSV de los **últimos 5 días** (*Días grabados a cargar*) y
vuelve a dibujar esos eventos en gris con `(CSV)`, sin sonar. Así se ven los icebergs "de la semana pasada" que se
grabaron.

- Se dibujan en los modos **Marcadores** y **Completo**.
- El panel solo lista lo reciente (*Vida de un evento*, 30 min).

Cómo revisarlos en Excel para calibrar: [OPERATIVA.md, sección 8](OPERATIVA.md#8-calibración-con-los-csv).

## 9. Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| No aparece "Suite" en la lista de indicadores | La DLL no está en la carpeta de ATAS o ATAS estaba abierto al instalar. Cierre ATAS, use el acceso directo y vuelva a abrir. |
| El panel dice `(sin eventos recientes)` todo el rato | Normal fuera de horas con poco volumen. Compruebe que el gráfico está **conectado en vivo** (los icebergs, stops y barridos necesitan trades en vivo). |
| `Modo: Inferido (sin MBO)` con Rithmic | El feed no está enviando MBO. Revise que la conexión tiene Market by Order activado y que **Datos MBO** está en Automático. |
| No salen icebergs inferidos | Hace falta el **DOM** (nivel 2) del instrumento; ábralo o actívelo en la conexión. Fuera de RTH los umbrales son más bajos, en la apertura más altos. |
| Demasiados o muy pocos eventos | Calibre los umbrales con los CSV ([OPERATIVA.md](OPERATIVA.md#8-calibración-con-los-csv)). Para MNQ use **Perfil de instrumento = MNQ** (Auto lo hace por el nombre). |
| Los paneles se tapan | Grupo 0. General: cambie la esquina o el desplazamiento. |
| Cambié un parámetro y los icebergs siguen | Correcto: los cambios de los motores en vivo se aplican a lo nuevo sin borrar lo detectado (el historial no lo puede reconstruir). Los de velas (imbalance, swings, IB, tendencia) recalculan. |
| Un error en el panel (texto naranja) | Está también en el registro de ATAS (Logs). Mándelo tal cual. |

## 10. Lo que NO puede hacer

Explicado en sencillo (sección 3 del documento):

- **Un iceberg no se puede conocer antes de empezar a ejecutarse.** La parte escondida no se ve: se descubre al
  ejecutarse y recargarse. Por eso todo iceberg aparece cuando ya está trabajando.
- **Los stops no son visibles antes de dispararse.** Las "zonas de stops" son sitios **probables** (donde la gente
  suele ponerlos), no órdenes vistas. La cascada se detecta cuando ya pasó.
- **Con fiabilidad, icebergs y barridos necesitan MBO.** Sin MBO el modo inferido estima con el DOM, que suma todas las
  órdenes de un precio: es útil, pero menos seguro.
- **ATAS no tiene MBO en el historial.** Los icebergs solo se detectan en vivo. Los de días anteriores solo se ven si se
  grabaron en el CSV.
- **La puntuación 0-100 no es una probabilidad de acierto.** Es una suma de reglas con pesos; para saber cuánto acierta
  hay que medirlo con los CSV.

## 11. Compilar a mano y pruebas

En Visual Studio 2026:

1. Abra `SuiteOrderFlowStacker\SuiteOrderFlowStacker.slnx`.
2. Elija **Release** y **Compilar solución**.
3. La DLL queda en `SuiteOrderFlowStacker\bin\Release\net10.0-windows\SuiteOrderFlowStacker.dll`. Cópiela a la carpeta
   de indicadores de ATAS.

Las referencias a las DLL de ATAS van con *Copy Local = No*: ATAS.Indicators, ATAS.DataFeedsCore, OFT.Rendering,
OFT.Attributes y Utils.Common. Las configura `Directory.Build.props`.

Pruebas sin ATAS (Linux o Windows con el SDK de .NET 10), desde la raíz del repositorio:

```bash
bash SuiteOrderFlowStacker/pruebas/verificar.sh
```

1. Compila la solución contra sustitutos de las DLL de ATAS (0 avisos, 0 errores).
2. Ejecuta 159 comprobaciones con datos sintéticos de trades, DOM y MBO:
   - los motores;
   - los cinco indicadores reales;
   - la historia del documento: barrido del PDL, iceberg comprador, Setup B;
   - el CSV y su recarga;
   - que PARAMETROS.md esté al día.

Para cambiar un parámetro, edite `herramientas/generar_parametros.py` y ejecútelo (genera `Atas/Generado/`). Después:

```bash
ACTUALIZAR_DOCS=1 bash SuiteOrderFlowStacker/pruebas/verificar.sh
```
