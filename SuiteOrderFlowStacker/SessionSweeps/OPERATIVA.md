# Session Sweeps Pro v2.0 — Operativa (MOD1-MOD4 y BFVG)

> **Estadística simulada; no es un registro de operaciones reales.** El indicador **no opera**: avisa, dibuja y calcula
> entrada, stop, objetivos y contratos. Las puntuaciones son puntos para ordenar, no probabilidades.

## 1. La secuencia (fase final del anexo)

1. **Contexto:** sesgo H1 / 15m / 5m (sección 1 del panel).
2. **Barrido de liquidez externa** validado: TRAMPA (A o PREMIUM preferible), idealmente **certificada por iceberg
   (MBO)**, y con **recuperación >= 16 ticks** en 1m y 5m (sección 2).
3. **Retroceso** a una zona: **LVN** del Thermal, **FVG / BFVG**, **EMA 15** de 1m o el nivel roto.
4. **Vela de arranque** en la zona, de una de estas formas:
   - una sola vela: pinbar / hammer con el **POC en el tercio inferior** (largo; superior en corto), envolvente o
     inside bar;
   - o una **vela compuesta** (varias velas del retroceso juntas como un footprint macro): POC en el tercio correcto,
     delta acumulado que **vira a favor**, imbalances de intención.
5. **Confluencias:** imbalance de intención, ruptura de la micro-tendencia del retroceso, EMA 15, CVD a favor o sin
   divergencia en contra.
6. **Entrada** al cierre de la vela gatillo (es la que simula el motor; entrar en el cruce de la EMA 15 es una
   alternativa manual que el motor no simula). **Stop** más allá de la zona / estructura o
   del POC de la vela de arranque (*Modo del stop*) + margen. **TP1** el próximo LVN (o 1.5 R); **TP2** el siguiente
   nivel externo (o 3 R). Después de TP1, el stop a la entrada y la **gestión de micro-impulso** con la EMA 15.
7. Estado del gatillo: **VÁLIDO** (entrar), **PENDIENTE** (falta algo: el panel dice qué), **INVÁLIDO** (no entrar:
   zona atravesada, iceberg o stacked en contra, delta en contra con avance, F3 lenta, noticia, R:R o stop).

## 2. Los modelos

| Modelo | Cuándo se arma | Zona | Gatillo |
|---|---|---|---|
| **MOD1** | Barrido congelado como TRAMPA (B, A o PREMIUM). | La zona del barrido (del nivel al extremo). | Solo **ARRANQUE**. |
| **BFVG** | Cada FVG que nace en la **pierna de recuperación** de una trampa (con o sin LVN; BFVG+LVN suma bonificación). | El FVG. | Arranque o continuidad. |
| **MOD2** | A favor del sesgo (H1 o 5m, sin 15m en contra). | LVN AUTO/MANUAL, FVG, EMA, nivel roto. | Arranque o continuidad. "MOD2 tras MOD3": zonas de la pierna de un MOD3. |
| **MOD3** | **Rompimiento con continuidad**: iceberg (o super big trade) ROTO + imbalance + stops + aceptación. | Las zonas de la pierna. | Invalida los MOD1/MOD2 contrarios (o todos, con *MOD3 invalida todos*). Caja fucsia en el panel. |
| **MOD4** | **RUPTURA** con aceptación de liquidez externa (interna si se permite). | Retest del nivel roto y zonas de la pierna (FVG+LVN primero). | Retroceso <= 61.8 % de la pierna, como mucho *MOD4: intentos máximos* entradas, espera máxima. |

Conflictos: una posición por dirección; un añadido solo si el riesgo abierto total no pasa de *Riesgo abierto máximo*.
FALLIDO invalida MOD1/BFVG de ese barrido; RUPTURA FALLIDA invalida MOD3/MOD4 de esa ruptura (y nace una evaluación
nueva como trampa). Un cambio de rango del Thermal cancela los setups pendientes que dependían solo de sus LVN.

## 3. Lectura rápida del panel antes de entrar

- Sección 2 en **verde/rojo negrita** con **Certificada** y recuperación **Confirmada** en 1m y 5m → contexto fuerte.
- Sección 3: Iceberg **OK** y Stops **OK** → absorción real en el barrido.
- Sección 4: hay **objetivo LVN** a distancia suficiente (si dice *Sin objetivo LVN*, el TP1 es 1.5 R).
- Sección 5: **VÁLIDO** con flecha. Si dice PENDIENTE, esperar lo que falta; INVÁLIDO, no entrar.
- Sección 6: contratos > 0; si dice RIESGO INSUFICIENTE, usar MNQ.

## 4. Calibrar

Con 1-2 semanas de CSV (`_barridos.csv` y `_modelos.csv`): comparar `ScoreTotal`, `Estado` y `PremiumGates` con el
resultado de los modelos (`Result`, `MAE_Ticks`, `MFE_Ticks`) y ajustar umbrales en *22. Estados y alertas* y el
*Contratos por barra* (16). El gatillo manual también se graba (Origen MANUAL) para comparar su lectura con la del
motor.
