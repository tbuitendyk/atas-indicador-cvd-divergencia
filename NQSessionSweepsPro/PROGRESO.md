# Progreso de NQSessionSweepsPro (rama `nq-session-sweeps`)

Especificación: "Indicador de barrido de liquidez01112026.docx" (prompt "NQ SESSION SWEEPS PRO" + manual + código Pine
"Session Sweeps" de LuxAlgo). Decisiones del propietario (2026-11-01): las 6 fases de una vez; la IA decide lo que el
documento deja abierto y lo documenta en DECISIONES.md. Al fusionar: `instalar.txt` → NQSessionSweepsPro.

Arquitectura: `Nucleo/` (lógica pura sin tipos de ATAS, probada en Linux), `Atas/` (indicador, dibujo, correo),
`pruebas/` (núcleo + indicador real contra sustitutos).

## Estado

- [x] Proyecto autónomo (slnx, props, targets)
- [x] F1 Sesiones (3 modos, premercado, PineOriginal, DST automático, ajuste manual), tabla
- [x] F2 Niveles (sesiones, PDH/PDL, días -N, PWH/PWL, equal H/L), barridos Pine, FVG, MSS
- [x] F3 Order Flow (ventanas 1/3/5, absorción 0-10, velocidad, clasificación), panel
- [ ] F4 Ruptura en el tick, cuentas atrás 1m/5m, contexto H1, proyección T1-T3
- [ ] F5 Perfil/LVN, Modelo A, IFVG, Modelo B
- [ ] F6 Correo (cola, reintentos, anti-duplicados, prueba, solo en vivo)
- [ ] Dibujo completo, alertas, verificación (compilación + pruebas)
- [ ] LEEME.md, DECISIONES.md, fusión a main con instalar.txt

## Notas

- 2026-11-01: núcleo completo (F1-F6 de cálculo) y compilando sin avisos. Pruebas F1-F3 + FVG/IFVG/MSS: 57 ALL PASS.
  Corregido por las pruebas: el modo Pine vigilaba PDH/PDL; el VA de vela se inclinaba hacia arriba en los empates.
  Siguiente: pruebas F4/F5 (vivo, H1, proyección, modelos A/B, LVN), indicador de ATAS (Atas/), correo, dibujo.
