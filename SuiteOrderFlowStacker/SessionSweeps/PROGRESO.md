# Progreso — Session Sweeps Pro v2.0 (rama `session-sweeps-pro`)

Plan y decisiones: [PLAN.md](PLAN.md). Verificación: `bash SuiteOrderFlowStacker/pruebas/verificar.sh` (0 avisos, todas
las comprobaciones).

| Subfase | Estado | Notas |
|---|---|---|
| S1 Common + SuiteEventBus + publicación de los 5 indicadores + latido | **hecha** | `Comun/EventosSuite.cs`. Los 5 publican sus eventos (también el historial) y laten cada 2 s (CARGADO / SIN DATOS / NO CARGADO). 174 comprobaciones. La recarga del CSV cuenta los días desde la última vela del gráfico. |
| S2 Barras de volumen, trades, medianas, compuesta (3 contextos), fases | **hecha** | Motor del NQ copiado a `SessionSweeps/Nucleo` (espacio de nombres propio) sin su perfil LVN ni los Modelos A/B. `BarrasVolumen.cs` (barras de VolumePerBar, reparto, medianas RTH, velocidad 5 s, APROX desde el footprint), `AlmacenTicks` (historial delante + vivo, mismo resultado al recalcular), `Compuesta.cs` (POC, VA, tercios, subasta, stacked contiguo P11, fases F1/F2/F3 y F3 lenta). 193 comprobaciones. |
| S3 Clasificador de barridos (P1-P12, bloques A-E, estados, anti-repintado) | **hecha** | `Clasificador.cs` (bloques A-E con topes, penalizaciones, 7 puertas, 9 estados, congelación y transiciones 7.7), `MotorV2.Clasificador.cs` (T0 en el tick anclado, extremos, evaluación en cada barra de volumen, hijas tras FALLIDO / RUPTURA FALLIDA, recuperación 1m/5m), `Contexto.cs` (H1, 15m, 5m, CVD, VWAP, EMA 15). Pruebas T1-T13, T15, T17-T19 y espejo sellside: 223 comprobaciones. |
| S4 Integración Thermal (LvnRepository, RangeReset, sin LVN interno) | **hecha** | El Thermal (Suite) publica sus LVN (AUTO y MANUAL), BORRADO, RangeReset y su latido (`ThermalVolumeProfile.Suite.cs`, solo con SUITE: el Thermal por separado no cambia, 123 comprobaciones). `ClearLinesOnNewRange` (por defecto sí) y `ConfirmBeforeClear`. `Lvn.cs`: repositorio, próximo objetivo (5.6), mitigación, aviso de zonas borradas. Los LVN del Thermal son objetivos de la proyección. 236 comprobaciones. |
| S5 FvgEngine + BFVG | **hecha** | `Fvg.cs`: FVG de 3 velas en 1m y 5m (ACTIVO, PARCIAL, MITIGADO, INVALIDADO; máximo por lado), tipo y calidad de zona (FVG+LVN > LVN > FVG > nivel), BFVG para TODOS los FVG de la pierna de recuperación (con o sin LVN; BFVG+LVN con +ConfluenceBonus). T22. 244 comprobaciones. |
| S6 TriggerAnalyzer (+ confluencias del anexo) | **hecha** | `Gatillo.cs`: VÁLIDO por ARRANQUE (F1, F2 absorción/compresión/subasta/delta sin progreso, F3 fuera de la zona) o CONTINUIDAD (sesgo 5m/15m, barra rápida con intención, tercio a favor), PENDIENTE con lo que falta, INVÁLIDO / NO EJECUTABLE (zona atravesada, iceberg en contra, stacked en contra, delta en contra con progreso, F3 lenta, ventana, noticia, killzone, R:R, stop). Anexo: imbalance de intención, micro-tendencia, EMA 15, CVD, velas de arranque. Gatillo manual (vela de volumen / compuesta / auto, dirección auto con [-]). 259 comprobaciones. |
| S7 ModelEngine MOD1-MOD4, ConflictResolver, RiskCalculator, micro-impulso | **hecha** | `MotorV2.Modelos.cs`: MOD1 (solo ARRANQUE), BFVG, MOD2 (sesgo, LVN AUTO/MANUAL, FVG, EMA, nivel roto; "MOD2 tras MOD3"), MOD3 (iceberg o super big trade ROTO + imbalance + stops + aceptación; invalida contrarios), MOD4 (ruptura con aceptación, zonas de la pierna, retroceso ≤ 61.8 %, intentos), matriz de conflictos 8.6, una posición por dirección, gestión simulada (TP1, TP2, stop, MAE/MFE) y micro-impulso. `Riesgo.cs`. T20, T21, T23-T27, T29 por el motor real con trades tick a tick. 276 comprobaciones. |
| S8 Panel flotante principal + opciones del gráfico + CVD | pendiente | |
| S9 Panel flotante Thermal + GATILLO + vela compuesta manual | pendiente | |
| S10 Alertas, correo, CSV, documentación, lista para ATAS real | pendiente | |

## // VERIFICAR pendientes (solo en ATAS real)

- Estáticos compartidos entre indicadores de la misma DLL (el bus). El latido lo muestra: si no, el panel dirá NO CARGADO.
- MessageBox de WPF desde el ratón del gráfico (ConfirmBeforeClear del Thermal).
