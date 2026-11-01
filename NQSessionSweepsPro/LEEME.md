# NQ Session Sweeps Pro (indicador para ATAS)

Vigila los máximos y mínimos de las sesiones y de los días anteriores en el Nasdaq (NQ/MNQ). Cuando el precio los rompe
para tomar liquidez:

1. Detecta el barrido **en el tick exacto** de la ruptura (t0) y avisa.
2. Revisa el Order Flow: **toma real de liquidez**, **falso rompimiento** o **barrido sin liquidez**.
3. Muestra contratos, delta, absorción en 1, 3 y 5 min y velocidad de compra o venta.
4. Avisa antes del cierre de la vela gatillo de 1 y de 5 min.
5. Calcula el contexto H1 y los objetivos (T1, T2, T3).
6. Marca FVG e IFVG, y los LVN (líneas amarillas).
7. Detecta dos entradas: **Modelo A** (rechazo inmediato) y **Modelo B** (continuación tras retroceso a un LVN).
8. Envía un correo en el barrido y en cada entrada.

La lógica de sesiones, barridos, MSS y FVG es la del indicador "Session Sweeps" de LuxAlgo (TradingView). Las horas
son de Nueva York (ET) con el horario de verano ya incluido. Las decisiones que tomó la IA donde el documento dejaba
algo abierto están en **[DECISIONES.md](DECISIONES.md)**.

## 1. Instalar

**Con el acceso directo (recomendado):** cierre ATAS y haga doble clic en el acceso directo del escritorio
(`C:\homs\bic.cmd`): sincroniza GitHub, compila **NQSessionSweepsPro** (lo nombra `instalar.txt`) e instala la DLL.
Abra ATAS y agregue **NQ Session Sweeps Pro** (grupo *Custom*) a un gráfico de **NQ de 1 minuto**.

A mano: abrir `NQSessionSweepsPro\NQSessionSweepsPro.slnx` en Visual Studio 2026, **Release**, **Compilar solución**,
y en ATAS **Add custom indicator** → `NQSessionSweepsPro\bin\Release\net10.0-windows\NQSessionSweepsPro.dll`.

Requisitos: datos de footprint (clúster) del proveedor; datos en vivo para la ruptura en el tick, la velocidad y los
correos; gráfico de tiempo de 1 a 5 minutos (en otros muestra "Session Sweeps funciona en graficos de 1 a 5 min").

## 2. Puesta en marcha (5 pasos)

1. Cargue el indicador en un gráfico de 1 min.
2. En **1. Sesiones**, elija el modo de cada sesión (completa, Kill Zone o personalizado) y compruebe el premercado.
3. En **13. Correo**: su correo en *Para*, su usuario de Gmail y la **contraseña de aplicación** (ver sección 7).
4. Marque **Enviar correo de prueba** y compruebe que llega.
5. Deje lo demás con los valores por defecto la primera semana y luego ajuste (sección 10).

## 3. Qué se ve en el gráfico

| Elemento | Significado |
|---|---|
| Sombreado suave con el nombre | Sesión (máximo y mínimo continuos mientras está abierta) |
| Líneas punteadas | Máximo, mínimo (y media) de la sesión cerrada, hasta que se barren o vuelve a empezar |
| Caja naranja / azul intensa | Barrido de un máximo / mínimo: **SWEEP REAL** |
| Caja gris | **FALSO** rompimiento (el precio aceptó el nivel: posible continuación) |
| Caja muy transparente | **SIN LIQ** (toque débil) |
| Etiqueta "SWEEP REAL 8/10" | Clasificación y absorción (también "DUDOSO 5/10", "PENDIENTE") |
| Panel de datos | Último barrido: nivel y hora, contratos, delta, absorción 1/3/5 min, velocidad, estado |
| Líneas amarillas "LVN" | Nodos de bajo volumen (calculados y manuales) |
| Líneas "T1 / T2 / T3" | Objetivos del último barrido |
| Cajas FVG / "FVG post sweep" / "IFVG" | Huecos de valor justo; borde grueso = de desplazamiento tras el barrido; IFVG = invertido |
| Triángulos "A SHORT", "B LONG"… | Entradas del Modelo A y del Modelo B |
| Línea fina | Directriz del retroceso (Modelo B) |
| "CHoCH" | Cambio de estructura (MSS, desactivado por defecto) |
| Tabla arriba a la derecha | Contexto H1 y cada sesión: verde abierta / rojo cerrada, "Abre en" / "Cierra en" / "Fin de semana" |
| Cuadro "Modelo B" | Etapa de la secuencia: esperando impulso, impulso, retroceso a LVN, entrada o cancelado |

