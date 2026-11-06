# Session Sweeps Pro v2.0 (Suite) — Manual de usuario

Es el NQ Session Sweeps Pro **rehecho dentro de la suite**, con otro nombre para que **no sobrescriba** al anterior:
en ATAS se llama **Session Sweeps Pro (Suite)** y vive en `SuiteOrderFlowStacker.dll`. El **NQ Session Sweeps Pro**
(`NQSessionSweepsPro.dll`) sigue igual y se pueden tener los dos.

Qué hace:

1. Vigila la liquidez **externa** (PDH/PDL, días y semana anteriores) e **interna** (sesiones, máximos/mínimos iguales).
2. Detecta el barrido **en el tick** (T0) y lo **clasifica** con barras de volumen y los eventos de la suite:
   TRAMPA B / A / **PREMIUM** (giro), **RUPTURA** (continuación), FALLIDO, RUPTURA FALLIDA, SIN LIQ, INDETERMINADO.
3. Arma los modelos **MOD1** (trampa), **BFVG** (FVG de la pierna de recuperación), **MOD2** (a favor del sesgo),
   **MOD3** (rompimiento con continuidad) y **MOD4** (ruptura y retroceso) y busca el **gatillo** de order flow.
4. Lo muestra en un **panel flotante** que se mueve a cualquier lugar o monitor (Resumen / Detalle).
5. Avisa (sonido y ventana de ATAS) y manda un **correo** en cada toma de liquidez externa válida con entrada, stop y
   objetivos. Graba todo en **CSV** para calibrar.

> **Estadística simulada; no es un registro de operaciones reales.** Las puntuaciones son puntos, no probabilidades.
> Todos los umbrales son valores iniciales para el NQ en la sesión de Nueva York: **calibrar** con el CSV.

Otros documentos: **[OPERATIVA.md](OPERATIVA.md)** (cómo operar MOD1-MOD4 y BFVG) ·
**[PARAMETROS.md](PARAMETROS.md)** (los 116 parámetros nuevos) · **[DECISIONES.md](DECISIONES.md)** (lo que decidió la
IA) · **[PLAN.md](PLAN.md)** · **[PROGRESO.md](PROGRESO.md)** (hecho y lo que hay que comprobar en ATAS).

---

## 1. Instalar

Cierre ATAS y haga doble clic en el acceso directo del escritorio (`C:\homs\bic.cmd`): sincroniza GitHub, compila e
instala `SuiteOrderFlowStacker` (ya está en `instalar.txt`). Al abrir ATAS, en el grupo de indicadores personalizados
aparecen, además de los seis de la suite:

| En ATAS | Para qué |
|---|---|
| **Session Sweeps Pro (Suite)** | El indicador principal (este manual). |
| **Session Sweeps CVD (Suite)** | Subpanel con el CVD en velas y sus divergencias (opcional). |

La ventana flotante es de **ATAS clásico** (Windows, WPF). Si no se abre, active *28. Panel y dibujo → Panel dentro del
gráfico*.

## 2. Ponerlo en el gráfico

1. Gráfico de **NQ (o MNQ) de 1 minuto** (*TimeFrame M1*). En otra temporalidad el indicador lo dice y no calcula.
2. Agregue **Session Sweeps Pro (Suite)**.
3. Agregue el **Thermal Volume Profile + LVN (Suite)** del mismo instrumento (en este gráfico o en otro): es la **única
   fuente de LVN** y tiene el botón **GATILLO**. Sin él, el panel dice `SIN FUENTE LVN` y los modelos usan solo FVG, EMA y
   niveles.
4. Para los bloques B y C del clasificador (stops, icebergs, sweeps, stacked, absorción) agregue los indicadores de la
   suite del **mismo instrumento** en cualquier gráfico: **Iceberg Tracker Pro**, **Stops Tracker Pro**,
   **Sweeps Tracker Pro**, **Stacked Imbalance Pro** y/o **Absorption Master** (que ya los lleva todos dentro). Se
   comunican por dentro de la DLL; el panel dice de cada uno `CARGADO`, `SIN DATOS` o `NO CARGADO`.
