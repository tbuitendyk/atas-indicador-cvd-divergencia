# Decisiones de la IA — Suite Order Flow Stacker

El documento ("PROMPT: SUITE ORDER FLOW…") pide parar en cada fase y esperar confirmación. Por instrucción del dueño
("just build it, document to follow") la IA hizo todo de una vez y **decidió ella** lo que el documento dejaba
abierto. Aquí está cada decisión con su motivo, para revisarla y cambiarla si hace falta.

## Estructura

1. **Una sola DLL y una carpeta, en vez de `OrderFlowSuite.sln` con dos proyectos (Core + Indicators).**
   - El instalador del escritorio trabaja con "una carpeta = un proyecto = una DLL" (`instalar.txt`), y en ATAS es más
     simple copiar un solo archivo.
   - La separación del documento se mantiene por espacios de nombres:
     - `OrderFlowSuite.Nucleo` es el *Core*: motores sin tipos de ATAS, que se prueban con datos sintéticos.
     - `OrderFlowSuite.Atas` y `Atas/` son los indicadores.
2. **El indicador 0 es el Thermal Volume Profile que ya existe** (instrucción del dueño: "make sure that the thermal
   volume profile + lvn is the one that we just did"). El documento describe otro.
   - La suite compila **el mismo código** (`..\ThermalVolumeProfile\*.cs`, enlazado).
   - Con la constante `SUITE`, la clase se llama `ThermalVolumeProfileLVN` (nombre del documento) y en ATAS se ve como
     "Thermal Volume Profile + LVN (Suite)". Así puede convivir con el `ThermalVolumeProfile.dll` por separado.
   - Un cambio en el Thermal llega a los dos. El instalador recompila la suite si cambia un archivo enlazado (se
     añadió esa regla al instalador).
3. **Nombres en ATAS con "(Suite)"**, por ejemplo "Stacked Imbalance Pro (Suite)": no se confunden con los
   indicadores propios de ATAS (Stacked Imbalance, trackers MBO). Las clases tienen el nombre del documento
   (`StackedImbalancePro`, `IcebergTrackerPro`…).
4. **Sin `[Category]`.** El documento lo pide, pero la categoría oficial de ATAS (`IndicatorCategories`) no está
   comprobada en este proyecto y los otros indicadores funcionan sin ella. Al buscar "Suite" en la lista salen todos.
5. **Sin clase base abstracta (composición).**
   - Los indicadores oficiales de ATAS no usan bases abstractas propias. Una base abstracta podría salir en la lista
     de ATAS o fallar al crearse.
   - Lo común está en `CarcasaSuite` (datos, alertas, CSV, panel) y en `DibujoSuite` (marcadores).
6. **Parámetros definidos una sola vez y generados** (`herramientas/generar_parametros.py` → `Atas/Generado/`).
   - Cada indicador expone los bloques que usa.
   - El **Absorption Master expone todos** (puntuación, setups, gestión, filtros, icebergs, umbrales, sesgo,
     imbalance, stops y barridos): así es independiente, como pide el documento.
   - La tabla de parámetros (`PARAMETROS.md`) se genera del código y una prueba avisa si se desfasa.
7. **Cada indicador ejecuta todos los motores por dentro, pero solo muestra, suena y graba lo suyo.**
   - El sesgo de un iceberg necesita saber de barridos y stops; "con iceberg" necesita los icebergs; "contra iceberg"
     también.
   - Los indicadores de ATAS no comparten datos de forma fiable (lo dice el propio documento).
8. **Solo .NET 10 (`net10.0-windows`)**: es el de la versión de ATAS instalada, igual que los demás indicadores. Sin
   multitarget.
9. **Las enumeraciones duplicadas de ATAS.**
   - `TradeDirection` y `MarketDataType` existen en `ATAS.Indicators` **y** en `ATAS.DataFeedsCore`. La compilación
     real del NQ Session Sweeps falló justo por eso.
   - El código nunca las nombra: compara `ToString()` ("Buy", "Ask", "Delete"…).
   - El único archivo que importa `ATAS.DataFeedsCore` (por `MarketByOrder`) es `*.Mbo.g.cs`.
   - Las firmas de MBO y DOM se copiaron del código oficial de ATAS (`DomV3`, `OrderBookAlerts`):
     - `OnInitialize` + `SubscribeMarketByOrderData`
     - `OnMarketByOrdersChanged`
     - `MarketDepthChanged`
     - `MarketDataArg.ExchangeOrderId` / `AggressorExchangeOrderId`

## Datos en vivo y tiempo

10. **Icebergs, stops, barridos y señales: solo en vivo.** ATAS no tiene MBO ni trades en el historial (3.4). No se
    reconstruyen con las velas: saldrían eventos que no son. Los días anteriores se ven por los CSV (decisión 30).
11. **Un cambio de parámetro no borra lo detectado en vivo.**
    - Los parámetros de icebergs, stops, barridos y Master se aplican al momento, sin recalcular.
    - Los de velas (imbalance, swings, IB, tendencia, `RealTimeOnly`, carga del CSV) piden recálculo. Al recalcular se
      rehacen contexto e imbalance y **se conservan** icebergs, stops, barridos y señales, porque el historial no los
      puede reconstruir.
12. **Reloj de mercado.** DOM, MBO y el reloj de 1 s usan la hora del último trade más el tiempo transcurrido desde él,
    no la del PC. Así todo usa la misma hora que los trades, también en Market Replay.
13. **Reloj de 1 s (temporizador).** Cierra ráfagas, resuelve TRAMPA/CONTINUACIÓN y LIQUIDEZ/RUPTURA por tiempo, pasa a
    AGUANTANDO aunque no haya trades y reevalúa el Master. Los callbacks solo marcan y piden `RedrawChart()`; nunca
    dibujan (4.9).
14. **Un cerrojo por indicador** para todo el motor: los callbacks de trades, DOM y MBO pueden llegar en otros hilos.

## Sesiones

15. **Premercado de NY añadido** (nota del documento: "AGREGAR EL PREMERCADO DE NY 1 HORA ANTES, CONFIGURABLE").
    - `PremarketMinutes` = 60: de 08:30 a 09:30.
    - Umbrales NQ **100 / 300 / 4**, entre Londres y la apertura. MNQ 50 / 150 / 4.
    - Londres pasa a ser 02:00 hasta el premercado. **CALIBRAR.**
16. **Fuera de horario (16:00-18:00)**: el documento no lo cubre. Usa los valores de Asia (40 / 120 / 3), el periodo
    más tranquilo.
17. **Hora de Nueva York con su cambio de horario** (`TimeZoneInfo`, nombre IANA o el de Windows). Las pruebas
    comprueban la apertura a las 13:30 UTC en octubre y a las 14:30 UTC en noviembre.

## Stacked Imbalance

18. **Se calcula con la vela CERRADA** (no repinta). En vivo, la vuelta a una zona NUEVA se ve en el tick, sin esperar
    al cierre: la alerta llega cuando hace falta.
19. **PROBADA exige que el precio haya salido antes de la zona** ("vuelve y respeta"). Una vela que sigue dentro no
    cuenta como prueba.
20. **ROTA = cierre a MinBreakTicks o más** fuera de la zona.
21. **`IgnoreZeroSide`.** El volumen mínimo se aplica siempre al lado dominante.
    - Con el parámetro activado (por defecto), un lado débil a 0 cuenta como 1 contrato: hay imbalance si el dominante
      llega al mínimo, sin "infinitos" con 1-2 contratos.
    - Desactivado, un nivel con un lado a 0 no cuenta.
22. **El ratio que se muestra es la media** de los niveles de la zona (el lado débil a 0 cuenta como 1).
23. **"con iceberg" publica un cambio**: una alerta y una fila de CSV. Es información nueva que interesa.

## Icebergs

24. **NATIVO.** Recarga = la misma orden con **peor prioridad** y una ejecución vista: un trade con su identificador
    pasivo, o un trade en ese precio en los 500 ms anteriores.
    - Peor prioridad sin ejecución = el dueño la modificó; no es recarga.
    - Lo ejecutado se cuenta por ciclos: lo mayor de los trades atribuidos, de las reducciones del MBO y del tamaño
      mostrado.
    - Si el feed manda la recarga sin la reducción previa, se cuenta el tamaño mostrado.
25. **Llenado o cancelación** (para SINTÉTICO y CANCELADO): una orden borrada cuenta como llenada si hubo un trade en
    ese precio, del lado agresor contrario, en los 500 ms anteriores.
    - Sin trade es cancelación. Por eso **cancelar y reponer** (spoofing) **no** es un iceberg; la prueba lo comprueba.
26. **INFERIDO.**
    - Ratio = ejecutado / máximo visible en el DOM en ese nivel durante el evento.
    - Recargas = las subidas del visible después de ejecutarse, o como mínimo (ejecutado / visible) − 1.
    - **No exige las recargas mínimas**: el DOM suma muchas órdenes por nivel y las recargas solo se estiman.
    - Sí exige el ejecutado mínimo de la sesión, el ratio y el desplazamiento.
27. **Un solo iceberg vivo por lado y precio.** Si dos métodos lo detectan, se suman. El modo queda en el más fiable
    (NATIVO > SINTÉTICO > INFERIDO).
28. **Parámetros añadidos:**
    - `ActiveRefillSeconds` = 10: el documento dice "recarga en los últimos N segundos" sin dar N.
    - `MaxTrackedOrders` = 20000: límite de memoria que pide la sección 15.
    - `BiasLevelToleranceTicks` = 4: la distancia "en un extremo" que el documento no fija. Se llama así porque
      `LevelToleranceTicks` ya es de los barridos.
29. **Estados.**
    - AGUANTANDO no vuelve a ACTIVO: si sigue recargando es que sigue aguantando.
    - ACTIVO se mantiene hasta otro estado.
    - Se publica (alerta y CSV) al nacer, al cambiar de estado, al ganar el `*` y al cambiar de sesgo. Las recargas y
      el volumen se actualizan en el texto sin sonar.
30. **Sesgo.**
    - **GIRO**, si se cumple una de estas:
      - El iceberg está a la tolerancia de un extremo **de su lado**: compra = VAL, LVN o mínimo de la sesión; venta =
        VAH, LVN o máximo.
      - Hay un barrido o una toma de stops del lado contrario entre (inicio − 30 s) y su última actividad.
    - **CONTINUIDAD**: tendencia a favor (lado de la VWAP de la sesión RTH, o pendiente de la EMA) y retroceso a POC,
      VWAP o una zona de imbalance del mismo lado.
    - "El delta va en contra y el precio no avanza" ya está en la definición de iceberg.

## Stops y barridos

31. **Cascada.**
    - Una ráfaga por lado: los trades agresivos del mismo lado en `CascadeMaxMs` desde el primero.
    - Tiene que **avanzar** en su dirección.
    - La zona asociada es la que la ráfaga cruza (± tolerancia); primero las de su tipo (mínimos para ventas, máximos
      para compras).
    - Parámetro añadido `RequireZone` = Sí: solo cascadas en una zona, para no llenar el panel.
32. **TRAMPA / CONTINUACIÓN** respecto al precio de la zona tomada. La dirección del nuevo impulso:
    - TRAMPA → contraria a la cascada.
    - CONTINUACIÓN → la misma.
    - EVALUANDO mientras no se sabe.
33. **Zonas probables:**
    - PDH / PDL: día de 18:00 a 17:00 NY.
    - ON: de 18:00 a 09:30.
    - IB: desde las 09:30 (`IbMinutes`); cuenta como zona cuando termina.
    - Swings: pivote de N velas a cada lado; los 6 últimos sin superar.
    - Números redondos: los 2 de arriba y los 2 de abajo.
    - El aviso "Zona probable de stops … a N ticks" es solo de panel (no suena ni se graba) y se rearma al alejarse el
      precio 3 veces la tolerancia.
34. **Barridos.**
    - Con identificador de agresor, la orden se cierra 100 ms después de su último trozo.
    - Sin él, se agrupan los trades del mismo lado y el mismo milisegundo.
    - Primero se mira CONTRA ICEBERG; después un nivel **cruzado** (empezar a la tolerancia o antes y pasarlo).
    - El precio de un iceberg del lado contrario (vivo, o roto hace menos de 1 min) **también es un nivel**: así el
      barrido que rompe un iceberg puede ser la RUPTURA del setup D.
35. **`ReferenceLevels` como cinco casillas** (`RefPdhPdl`, `RefIbhIbl`, `RefOnhOnl`, `RefSwings`, `RefVahVal`): el
    editor de ATAS no garantiza las enumeraciones *flags*. Parámetro añadido `OnlyAtReferenceLevels` = Sí.

## Absorption Master

36. **Setups:**
    - A y B se confirman con el iceberg **AGUANTANDO**. Antes salen como **POSIBLE SETUP … esperando**.
    - Si un mismo iceberg cumple A y B, gana **B** (máxima calidad).
    - **Una señal por iceberg o por zona.**
    - C exige la zona de imbalance en el LVN de los últimos 5 min (parámetro añadido `SetupCZoneMaxAgeSeconds` = 300).
    - D: iceberg ROTO en los últimos 60 s y barrido de RUPTURA a ±30 s de la rotura.
37. **Giro del delta:** el delta de los últimos 10 s (un tercio de `EventContextSeconds`) a favor y el de los 30 s
    anteriores en contra. Con dos ventanas iguales, la absorción y el giro caen en la misma ventana y se anulan.
38. **Puntuación.**
    - El "+10 si HighProb" es un peso propio (`WeightHighProbBonus`).
    - La suma se limita a 100.
    - Para D, "barrido o stops previos" es el barrido de ruptura e "iceberg válido" el iceberg roto.
39. **Gestión.**
    - Entrada = referencia ± `EntryConfirmTicks`.
    - Stop = el extremo (iceberg o barrido, el más alejado) ± `StopBeyondTicks`.
    - Objetivo = el nivel de perfil más cercano a 1 tick o más de la entrada.
    - Si no hay ninguno, R:R fijo: parámetro añadido `TargetRewardRisk` = 2. `TargetMode` también permite R:R fijo
      siempre.
40. **"Iceberg en contra de tendencia fuerte sin barrido previo"** (`BlockAgainstTrend` = Sí): GIRO contra la
    tendencia (VWAP o EMA, decisión 30) sin barrido ni stops previos. "Fuerte" no tiene medida en el documento; se
    usa la tendencia tal cual.
41. **Cooldown por lado.** Las candidatas en enfriamiento no salen ni como "posible".
42. **El Master se evalúa** cuando se publica algo nuevo y, con el reloj, como mucho una vez por segundo (no en cada
    trade: la apertura de NY tiene miles por segundo).

## Texto, panel y alertas

43. **Sin emojis ni estrellas.** ATAS dibuja con Arial, que no tiene 🧊 🛑 🧹 ⭐ ▦ ✔ ✖ ⏳.
    - `*` = alta probabilidad.
    - `OK` / `NO` / `..` en el checklist.
    - La línea empieza por la palabra del evento (ICEBERG, STOPS, BARRIDO, STACKED IMB, SEÑAL).
    - Los acentos y la Ñ sí se ven.
44. **Paneles en esquinas distintas por defecto** y con desplazamiento: el documento pone todos arriba a la derecha y se
    taparían. Se añadieron `InfoPanelOffsetX` / `InfoPanelOffsetY`.
45. **Una alerta por `Id|Version`.** Cada publicación tiene versión nueva; nunca suena al recalcular.
46. **Modo Mínimo:** panel y la flecha del Master. **Marcadores** añade además la entrada, el SL y el TP de la última
    señal (como el ejemplo del documento).

## Grabación (CSV)

47. **Columnas del documento + `InicioUTC, StopLoss, Id, Version, Texto`**, para volver a cargarlos (una fila por
    publicación; al cargar se toma la última versión de cada Id).
48. **Fecha del archivo = día de Nueva York.** UTF-8 con BOM (Excel muestra los acentos). Separador **coma** y punto
    decimal (configuración de México y EE. UU.); el manual explica cómo abrirlo.
49. **Cada indicador graba sus eventos.** Con varios en el mismo gráfico, el archivo del día se comparte con un cerrojo
    por archivo (mismo proceso de ATAS). Si Excel lo bloquea, las filas esperan en la cola y se reintenta.
50. **Recarga:** los eventos grabados se dibujan en gris con "(CSV)" en los modos Marcadores y Completo. En Mínimo no hay
    dibujo y el panel solo lista lo reciente, así que no se ven allí.
51. **El "Resumen diario y semanal" (13.5) no se hizo**: el propio documento lo llama "propuesta para añadir al prompt".
    Las columnas del CSV ya permiten hacerlo después.

## Pruebas

52. **159 comprobaciones sin ATAS** (`pruebas/verificar.sh`).
    - Antes, la solución se compila contra sustitutos de las DLL de ATAS: 0 avisos, 0 errores.
    - Cubren:
      - los motores;
      - los cinco indicadores reales (historial sin alertas, vivo, MBO, modos, CSV, recarga, parámetros, MNQ);
      - la historia del documento.
    - **La compilación contra las DLL reales de ATAS y el MBO de Rithmic solo se pueden comprobar en Windows**: ver la
      lista de [PROGRESO.md](PROGRESO.md).
