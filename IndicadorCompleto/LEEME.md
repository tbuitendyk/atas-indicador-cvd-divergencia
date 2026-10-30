# Indicador Completo (Indicador Combinado1)

Indicador de análisis para ATAS que lee la estructura de 1 hora, detecta barridos de liquidez, construye mapas LVN a
partir del volumen real por precio y confirma entradas con dos modelos (M1 y M2), con el riesgo calculado en
micro contratos, gestión simulada, CVD con divergencias, una ventana flotante con el reporte y estadística.

**No envía órdenes.** Todo lo que muestra es análisis; los resultados de la estadística son **simulados** sobre las
velas, no operaciones reales.

Está en su **propia solución**, separada de los otros indicadores: `IndicadorCompleto\IndicadorCompleto.slnx`.

Contenido: [1. Compilar](#1-compilar) · [2. Instalar y preparar los gráficos](#2-instalar-y-preparar-los-gráficos) ·
[3. Leer el gráfico](#3-leer-el-gráfico) · [4. La ventana del reporte](#4-la-ventana-del-reporte) ·
[5. Ajustes](#5-ajustes) · [6. Cómo decide](#6-cómo-decide) · [7. Limitaciones](#7-limitaciones) ·
[8. Pruebas](#8-pruebas-para-desarrolladores)

---

## 1. Compilar

1. En Visual Studio 2026: **Archivo → Abrir → Proyecto o solución** y elegir
   **`IndicadorCompleto\IndicadorCompleto.slnx`** (no `Indicadores.slnx`, que es la de los otros indicadores).
2. En la barra de herramientas, cambiar **Debug** por **Release**.
3. **Compilar → Compilar solución** (Ctrl+Mayús+B). Debe terminar con *0 errores*.
4. La DLL queda en:

   ```
   IndicadorCompleto\bin\Release\net10.0-windows\IndicadorCompleto.dll
   ```

Desde PowerShell, en la carpeta del repositorio, lo mismo es:

```powershell
dotnet build IndicadorCompleto\IndicadorCompleto.slnx -c Release
```

Igual que los demás, toma las librerías de ATAS de `C:\Program Files (x86)\ATAS Platform`. Si ATAS está en otra
carpeta, la compilación se detiene con un mensaje en español: cambie `ATAS_BASE` en
`IndicadorCompleto\Directory.Build.props`.

## 2. Instalar y preparar los gráficos

**Solo ATAS clásico.** La ventana flotante usa WPF y ATAS X rechaza las DLL que usan WPF; en ATAS X no cargará.

1. En la ventana de indicadores de un gráfico, **Add custom indicator** → elegir `IndicadorCompleto.dll`. (O copiarla
   a `%APPDATA%\ATAS\Indicators` y pulsar el botón de recarga.) Si ATAS tenía cargada una versión anterior de la DLL,
   cierre ATAS antes de copiar la nueva.
2. La DLL trae **dos** indicadores en el grupo **Custom**:
   - **Indicador Completo** — el principal (dibujo en el gráfico y ventana del reporte).
   - **Indicador Completo CVD** — panel aparte con velas CVD y las divergencias.

### Maestro y visores

Todo se calcula **una sola vez**, en el gráfico **maestro**, que debe ser de **1 minuto** (NQ o MNQ, con datos de
volumen por precio: un gráfico de footprint/clúster o cualquier gráfico de 1 minuto con datos de ticks). Los demás
gráficos del mismo instrumento son **visores**: dibujan lo que calculó el maestro, sin calcular nada.

| Gráfico | Indicador | Ajuste *Rol* |
|---|---|---|
| NQ 1 minuto (obligatorio) | Indicador Completo | *Automático* (o *Maestro 1M*) |
| NQ 5 minutos (opcional) | Indicador Completo | *Automático* (o *Visor 5M*) |
| Cualquiera (opcional) | Indicador Completo CVD | — |

- En *Automático*, el primer gráfico de 1 minuto se vuelve maestro y los demás, visores.
- Maestro y visores se emparejan por **instrumento** y por el ajuste **Grupo compartido** (por defecto `A`). Para
  analizar dos instrumentos o dos configuraciones a la vez, use grupos distintos.
- El recuadro de estado del gráfico dice qué papel tomó: `MAESTRO 1M · grupo A · NQZ6`, `VISOR 5M · …`, o un aviso
  como `EL MAESTRO EXIGE UN GRÁFICO DE 1 MINUTO: no calcula`.
- Los **ajustes de cálculo** (riesgo, estructura, LVN, modelos…) que cuentan son los del **maestro**. En los visores
  solo cuentan los de dibujo.

**Historial.** Los mapas LVN usan hasta **20 sesiones** de Nueva York. Cargue en el gráfico maestro al menos
**30 días** de historial de 1 minuto (más es mejor; 90 días funcionan bien). Con menos días el indicador funciona,
pero los primeros mapas tienen poca evidencia.

**Antes de la primera señal:** en el maestro, active **1. Riesgo → Valores monetarios verificados** después de
comprobar el valor del tick, la comisión y el deslizamiento (ver [5. Ajustes](#5-ajustes)). Mientras esté desactivado,
**ninguna señal se admite**: el reporte lo dice en rojo.

## 3. Leer el gráfico

### Mapa LVN (lo que se pidió corregir)

Al abrir Nueva York (09:30 NY) se crea el **mapa LVN de la sesión** y queda **congelado** hasta la sesión siguiente:
no se mueve durante el día.

| Color | Significado | Cuántos |
|---|---|---|
| **Amarillo** | **ALTA PRIORIDAD**: los mejores LVN del mapa; son los únicos objetivos de TP | 3 (ajustable) |
| **Naranja** | **SECUNDARIO**: LVN válidos de menor puntuación | 5 (ajustable) |
| Gris | Menor: el resto (ocultos por defecto) | — |

- Los LVN están separados como mínimo **10 puntos** (ajuste *Separación mínima entre LVN*): ya no se dibujan pegados.
- Cada banda lleva una etiqueta `LVN 12 · ALTA PRIORIDAD · 74`: identificador (se conserva entre días), prioridad y
  puntuación. Las etiquetas no se enciman: si dos quedan demasiado juntas en pantalla (gráfico muy alejado), se
  muestra la de mayor prioridad; la banda de la otra se sigue dibujando.
- Los amarillos se dibujan por encima de los naranjas.

**Puntuación** (de 0 a 100 aprox.):

```
50 × profundidad del valle                         (1 = vacío total de volumen respecto a sus hombros)
+ 10 × sesiones con reacción en el LVN (máx. 3)    (el precio se alejó al menos 40 ticks en 15 min desde él)
+ 5 × perfiles que lo detectan (máx. 4)            (última sesión NY, 5, 10, 15 y 20 sesiones)
+ 10 si reaccionó como soporte y como resistencia
```

Se ordenan por puntuación: los 3 primeros son amarillos, los 5 siguientes naranjas.

### Perfil de volumen de origen

En el borde derecho del gráfico se dibuja el **perfil de volumen** con el que se calcularon los LVN (por defecto,
las **últimas 20 sesiones**; elegible en *9. Dibujo → Perfil de volumen de origen*: última sesión NY, 5, 10, 15 o 20
sesiones, o ninguno). Así se ve con los propios ojos dónde están los valles y se puede juzgar cada LVN. El ancho se
cambia con *Ancho del perfil (píxeles)*.

### Señales y demás marcas

| Marca | Significado |
|---|---|
| Flecha **blanca** con líneas SL/TP | Señal sobre liquidez **EXTERNA** (sesiones, semana anterior, swings 1H) |
| Flecha **naranja** con líneas SL/TP | Señal sobre liquidez **INTERNA** (pivotes dentro del rango 1H) |
| Flecha **gris** con texto `M1 bloqueada: …` | Setup completo pero **bloqueado** (riesgo, RR, instrumento…); motivo completo en el reporte |
| Aspa `×` con `B12 EXT` / `B12 INT` | Barrido de liquidez en el extremo de la mecha (gris tenue si quedó bloqueado) |
| Rectángulo tenue cian / violeta | FVG alcista / bajista, hasta que se invalida |
| Línea continua | EMA 15 (1M por defecto) |
| Línea inclinada | Directriz del retroceso (M1/M2) |
| Línea verde / roja con `div 1M` o `div 5M` | Divergencia CVD alcista / bajista entre dos pivotes |

El **recuadro de estado** (arriba a la izquierda) muestra el papel del indicador, el sesgo 1H y el número de señales.
Se mueve **arrastrándolo con el ratón** por su barra de título, como los paneles de los otros indicadores.

### Alertas sonoras

Tres alertas, cada una con su sonido. Usan el sistema de alertas de ATAS: suena el sonido y el mensaje aparece en la
ventana de alertas de ATAS.

| Alerta | Cuándo suena | Mensaje (ejemplo) |
|---|---|---|
| **Señal** | Señal M1 o M2 confirmada | `SEÑAL M1 LONG EXTERNA · entrada 20005.50 · SL 19995.00 · TP 20069.50 · 10 micros · RR 5.3` |
| **Barrido** | Aviso previo: barrido en evaluación que puede acabar en señal | `BARRIDO B12 DE MÍNIMOS EXTERNA en 20000.00 (…): posible LONG, esperar recuperación y gatillo` |
| **Bloqueado** | Patrón completo que el control de riesgo no admitió | `M1 LONG EXTERNA BLOQUEADA: RR NETO 0.23 MENOR QUE EL MÍNIMO 1.00` |

- **Iconos para silenciar o activar cada una:** en la parte de abajo del recuadro de estado hay tres altavoces
  (*Señal*, *Barrido*, *Bloqueado*). **Un clic** alterna: altavoz verde con ondas = activa; gris con una **X roja** =
  silenciada. Los iconos funcionan en cualquier gráfico del grupo (1M o 5M) y cambian las alertas del maestro.
- También en los ajustes, grupo **10. Alertas sonoras**: activar/desactivar y elegir el **sonido** de cada una de la
  lista de sonidos de ATAS (por defecto *alert1* para señales y *alert2* para las otras dos).
- Suenan solo con velas **en vivo**: nunca durante la carga del historial ni al recalcular.
- Suenan al **cierre** de la vela de 1 minuto que confirma el evento (con un feed retrasado, con el mismo retraso).
- No suenan los barridos bloqueados desde el principio (contexto contrario, fuera de ventana, doble…) ni una señal
  suprimida por haber otra reciente en la misma dirección.

## 4. La ventana del reporte

El maestro abre una **ventana flotante** (se puede mover fuera del gráfico y a otro monitor). Recuerda su posición,
su tamaño y el tamaño de letra.

**Tamaño de letra:** botones **A−** y **A+** arriba a la derecha (o **Ctrl + rueda del ratón** sobre la ventana),
de 8 a 16; empieza en 10. Si aparece la barra de desplazamiento vertical, baje la letra, agrande la ventana o
pliegue secciones (botón **Resumen**).
Se actualiza dos veces por segundo; si pasan más de 3 minutos sin datos nuevos, lo avisa (feed detenido o mercado
cerrado).

**La cabecera dice qué hacer ahora**, en este orden de importancia:

| Cabecera | Significado |
|---|---|
| `SEÑAL CONFIRMADA — M1 LONG EXTERNA` | Hay una señal nueva con su plan (entrada, SL, TP, micros, RR) |
| `CONTEXTO 1H NO DETERMINADO — ESPERAR` | La estructura de 1H todavía no tiene sesgo |
| `ESPERAR RECUPERACIÓN` | Hubo barrido; falta el cierre de recuperación |
| `ESPERAR GATILLO` | Recuperado; falta la vela gatillo con footprint |
| `ESPERAR — SEÑAL BLOQUEADA` | El patrón se completó pero el riesgo no la admite (motivo debajo) |
| `ESPERAR — SIN OPORTUNIDAD ACTIVA` | Contexto válido, sin barrido en curso |
| `SIN DATOS` | El maestro aún no ha recibido velas de 1M |

Una señal vieja se muestra como `SEÑAL ANTERIOR · confirmada hace N min (no es una entrada nueva)`.

Debajo hay diez secciones plegables (1. Contexto 1H … 8. Gestión, Estadística, Eventos). Botones:
**Resumen / Detalle / Eventos** (qué secciones abrir), **Exportar instantánea JSON** y **Exportar estadística CSV**.
Los archivos van a `Documentos\IndicadorCompleto\`. Las horas se muestran en **Ciudad de México** (ajuste
*Zona horaria del reporte*).

Para cerrarla del todo: desactive *Mostrar ventana flotante del reporte* en el maestro (cerrarla con la X también
vale hasta el siguiente recálculo).

## 5. Ajustes

Los ajustes marcados **(propuesto)** son umbrales que el documento de especificación deja como propuesta: sus valores
iniciales son razonables, pero deben confirmarse con la experiencia sobre el gráfico.

| Grupo | Lo más importante |
|---|---|
| 0. Rol, grupo y ventana | *Rol*, *Grupo compartido*, ventana sí/no, *Ventana siempre visible*, zona horaria |
| 1. Riesgo | **Valores monetarios verificados** (obligatorio), riesgo por señal 250 USD, valor del tick 0,50 USD (MNQ), comisión 1,50 USD, deslizamiento 2 ticks, máximo 20 contratos, RR neto mínimo 1, instrumentos permitidos `NQ;MNQ` |
| 2. Estructura 1H | Fuerza de pivote 1H (2), margen estructural (4 ticks), fuerza de pivote 1M/5M (2) |
| 3. Liquidez y barridos | Ventanas Londres y Nueva York activas, Tokio no; cierres anticipados de NY; penetración mínima 4 ticks; regla de recuperación; plazos |
| 4. Absorción | Apagado / Informativo / Obligatorio y sus umbrales |
| 5. Modelos y gatillo | M1 y M2 activos; modelos independientes; posición POC/VA del gatillo (0,60); M2 con o sin FVG obligatorio; anclaje del SL; buffer del SL 12 ticks; anticipación del TP 2 ticks |
| 6. LVN | Banda ±400 puntos, hombros 24 ticks, umbral de valle 0,60, suavizado 2, **separación mínima 10 puntos**, **3 amarillos**, **5 naranjas**, reglas de reacción |
| 7. EMA y gestión simulada | EMA 15 en 1M; **cerrar al cierre de la vela cruzada por la EMA** (activado, nota del propietario); agotamiento y arranque contrario; protección POC |
| 8. CVD | Hora de reinicio del CVD (09:30 NY) |
| 9. Dibujo | Colores (amarillo canario y naranja), LVN menores, mapas anteriores, perfil de origen, EMA, FVG, barridos, bloqueados, divergencias, directrices |
| 10. Alertas sonoras | Señal, barrido y setup bloqueado: activar/silenciar y sonido de cada una (también con los iconos del recuadro de estado) |

**Valor del tick.** El riesgo se calcula en **micro contratos**: con 0,50 USD por tick (MNQ). Si se opera NQ
completo, ponga 5,00 USD. Compruebe también comisión y deslizamiento con su bróker y luego active *Valores monetarios
verificados*.

Cambiar un ajuste de cálculo en el maestro **recalcula todo el historial** (unos segundos con 90 días).

## 6. Cómo decide

Resumen; el detalle completo de cada condición se ve en el reporte, con su estado (cumplida, pendiente, fallida).

1. **Contexto 1H.** Pivotes de 1H (confirmados, sin repintar) → BOS / MSS → sesgo alcista, bajista o sin
   determinar. Solo se buscan barridos **contrarios al sesgo** del lado de la liquidez correcta (en contexto
   alcista, barridos de mínimos para comprar).
2. **Liquidez.** Externa: máximos y mínimos de Tokio, Londres y Nueva York completas, semana anterior y swings de
   1H. Interna: pivotes de 1M/5M **dentro** del rango interno de 1H.
3. **Barrido.** Una vela de 1M cruza **por primera vez** una referencia intacta al menos 4 ticks, dentro de las
   ventanas activas. Si la misma vela barre máximo y mínimo, se bloquean los dos (ambiguo).
4. **Recuperación.** Cierre de vuelta al otro lado del nivel (1M, 5M, la primera o ambas, según el ajuste) dentro
   del plazo.
5. **M1 — recuperación directa.** Tras la recuperación, una vela gatillo direccional con el POC / área de valor del
   footprint en el lado correcto (posición ≥ 0,60). SL detrás del extremo del barrido + buffer; TP en el siguiente
   **LVN amarillo** menos la anticipación.
6. **M2 — ruptura de LVN y retroceso.** Ruptura de un LVN con desplazamiento real (cuerpo y expansión), retroceso al
   LVN, respuesta y gatillo. Variante con FVG si hay un FVG junto al LVN y el precio lo toca.
7. **Riesgo.** Micros = riesgo USD / (distancia SL + costes). Bloquea si: valores no verificados, instrumento no
   permitido, sin LVN amarillo como objetivo, SL del lado incorrecto, objetivo demasiado cerca, cantidad 0 o RR neto
   menor que el mínimo.
8. **Gestión simulada.** SL / TP (si una vela toca ambos, cuenta como SL), salida por cierre cruzado de la EMA,
   agotamiento + arranque contrario, protección del stop en el POC. Resultado en R y en USD netos de costes.
9. **CVD.** Se reinicia a las 09:30 NY; divergencias entre pivotes de la misma sesión.

Todo se calcula con **velas cerradas**: nada se repinta; una señal conserva su precio y su hora originales.

## 7. Limitaciones

Dígalas antes de operar con el indicador:

- **Solo ATAS clásico** (la ventana WPF no carga en ATAS X).
- **Verificado sin ATAS.** El código se compiló (0 avisos, 0 errores) contra sustitutos de las DLL de ATAS, y la
  lógica pasó 126 comprobaciones con datos sintéticos. **No se ha ejecutado dentro de ATAS**: la primera compilación
  contra las DLL reales, el comportamiento de la ventana WPF, el emparejamiento maestro/visores entre gráficos y que
  las alertas suenen se comprueban en su PC. Si algo falla, el registro de ATAS (*Logs*) muestra `IndicadorCompleto: error …`.
- **Estadística simulada**: no incluye cola en la cola, rechazos ni deslizamiento distinto del configurado.
- **Umbrales (propuesto)**: pendientes de calibrar con la experiencia.
- **Feed con retraso**: con un feed retrasado (por ejemplo dxFeed con 15 minutos de retraso), las señales llegan con
  el mismo retraso. Para operar hace falta un feed en tiempo real.
- **Volumen por precio**: sin datos de ticks/footprint en el gráfico maestro, no hay gatillos (el reporte dice
  `FOOTPRINT INCOMPLETO`).
- Rendimiento medido: ~89 600 velas de 1M (90 días) se cargan en unos 5 segundos; cada vela nueva tarda menos de
  1 milisegundo.

## 8. Pruebas (para desarrolladores)

Sin Windows ni ATAS, con el SDK de .NET 10, desde la raíz del repositorio:

```bash
bash IndicadorCompleto/pruebas/verificar.sh
```

Compila la solución contra sustitutos de ATAS y ejecuta las pruebas del núcleo (`pruebas/`): sesiones con cambio de
horario, agregación, footprint, estructura, liquidez, CVD, LVN, M1 y M2 completos con sus bloqueos, gestión simulada,
reporte, JSON/CSV y un histórico de 90 días (estabilidad, no repintado y rendimiento). Debe terminar en
`ALL PASS`.

Arquitectura: `Nucleo/` (lógica pura, sin tipos de ATAS), `Atas/` (indicadores y dibujo), `Ventana/` (ventana WPF),
`pruebas/`.
