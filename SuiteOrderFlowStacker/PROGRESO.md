# Progreso de SuiteOrderFlowStacker (rama `suite-order-flow`)

Especificación: PDF "PROMPT: SUITE ORDER FLOW Thermal Volume Profile + LVN | Stacked Imbalance | Icebergs | Stops |
Sweeps | Absorption Master" (2026-11-02). Decisiones del propietario: el indicador 0 (Thermal Volume Profile + LVN) es
NUESTRO ThermalVolumeProfile (no el del PDF); construir todo sin preguntar; decisiones en DECISIONES.md; manual de
usuario y de operativa. Al fusionar: `instalar.txt` → SuiteOrderFlowStacker.

## Estado

- [x] Proyecto (una DLL con los 6 indicadores; ThermalVolumeProfile compartido con SUITE)
- [ ] Núcleo: modelos, sesiones (con premercado), contexto (PDH/PDL, IB, ON, swings, redondos, VWAP, EMA, perfil)
- [ ] Motor de imbalance apilado (zonas NUEVA / PROBADA / ROTA)
- [ ] Motor de icebergs (MBO nativo, sintético, inferido sin MBO; estados; sesgo; umbrales por sesión NQ/MNQ)
- [ ] Motor de stops (zonas probables, cascadas, TRAMPA / CONTINUACIÓN)
- [ ] Motor de barridos (reconstrucción por AggressorExchangeOrderId o inferida; LIQUIDEZ / RUPTURA / CONTRA ICEBERG)
- [ ] Motor de confluencia (setups A-D, puntuación, gestión, noticias, cooldown, checklist)
- [ ] Grabación CSV y carga de días anteriores
- [ ] Indicadores de ATAS (5 carcasas + parámetros comunes), dibujo, alertas
- [ ] Pruebas, verificación de compilación
- [ ] LEEME.md (manual de usuario), OPERATIVA.md (manual de operativa), DECISIONES.md, fusión
