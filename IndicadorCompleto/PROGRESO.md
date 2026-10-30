# Progreso de IndicadorCompleto (rama `indicador-completo`)

Especificación: el prompt "Lógica y arquitectura de indicador completo" (Indicador Combinado1) del propietario.
Decisiones del propietario (2026-10-30): construir todo de una vez; LVN amarillo/naranja por puntuación y ranking;
ventana flotante (solo ATAS clásico); estadística incluida (simulada).

Arquitectura: `Nucleo/` (lógica pura, sin tipos de ATAS, probada en Linux), `Atas/` (indicadores: maestro 1M,
visores 1M/5M, panel CVD; dibujo), `Ventana/` (ventana WPF del reporte), `pruebas/` (pruebas y verificación).

## Estado

- [x] Proyecto autónomo (slnx, props, targets)
- [x] Núcleo: tipos y configuración
- [x] Núcleo: tiempo y sesiones (NY, Londres, Tokio, día Globex, semana)
- [x] Núcleo: velas 1M, agregación 5M/15M/1H solo completas, footprint (POC, VAL, VAH)
- [x] Núcleo: estructura 1H (pivotes, BOS/MSS, swing protegido), lecturas de rango, sincronización, EMA
- [x] Núcleo: referencias de liquidez (sesiones, semana anterior, swings 1H, pivotes internos en rango)
- [x] Núcleo: barridos (cruce nuevo, doble, recuperación 1M/5M, vigencia), absorción
- [x] Núcleo: LVN (perfiles reales por sesión, compuestos, zonas, separación, registro y reacciones, ranking, mapa congelado)
- [x] Núcleo: FVG y directriz
- [x] Núcleo: M1 y M2, riesgo (SL, TP, micros, RR), señales
- [x] Núcleo: gestión simulada (EMA, agotamiento, arranque contrario, protección POC) y estadística
- [x] Núcleo: CVD (reinicio 09:30 NY, base parcial, 5M, divergencias)
- [x] Núcleo: calidad de datos e instantánea del reporte; exportación JSON y CSV
- [x] ATAS: maestro/visores, registro compartido, dibujo en 1M y 5M
- [x] ATAS: indicador del panel CVD
- [x] Ventana flotante WPF
- [x] Pruebas, verificación de compilación y rendimiento
- [x] Manual (LEEME.md) y fusión a main

## Notas

- 2026-10-30: todo el código escrito y compilando (0 avisos) contra sustitutos de ATAS y WPF real.
  Siguiente: pruebas de escenarios del núcleo, rendimiento, manual LEEME.md, revisión final y fusión a main.
- 2026-10-30: `pruebas/verificar.sh`: compilación 0 avisos / 0 errores, 118 comprobaciones ALL PASS. Las pruebas
  encontraron y corrigieron dos fallos reales (centro de un LVN de fondo plano; desempate del POC). Histórico
  sintético de 90 días: sin excepciones, sin repintado, carga ~5 s, cada vela nueva ~0,05 ms. Manual en LEEME.md.
  Pendiente solo de comprobar en Windows con ATAS real: compilación contra las DLL reales, ventana WPF y
  emparejamiento maestro/visores.
