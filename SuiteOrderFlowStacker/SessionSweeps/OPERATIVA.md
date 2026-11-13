# Indicador Global Session Sweep v4 — Operativa (M1, M2 y M3)

> **Las señales y estadísticas son informativas y deben validarse. No son un registro de operaciones reales salvo
> que provengan de ejecuciones confirmadas.** El indicador avisa, dibuja y calcula entrada, stop, objetivo y
> contratos; solo opera si se activa el puente MT5 (apagado por defecto). Las puntuaciones no son probabilidades.

Hay **tres modelos operativos** (el propietario los acepta como prioritarios): **M1** barrido, **M2** retroceso a
LVN / EDGE / FVG y **M3** iceberg rebasado con continuidad. La ruptura con aceptación es contexto (arma un M2).

## 1. M1 — barrido de liquidez

1. **Contexto:** sesgo H1 / 15m / 5m (cabecera y recuadro 1).
2. **Barrido** de un nivel externo o de sesión clasificado **BARRIDO REAL** o **PREMIUM** (recuadro 2 y cabecera),
   idealmente certificado por iceberg (MBO) y con **recuperación ≥ 16 ticks** (NQ) en 1m y 5m.
3. **Zona:** la del barrido (del nivel al extremo) — `M1 - BARRIDO` — o un **FVG de la pierna de recuperación** —
   `M1 - BFVG`, `M1 - BFVG+LVN`, `M1 - BFVG+EDGE` (más fuerte con confluencia).
4. **Gatillo VÁLIDO** (recuadro 5) por vela de arranque (el barrido) o arranque / continuidad (BFVG).
5. **Stop** más allá del extremo del barrido + *Margen del stop* del instrumento. **TP** a 2R (o el próximo LVN/EDGE
   con *Objetivo = LVN/EDGE*). **Salida:** TP o cierre de 1 m al otro lado de la EMA 15, lo primero.

FALLIDO (cierre más allá del extremo) invalida los M1 de ese barrido. Un barrido que consolida 10-14 velas en el
extremo con absorción y CVD que gira también es REAL (ver LEEME, sección 9).

## 2. M2 — retroceso a LVN / EDGE / FVG

1. **Sesgo a favor** (H1 o 5m, sin 15m en contra) o una ruptura con aceptación.
2. **Zona**, por prioridad: **FVG+LVN > FVG+EDGE > LVN > EDGE > FVG > EMA 15 / nivel roto**. Cada tipo se puede
   apagar en *24. Modelos*. Solo LVN y EDGE **ACTIVOS o PROBADOS** (nunca PROVISIONAL).
3. **M2 - RETROCESO TRAS ROMPIMIENTO:** tras una RUPTURA / ACEPTACIÓN de liquidez externa, retroceso ≤ 61,8 % de la
   pierna a una zona de la pierna, como mucho 2 intentos y 45 min de espera.
4. Mientras el precio va hacia la zona, la cabecera dice `NO SE HA CUMPLIDO — EN CAMINO A EDGE PISO HVN 21,404.00 · a
   20 ticks / 5 pts`. Al tocarla: GATILLO PENDIENTE (con lo que falta) hasta el cierre de la vela.
5. **Gatillo VÁLIDO** por arranque o continuidad → entrada al cierre de esa vela. Stop más allá de la zona o
   estructura (o del POC de la vela de arranque, *Modo del stop*) + margen; TP 2R; salida TP o EMA 15.
6. Como mucho *Intentos máximos por zona* (2) entradas en la misma zona y dirección. Un rango nuevo del Thermal
   cancela los M2 que dependían solo de sus LVN.

## 3. M3 — iceberg rebasado con continuidad

1. Un **iceberg** (o un super big trade) es **rebasado** con imbalance a favor, toma de stops y aceptación: se activa
   **M3** con su señal (aviso **M3 ACTIVADO** y el recuadro fucsia del panel).
2. Al activarse **invalida los M1 y M2 pendientes** (todos; con *M3 invalida todos* apagado, solo los contrarios).
3. **Zonas:** los FVG, LVN y EDGE de la pierna de la ruptura y los retrocesos a favor de la continuidad:
   "M3 - ROMPIMIENTO CONTINUIDAD".
