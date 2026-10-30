using System;
using System.Collections.Generic;
using System.Linq;

using Completo.Nucleo;

using static Velas;

/// <summary>
/// Escenarios de los modelos con el motor completo. Para aislar cada regla, el contexto de 1H, la referencia de
/// liquidez y el mapa LVN se fijan directamente; las velas de 1M (con volumen por precio) recorren todo el resto.
/// Hora base: 28/10/2026 13:00 UTC = 09:00 de Nueva York; el barrido ocurre a las 09:40 NY, en la sesión de NY.
/// </summary>
internal static class PruebasModelos
{
	private static readonly DateTime T0 = new(2026, 10, 28, 13, 0, 0);

	private static DateTime T(int minuto, DateTime? inicio = null) => (inicio ?? T0).AddMinutes(minuto);

	private static MotorCompleto Preparar(Action<ConfiguracionCompleto>? ajustar = null, Sesgo sesgo = Sesgo.Alcista,
		IEnumerable<ZonaLvn>? zonas = null, int calentamiento = 40, DateTime? inicio = null, bool conMaximo = false)
	{
		var cfg = new ConfiguracionCompleto { Instrumento = "NQZ6", ValoresMonetariosVerificados = true };
		ajustar?.Invoke(cfg);
		var m = new MotorCompleto(cfg);
		var t = inicio ?? T0;

		foreach (var v in Planas(t, calentamiento, 20010))
			m.Procesar(v);

		Fijar(m.Estructura, "Sesgo", sesgo);
		m.Liquidez.Referencias.Add(new ReferenciaLiquidez
		{
			Id = 900, Tipo = TipoLiquidez.Externa, Origen = OrigenReferencia.Londres, EsMaximo = false, Precio = 20000m,
			DisponibleUtc = t.AddMinutes(-60), Etiqueta = "Mínimo de Londres 28/10"
		});

		if (conMaximo)
		{
			m.Liquidez.Referencias.Add(new ReferenciaLiquidez
			{
				Id = 901, Tipo = TipoLiquidez.Externa, Origen = OrigenReferencia.Londres, EsMaximo = true, Precio = 20013m,
				DisponibleUtc = t.AddMinutes(-60), Etiqueta = "Máximo de Londres 28/10"
			});
		}

		m.Lvn.Mapas.Add(new MapaLvn { Version = 1, CreadoUtc = t, Referencia = 20010m, Zonas = (zonas ?? new[] { Zona(7, 20070, 20072, PrioridadLvn.Alta) }).ToList() });
		return m;
	}

	/// <summary>Barrido del mínimo de Londres (20000): mínimo 19998, cierre 20001, volumen climático y ventas agresivas.</summary>
	private static EntradaVela1M Barrido(DateTime? inicio = null, decimal volumen = 300, decimal maximo = 20010, bool conNiveles = true) =>
		V(T(40, inicio), 20009, maximo, 19998, 20001, volumen, -60, conNiveles: conNiveles);

	/// <summary>Recuperación (cierre ≥ 20000 + 16 ticks) que además es gatillo largo (POC y VA abajo).</summary>
	private static EntradaVela1M RecuperacionGatillo(DateTime? inicio = null, decimal maximo = 20006) =>
		V(T(41, inicio), 20002, maximo, 20001, 20005.5m, 150, 40, Abajo(20001, maximo));

	private static string Cabecera(MotorCompleto m) => m.Instantanea(T(60), ZonaPresentacion.CiudadDeMexico, "prueba", "prueba").Cabecera;

	public static void Ejecutar(Comprobador c)
	{
		M1Completo(c);
		Bloqueos(c);
		Barridos(c);
		Secuencia(c);
		M2(c);
		Gestion(c);
		Reporte(c);
	}

