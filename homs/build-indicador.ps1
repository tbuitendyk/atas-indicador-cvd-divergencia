# build-indicador.ps1 - Instalador de indicadores para ATAS (va en C:\homs\, lo lanza bic.cmd).
#
# 1. Sincroniza GitHub: git pull upstream main (origen) y git push origin main (destino).
# 2. Lee en el repositorio el archivo instalar.txt, que dice que indicador(es) instalar. Ese archivo llega con la
#    sincronizacion, asi que el acceso directo es siempre el mismo: lo que se instala lo decide instalar.txt.
# 3. Compila cada indicador (carpeta\carpeta.csproj, en Release) y copia su DLL a la carpeta de ATAS.
#
# Uso normal: doble clic en el acceso directo del escritorio a C:\homs\bic.cmd.
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
    [switch] $SinSincronizar
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

foreach ($nombre in $Indicador) {
    $proyecto = Join-Path (Join-Path $Repo $nombre) "$nombre.csproj"

    if (-not (Test-Path $proyecto)) {
        $hay = Get-ChildItem $Repo -Directory | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } | ForEach-Object { $_.Name }
        Error $nombre "No existe el indicador '$nombre' en el repositorio. Disponibles: $($hay -join ', ')"
        continue
    }

    Paso "3. Compilar $nombre (Release)"
    dotnet build $proyecto -c Release

    if ($LASTEXITCODE -ne 0) {
        Error $nombre "La compilacion de $nombre fallo (ver los errores de arriba). No se instalo."
        continue
    }

    # La DLL recien compilada (la carpeta del framework, net10.0-windows, no se fija aqui).
    $dll = Get-ChildItem (Join-Path (Join-Path (Join-Path $Repo $nombre) 'bin') 'Release') -Recurse -Filter "$nombre.dll" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $dll) {
        Error $nombre "No se encontro $nombre.dll en $nombre\bin\Release despues de compilar."
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
