# Decisiones tomadas al construir NQSessionSweepsPro

El propietario pidió construir las 6 fases de una vez y que la IA decidiera lo que el documento "Indicador de barrido
de liquidez 01112026" dejaba abierto, documentándolo aquí. Cualquiera se puede cambiar: diga cuál y cómo.

## A. Entrega

1. **Carpeta y solución propias**: `NQSessionSweepsPro\NQSessionSweepsPro.slnx`. Se instala con el acceso directo de
   `C:\homs\` (el archivo `instalar.txt` ya lo nombra).
2. **Varios archivos en lugar de uno solo** (el documento pedía un único `.cs`): el cálculo va en `Nucleo/` (sin tipos
   de ATAS, probado sin la plataforma) y el indicador en `Atas/` (ajustes, velas y trades, alertas, correo, dibujo).
3. **Instalación**: no en `Documentos\ATAS\Indicators` (no funciona), sino `%APPDATA%\ATAS\Indicators` o
   *Add custom indicator*; el acceso directo de `C:\homs\` lo hace solo.
4. **Cada llamada a ATAS se comprobó** contra el código oficial: `OnNewTrade(MarketDataArg)` con `Price`, `Volume`,
   `Time` y `Direction` (`TradeDirection.Buy/Sell`), `GetAllPriceLevels()`, `candle.Ask/Bid`, `AddAlert(sonido, texto)`,
   `SecureString` para la contraseña (campo de contraseña en ATAS), `[Mask]` para HH:mm y fechas,
   `[SoundComboBoxEditor]`. No se usa `RequestForCumulativeTrades`: en el histórico el footprint de cada vela da el
   volumen por precio (exacto por minuto en 1 min) y en vivo los trades llegan por `OnNewTrade`.
5. Ajustes en español (los tipos de las listas con nombres en español para que no choquen con tipos de ATAS); todo el
   texto del gráfico, de las alertas y de los correos en **ASCII** (sin acentos), como pide el documento.

## B. Sesiones y niveles

6. **Zonas horarias**: Nueva York, Londres y Tokio con su horario de verano automático (nombres IANA, o los de Windows
   del documento). Probado en marzo y noviembre, cuando EE.UU. y Europa cambian en fechas distintas. Se deja el ajuste
   manual en horas.
7. **"Última sesión completada"**: los niveles de una sesión se retiran cuando esa misma sesión vuelve a empezar (como
   sus líneas en el Pine).
8. **Días anteriores**: N = 3 cuenta el día anterior (PDH/PDL, "Dia anterior") y los días -2 y -3. *PDH/PDL* activa el
   del día anterior; *Días anteriores*, los demás. Un nivel desactivado que el precio supera queda roto, para que no
   reaparezca como "sin barrer".
9. **Equal highs / lows** (desactivado): pivotes de longitud 5 (el documento no la daba) a 3 ticks o menos y sin que
   el precio los haya superado entre medias.
10. **Ventana NY** = los horarios de premercado, NY AM y NY PM (la pausa de 12:00 a 13:30 queda fuera).
11. **Contradicción del documento** (la sección 6 dice que un nivel barrido deja de vigilarse; la 7.2c, que vuelve a
    detectar barridos): en el modo normal, al cerrarse su zona el nivel queda *Barrido* (válido) o *Roto* (falso) y
    deja de vigilarse. El rearme del Pine (*Detectar una vez por sesión*) se aplica en el *modo compatible con el Pine*,
    que además ignora la ventana de detección (el Pine no la tiene) y solo vigila sus dos pares.
12. Se mantienen las rarezas del Pine para ser fiel: en sellside el borde derecho del falso por cierre lejano es
    bar + 1 (en buyside, bar); el rearme ocurre en cualquier vela fuera del margen; sin ATR21 (primeras 21 velas) no
    hay barridos.

## C. Order Flow y clasificación

13. **Ventanas W1/W3/W5** desde la apertura del minuto de t0, con las velas del gráfico: exactas en 1 min; en 3 y 5 min
    se ajustan a velas completas. **Cambio de delta**: en vivo se resta el delta del minuto antes de t0 (medido con los
    trades); en el histórico es el delta de la ventana.
14. **Absorción**: "VolBeyond >= 2 veces el volumen medio por tick" se aplica tal cual (volumen medio por tick de las 20
    velas anteriores). Eficiencia alta = contratos por tick de avance >= 1,5 veces ese mismo volumen medio. *Bid
    stacked*: Bid de un precio >= 3 veces el Ask del precio de encima, 3 seguidos en el tercio superior (en espejo,
    *Ask stacked* abajo).
15. **Velocidad**: en vivo, pico por tramos de 5 s de los trades agresivos más allá del nivel. En el histórico no hay
    trades: se aproxima con las compras (o ventas) más allá en W1 / 60 s y el panel lo marca "aprox".
16. **Clasificación** con prioridad FALSO > SWEEP REAL > SIN LIQ; si no encaja en ninguna, **DUDOSO** (caso que el
    documento no cubría). Suena al cierre de W1 y, en W3/W5, solo si cambia. Si el Pine lo marca falso más tarde, pasa
    a FALSO con su aviso.
17. El VA de vela, en un empate, crece hacia los dos lados (crecer siempre hacia arriba favorecía a los barridos
    buyside: lo encontraron las pruebas).

## D. Alertas, correo, H1 y proyección

18. **Ruptura en el tick**: el trade que cruza L con la vela abierta al otro lado. La alerta y el correo salen en ese
    momento con los datos del minuto hasta t0; la absorción todavía no existe, así que el **correo de actualización al
    cierre de W5 viene activado** (el documento lo dejaba opcional) y lleva la clasificación y la absorción 1/3/5.
19. **Cuentas atrás**: de la vela de 1 min y de la de 5 min que contienen t0, cuando el precio ya está de vuelta y
    quedan TriggerCountdownSec segundos. Se comprueban con cada trade: si no hay ningún trade en esos últimos segundos,
    no suena.
20. Alertas activables por tipo (ruptura, cuentas atrás, clasificación, entradas, IFVG). Ninguna alerta ni correo
    durante la carga del historial.
21. **Correo**: cola en segundo plano, anti-duplicados por clave (nivel + tipo + barra), 2 reintentos de 10 s, errores
    al log. Sin destinatario no se envía nada. SSL en el puerto 587 (STARTTLS).
22. **Contexto H1**: velas H1 alineadas a la hora de Nueva York; pivotes estrictos; desempate con la EMA 20.
23. **Proyección**: niveles opuestos sin barrer, POC y VAL/VAH del día anterior, LVN y bordes de FVG sin mitigar. T2 y
    T3 = los siguientes tras T1 (a más de 1 tick entre sí). "Fuera de rango diario" se marca en cualquier objetivo
    más lejos que el rango restante (ADR 10 menos lo recorrido hoy). En el gráfico se dibujan los del último barrido.

## E. FVG, LVN y modelos

24. **FVG**: lista de todos (como pide el documento) en lugar de una sola caja; por eso no se aplica el borrado del Pine
    cuando aparece un hueco entre velas. Un FVG puede pasar a IFVG aunque ya esté mitigado.
25. **Perfil LVN** por día operativo, separando RTH del resto: "overnight actual" = todo lo que no es RTH del día en
    curso. *Personalizado* elige días completos por fecha. Sigma 1,5 en filas; picos de referencia hasta un punto más
    bajo que el valle (como ThermalVolumeProfile). LVN manuales separados por comas, con punto decimal.
26. **Modelo A**: "toca o cruza un LVN" = el máximo a 4 ticks o menos, o la vela abre por debajo y lo supera. Si la vela
    de barrido cumple LVN, POC y VA pero no cierra de vuelta, la primera de las 5 siguientes que cierre de vuelta es la
    vela gatillo (sin exigir la pin bar). Con *Solo entradas alineadas con H1* se filtra salvo PDH/PDL.
27. **Modelo B** empieza tras un Modelo A o un SWEEP REAL. Impulso y cancelación se miden contra el máximo del barrido
    al empezar la secuencia (fijo). Confluencia a) = absorción de la vela del techo del retroceso con L = el LVN;
    b) = subasta terminada en esa misma vela; d) = pivotes bajos de longitud 2 entre el impulso y la entrada. Si una
    vela rompe el LVN con pocas confluencias, la secuencia sigue esperando otra. Una señal B por barrido.

## F. Lo que no se pudo comprobar aquí

28. Compilado contra sustitutos de las DLL de ATAS (0 avisos, 0 errores) y **101 comprobaciones automáticas** con datos
    sintéticos. En Windows con ATAS falta ver: la compilación contra las DLL reales, el dibujo, que `OnNewTrade` llegue
    en vivo, el envío real por Gmail y la comparación con el Pine en TradingView el mismo día (sección 22 del
    documento).
29. Solo gráficos de tiempo de 1 a 5 minutos; en los demás muestra "Session Sweeps funciona en graficos de 1 a 5 min".
