# Guía: subir, descargar, compilar e instalar

| | Repositorio | Para qué |
|---|---|---|
| **Origen** | [tbuitendyk/atas-indicador-cvd-divergencia](https://github.com/tbuitendyk/atas-indicador-cvd-divergencia) | Donde se desarrolla el código |
| **Destino** | [uniontrading777-sketch/atas-indicador-cvd-divergencia](https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia) | Copia que se compila y se usa en ATAS |

Resumen:

1. Preparar la PC con Windows (una vez).
2. Carga inicial: copiar el origen al destino, que hoy está vacío (una vez). Deja también el código en la PC.
3. Compilar en Visual Studio 2026.
4. Instalar la DLL en ATAS.
5. Actualizaciones: pasar los indicadores nuevos al GitHub destino y descargarlos desde ahí.

> GitHub no tiene interfaz en español: los nombres de menús y botones de GitHub aparecen aquí en inglés,
> tal como se ven en pantalla.

---

## 1. Preparar la PC con Windows (una sola vez)

1. **Git para Windows**. Lo más rápido es instalarlo desde PowerShell:

   ```powershell
   winget install --id Git.Git -e --source winget
   ```

   O descargar el instalador de <https://git-scm.com/download/win> y aceptar las opciones por defecto.
   Incluye *Git Credential Manager*, que abre el navegador para iniciar sesión en GitHub la primera vez.

   **Después de instalarlo, cerrar PowerShell y abrir una ventana nueva**: las ventanas que ya estaban abiertas no
   encuentran el comando `git`. Si en una ventana nueva sigue apareciendo *"git no se reconoce como nombre de un
   cmdlet..."*, volver a ejecutar el instalador y, en la pantalla *Adjusting your PATH environment*, elegir la
   opción recomendada (*Git from the command line and also from 3rd-party software*).
2. **Visual Studio 2026**: en el *Visual Studio Installer*, marcar la carga de trabajo
   **Desarrollo de escritorio de .NET** (incluye el SDK de .NET 10).
3. **ATAS**: instalado y actualizado a la versión más reciente.
4. Comprobar en **PowerShell**:

   ```powershell
   git --version
   dotnet --list-sdks
   ```

   `dotnet --list-sdks` debe mostrar al menos una línea que empiece por `10.`.

---

## 2. Carga inicial al repositorio destino (una sola vez)

La hace **uniontrading777-sketch**, en PowerShell:

```powershell
mkdir $HOME\source\repos -Force
cd $HOME\source\repos
git clone https://github.com/tbuitendyk/atas-indicador-cvd-divergencia.git
cd atas-indicador-cvd-divergencia
git remote rename origin upstream
git remote add origin https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia.git
git push -u origin main
```

- En el `git push` se abre el navegador para iniciar sesión en GitHub: entrar con la cuenta **uniontrading777-sketch**.
- Comprobar en <https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia> que aparecen los archivos.

Qué quedó configurado (se puede ver con `git remote -v`):

- `origin` = repositorio **destino** (el propio).
- `upstream` = repositorio **origen** (de donde llegan las actualizaciones).

> ¿Por qué no es un pull request? Un pull request necesita que la rama `main` ya exista en el destino. Como el
> destino está vacío, la primera carga se hace con un `push` directo. Las actualizaciones posteriores están en la
> sección 5. Si el `push` es rechazado, es que el destino ya no está vacío (por ejemplo, se le agregó un README
> desde la web): hay que vaciarlo o crearlo de nuevo sin archivos.

---

## 3. Compilar en Visual Studio 2026

1. Abrir la carpeta `%USERPROFILE%\source\repos\atas-indicador-cvd-divergencia` y hacer doble clic en
   **`Indicadores.slnx`** (o, en Visual Studio: **Archivo → Abrir → Proyecto o solución**).
2. En la barra de herramientas, cambiar **Debug** por **Release**.
3. Menú **Compilar → Compilar solución** (Ctrl+Mayús+B).
4. La ventana **Salida** debe terminar indicando que la compilación fue correcta, con 0 errores.
5. Cada proyecto deja su DLL en su carpeta:

   ```
   IndicadorTendenciaHourly\bin\Release\net10.0-windows\IndicadorTendenciaHourly.dll
   IndicadorCvdDivergencia\bin\Release\net10.0-windows\IndicadorCvdDivergencia.dll
   IndicadorFootprintPredictor\bin\Release\net10.0-windows\IndicadorFootprintPredictor.dll
   IndicadorLvn\bin\Release\net10.0-windows\IndicadorLvn.dll
   IndicadorCombinado\bin\Release\net10.0-windows\IndicadorCombinado.dll
   ```

Alternativa sin abrir Visual Studio (PowerShell, dentro de la carpeta del repositorio):

```powershell
dotnet build Indicadores.slnx -c Release
```

### Indicador Completo (solución aparte)

**Indicador Completo** no está en `Indicadores.slnx`: tiene su propia solución. Se compila igual, abriendo
**`IndicadorCompleto\IndicadorCompleto.slnx`**, en **Release**, **Compilar → Compilar solución**. La DLL queda en:

```
IndicadorCompleto\bin\Release\net10.0-windows\IndicadorCompleto.dll
```

O desde PowerShell: `dotnet build IndicadorCompleto\IndicadorCompleto.slnx -c Release`. Se instala con
**Add custom indicator** como los demás (sección 4), **solo en ATAS clásico**. Cómo preparar los gráficos y usarlo:
[IndicadorCompleto/LEEME.md](../IndicadorCompleto/LEEME.md).

### Si la compilación falla

- **`No se encontró ATAS.Indicators.dll en '...'`**: ATAS no está en `C:\Program Files (x86)\ATAS Platform`.
  Buscar en qué carpeta está `ATAS.Indicators.dll` y poner esa ruta en `ATAS_BASE`, dentro de
  `Directory.Build.props`.
- **Cualquier otro error**: copiar la lista completa de la ventana **Lista de errores** y enviarla para corregirlo.

---

## 4. Instalar en ATAS

**Opción A (recomendada): desde ATAS.** En la ventana de indicadores de un gráfico, pulsar el botón
**Add custom indicator** (abajo a la izquierda) y elegir la DLL en `<carpeta>\bin\Release\net10.0-windows\`
(una vez por DLL). ATAS la copia a su carpeta y la carga. Para actualizar un indicador ya instalado, hacer lo mismo
con la DLL nueva.

**Opción B: copiar la DLL a mano** a la carpeta de indicadores de ATAS, que está en *AppData*
(**no** en *Documentos*): `C:\Users\<usuario>\AppData\Roaming\ATAS\Indicators`. Desde PowerShell, en la carpeta
del repositorio:

```powershell
Copy-Item .\IndicadorTendenciaHourly\bin\Release\net10.0-windows\IndicadorTendenciaHourly.dll "$env:APPDATA\ATAS\Indicators\"
Copy-Item .\IndicadorCvdDivergencia\bin\Release\net10.0-windows\IndicadorCvdDivergencia.dll "$env:APPDATA\ATAS\Indicators\"
Copy-Item .\IndicadorFootprintPredictor\bin\Release\net10.0-windows\IndicadorFootprintPredictor.dll "$env:APPDATA\ATAS\Indicators\"
Copy-Item .\IndicadorLvn\bin\Release\net10.0-windows\IndicadorLvn.dll "$env:APPDATA\ATAS\Indicators\"
Copy-Item .\IndicadorCombinado\bin\Release\net10.0-windows\IndicadorCombinado.dll "$env:APPDATA\ATAS\Indicators\"
```

Si ATAS ya estaba abierto, pulsar el **botón de recarga que aparece en la barra de estado** (abajo) de la ventana
principal; no hace falta reiniciar.

Después, en cualquiera de las dos opciones:

1. Abrir un gráfico de **1 minuto** (o menor) y la ventana de indicadores.
2. Los indicadores aparecen en el grupo **Custom**: seleccionar el deseado y pulsar **Add**.
   - **Indicador de Tendencia Hourly**: panel de tendencia 1H/15M/5M.
   - **Institutional CVD Trigger & Absorption Engine**: banderas de gatillo y panel de los cuatro filtros, en el
     gráfico de precio.
   - **Velas CVD (panel)**: las velas del CVD en un panel aparte; se agrega además del motor si se quieren ver.
   - **Footprint Predictor Pro (Sweeps & Context)**: puntos de pronóstico de la siguiente vela y panel de estructura
     de mercado.
   - **Institutional 90-Day Deep LVN Profile Engine**: líneas de los nodos bajos de volumen y panel del objetivo de
     retest. Necesita los días de historial cargados en el gráfico (hasta 90).
   - **Indicador Combinado (Tendencia, CVD, Footprint y LVN)**: los cuatro anteriores en uno, con un solo tablero.
     Usar este **o** los individuales, no ambos (se vería todo dos veces).
3. Los paneles se mueven arrastrándolos con el ratón por su barra de título.
4. Qué hace cada ajuste: ver el [README](../README.md).

### Si el indicador no aparece en la lista

1. Comprobar que la DLL está en `%APPDATA%\ATAS\Indicators` (pegar esa ruta en la barra de direcciones del
   Explorador). Una copia en *Documentos* no sirve: ATAS no la lee.
2. Al recargar, ATAS indica cada archivo que no pudo cargar y el motivo. Los errores también quedan en la pestaña
   **Logs** (botón *Logs* de la barra superior) y en la carpeta `%APPDATA%\ATAS\Logs`.
3. Comprobar la versión de .NET de ATAS: en `C:\Program Files (x86)\ATAS Platform`, abrir con el Bloc de notas
   `OFT.Platform.runtimeconfig.json` y mirar el valor `"tfm"`. Debe ser `net10.0`. Si es `net8.0`, actualizar ATAS
   (o cambiar `net10.0-windows` por `net8.0-windows` en `Directory.Build.props` y compilar de nuevo).

En **ATAS X** (la versión nueva de ATAS) la carpeta es `%APPDATA%\ATAS X\Indicators`.

---

## 5. Actualizaciones: traer los indicadores nuevos

Cada mejora o indicador nuevo se publica primero en el **origen**. Llega a la PC con ATAS en dos pasos: primero se
pasa al GitHub **destino** y después se descarga desde ahí.

GitHub no tiene un botón para copiar cambios entre dos repositorios independientes (el destino no es un *fork* del
origen), así que el paso 5.1 se hace con Git, desde cualquier PC que lo tenga instalado.

### 5.1 Pasar las novedades del origen al GitHub destino

En la PC que ya tiene la carpeta de la sección 2, en PowerShell:

```powershell
cd $HOME\source\repos\atas-indicador-cvd-divergencia
git pull upstream main
git push origin main
```

- `git pull upstream main` trae los indicadores nuevos desde el **origen** (tbuitendyk).
- `git push origin main` los publica en el **destino** (uniontrading777-sketch). Si pide iniciar sesión, entrar con
  la cuenta **uniontrading777-sketch**, o con **tbuitendyk**, que es colaborador del destino.
- Comprobar en <https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia> que aparecen las carpetas
  nuevas (por ejemplo `IndicadorCvdDivergencia/`).

En una PC **sin** esa carpeta, primero hay que clonar el destino y agregar el origen como `upstream`; después, los dos
comandos de arriba:

```powershell
cd $HOME\source\repos
git clone https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia.git
cd atas-indicador-cvd-divergencia
git remote add upstream https://github.com/tbuitendyk/atas-indicador-cvd-divergencia.git
```

### 5.2 Descargar desde el GitHub destino en la PC con ATAS

- **Si la PC ya tiene la carpeta** (sección 2), basta con actualizarla. Si el paso 5.1 se hizo en esta misma PC, ya
  está al día.

  ```powershell
  cd $HOME\source\repos\atas-indicador-cvd-divergencia
  git pull origin main
  ```

- **Para una copia nueva**, clonar el destino. Si ya existe una carpeta con ese nombre, borrarla o renombrarla antes.

  ```powershell
  cd $HOME\source\repos
  git clone https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia.git
  ```

Después: compilar (sección 3) e instalar las DLL nuevas o actualizadas con **Add custom indicator** (sección 4).

### 5.3 Alternativa con revisión: pull request

Si uniontrading777-sketch prefiere revisar cada cambio antes de que entre en su `main`, tbuitendyk (ya es
colaborador del destino) sube los cambios a una rama nueva del destino y abre un pull request. Desde una copia local
del origen (cambiar la fecha del nombre de la rama en cada actualización):

```bash
git checkout main
git pull origin main
git remote add destino https://github.com/uniontrading777-sketch/atas-indicador-cvd-divergencia.git   # solo la primera vez
git push destino main:actualizacion-2026-10-26
```

Después, en GitHub, en el repositorio **destino**:

1. Aparece el aviso *"actualizacion-2026-10-26 had recent pushes"*: pulsar **Compare & pull request**.
   (Si no aparece: **Pull requests → New pull request**, con *base:* `main` y *compare:* `actualizacion-2026-10-26`.)
2. Escribir qué cambia y pulsar **Create pull request**.

**uniontrading777-sketch** lo revisa y lo aprueba:

1. Abrir el pull request; la pestaña **Files changed** muestra exactamente qué cambió.
2. Pulsar **Merge pull request → Confirm merge** y luego **Delete branch**.
3. Descargar en la PC con ATAS (sección 5.2), compilar e instalar.