	private static void M1Completo(Comprobador c)
	{
		c.Seccion("M1: barrido → recuperación → gatillo → riesgo → señal → TP simulado");
		var m = Preparar();
		m.Procesar(Barrido());
		c.Check("barrido externo detectado y en evaluación", $"{m.Barridos.Count} {m.Barridos[0].Tipo.Texto()} {m.Barridos[0].Estado}", "1 EXTERNA EnEvaluacion");
		c.Check("penetración 8 ticks; extremo 19998", $"{m.Barridos[0].PenetracionTicks} {m.Barridos[0].Extremo}", "8 19998");

		m.Procesar(RecuperacionGatillo());
		var s = m.Senales.SingleOrDefault();
		c.Check("una señal M1 LONG EXTERNA, confirmada al cierre de la vela de recuperación", s is null ? "ninguna" : $"{s.Titulo} {s.ConfirmadaUtc:HH:mm}", "M1 LONG EXTERNA 13:42");

		if (s is null)
			return;

		var p = s.Plan;
		c.Check("entrada = cierre del gatillo; SL = extremo 19998 − 12 ticks", $"{p.Entrada} {p.Sl}", "20005.5 19995.00");
		c.Check("TP = borde inferior del LVN amarillo 20070 − 2 ticks", $"{p.Tp} LVN {p.Objetivo?.Id}", "20069.50 LVN 7");
		c.Check("riesgo por contrato = 42 ticks × 0,50 + 1,50 + 2 × 0,50", p.RiesgoPorContrato.ToString("0.00"), "23.50");
		c.Check("micros = floor(250 / 23,50) = 10; riesgo total 235", $"{p.Cantidad} {p.RiesgoTotal:0.00}", "10 235.00");
		c.Check("RR neto = (256 × 0,50 − 2,50) / 23,50", p.RrNeto.ToString("0.00"), "5.34");
		c.Check("absorción confirmada (4/4) con el cambio de delta de la vela siguiente", m.Barridos[0].Absorcion.Texto(), "CONFIRMADA (4/4)");
		c.Check("evento consumido por la señal; M2 no se reutiliza", $"{m.Barridos[0].Estado} {m.Oportunidades[0].M2}", "Consumido Consumido");

		// Subida de 5 puntos por vela: el TP se alcanza en la vela de 13:54.
		for (var i = 0; i < 14; i++)
		{
			var o = 20005.5m + 5 * i;
			m.Procesar(V(T(42 + i), o, o + 5.5m, o - 0.25m, o + 5, 120, 30));
		}

		var sim = s.Resultado!;
		c.Check("simulación: cerrada en el TP a las 13:55", $"{sim.Abierta} {sim.MotivoSalida} {sim.SalidaUtc:HH:mm}", "False TP EN LVN 7 13:55");
		c.Check("resultado = 10 × (256 ticks × 0,50 − 2,50) = 1255 USD = 5,34 R", $"{sim.ResultadoUsd:0.00} {sim.R:0.00}", "1255.00 5.34");

		var total = m.Simulacion.Calcular("TOTAL");
		c.Check("estadística: 1 operación, 100 % de acierto", $"{total.Operaciones} {total.Acierto:0}", "1 100");
		c.Check("estadística por modelo y liquidez", string.Join(" | ", m.Simulacion.Desglose().Select(e => $"{e.Nombre} {e.Operaciones}")), "TOTAL 1 | M1 1 | M2 0 | EXTERNA 1 | INTERNA 0");
	}

