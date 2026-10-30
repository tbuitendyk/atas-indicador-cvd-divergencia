# Progreso de IndicadorCompleto (rama `indicador-completo`)

Especificación: el prompt "Lógica y arquitectura de indicador completo" (Indicador Combinado1) del propietario.
Decisiones del propietario (2026-10-30): construir todo de una vez; LVN amarillo/naranja por puntuación y ranking;
ventana flotante (solo ATAS clásico); estadística incluida (simulada).

Arquitectura: `Nucleo/` (lógica pura, sin tipos de ATAS, probada en Linux), `Atas/` (indicadores: maestro 1M,
visores 1M/5M, panel CVD; dibujo), `Ventana/` (ventana WPF del reporte), `pruebas/` (pruebas y verificación).

## Estado

- [ ] Proyecto autónomo (slnx, props, targets)
- [ ] Núcleo: tipos y configuración
- [ ] Núcleo: tiempo y sesiones (NY, Londres, Tokio, día Globex, semana)
- [ ] Núcleo: velas 1M, agregación 5M/15M/1H solo completas, footprint (POC, VAL, VAH)
- [ ] Núcleo: estructura 1H (pivotes, BOS/MSS, swing protegido), lecturas de rango, sincronización, EMA
- [ ] Núcleo: referencias de liquidez (sesiones, semana anterior, swings 1H, pivotes internos en rango)
- [ ] Núcleo: barridos (cruce nuevo, doble, recuperación 1M/5M, vigencia), absorción
- [ ] Núcleo: LVN (perfiles reales por sesión, compuestos, zonas, separación, registro y reacciones, ranking, mapa congelado)
- [ ] Núcleo: FVG y directriz
- [ ] Núcleo: M1 y M2, riesgo (SL, TP, micros, RR), señales
- [ ] Núcleo: gestión simulada (EMA, agotamiento, arranque contrario, protección POC) y estadística
- [ ] Núcleo: CVD (reinicio 09:30 NY, base parcial, 5M, divergencias)
- [ ] Núcleo: calidad de datos e instantánea del reporte; exportación JSON y CSV
- [ ] ATAS: maestro/visores, registro compartido, dibujo en 1M y 5M
- [ ] ATAS: indicador del panel CVD
- [ ] Ventana flotante WPF
- [ ] Pruebas, verificación de compilación y rendimiento
- [ ] Manual (LEEME.md) y fusión a main