5. Opcional: **Session Sweeps CVD (Suite)** en el mismo gráfico.

Al terminar de cargar el historial se abre el panel flotante. Si una fuente de la suite carga **después**, Session
Sweeps recalcula una vez para verla (como mucho 3 veces por instrumento).

**Trades del historial.** Al terminar de cargar, se piden a ATAS los trades del día (desde las 18:00 ET) y se recalcula
una vez con ellos. Mientras tanto (o si el proveedor no los da) las barras de volumen se reconstruyen del footprint de
1 minuto: el panel dice `Datos: APROX`; con los trades, `Datos: TICKS`.

## 3. El panel flotante

Se arrastra por la barra de título a cualquier lugar o monitor y recuerda posición, tamaño, modo, letra y opacidad
(`%APPDATA%\ATAS\OrderFlowSuite\ventanas`). Si quedó fuera de pantalla (se quitó un monitor) vuelve al principal.
Botones: **Detalle/Resumen**, **Fijar** (siempre visible), **A− / A+**, **Opacidad**, **Minimizar**.

| Sección | Qué dice |
|---|---|
| 1. Contexto temporal | H1, 15m y 5m: ALCISTA (verde) / BAJISTA (rojo) / RANGO, con flecha. |
| 2. Barrido de liquidez externa | Hora **CDMX** del barrido, nivel, **nueva dirección** (verde/rojo negrita), clasificación y puntos (PROVISIONAL en naranja mientras evalúa; CONFLICTO PINE; NOTICIA), stops tomados, **validación** y recuperación 1m / 5m (>= 16 ticks). |
| 3. Absorción y modelos | Recuadro con Iceberg, Stops, Sweeps, Stacked e Imbalance (OK verde / parcial naranja / no gris / NO CARGADO / APAGADO); estado de MOD1, BFVG, MOD2, MOD3, MOD4; caja fucsia **MOD3 ROMPIMIENTO CONTINUIDAD** y los contrarios **INVALIDADO POR MOD3**. |
| 4. Próximo LVN objetivo | `Próximo LVN objetivo (alcista): 29,412.50` — "(alcista)/(bajista)" en naranja, el precio verde (largo) o rojo (corto), con origen AUTO/MANUAL y hora. Sin objetivo: `(short)/(long): Sin objetivo LVN` en amarillo. |
| 5. Footprint gatillo | **VÁLIDO** (texto blanco y flecha verde/roja, ARRANQUE o CONTINUIDAD), **PENDIENTE** (naranja, con lo que falta) o **INVÁLIDO** (rojo, con el motivo). |
| 6. Plan de riesgo | Modo MONTO ($) o PORCENTAJE de la cuenta; contratos = floor(riesgo / (ticks de stop × valor del tick)); "RIESGO INSUFICIENTE: usar MNQ". |
| 7. Micro-impulso | EMA 15 de 1 minuto y la gestión tras TP1. |
| 8. Calidad y trazabilidad | Versión, eventos del día, datos TICKS/APROX, MBO/INFERIDO, archivo CSV y estado de cada fuente. |

**Validación del barrido:** "**Certificada como real por iceberg (MBO)**" **solo** con MBO y el iceberg AGUANTANDO; con
el iceberg inferido (sin MBO) dice "**Probable: iceberg inferido**"; sin iceberg, "Sin iceberg".

**Detalle** añade los bloques A-E con sus topes, penalizaciones, las 7 puertas premium, el desglose punto a punto, las
transiciones, las zonas de los modelos y las fuentes usadas.

## 4. En el gráfico

