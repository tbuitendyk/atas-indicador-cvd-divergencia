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
5. Actualizaciones futuras, por pull request.

> GitHub no tiene interfaz en español: los nombres de menús y botones de GitHub aparecen aquí en inglés,
> tal como se ven en pantalla.

---

## 1. Preparar la PC con Windows (una sola vez)

1. **Git para Windows**: descargarlo de <https://git-scm.com/download/win> e instalarlo con las opciones por defecto.
   Incluye *Git Credential Manager*, que abre el navegador para iniciar sesión en GitHub la primera vez.
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
> destino está vacío, la primera carga se hace con un `push` directo. Las actualizaciones posteriores sí van por
> pull request (sección 5). Si el `push` es rechazado, es que el destino ya no está vacío (por ejemplo, se le
> agregó un README desde la web): hay que vaciarlo o crearlo de nuevo sin archivos.

---

## 3. Compilar en Visual Studio 2026

1. Abrir la carpeta `%USERPROFILE%\source\repos\atas-indicador-cvd-divergencia` y hacer doble clic en
   **`Indicadores.slnx`** (o, en Visual Studio: **Archivo → Abrir → Proyecto o solución**).
2. En la barra de herramientas, cambiar **Debug** por **Release**.
3. Menú **Compilar → Compilar solución** (Ctrl+Mayús+B).
4. La ventana **Salida** debe terminar indicando que la compilación fue correcta, con 0 errores.
5. La DLL queda en:

   ```
   IndicadorTendenciaHourly\bin\Release\net10.0-windows\IndicadorTendenciaHourly.dll
   ```

Alternativa sin abrir Visual Studio (PowerShell, dentro de la carpeta del repositorio):

```powershell
dotnet build Indicadores.slnx -c Release
```

### Si la compilación falla

- **`No se encontró ATAS.Indicators.dll en '...'`**: ATAS no está en `C:\Program Files (x86)\ATAS Platform`.
  Buscar en qué carpeta está `ATAS.Indicators.dll` y poner esa ruta en `ATAS_BASE`, dentro de
  `Directory.Build.props`.
- **Cualquier otro error**: copiar la lista completa de la ventana **Lista de errores** y enviarla para corregirlo.

---

## 4. Instalar en ATAS

1. Copiar la DLL a la carpeta de indicadores de ATAS. Desde PowerShell, en la carpeta del repositorio (funciona
   aunque *Documentos* esté en OneDrive):

   ```powershell
   Copy-Item .\IndicadorTendenciaHourly\bin\Release\net10.0-windows\IndicadorTendenciaHourly.dll "$([Environment]::GetFolderPath('MyDocuments'))\ATAS\Indicators\"
   ```

   O a mano, con el Explorador: `Documentos\ATAS\Indicators`. En instalaciones antiguas la carpeta puede llamarse
   `Documentos\Advanced Time And Sales\Indicators`.
   Si Windows no deja reemplazar una versión anterior de la DLL, cerrar ATAS, copiarla y volver a abrirlo.
2. En ATAS aparece un **botón parpadeante en el panel inferior**: pulsarlo para recargar la lista de indicadores.
3. Abrir un gráfico de **1 minuto** (o menor) y agregar **Indicador de Tendencia Hourly** desde la lista de
   indicadores.
4. Ajustes disponibles en la configuración del indicador:
   - **Cálculo → Confirmar al cierre**: activado por defecto. Ver el [README](../README.md) para la diferencia entre
     los dos modos.
   - **Ubicación del panel → Posición X / Posición Y**.
   - **Colores**: flecha alcista, flecha bajista, símbolo de rango y fondo de la pastilla.

### Si ATAS no carga el indicador

El proyecto está compilado para **.NET 10**, que es el que usan las versiones actuales de ATAS. Para comprobar la
versión de la instalación: en la carpeta de ATAS (`C:\Program Files (x86)\ATAS Platform`), abrir con el Bloc de notas
el archivo `*.runtimeconfig.json` del programa principal y mirar el valor `"version"`. Si empieza por `8.` en lugar de
`10.`, cambiar `net10.0-windows` por `net8.0-windows` en `Directory.Build.props` y compilar de nuevo (o, mejor,
actualizar ATAS).

---

## 5. Actualizaciones futuras (pull request)

Cada mejora o indicador nuevo se desarrolla en el **origen** y llega al **destino** como pull request, que
uniontrading777-sketch revisa y aprueba antes de que entre en su `main`.

GitHub solo permite pull requests dentro de un mismo repositorio (o entre un repositorio y su *fork*). El destino no
es un fork del origen, así que tbuitendyk necesita ser **colaborador** del destino para subir ahí la rama del pull
request.

### 5.1 Una sola vez: agregar a tbuitendyk como colaborador

1. **uniontrading777-sketch**: en el repositorio destino, **Settings → Collaborators → Add people**, escribir
   `tbuitendyk` y enviar la invitación (GitHub puede pedir la contraseña para confirmar).
2. **tbuitendyk**: aceptar la invitación, que llega por correo y aparece en <https://github.com/notifications>.

### 5.2 Cada actualización

**tbuitendyk** sube los cambios a una rama nueva del destino, desde su copia local del origen
(cambiar la fecha del nombre de la rama en cada actualización):

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
3. Traer los cambios a la PC, recompilar (sección 3) y volver a copiar la DLL (sección 4):

   ```powershell
   cd $HOME\source\repos\atas-indicador-cvd-divergencia
   git pull origin main
   ```

### Alternativa sin pull request

Si no hace falta revisar cada cambio en GitHub, uniontrading777-sketch puede traer las actualizaciones directamente
desde el origen y publicarlas en su repositorio:

```powershell
cd $HOME\source\repos\atas-indicador-cvd-divergencia
git pull upstream main
git push origin main
```