4. **Gatillo VÁLIDO** como en M2 → entrada; stop y salida igual (TP 2R o EMA 15). Una RUPTURA FALLIDA lo invalida.

## 4. El gatillo de order flow

- **Vela de arranque:** en la zona, freno (F2: absorción, compresión, subasta terminada o delta sin progreso) y
  desplazamiento fuera de la zona (F3) con delta a favor.
- **Vela de continuidad:** a favor del sesgo 5m/15m, barra rápida con intención y cierre en el tercio a favor.
- **Vela compuesta:** varias velas del retroceso juntas como un footprint: POC en el tercio correcto, delta acumulado
  que vira a favor, imbalances de intención.
- **NO EJECUTABLE:** zona atravesada, iceberg o stacked en contra, delta en contra con avance, F3 lenta, NOTICIA,
  R:R menor que el mínimo o stop mayor que el máximo del instrumento.
- **Confluencias:** imbalance de intención, ruptura de la micro-tendencia del retroceso, EMA 15, CVD Trigger Matrix
  (confluencia, nunca una operación por sí sola).

## 5. Práctica con LVN y EDGE (Thermal)

1. **RANGO:** elija el rango del perfil (la sesión o el impulso que importa). El Thermal detecta los LVN como **valles
   entre picos** del perfil fino y los EDGE como **bordes de HVN**.
2. **PIERNA:** marque la pierna del impulso para ver su perfil congelado (no suma el volumen nuevo). Los LVN y EDGE de
   esa pierna son las zonas de retroceso típicas del M2. DESCONGELAR o BORRAR PIERNA al terminar.
3. **Espere la confirmación:** un LVN nuevo es PROVISIONAL hasta que cierran velas fuera de él; el indicador solo
   usa ACTIVO o PROBADO. ATRAVESADO = ya no es zona.
4. **LVN manual:** pulse LVN y haga clic (punto) o arrastre (zona); se ajusta al valle cercano. BORRAR LVN quita uno
   visible; DESHACER el último.
5. **EDGE manual:** pulse EDGE y haga clic; el lado (TECHO / PISO HVN) lo decide el perfil. Use *Solo los N más
   cercanos* para no llenar el gráfico: el dibujo no cambia el cálculo.
6. **Objetivo:** el recuadro 4 dice el próximo LVN o EDGE en la dirección con su distancia; con *Objetivo = LVN /
   EDGE* el TP va ahí (si no hay, 2R).
7. **GATILLO manual:** cuando dude del gatillo automático, pulse GATILLO y arrastre un recuadro sobre las velas del
   retroceso; la respuesta (ARRANQUE / CONTINUIDAD / NO EJECUTABLE / [-]) se graba en el CSV para comparar.

## 6. Lectura rápida del panel antes de entrar

- Cabecera, línea ESTADO en **BARRIDO DE LIQUIDEZ REAL / PREMIUM** (M1), o un M2 / M3 armado (recuadro 3).
- Cabecera, línea del gatillo **GATILLO … CUMPLIDO**: solo entonces hay entrada.
- Recuadro 2: validación **Certificada**, recuperación **Confirmada** y **ESTRUCTURA CONFIRMADA** → contexto fuerte;
  recuadro 3: sumatoria **BARRIDO DE LIQUIDEZ REAL**.
- Recuadro 4: hay objetivo LVN y/o FVG a distancia suficiente.
- Recuadro 6: contratos > 0; si dice RIESGO INSUFICIENTE, use el micro (MNQ, MES, MYM).
- Cabecera y recuadro 9: perfil **CALIBRADO**; con PROVISIONAL, más cautela (y MT5 no manda órdenes).

## 7. Calibrar

Con 1-2 semanas de CSV (`_barridos_v3.csv` y `_modelos_v3.csv`) por instrumento: compare `ScoreTotal`, `Estado` y
`PremiumGates` con el resultado de los modelos (`Result`, `MAE_Ticks`, `MFE_Ticks`, `RewardRisk`) y ajuste los
umbrales del perfil del instrumento (31-36) y *22. Estados y alertas*. Nunca copie los valores de NQ a ES o YM.