	private static void Bloqueos(Comprobador c)
	{
		c.Seccion("Control de riesgo: el patrón se forma pero la señal se bloquea");

		string Bloqueo(MotorCompleto m) => m.SetupsBloqueados.FirstOrDefault()?.Motivo ?? $"sin bloqueo (señales {m.Senales.Count})";

		var noVerificados = Preparar(cfg => cfg.ValoresMonetariosVerificados = false);
		noVerificados.Procesar(Barrido());
		noVerificados.Procesar(RecuperacionGatillo());
		c.Check("valores monetarios sin verificar (por defecto): bloqueada", Bloqueo(noVerificados), "VALORES MONETARIOS NO VERIFICADOS (ajuste de riesgo)");
		c.Check("la oportunidad queda BLOQUEADA y no hay señal", $"{noVerificados.Oportunidades[0].M1.Texto()} {noVerificados.Senales.Count}", "BLOQUEADO 0");
		c.Check("cabecera del reporte", Cabecera(noVerificados), "ESPERAR — SEÑAL BLOQUEADA");

		var sinObjetivo = Preparar(zonas: new[] { Zona(7, 19950, 19952, PrioridadLvn.Alta), Zona(8, 20060, 20061, PrioridadLvn.Secundaria) });
		sinObjetivo.Procesar(Barrido());
		sinObjetivo.Procesar(RecuperacionGatillo());
		c.Check("sin LVN amarillo por encima (el naranja no sirve de TP)", Bloqueo(sinObjetivo), "SIN OBJETIVO LVN AMARILLO DISPONIBLE");

		var poco = Preparar(cfg => cfg.RiesgoUsd = 10);
		poco.Procesar(Barrido());
		poco.Procesar(RecuperacionGatillo());
		c.Check("riesgo de 10 USD < 23,50 por contrato: cantidad 0", Bloqueo(poco), "SEÑAL BLOQUEADA POR RIESGO (cantidad 0)");

		var cerca = Preparar(zonas: new[] { Zona(7, 20010, 20011, PrioridadLvn.Alta) });
		cerca.Procesar(Barrido());
		cerca.Procesar(RecuperacionGatillo());
		c.Check("objetivo demasiado cerca: RR neto insuficiente", Bloqueo(cerca), "RR NETO 0.23 MENOR QUE EL MÍNIMO 1.00");

		var otro = Preparar(cfg => cfg.Instrumento = "ESZ6");
		otro.Procesar(Barrido());
		otro.Procesar(RecuperacionGatillo());
		c.Check("instrumento no permitido", Bloqueo(otro), "INSTRUMENTO NO PERMITIDO (ESZ6)");
	}

	private static void Barridos(Comprobador c)
	{
		c.Seccion("Barridos: cruce nuevo, doble, contexto, ventana, footprint");

		var doble = Preparar(conMaximo: true);
		doble.Procesar(Barrido(maximo: 20014.25m));
		c.Check("la misma vela barre máximo y mínimo: los dos eventos bloqueados",
			string.Join(" | ", doble.Barridos.Select(b => $"{b.Direccion.Texto()} {b.Motivo}")), "LONG BARRIDO DOBLE — SECUENCIA AMBIGUA | SHORT BARRIDO DOBLE — SECUENCIA AMBIGUA");
		doble.Procesar(RecuperacionGatillo());
		c.Check("sin señal tras un barrido doble", doble.Senales.Count.ToString(), "0");

		var tocada = Preparar(calentamiento: 39);
		tocada.Procesar(V(T(39), 20010, 20012, 19999.75m, 20010));
		tocada.Procesar(Barrido());
		var referencia = tocada.Liquidez.Referencias.First(r => r.Id == 900);
		c.Check("la vela anterior ya había cruzado el nivel: no es un barrido nuevo", $"{tocada.Barridos.Count} {referencia.Estado} ({referencia.MotivoFin})",
			"0 Rota (superada sin cruce nuevo (ya estaba cruzada))");

		var bajista = Preparar(sesgo: Sesgo.Bajista);
		bajista.Procesar(Barrido());
		c.Check("barrido de mínimos con contexto 1H bajista: no es contrario al contexto", bajista.Barridos[0].Motivo, "CONTEXTO 1H BAJISTA: NO ES UN BARRIDO CONTRARIO AL CONTEXTO");

		var indeterminado = Preparar(sesgo: Sesgo.NoDeterminado);
		indeterminado.Procesar(Barrido());
		c.Check("contexto 1H no determinado", indeterminado.Barridos[0].Motivo, "CONTEXTO 1H NO DETERMINADO — ESPERAR");

		var fuera = new DateTime(2026, 10, 28, 12, 0, 0);
		var sinVentana = Preparar(inicio: fuera);
		sinVentana.Procesar(Barrido(fuera));
		c.Check("12:40 UTC (08:40 NY): entre Londres y Nueva York", sinVentana.Barridos[0].Motivo, "FUERA DE LAS VENTANAS ADMITIDAS");

		var sinHuella = Preparar();
		sinHuella.Procesar(Barrido(conNiveles: false));
		c.Check("vela del barrido sin volumen por precio", sinHuella.Barridos[0].Motivo, "FOOTPRINT INCOMPLETO EN LA VELA DEL BARRIDO");

		var lento = Preparar();
		lento.Procesar(Barrido());

		for (var i = 0; i < 11; i++)
			lento.Procesar(V(T(41 + i), 20001, 20003, 20000.5m, 20002));

		c.Check("sin cierre de recuperación en 10 minutos: vencido", $"{lento.Barridos[0].Estado} · {lento.Barridos[0].Motivo} · M1 {lento.Oportunidades[0].M1.Texto()}",
			"Vencido · SIN RECUPERACIÓN EN 10 MIN · M1 VENCIDO");
	}

