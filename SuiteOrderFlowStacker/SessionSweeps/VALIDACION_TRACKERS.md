# Validación de los trackers contra ATAS

Cómo comprobar en ATAS real que lo que Sessions Sweeps Pro recibe de cada tracker de la suite es el mismo evento que el
tracker dibuja, y compararlo con las herramientas propias de ATAS. Los nombres exactos de las herramientas de ATAS
dependen de la versión instalada: **// VERIFICAR** cada uno en la lista de indicadores de su ATAS.

## 1. Preparación

1. Gráfico de NQ (o ES / YM) de 1 minuto con datos en vivo (con MBO si el proveedor lo da).
2. Agregue el tracker a validar (Iceberg Tracker Pro, Stacked Imbalance Pro, Stops Tracker Pro o Sweeps Tracker Pro) con
   su **Modo visual: Markers** (dibuja una marca por evento) y **Panel propio en el gráfico: Sí** solo para esta prueba.
3. Agregue Sessions Sweeps Pro (Suite) con **28. Panel y dibujo → Diagnóstico de trackers: Sí**. Cada evento que recibe
   por el bus aparece como un **rombo hueco fucsia** con "SSP ICE / STOP / SWEEP / STK / IMB".
4. Agregue la herramienta equivalente de ATAS (tabla de abajo) con una configuración parecida.

## 2. Qué debe coincidir

| Tracker | Rombo SSP | Debe coincidir con | Herramienta de ATAS para comparar (// VERIFICAR) |
|---|---|---|---|
| Iceberg Tracker Pro | SSP ICE + estado | la marca del iceberg (precio y vela) y su estado ACTIVO / AGUANTANDO / ROTO | DOM con MBO (órdenes que se recargan en el mismo precio); "Smart Tape" agrupando por precio |
| Stacked Imbalance Pro | SSP STK / SSP IMB | la zona stacked (precio medio) y cada imbalance diagonal | Gráfico de clúster con "Imbalance" activado y el mismo ratio (300 %) y volumen mínimo |
| Stops Tracker Pro | SSP STOP | el extremo de la cascada | "Big Trades" / "Smart Tape" con filtro de volumen igual a la cascada mínima |
| Sweeps Tracker Pro | SSP SWEEP | el precio final de la orden que barre niveles | "Smart Tape" (una orden agresiva con varios precios) |

Cada rombo debe caer en la **misma vela y el mismo precio** que la marca del tracker. Un rombo sin marca, o una marca
sin rombo, es un fallo: anote hora (CDMX y ET), precio y la fila del CSV del tracker
(`%APPDATA%\ATAS\OrderFlowSuite\{Instrumento}\yyyy-MM-dd.csv`) y del CSV de Sessions Sweeps
(`...\SessionsSweeps\{Instrumento}\yyyy-MM-dd_barridos.csv`).

## 3. Comprobaciones por instrumento

- En ES y YM el tracker usa **sus** umbrales (grupos "ES: …", "YM: …" y "Umbrales por sesión (ES/YM)"), nunca los de NQ.
  Son PROVISIONALES: compare cuántos eventos marca por sesión con lo que se ve en ATAS y ajústelos.
- El panel de Sessions Sweeps Pro muestra solo el **último** evento de cada tracker (con "CARGADO / SIN DATOS / NO
  CARGADO / VERSIÓN DISTINTA") y, en Detalle, el contador del día.

## 4. Resultado

Anote por tracker: eventos del tracker, rombos SSP, coincidencias, diferencias con ATAS y la configuración usada. Con
coincidencia completa, desactive el diagnóstico y vuelva a apagar el panel propio del tracker.
