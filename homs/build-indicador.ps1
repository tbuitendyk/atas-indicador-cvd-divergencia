# build-indicador.ps1 - Instalador de indicadores para ATAS (va en C:\homs\, lo lanza bic.cmd).
#
# 1. Sincroniza GitHub: git pull upstream main (origen) y git push origin main (destino).
# 2. Lee en el repositorio el archivo instalar.txt, que dice que indicador(es) instalar. Ese archivo llega con la
#    sincronizacion, asi que el acceso directo es siempre el mismo: lo que se instala lo decide instalar.txt.
# 3. Compila cada indicador (carpeta\carpeta.csproj, en Release) solo si su codigo cambio desde la ultima
#    compilacion (si la DLL ya esta y es mas nueva que todo su codigo, no se compila otra vez).
# 4. Copia su DLL a la carpeta de ATAS, salvo que la instalada ya sea identica (asi no hace falta cerrar ATAS).
#
# Uso normal: doble clic en el acceso directo del escritorio a C:\homs\bic.cmd.
# Compilar todo aunque no haya cambios:     C:\homs\bic.cmd -Forzar
# Otro indicador sin tocar instalar.txt:   C:\homs\bic.cmd IndicadorCompleto
# Varios a la vez:                          C:\homs\bic.cmd IndicadorCompleto ThermalVolumeProfile
#
# Solo texto ASCII (sin acentos) para que Windows PowerShell 5.1 lo lea igual con cualquier codificacion.

[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $Indicador,

    [string] $Repo = (Join-Path $env:USERPROFILE 'source\repos\atas-indicador-cvd-divergencia'),

    [string] $Destino = (Join-Path $env:APPDATA 'ATAS\Indicators'),

    # Sin git pull / git push (para volver a instalar lo que ya hay en la carpeta).
    [switch] $SinSincronizar,

    # Compilar aunque la DLL este al dia.
    [switch] $Forzar
)

$ErrorActionPreference = 'Continue'

