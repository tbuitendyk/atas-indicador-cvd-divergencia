# Progreso de ThermalVolumeProfile (rama `thermal-volume-profile`)

Especificación: el PDF "Prompt Volumen Profile térmico" del propietario (2026-10-31). El propietario pidió que la IA
respondiera sola las preguntas abiertas y entregara las decisiones: DECISIONES.md.

## Estado

- [x] Solución propia (slnx, csproj, props, targets)
- [x] Núcleo `MotorTermico.cs`: perfil (Volumen, TPO, Delta, Bid, Ask; AutoRowSize; velas sin niveles), POC/VA CME,
      color térmico (4 paletas, 3 escalas, caché de 256), EDGE, SPIKE, CLÚSTER (con ajuste al EDGE), LVN (con LVN
      manuales), modos de rango y zonas horarias
- [x] Indicador `ThermalVolumeProfile.cs`: parámetros del documento + notas del propietario, cálculo una vez al cargar
      y solo la vela abierta en vivo, dibujo de filas visibles, etiquetas ASCII sin solaparse, alertas, barra de
      herramientas (PERFIL, RANGO, ZONA, LVN)
- [x] Pruebas: núcleo, indicador real con sustitutos, validación análoga, rendimiento (123 comprobaciones)
- [x] LEEME.md y DECISIONES.md
- [x] Fusión a main y documentación de la raíz

## Pendiente (en Windows, con ATAS)

- Compilación contra las DLL reales, aspecto en pantalla, editores de fecha y selector de sonido.
- Prueba de validación real con el gráfico NQ de 60 minutos del propietario (LEEME.md, punto 8).
- Después: incorporarlo a IndicadorCompleto (copiar `MotorTermico.cs` al núcleo).
