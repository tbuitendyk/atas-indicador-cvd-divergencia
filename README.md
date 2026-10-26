# Indicadores ATAS

Indicadores personalizados para la plataforma [ATAS](https://atas.net), en C# para .NET 10 y Visual Studio 2026.
Un solo repositorio con una carpeta por indicador; `Indicadores.slnx` los compila todos a la vez.

| Indicador | Carpeta | DLL |
|---|---|---|
| Indicador de Tendencia Hourly | `IndicadorTendenciaHourly/` | `IndicadorTendenciaHourly.dll` |
| Institutional CVD Trigger & Absorption Engine | `IndicadorCvdDivergencia/` | `IndicadorCvdDivergencia.dll` |
| Velas CVD (panel) | `IndicadorCvdDivergencia/` | `IndicadorCvdDivergencia.dll` (la misma) |
| Footprint Predictor Pro (Sweeps & Context) | `IndicadorFootprintPredictor/` | `IndicadorFootprintPredictor.dll` |

Pasos completos para subir el código a otro repositorio de GitHub, descargarlo en Windows, compilarlo, instalarlo y
recibir actualizaciones: **[docs/GUIA.md](docs/GUIA.md)**.

## Requisitos

- Windows con la plataforma ATAS instalada (versión actual, que usa .NET 10).
- Visual Studio 2026 con la carga de trabajo **Desarrollo de escritorio de .NET** (incluye el SDK de .NET 10).

## Compilar

1. Abrir `Indicadores.slnx` en Visual Studio 2026.
2. Elegir la configuración **Release** en la barra de herramientas.
3. Menú **Compilar → Compilar solución** (Ctrl+Mayús+B).
4. Cada proyecto deja su DLL en `<carpeta>\bin\Release\net10.0-windows\`, por ejemplo
   `IndicadorCvdDivergencia\bin\Release\net10.0-windows\IndicadorCvdDivergencia.dll`.

El proyecto toma las librerías de ATAS de `C:\Program Files (x86)\ATAS Platform`. Si ATAS está instalado en otra
carpeta, la compilación se detiene con un mensaje que lo indica: cambie `ATAS_BASE` en `Directory.Build.props`.

## Instalar en ATAS

1. En la ventana de indicadores de un gráfico, pulsar **Add custom indicator** (abajo a la izquierda) y elegir la
   DLL. O copiarla a mano a `%APPDATA%\ATAS\Indicators` (en *AppData*, **no** en *Documentos*) y pulsar el botón de
   recarga que aparece en la barra de estado.
2. Agregar el indicador (grupo **Custom**) a un gráfico de **1 minuto o menor**.

Detalles y solución de problemas: [docs/GUIA.md](docs/GUIA.md#4-instalar-en-atas).

**Paneles movibles.** Cada panel se mueve **arrastrándolo con el ratón por su barra de título** (la franja de
arriba; el cursor cambia a una mano) y no puede salir del área del gráfico. La posición queda guardada en sus ajustes
*Posición X / Posición Y*. Por defecto los paneles quedan apilados a la izquierda (X 25): Tendencia en Y 60, CVD
Trigger Engine en Y 230 y Footprint Predictor en Y 410. Un indicador agregado a un gráfico antes de esta versión
conserva la posición que tenía guardada.

## Indicador de Tendencia Hourly

Panel sobre el gráfico con la tendencia de **1 hora, 15 minutos y 5 minutos**, calculada a partir de las velas del
gráfico actual (no hace falta abrir tres gráficos). Para cada marco temporal compara el cierre con la vela anterior
del mismo marco:

| Símbolo | Significado |
|---|---|
| ▲ verde | El cierre supera el **máximo** de la vela anterior: alcista |
| ▼ rojo | El cierre cae por debajo del **mínimo** de la vela anterior: bajista |
| (-) negro | El cierre está **dentro** del rango de la vela anterior: en rango |

A la derecha, **Desde** muestra la hora del gráfico en que empezó el estado actual (con la fecha, si fue otro día).

### Opción "Confirmar al cierre" (grupo Cálculo)

- **Activada** (por defecto): solo evalúa cuando cierra cada vela de 1H/15M/5M, comparando su cierre con la vela
  anterior, y mantiene ese estado durante todo el período siguiente. *Desde* es la hora en que cerró la vela que
  confirmó el estado. La fila de 1H necesita al menos dos horas completas en el gráfico; mientras tanto muestra `--:--`.
- **Desactivada**: compara el precio actual con la vela anterior y cambia en tiempo real. Al empezar cada período
  nuevo el estado normalmente vuelve a (-), porque el precio arranca dentro del rango de la vela que acaba de cerrar,
  hasta que rompe su máximo o su mínimo.

### Aviso de gráfico no compatible

Si el gráfico es de tiempo y su período no divide exactamente 5 minutos (por ejemplo 3, 15 o 60 minutos), el panel
muestra *"Aviso: usar un gráfico de 5 min o menor"*: en esos gráficos las filas de 5M y 15M no serían exactas.

## Institutional CVD Trigger & Absorption Engine

Motor de gatillo sobre el **CVD** (delta acumulado de la sesión) para el gráfico de precio, pensado para NQ en
1 minuto. Dibuja una **bandera verde** (compra) o **roja** (venta) en la vela donde se cumplen a la vez cuatro
filtros:

| Filtro | Qué exige en una compra (la venta es el espejo) |
|---|---|
| 1. Macro-divergencia | Un mínimo amplio más bajo que el anterior, con el CVD más alto (swings de *Fuerza macro* velas a cada lado, 12 por defecto) |
| 2. Micro-divergencia | Lo mismo en swings cortos (*Fuerza micro*, 3 por defecto); acepta doble suelo |
| 3. Absorción en mecha | En la vela del mínimo micro: mecha inferior ≥ 40 % del rango **y** ≥ 30 % del volumen de la vela negociado en esa mecha |
| 4. Giro de delta | Una vela posterior a ese mínimo con delta ≥ +400 contratos |

- Todo se evalúa con **velas cerradas**, así que las banderas no se repintan. La bandera queda en la vela que
  completa la confirmación: un swing se confirma cuando cierran *Fuerza* velas a su derecha.
- Bajo cada bandera: el delta del giro y la absorción (mecha % · volumen %). El panel muestra el estado de cada
  filtro, el delta de la vela en curso frente al umbral y el último disparo.
- **Vigencia de las divergencias**: *Por barras* (macro 60 y micro 7, contadas desde que se confirman) o
  *Hasta divergencia opuesta*.
- Escribe el CVD de la sesión en cada vela cuando las velas son lo bastante anchas para que no se superpongan
  (acercar el gráfico).
- **Velas CVD (panel)**, en la misma DLL: las velas del CVD en un panel aparte, con el mismo cálculo y el mismo
  reinicio por sesión.

> El umbral de delta (400), la mecha (40 %) y el volumen en la mecha (30 %) son valores de partida: conviene
> calibrarlos con datos reales de NQ (el 30 % no venía en la especificación). El filtro 3 usa el volumen por nivel de
> precio de ATAS: si el panel muestra *Vol 0%* en todas las divergencias, esos datos no están cargados en el gráfico.

## Footprint Predictor Pro (Sweeps & Context)

Pronóstico de la **siguiente vela** a partir del footprint. Pone un **punto** en la vela cerrada que cumple todo
esto (compra; la venta es el espejo):

1. **Barre liquidez**: penetra un mínimo de swing al menos 2 ticks y cierra de vuelta al menos 1 tick por encima.
2. **Volumen climático**: al menos 2 veces la media de las 20 velas anteriores.
3. **Desequilibrios compradores apilados**: 3 o más precios seguidos en los que el Ask es al menos 3 veces el Bid del
   precio inferior (diagonal). Un precio sin operaciones corta la racha.
4. Cierra **por encima de su POC**, en la **mitad superior** de la vela y con **delta positivo**.

| Punto | Significado |
|---|---|
| Verde grande | Compra tras barrer liquidez **externa** |
| Azul pequeño | Compra tras barrer liquidez **interna** |
| Rojo grande | Venta tras barrer liquidez externa |
| Naranja pequeño | Venta tras barrer liquidez interna |

- **Externa**: el swing (fractal de 3 velas) es el mínimo más bajo, o el máximo más alto, de los 60 minutos
  anteriores (*Pivot Window*). Si no, es **interna**.
- Un nivel deja de contar como liquidez cuando una vela lo barre o cuando cierra más allá de él sin recuperarlo.
- El panel **Estructura de mercado** muestra el contexto: **ALCISTA** tras barrer liquidez externa por abajo y
  **BAJISTA** tras barrerla por arriba, con la hora en que empezó.
- Todo se evalúa con velas cerradas, así que los puntos no se repintan. Usa los datos por nivel de precio (Bid/Ask)
  del gráfico.
- Colores y tamaños de los puntos: en la configuración de cada serie del indicador.
