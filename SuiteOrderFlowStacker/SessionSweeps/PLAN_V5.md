# Plan v5 — tres indicadores FAST (correcciones del 20-11-2026)

Especificación: [PROMPT_V5.md](PROMPT_V5.md). Base: v4 (`main` 02fd6a8). Rama `sweep-pro-fast-v5`. El propietario
delegó las decisiones; las de abajo quedan registradas.

## Lo que se encontró en el código

- **Por qué tarda en cargar:** en la v4 un solo indicador calculaba el Thermal, los trackers y el barrido. Además, el
  barrido reconstruye **barras de volumen desde el footprint de CADA vela de 1 minuto del historial** (cada nivel de
  precio se convierte en trades) y pasa cada uno por el clasificador, las líneas de sesión y los modelos. Con semanas
  de historial de 1 minuto son cientos de miles de "trades" aproximados. Los trackers dentro también reconstruían sus
  eventos con todo el historial.
- El Thermal solo calcula en la última vela (no por vela), pero estaba dentro del indicador global.

## Decisiones

1. **Tres indicadores independientes** en la DLL de la suite:
   - **LVN Thermal Profile (FAST)**: el Thermal (Suite), con su panel flotante (Perfil, Rango, Pierna, Zona, LVN,
     EDGE, Gatillo…). El Thermal suelto de su propia DLL no cambia.
   - **Indicador de Vela Compuesta (FAST)**: nuevo.
   - **Sweep Pro (FAST)**: el antiguo Sessions Sweeps Pro / Indicador Global (misma clase: las plantillas siguen
     abriendo). Ya no lleva el Thermal dentro.
2. **Carga rápida del Sweep Pro:** lo pesado (barras de volumen, clasificador, modelos, CVD, líneas de sesión) solo se
   calcula para los **últimos N días operativos** (por defecto 2: hoy y la sesión anterior; configurable). Los días
   anteriores solo aportan lo barato (máximos y mínimos de sesiones y días, H1, calibración) y su footprint no se lee.
   Los barridos ya congelados del día siguen viniendo del CSV.
3. **Trackers:** siguen dentro del Sweep Pro (el propietario pidió los stops tomados en el recuadro 2), pero **solo en
   tiempo real** por defecto: no reconstruyen el historial al cargar.
4. **Persistencia de LVN y EDGE:** el LVN Thermal Profile guarda en un archivo por instrumento sus LVN y EDGE
   vigentes (automáticos y manuales) cada vez que cambian. El Sweep Pro los lee al cargar y los sigue usando y
   dibujando aunque el Thermal esté cerrado o apagado (objetivos, Modelo 2, gatillos). Cerrar el panel del Thermal no
   detiene el indicador.
5. **Indicador de Vela Compuesta (FAST):**
   - Velas volumétricas de **2,300 contratos** por defecto (el "margen base"): una vela nueva cada N contratos.
   - Panel propio con **Gatillo Comp** (vela compuesta: arranque o continuidad con POC en el tercio, delta que vira,
     desplazamiento), **Gatillo Velas Volumétricas** (una vela volumétrica de desplazamiento: rango, delta y rapidez) y
     **FVG** (mostrar u ocultar).
   - Contratos por franja horaria (Asia, Londres, Premercado, NY apertura, NY mañana, NY mediodía, NY tarde) con un
     desplegable que muestra cada franja y sus contratos sugeridos, botones para subir y bajar, y el **sugerido**
     calculado con el promedio de volumen de las **últimas 20 velas** (de la franja, y el actual).
   - Sus gatillos van al bus de la suite: el Sweep Pro los muestra y los cuenta como confluencia de la entrada.
6. **Sweep Pro, marcado manual** (botones dentro del recuadro 2 del panel): **Barrido Real**, **Barrido Real Prime**,
   **Rompimiento**. Se arrastra un recuadro en el gráfico; queda como un barrido MANUAL con ese estado y arranca la
   verificación de los modelos (Real / Prime → M1; Rompimiento → M2 tras rompimiento). La dirección sale del precio
   después del recuadro. Se guardan con el gráfico y se pueden borrar.
7. **Recuadro de vela compuesta** (botón en el recuadro 2): se arrastra sobre las velas; el rango es el máximo y el
   mínimo de esas velas; la primera ruptura dibuja una **flecha verde** (alcista) o **roja** (bajista).
8. **Dudoso:** lo que se mostraba como INDETERMINADO se llama DUDOSO (como pide el documento).
9. **Gráfico de 5 minutos:** el Sweep Pro sigue calculando con velas de 1 minuto: las del día las arma con los trades
   del historial que pide a ATAS; las de días anteriores las aproxima partiendo las velas de 5 minutos (solo para
   niveles). Dibuja las zonas de Asia, Londres, Premercado, NY mañana y tarde, los barridos y los gatillos en el
   gráfico de 5 minutos, cada cosa en su hora.
10. **Nombres:** LVN Thermal Profile (FAST), Sweep Pro (FAST), Indicador de Vela Compuesta (FAST).

## Subfases

| | Subfase |
|---|---|
| F1 | Especificación y plan (este archivo). |
| F2 | Separar: el Sweep Pro deja de heredar del Thermal; nombres; trackers en tiempo real por defecto. |
| F3 | Carga rápida: días con detalle (footprint y barras de volumen) en el Sweep Pro. |
| F4 | LVN y EDGE persistentes: el Thermal los guarda, el Sweep Pro los lee, usa y dibuja. |
| F5 | Sweep Pro: DUDOSO, botones dentro del recuadro 2, recuadros manuales de barrido y rompimiento, recuadro de vela compuesta con flechas. |
| F6 | Indicador de Vela Compuesta (FAST). |
| F7 | Gráfico de 5 minutos con datos de 1 minuto. |
| F8 | Documentación, manual, pruebas, fusión a `main`. |