	private static void Secuencia(Comprobador c)
	{
		c.Seccion("Secuencia: gatillo posterior, extremo actualizado, absorción obligatoria, cambio de contexto");

		var despues = Preparar();
		despues.Procesar(Barrido());
		despues.Procesar(V(T(41), 20002, 20006, 20001, 20005.5m, 150, 40, Arriba(20001, 20006)));
		c.Check("recuperación sin gatillo (POC arriba): esperando gatillo", $"{despues.Oportunidades[0].M1.Texto()} · {despues.Oportunidades[0].MotivoM1}",
			"EN FORMACIÓN · ESPERANDO GATILLO (vela direccional y footprint)");

		despues.Procesar(V(T(42), 20004, 20008, 19997, 20006, 150, 30, Abajo(19997, 20008)));
		var s = despues.Senales.SingleOrDefault();
		c.Check("gatillo en la vela siguiente, que hizo un nuevo mínimo: SL desde el extremo actualizado 19997",
			s is null ? "ninguna" : $"{s.ConfirmadaUtc:HH:mm} SL {s.Plan.Sl}", "13:43 SL 19994.00");

		var obligatoria = Preparar(cfg => cfg.ModoAbsorcion = ModoFiltro.Obligatorio);
		obligatoria.Procesar(Barrido(volumen: 100));
		obligatoria.Procesar(RecuperacionGatillo());
		c.Check("absorción obligatoria sin volumen climático: M1 invalidado",
			$"{obligatoria.Oportunidades[0].M1.Texto()} · {obligatoria.Oportunidades[0].MotivoM1}", "INVALIDADO · ABSORCIÓN NO CONFIRMADA (OBLIGATORIA)");

		var cambio = Preparar();
		cambio.Procesar(Barrido());
		cambio.Procesar(V(T(41), 20002, 20006, 20001, 20005.5m, 150, 40, Arriba(20001, 20006)));
		Fijar(cambio.Estructura, "Sesgo", Sesgo.Bajista);
		cambio.Procesar(V(T(42), 20005, 20007, 20003, 20006));
		c.Check("el contexto 1H cambia antes del gatillo: oportunidad invalidada",
			$"{cambio.Oportunidades[0].M1.Texto()} · {cambio.Oportunidades[0].MotivoM1}", "INVALIDADO · CAMBIO DE CONTEXTO 1H (BAJISTA)");
	}