| Dibujo | Color |
|---|---|
| Caja de barrido TRAMPA B / A / PREMIUM | Verde (giro al alza) o rojo (a la baja); PREMIUM con **borde dorado**. |
| RUPTURA | Azul. |
| FALLIDO / RUPTURA FALLIDA | Morado. |
| PROVISIONAL (aún evaluando) | Naranja. |
| SIN LIQ / indeterminado | Gris. |
| Zona MOD1 / BFVG / MOD2 | Verde o rojo; punteada mientras espera el gatillo, con ENTRADA, STOP, TP1, TP2. |
| Zona MOD3 | Fucsia. MOD4: azul claro. |
| FVG (1m y 5m) | Verde/rojo translúcido; los de 5m más intensos y rotulados. |
| LVN del Thermal | Naranja (activos), gris punteado (mitigados). |
| Divergencias del CVD | Tramos verdes (alcistas) o rojos (bajistas) entre pivotes; las micro, punteadas. |

Interruptores en *28. Panel y dibujo* (también FVG, liquidez interna y externa desde el panel del Thermal).

## 5. GATILLO manual (desde el Thermal)

1. En la barra del **Thermal Volume Profile + LVN (Suite)** o en su panel flotante pulse **GATILLO**.
2. Arrastre un recuadro sobre las velas del retroceso (de tiempo y de precios). **ESC** cancela.
3. Session Sweeps analiza las barras de volumen del recuadro como **vela de volumen**, **vela compuesta** o AUTO
   (botón *Tipo*), en la dirección AUTO / LARGO / CORTO (botón *Dirección*), y responde:
   - **GATILLO POR VELA DE ARRANQUE** o **DE CONTINUIDAD** con flecha verde (largo) o roja (corto), entrada, SL y R:R;
   - **NO EJECUTABLE** con lo que falta o el motivo;
   - **[-]** sin direccionalidad clara: se espera nueva confirmación.
4. El panel del Thermal muestra la lectura de la vela compuesta (POC y su tercio, delta que vira, subasta, razones).
   Un recuadro que llega a la vela actual se **reevalúa en vivo** cada 5 s hasta pulsar **Fijar**. **Recalcular** lo
   vuelve a mandar; **Borrar cajas** las quita. El gatillo manual se graba en el CSV con Origen MANUAL y **no alerta**.

El panel del Thermal también tiene PERFIL, RANGO, ZONA LVN, LÍNEAS LVN (las líneas manuales valen para MOD2; al elegir
un rango nuevo se borran) y la línea de estado: LVN AUTO/MANUAL, rango y `Session Sweeps Pro: CONECTADO`.

## 6. CVD

**Session Sweeps CVD (Suite)**: el delta acumulado en velas (reinicio 18:00 NY o 09:30) con divergencias **de máximo a
máximo** (bajista: precio más alto, CVD más bajo) y **de mínimo a mínimo** (alcista). **Macro** con pivotes de 10 velas
a cada lado; **micro** (3 velas) solo dentro del tramo de una macro, para afinar el punto de giro. Sin líneas
verticales. Las mismas divergencias se dibujan sobre el precio en Session Sweeps Pro (*Divergencias del CVD en el
precio*).

## 7. Alertas y correo

Solo en vivo (nunca al cargar o recalcular el historial), una vez por evento y **nunca con NOTICIA** (5 min antes y
después de las horas de *Horas de noticias*, por defecto 08:30;10:00;14:00 NY):

| Alerta (12. Alertas) | Cuándo |
|---|---|
| Alerta de ruptura | El tick que rompe un máximo/mínimo vigilado (informativa). |
| Cuentas atrás 1m y 5m | Antes del cierre de la vela gatillo tras el barrido. |
| TRAMPA / UPGRADE PREMIUM | Al congelar una TRAMPA >= *Estado mínimo para alertar* (TRAMPA A), y en cada UPGRADE PREMIUM. |
| Gatillo VÁLIDO | Entrada de MOD1, BFVG, MOD2, MOD3 o MOD4. |
| MOD3 / RUPTURA (arma MOD4) | Activación de MOD3; ruptura confirmada que arma MOD4. |
| FALLIDO / RUPTURA FALLIDA | Las dos transiciones. |

