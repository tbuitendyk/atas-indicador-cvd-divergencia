# Decisiones tomadas al construir ThermalVolumeProfile

El propietario pidió que la IA respondiera sola todas las preguntas que dejaba abiertas el documento
"Prompt Volumen Profile térmico" y que las decisiones se entregaran junto con el resultado. Estas son, con el
motivo de cada una. Cualquiera se puede cambiar: diga cuál y cómo.

## A. Entrega y estructura

1. **Solución propia de Visual Studio**: `ThermalVolumeProfile\ThermalVolumeProfile.slnx`, en su propia carpeta del
   mismo repositorio (como IndicadorCompleto), autónoma (no hereda la configuración de la raíz). Es un indicador
   separado para probarlo antes de unirlo a IndicadorCompleto, como dice el documento.
2. **Dos archivos en lugar de uno.** El documento pide un único `ThermalVolumeProfile.cs`. Se separó en:
   - `MotorTermico.cs`: todo el cálculo (perfil, POC/VA, EDGE, SPIKE, CLÚSTER, LVN, color, rangos), **sin ningún tipo
     de ATAS**. Así se prueba sin la plataforma y, cuando llegue el momento, se copia tal cual a IndicadorCompleto.
   - `ThermalVolumeProfile.cs`: el indicador de ATAS (parámetros, elección de velas, dibujo, alertas, ratón).

   Entre los dos están todas las `#region` pedidas (Parámetros, Perfil, VA, Edge, Spike, Cluster, LVN, Render,
   Alertas, Utilidades). Todo comentado en español.
3. **Nombres**: en el código, los nombres en inglés del documento (`RangeMode`, `ClusterGapTicks`…). En la ventana de
   ajustes de ATAS se ven en español ("Modo del rango", "Distancia máxima entre espigas (ticks)"…).
4. **Todo el texto del gráfico en ASCII** (etiquetas y barra de herramientas), como pide el documento para las
   etiquetas: "CLU ARRIBA 15435.50", "EDGE ABAJO …", "SPK …", "LVN 1 …", "LVN MANUAL …", "POC …", "VAH …", "VAL …",
   "cPOC …". Los ajustes sí llevan acentos.
5. **Instalación**: el documento dice copiar la DLL a `Documentos\ATAS\Indicators`. Esa carpeta **no** funciona (ya
   lo comprobamos con los primeros indicadores): se usa **Add custom indicator** o `%APPDATA%\ATAS\Indicators`.
6. **Referencias**: además de las tres del documento (ATAS.Indicators, OFT.Rendering, OFT.Attributes), se usan
   `Utils.Common` (para escribir en el log de ATAS) y `ATAS.DataFeedsCore` (igual que los demás indicadores). Todas se
   toman de la carpeta de instalación de ATAS y no se copian a la salida.
7. **Cada llamada a ATAS se comprobó** contra el código oficial de ATAS (`AtasPlatform/Indicators`):
   `GetAllPriceLevels()`, `AddAlert(sonido, texto)`, `[SoundComboBoxEditor]`, `[Mask]` para fechas y horas,
   `IndicatorDataProvider.CandlesPanel`, `InstrumentInfo.TimeZoneOffset`, `this.LogWarn`, `this.LogError`.

## B. Las notas del final del documento

8. **"Las líneas amarillas que yo marque de manera manual, que las tome en cuenta como LVN."** En el código oficial
   de ATAS no hay forma de que un indicador lea las líneas dibujadas con las herramientas de dibujo de ATAS. Por eso
   el indicador trae su **propia herramienta LVN**: botón **LVN** de la barra y luego un clic en el precio → línea
   amarilla; un clic sobre una línea existente la quita. Los precios quedan guardados en el ajuste *LVN manuales*
   (también se pueden escribir a mano, separados por `;`). Un LVN manual **cuenta como LVN**:
   - se dibuja siempre (aunque los LVN automáticos estén apagados o fuera de la zona de búsqueda), etiqueta
     `LVN MANUAL precio`;
   - anula los LVN automáticos que tenga a menos de *Separación mínima* (8 ticks) y no cuenta para el máximo de 5;
   - suena con *Alerta al tocar un LVN*.

   Color: amarillo canario (255, 239, 0), configurable. El POC también es amarillo (lo pide el documento); se
   distinguen por la etiqueta, y los dos colores se pueden cambiar.
