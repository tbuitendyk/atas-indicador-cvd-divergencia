# Progreso de SuiteOrderFlowStacker

Especificación: PDF "PROMPT: SUITE ORDER FLOW Thermal Volume Profile + LVN | Stacked Imbalance | Icebergs | Stops |
Sweeps | Absorption Master" (2026-11-02).

Decisiones del propietario:

- El indicador 0 (Thermal Volume Profile + LVN) es NUESTRO ThermalVolumeProfile, no el del PDF.
- Construir todo sin preguntar, con las decisiones documentadas en DECISIONES.md.
- Manual de usuario y manual de operativa.

## Hecho

- [x] Proyecto: una DLL con los 6 indicadores; el ThermalVolumeProfile se comparte con `SUITE` → `ThermalVolumeProfileLVN`.
- [x] Núcleo:
  - modelos;
  - sesiones de Nueva York con premercado configurable;
  - contexto: PDH/PDL, IB, ON, swings, números redondos, VWAP, EMA y el perfil RTH con MotorTermico.
- [x] Motor de stacked imbalance (NUEVA / PROBADA / ROTA, "con iceberg").
- [x] Motor de icebergs:
  - detección NATIVA y SINTÉTICA (MBO) e INFERIDA (DOM);
  - estados y sesgo;
  - umbrales por sesión para NQ y MNQ;
  - `*` de alta probabilidad.
- [x] Motor de stops (zonas probables, cascadas, TRAMPA / CONTINUACIÓN, dirección del impulso).
- [x] Motor de barridos (por AggressorExchangeOrderId o inferido; LIQUIDEZ / RUPTURA / CONTRA ICEBERG).
- [x] Motor de confluencia:
  - setups A-D, puntuación y gestión;
  - noticias, cooldown y tendencia;
  - checklist y setup en espera.
- [x] Grabación CSV asíncrona y carga de los días anteriores.
- [x] Los 5 indicadores de ATAS:
  - parámetros generados;
  - MBO, DOM y trades;
  - panel, marcadores y modos Mínimo / Marcadores / Completo;
  - alertas.
- [x] Instalador: recompila si cambia un archivo enlazado de otra carpeta; `instalar.txt` incluye la suite.
- [x] Pruebas (`pruebas/verificar.sh`): compila contra sustitutos (0 avisos, 0 errores) y pasa 159 comprobaciones.
- [x] Documentación: LEEME.md (manual de usuario), OPERATIVA.md (manual de operativa y calibración), PARAMETROS.md
  (generada), DECISIONES.md.

## Comprobar en Windows con ATAS (no se puede sin ATAS ni Rithmic)

- [ ] El acceso directo compila e instala `SuiteOrderFlowStacker.dll` sin errores contra las DLL reales de ATAS.
- [ ] En la lista de indicadores, al buscar "Suite", salen los 6.
- [ ] Con Rithmic (MBO activado), el Iceberg Tracker dice `Modo: MBO`; sin MBO, `Modo: Inferido (sin MBO)`.
- [ ] Llegan los trades (`OnNewTrade`), el DOM (`MarketDepthChanged`) y el MBO (`OnMarketByOrdersChanged`) en vivo.
- [ ] `trade.Time` está en UTC: la hora del dato coincide con la de Nueva York.
- [ ] Los identificadores `ExchangeOrderId` / `AggressorExchangeOrderId` llegan en los trades con Rithmic.
- [ ] Una alerta por evento y por estado, ninguna al cargar el gráfico.
- [ ] El CSV aparece en `%APPDATA%\ATAS\OrderFlowSuite\{Instrumento}\` y se abre en Excel.
- [ ] POC, VAH y VAL del Thermal coinciden con el Volume Profile de ATAS en el mismo rango (validación del documento).
- [ ] Rendimiento en la apertura de NY con MBO (el gráfico no se frena).