**Correo** (13. Correo): en cada **toma de liquidez externa válida** (gatillo VÁLIDO sobre PDH/PDL, días o semana
anteriores) con entrada, stop, TP1 (con R:R), TP2, contratos y razones. Destinatario por defecto
`uniontradingacademy@gmail.com`. **Hay que poner el usuario y la contraseña SMTP en ATAS** (Gmail: contraseña de
aplicación de 16 letras con la verificación en dos pasos). Sin ellos no se intenta enviar (queda un aviso en el
registro de ATAS). *Enviar correo de prueba* comprueba la configuración. La contraseña no se guarda en ningún archivo
del proyecto.

## 8. CSV y recarga del historial

En `%APPDATA%\ATAS\OrderFlowSuite\SessionSweeps\{Instrumento}\` (29. CSV):

- `yyyy-MM-dd_barridos.csv` — una fila al congelar cada barrido y otra en cada transición (columnas de la sección 14:
  medidas, bloques A-E, estado, puertas, fuentes, transiciones).
- `yyyy-MM-dd_modelos.csv` — entradas, cierres e invalidaciones de MOD1-MOD4 y BFVG (zona, gatillo, entrada, stop,
  TP1, TP2, contratos, riesgo, resultado, MAE/MFE) y los gatillos manuales (Origen MANUAL).

**Recarga:** icebergs, stops y sweeps de la suite solo existen en vivo, así que recalcular no los reproduce. Por eso,
al recargar, los barridos ya congelados del día **toman su estado del CSV** (*Restaurar los barridos del día desde el
CSV*), y la recarga no hace sonar nada.

## Ajustes heredados del NQ

Los grupos 1-15 son los del NQ Session Sweeps Pro ([NQSessionSweepsPro/LEEME.md](../../NQSessionSweepsPro/LEEME.md)):
sesiones, tabla, niveles de liquidez, barridos del Pine, order flow, H1, proyección, FVG/IFVG, alertas, correo, MSS y
visual. Cambios:

- Sin los grupos 9-11 (Modelo A, Modelo B y LVN/perfil propio): los modelos son MOD1-MOD4 y BFVG, y los LVN vienen del
  Thermal.
- "Margen del stop" de la proyección se llama ahora `ProjectionStopBufferTicks`; el `StopBufferTicks` de v2 está en
  *21. Recuperación*.
- El panel clásico del NQ (*Panel clásico del último barrido*) viene apagado: el de v2 es el panel flotante.
- Solo gráfico de 1 minuto.

## 9. Problemas frecuentes

| Ve | Causa y arreglo |
|---|---|
| `Session Sweeps Pro funciona en graficos de 1 minuto` | Cambie el gráfico a M1. |
| Panel: fuentes `NO CARGADO` | Ese indicador de la suite no está en ningún gráfico del mismo instrumento (o no late: ver PROGRESO.md, VERIFICAR). |
| `SIN FUENTE LVN` | Agregue el Thermal Volume Profile + LVN (Suite) del mismo instrumento. |
| `Datos: APROX` todo el día | El proveedor no respondió a la petición de trades del historial; con datos en vivo, lo nuevo es TICKS. |
| GATILLO: `MOTOR NO CARGADO` | Session Sweeps Pro (Suite) no está en ningún gráfico de ese instrumento. |
| No llega el correo | Usuario y contraseña SMTP en *13. Correo*; pruebe *Enviar correo de prueba*; mire el registro de ATAS. |
| No se ve la ventana | Puede estar en otro monitor o minimizada; o active *Panel dentro del gráfico*. |

## 10. Pruebas

`bash SuiteOrderFlowStacker/pruebas/verificar.sh` (Linux o Windows con el SDK de .NET 10): compila la DLL contra
sustitutos de ATAS con 0 avisos y ejecuta las comprobaciones (T1-T31 del documento maestro y las del anexo). Los
parámetros de v2 se generan con `python3 SuiteOrderFlowStacker/herramientas/generar_parametros_v2.py` (también
escribe [PARAMETROS.md](PARAMETROS.md)).
