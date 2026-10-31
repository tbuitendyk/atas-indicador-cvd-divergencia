# Thermal Volume Profile (perfil térmico)

Volume Profile con **color térmico** (amarillo claro = poco volumen, naranja = medio, rojo oscuro = mucho) que marca
el **POC**, el **VAH** y el **VAL**, y detecta automáticamente:

- **CLÚSTERES de espigas** (la función principal): grupos de filas rojo-naranja que salen mucho más a la derecha y
  forman rayos casi horizontales. Cada clúster se dibuja con **2 líneas rojas** (arriba y abajo): la franja entre
  ellas es una **zona de rebote**. Una línea verde fina marca su **cPOC** (la espiga más larga).
- **EDGE**: bordes donde el volumen cae de golpe (escalón casi horizontal del contorno).
- **SPIKE**: espigas sueltas.
- **LVN**: valles de poco volumen. Las **líneas amarillas que usted marque a mano** también cuentan como LVN.

Todo dentro de una **zona de búsqueda** opcional. Funciona en todas las temporalidades y tipos de gráfico (segundos,
minutos, horas, diario, semanal, tick, range, volumen y Renko).

Es un indicador **separado** para comprobarlo; después se agregará a IndicadorCompleto. Las decisiones que tomó la IA
donde el documento dejaba algo abierto están en **[DECISIONES.md](DECISIONES.md)**.