	private static void M2(Comprobador c)
	{
		c.Seccion("M2: ruptura de LVN con desplazamiento → retroceso → respuesta → gatillo");
		var zonas = new[] { Zona(21, 20015, 20016, PrioridadLvn.Secundaria), Zona(7, 20070, 20072, PrioridadLvn.Alta) };

		var sinFvg = Preparar(cfg => cfg.ActivarM1 = false, zonas: zonas);
		sinFvg.Procesar(Barrido());
		sinFvg.Procesar(V(T(41), 20002, 20012, 20001, 20005.5m, 150, 40));
		sinFvg.Procesar(V(T(42), 20006, 20019, 20005.5m, 20018, 300, 120));
		var op = sinFvg.Oportunidades[0];
		c.Check("ruptura del LVN 21 (secundario) confirmada", $"{op.ZonaM2?.Id} · {op.MotivoM2}", "21 · RUPTURA DEL LVN 21 CONFIRMADA: ESPERANDO RETROCESO");

		sinFvg.Procesar(V(T(43), 20017, 20018, 20011.75m, 20017.5m, 200, 60, Abajo(20011.75m, 20018)));
		var s = sinFvg.Senales.SingleOrDefault();
		c.Check("retroceso al LVN y respuesta con gatillo: señal M2 LVN SIN FVG",
			s is null ? $"ninguna ({op.MotivoM2})" : $"{s.Titulo} · {s.Variante}", "M2 LONG EXTERNA · M2 LVN SIN FVG");

		if (s is not null)
		{
			c.Check("SL = extremo del rechazo en el LVN 20011.75 − 12 ticks; TP en el LVN amarillo",
				$"SL {s.Plan.Sl} TP {s.Plan.Tp} micros {s.Plan.Cantidad}", "SL 20008.75 TP 20069.50 micros 12");
		}

		var conFvg = Preparar(cfg => { cfg.ActivarM1 = false; cfg.AnclajeSlFvg = AnclajeSlFvg.BordeFvg; }, zonas: zonas);
		conFvg.Procesar(Barrido());
		conFvg.Procesar(V(T(41), 20002, 20006, 20001, 20005.5m, 150, 40));
		conFvg.Procesar(V(T(42), 20006, 20019, 20005.5m, 20018, 300, 120));
		conFvg.Procesar(V(T(43), 20017, 20018, 20014.5m, 20017.5m, 200, 60, Abajo(20014.5m, 20018)));
		var sf = conFvg.Senales.SingleOrDefault();
		c.Check("FVG alcista (20006–20014.5) junto al LVN y tocado: variante M2 LVN + FVG",
			sf is null ? $"ninguna ({conFvg.Oportunidades[0].MotivoM2})" : $"{sf.Variante} FVG {sf.Fvg?.Inferior}–{sf.Fvg?.Superior}", "M2 LVN + FVG FVG 20006–20014.5");
		c.Check("anclaje del SL en el borde del FVG: 20006 − 12 ticks", sf?.Plan.Sl.ToString() ?? "-", "20003.00");

		var exige = Preparar(cfg => { cfg.ActivarM1 = false; cfg.ExigirFvg = true; }, zonas: zonas);
		exige.Procesar(Barrido());
		exige.Procesar(V(T(41), 20002, 20012, 20001, 20005.5m, 150, 40));
		exige.Procesar(V(T(42), 20006, 20019, 20005.5m, 20018, 300, 120));
		exige.Procesar(V(T(43), 20017, 20018, 20011.75m, 20017.5m, 200, 60, Abajo(20011.75m, 20018)));
		c.Check("FVG obligatorio y ausente: sin señal", $"{exige.Senales.Count} · {exige.Oportunidades[0].MotivoM2}", "0 · FVG OBLIGATORIO AUSENTE O SIN CONTACTO");

		var fallida = Preparar(cfg => cfg.ActivarM1 = false, zonas: zonas);
		fallida.Procesar(Barrido());
		fallida.Procesar(V(T(41), 20002, 20012, 20001, 20005.5m, 150, 40));
		fallida.Procesar(V(T(42), 20006, 20019, 20005.5m, 20018, 300, 120));
		fallida.Procesar(V(T(43), 20017, 20018, 20012, 20013));
		c.Check("cierre al otro lado del LVN tras la ruptura: invalidado", $"{fallida.Oportunidades[0].M2.Texto()} · {fallida.Oportunidades[0].MotivoM2}",
			"INVALIDADO · RUPTURA FALLIDA: CIERRE AL OTRO LADO DEL LVN 21");

		var lenta = Preparar(cfg => cfg.ActivarM1 = false, zonas: zonas);
		lenta.Procesar(Barrido());
		lenta.Procesar(V(T(41), 20002, 20012, 20001, 20005.5m, 150, 40));
		lenta.Procesar(V(T(42), 20015.5m, 20017.25m, 20015.25m, 20017, 100, 20));
		c.Check("cruce del LVN con rango de 2 puntos (< 1,2 × rango medio): sin desplazamiento, no es ruptura", $"{lenta.Oportunidades[0].ZonaM2?.Id.ToString() ?? "ninguna"} · {lenta.Oportunidades[0].MotivoM2}",
			"ninguna · CRUCE DE LVN SIN DESPLAZAMIENTO SUFICIENTE");
	}