9. **"Que se pueda seleccionar a mano."** Se interpretó como elegir **con el ratón** lo que el documento pide por
   fechas y precios. Barra de herramientas sobre el gráfico (arrastrable):
   - **RANGO**: se arrastra de la vela inicial a la final → modo *Fecha y hora* con esas dos velas. Si se llega a la
     última vela, el perfil sigue en vivo.
   - **ZONA**: se arrastra un rectángulo → zona de búsqueda (precios y fechas del rectángulo).
   - **LVN**: líneas amarillas manuales (punto 8).
   - **PERFIL**: muestra u oculta el perfil.
10. **Visible o no** (ajuste y botón PERFIL; las líneas y etiquetas siguen), **delante o detrás de las velas** (por
    defecto **detrás**), y **de izquierda a derecha o de derecha a izquierda** (ajuste *Dirección de las barras*:
    automática según el anclaje, o forzada en un sentido).
11. **"Que sea configurable todas las notas que añade."** Cada tipo de nivel tiene su interruptor de cálculo, su color
    y su interruptor de **etiquetas**; además hay un interruptor general de etiquetas, el tamaño de letra y la barra de
    herramientas se puede ocultar.

## C. Fechas, horas y temporalidades

12. **Fechas que escribe el usuario** (desde/hasta del perfil y de la zona) = **hora del gráfico**, la que muestra
    ATAS en el eje de tiempo (`InstrumentInfo.TimeZoneOffset`). La herramienta RANGO también las escribe así.
13. **Horario de sesión 09:30-16:00**: en **hora de Nueva York** por defecto, con su cambio de horario (es el horario de
    Nueva York). Ajuste *Horario de sesión en*: Nueva York u hora del gráfico. Admite sesiones que cruzan la medianoche.
    Se toma la sesión más reciente que tenga velas.
14. **Día / Semana / Mes**: por **fecha operativa** = fecha de Nueva York 7 horas después. Así la sesión de Globex que
    abre a las 18:00 de Nueva York cuenta para el día siguiente (como en CME) y una sesión diurna, para su día. La
    semana empieza el lunes. No depende de la configuración de sesiones de ATAS.
15. **Todas las temporalidades y tipos de gráfico**: solo se usa la hora de apertura de cada vela (`candle.Time`) y
    búsqueda binaria; ninguna suposición de duración. En gráficos de ticks con varias velas en el mismo segundo
    también funciona (probado).
16. **"Hasta la vela actual" apagado** en los modos que llegan al presente (últimas N, sesión, día…): el perfil se
    queda en la última vela **cerrada** y no cambia en vivo.
17. **Valores por defecto que el documento no daba**: *Desde* = 20 días antes del día en que se agrega el indicador,
    *Hasta* = mañana (con "hasta la vela actual" activo, el fin no se usa). Zona de tiempo de la búsqueda: últimos 5
    días.

## D. Cálculo (donde el documento dejaba margen)

18. **Unidades**: los parámetros en *ticks* se comparan en ticks (filas × ticks por fila), así siguen valiendo cuando
    AutoRowSize agranda las filas; los parámetros en *filas* (Rows) se aplican en filas. El suavizado del EDGE
    (sigma 1,0) va en filas; el del LVN, en ticks como dice el documento (sigma = LVNSmoothTicks / 2).
19. **Precio de una fila** = su precio inferior (el de `Floor(precio / tamaño de fila)`). Las líneas y etiquetas usan
    ese precio. Con filas de 4 ticks el error máximo es de 3 ticks, el margen que acepta la prueba de validación.
20. **POC**: empate → el más cercano al centro (si sigue el empate, el de abajo). **VA**: método CME; empate entre
    las 2 filas de arriba y las 2 de abajo → se añaden las de arriba; el porcentaje se limita a 50-95.
21. **SPIKE**: se busca alrededor de cada pico local, con anchos de 1 a 3 filas, y se queda la anchura de mayor
    puntuación (n × ratio). **Regla añadida**: la espiga tiene que terminar; si la fila de al lado cumple también la
    condición, no es una espiga sino un bloque. Sin esta regla, la fila de abajo de un bloque de 5 filas pasaba como
    espiga (100 ≥ 1,8 × 52), a pesar del ancho máximo de 3 filas.
