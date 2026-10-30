#!/usr/bin/env bash
# Verificación de IndicadorCompleto sin Windows ni ATAS (necesita el SDK de .NET 10). Desde la raíz del repositorio:
#   bash IndicadorCompleto/pruebas/verificar.sh
# 1. Compila IndicadorCompleto.slnx (indicadores y ventana WPF) contra sustitutos de las DLL de ATAS.
# 2. Ejecuta las pruebas del núcleo (escenarios con velas de 1M sintéticas).
# La primera compilación contra las DLL reales de ATAS sigue siendo la de Windows.
set -euo pipefail

raiz="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "== 1. Sustitutos de las DLL de ATAS =="
dotnet build "$raiz/pruebas/compilacion/ATAS.Indicators.csproj" -c Release -o "$tmp/atas" -v q

for nombre in ATAS.DataFeedsCore OFT.Rendering Utils.Common OFT.Attributes; do
  mkdir -p "$tmp/$nombre"
  printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><EnableWindowsTargeting>true</EnableWindowsTargeting><AssemblyName>%s</AssemblyName></PropertyGroup></Project>\n' "$nombre" > "$tmp/$nombre/$nombre.csproj"
  echo 'namespace Vacio { internal class Vacio { } }' > "$tmp/$nombre/Vacio.cs"
  dotnet build "$tmp/$nombre/$nombre.csproj" -c Release -o "$tmp/atas" -v q
done

echo "== 2. Compilación de IndicadorCompleto contra los sustitutos =="
dotnet build "$raiz/IndicadorCompleto.slnx" -c Release -p:ATAS_BASE="$tmp/atas" -p:EnableWindowsTargeting=true --artifacts-path "$tmp/build"

echo "== 3. Pruebas del núcleo =="
dotnet run --project "$raiz/pruebas/Pruebas.csproj" -c Release
