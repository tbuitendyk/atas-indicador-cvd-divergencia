# Instalador de indicadores (C:\homs\)

Un solo acceso directo en el escritorio que, con un doble clic:

1. sincroniza GitHub (`git pull upstream main` y `git push origin main`),
2. lee en el repositorio el archivo **`instalar.txt`**, que dice qué indicador instalar,
3. lo compila en Release **solo si su código cambió** desde la última compilación,
4. y copia su DLL a `%APPDATA%\ATAS\Indicators` **solo si es distinta** de la instalada.

`instalar.txt` llega con la sincronización: cuando hay un indicador nuevo, se cambia ese archivo en GitHub y el
mismo acceso directo instala el nuevo. El acceso directo no se toca nunca.

## Archivos

| Archivo | Dónde va | Qué hace |
|---|---|---|
| `bic.cmd` | `C:\homs\` | Lo que abre el acceso directo. Lanza el script y deja la ventana abierta al terminar. |
| `build-indicador.ps1` | `C:\homs\` | Sincroniza, compila e instala. Se actualiza solo desde GitHub. |
| `instalar.txt` | En el repositorio (raíz), **no** en `C:\homs\` | Nombre del indicador a instalar: uno por línea. Las líneas con `#` no cuentan. |

## Preparación (una sola vez)

1. Si todavía no los tiene, traiga del GitHub los archivos nuevos con el acceso directo de siempre (o
   `git pull upstream main` en la carpeta del repositorio).
2. Copie los dos archivos a `C:\homs\` (en PowerShell):

   ```powershell
   Copy-Item $HOME\source\repos\atas-indicador-cvd-divergencia\homs\bic.cmd, $HOME\source\repos\atas-indicador-cvd-divergencia\homs\build-indicador.ps1 C:\homs\ -Force
   ```

   El nuevo `bic.cmd` sustituye al anterior, así que el acceso directo del escritorio sigue sirviendo.
   `build-indicador-completo.cmd` ya no se usa (se puede borrar).

## Uso

- **Cierre ATAS** si hay algo nuevo que instalar (si tiene cargada la DLL, Windows no deja reemplazarla). Si no hay
  cambios, el acceso directo no compila ni copia nada y no hace falta cerrarlo: dice `ya compilado y al dia` y
  `ya instalado (sin cambios)`.
- Doble clic en el acceso directo. Al final dice `LISTO: <indicador> instalado(s)`.
- Abra ATAS. La primera vez que se instala un indicador, agréguelo al gráfico desde el grupo **Custom**.

Si `instalar.txt` nombra varios y uno falla, los demás se instalan igual; al final la ventana dice cuáles se instalaron
(`LISTO: ...`) y cuáles no (`NO SE INSTALO: ...`).

Para compilar todo aunque no haya cambios: `C:\homs\bic.cmd -Forzar`.

Para instalar otro indicador sin cambiar `instalar.txt`, en una ventana de comandos:
`C:\homs\bic.cmd IndicadorCompleto` (o varios: `C:\homs\bic.cmd IndicadorCompleto ThermalVolumeProfile`).

Los nombres son los de las carpetas del repositorio: IndicadorTendenciaHourly, IndicadorCvdDivergencia,
IndicadorFootprintPredictor, IndicadorLvn, IndicadorCombinado, IndicadorCompleto, ThermalVolumeProfile,
NQSessionSweepsPro, SuiteOrderFlowStacker…

"Código del indicador" incluye los archivos de otras carpetas que su proyecto usa (`Include="..\Carpeta\..."` en el
`.csproj`): la suite usa el código de ThermalVolumeProfile, así que un cambio allí recompila los dos.

## Si algo falla

| Mensaje | Qué hacer |
|---|---|
| `No se pudo copiar ...dll: ATAS probablemente lo tiene abierto` | Cierre ATAS y vuelva a usar el acceso directo. |
| `git pull upstream main fallo` | Revise Internet o si hay cambios hechos a mano en la carpeta del repositorio. |
| `AVISO: git push origin main fallo` | La instalación sigue; el GitHub destino se actualiza la próxima vez. |
| `No existe el indicador '...'` | El nombre de `instalar.txt` no coincide con ninguna carpeta; el mensaje lista los que hay. |
| `La compilacion de ... fallo` | Copie los errores que se ven arriba y envíelos. Ese indicador no se instaló; los demás de la lista sí. |
| `No se encontro el repositorio` | El repositorio no está en `C:\Users\<usuario>\source\repos\atas-indicador-cvd-divergencia`. |
