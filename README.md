# Indicadores ATAS

Indicadores personalizados para la plataforma [ATAS](https://atas.net), en C# para .NET 10 y Visual Studio 2026.
Un solo repositorio con una carpeta por indicador; `Indicadores.slnx` los compila todos a la vez.

| Indicador | Carpeta | DLL |
|---|---|---|
| Indicador de Tendencia Hourly | `IndicadorTendenciaHourly/` | `IndicadorTendenciaHourly.dll` |

## Requisitos

- Windows con la plataforma ATAS instalada (versión actual, que usa .NET 10).
- Visual Studio 2026 con la carga de trabajo **Desarrollo de escritorio de .NET** (incluye el SDK de .NET 10).

## Compilar

1. Abrir `Indicadores.slnx` en Visual Studio 2026.
2. Elegir la configuración **Release** en la barra de herramientas.
3. Menú **Compilar → Compilar solución** (Ctrl+Mayús+B).
4. La DLL queda en `IndicadorTendenciaHourly\bin\Release\net10.0-windows\IndicadorTendenciaHourly.dll`.

El proyecto toma las librerías de ATAS de `C:\Program Files (x86)\ATAS Platform`. Si ATAS está instalado en otra
carpeta, la compilación se detiene con un mensaje que lo indica: cambie `ATAS_BASE` en `Directory.Build.props`.

## Instalar en ATAS

1. Copiar la DLL a `Documentos\ATAS\Indicators`.
2. En ATAS aparece un botón parpadeante en el panel inferior: pulsarlo para actualizar la lista de indicadores.
3. Agregar **Indicador de Tendencia Hourly** a un gráfico de **1 minuto o menor** (ticks y segundos también sirven).

Si Windows no deja reemplazar una versión anterior de la DLL, cerrar ATAS, copiarla y volver a abrir.

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

- **Desactivada** (por defecto, como en la especificación original): compara el precio actual con la vela anterior y
  cambia en tiempo real. Al empezar cada período nuevo el estado normalmente vuelve a (-), porque el precio arranca
  dentro del rango de la vela que acaba de cerrar, hasta que rompe su máximo o su mínimo.
- **Activada**: solo evalúa cuando cierra cada vela de 1H/15M/5M y mantiene ese estado durante todo el período
  siguiente.

### Aviso de gráfico no compatible

Si el gráfico es de tiempo y su período no divide exactamente 5 minutos (por ejemplo 3, 15 o 60 minutos), el panel
muestra *"Aviso: usar un gráfico de 5 min o menor"*: en esos gráficos las filas de 5M y 15M no serían exactas.
