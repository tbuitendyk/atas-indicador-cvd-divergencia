#!/usr/bin/env python3
"""Genera SessionSweeps/Atas/Generado/SessionSweepsPro.V2.g.cs: los parámetros de Session Sweeps Pro v2.0.

Sección 15 del documento maestro (ParametrosV2 en SessionSweeps/Nucleo) más el panel y el dibujo del anexo. Cada
parámetro se define UNA vez aquí; PARAMETROS.md se genera de este mismo archivo (pruebas, ACTUALIZAR_DOCS=1).
Uso (desde la raíz del repositorio):  python3 SuiteOrderFlowStacker/herramientas/generar_parametros_v2.py
"""
import os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SALIDA = os.path.join(RAIZ, "SessionSweeps", "Atas", "Generado", "SessionSweepsPro.V2.g.cs")

P = "_cfg.V2."

# (propiedad, tipo, destino, nombre en español, descripción, mínimo, máximo, recalcular)
# tipo: int | decimal | bool | string | hora | enum:<Tipo>
GRUPOS = [
    ("16. Barras de volumen y horario", [
        ("VolumePerBar", "int", P + "VolumePerBar", "Contratos por barra (NQ)",
         "Cada barra de volumen cierra al llegar a estos contratos. NQ: 500 (calibrar con el CSV).", 10, 100000, True),
        ("VolumePerBarMnq", "int", P + "VolumePerBarMnq", "Contratos por barra (MNQ)",
         "El MNQ negocia más contratos: 1000 como punto de partida.", 10, 100000, True),
        ("MedianLookbackBars", "int", P + "MedianLookbackBars", "Barras para las medianas",
         "Medianas de duración, rango y volumen (solo barras de RTH). Recomendado: 30.", 5, 500, True),
        ("PreT0DeltaSec", "int", P + "PreT0DeltaSec", "Delta previo a T0 (s)",
         "Segundos antes de la ruptura para el delta previo. Recomendado: 60.", 5, 600, True),
        ("PanelRefreshMs", "int", P + "PanelRefreshMs", "Refresco del panel (ms)",
         "Cada cuánto se redibuja el panel flotante. Recomendado: 250.", 100, 5000, False),
        ("KillzoneStartNY", "hora", P + "KillzoneStartNY", "Killzone: inicio (NY)",
         "Inicio de la ventana de mayor peso (hora de Nueva York). 09:30 = 07:30 CDMX en verano, 08:30 en invierno.", None, None, True),
        ("KillzoneEndNY", "hora", P + "KillzoneEndNY", "Killzone: fin (NY)",
         "Fin de la killzone (hora de Nueva York).", None, None, True),
        ("NewsBlockMin", "int", P + "NewsBlockMin", "Bloqueo por noticia (min)",
         "Minutos antes y después de cada hora de noticia en los que no se alerta ni se entra (NOTICIA).", 0, 60, True),
        ("NewsTimes", "string", P + "NewsTimes", "Horas de noticias (NY)",
         "Separadas por ';' en hora de Nueva York, por ejemplo 08:30;10:00;14:00.", None, None, True),
    ]),
    ("17. Fuentes de la suite", [
        ("DedupTicks", "int", P + "DedupTicks", "Duplicados: distancia (ticks)",
         "Dos barridos del mismo nivel a esta distancia o menos son el mismo evento.", 0, 20, True),
        ("DedupSec", "int", P + "DedupSec", "Duplicados: tiempo (s)", "Y dentro de estos segundos.", 0, 60, True),
        ("SuitePreSec", "int", P + "SuitePreSec", "Eventos previos a T0 (s)",
         "Los eventos de la suite hasta estos segundos antes de la ruptura también cuentan.", 0, 300, True),
        ("SuitePriceMarginTicks", "int", P + "SuitePriceMarginTicks", "Margen de precio (ticks)",
         "Distancia máxima entre un evento de la suite y el barrido para asociarlos.", 0, 40, True),
        ("IcebergEnabled", "bool", P + "Iceberg.Enabled", "Iceberg: activado", "Usar los eventos del Iceberg Tracker Pro.", None, None, True),
        ("IcebergWeight", "decimal", P + "Iceberg.Weight", "Iceberg: peso", "0 a 2 sobre los puntos que aporta. 1 = tal cual.", 0, 2, True),
        ("StopsEnabled", "bool", P + "Stops.Enabled", "Stops: activado", "Usar los eventos del Stops Tracker Pro.", None, None, True),
        ("StopsWeight", "decimal", P + "Stops.Weight", "Stops: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("SweepsEnabled", "bool", P + "Sweeps.Enabled", "Sweeps: activado", "Usar los eventos del Sweeps Tracker Pro.", None, None, True),
        ("SweepsWeight", "decimal", P + "Sweeps.Weight", "Sweeps: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("StackedEnabled", "bool", P + "Stacked.Enabled", "Stacked: activado", "Usar las zonas del Stacked Imbalance Pro.", None, None, True),
        ("StackedWeight", "decimal", P + "Stacked.Weight", "Stacked: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("ImbalanceEnabled", "bool", P + "Imbalance.Enabled", "Imbalance: activado", "Usar los imbalances diagonales de la suite.", None, None, True),
        ("ImbalanceWeight", "decimal", P + "Imbalance.Weight", "Imbalance: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
        ("AbsorptionEnabled", "bool", P + "Absorption.Enabled", "Absorción: activado", "Usar las señales del Absorption Master.", None, None, True),
        ("AbsorptionWeight", "decimal", P + "Absorption.Weight", "Absorción: peso", "0 a 2 sobre los puntos que aporta.", 0, 2, True),
    ]),
    ("18. Niveles", [
        ("EqualLevelTolTicks", "int", P + "EqualLevelTolTicks", "Iguales: tolerancia (ticks)",
         "Máximos o mínimos a esta distancia o menos cuentan como iguales.", 0, 20, True),
        ("EqualLevelMinTouches", "int", P + "EqualLevelMinTouches", "Iguales: toques mínimos", "Toques para un nivel igual.", 2, 10, True),
        ("LevelMinAgeMin", "int", P + "LevelMinAgeMin", "Edad mínima del nivel (min)",
         "Un nivel más reciente puntúa menos en el bloque A.", 0, 1440, True),
    ]),
    ("19. Stops", [
        ("MinPenetrationTicks", "int", P + "MinPenetrationTicks", "Penetración mínima (ticks)", "Ticks más allá del nivel para que cuente como barrido.", 1, 40, True),
        ("MaxSweepTicks", "int", P + "MaxSweepTicks", "Barrido máximo (ticks)", "Más allá de esto deja de ser un barrido y se vigila la ruptura.", 4, 200, True),
        ("BurstSpeedMult", "decimal", P + "BurstSpeedMult", "Ráfaga: × velocidad mediana", "Velocidad de 5 s respecto a la mediana para una ráfaga de stops.", 1, 10, True),
        ("FastBarDurationMult", "decimal", P + "FastBarDurationMult", "Barra rápida: × duración mediana", "Barra de volumen que dura menos de esto = rápida.", 0.1, 1, True),
        ("AggressorShareMin", "decimal", P + "AggressorShareMin", "Agresor mínimo (fracción)", "Parte del volumen más allá del nivel del lado agresor (0,65 = 65 %).", 0.5, 1, True),
        ("StopsEventWindowSec", "int", P + "StopsEventWindowSec", "Ventana de eventos de stops (s)", "Alrededor de T0.", 1, 300, True),
        ("VolBeyondRelMult", "decimal", P + "VolBeyondRelMult", "Volumen más allá: × medio de barridos", "Respecto a los últimos barridos.", 0.5, 10, True),
        ("SweepHistoryCount", "int", P + "SweepHistoryCount", "Barridos para la media", "Barridos anteriores para el volumen relativo.", 3, 200, True),
        ("MinVolBeyondFloor", "int", P + "MinVolBeyondFloor", "Volumen más allá: piso (contratos)", "Mínimo absoluto.", 0, 100000, True),
    ]),
    ("20. Absorción", [
        ("DivergenceDeltaPctMin", "decimal", P + "DivergenceDeltaPctMin", "Divergencia de delta mínima", "Delta % de la barra contra el movimiento (0,15 = 15 %).", 0, 1, True),
        ("IcebergMaxDistTicks", "int", P + "IcebergMaxDistTicks", "Iceberg: distancia máxima (ticks)", "Del extremo del barrido.", 0, 20, True),
        ("F2MinBars", "int", P + "F2MinBars", "F2: barras mínimas", "Compresión tras el extremo.", 1, 20, True),
        ("F2MaxBars", "int", P + "F2MaxBars", "F2: barras máximas", "", 1, 30, True),
        ("F2MaxBarsRetrace", "int", P + "F2MaxBarsRetrace", "F2: barras máximas en retroceso", "", 1, 40, True),
        ("CompressionRangeMult", "decimal", P + "CompressionRangeMult", "Compresión: × rango mediano", "", 0.1, 2, True),
        ("CompressionMaxTicks", "int", P + "CompressionMaxTicks", "Compresión: máximo (ticks)", "", 1, 60, True),
        ("ValueAreaPct", "decimal", P + "ValueAreaPct", "Área de valor (fracción)", "0,70 = 70 % del volumen.", 0.5, 0.95, True),
        ("FinishedAuctionMaxContracts", "int", P + "FinishedAuctionMaxContracts", "Subasta terminada: contratos máximos", "En el extremo.", 0, 50, True),
        ("StackedMinLevels", "int", P + "StackedMinLevels", "Stacked: niveles mínimos", "Contiguos.", 2, 10, True),
        ("ImbalanceRatio", "decimal", P + "ImbalanceRatio", "Imbalance: ratio", "Diagonal (3 = 300 %).", 1.5, 10, True),
        ("ImbalanceMinVolume", "int", P + "ImbalanceMinVolume", "Imbalance: volumen mínimo", "Del lado dominante.", 1, 1000, True),
    ]),
    ("21. Recuperación", [
        ("ReclaimBufferTicks", "int", P + "ReclaimBufferTicks", "Recuperación: margen (ticks)", "Cierre de vuelta al otro lado del nivel.", 0, 20, True),
        ("ReclaimMaxBars", "int", P + "ReclaimMaxBars", "Recuperación: barras máximas", "", 1, 30, True),
        ("CompositeMaxBars", "int", P + "CompositeMaxBars", "Vela compuesta: barras máximas", "", 2, 30, True),
        ("ZoneMaxBars", "int", P + "ZoneMaxBars", "Zona: barras máximas", "", 2, 60, True),
        ("F3RangeMult", "decimal", P + "F3RangeMult", "F3: × rango mediano", "Barra de desplazamiento.", 0.5, 10, True),
        ("F3DeltaPctMin", "decimal", P + "F3DeltaPctMin", "F3: delta % mínimo", "", 0, 1, True),
        ("ContDeltaPctMin", "decimal", P + "ContDeltaPctMin", "Continuación: delta % mínimo", "", 0, 1, True),
        ("DeltaFlipMin", "decimal", P + "DeltaFlipMin", "Giro de delta mínimo", "", 0, 1, True),
        ("RecoveryTicks", "int", P + "RecoveryTicks", "Recuperación 1m/5m (ticks)", "Recuperación que valida el barrido en el panel (anexo: 16).", 1, 200, True),
        ("StopBufferTicks", "int", P + "StopBufferTicks", "Margen del stop (ticks)", "Más allá de la zona, estructura o POC; también para FALLIDO.", 0, 40, True),
    ]),
    ("22. Estados y alertas", [
        ("ScoreTrapB", "int", P + "ScoreTrapB", "Puntos TRAMPA B", "Puntuación (no probabilidad).", 0, 100, True),
        ("ScoreTrapA", "int", P + "ScoreTrapA", "Puntos TRAMPA A", "", 0, 100, True),
        ("ScorePremium", "int", P + "ScorePremium", "Puntos TRAMPA PREMIUM", "Además de las 7 puertas.", 0, 100, True),
        ("PremiumMinLevelScore", "int", P + "PremiumMinLevelScore", "PREMIUM: bloque A mínimo", "", 0, 20, True),
        ("PremiumMaxReclaimBars", "int", P + "PremiumMaxReclaimBars", "PREMIUM: recuperación máxima (barras)", "", 1, 20, True),
        ("PremiumUpgradeMaxBars", "int", P + "PremiumUpgradeMaxBars", "UPGRADE PREMIUM: barras máximas", "", 1, 60, True),
        ("RuptureMinConditions", "int", P + "RuptureMinConditions", "RUPTURA: condiciones mínimas", "", 1, 6, True),
        ("AcceptanceCloses", "int", P + "AcceptanceCloses", "Aceptación: cierres", "Cierres más allá del nivel.", 1, 10, True),
        ("RuptureMaxAbsorptionScore", "int", P + "RuptureMaxAbsorptionScore", "RUPTURA: absorción máxima", "Bloque C máximo para una ruptura.", 0, 25, True),
        ("FailedBreakTicks", "int", P + "FailedBreakTicks", "RUPTURA FALLIDA: ticks", "", 1, 40, True),
        ("FailedBreakBars", "int", P + "FailedBreakBars", "RUPTURA FALLIDA: barras", "", 1, 30, True),
        ("MaxEvaluationBars", "int", P + "MaxEvaluationBars", "Evaluación: barras máximas", "", 3, 100, True),
        ("MaxEvaluationSec", "int", P + "MaxEvaluationSec", "Evaluación: segundos máximos", "", 30, 3600, True),
        ("AlertMinState", "enum:EstadoMinimoAlerta", P + "AlertMinState", "Estado mínimo para alertar",
         "Las trampas por debajo se ven en el panel pero no suenan.", None, None, False),
    ]),
    ("23. FVG / LVN", [
        ("FvgMinTicks", "int", P + "FvgMinTicks", "FVG: tamaño mínimo (ticks)", "", 1, 100, True),
        ("FvgMaxActive", "int", P + "FvgMaxActive", "FVG: máximo activos", "Por marco y lado.", 1, 100, True),
        ("Fvg1m", "bool", P + "Fvg1m", "FVG de 1m", "", None, None, True),
        ("Fvg5m", "bool", P + "Fvg5m", "FVG de 5m", "", None, None, True),
        ("BfvgWindowMin", "int", P + "BfvgWindowMin", "BFVG: ventana (min)", "Tras el extremo del barrido.", 1, 240, True),
        ("ConfluenceBonus", "int", P + "ConfluenceBonus", "Bonificación FVG + LVN", "", 0, 30, True),
        ("TargetMinDistTicks", "int", P + "TargetMinDistTicks", "Objetivo LVN: distancia mínima (ticks)", "", 0, 200, True),
    ]),
    ("24. Modelos", [
        ("Mod3InvalidatesAllMod1Mod2", "bool", P + "Mod3InvalidatesAllMod1Mod2", "MOD3 invalida todos los MOD1/MOD2",
         "Desactivado: solo los contrarios a la dirección del MOD3.", None, None, True),
        ("Mod4AllowInternal", "bool", P + "Mod4AllowInternal", "MOD4 en liquidez interna", "Por defecto solo externa.", None, None, True),
        ("Mod4MaxWaitMin", "int", P + "Mod4MaxWaitMin", "MOD4: espera máxima (min)", "", 1, 480, True),
        ("Mod4MaxRetracePct", "decimal", P + "Mod4MaxRetracePct", "MOD4: retroceso máximo", "Fracción de la pierna (0,618).", 0.1, 1, True),
        ("Mod4MaxAttempts", "int", P + "Mod4MaxAttempts", "MOD4: intentos máximos", "Entradas por ruptura.", 1, 10, True),
        ("MinRR", "decimal", P + "MinRR", "R:R mínimo", "", 0.5, 10, True),
        ("MaxStopTicks", "int", P + "MaxStopTicks", "Stop máximo (ticks)", "", 4, 400, True),
        ("SuperBigTradeMinContracts", "int", P + "SuperBigTradeMinContracts", "MOD3: super big trade (contratos)",
         "El documento no fija el valor: calibrar con el CSV.", 10, 100000, True),
        ("StopMode", "enum:ModoStop", P + "StopMode", "Modo del stop", "", None, None, True),
    ]),
    ("25. Riesgo", [
        ("RiskMode", "enum:ModoRiesgo", P + "RiskMode", "Modo de riesgo", "", None, None, False),
        ("RiskAmountUSD", "decimal", P + "RiskAmountUSD", "Riesgo por operación (USD)", "", 1, 1000000, False),
        ("RiskPct", "decimal", P + "RiskPct", "Riesgo por operación (%)", "", 0.01, 10, False),
        ("AccountSize", "decimal", P + "AccountSize", "Tamaño de la cuenta (USD)", "Si no se lee de ATAS.", 100, 100000000, False),
        ("ReadAtasAccount", "bool", P + "ReadAtasAccount", "Leer el saldo de ATAS", "Del portafolio seleccionado en ATAS.", None, None, False),
        ("MaxOpenRiskPct", "decimal", P + "MaxOpenRiskPct", "Riesgo abierto máximo (%)", "", 0.1, 20, True),
        ("MicroImpulseEmaLen", "int", P + "MicroImpulseEmaLen", "Micro-impulso: EMA", "EMA de 1m (anexo: 15).", 2, 200, True),
        ("MicroImpulseFromEntry", "bool", P + "MicroImpulseFromEntry", "Micro-impulso desde la entrada", "Si no, desde el armado.", None, None, True),
    ]),
    ("26. Gatillo manual", [
        ("ManualZoneTicks", "int", P + "ManualZoneTicks", "Zona manual (ticks)", "Margen de la caja dibujada.", 0, 40, False),
        ("ManualLookbackBars", "int", P + "ManualLookbackBars", "Barras de contexto", "", 1, 100, False),
        ("MaxManualBoxes", "int", P + "MaxManualBoxes", "Cajas manuales máximas", "", 1, 50, False),
    ]),
    ("27. CVD", [
        ("CvdMacroPivotBars", "int", P + "CvdMacroPivotBars", "Divergencia macro: velas por lado",
         "Pivote de máximo a máximo o de mínimo a mínimo. Más = menos sensible. NY NQ: 10.", 2, 100, False),
        ("CvdMicroPivotBars", "int", P + "CvdMicroPivotBars", "Divergencia micro: velas por lado",
         "Dentro de una macro, para afinar el punto de giro. NY NQ: 3.", 1, 30, False),
        ("CvdShowMicro", "bool", P + "CvdShowMicro", "Mostrar micro-divergencias", "", None, None, False),
        ("CvdMaxLookbackBars", "int", P + "CvdMaxLookbackBars", "Velas que se revisan", "", 20, 5000, False),
    ]),
    ("28. Panel y dibujo", [
        ("MostrarPanel", "bool", "_v.MostrarPanel", "Panel flotante",
         "Ventana movible a cualquier lugar o monitor (Resumen / Detalle). Guarda su posición.", None, None, False),
        ("ShowInChartPanel", "bool", "_v.ShowInChartPanel", "Panel dentro del gráfico",
         "El mismo contenido dibujado sobre el gráfico (si no se quiere la ventana).", None, None, False),
        ("InChartPanelX", "int", "_v.InChartPanelX", "Panel en el gráfico: X", "", 0, 5000, False),
        ("InChartPanelY", "int", "_v.InChartPanelY", "Panel en el gráfico: Y", "", 0, 5000, False),
        ("ShowSweepBoxes", "bool", "_v.ShowSweepBoxes", "Cajas de barrido (v2)", "Coloreadas por estado (TRAMPA, PREMIUM, RUPTURA, FALLIDO…).", None, None, False),
        ("ShowModelBoxes", "bool", "_v.ShowModelBoxes", "Zonas de los modelos", "MOD1-MOD4 y BFVG con entrada, stop y objetivos.", None, None, False),
        ("ShowFvgV2", "bool", "_v.ShowFvgV2", "FVG / BFVG (v2)", "", None, None, False),
        ("ShowInternalLiquidity", "bool", "_v.ShowInternalLiquidity", "Liquidez interna", "Niveles de sesión e iguales.", None, None, False),
        ("ShowExternalLiquidity", "bool", "_v.ShowExternalLiquidity", "Liquidez externa", "PDH/PDL y semana anterior.", None, None, False),
        ("ShowLvnLines", "bool", "_v.ShowLvnLines", "LVN del Thermal", "Las zonas que llegan del Thermal (activas y mitigadas).", None, None, False),
        ("ShowManualTriggerBoxes", "bool", "_v.ShowManualTriggerBoxes", "Cajas del gatillo manual", "", None, None, False),
        ("ShowCvdDivergences", "bool", "_v.ShowCvdDivergences", "Divergencias del CVD en el precio", "Tramos entre pivotes, sin líneas verticales.", None, None, False),
    ]),
]


def numero(v, decimal_):
    if decimal_:
        return f"{v}m"
    return str(v)


def propiedad(p, grupo, orden):
    nombre, tipo, destino, titulo, descripcion, minimo, maximo, recalcular = p
    desc = descripcion.replace("\\", "\\\\").replace('"', '\\"')
    tit = titulo.replace('"', '\\"')
    attrs = [f'\t[Display(Name = "{tit}", GroupName = "{grupo}", Order = {orden}, Description = "{desc}")]']
    cambio = "Recalcular();" if recalcular else "RedrawChart();"
    if tipo == "int":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = f"public int {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {minimo}, {maximo}); {cambio} }} }}"
    elif tipo == "decimal":
        attrs.append(f"\t[Range({minimo}, {maximo})]")
        cuerpo = (f"public decimal {nombre} {{ get => {destino}; set {{ {destino} = Math.Clamp(value, {numero(minimo, True)}, "
                  f"{numero(maximo, True)}); {cambio} }} }}")
    elif tipo == "bool":
        cuerpo = f"public bool {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    elif tipo == "string":
        cuerpo = f'public string {nombre} {{ get => {destino}; set {{ {destino} = value ?? ""; {cambio} }} }}'
    elif tipo == "hora":
        attrs.append('\t[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]')
        cuerpo = f"public TimeSpan {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    elif tipo.startswith("enum:"):
        cuerpo = f"public {tipo[5:]} {nombre} {{ get => {destino}; set {{ {destino} = value; {cambio} }} }}"
    else:
        raise ValueError(tipo)
    return "\n".join(attrs) + "\n\t" + cuerpo + "\n"


def main():
    partes = []
    for grupo, params in GRUPOS:
        partes.append(f"\t// --- {grupo} " + "-" * max(3, 100 - len(grupo)) + "\n")
        for i, p in enumerate(params):
            partes.append(propiedad(p, grupo, (i + 1) * 10))
    cuerpo = "\n".join(partes)
    texto = f"""// <auto-generated>
// Generado por SuiteOrderFlowStacker/herramientas/generar_parametros_v2.py: no editar a mano (cambiar el generador y
// volver a ejecutarlo). Parámetros de Session Sweeps Pro v2.0 (sección 15 del documento maestro y anexo).
// </auto-generated>
#nullable enable
namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes.Editors;

using SessionSweeps.Nucleo;

public partial class SessionSweepsPro
{{
{cuerpo}}}
"""
    os.makedirs(os.path.dirname(SALIDA), exist_ok=True)
    with open(SALIDA, "w", encoding="utf-8", newline="\n") as f:
        f.write(texto)
    print("generado:", os.path.relpath(SALIDA, RAIZ), sum(len(g[1]) for g in GRUPOS), "parámetros")


if __name__ == "__main__":
    main()
