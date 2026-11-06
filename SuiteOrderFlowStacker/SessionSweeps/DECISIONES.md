# Session Sweeps Pro v2.0 — Decisiones de la IA

El propietario delegó todas las decisiones ("tienes que hacer las propias decisiones -- hágalo", 2026-11-06). Aquí
están las que el documento maestro y el anexo dejaban abiertas. **Léalas antes de cambiar el comportamiento.** El plan
completo y cómo se aplica cada punto del anexo: [PLAN.md](PLAN.md).

## Alcance y forma

1. **Nombre y DLL.** "Session Sweeps Pro (Suite)", 7.º indicador de `SuiteOrderFlowStacker.dll` (clase
   `SessionSweepsPro`). El NQ Session Sweeps Pro no se toca y se puede tener a la vez. Misma DLL que los seis de la suite
   porque el bus de eventos es un estático compartido (patrón ya probado por el Indicador Completo).
2. **Base del motor:** el del NQ (sesiones, niveles, Pine, H1, MSS, FVG) copiado a `SessionSweeps/Nucleo` con su
   propio espacio de nombres; sin su perfil LVN ni los Modelos A/B. La clasificación del NQ (Real/Falso/Dudoso) queda
   como dato informativo (CONFLICTO PINE); manda el clasificador v2.
3. **Solo gráfico de 1 minuto.** Las barras de volumen se arman por minuto; 5m, 15m y H1 se agregan por dentro.
4. **Documentos que no llegaron** (Prompt_Clasificador_Barridos_Premium, Prompt_Integrado, Fase 10): el maestro dice
   que los sustituye; se trabajó con el maestro + el anexo.

## Datos

5. **Trades del historial:** `RequestForCumulativeTrades` desde las 18:00 ET del día (como TapePattern y DomStrength
   oficiales), una vez por instrumento y día, y un solo recálculo al llegar. Sin respuesta: reconstrucción APROX desde
   el footprint de 1 minuto (apertura → extremo más cercano → el otro → cierre, con el volumen y el delta de cada
   precio). El panel dice TICKS o APROX.
6. **Trades entre recálculos:** se guardan en un almacén que sobrevive al recálculo; los que llegan mientras se
   recalcula entran antes del primer trade en vivo del motor nuevo (sin perder ni duplicar).
7. **LVN del Thermal:** su hora es la de publicación, así que entran **al terminar de cargar el historial** (antes, el
   historial los daría todos por mitigados). Después, cada cambio llega en vivo.
8. **Fuentes que cargan tarde:** si un indicador de la suite empieza a latir después de que Session Sweeps cargó y trae
   eventos del historial, se recalcula una vez (como mucho 3 por instrumento).
9. **Recarga = CSV (7.7, T16).** Icebergs, stops y sweeps de la suite solo existen en vivo (ATAS no guarda MBO), así
   que el historial no puede reproducir los bloques B y C. Los barridos ya congelados del día toman su estado del CSV:
   se emparejan por lado, nivel (1 tick) y T0 (2 minutos); quedan pendientes hasta su hora de congelación y aplican
   cada fila a su hora. Pasada la última fila, manda el precio. Las novedades del historial nunca suenan ni se graban.

## Clasificador y modelos

10. **"Super big trade" (MOD3):** el documento no da umbral → `SuperBigTradeMinContracts` = 150 (calibrar).
11. **Noticias:** no hay calendario → `NewsTimes` = 08:30;10:00;14:00 NY con `NewsBlockMin` = 5.
12. **MNQ:** `VolumePerBarMnq` = 1000 aparte del NQ (500).
13. **Certificada:** solo con MBO y el iceberg AGUANTANDO; inferido = "Probable". Así lo pide la regla 1.3.
14. **Entrada simulada al cierre de la vela gatillo.** El cruce de la EMA 15 del anexo queda como confluencia, no como
    segunda forma de entrada.

## Panel, dibujo y GATILLO

15. **Panel flotante = ventana WPF** (ATAS clásico), con el patrón de la ventana del Indicador Completo. El contenido lo
    arma el núcleo (sin WPF, probado); la ventana solo lo pinta. Alternativa: el mismo contenido dentro del gráfico.
16. **CVD:** indicador aparte ("Session Sweeps CVD (Suite)") porque un indicador dibuja en un solo panel. Las
    divergencias se dibujan como tramos entre pivotes (nunca líneas verticales). Una divergencia detectada no cambia
    (sus pivotes ya tienen las velas de la derecha cerradas); la micro sale cuando aparece la macro que la contiene.
17. **Cajas del GATILLO:** las dibuja el Thermal que las pidió; en Session Sweeps vienen apagadas (para no dibujarlas
    dos veces en el mismo gráfico).
18. **FVG / liquidez interna / externa desde el panel del Thermal:** cambian los ajustes de Session Sweeps por el bus
    (un solo lugar donde vive el valor).

## Alertas, correo y CSV

19. **Alertas del NQ que se retiran:** clasificación Real/Falso y entradas de los Modelos A/B. Se quedan las del tick
    (ruptura, cuentas atrás) y la de IFVG. Las nuevas son las de la sección 13.
20. **Correo:** solo en el gatillo VÁLIDO sobre liquidez **externa** (la "toma de liquidez externa válida con entrada,
    SL y TP" del anexo). Destinatario por defecto `uniontradingacademy@gmail.com`. Sin usuario o contraseña SMTP no se
    intenta enviar. Los correos del NQ en t0 y en W5 vienen apagados.
21. **CSV:** dos archivos por día operativo (barridos y modelos), una fila por congelación y por transición; el gatillo
    manual se graba cuando cambia su respuesta (Origen MANUAL). Además de las columnas de la sección 14 llevan la hora
    de la fila y `T0Utc` con precisión completa para restaurar.
