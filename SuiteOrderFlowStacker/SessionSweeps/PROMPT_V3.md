# Prompt maestro — corrección del 08-11-2026 (texto del propietario)

Copia literal del documento `SESSIONS_SWEEPS_PRO_CORRECCIÓN_08112026.docx` recibido el 2026-11-09. Es la especificación de la versión 3 (Sessions Sweeps Pro). El propietario aprobó construirla sin esperar la aprobación del plan por subfase.

    PROMPT MAESTRO PARA CLAUDE CODE
    SESSIONS SWEEPS PRO
    ATAS + ORDER FLOW SUITE + THERMAL VOLUME PROFILE + LVN + EDGE + CVD
    NQ / ES / YM | Panel unificado | Alertas | Correo | Telegram | MT5
    ================================================================================
    ============================ INICIO DEL PROMPT ============================
    0. INSTRUCCIONES DE ARRANQUE Y JERARQUÍA
    Vas a modificar e integrar los indicadores existentes del repositorio. NO
    empieces de cero, no reemplaces código funcional sin revisar antes su propósito
    y no inventes APIs de ATAS.
    El nombre visible y comercial del indicador principal será:
    SESSIONS SWEEPS PRO
    Elimina el nombre visible "NQ Session Sweeps Pro" de títulos, paneles, menús,
    documentación y mensajes nuevos. Si el nombre anterior se utiliza internamente
    en namespaces, nombres de archivo, configuraciones o datos persistidos, conserva
    la compatibilidad necesaria y documenta cualquier migración. No rompas las
    configuraciones guardadas del usuario sin una estrategia de migración.
    Este documento sustituye y unifica los prompts anteriores relacionados con:
    - NQ Session Sweeps Pro.
    - Session Sweeps Pro.
    - Clasificador Premium de barridos.
    - Modelos A, B y C.
    - Thermal Volume Profile + LVN (Suite).
    - Líneas EDGE.
    - Paneles flotantes.
    - Trackers de Iceberg, Stacked/Imbalance, Stops y Sweeps.
    - CVD y CVD Institutional Trigger Matrix.
    - Correo, Telegram y puente con MT5.
    Si el código, la documentación o un prompt previo contradicen este, sigue este
    prompt. No cambies alcance ni reglas aquí marcadas como DECIDIDA sin informarlo
    en el plan.
    ANTES DE ESCRIBIR CÓDIGO:
    1. Lee el repositorio y su documentación: README/LEEME, OPERATIVA, PARAMETROS,
    DECISIONES, PROGRESO y /docs.
    2. Inspecciona especialmente:
    - El indicador principal actual.
    - Thermal Volume Profile + LVN (Suite) y MotorTermico.cs.
    - Iceberg Tracker.
    - Stacked Imbalance.
    - Stops Tracker.
    - Sweeps Tracker.
    - Absorption Master, si está en el proyecto.
    - La última versión corregida del CVD y CVD Institutional Trigger Matrix.
    - El indicador o módulo que muestra Asia, Londres, NY AM y NY PM.
    3. Antes de tocar el código, entrega un plan que contenga:
    a. Archivos que vas a modificar, crear, migrar o retirar.
    b. Arquitectura de comunicación entre indicadores.
    c. APIs de ATAS que necesitas verificar.
    d. Cómo se separarán los datos y parámetros de NQ, ES y YM.
    e. Cómo se conservará el dibujo original de los trackers.
    f. Diseño del panel flotante y alternativa si WPF no es compatible.
    g. Diseño del puente MT5 y protección de credenciales.
    h. Riesgos, pruebas y funcionalidades que no puedan confirmarse sin ATAS real.
    4. Marca con // VERIFICAR cualquier API o comportamiento que no hayas comprobado
    con la versión de ATAS instalada. Para cada uno ofrece una alternativa.
    5. No escribas código hasta que yo apruebe el plan.
    6. Implementa una subfase por entrega. No afirmes que algo compila o funciona en
    ATAS real si solo se probó con DLL simuladas.
    =============================================================================
    1. PRIORIDADES Y ALCANCE OPERATIVO (DECIDIDA)
    =============================================================================
    Solo hay DOS prioridades de operativa:
    PRIORIDAD A — MODELO A: BARRIDO DE LIQUIDEZ
    La operativa busca un barrido real de liquidez, toma de stops, absorción y
    recuperación confirmada. La variante Premium es una clasificación de mayor
    calidad del mismo Modelo A, no un tercer modelo.
    PRIORIDAD B — MODELO B: NODO BAJO / LVN Y EDGE
    Es la prioridad de mayor interés del usuario. Debe recibir la mayor atención de
    desarrollo, visualización, control de zonas y análisis de gatillos.
    Busca continuación o reacción tras el retroceso del precio a un LVN, EDGE, FVG
    o zona de confluencia, con un gatillo confirmado. La detección y el uso correcto
    de los LVN de Thermal son especialmente importantes.
    El rompimiento con aceptación NO debe convertirse en un tercer modelo operativo.
    Se muestra como estado/contexto del mercado. Si el precio rompe un nivel y
    retrocede a un LVN, EDGE o FVG, se evalúa como una configuración del MODELO B,
    identificada, si es útil, con la etiqueta secundaria:
    B - RETROCESO TRAS ROMPIMIENTO
    El iceberg roto y la continuación se muestran como contexto y confirmación del
    Modelo B. No crear una tercera prioridad ni duplicar señales para la misma zona.
    Regla de prioridad de trabajo:
    - Desarrollar primero el Modelo B y la detección/visualización de LVN y EDGE.
    - Desarrollar después el Modelo A y su clasificador de barridos.
    - Compartir entre ambos el motor de gatillos, riesgo, panel, registro y alertas.
    No generar señales de entrada por tocar una línea o zona. Una entrada necesita
    zona válida, dirección definida, gatillo confirmado, stop calculable y riesgo
    aceptable.
    =============================================================================
    2. INSTRUMENTOS: NQ, ES Y YM (DECIDIDA)
    =============================================================================
    2.1 Instrumentos soportados
    El indicador debe funcionar en los futuros:
    - NQ: E-mini Nasdaq-100.
    - ES: E-mini S&P 500.
    - YM: E-mini Dow.
    - Opcionalmente permitir MNQ, MES y MYM mediante perfiles separados, si el feed
    y la cuenta de ATAS los ofrecen.
    No usar umbrales de volumen o delta de NQ cuando el gráfico activo sea ES o YM.
    No usar valores de ES o YM para NQ. Cada cálculo, perfil, buffer, evento, panel,
    CSV, alarma y parámetro dependiente del instrumento debe estar separado por
    instrumento y símbolo real.
    2.2 Detección del instrumento
    Al iniciar o cambiar de gráfico:
    1. Leer el instrumento desde los metadatos de ATAS.
    2. Detectar el producto raíz y el vencimiento del contrato.
    3. Mapear símbolos y alias de forma configurable:
    NQ/MNQ, ES/MES, YM/MYM.
    4. No depender solo de buscar texto en el nombre del gráfico si ATAS proporciona
    metadatos fiables.
    5. Si el símbolo no se reconoce, mostrar:
    "INSTRUMENTO NO SOPORTADO — CONFIGURACIÓN MANUAL REQUERIDA".
    6. No permitir ejecución automática en MT5 mientras no haya un mapeo de símbolo
    validado.
    2.3 Especificaciones de referencia
    Usar estas especificaciones iniciales como validación, pero comprobarlas contra
    los metadatos de ATAS y las especificaciones actuales del contrato:
    Instrumento | Tick de precio | Valor de 1 punto | Valor de 1 tick
    NQ | 0.25 | 20 USD | 5 USD
    ES | 0.25 | 50 USD | 12.50 USD
    YM | 1.00 | 5 USD | 5 USD
    Para MNQ, MES y MYM usar su propio multiplicador y valor por tick. No inferirlo
    copiando el contrato grande.
    Prioridad de datos:
    1. Metadatos de instrumento proporcionados por ATAS.
    2. Configuración por instrumento validada por el usuario.
    3. Valores de referencia de este prompt.
    4. Si el tick, multiplicador o valor monetario no se pueden confirmar:
    mostrar "VALOR DE CONTRATO NO VALIDADO" y bloquear cálculo de contratos y
    envío de órdenes.
    2.4 Perfil de parámetros por instrumento
    Crear InstrumentProfile separado por símbolo y familia de contrato:
    InstrumentProfile
    InstrumentRoot
    ContractSymbol
    TickSize
    PointValueUsd
    TickValueUsd
    VolumeBarTarget
    DeltaThresholds
    ImbalanceThresholds
    SweepThresholds
    AbsorptionThresholds
    StackedThresholds
    LvnParameters
    EdgeParameters
    RiskDefaults
    CalibrationStatus
    DataSource
    LastCalibrationTime
    Guardar perfiles independientemente. Si el usuario abre NQ, usar exclusivamente
    la configuración NQ. Si abre ES, usar exclusivamente ES. Si abre YM, usar
    exclusivamente YM.
    No compartir:
    - VolumePerBar.
    - Volumen mínimo para Imbalance.
    - Volumen mínimo para Stacked.
    - Delta mínimo bruto.
    - Umbrales de agresor.
    - Umbrales de absorción.
    - Velocidad de barrido.
    - Distribución de volumen por barra.
    - Cualquier umbral que dependa del número de contratos.
    2.5 Calibración de volumen y delta
    No inventes que una cantidad bruta de contratos es válida para los tres
    instrumentos. El volumen de NQ, ES y YM es diferente y también varía por horario,
    sesión, vencimiento, día de noticias y proveedor de datos.
    Implementar dos clases de medidas:
    A. MEDIDAS NORMALIZADAS, preferidas para comparar condiciones:
    - DeltaPct = Delta / VolumenTotal.
    - VolumenRelativo = volumen actual / mediana de volumen de referencia.
    - DeltaRelativo = valor absoluto de delta / mediana histórica de delta para el
    mismo instrumento y franja horaria.
    - AggressorShare = volumen agresor / volumen total.
    - BurstRatio = velocidad actual / mediana de velocidad del instrumento.
    - ImbalanceRatio = ask/bid o bid/ask, calculado según dirección y usando el
    mínimo de volumen propio del perfil del instrumento.
    B. MEDIDAS BRUTAS, conservadas para filtros mínimos:
    - Contratos de volumen.
    - Contratos bid/ask.
    - Contratos de delta.
    - Contratos de una ráfaga.
    Estas medidas deben leerse del perfil específico del instrumento y no copiarse
    de otra familia.
    Calibración inicial:
    - Usar, si hay datos suficientes, al menos 20 sesiones completas del mismo
    instrumento.
    - Calcular estadísticas por franja horaria comparable (por ejemplo, RTH y
    franjas de 5 o 15 minutos), no mezclar sin control toda la sesión.
    - Usar mediana y percentiles robustos, no solo promedios sensibles a valores
    extremos.
    - Permitir calibración separada para Asia, Londres, NY AM y NY PM.
    - Recalibrar al cambiar de contrato, proveedor, instrumento o sesión seleccionada.
    - Guardar la fecha, cantidad de sesiones, instrumento y calidad de los datos.
    - Exponer en el panel si el perfil está:
    CALIBRADO, CALIBRACIÓN INSUFICIENTE o CONFIGURACIÓN MANUAL.
    Si no hay suficientes datos, aplicar valores provisionales configurables propios
    de ese instrumento, marcarlos como "PROVISIONAL" y reducir el nivel de confianza.
    No presentar valores provisionales como estadística validada. No enviar órdenes
    automáticas cuando los parámetros esenciales estén sin calibrar, salvo que el
    usuario habilite explícitamente un modo de prueba.
    Crear una función de inicialización que, al cargar cada instrumento, prepare y
    seleccione automáticamente sus valores. No exigir que el usuario copie a mano
    los parámetros de NQ a ES o YM.
    2.6 Barras de volumen
    VolumePerBar debe poder configurarse por instrumento y contar con modo AUTO:
    VolumePerBarMode = AutoPorInstrumento | ManualPorInstrumento
    En modo AUTO:
    - Derivar la cantidad de contratos para cada barra virtual de una estadística
    propia de ese instrumento y franja horaria.
    - Exponer el valor actual y su método de cálculo en el panel Detalle.
    - Recalcular cuando cambie el contrato o la sesión, usando límites de seguridad.
    - No asumir que 500 contratos son apropiados para NQ, ES y YM a la vez.
    En modo MANUAL:
    - Usar un valor independiente para NQ, ES y YM.
    - Mostrar claramente el instrumento al que pertenece cada valor.
    2.7 Delta e imbalance
    Los umbrales por instrumento deben tener dos formas:
    - Umbral relativo, por ejemplo DeltaPct o DeltaRelativo.
    - Umbral bruto en contratos, opcional y específico del instrumento.
    Si falta bid/ask o delta fiable, marcar el dato como APROX o NO DISPONIBLE. No
    convertir volumen total en delta ni suponer dirección. No mezclar delta
    acumulado entre instrumentos ni entre contratos vencidos sin reinicio o
    normalización explícita.
    =============================================================================
    3. ARQUITECTURA E INTEGRACIÓN
    =============================================================================
    3.1 Capas
    - OrderFlowSuite.Common:
    Tipos y contratos compartidos, sin UI ni dependencias de ATAS.
    - SessionsSweepsPro.Core:
    Clasificador, modelos A/B, zonas LVN/EDGE/FVG, análisis de gatillo, riesgo,
    calibración por instrumento, sesiones, CVD y CSV.
    - SessionsSweepsPro.Atas:
    Indicador ATAS, adaptadores, dibujo, acceso al mercado y eventos.
    - SessionsSweepsPro.UI:
    Panel flotante general y panel flotante Thermal.
    - SessionsSweepsPro.Notifications:
    Correo, Telegram y cola de notificaciones.
    - SessionsSweepsPro.MT5:
    Cliente del puente con MetaTrader 5.
    - Thermal Volume Profile + LVN (Suite):
    Perfil, LVN, EDGE, selección de rangos, herramientas y gatillo manual.
    3.2 Bus de eventos
    Crear un SuiteEventBus thread-safe y aislado por instrumento y contrato. Debe
    publicar/consultar por hora, precio, instrumento y tipo de evento.
    Tipos:
    - IcebergEvent.
    - StackedEvent.
    - ImbalanceEvent.
    - StopsEvent.
    - SweepTrackerEvent.
    - AbsorptionEvent.
    - LvnZone.
    - EdgeZone.
    - FvgZone.
    - CvdBar.
    - CvdDivergence.
    - SessionInfo.
    - RangeReset.
    - ManualTriggerRequest.
    - ManualTriggerResult.
    - InstrumentProfileUpdated.
    - TradeCommand.
    - TradeReport.
    Cada indicador envía un latido para mostrar:
    CARGADO / SIN DATOS / NO CARGADO / VERSIÓN DISTINTA.
    Deduplicar eventos usando instrumento, precio, dirección, hora y origen. Un mismo
    hecho de mercado comunicado por dos fuentes solo se contabiliza una vez.
    // VERIFICAR: que ATAS comparta DLLs y objetos estáticos entre todos los
    // indicadores. Alternativas: MemoryMappedFile local por instrumento, named pipe
    // o lectura incremental de CSV. No seleccionar una alternativa sin documentar
    // sus limitaciones y probarla con ATAS real.
    =============================================================================
    4. DOS PANELES FLOTANTES
    =============================================================================
    4.1 Panel principal
    Un único panel flotante general con el nombre:
    SESSIONS SWEEPS PRO
    Integrar en él:
    - Sesión Asia / Londres / NY AM / NY PM.
    - Contexto H1, 15m y 5m.
    - Clasificador de barridos.
    - Resumen de Iceberg, Stacked/Imbalance, Stops y Sweeps.
    - CVD y CVD Institutional Trigger Matrix.
    - Modelos A y B.
    - Thermal: LVN y EDGE activos y objetivos.
    - Gatillos.
    - Riesgo.
    - Alertas, correo y Telegram.
    - Estado de conexión y operaciones MT5.
    - Calidad de datos y trazabilidad.
    4.2 Panel Thermal
    Mantener un panel flotante separado para herramientas del Thermal Volume
    Profile + LVN (Suite):
    - Perfil.
    - Rango.
    - Pierna.
    - Zona.
    - LVN.
    - Borrar LVN.
    - Deshacer.
    - EDGE.
    - Gatillo.
    - Borrar gatillo.
    No mover el panel de herramientas de selección y dibujo del Thermal al panel
    principal. Ese panel debe seguir dedicado a operar directamente sobre el
    Volume Profile, crear zonas y dibujar el análisis manual de gatillo.
    4.3 Retirar paneles antiguos duplicados
    Retirar de su ubicación visual anterior y centralizar en Sessions Sweeps Pro:
    - Panel de Iceberg Tracker.
    - Panel de Stacked Imbalance.
    - Panel de Stops Tracker.
    - Panel de Sweeps Tracker.
    - Panel de sesiones.
    - Panel de CVD Institutional Trigger Matrix.
    Conservar el dibujo original de los trackers sobre el gráfico de precios.
    4.4 Movimiento y persistencia
    Los dos paneles flotantes deben:
    - Poder arrastrarse a cualquier zona de la pantalla y a otro monitor.
    - Permitir minimizar, redimensionar y configurar Topmost.
    - Guardar posición, tamaño, opacidad y estado de secciones desplegables.
    - Restaurar posición aunque se reinicie ATAS.
    - Detectar si la posición guardada quedó fuera de pantalla y recolocarse.
    - Respetar monitores con distinto DPI y escalado.
    - Cerrar ordenadamente al retirar el indicador.
    Guardar el diseño en una configuración local del usuario por instrumento y
    temporalidad. No guardar secretos ni credenciales en el archivo de diseño.
    // VERIFICAR: creación y ciclo de vida de una ventana WPF desde un indicador ATAS.
    // Alternativa: panel movible dentro de toda la región del gráfico, con posición
    // porcentual y botón minimizar. Explicar en el plan cómo se activa el respaldo.
    =============================================================================
    5. TRACKERS: DIBUJO ORIGINAL Y VALIDACIÓN EN TIEMPO REAL
    =============================================================================
    Los indicadores de la suite deben seguir graficándose en el chart igual que sus
    versiones originales para poder compararlos visualmente con los marcadores
    nativos de ATAS.
    No cambiar el algoritmo interno de los trackers en esta integración. Se permite
    agregar publicación de eventos, control de visibilidad o mover su panel.
    Cada tracker publica el último evento y conserva su dibujo original:
    A. ICEBERG
    - Precio.
    - Hora.
    - Dirección/lado pasivo.
    - Estado: ACTIVO, AGUANTANDO o ROTO.
    - Volumen ejecutado.
    - Recargas.
    - Modo MBO o INFERIDO.
    B. STACKED / IMBALANCE
    - Dirección.
    - Precio inicial y final.
    - Niveles contiguos.
    - Ratio.
    - Volumen mínimo empleado.
    - Hora.
    C. STOPS TRACKER
    - Precio.
    - Hora.
    - Dirección.
    - Contratos estimados o detectados.
    - Cascada y otros campos ya existentes.
    D. SWEEPS TRACKER
    - Precio inicial/final.
    - Dirección.
    - Contratos.
    - Niveles barridos.
    - Duración.
    - Hora.
    Cada recuadro del panel principal debe mostrar solo el último evento de su
    tracker, además de su estado de conexión y el contador diario en modo Detalle.
    Opciones de dibujo:
    - Usar los dibujos originales de cada indicador.
    - Ocultar/mostrar cada tracker individualmente.
    - Modo de diagnóstico que superponga dibujo de Session Sweeps Pro y del
    indicador original, claramente distinguible.
    - Evitar duplicar marcadores en modo normal.
    Añadir documentación VALIDACION_TRACKERS.md para comparar los eventos propios
    con los indicadores nativos de ATAS usando configuraciones equivalentes. Marcar
    como // VERIFICAR los nombres exactos de herramientas nativas según la versión
    instalada.
    =============================================================================
    6. CVD
    =============================================================================
    Usar la última versión corregida del CVD que ya exista en el repositorio. No
    reescribir su cálculo sin demostrar una necesidad y pedir autorización.
    El CVD debe manejar datos aislados por instrumento, contrato y sesión.
    En el nuevo panel:
    - Mostrar un minigráfico de velas CVD.
    - Mostrar el valor acumulado actual.
    - Mostrar el delta de la última vela.
    - Mostrar el marco temporal y el tipo de reinicio del acumulado.
    - Dibujar las divergencias alcistas y bajistas SOBRE LAS VELAS DEL CVD.
    - No dibujar la divergencia en las velas de precio.
    - Si existe subgráfico CVD en ATAS, representar también ahí la divergencia.
    Mostrar diferencias regulares y ocultas si la versión actual ya las calcula.
    La divergencia puede ser confluencia, pero no debe crear una operación por sí
    sola.
    =============================================================================
    7. THERMAL VOLUME PROFILE: LVN
    =============================================================================
    7.1 Fuente única
    Sessions Sweeps Pro no calcula LVN. La fuente única de LVN es:
    Thermal Volume Profile + LVN (Suite)
    Los LVN automáticos y los LVN creados manualmente con el botón LVN son válidos
    para el Modelo B, para confluencias con FVG, para objetivos y para análisis de
    gatillo.
    7.2 Problemas que deben corregirse
    - Separar filas gruesas de dibujo de filas finas de detección.
    - Evitar detectar como LVN cualquier cola de volumen bajo respecto al POC.
    - Detectar un valle estructural entre dos picos relevantes, no solo un mínimo
    relativo al POC.
    - Representar un LVN como zona con límite bajo, límite alto y centro.
    - Reducir repintado por actualizaciones de vela abierta.
    - Permitir congelar el perfil de una pierna.
    - Ajustar LVN manual al valle cercano.
    - Corregir conversión de coordenada vertical a precio para escalas no lineales.
    - Borrar LVN manuales y automáticos al cambiar de rango cuando la opción esté
    activada.
    - Conservar origen, hora y rango de cada LVN.
    - Evitar borrar líneas manuales invisibles.
    - Evitar cálculos costosos en el hilo de dibujo.
    - Evitar alertas repetidas en cada vela mientras el precio siga junto a una zona.
    7.3 Perfil fino de detección
    Crear un perfil de detección separado del perfil visual:
    LvnDetectRowTicks = 1 o 2 ticks por fila
    Debe calcular LVN y EDGE en filas finas, independientemente del agrupamiento
    visual. El tamaño de fila debe respetar el TickSize real del instrumento activo.
    No mantener un límite visual de 300 filas como límite del perfil de detección.
    Si el rango es grande, controlar memoria y cálculo con límites explícitos y
    documentados, no agrupando silenciosamente todas las filas en una sola.
    7.4 Detección de valle entre picos
    Como requisito funcional, detectar un LVN como valle entre dos picos locales
    relevantes. La lógica debe:
    1. Suavizar el perfil fino sin borrar escalones y valles pequeños.
    2. Encontrar máximos locales cuya prominencia supere PicoMinRelative.
    3. Analizar pares de picos vecinos.
    4. Encontrar el mínimo entre ellos.
    5. Comparar la profundidad del valle contra el pico menor de los dos.
    6. Rechazar el candidato si no alcanza ProfundidadMin.
    7. Construir la zona LVN según el ancho a media profundidad, limitada por los
    picos que la enmarcan.
    8. Calcular el centro en el mínimo del volumen real dentro de la zona.
    9. Descartar los extremos del perfil que no estén enmarcados por dos picos.
    10. Evitar duplicar zonas superpuestas; combinar o priorizar los candidatos.
    Mantener el detector heredado como opción de compatibilidad:
    LvnMethod = ValleEntrePicos | PorcentajePocHeredado
    Usar por defecto ValleEntrePicos. El método heredado debe identificarse como tal
    y no mezclarse silenciosamente con el nuevo.
    7.5 Datos de cada zona LVN
    Publicar y almacenar:
    LvnZone
    Id
    Instrumento
    ContractSymbol
    RangeId
    PriceLow
    PriceHigh
    Center
    Origin: AUTO | MANUAL
    State: ACTIVO | PROBADO | ATRAVESADO | BORRADO
    Score
    Depth
    WidthTicks
    WasTouched
    FvgConfluence
    CreatedAtUtc
    ConfirmedAtUtc
    LastUpdatedAtUtc
    Dibujar por defecto:
    - Franja entre PriceLow y PriceHigh.
    - Línea de centro.
    - Etiqueta con precio, puntuación y origen configurable.
    7.6 Puntuación inicial LVN
    Puntuación 0–100, solo ranking de calidad; nunca llamarla probabilidad:
    - Profundidad del valle: hasta 40 puntos.
    - Ancho estrecho, por defecto <= 8 ticks del instrumento: hasta 15.
    - Volumen muy bajo o single prints: hasta 15.
    - No tocado desde su formación: hasta 15.
    - Coincidencia con FVG: hasta 15.
    Todos los valores deben poder configurarse por perfil de instrumento. Si un valor
    depende del número de contratos, debe calibrarse por separado para NQ, ES y YM.
    7.7 Estados y anti-repaint
    - ACTIVO: zona confirmada y no tocada.
    - PROBADO: tocada con rechazo observable.
    - ATRAVESADO: cierre con aceptación al otro lado según los criterios configurados.
    - BORRADO: eliminada por usuario o por cambio de rango.
    Confirmar solo con velas cerradas o persistencia de LvnConfirmUpdates. Después de
    confirmarse, no desplazar ni eliminar el LVN por una pequeña fluctuación del
    perfil. Implementar histéresis configurable.
    No presentar como confirmado un LVN provisional calculado únicamente sobre una
    vela todavía abierta. Marcarlo PROVISIONAL.
    7.8 Modo PIERNA y perfil congelado
    Añadir herramienta PIERNA. El usuario selecciona el inicio y el final de una
    pierna de impulso o recuperación. Al terminar la selección:
    - Congelar el perfil de esa pierna.
    - Calcular LVN y EDGE con los datos de esa pierna.
    - Dejar de incorporar volumen nuevo al perfil congelado.
    - Seguir actualizando el estado de toque/rechazo/aceptación usando el precio
    posterior.
    - Mostrar la etiqueta "PERFIL CONGELADO".
    - Permitir descongelar, guardar o borrar el perfil.
    7.9 LVN manual
    Al pulsar o dibujar un LVN manual:
    - Buscar en +/- ManualSnapTicks el valle más cercano del perfil fino.
    - Permitir ajustar automáticamente el punto al mínimo local.
    - Mostrar el precio original y el precio ajustado cuando haya diferencia.
    - Permitir clic y arrastre vertical para definir una zona completa.
    - Convertir coordenada de pantalla a precio mediante la API de ATAS que respeta
    la escala real del gráfico.
    - No usar interpolación lineal entre POC y un precio artificial.
    - No permitir borrar un LVN manual oculto si no se puede ver o seleccionar.
    - Añadir botones BORRAR LVN y DESHACER.
    - Migrar el formato antiguo de líneas si existe.
    Formato de persistencia recomendado:
    Centro | Bajo | Alto | Instrumento | RangeId | Origen | HoraUtc
    7.10 Cambio de rango
    ClearLinesOnNewRange = true
    Al seleccionar un nuevo rango:
    - Confirmar si ConfirmBeforeClear está activo.
    - Borrar las líneas/zones del rango anterior según la configuración.
    - Aumentar RangeId.
    - Publicar RangeReset.
    - Notificar a Sessions Sweeps Pro.
    - Cancelar setups pendientes que dependan exclusivamente del rango borrado.
    - No borrar operaciones cerradas ni alterar datos históricos.
    7.11 Rendimiento
    - No recalcular perfiles pesados dentro del hilo de dibujo.
    - Debounce al mover rango visible.
    - Usar caché por instrumento, contrato y RangeId.
    - Cancelar cálculos viejos si el usuario cambia de rango antes de que terminen.
    - Mostrar estado de cálculo sin bloquear ATAS.
    =============================================================================
    8. EDGE: DETECCIÓN Y DIBUJO
    =============================================================================
    EDGE representa una frontera del perfil donde una zona de volumen alto se aplana
    o cae con fuerza hacia una zona de menor volumen.
    Usar el perfil fino de detección, no el perfil visual agrupado. Convertir los
    parámetros de EDGE a ticks reales del instrumento, no a un número de filas
    visuales.
    Añadir una opción de dibujo separada del cálculo:
    ShowEdgeLines = true
    EdgeDisplay = Linea | Franja | LineaYFranja
    EdgeLineWidth = 2
    EdgeDashed = false
    EdgeBandTicks = configurable por instrumento
    EdgeBandOpacity = 45
    EdgeNearestOnly = 0 (0 = todos)
    El cálculo y la publicación pueden seguir activos aunque ShowEdgeLines esté
    apagado. Si se desactiva la detección completa, indicarlo claramente.
    Dibujar y etiquetar:
    - EDGE TECHO HVN: el HVN está debajo y el área de bajo volumen queda arriba.
    - EDGE PISO HVN: el HVN está encima y el área de bajo volumen queda abajo.
    Usar el lado detectado por MotorTermico si existe. Si no existe, calcularlo a
    partir del perfil fino con una regla robusta y testeable.
    Publicar:
    EdgeZone
    Id
    Instrumento
    ContractSymbol
    RangeId
    Price
    Side: TECHO_HVN | PISO_HVN
    State: ACTIVO | PROBADO | ATRAVESADO | BORRADO
    Score
    CreatedAtUtc
    Origin: AUTO | MANUAL
    Añadir botón EDGE al panel Thermal. El primer toque, los toques posteriores y
    las rupturas deben identificarse como eventos distintos.
    =============================================================================
    9. CLASIFICADOR DE BARRIDOS: MODELO A
    =============================================================================
    Fases:
    1. Nivel de liquidez existente.
    2. Toma de liquidez/stops.
    3. Absorción, atrapamiento o pérdida de continuidad.
    4. Recuperación con intención.
    Los stops antes de ser ejecutados no son directamente visibles. Etiquetar como
    inferidos cuando así corresponda. No afirmar que un iceberg es real sin datos
    MBO fiables.
    Corregir el código actual:
    - Anclar las ventanas de análisis al instante del barrido, no al inicio del
    minuto.
    - Evitar contar dos veces el cierre de vuelta.
    - Comparar volumen con unidades equivalentes.
    - Calcular POC y Value Area sobre la vela compuesta del evento, no sobre una
    sola vela.
    - Puntuar la calidad del nivel.
    - Integrar Stops Tracker, Sweeps Tracker, Iceberg, Stacked/Imbalance y CVD.
    - El Pine o indicador auxiliar no debe vetar el estado; es confluencia.
    - Requerir ticks contiguos para validar stacked imbalance.
    - Usar delta previo real o etiquetarlo APROX.
    - Aplicar congelación anti-repaint.
    Estados:
    - PENDIENTE.
    - SIN LIQUIDEZ.
    - BARRIDO REAL.
    - BARRIDO REAL PREMIUM.
    - RUPTURA / ACEPTACIÓN.
    - FALLIDO.
    - RUPTURA FALLIDA.
    - INDETERMINADO.
    Clasificar como BARRIDO REAL solo si se confirma toma de liquidez y recuperación
    según los criterios configurados. La categoría PREMIUM requiere criterios más
    estrictos, puertas de calidad y contexto. Los umbrales son valores iniciales
    calibrables, no resultados estadísticos.
    Puntuación por bloques A–E:
    - A: calidad del nivel.
    - B: toma de stops/actividad de barrido.
    - C: absorción.
    - D: recuperación.
    - E: contexto.
    Cada bloque tiene un tope. Aplicar penalizaciones después de los topes y limitar
    la suma total al rango 0–100. Mostrar el desglose y la razón de cada punto.
    Datos dependientes del volumen o del delta deben usar el InstrumentProfile del
    símbolo activo. No utilizar umbrales brutos compartidos entre NQ, ES y YM.
    Premium solo si se cumplen todas las puertas configuradas: nivel relevante,
    actividad de toma de stops, absorción/F2, subasta confirmada cuando haya datos,
    recuperación rápida con intención y contexto permitido.
    Antes de confirmar, mostrar PROVISIONAL. Al confirmar, congelar estado, hora,
    puntuación y datos del evento. Permitir únicamente transiciones explícitas:
    - BARRIDO REAL -> PREMIUM, dentro de la ventana configurada.
    - BARRIDO REAL -> FALLIDO.
    - RUPTURA -> RUPTURA FALLIDA.
    =============================================================================
    10. MODELO B: LVN / EDGE COMO PRIORIDAD DE MAYOR INTERÉS
    =============================================================================
    El Modelo B debe evaluar retrocesos hacia:
    1. FVG + LVN.
    2. FVG + EDGE.
    3. LVN de Thermal (automático o manual).
    4. EDGE de Thermal.
    5. FVG.
    6. EMA o nivel roto, si están habilitados.
    Prioridad de calidad de zona configurable:
    FVG+LVN > FVG+EDGE > LVN > EDGE > FVG > EMA/nivel
    La prioridad no sustituye al análisis del gatillo.
    Para cada zona:
    - Registrar instrumento, contrato, rango y origen.
    - Mostrar distancia actual en ticks y puntos.
    - Mostrar si está activa, probada, mitigada, atravesada o inválida.
    - Mostrar el próximo objetivo LVN o EDGE en dirección de la operación.
    - Analizar cada FVG individualmente.
    - No exigir que el FVG carezca de LVN para analizarlo.
    - Si un FVG contiene un LVN, marcar "FVG+LVN".
    - Si coincide con EDGE, marcar "FVG+EDGE".
    - Registrar cada análisis de gatillo y su resultado.
    Estado antes de llegar a la zona:
    NO SE HA CUMPLIDO — EN CAMINO A LVN <precio>
    NO SE HA CUMPLIDO — EN CAMINO A EDGE <tipo> <precio>
    No mostrar gatillo cumplido antes de confirmar la vela necesaria.
    Las entradas de continuación tras rompimiento o iceberg roto se consideran
    configuraciones del Modelo B. No crear un modelo operativo independiente ni
    duplicar alertas o entradas para el mismo movimiento.
    =============================================================================
    11. FVG Y BFVG
    =============================================================================
    Detectar FVG con regla de tres velas en los marcos configurados, por defecto 1m
    y 5m. El tamaño mínimo se expresa en ticks del instrumento activo.
    Estados:
    - ACTIVO.
    - PARCIAL.
    - MITIGADO.
    - INVALIDADO.
    Límite configurable de FVG activos por instrumento y dirección.
    Tras un BARRIDO REAL o PREMIUM:
    - Registrar todos los FVG generados en la pierna de recuperación.
    - Analizar cada FVG, tenga LVN, EDGE o no.
    - Etiquetar BFVG.
    - Si coincide con LVN, etiquetar BFVG+LVN.
    - Si coincide con EDGE, etiquetar BFVG+EDGE.
    - No dejar de evaluar un FVG por el hecho de contener un LVN.
    - Dar a cada zona un análisis independiente de gatillo.
    - Cerrar la ventana BFVG por tiempo, nuevo barrido o invalidación configurada.
    =============================================================================
    12. GATILLOS AUTOMÁTICOS Y MANUALES
    =============================================================================
    12.1 Condiciones comunes
    El gatillo analiza:
    - Zona candidata.
    - Dirección esperada.
    - Barras de volumen.
    - Footprint por precio.
    - Bid/ask y delta disponibles.
    - Compuesta de la zona.
    - Eventos de Iceberg, Stops, Sweeps, Stacked/Imbalance y CVD.
    - Estado del LVN/EDGE/FVG.
    - InstrumentProfile correspondiente.
    Devolver:
    - VÁLIDO.
    - PENDIENTE.
    - NO EJECUTABLE.
    Indicar tipo:
    - VELA DE ARRANQUE.
    - VELA DE CONTINUIDAD.
    12.2 Vela de arranque
    Validar llegada a la zona, freno/absorción y expansión a favor. Usar magnitudes
    normalizadas por el instrumento:
    - Rango relativo a la mediana del instrumento y horario.
    - Duración relativa.
    - DeltaPct y DeltaRelativo.
    - Bid/ask por nivel.
    - Stacked imbalance contiguo cuando esté disponible.
    - Absorción/iceberg y subasta cuando haya datos suficientes.
    - Cierre respecto a la zona.
    No trasladar cantidades brutas de contratos de un símbolo a otro.
    12.3 Vela de continuidad
    Permitirla cuando exista sesgo/contexto favorable y una reacción con intención
    en la zona, usando valores calibrados por instrumento. Debe haber cierre
    favorable y ausencia de evidencia importante en contra.
    12.4 Pendiente e inválido
    PENDIENTE:
    - Zona tocada, pero falta una condición.
    - Mostrar exactamente qué falta.
    NO EJECUTABLE:
    - Cierre de invalidación.
    - Absorción fuerte en contra.
    - Delta adverso sostenido.
    - Stacked contrario confirmado.
    - Se agotó la ventana.
    - Noticia bloqueada.
    - Stop excesivo.
    - Riesgo o R:R fuera de límites.
    - Datos esenciales ausentes o perfil de instrumento no validado.
    12.5 Botón manual GATILLO en Thermal
    El usuario puede seleccionar un rectángulo de precio y tiempo o una zona
    alrededor de un precio. Opciones:
    - Análisis de vela de volumen.
    - Análisis de vela compuesta.
    - AUTO.
    - Dirección AUTO, LARGO o CORTO.
    Respuesta única:
    - GATILLO POR VELA DE ARRANQUE.
    - GATILLO POR VELA DE CONTINUIDAD.
    - NO EJECUTABLE.
    Mostrar:
    - Instrumento y contrato.
    - Zona y marco temporal.
    - Puntuación y razones.
    - Eventos de suite encontrados.
    - POC, delta, volumen y subasta disponibles.
    - Entrada sugerida, stop, TP y R:R, si se pueden calcular.
    - Hora CDMX.
    El botón BORRAR GATILLO elimina el recuadro seleccionado o, sin selección,
    permite borrar todos los recuadros manuales.
    El gatillo manual no manda órdenes. Registrar Origen=MANUAL.
    =============================================================================
    13. PANEL GENERAL: SESSIONS SWEEPS PRO
    =============================================================================
    Cabecera siempre visible:
    LÍNEA 1 — ESTADO:
    - SIN OPERACIÓN.
    - PENDIENTE DE CUMPLIMIENTO DE BARRIDO DE LIQUIDEZ.
    - BARRIDO DE LIQUIDEZ REAL.
    - BARRIDO DE LIQUIDEZ REAL PREMIUM.
    - RUPTURA / ACEPTACIÓN (contexto, no tercer modelo).
    LÍNEA 2 — GATILLO:
    - GATILLO ALCISTA CUMPLIDO, verde negrita y flecha hacia arriba.
    - GATILLO BAJISTA CUMPLIDO, rojo negrita y flecha hacia abajo.
    - NO SE HA CUMPLIDO — EN CAMINO A LVN/EDGE <precio>.
    - PENDIENTE, naranja, si falta una condición.
    LÍNEA 3 — INSTRUMENTO Y CONEXIONES:
    - NQ / ES / YM.
    - Contrato activo.
    - Perfil: CALIBRADO / PROVISIONAL / INSUFICIENTE.
    - Estado de datos, Thermal, trackers y MT5.
    - No mostrar datos de otro instrumento en la ventana actual.
    Recuadros desplegables:
    1. INSTRUMENTO Y CALIBRACIÓN
    - Símbolo activo y vencimiento.
    - TickSize, valor monetario de tick y punto.
    - VolumePerBar.
    - Delta mínimo normalizado y delta bruto propio del instrumento.
    - Sesiones de calibración y fecha.
    - Estado de perfil.
    2. SESIÓN
    - Asia, Londres, NY AM o NY PM.
    - Hora CDMX y ET.
    - Killzone activa/inactiva.
    - Fuente de calendario de noticias si está configurada.
    3. CONTEXTO
    - H1 alcista, bajista o rango.
    - 15m y 5m con flecha vectorial.
    - No usar emojis como flechas.
    4. BARRIDO
    - Nivel, hora, dirección, clasificación y puntuación.
    - Stops y eventos relacionados.
    - Estado de confirmación MBO o INFERIDO.
    - Recuperación 1m y 5m en ticks/puntos según TickSize real del instrumento.
    5. ICEBERG — solo último evento.
    6. STACKED/IMBALANCE — solo último evento.
    7. STOPS TRACKER — solo último evento.
    8. SWEEPS TRACKER — solo último evento.
    9. CVD — velas CVD, divergencia sobre esas velas y acumulado.
    10. MODELO A — estado, barrido padre y BFVG.
    11. MODELO B — zona objetivo, tipo LVN/EDGE/FVG, distancia, estado de gatillo.
    12. LVN/EDGE — próximo objetivo, origen, estado y precio.
    13. FOOTPRINT/GATILLO — válido, pendiente o no ejecutable.
    14. RIESGO — riesgo monetario/porcentaje, contratos, stop, TP y R:R.
    15. ALERTAS — correo/Telegram y resultado de último envío.
    16. MT5 — modo, cuenta, símbolo, conexión, posición y controles.
    17. CALIDAD Y TRAZABILIDAD — archivo CSV, fuentes, versión y modo de datos.
    Todos los módulos que muestran volumen o delta deben presentar unidades:
    - Contratos.
    - Delta en contratos.
    - DeltaPct o métrica normalizada.
    - Instrumento al que pertenecen.
    =============================================================================
    14. EDGE Y LVN EN LA OPERATIVA
    =============================================================================
    Incluir en OPERATIVA.md una explicación práctica:
    - Un LVN es un área de bajo volumen entre áreas relevantes del perfil.
    - Un EDGE es el límite donde el volumen alto cae hacia una zona de menor
    aceptación.
    - Pueden actuar como zonas de reacción, continuación, invalidación o destino.
    - No abrir una operación solo porque el precio tocó el LVN o EDGE.
    - Esperar gatillo y validar el contexto.
    - El primer toque debe diferenciarse de los toques repetidos.
    - Un rompimiento aceptado puede cambiar el papel del nivel; confirmarlo con
    cierres y comportamiento de volumen/delta.
    - El stop debe ir más allá de la zona o estructura, con margen configurable.
    - Mostrar confluencias FVG, LVN, EDGE, sweep, iceberg y CVD sin contar dos veces
    el mismo hecho.
    - Medir los resultados en el registro; no afirmar tasas de acierto no probadas.
    =============================================================================
    15. ALERTAS: SOLO LAS SOLICITADAS
    =============================================================================
    Las alertas automáticas principales son:
    1. BARRIDO DE LIQUIDEZ REAL.
    2. BARRIDO DE LIQUIDEZ REAL PREMIUM.
    3. RUPTURA / ACEPTACIÓN CONFIRMADA como aviso de contexto, no como tercera
    estrategia.
    4. GATILLO VÁLIDO DEL MODELO A.
    5. GATILLO VÁLIDO DEL MODELO B.
    Un upgrade de real a premium no debe generar una segunda alerta duplicada de
    barrido real; debe etiquetarse como UPGRADE PREMIUM.
    Para cada alerta configurar:
    - Sonido y ventana de ATAS.
    - Correo ON/OFF.
    - Telegram ON/OFF.
    Enviar correo y Telegram inmediatamente después de confirmar el evento, de
    forma asíncrona, sin bloquear ATAS. Incluir instrumento, contrato, dirección,
    hora CDMX/ET, clasificación, zona, gatillo, entrada, SL, TP, datos relevantes de
    trackers y estado MT5.
    No enviar alertas durante el recálculo histórico, si el evento es provisional,
    si hay noticia bloqueada o si la misma alerta ya fue enviada.
    =============================================================================
    16. CORREO Y TELEGRAM
    =============================================================================
    Correo SMTP:
    - Servidor.
    - Puerto.
    - TLS.
    - Usuario.
    - Contraseña de aplicación.
    - Remitente y destinatarios.
    Telegram:
    - Bot token.
    - Chat ID.
    - Botón de prueba.
    Las credenciales deben cifrarse con el mecanismo seguro disponible en Windows,
    por ejemplo DPAPI. Nunca escribir contraseñas, tokens o datos sensibles en:
    - Logs.
    - CSV.
    - Mensajes de error.
    - Archivos de configuración sin cifrar.
    Añadir cola, reintentos limitados y registro del resultado de entrega.
    =============================================================================
    17. MT5: OPCIONAL, SEGURO Y POR INSTRUMENTO
    =============================================================================
    La ejecución en MT5 es opcional. Debe estar APAGADA por defecto.
    Arquitectura:
    - Puente local entre el indicador y MT5.
    - Comunicación asíncrona por localhost.
    - Idempotencia para evitar órdenes duplicadas.
    - Credenciales introducidas en una ventana segura.
    - Cifrado de credenciales.
    - Modo SIMULACIÓN, DEMO y REAL.
    - REAL requiere confirmación expresa del usuario.
    No recibir ni solicitar credenciales dentro de este prompt. El usuario debe
    ingresarlas directamente en la configuración segura de su instalación.
    17.1 Mapeo por instrumento
    Crear tabla editable independiente:
    ATAS NQ -> símbolo MT5 elegido por el usuario.
    ATAS ES -> símbolo MT5 elegido por el usuario.
    ATAS YM -> símbolo MT5 elegido por el usuario.
    Puede tratarse de futuros o CFD. El precio de ATAS y el de MT5 pueden no
    coincidir. No copiar precios absolutos del futuro al símbolo de MT5.
    Antes de habilitar cada mapeo:
    - Verificar tamaño de tick, point, digits y tamaño mínimo de volumen de MT5.
    - Verificar valor monetario por punto/lote.
    - Mostrar una prueba de conversión.
    - Bloquear órdenes si no se valida la conversión.
    17.2 SL, TP y tamaño de posición
    SL para Modelo A:
    - Compra: debajo del extremo del barrido menos el buffer.
    - Venta: encima del extremo del barrido más el buffer.
    SL para Modelo B:
    - Más allá del límite relevante de LVN/EDGE/FVG/estructura, con buffer.
    TP inicial:
    - Distancia de 2R, configurable.
    - O salida por cierre de vela 1m al otro lado de EMA15.
    - O lo que ocurra primero, opción por defecto.
    - El SL nunca se amplía.
    Calcular tamaño de posición con:
    - Riesgo monetario o porcentaje.
    - Distancia real entre entrada y SL.
    - Tick/point value del instrumento ATAS.
    - Conversión validada al símbolo MT5.
    - volume_min, volume_max y volume_step de MT5.
    Si el volumen calculado es menor al mínimo permitido, NO enviar la orden.
    Antes de mandar la orden, mostrar en panel:
    - Símbolo ATAS y símbolo MT5.
    - Dirección.
    - Volumen/lotes.
    - Precio de ejecución esperado si está disponible.
    - SL, TP, riesgo estimado y conversión.
    - Modo SIMULACIÓN/DEMO/REAL.
    Controles siempre visibles:
    - PAUSAR.
    - CERRAR TODO.
    - DESHABILITAR ÓRDENES.
    Límites configurables:
    - Operaciones máximas por día.
    - Pérdida máxima diaria.
    - Una posición por dirección.
    - Sin posiciones opuestas simultáneas.
    - No operar con noticia bloqueada.
    - No operar en histórico ni durante la recarga.
    - No operar con perfil/calibración insuficiente, salvo autorización explícita en
    SIMULACIÓN.
    =============================================================================
    18. PARÁMETROS Y DEFINICIONES
    =============================================================================
    Todos los parámetros deben tener nombre, unidad, descripción en español, valor
    por instrumento cuando corresponda y validación de rango.
    Crear grupos de parámetros:
    A. GENERAL
    - InstrumentProfileMode.
    - PanelMode.
    - SessionTimezone.
    - PanelRefreshMs.
    - KillzoneStart/End.
    - NewsBlockMin.
    - DataQualityMinimum.
    B. POR INSTRUMENTO
    - TickSize.
    - PointValueUsd.
    - TickValueUsd.
    - VolumePerBarMode.
    - VolumePerBar.
    - CalibrationLookbackSessions.
    - TimeOfDayBucketMinutes.
    - MinimumCalibrationSessions.
    - DeltaPctThresholds.
    - DeltaContractThresholds.
    - AggressorShareThreshold.
    - ImbalanceRatio.
    - ImbalanceMinVolume.
    - StackedMinLevels.
    - BurstRatioThreshold.
    - AbsorptionThresholds.
    C. BARRIDOS
    - MinPenetrationTicks.
    - MaxSweepTicks.
    - ReclaimBufferTicks.
    - ReclaimMaxBars.
    - RecoveryTicks.
    - FastBarDurationMult.
    - F3RangeMult.
    - F3DeltaPctMin.
    - ScoreRealMin.
    - ScorePremiumMin.
    - MaxEvaluationBars.
    - MaxEvaluationSec.
    D. LVN
    - LvnMethod.
    - LvnDetectRowTicks.
    - PicoMinRelative.
    - ProfundidadMin.
    - LvnMinScore.
    - LvnConfirmUpdates.
    - LvnHysteresisPct.
    - ManualSnapTicks.
    - ClearLinesOnNewRange.
    - ConfirmBeforeClear.
    - RearmTicks.
    E. EDGE
    - ShowEdgeLines.
    - EdgeDisplay.
    - EdgeLineWidth.
    - EdgeDashed.
    - EdgeBandTicks.
    - EdgeBandOpacity.
    - EdgeNearestOnly.
    - Parámetros de detección en ticks por instrumento.
    F. MODELOS
    - EnableModelA.
    - EnableModelB.
    - ModelBEnableLvn.
    - ModelBEnableEdge.
    - ModelBEnableFvg.
    - MaxAttemptsPerZone.
    - StopBufferTicks por instrumento.
    - MaxStopTicks por instrumento.
    - MinRR.
    - TargetMode.
    G. RIESGO Y MT5
    - RiskMode.
    - RiskAmount.
    - RiskPct.
    - AccountSize.
    - TpRMultiple.
    - ExitMode.
    - Ema15Length.
    - MaxTradesPerDay.
    - MaxDailyLoss.
    - Mt5Mode.
    - Mt5SymbolMapping por NQ/ES/YM.
    - MaxSlippage.
    - Mt5VolumeStep y límites leídos del broker.
    No compartir automáticamente un parámetro bruto de volumen entre instrumentos.
    Si se usa un mismo porcentaje normalizado, dejarlo como parámetro general solo
    después de comprobar que su normalización es comparable.
    =============================================================================
    19. CSV Y AUDITORÍA
    =============================================================================
    Guardar CSV separado o con columna Instrument para evitar mezclar instrumentos.
    Campos mínimos:
    - Timestamp UTC, ET y CDMX.
    - InstrumentRoot, ContractSymbol, proveedor y timeframe.
    - TickSize, TickValue, PointValue.
    - CalibrationStatus, CalibrationSessions y versión del perfil.
    - Volumen total, delta bruto, DeltaPct, VolumenRelativo y BurstRatio.
    - Fuente de datos: REAL, MBO, INFERIDO, APROX o NO DISPONIBLE.
    - Eventos de trackers usados y deduplicados.
    - Estado de barrido y puntuación por bloques.
    - LVN/EDGE/FVG: límites, centro, origen, rango y estado.
    - Modelo A o B.
    - Tipo y resultado del gatillo.
    - Entrada, stop, TP, riesgo y resultado.
    - Alertas y canales.
    - Estado MT5 y errores, sin credenciales.
    No llamar "estadística real" a una simulación o señal teórica. El aviso legal
    debe decir:
    "Las señales y estadísticas son informativas y deben validarse. No son un
    registro de operaciones reales salvo que provengan de ejecuciones confirmadas."
    =============================================================================
    20. PRUEBAS OBLIGATORIAS
    =============================================================================
    Crear pruebas unitarias buyside y sellside y datos sintéticos para verificar:
    1. Al abrir NQ se selecciona únicamente el perfil NQ.
    2. Al abrir ES se selecciona únicamente el perfil ES.
    3. Al abrir YM se selecciona únicamente el perfil YM.
    4. Cambiar de NQ a ES no conserva volumen bruto, delta bruto ni buffers de NQ.
    5. TickSize y valor monetario se validan por instrumento.
    6. Instrumento no reconocido bloquea señales automáticas y órdenes.
    7. Calibración insuficiente se muestra claramente y no se llama validada.
    8. Los datos del feed ausentes se marcan APROX/NO DISPONIBLE.
    9. Volumen relativo y DeltaPct se calculan correctamente.
    10. Umbral de contratos de NQ no se reutiliza en ES ni YM.
    11. Perfil de detección fino encuentra un LVN estrecho que el perfil de dibujo
    agrupa.
    12. Los extremos del perfil sin dos picos no se marcan como LVN.
    13. LVN se representa como zona con centro.
    14. LVN confirmado no desaparece por una pequeña fluctuación.
    15. El perfil PIERNA deja de actualizar el volumen después de congelarse.
    16. El LVN manual se ajusta al valle cercano y conserva su origen.
    17. Borrar un rango publica RangeReset y cancela solo setups pendientes.
    18. El dibujo EDGE puede apagarse sin apagar cálculo/publicación.
    19. Detección EDGE mantiene resultado al cambiar el agrupamiento visual.
    20. Cada FVG posterior a barrido tiene análisis aunque coincida con LVN.
    21. Modelo B no muestra gatillo cumplido antes de una confirmación válida.
    22. Modelo A requiere barrido y recuperación según el clasificador.
    23. Reglas sellside son espejo de buyside.
    24. Tracker dibuja igual que su indicador original.
    25. No se duplican marcadores en modo normal.
    26. CVD divergencia aparece en velas CVD, no en velas de precio.
    27. Dos indicadores que publican el mismo evento no lo cuentan dos veces.
    28. No se mandan alertas durante recálculo histórico.
    29. Correo y Telegram no bloquean ATAS.
    30. Perfil de NQ, ES y YM se conserva separadamente tras reiniciar.
    31. MT5 no manda órdenes en modo APAGADO o SIMULACIÓN.
    32. Un mapeo MT5 sin conversión validada no envía órdenes.
    33. Un mismo EventId no produce órdenes duplicadas.
    34. Riesgo, SL, TP y volumen se calculan con el instrumento correspondiente.
    35. La ventana flotante restaura su posición en varios monitores.
    36. No aparecen secretos en logs, CSV ni archivos de configuración sin cifrar.
    =============================================================================
    21. SUBFASES DE ENTREGA
    =============================================================================
    S1. Inspección del repositorio, plan, compatibilidad y documentación.
    S2. Rename visible a SESSIONS SWEEPS PRO y migración sin pérdida de ajustes.
    S3. InstrumentProfile y detección automática de NQ/ES/YM.
    S4. Parámetros independientes por instrumento y calibración de volumen/delta.
    S5. SuiteEventBus por instrumento, latidos y publicación de trackers.
    S6. Conservación del dibujo original y modo de comparación con ATAS.
    S7. Paneles flotantes Thermal y Sessions Sweeps Pro.
    S8. Thermal: perfil fino y LVN entre picos, zonas, scoring, estados e histéresis.
    S9. Thermal: PIERNA, congelación, LVN manual, cambio de rango y publicación.
    S10. EDGE en ticks, control de dibujo y publicación.
    S11. Clasificador de barridos y Modelo A.
    S12. FVG/BFVG, Modelo B y gatillos automáticos/manuales.
    S13. CVD, minigráfico y divergencias sobre velas CVD.
    S14. Alertas, correo y Telegram.
    S15. Puente MT5 con SIMULACIÓN, DEMO y protecciones.
    S16. CSV, documentación, pruebas y lista de validación en ATAS real.
    Después de cada subfase:
    - Compilar.
    - Ejecutar pruebas relevantes y regresión completa.
    - Actualizar PROGRESO.md, PARAMETROS.md, DECISIONES.md y OPERATIVA.md.
    - Entregar archivos modificados.
    - Enumerar pruebas pasadas y fallidas.
    - Enumerar // VERIFICAR pendientes.
    - No avanzar a la siguiente subfase sin aprobación del usuario.
    =============================================================================
    22. CRITERIOS DE ACEPTACIÓN
    =============================================================================
    - El nombre visible es SESSIONS SWEEPS PRO.
    - Solo existen dos prioridades operativas: A y B.
    - El Modelo B, LVN y EDGE reciben la mayor prioridad de desarrollo solicitada.
    - NQ, ES y YM usan perfiles diferentes de volumen, delta, tick y riesgo.
    - No se copian umbrales brutos de un instrumento a otro.
    - El cambio de símbolo selecciona automáticamente el perfil correcto.
    - El panel muestra el instrumento y estado de calibración.
    - Thermal detecta LVN como valle entre picos y dibuja una zona completa.
    - EDGE tiene una opción de dibujo separada del cálculo.
    - Los trackers conservan su representación gráfica original.
    - CVD divergencia queda dibujada en el panel/velas de CVD, no en el precio.
    - Los paneles flotantes se pueden mover y guardar su posición.
    - Las alertas son las definidas en la sección 15.
    - MT5 permanece apagado por defecto y no envía órdenes sin mapeo validado.
    - Todas las pruebas están documentadas.
    - La verificación final se realiza en ATAS real con datos del feed utilizado.
    - Las pruebas simuladas nunca se presentan como prueba de ejecución real.
    =============================================================================
    23. LÍMITES
    =============================================================================
    - No inventar APIs de ATAS, MT5 o del proveedor de datos.
    - No inventar datos de volumen ni delta.
    - No usar valores de NQ en ES o YM.
    - No presentar umbrales provisionales como calibrados.
    - No afirmar una tasa de acierto sin backtest y trazabilidad.
    - Sin datos MBO, iceberg = INFERIDO/PROBABLE.
    - No cambiar la lógica interna de los trackers sin autorización.
    - No dibujar la divergencia CVD en el gráfico de precio.
    - No añadir un tercer modelo operativo.
    - No mandar órdenes en histórico, al recargar o con conexión/perfil sin validar.
    - No tocar módulos fuera del alcance sin indicarlo en el plan.
    - Las DLL simuladas no sustituyen las pruebas en ATAS real.
    - MT5 se prueba primero en SIMULACIÓN, después en DEMO; REAL requiere
    confirmación explícita del usuario.
    ============================= FIN DEL PROMPT ===============================
