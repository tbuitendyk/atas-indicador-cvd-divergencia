@echo off
rem Acceso directo del escritorio -> C:\homs\bic.cmd
rem Sincroniza GitHub, compila el indicador que nombra instalar.txt (en el repositorio) y lo instala en ATAS.
rem Otro indicador sin tocar instalar.txt:  bic.cmd NombreDelIndicador
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-indicador.ps1" %*
pause