	/// <summary>Señal M1 de referencia (entrada 20005.5, SL 19995, TP 20069.5) para las pruebas de gestión.</summary>
	private static (MotorCompleto M, Senal S) ConSenal(Action<ConfiguracionCompleto>? ajustar = null)
	{
		var m = Preparar(ajustar);
		m.Procesar(Barrido());
		m.Procesar(RecuperacionGatillo());
		return (m, m.Senales.Single());
	}

	private static void Gestion(Comprobador c)
	{
		c.Seccion("Gestión simulada: SL, ambigüedad, EMA, agotamiento + arranque contrario");

		var (m1, s1) = ConSenal();
		m1.Procesar(V(T(42), 20005, 20006, 19994, 19996));
		c.Check("toca el SL: −1 R", $"{s1.Resultado!.MotivoSalida} {s1.Resultado.ResultadoUsd:0.00} {s1.Resultado.R:0.00}", "SL INICIAL -235.00 -1.00");

		var (m2, s2) = ConSenal();
		m2.Procesar(V(T(42), 20005, 20070, 19990, 20030));
		c.Check("una vela toca SL y TP: ambigua, cuenta como SL", $"{s2.Resultado!.Ambigua} {s2.Resultado.ResultadoUsd:0.00}", "True -235.00");

		var (m3, s3) = ConSenal();
		m3.Procesar(V(T(42), 20005, 20006, 20002, 20003));
		c.Check("cierre bajo la EMA 15: se cierra al cierre (nota del propietario)", $"{s3.Resultado!.MotivoSalida} a {s3.Resultado.PrecioSalida}", "CIERRE CRUZADO POR LA EMA 15 (1M) a 20003");

		var (m4, s4) = ConSenal(cfg => cfg.SalidaPorEma = false);
		m4.Procesar(V(T(42), 20005, 20006, 20002, 20003));
		c.Check("con la salida por EMA desactivada: solo advertencia", $"{s4.Resultado!.Abierta} {s4.Resultado.Advertencia}", "True ADVERTENCIA: CRUCE ADVERSO DE EMA");

		var (m5, s5) = ConSenal();

		for (var i = 0; i < 6; i++)
		{
			var o = 20005.5m + 5 * i;
			m5.Procesar(V(T(42 + i), o, o + 5.5m, o - 0.25m, o + 5, 120, 30));
		}

		// Candidata: POC y VA arriba y ninguna compra en el máximo (subasta terminada).
		var candidata = V(T(48), 20035.5m, 20040, 20035, 20039.5m, 200, 20, Arriba(20035, 20040));
		var alto = candidata.Niveles.Last();
		candidata.Niveles[^1] = new NivelEntrada(alto.Precio, alto.Bid + alto.Ask, 0);
		m5.Procesar(candidata);
		c.Check("candidata de agotamiento: solo aviso, la operación sigue abierta", $"{s5.Resultado!.Abierta} {s5.Resultado.Advertencia}", "True POSIBLE AGOTAMIENTO — FALTA ARRANQUE CONTRARIO");

		m5.Procesar(V(T(49), 20039, 20039.5m, 20033, 20034.25m, 200, -40, Arriba(20033, 20039.5m)));
		c.Check("arranque contrario confirmado: salida", $"{s5.Resultado.Abierta} {s5.Resultado.MotivoSalida} a {s5.Resultado.PrecioSalida}",
			"False SALIDA CONFIRMADA: AGOTAMIENTO + ARRANQUE CONTRARIO a 20034.25");
	}