Etiquetas de los niveles en el borde derecho ("MAX Londres 18250.25", "MIN Dia anterior …").

## 4. Sesiones y niveles

Horarios por defecto (ET): Asia 18:00-02:00 (Kill Zone 20:00-00:00), Londres 02:00-08:30 (02:00-05:00), **Premercado
08:30-09:30** (zona propia: empieza *PremarketMinutesBeforeOpen* = 60 min antes de *NYOpen* = 09:30), NY AM 09:30-12:00
(09:30-11:00), NY PM 13:30-16:00. Modo *Personalizado* con HH:mm y su zona horaria. Preset **PineOriginal**: los
horarios del Pine (Asia 09-18 Tokio, Londres 08-17 Londres, NY AM 08-13 y NY PM 13-19 ET).

Niveles vigilados: máximo y mínimo de cada sesión (la última completada), PDH/PDL (día 18:00-17:00 ET), días -2 y -3
sin barrer, PWH/PWL y equal highs/lows (estos dos desactivados). Por defecto los barridos se detectan solo en la
**ventana NY** (premercado + NY AM + NY PM). *Modo compatible con el Pine*: replica el Pine (un par de niveles de la
última sesión cerrada, más Londres durante NY AM).

## 5. Barridos y Order Flow

Un barrido nace cuando una vela abre por debajo del máximo L y lo supera (en vivo, en el tick). La caja crece mientras
el precio se mueve dentro del margen (1,3 × ATR21). Se cierra como **FALSO** si una vela cierra más allá de
L + 1,5 × margen, o si pasadas 5 velas el precio sigue más allá del margen; si no, queda como barrido válido.

**Absorción (0 a 10)**, en ventanas de 1, 3 y 5 minutos desde el minuto de t0:

| Condición | Puntos |
|---|---|
| Volumen más allá del nivel >= 2 veces el volumen medio por tick | +2 |
| Divergencia: agresión compradora más allá, pero cierre bajo L o en la mitad baja | +2 |
| Cierre de la ventana de vuelta bajo L | +2 |
| POC de la vela de barrido en su tercio superior | +1 |
| VA de la vela de barrido en su tercio superior | +1 |
| Mucho volumen y poco avance (8 ticks o menos) | +1 |
| Agotamiento: la velocidad cae más de un 50 % desde su pico, o bid stacked arriba | +1 |

8-10 fuerte · 6-7 válida · 4-5 dudosa · 0-3 sin absorción.

**Clasificación**: SWEEP REAL = 150 contratos o más más allá (*MinVolBeyond*; recalibrar para MNQ), absorción de 6 o
más en 1 o 3 min y cierre de vuelta en 5 velas o menos. FALSO = regla de aceptación del Pine, o 2 cierres más allá con
el delta a favor y absorción menor de 4. SIN LIQ = 2 ticks o menos más allá y poco volumen. Si no, DUDOSO.

En el histórico la velocidad es aproximada (el panel dice "aprox"); en vivo se mide con los trades.

## 6. Alertas

1. **Ruptura** en el tick: "NQ rompe MAX Londres 18250.25 a las 08:47:13 ET" (más el correo).
2. **Cuenta atrás 1m** (15 s antes del cierre, si el precio ya volvió): "Vela gatillo 1m cierra en 15 s - precio bajo el
   MAX roto".
3. **Clasificación** al cierre del minuto 1 (y a los 3 y 5 si cambia).
4. **Cuenta atrás 5m**.
5. **Entradas**: "ENTRADA BAJISTA - Modelo A …" y Modelo B (más el correo).
6. IFVG (opcional).

No suenan al cargar el gráfico: solo con datos en vivo.

## 7. Correo (Gmail)

1. Active la **verificación en 2 pasos** en su cuenta de Google.
2. Cuenta de Google → Seguridad → **Contraseñas de aplicaciones**: cree una para "ATAS".
3. Pegue esa contraseña de 16 letras en *Contraseña de aplicación* (con la contraseña normal no funciona).
4. Servidor smtp.gmail.com, puerto 587, SSL activado.
5. *Zona horaria del correo*: para la hora de México, `Central Standard Time (Mexico)`.

Correos: **barrido** (en t0), **actualización** al cierre del minuto 5 con la clasificación y la absorción (activado),
**entradas A y B** y, si se activa, **falso rompimiento**. Uno por evento; si el servidor falla, 2 reintentos y el
error queda en el log de ATAS, sin congelar la plataforma. Su correo y su contraseña quedan solo en los ajustes de ATAS.