function Fallo([string] $mensaje) {
    Write-Host ''
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

function Paso([string] $texto) {
    Write-Host ''
    Write-Host "== $texto ==" -ForegroundColor Cyan
}

if (-not (Test-Path (Join-Path $Repo '.git'))) {
    Fallo "No se encontro el repositorio en $Repo"
}

Set-Location $Repo

if (-not $SinSincronizar) {
    Paso '1. Traer las novedades del GitHub de origen (upstream)'
    git pull upstream main

    if ($LASTEXITCODE -ne 0) {
        Fallo 'git pull upstream main fallo. Revise la conexion a Internet o si hay cambios locales sin guardar.'
    }

    Paso '2. Publicarlas en el GitHub destino (origin)'
    git push origin main

    if ($LASTEXITCODE -ne 0) {
        Write-Host 'AVISO: git push origin main fallo; se sigue con la compilacion.' -ForegroundColor Yellow
    }

    # El propio script se actualiza desde GitHub: si el repositorio trae una version nueva, se copia encima de este
    # archivo y se vuelve a ejecutar ya actualizado (sin sincronizar otra vez).
    $nuevo = Join-Path (Join-Path $Repo 'homs') 'build-indicador.ps1'

    if ($PSCommandPath -and (Test-Path $nuevo) -and ((Get-FileHash $nuevo).Hash -ne (Get-FileHash $PSCommandPath).Hash)) {
        Write-Host ''
        Write-Host 'El instalador tiene una version nueva en GitHub: se actualiza y continua.' -ForegroundColor Yellow
        Copy-Item $nuevo $PSCommandPath -Force
        $argumentos = @{ Repo = $Repo; Destino = $Destino; SinSincronizar = $true }

        if ($Indicador) {
            $argumentos['Indicador'] = $Indicador
        }

        if ($Forzar) {
            $argumentos['Forzar'] = $true
        }

        & $PSCommandPath @argumentos
        exit $LASTEXITCODE
    }
}

# Que indicador(es) instalar: los de la linea de comandos o, si no hay, los de instalar.txt.
if (-not $Indicador -or $Indicador.Count -eq 0) {
    $archivo = Join-Path $Repo 'instalar.txt'

    if (-not (Test-Path $archivo)) {
        Fallo "No existe $archivo"
    }

    $Indicador = @(Get-Content $archivo | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
}

if (-not $Indicador -or $Indicador.Count -eq 0) {
    Fallo 'instalar.txt no nombra ningun indicador.'
}

if (-not (Test-Path $Destino)) {
    New-Item -ItemType Directory -Path $Destino | Out-Null
}

# Cada indicador por separado: si uno falla, se sigue con los demas y al final se resume.
$instalados = @()
$fallidos = @()

function Error([string] $nombre, [string] $mensaje) {
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    $script:fallidos += $nombre
}

# La DLL compilada mas reciente de un indicador (la carpeta del framework, net10.0-windows, no se fija aqui).
function DllCompilada([string] $nombre) {
    Get-ChildItem (Join-Path (Join-Path (Join-Path $Repo $nombre) 'bin') 'Release') -Recurse -Filter "$nombre.dll" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

# Fecha del archivo de codigo mas reciente del indicador: su carpeta (sin bin, obj ni pruebas), lo compartido de la
# raiz (Directory.Build.* y Compartido) y los archivos de OTRAS carpetas que su .csproj incluye
# (Include="..\Carpeta\Archivo.cs", p. ej. la suite usa el codigo de ThermalVolumeProfile). Un git pull que cambia
# codigo deja esos archivos con la fecha de hoy.
function CodigoMasReciente([string] $nombre) {
    $carpeta = Join-Path $Repo $nombre
    $archivos = @(Get-ChildItem $carpeta -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|pruebas)[\\/]' })
    $archivos += @(Get-ChildItem $Repo -File -Filter 'Directory.Build.*')
    $compartido = Join-Path $Repo 'Compartido'

    if (Test-Path $compartido) {
        $archivos += @(Get-ChildItem $compartido -Recurse -File)
    }

    $proyecto = Join-Path $carpeta "$nombre.csproj"

    foreach ($m in [regex]::Matches((Get-Content $proyecto -Raw), 'Include="(\.\.[\\/][^"]+)"')) {
        $relativa = $m.Groups[1].Value -replace '[\\/]', [IO.Path]::DirectorySeparatorChar
        $ruta = Join-Path $carpeta $relativa
        $archivos += @(Get-ChildItem $ruta -File -ErrorAction SilentlyContinue)
    }

    ($archivos | Measure-Object -Property LastWriteTime -Maximum).Maximum
}

foreach ($nombre in $Indicador) {
    $proyecto = Join-Path (Join-Path $Repo $nombre) "$nombre.csproj"

    if (-not (Test-Path $proyecto)) {
        $hay = Get-ChildItem $Repo -Directory | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } | ForEach-Object { $_.Name }
        Error $nombre "No existe el indicador '$nombre' en el repositorio. Disponibles: $($hay -join ', ')"
        continue
    }

    # Compilar solo si hace falta: no hay DLL, o algun archivo de codigo es mas nuevo que ella.
    $dll = DllCompilada $nombre

    if (-not $Forzar -and $dll -and $dll.LastWriteTime -ge (CodigoMasReciente $nombre)) {
        Paso "3. $nombre ya compilado y al dia (no se compila otra vez)"
    }
    else {
        Paso "3. Compilar $nombre (Release)"
        dotnet build $proyecto -c Release

        if ($LASTEXITCODE -ne 0) {
            Error $nombre "La compilacion de $nombre fallo (ver los errores de arriba). No se instalo."
            continue
        }

        $dll = DllCompilada $nombre
    }

    if (-not $dll) {
        Error $nombre "No se encontro $nombre.dll en $nombre\bin\Release despues de compilar."
        continue
    }

    # Instalar: si la de ATAS ya es identica no se copia (y no hace falta cerrar ATAS).
    $instalada = Join-Path $Destino "$nombre.dll"

    if ((Test-Path $instalada) -and (Get-FileHash $instalada).Hash -eq (Get-FileHash $dll.FullName).Hash) {
        Paso "4. $nombre.dll ya instalado en $Destino (sin cambios)"
        $instalados += $nombre
        continue
    }

    Paso "4. Instalar $nombre.dll en $Destino"

    try {
        Copy-Item $dll.FullName $Destino -Force -ErrorAction Stop
    }
    catch {
        Error $nombre "No se pudo copiar $nombre.dll: ATAS probablemente lo tiene abierto. Cierre ATAS y vuelva a usar el acceso directo."
        continue
    }

    $instalados += $nombre
}

Write-Host ''

if ($instalados.Count -gt 0) {
    Write-Host "LISTO: $($instalados -join ', ') instalado(s) en $Destino" -ForegroundColor Green
    Write-Host 'Si ATAS estaba abierto, cierrelo y vuelva a abrirlo para cargar la version nueva.'
}

if ($fallidos.Count -gt 0) {
    Write-Host "NO SE INSTALO: $($fallidos -join ', ') (ver los errores de arriba)" -ForegroundColor Red
    exit 1
}

exit 0
