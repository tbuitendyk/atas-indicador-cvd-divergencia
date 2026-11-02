# Suite Order Flow Stacker — Manual de operativa (NQ / MNQ)

Cómo leer y operar lo que muestra la suite. El manual de uso (instalar, parámetros, alertas) está en
[LEEME.md](LEEME.md). Precios y horas de los ejemplos: inventados, en hora de Nueva York, siguiendo el mismo caso del
documento (el precio barre el mínimo de ayer, un iceberg comprador absorbe y el precio gira al alza).

> **Aviso.** Es una herramienta de lectura del flujo de órdenes, no un sistema automático. La puntuación 0-100 es una
> suma de reglas con pesos, **no una probabilidad**. Mida los resultados con sus propios CSV antes de arriesgar dinero.

Contenido: [1. Leer cada dato](#1-leer-cada-dato-y-cada-estado) · [2. Los setups](#2-los-setups-a-b-c-y-d) ·
[3. Absorción, iceberg y spoofing](#3-absorción-iceberg-de-acumulacióndistribución-y-spoofing) ·
[4. ¿Barrido contra un iceberg?](#4-cómo-saber-si-un-barrido-se-hizo-contra-un-iceberg) ·
[5. Limitaciones](#5-limitaciones-en-sencillo) · [6. Valores por temporalidad](#6-valores-recomendados-por-temporalidad-para-nq) ·
[7. Herramientas de ATAS](#7-herramientas-nativas-de-atas-como-complemento) · [8. Calibración](#8-calibración-con-los-csv)

---

## 1. Leer cada dato y cada estado

### El caso completo, segundo a segundo

```
09:42:05  Zona probable de stops PDL 21398.50 a 2 ticks
09:42:11  STOPS TOMADOS VENTA | 310c | 21398.25-21396.50 | bajo PDL | EVALUANDO
09:42:11  BARRIDO VENTA | 8 niveles | 185c | barrió PDL 21398.50 | EVALUANDO
09:42:14  ICEBERG COMPRA NATIVO | 21396.25 | 180c | 6 recargas | 6:1 | ACTIVO | GIRO
09:42:20  STACKED IMB COMPRA | 21396.50-21397.25 | 4 niv | 340% | NUEVA | con iceberg
09:42:29  BARRIDO CONTRA ICEBERG VENTA | 4 niveles | 96c | frenado en 21396.25
09:42:33  BARRIDO LIQUIDEZ VENTA ... volvió en 22 s      STOPS ... TRAMPA (volvió en 22 s) | IMPULSO ALCISTA
09:42:40  * ICEBERG COMPRA NATIVO | 21396.25 | 520c | 17 recargas | 13:1 | ACTIVO | GIRO
09:42:52  * ICEBERG COMPRA NATIVO | ... | AGUANTANDO (+4 ticks) | GIRO
09:42:52  SEÑAL GIRO LARGO | 90/100 | Setup B | Entrada 21397.25 | SL 21395.50 | TP 21405.00 (VAL)
```

Cómo se lee:

1. El precio llega al **mínimo de ayer** (PDL), donde suele haber stops de los que compraron.
2. Una **cascada de ventas a mercado** se lleva esos stops (310 contratos en milisegundos) y una **orden agresiva
   grande** barre 8 niveles.
3. Justo debajo, en 21396.25, alguien **compra escondido**: se ejecutan 180, luego 520 contratos contra un tamaño
   visible pequeño. Es el **iceberg**, y su sesgo es **GIRO** porque absorbe justo después del barrido.
4. Los compradores agresivos aparecen (**stacked imbalance de compra** sobre el iceberg).
5. Un último intento vendedor **choca con el iceberg** y no pasa.
6. El precio vuelve sobre el PDL: el barrido fue de **LIQUIDEZ** y los stops una **TRAMPA**.
7. El iceberg deja de recargarse y el precio se aleja 4 ticks: **AGUANTANDO**. Ahí sale la **señal**.

### Qué hacer con cada estado de un iceberg

| Estado | Lectura | Qué hacer |
|---|---|---|
| ACTIVO | Alguien defiende el precio **ahora**. Aún puede agotarse. | Observar. No anticiparse. |
| AGUANTANDO | Defendió y el precio se alejó a su favor. | Es la confirmación de los setups A y B. |
| ROTO | Se agotó: el agresor ganó. | El nivel deja de ser soporte o resistencia; es posible la continuación (setup D). |
| CANCELADO | Lo retiraron sin ejecutarse del todo. | No cuenta. Si pasa muchas veces en el mismo sitio, sospeche spoofing (sección 3). |

## 2. Los setups A, B, C y D

**Gestión común a todos:**

- **Entrada** cuando el precio se aleja **4-6 ticks a favor** (EntryConfirmTicks = 4).
- **Stop** a **2-4 ticks más allá** del iceberg o del extremo del barrido (StopBeyondTicks = 3).
- **Objetivo** en el siguiente POC / VAH / VAL / LVN del Thermal Volume Profile.
- **Parcial** (la mitad) en el **primer objetivo** y el **stop a breakeven** para el resto.
- **Sin operar** si el R:R no llega a 1,5.

### Setup B — Barrido + iceberg (GIRO, máxima calidad)

- **Contexto:** el precio barre un nivel relevante: PDH/PDL, ONH/ONL, IBH/IBL o un swing.
- **Confirmación:** en los 30 s siguientes un **iceberg del lado contrario** absorbe al agresor (iceberg de compra tras
  un barrido de venta) y pasa a **AGUANTANDO**. Mejor aún si hay un barrido CONTRA ICEBERG o un stacked imbalance
  encima del iceberg.
- **Ejemplo NQ:** barren el PDL 21398.50. Iceberg de compra en 21396.25 con `*`, que pasa a AGUANTANDO.
  - Entrada 21397.25, SL 21395.50 (3 ticks bajo el iceberg).
  - TP en el VAL, 21405.00. Riesgo 1,75 pts, beneficio 7,75 pts, R:R 4,4.
- **NO operar si:**
  - El iceberg se ROMPE antes de AGUANTANDO.
  - El barrido acaba en RUPTURA y el precio no vuelve.
  - Hay una noticia en ±2 min.

### Setup A — Absorción en nivel (GIRO)

- **Contexto:** el precio llega a un LVN, al VAH o al VAL del perfil.
- **Confirmación:** un iceberg en ese nivel (±4 ticks) que pasa a **AGUANTANDO**.
- **Ejemplo NQ:** iceberg de venta en el VAH 21447.75 que absorbe compras y luego el precio cae 4 ticks.
  - Entrada corta 21446.75, SL 21448.50.
  - TP en el POC.
- **NO operar si** el iceberg va contra una tendencia clara sin barrido previo: la suite lo bloquea
  (*Sin GIRO contra tendencia sin barrido*).

### Setup C — Rechazo de LVN (GIRO)

- **Contexto:** el precio toca un **LVN** (zona de poco volumen, por donde el precio suele pasar rápido o rebotar).
- **Confirmación:** aparece un **stacked imbalance a favor del giro** pegado al LVN y el **delta gira a favor**: tras
  vender en el LVN, los últimos 10 s son compradores.
- **Ejemplo NQ:**
  - Contexto: el LVN 21421.25 se toca desde arriba.
  - Confirmación: un stacked imbalance de compra en 21421.00-21421.75 y el delta gira.
  - Entrada 21422.25, SL 3 ticks bajo la zona.
  - TP en el siguiente nivel.
- **NO operar si** la zona de imbalance es vieja (más de 5 min) o el LVN está en medio de un movimiento muy rápido sin
  pausa.

### Setup D — Iceberg roto (CONTINUIDAD)

- **Contexto:** un iceberg que defendía un nivel se **ROMPE**.
- **Confirmación:** un **barrido de RUPTURA** en la misma dirección: atraviesa el nivel y sigue fuera pasados 30 s.
- **Ejemplo NQ:** el iceberg de compra en 21400.25 se rompe y un barrido de venta se mantiene por debajo.
  - Entrada corta 21399.25 (4 ticks bajo el nivel roto), SL 21401.00 (3 ticks por encima).
  - TP en el siguiente nivel por debajo.
- **NO operar si** el precio vuelve por encima del nivel roto: la ruptura falló.

### Cuándo NO operar (con cualquier setup)

- **±2 minutos de noticias**: 08:30, 10:00 y 14:00 NY por defecto. Añada las del día en *Horas de noticias*.
- Los primeros segundos de la apertura (09:30:00-09:30:30): el libro es caótico.
- Si el **R:R** no llega a 1,5, o con una señal del mismo lado en los últimos 60 s.
- Con el feed desconectado o con retraso: el panel deja de actualizarse.

## 3. Absorción, iceberg de acumulación/distribución y spoofing

| | Qué es | Cómo se ve en la suite |
|---|---|---|
| **Absorción** | Mucho volumen agresivo en un precio que **no se mueve**: alguien pasivo se lo come todo. | Volumen grande en el footprint de ese precio, delta en contra y el precio no avanza. Si se ve recargarse, el Iceberg Tracker lo marca. |
| **Iceberg de acumulación / distribución** | Una orden grande escondida que compra (acumula) o vende (distribuye) poco a poco sin enseñar su tamaño. Puede durar minutos y repetirse en el mismo precio. | ICEBERG con muchas **recargas**, ratio alto y estado ACTIVO o AGUANTANDO; a veces varios en precios cercanos. |
| **Spoofing** | Órdenes grandes **visibles** que se ponen para asustar y **se cancelan** cuando el precio se acerca, sin ejecutarse. | Lo contrario de un iceberg: mucho visible y poco ejecutado. En la suite: CANCELADO, o nada (no llega a iceberg porque no se ejecuta). En el DOM: el nivel grande desaparece al acercarse el precio. |

La diferencia clave: un iceberg **se ejecuta** mucho más de lo que enseña; el spoofing **enseña** mucho más de lo que
se ejecuta.

## 4. Cómo saber si un barrido se hizo contra un iceberg

- El Sweeps Tracker lo dice: **BARRIDO CONTRA ICEBERG ... frenado en {precio}**. El barrido acabó a 2 ticks o menos
  del precio de un iceberg vivo del lado contrario.
- A mano:
  1. Antes del barrido había un ICEBERG ACTIVO en ese precio.
  2. El barrido termina justo ahí.
  3. En el footprint ese precio tiene un volumen enorme con el delta en contra.
  4. El precio no avanza en las velas siguientes.
- Es el mejor contexto para el Setup B: el agresor gastó su munición contra una orden que no se movió.

## 5. Limitaciones, en sencillo

- **Nadie ve un iceberg antes de que empiece a ejecutarse.** La parte escondida aparece al ejecutarse y recargarse.
- **Nadie ve los stops.** Las zonas son sitios donde **suele** haber stops. La cascada se ve cuando ya pasó.
- **Sin MBO, los icebergs y barridos son estimaciones.** El DOM suma todas las órdenes de un precio: un "iceberg
  inferido" puede ser varias órdenes normales que van llegando. El MBO (Rithmic) distingue cada orden.
- **El historial no tiene MBO ni trades.** Al abrir ATAS no hay icebergs pasados; solo los grabados en CSV.
- **La puntuación no es una probabilidad.** Mida el acierto con sus CSV (sección 8).

## 6. Valores recomendados por temporalidad para NQ

Para el **Thermal Volume Profile + LVN**. Son **puntos de partida**: ajústelos mirando el gráfico. Para MNQ use los
mismos (el precio es igual; solo cambia el volumen).

| Temporalidad | Modo del rango | Tamaño de fila (ticks) | Máx. filas | LVN: suavizado (ticks) | LVN: distancia mínima (ticks) | LVN: prominencia mínima (%) | Clúster: hueco (ticks) | Clúster: altura mínima (ticks) |
|---|---|---|---|---|---|---|---|---|
| **1 min** | Sesión 09:30-16:00 | 1 (auto) | 300 | 3 | 8 | 20 | 12 | 20 |
| **5 min** | Sesión o Día | 2 (auto) | 300 | 4 | 12 | 20 | 16 | 24 |
| **15 min** | Día | 4 (auto) | 300 | 6 | 20 | 25 | 24 | 40 |
| **1 hora** | Semana | 8 (auto) | 300 | 8 | 40 | 25 | 40 | 80 |
| **Diario** | Mes | 20 (auto) | 300 | 12 | 80 | 30 | 80 | 160 |

Área de valor: 70 % en todas. Con **Tamaño de fila automático** las filas se agrandan solas para no pasar de 300.

Para la suite, en NQ se recomienda operar las señales en **1 min** (o en un gráfico de ticks), con el perfil de la
sesión. Los demás parámetros de la suite no dependen de la temporalidad: icebergs, stops y barridos usan trades y
tiempo, no velas.

## 7. Herramientas nativas de ATAS como complemento

Úselas para **comprobar** lo que dice la suite y para calibrar. Los nombres pueden variar según la versión de ATAS;
los valores son puntos de partida para NQ (para MNQ, aproximadamente el doble de contratos).

| Herramienta | Configuración sugerida (NQ) | Para qué |
|---|---|---|
| **Smart Tape** | Agrupar trades por agresor / tiempo, filtro ≥ 10 contratos | Ver en directo los barridos y las cascadas; comparar con el Sweeps y el Stops Tracker. |
| **Big Trades** | Volumen mínimo 20-30 (con agregación) | Órdenes agresivas grandes; deben coincidir con los barridos grandes. |
| **DOM Levels** | Filtro ≥ 50-100 contratos | Órdenes grandes visibles. Si desaparecen al acercarse el precio: spoofing (sección 3). |
| **Cluster Search** | Volumen en un precio ≥ 300 en una vela de 1 min, con delta en contra del cierre | Encuentra absorciones; debe coincidir con los icebergs. |
| **Footprint** | Bid x Ask, imbalance 300 %, 3 niveles | Comparar con el Stacked Imbalance Pro (deben coincidir con los mismos ajustes). |
| **Volume Profile** | Mismo rango que el perfil térmico | El POC, el VAH y el VAL deben coincidir con los del Thermal Volume Profile. |
| **Trackers MBO de ATAS** (Icebergs / Stops / Sweeps), si su licencia los tiene | Valores por defecto | Referencia independiente: compare sus icebergs nativos con los NATIVOS de la suite. |

## 8. Calibración con los CSV

Los umbrales de icebergs por sesión son **valores iniciales**. Con 1-2 semanas de grabación se ajustan a su mercado y
a su feed.

### Abrir el CSV en Excel

1. Copie el archivo de `%APPDATA%\ATAS\OrderFlowSuite\{Instrumento}\` a otra carpeta. Si abre el original, ATAS no
   podrá escribir mientras esté abierto.
2. En Excel: **Datos → Obtener datos → Desde archivo → Desde texto/CSV**. Origen: **65001: Unicode (UTF-8)**.
   Delimitador: **Coma**. **Cargar**.
3. Varios días: repita, o junte los CSV en una carpeta y use **Desde carpeta**.

### Quedarse con el estado final de cada evento

Cada evento tiene varias filas: una por cambio de estado (ACTIVO, AGUANTANDO, ROTO...).

1. Añada una columna `Final`. Supongamos que `Id` está en la columna U y `Version` en la V:

   ```
   =SI(CONTAR.SI.CONJUNTO(U:U;U2;V:V;">"&V2)=0;1;0)
   ```

   Si su Excel separa los argumentos con coma, cambie los `;` por `,`.
2. Filtre `Final = 1`.

### Icebergs

1. Filtre `Indicador = Iceberg` y `Final = 1`.
2. **Tabla dinámica**: filas `Sesion`, columnas `Estado`, valores *Cuenta de Id* y *Promedio de Volumen*.
3. Lectura y ajustes:
   - **Muchos ROTO o CANCELADO con poco volumen en una sesión:** el umbral es bajo y entra ruido. Suba el
     *ejecutado mínimo* de esa sesión, por ejemplo al volumen que deja fuera el 25 % más pequeño de los que acabaron
     AGUANTANDO.
   - **Casi no hay icebergs en una sesión con volumen:** baje el *ejecutado mínimo* o *Ejecutado / visible mínimo*.
   - **Alta probabilidad (`*`):** póngala cerca del volumen del 25 % más grande de los AGUANTANDO. Debe marcar
     pocos, pero buenos.
4. Compare los modos: con Rithmic, ¿los SINTÉTICO aguantan tanto como los NATIVO? Si no, desactive *Detectar
   sintéticos* o súbales el umbral.

### Stops y barridos

- **Stops:** tabla dinámica con filas `Contexto` (la zona: PDL, ONH, 21400R...) y columnas `Estado` (TRAMPA o
  CONTINUACIÓN). Las zonas que casi siempre son trampa son las mejores para los giros.
- **Barridos:** igual, con `Contexto` (el nivel) y `Estado` (LIQUIDEZ, RUPTURA, CONTRA ICEBERG).
- **Si salen demasiados**, suba *Cascada mínima* o *Volumen mínimo de la orden*.

### Señales del Absorption Master

Cada fila `Indicador = Master` tiene:

- `Precio` = entrada, `PrecioFin` = objetivo y `StopLoss`.
- `Score` y el setup (`Contexto` = SetupA..D).

Para medirlas:

1. Para cada señal, mire en el gráfico qué tocó primero: el TP o el SL. Apúntelo en una columna `Resultado`
   (TP / SL / BE).
2. Tabla dinámica: filas `Contexto` (setup), valores *Cuenta* y *Resultado*.
3. Ajuste los **pesos** y la **puntuación mínima**:
   - Si las de 65-75 fallan mucho y las de más de 80 aciertan, suba la mínima.
   - Si un setup pierde de forma sistemática, desactívelo (*Setup X* = No).
4. Cambie **un parámetro cada vez** y mida al menos una semana antes del siguiente cambio.
