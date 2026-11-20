# Correcciones v5 — tres indicadores FAST (20-11-2026)

Texto del propietario, sin cambios (recibido el 20-11-2026). Plan y decisiones: [PLAN_V5.md](PLAN_V5.md).

```text
Objetivo General y Arquitectura del Sistema
Separar la lógica en tres indicadores independientes enfocados en la operativa de Barrido de Liquidez, optimizados al 100% para evitar retrasos de carga y sin almacenar historiales pesados en memoria.
________________________________________
1. Indicador: LVN Thermal Profile (Independiente)
•	Objetivo: Eliminar los tiempos largos de carga separando este cálculo del resto de herramientas.
•	Comportamiento en memoria: No debe guardar datos históricos pesados.
•	Persistencia visual: Una vez que cargue y se apague o cierre el panel, el sistema debe seguir reconociendo y respetando en el gráfico:
o	Las marcas y líneas trazadas de forma automática.
o	Las marcas y líneas trazadas de forma manual mediante los botones EDGE y LVN.
•	Integración operativa: Toda esta información (LVN automáticos/manuales y EDGE) servirá como base activa para buscar nuevos niveles de LVN, EDGE y validar Gatillos, incluso con el panel cerrado.
________________________________________
2. Indicador: Vela Compuesta y Gatillos Volumétricos (Independiente)
•	Objetivo: Unificar la lectura de Vela Compuesta, volumen y desequilibrios en un solo panel ligero.
•	Botones principales del panel:
o	Gatillo Comp: Activa la detección por vela compuesta.
o	Gatillo Velas Volumétricas: Activa la detección por volumen.
o	FVG: Muestra u oculta los Fair Value Gaps.
•	Reglas de Volumen y Contratos:
o	Margen base: Detectar la unión/confluencia cuando exista un margen de 2,300 contratos negociados.
o	Configuración dinámica: Controles en el mismo panel para subir o bajar la cantidad de contratos requeridos según el horario de sesión.
o	Desplegable de horarios: Menú desplegable con los horarios clave y la cantidad de contratos sugeridos para cada franja.
o	Cálculo automático sugerido: El sistema debe calcular y sugerir un umbral de volumen basándose en el promedio de las últimas 20 velas.
________________________________________
3. Indicador: Sweep Pro (Antes Session Sweeps Pro)
•	Objetivo: Identificar barridos de liquidez externa, permitir el marcado manual y validar modelos de entrada.
•	Ubicación en interfaz: Todo debe integrarse en el panel principal, dentro del detalle de Barrido (sección 2. Barrido de Liquidez Externa y Recuperación).
•	Detección automática (mantiene funciones anteriores):
o	Barrido Real
o	Barrido Real Prime
o	Rompimiento
o	Dudoso
•	Controles de Marcado Manual (Recuadros):
o	Botón 1 (Barrido Real): Permite dibujar/marcar manualmente un recuadro de Barrido Real.
o	Botón 2 (Barrido Real Prime): Permite dibujar/marcar manualmente un recuadro de Barrido Real Prime.
o	Botón 3 (Rompimiento): Permite dibujar/marcar manualmente un recuadro de Rompimiento.
o	Lógica de activación: Cuando el usuario marque manualmente un recuadro (especialmente el de Barrido Real), el indicador debe tomarlo como referencia activa para iniciar la verificación de cumplimiento de los Modelos de Entrada (1, 2, 3 o A, B, C).
•	Cálculo de Rango por Vela Compuesta:
o	Botón de Recuadro de Vela Compuesta: Dibuja un recuadro que calcula el rango de la vela compuesta.
o	Señal de ruptura: Cuando el precio rompa ese pequeño rango, debe mostrar una flecha direccional:
	Flecha Verde: Rompe alcista.
	Flecha Roja: Rompe bajista.
________________________________________
Resumen del Flujo Operativo Integrado
1.	Se cargan los niveles en LVN Thermal Profile (automáticos o con botones EDGE/LVN) y se cierra el panel para liberar recursos.
2.	En Sweep Pro, al detectarse o marcarse manualmente un Barrido Real (dentro del apartado 2. Barrido de Liquidez Externa y Recuperación), se activa el conteo/verificación de los modelos de entrada (1, 2, 3 / A, B, C).
3.	Se confirman las entradas apoyándose en los niveles vigentes del perfil térmico (LVN/EDGE), la ruptura del rango de vela compuesta (flecha verde/roja) y los gatillos de volumen/FVG del segundo indicador.
4.	CUANDOCAMBIE A 5 MIN, TIENE QUE DIBUJARSE LAS ZONAS DE NY MAÑANA Y TARDE, ASIATICO, LONDRES,PREMERCADO, LOS BARRIDOS DE LIQUIDEZ,Y LOS GATILLOS
5.	PERO LOS DATOS SIGUEN SIENDO DE 1 MIN
6.	NUEVOS NOMBRES:       
1  PANEL DE THERMAL VOLUME PROFILE + LNV MNQ, POR NUEVO NOMBRE: LNV THERMAL PROFILE (fAST)
2 CAMBIOS EN EL PANEL DEL INDICADOR GLOBAL SESSION SWEEP ,PRIMERO SU NOMBRE QUE VA A SER Sweep Pro (FAST)
3 INDICADOR DE VELA COMPUESTA (FAST)
```