	private static void Reporte(Comprobador c)
	{
		c.Seccion("Reporte: la cabecera dice ESPERAR mientras falte algo obligatorio");

		var vacio = new MotorCompleto(new ConfiguracionCompleto());
		c.Check("sin datos", vacio.Instantanea(T0, ZonaPresentacion.CiudadDeMexico, "", "").Cabecera, "SIN DATOS — ESPERANDO VELAS DE 1M DEL MAESTRO");

		c.Check("contexto no determinado", Cabecera(Preparar(sesgo: Sesgo.NoDeterminado)), "CONTEXTO 1H NO DETERMINADO — ESPERAR");
		c.Check("contexto válido sin barrido", Cabecera(Preparar()), "ESPERAR — SIN OPORTUNIDAD ACTIVA");

		var enCurso = Preparar();
		enCurso.Procesar(Barrido());
		c.Check("barrido sin recuperación", Cabecera(enCurso), "ESPERAR RECUPERACIÓN");

		var (m, _) = ConSenal();
		var r = m.Instantanea(T(60), ZonaPresentacion.CiudadDeMexico, "Maestro 1M", "1M y 5M");
		c.Check("señal confirmada", r.Cabecera, "SEÑAL CONFIRMADA — M1 LONG EXTERNA");
		c.Check("recién confirmada (no una señal antigua)", r.Resumen[0].Texto, "SEÑAL RECIÉN CONFIRMADA");
		c.Check("hora en Ciudad de México (13:42 UTC = 07:42 CDMX)", r.Resumen.Any(l => l.Texto == "Confirmada al cierre: 07:42 CDMX (1M)").ToString(), "True");
		c.Check("deja claro que no hay ejecución real", r.Resumen[^1].Texto, "Ejecución real: NO CONFIRMADA · órdenes enviadas por el indicador: NINGUNA");
		c.Check("10 secciones (8 + estadística + eventos)", r.Secciones.Count.ToString(), "10");

		var condiciones = r.Secciones[2].Lineas.Select(l => l.Texto).ToList();
		c.Check("condiciones de M1 con su estado", condiciones.Any(t => t.Contains("✓ Recuperación según configuración · Cumplida")).ToString(), "True");

		for (var i = 0; i < 3; i++)
			m.Procesar(V(T(42 + i), 20006, 20012, 20005, 20011));

		c.Check("tres minutos después: señal anterior, con su antigüedad", m.Instantanea(T(60), ZonaPresentacion.CiudadDeMexico, "", "").Resumen[0].Texto,
			"SEÑAL ANTERIOR · confirmada hace 3 min (no es una entrada nueva)");

		var json = System.Text.Json.JsonDocument.Parse(m.ExportarJson(T(60)));
		c.Check("instantánea JSON válida con la señal y su plan", json.RootElement.GetProperty("senales")[0].GetProperty("plan").GetProperty("Cantidad").GetInt32().ToString(), "10");

		var csv = m.ExportarCsv(ZonaPresentacion.CiudadDeMexico).Split('\n', StringSplitOptions.RemoveEmptyEntries);
		c.Check("diario CSV: cabecera + una operación", $"{csv.Length} {csv[0][..20]}", "2 senal;modelo;variant");
	}
}
