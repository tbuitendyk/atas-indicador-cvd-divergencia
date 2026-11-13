# Correcciones al indicador Session Sweep Pro (v4)

Texto del propietario, sin cambios (convertido del RTF "Correcciones_al_indicador_Session_Sweep_Pro.rtf", recibido el 13-11-2026). Plan y decisiones: [PLAN_V4.md](PLAN_V4.md).

```text

Correcciones al indicador Session Sweep Pro

la mas importante es que tiene que ser un solo indicador, no un conjunto de indicadores, LNV, CVD,SESIONES, STAKERS, JUNTA TODOS EN UNO QUE SU NUEVO NOMBRE SEA: INDICADOR GLOBAL SESSION SWEEP

Necesito refactorizar y mejorar el indicador "Session Sweeps Pro" para corregir la detección de barridos de liquidez con absorción, limpiar el ruido visual del gráfico y reestructurar por completo el panel informativo flotante



Orden y Jerarquía del Panel Principal (De arriba hacia abajo):

en la actualidad desaparece el indicador con las descripciones si seleccionamos el grafico, por favorque no desaparezca a menos que nosotros le movamos o minimicemos, 



Encabezado (Estado y Activo):

Mostrar en la parte superior el Estado del Indicador (ej. Calibrado / Activo), el Activo actual detectado automáticamente (NQ o MNQ), seguido inmediatamente por un Resumen Ejecutivo del Contexto (lo que antes estaba en el punto 3).

. Corrección de Lógica: Barridos de Liquidez (Sweeps) y Absorción Institucional

Problema actual: El indicador invalida un barrido de liquidez si el precio no revierte en un máximo de 7 velas, marcándolo erróneamente como "Falso". En sesiones reales (como la ruptura de máximos de Premercado/NY), el precio puede tomar entre 10 y 12 velas en consolidar y absorber liquidez antes de girar., y cuando supere y haya un rompimiento por que en vez de haber un Barrido de liquidez y giro, simplemente siguio en la misma direccion, en ese caso marcarlo como  Rompimiento, en vez de Falso





Nueva lógica de validación (Vela Compuesta y Order Flow):

No exigir únicamente un retroceso rápido o violento en pocas velas. Ampliar la ventana de validación y evaluar el comportamiento como una vela compuesta en la zona de ruptura.

Calcular la suma total de volumen (Compras vs. Ventas) en el rango de ruptura.

Detectar Absorción Real mediante el Cumulative Volume Delta (CVD): validar el barrido cuando se observe un alto intercambio de volumen en el extremo acompañado de un giro o divergencia en el Delta acumulado (cambio de positivo a negativo en máximos, o de negativo a positivo en mínimos).

Confirmar el modelo estructural una vez que el precio ejecute el BOS, genere un Fair Value Gap (FVG) a favor del giro y reaccione/busque un Low Volume Node (LVN).

2. Mejoras Visuales en el Gráfico y Alertas

Líneas de Máximos y Mínimos de Sesión (Asia, Londres, Premercado, NY):

Sustituir las actuales líneas punteadas del color de cada sesión por una línea sólida de color blanco  (configurable en color y estilo, con opción para desactivar o mantener la punteada original) para lograr máximo contraste visual.

Comportamiento en NY: Trazar la línea blanca desde el último máximo o mínimo reciente hacia la derecha. Esta línea debe detenerse al ser rota (no debe extenderse infinitamente a la derecha después de la ruptura).

Sistema de Alertas: Al romper la línea de sesión (especialmente NY), disparar una alarma sonora y mostrar un mensaje de alerta en el extremo superior derecho del indicador.

Filtrado de Fair Value Gaps (FVG) y BOS:

Agregar al menú de configuración la opción para marcar explícitamente los BOS de máximos y mínimos de Asia, Londres, Premercado y NY.

Limpieza de ruido visual: Añadir una opción configurable (True/False) para que el indicador dibuje únicamente los FVG que se formen DESPUÉS de un barrido de liquidez validado, ocultando los FVG irrelevantes previos.

Quitar todas las alertas de Iceberg, stops, imbalances que bloquean la vista y el panel de ejecución de órdenes.

solo mostrar las que sean muy importantes que ayudan a saber que hay un barrido de liquidez

quiero que cada uno de los indicadores Iceberg, Imbalance, Stops, y sweep, que tienen añadido suite, aparezcan como se ven los nativos de atas

se aceptan los 3 modelos de operativa como prioritarios



1. Redacción estructurada para la configuración del Panel Desplegable (Modo Resumen y Detalle)

A. Modalidad: ResumenPunto 1: Contexto y Estructura

Mostrar la dirección estructural en 1H: Alcista, Bajista o Rango.

Mostrar la alineación de 15m y 5m mediante iconografía limpia: flecha verde ascendente (▲) si es alcista, flecha roja descendente (▼) si es bajista, o un guion entre paréntesis (-) si está en rango.



Barrido de Liquidez Externa Y Recuperación, (Incluir la fecha y hora exacta del evento sincronizada al huso horario de Ciudad de México (CST/CDT). ): en caso que no tenga una recuperación inmediata, pero haya de 2 a12 o 14 velas haciendo una consolidacion, en donde hay un monto importante de comra venta, es decir volumen negociado en ese periodo de tiempo, se suma la absorsiony gira como un barrido de liquidez real

Nueva Dirección: ALCISTA (Verde Bold) / BAJISTA (Rojo Bold).

Información: Stops tomados y datos de Stop Trackers ProSuite.

Validación: Certificada como real por Iceberg desarrollado.

Recuperación 1m (16 Ticks): Confirmada a las 08:59 CDMX (Alcista verde, Bajista rojo, Pendiente naranja).

Recuperación 5m (16 Ticks): Confirmada a las 08:59 CDMX (Alcista verde, Bajista rojo, Pendiente naranja).

Resumen de Absorción y Modelos (Punto 3 y 3.1):

Mostrar el detalle técnico de las confluencias activas (ej. Absorción detectada, BOS confirmado externo , FVG activo). 

Recuadro sumario de absorción ejecutada: Iceberg, Stops, Sweeps, Stacked, Imbalance. Haciendo un recuadro de los resultados de cada uno y una sumatoria que diga Barrido de Liquidez Real o no completado o roto,de esa manera, el punto actual: 5 icebergs al 8 se mueven hasta el final y se reemplazan en orden los que seguían. 

en el detall MIN Dia anterior poner datos especificos de precio y hora 

Integración del Modelo 3: Se activa cuando un Iceberg es rebasado generando continuidad (invalida Automáticamente los Modelos 1 y 2, mostrando su señal específica).

Unificación (Punto 3.1): El grupo de indicadores de Iceberg, Stops, Sweeps, Stacked e Imbalance se integra al Indicador Completo (con opción de encender/apagar de forma configurable por peso). Sustituye al módulo de identificación de barrido externo actual.

Próximo LVN Objetivo y/o FVG (Grupo 4):especificando: esperar operacion alcista o bajista, sin 

Formato: Entre paréntesis (bajista/alcista) en color naranja.

Estado Ejecutivo: Verde si es alcista, Rojo si es bajista (con horario CDMX).

Alternativa: Próximo LVN objetivo (short): Sin objetivo LVN amarillo disponible.

Footprint Gatillo:

Estados: Válido cumplido alcista o bajista / Pendiente / Inválido./

Válido Cumplido: Texto en blanco acompañado de una flecha verde (alcista) o flecha roja (bajista).

Pendiente: Color naranja.

Inválido: Color rojo.

Plan de Riesgo:

Monto monetario o porcentaje fijo a arriesgar de la cuenta. Calcular y mostrar dinámicamente los niveles de ejecución: Entrada de referencia, Stop Loss (SL), Take Profit (TP) y Cantidad en riesgo por operación (en dólares y/o ticks según el activo NQ/MNQ).

(Nota: Asegúrate de que la numeración y el layout del panel queden limpios, compactos y sin duplicar información). 

Gestión de Micro-Impulso:

EMA 15 (1m): Precio de referencia (ej. 29,315.75). Activada por cierre cruzado de la EMA o vela de cambio de dirección verificada en X precio.

Calidad y Trazabilidad: Módulo activo.

Aviso Legal: Estadística simulada; no es un registro de operaciones reales.

Módulos Integrados y Panel Flotante Thermal Volume Profile / LVN Suite:

Sustituye el módulo anterior de Volume Profile + LVN.

Panel flotante independiente del principal con botones: Perfil, Rango, Zona LVN, Líneas LVN.

Las líneas trazadas mediante el botón LVN son válidas para el Modelo Operativo M2.

Opción obligatoria: Poder borrar las líneas al seleccionar un nuevo rango.

Gráfico principal: Opción para activar/desactivar Fair Value Gaps y visibilidad de señales de liquidez interna y externa.

Eliminación de panel redundante: Eliminar por completo el panel ubicado actualmente en la esquina superior derecha que muestra "H1 Direccionamiento / Asia / Londres / Premercado / Nueva York" e integrar toda la información en el panel principal de Session Sweeps Pro. 


 

```