Contenido: [1. Compilar](#1-compilar) · [2. Instalar](#2-instalar) · [3. Usar](#3-usar) ·
[4. Barra de herramientas](#4-barra-de-herramientas) · [5. Qué se dibuja](#5-qué-se-dibuja) · [6. Ajustes](#6-ajustes) ·
[7. Alertas](#7-alertas) · [8. Prueba de validación](#8-prueba-de-validación) · [9. Lista de pruebas](#9-lista-de-pruebas)
· [10. Limitaciones](#10-limitaciones)

---

## 1. Compilar

1. En Visual Studio 2026: **Archivo → Abrir → Proyecto o solución** →
   **`ThermalVolumeProfile\ThermalVolumeProfile.slnx`** (su propia solución, separada de los otros indicadores).
2. Cambiar **Debug** por **Release**.
3. **Compilar → Compilar solución** (Ctrl+Mayús+B). Debe terminar con *0 errores*.
4. La DLL queda en:

   ```
   ThermalVolumeProfile\bin\Release\net10.0-windows\ThermalVolumeProfile.dll
   ```

Desde PowerShell, en la carpeta del repositorio:

```powershell
dotnet build ThermalVolumeProfile\ThermalVolumeProfile.slnx -c Release
```

Es un proyecto de biblioteca de clases (.NET 10) que toma de la carpeta de instalación de ATAS
(`C:\Program Files (x86)\ATAS Platform`) las referencias `ATAS.Indicators.dll`, `OFT.Rendering.dll`,
`OFT.Attributes.dll`, `ATAS.DataFeedsCore.dll` y `Utils.Common.dll`. Si ATAS está en otra carpeta, la compilación se
detiene con un mensaje en español: cambie `ATAS_BASE` en `ThermalVolumeProfile\Directory.Build.props`.

## 2. Instalar

En la ventana de indicadores de un gráfico: **Add custom indicator** → elegir `ThermalVolumeProfile.dll`. O copiar
la DLL a `%APPDATA%\ATAS\Indicators` (en *AppData*, **no** en *Documentos*) y pulsar el botón de recarga.
Si ATAS ya tenía cargada una versión anterior, cierre ATAS antes de sustituirla.

El indicador aparece en el grupo **Custom** como **Thermal Volume Profile (perfil térmico)**.

## 3. Usar

1. Agréguelo a cualquier gráfico (por ejemplo NQ de 60 minutos). Por defecto dibuja el perfil desde **20 días antes
   del día en que lo agregó hasta la vela actual**, y se actualiza en vivo.
2. Para elegir el rango: botón **RANGO** de la barra y arrastre sobre el gráfico desde la vela inicial hasta la final.
   O en los ajustes, *Modo del rango*: fecha y hora, últimas N velas, rango visible en pantalla, sesión, día, semana
   o mes.
3. Para buscar solo en una parte: botón **ZONA** y arrastre un rectángulo de precios.
4. Para marcar sus propios LVN: botón **LVN** y un clic en cada precio (líneas amarillas).

Las fechas y horas que escribe en los ajustes son **las del gráfico** (las del eje de tiempo de ATAS). El horario de
sesión (09:30-16:00) es, por defecto, el de **Nueva York**.

## 4. Barra de herramientas

Recuadro arrastrable por su título (*THERMAL VOLUME PROFILE*), con cuatro botones y una línea de estado:

| Botón | Qué hace |
|---|---|
| **PERFIL** | Muestra u oculta el perfil (las líneas y etiquetas siguen). |
| **RANGO** | Siguiente arrastre sobre el gráfico = rango del perfil, de la vela inicial a la final. |
| **ZONA** | Siguiente arrastre = rectángulo de la zona de búsqueda (precios y fechas). |
| **LVN** | Cada clic en el gráfico añade una línea amarilla (LVN manual) en ese precio; un clic sobre una línea la quita. Se desactiva pulsando LVN otra vez. |

La línea de estado dice cuántas velas y filas tiene el perfil, o qué falta: `RANGO VACIO`, `FILTRO DE TIEMPO SIN
VELAS`… Mientras una herramienta está activa, explica qué hacer.

## 5. Qué se dibuja

| Nivel | Dibujo | Etiqueta (borde derecho) |
|---|---|---|
| Clúster | 2 líneas rojas gruesas + rectángulo semitransparente entre ellas | `CLU ARRIBA 15435.50` / `CLU ABAJO 15206.25` |
| cPOC del clúster | Línea verde fina | `cPOC 15297.50` |
| EDGE | Línea naranja | `EDGE ARRIBA precio` / `EDGE ABAJO precio` |
| SPIKE | Línea magenta punteada | `SPK precio` |
| LVN automático | Línea (o franja) cian; el número es el ranking | `LVN 1 precio` |
| LVN manual | Línea amarilla | `LVN MANUAL precio` |
| POC | Línea amarilla | `POC precio` |
| VAH / VAL | Líneas blancas | `VAH precio` / `VAL precio` |
| Zona de búsqueda | Rectángulo gris semitransparente | — |

Si dos etiquetas se solapan, la segunda se desplaza. Solo texto ASCII.

## 6. Ajustes

| Grupo | Contenido |
|---|---|
| 1. Rango del perfil | Modo del rango, desde/hasta, últimas N velas, horario de sesión y su zona horaria, hasta la vela actual |
| 2. Tipo de perfil | Volumen, TPO, Delta, Bid o Ask; periodo TPO; ticks por fila; tamaño automático y máximo de filas (300) |
| 3. Área de Valor | % del VA (70), mostrar POC/VAH/VAL, colores, extender a la derecha, etiquetas |
| 4. Térmico | **Mostrar el perfil**, **delante de las velas** (apagado = detrás), paleta (amarillo-rojo, azul-rojo, grises o 4 colores propios), escala (lineal, raíz cuadrada, logarítmica), opacidad, ancho (% del rango), anclaje, **dirección de las barras** |
| 5. Zona de búsqueda | Activar, precios (en cualquier orden), filtro de tiempo y sus fechas, dibujar la zona |
| 6. CLÚSTER | Todos los umbrales del documento, colores, grosor, rectángulo, cPOC, etiquetas, usar la zona |
| 7. EDGE | Umbrales del documento, color, etiquetas, usar la zona |
| 8. SPIKE | Umbrales, ocultar las de un clúster, color, etiquetas, usar la zona |
| 9. LVN | Umbrales, línea o franja, color, etiquetas, usar la zona, **LVN manuales** (mostrar, precios, color) |
| 10. Etiquetas y alertas | Mostrar etiquetas, tamaño de letra, alertas por tipo de nivel y sonido |
| 11. Barra de herramientas | Mostrarla y su posición |

Los valores por defecto son los del documento. Al cambiar un ajuste de cálculo, el perfil se recalcula.

## 7. Alertas

En el grupo 10 se activa cada tipo (clúster, EDGE, SPIKE, LVN —también manuales—, POC, VA). Suena cuando la vela
actual toca el nivel, **una sola vez por nivel y por vela**, con el sonido elegido de la lista de ATAS, y el mensaje
aparece en la ventana de alertas de ATAS: `Precio toca CLU ARRIBA 15435.50`. No suena al abrir el gráfico.

## 8. Prueba de validación

La del documento: gráfico **NQ de 60 minutos** con el bloque de alto volumen entre **15169 y 15451**.

1. Abra ese gráfico y agregue el indicador con los valores por defecto.
2. Elija el mismo rango que usó al definir la prueba (botón **RANGO**, o fechas en el grupo 1).
3. Deben aparecer, con un margen de 1 a 3 ticks: **CLU ARRIBA 15435.50**, **CLU ABAJO 15206.25** y **cPOC 15297.50**.

Si no salen, anote qué niveles aparecen (etiquetas) y la línea de estado de la barra (velas, filas y ticks por fila)
y envíelo: con eso se ajusta. Aquí se comprobó con un perfil sintético hecho según esa descripción (ver
DECISIONES.md, punto 33): CLU ARRIBA 15435.00, CLU ABAJO 15206.00 y cPOC 15297.00.

## 9. Lista de pruebas

Pruebas para hacer en ATAS (las mismas situaciones se prueban automáticamente sin ATAS, ver más abajo):

| Prueba | Cómo | Resultado esperado |
|---|---|---|
| Rango vacío | Fechas de un periodo sin velas (por ejemplo, del año que viene) | Sin perfil ni niveles; la barra dice `RANGO VACIO`. Sin errores. |
| Zona de búsqueda fuera del perfil | Zona en precios donde no hay perfil | Solo POC, VAH y VAL; ningún clúster, EDGE, SPIKE ni LVN automático. |
| Cambio de temporalidad | Pasar el gráfico de 60 min a 5 min y a diario | Se recalcula solo; en diario las filas se agrandan (máximo 300). |
| Cambio de instrumento | Cambiar NQ por ES o MNQ | Se recalcula con el tick del nuevo instrumento. |
| Tiempo real | Dejarlo con el mercado abierto | El perfil y los niveles se actualizan con la vela actual sin trabarse. |
| Gráfico de tick | Gráfico de ticks (o range, volumen, Renko) con modo fecha y hora | El rango por fechas funciona con la hora de cada vela. |

Pruebas automáticas, sin ATAS ni Windows (desde la raíz del repositorio, con el SDK de .NET 10):

```bash
bash ThermalVolumeProfile/pruebas/verificar.sh
```

Compila la solución contra sustitutos de las DLL de ATAS y ejecuta 123 comprobaciones: algoritmos (perfil, VA, color,
EDGE, SPIKE, CLÚSTER, LVN, rangos y zonas horarias) y el indicador real (carga, tiempo real, cambio de instrumento,
rango vacío, zona fuera del perfil, gráfico de ticks, dibujo, etiquetas, alertas, barra de herramientas), la prueba de
validación análoga y el rendimiento (90 días de velas de 1 minuto: carga en menos de 1 s).

## 10. Limitaciones

- **Compilado y probado sin ATAS** (contra sustitutos de sus DLL). La primera compilación contra las DLL reales, el
  aspecto en pantalla, los editores de fecha y hora y el selector de sonido se comprueban en Windows. Si algo falla,
  el log de ATAS muestra `ThermalVolumeProfile: error …`.
- La prueba de validación real necesita sus datos de NQ (punto 8).
- ATAS no permite a un indicador leer las líneas dibujadas con sus herramientas de dibujo: los LVN manuales se marcan
  con la herramienta **LVN** del propio indicador.
- Pensado para ATAS clásico, como los demás indicadores; no se probó en ATAS X.
