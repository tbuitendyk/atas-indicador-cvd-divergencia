#!/usr/bin/env bash
# Verificación completa sin Windows ni ATAS (necesita el SDK de .NET 10). Desde la raíz del repositorio:
#   bash pruebas/verificar.sh
# 1. Compila la solución contra sustitutos de las DLL de ATAS (pruebas/compilacion).
# 2. Ejecuta las pruebas de comportamiento de los cinco indicadores (pruebas/Pruebas.csproj).
# La primera compilación contra las DLL reales de ATAS sigue siendo la de Windows.
set -euo pipefail

raiz="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "== 1. Sustitutos de las DLL de ATAS =="
dotnet build "$raiz/pruebas/compilacion/ATAS.Indicators.csproj" -c Release -o "$tmp/atas" -v q

# Las otras DLL que referencia Directory.Build.props: ensamblados vacíos con su nombre.
for nombre in ATAS.DataFeedsCore OFT.Rendering Utils.Common; do
  mkdir -p "$tmp/$nombre"
  printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><EnableWindowsTargeting>true</EnableWindowsTargeting><AssemblyName>%s</AssemblyName></PropertyGroup></Project>\n' "$nombre" > "$tmp/$nombre/$nombre.csproj"
  echo 'namespace Vacio { internal class Vacio { } }' > "$tmp/$nombre/Vacio.cs"
  dotnet build "$tmp/$nombre/$nombre.csproj" -c Release -o "$tmp/atas" -v q
done

echo "== 2. Compilación de la solución contra los sustitutos =="
dotnet build "$raiz/Indicadores.slnx" -c Release -p:ATAS_BASE="$tmp/atas" -p:EnableWindowsTargeting=true --artifacts-path "$tmp/build"

echo "== 3. Pruebas de comportamiento =="
dotnet run --project "$raiz/pruebas/Pruebas.csproj" -c Release