## 8. Contexto H1 y proyección

Velas de 1 hora construidas con el gráfico: **H1 Alcista** (máximos y mínimos crecientes), **H1 Bajista** o **H1
Rango** (desempate con la EMA 20). Objetivos al romper: niveles opuestos sin barrer, POC/VAL del día anterior, LVN y
FVG sin mitigar; T1 = el primero a 20 ticks o más, T2 y T3 los siguientes; stop = extremo del barrido + 8 ticks; USD
(NQ 20/punto, MNQ 2/punto) y R:R; "fuera de rango diario" si superan el rango que queda (ADR 10 menos lo recorrido hoy).

## 9. Modelos de entrada

**Modelo A** (barrido de un máximo, corto): el máximo toca un LVN (4 ticks), pin bar (mecha >= 2 cuerpos y >= 60 % del
rango, cierre en el 40 % inferior), POC y VA en el tercio superior, cierre bajo el máximo roto. Con las 5, esa vela es
la vela gatillo; si cumple LVN, POC y VA pero no cierra abajo, la primera de las 5 siguientes que cierre abajo. Entrada
en el cierre; stop = máximo del barrido + 8 ticks; "Confluencias 5/5".

**Modelo B**: impulso (1,5 × ATR21 o FVG bajista post sweep) → retroceso que toca un LVN (o IFVG/FVG) → una vela cierra
rompiendo el LVN (obligatorio) con 3 o más confluencias: absorción en el techo, subasta terminada, 3 velas con el POC
arriba, ruptura de la directriz, giro fuerte del delta, toque de IFVG/FVG. Stop = techo del retroceso + 8 ticks. Se
cancela si cierra por encima del máximo del barrido o pasan 90 velas. Todo en espejo para los largos.

## 10. Ajustes recomendados

| Situación | Qué cambiar |
|---|---|
| Demasiados barridos | Subir *MinVolBeyond* o dejar la ventana NY |
| Ningún SWEEP REAL | Bajar *MinVolBeyond* (calibrar con el volumen normal de su horario) |
| Opera MNQ | Recalibrar *MinVolBeyond* y *Valor del punto* = MNQ |
| Días de noticias | Margen 1,5 e impulso del Modelo B 2,0 |
| Pocas señales B | Confluencias mínimas 2 (menos filtro, más riesgo) |
| Demasiados LVN | Bajar el máximo de LVN |
| Sesiones desplazadas 1 hora | Revisar la zona horaria; si hace falta, el ajuste manual en horas |
| Las cajas no coinciden con TradingView | Activar el modo compatible con el Pine y el preset PineOriginal |

## 11. Pruebas

Pruebas del documento (en ATAS): sesiones correctas en marzo y noviembre; Asia cruzando la medianoche; premercado como
zona propia; cajas iguales al Pine el mismo día en modo compatible; la ruptura suena en el tick; cuentas atrás 15 s
antes; ningún aviso al cargar el histórico; el correo de prueba llega y un SMTP caído no congela ATAS; Modelos A y B en
espejo; cambio de NQ a MNQ y de 1 a 3 y 5 min sin errores.

Automáticas (sin ATAS; desde la raíz del repositorio, con el SDK de .NET 10):

```bash
bash NQSessionSweepsPro/pruebas/verificar.sh
```

Compila la solución contra sustitutos de las DLL de ATAS y ejecuta 101 comprobaciones: sesiones y cambios de horario,
niveles, barridos del Pine, Order Flow y clasificación, FVG/IFVG, MSS, ruptura en vivo y cuentas atrás, H1,
proyección, LVN, Modelos A y B, y el indicador real (sin avisos al cargar, alerta y correo en vivo, temporalidad,
correo de prueba, errores de SMTP sin bloquear).

## 12. Limitaciones

- Probado sin ATAS: falta comprobar en Windows la compilación contra las DLL reales, el dibujo, los trades en vivo, el
  envío por Gmail y la comparación con el Pine. Los errores quedan en el log de ATAS (`NQSessionSweepsPro: ...`).
- Absorción, velocidad y subasta terminada dependen de la calidad del footprint; en el histórico son aproximaciones.
- Los objetivos son probabilidades, no promesas. Pruébelo en Market Replay 2 o 3 semanas antes de operar con dinero
  real. La decisión y el riesgo son del trader.