22. **CLÚSTER**: las espigas fuera de la zona de búsqueda no entran en ningún clúster. Se eligen los 3 mejores y
    después se ajusta cada extremo al EDGE más cercano a 4 filas o menos; ese EDGE ya no se dibuja aparte.
    *Ocultar las espigas de un clúster* oculta las espigas que caen dentro de la franja de algún clúster.
23. **EDGE**: se calculan siempre (aunque estén apagados) porque el clúster los necesita para ajustar sus líneas;
    solo se dibujan si están activados. El final del perfil no es un EDGE (hacen falta W filas fuera). El máximo de 6
    se aplica después de que los clústeres absorban los suyos.
24. **LVN**: "el pico máximo a cada lado" se toma **hasta encontrar un valor más bajo que el valle** (prominencia
    topográfica). Tomando el máximo de todo el lado, cualquier valle junto al POC tendría una prominencia enorme. Modo
    *Franja*: el valle y las filas de alrededor hasta un 25 % de la subida hacia el menor de sus dos picos.
25. **Perfil Delta**: el largo de la barra es el valor absoluto del delta; una franja de 3 píxeles junto al anclaje
    marca el signo (verde = más compras, rojo = más ventas). Los detectores usan el valor absoluto.
26. **TPO**: cada periodo de *TPOPeriodMinutes* cuenta una sola vez en cada precio que tocó; en temporalidades mayores
    que el periodo (diario con TPO de 30 min), cada vela cuenta como un periodo.
27. **Velas sin volumen por precio**: el volumen se reparte entre su High y su Low (y el Bid/Ask, según el delta de la
    vela); se avisa una sola vez en el log de ATAS.
28. **Zona de búsqueda**: con los dos precios en 0 no hay zona. El filtro de tiempo es un **rango de fechas**
    (desde/hasta), no unas horas de cada día. La herramienta ZONA rellena precios y fechas, pero no activa por sí sola
    el filtro de tiempo. Con el filtro activo, el rectángulo gris cubre solo esas velas.

## E. Rendimiento y tiempo real

29. Durante la carga del historial no se calcula nada vela a vela: todo se calcula una vez al llegar a la última vela
    (124.200 velas de 1 minuto: 0,7 s en las pruebas). En vivo, las velas cerradas quedan sumadas y solo se rehace la
    abierta, como mucho 4 veces por segundo (unos 6 ms cada vez); al cerrar una vela, se suma con sus valores finales.
30. **Rango visible**: ATAS no avisa al indicador cuando se desplaza el gráfico, así que en ese modo el perfil se
    recalcula desde el dibujo cuando cambian las velas visibles. Es la única excepción a "OnRender solo dibuja".
31. Plumas, fuente y los 256 colores térmicos se crean una vez y solo se rehacen si cambia un ajuste de dibujo. Se
    dibujan solo las filas visibles.

## F. Alertas

32. "Toca el nivel" = la vela actual tiene el precio entre su Low y su High. Una sola vez por nivel y por vela. No
    suena durante la carga ni por lo que la vela actual ya tocaba al abrir el gráfico. Texto: `Precio toca CLU
    ARRIBA 15435.50`. El sonido se elige de la lista de sonidos de ATAS (por defecto *alert1*).

## G. Lo que no se pudo comprobar aquí

33. **La prueba de validación del documento** (NQ 60 minutos, 15169-15451) necesita los datos reales del gráfico del
    propietario. Se hizo una **prueba análoga** con un perfil sintético construido según esa descripción y valores por
    defecto: CLU ARRIBA 15435.00 (2 ticks), CLU ABAJO 15206.00 (1 tick), cPOC 15297.00 (2 ticks). Los pasos para hacer
    la prueba real están en LEEME.md.
34. **Compilado contra sustitutos** de las DLL de ATAS (0 avisos, 0 errores) y 123 comprobaciones automáticas. La
    primera compilación contra las DLL reales, el aspecto en pantalla, los editores de fecha y el selector de sonido se
    comprueban en Windows con ATAS.
35. Pensado para **ATAS clásico**, como los demás indicadores (usa los colores de los ajustes de ATAS clásico). No se
    probó en ATAS X.
