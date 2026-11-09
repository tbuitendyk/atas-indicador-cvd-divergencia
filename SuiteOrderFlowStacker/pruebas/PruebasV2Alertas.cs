using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>S10: alertas (13), CSV (14), recarga = CSV (T16) y gatillo manual en el CSV sin alerta (T28).</summary>
internal static class PruebasV2Alertas
{
	private static List<string> FilasDe(IEnumerable<NovedadV2> novedades, DateTime ahora) =>
		new[] { CsvV2.CabeceraBarridos }.Concat(novedades.Where(n => n.Evaluacion is not null && n.Tipo is "CONFIRMADO" or "UPGRADE PREMIUM" or "FALLIDO" or "RUPTURA FALLIDA")
			.Select(n => CsvV2.FilaBarrido(n.Evaluacion!, ahora, "TICKS"))).ToList();

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S10 CSV de barridos: columnas de la sección 14 y lectura de vuelta");
		var a = new SimMotor();
		a.Ruptura();
		var novedadesA = a.M.TomarNovedadesV2();
		var filas = FilasDe(novedadesA, Utc(new DateTime(2026, 10, 30, 9, 45, 0)));
		var columnas = CsvV2.CabeceraBarridos.Split(',');
		c.Check("cabecera con las columnas de 14 (EventId ... Transiciones) y T0Utc para restaurar",
			$"{columnas.Length} {columnas[1]} {columnas[^2]} {columnas[^1]}", "50 EventId Transiciones T0Utc");
		c.Check("cada fila tiene tantas celdas como la cabecera", filas.Skip(1).All(f => CsvV2.Separar(f).Count == columnas.Length) ? "sí" : "no", "sí");
		var leidos = CsvV2.LeerBarridos(filas);
		var pdh = a.Pdh!;
		var r = leidos.FirstOrDefault(x => x.EventId == pdh.Id);
		c.Check("la fila del PDH se lee igual (Id, T0 al milisegundo, L, estado, total, puertas)",
			r is null ? "no está" : $"{r.T0Utc == pdh.T0Utc} {F(r.L)} {r.Estado} {F(r.Total)} {r.PremiumGates == pdh.PremiumGates}", $"True 21420 Ruptura {F(pdh.Total)} True");
		c.Check("comillas: una celda con coma vuelve entera", string.Join("|", CsvV2.Separar("1," + CsvV2.Celda("a, \"b\"") + ",3")), "1|a, \"b\"|3");

		c.Seccion("S10 T16: recarga del historial = CSV (el CSV manda sobre lo que se pueda recalcular)");
		// Sin CSV, con RuptureMinConditions inalcanzable el historial NO llegaría a RUPTURA.
		var sinCsv = new SimMotor(ajustar: cfg => cfg.V2.RuptureMinConditions = 6);
		sinCsv.Ruptura();
		c.Check("sin CSV, el recálculo da otra cosa", sinCsv.Pdh?.Estado == EstadoBarrido.Ruptura ? "Ruptura" : "otro", "otro");
		var b = new SimMotor(ajustar: cfg => cfg.V2.RuptureMinConditions = 6, csv: leidos);
		b.Ruptura();
		var pdhB = b.Pdh;
		c.Check("con el CSV: mismo estado, total y puertas que en vivo", pdhB is null ? "sin evaluación" : $"{pdhB.Estado} {F(pdhB.Total)} {pdhB.PremiumGates} {pdhB.Restaurada}",
			$"Ruptura {F(pdh.Total)} {pdh.PremiumGates} True");
		c.Check("y los modelos reaccionan al estado restaurado (MOD4 armado como en vivo)",
			string.Join(" ", b.Setups.Where(x => x.Modelo == "MOD4").Select(x => x.Zona.Tipo).OrderBy(x => x)),
			string.Join(" ", a.Setups.Where(x => x.Modelo == "MOD4").Select(x => x.Zona.Tipo).OrderBy(x => x)));
		var novedadesB = b.M.TomarNovedadesV2();
		c.Check("la confirmación restaurada se marca (CSV)", novedadesB.Any(n => n.Tipo == "CONFIRMADO" && n.Texto.EndsWith("(CSV)", StringComparison.Ordinal)) ? "sí" : "no", "sí");
		var p0 = new ParametrosV2 { NewsTimes = "" };
		var despacho = new DespachoAlertasV2();
		var ctxA = new ContextoMensaje { Instrumento = "NQ", Contrato = "NQZ6", Precio = a.M.Precio };
		c.Check("cero alertas al recargar: las novedades del historial se descartan (prueba 28)",
			despacho.Procesar(novedadesB, enVivo: false, p0, ctxA).Count.ToString(), "0");
		var vivas = despacho.Procesar(novedadesA, enVivo: true, p0, ctxA);
		c.Check("en vivo, la misma ruptura avisa: RUPTURA / ACEPTACIÓN CONFIRMADA (contexto)",
			vivas.Any(x => x.Categoria == CategoriaAlerta.Ruptura && x.Texto.Contains("RUPTURA / ACEPTACIÓN CONFIRMADA (contexto)", StringComparison.Ordinal)) ? "sí" : string.Join(" / ", vivas.Select(x => x.Texto)), "sí");
		c.Check("una vez por evento: repetir las mismas novedades no vuelve a sonar", despacho.Procesar(novedadesA, enVivo: true, p0, ctxA).Count.ToString(), "0");

		c.Seccion("v3 S14 Alertas: solo las cinco del prompt (15), NOTICIA, una vez por evento");
		var p = new ParametrosV2 { NewsTimes = "" };
		var ctx = new ContextoMensaje { Instrumento = "NQ", Contrato = "NQZ6", Mt5 = "MT5: APAGADO" };
		var nivel = new NivelLiquidez { Id = 1, Precio = 21380, Lado = Lado.Sellside, Origen = "Dia anterior", Grupo = "dia" };
		EvaluacionBarrido Ev(EstadoBarrido estado, bool noticia = false) => new() { Id = 99, Nivel = nivel, Signo = -1, L = 21380, Estado = estado, Total = 72, Noticia = noticia, T0Utc = Utc(new DateTime(2026, 10, 30, 9, 50, 0)) };
		NovedadV2 N(string tipo, EvaluacionBarrido e, SetupModelo? s = null) => new() { Tipo = tipo, Evaluacion = e, Setup = s, Utc = Utc(new DateTime(2026, 10, 30, 9, 55, 0)), Texto = tipo };
		AlertaV2? Al(NovedadV2 n, ParametrosV2? q = null) => PoliticaAlertasV2.Evaluar(n, q ?? p, ctx);
		string Cat(AlertaV2? x) => x?.Categoria.ToString() ?? "-";

		c.Check("REAL, PREMIUM y RUPTURA confirmados: sus tres categorías",
			$"{Cat(Al(N("CONFIRMADO", Ev(EstadoBarrido.Real))))} {Cat(Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium))))} {Cat(Al(N("CONFIRMADO", Ev(EstadoBarrido.Ruptura))))}", "Real Premium Ruptura");
		c.Check("UPGRADE PREMIUM: categoría PREMIUM con su etiqueta (no repite la de REAL)",
			Al(N("UPGRADE PREMIUM", Ev(EstadoBarrido.Premium))) is { } up ? $"{up.Categoria} {up.Asunto.Contains("UPGRADE PREMIUM", StringComparison.Ordinal)} {up.Clave != Al(N("CONFIRMADO", Ev(EstadoBarrido.Real)))!.Clave}" : "-",
			"Premium True True");
		c.Check("fuera de las cinco: INDETERMINADO, FALLIDO, RUPTURA FALLIDA, rompimiento con iceberg, arma B, modelos cerrados",
			string.Join(" ", new[] { ("CONFIRMADO", EstadoBarrido.Indeterminado), ("FALLIDO", EstadoBarrido.Fallido), ("RUPTURA FALLIDA", EstadoBarrido.RupturaFallida),
				("MOD3 ACTIVADO", EstadoBarrido.Ruptura), ("RUPTURA (arma MOD4)", EstadoBarrido.Ruptura), ("MODELO CERRADO", EstadoBarrido.Real) }.Select(t => Cat(Al(N(t.Item1, Ev(t.Item2)))))),
			"- - - - - -");
		var soloPremium = new ParametrosV2 { NewsTimes = "", AlertMinState = EstadoMinimoAlerta.Premium };
		c.Check("con mínimo PREMIUM, un REAL no suena y un PREMIUM sí",
			$"{Al(N("CONFIRMADO", Ev(EstadoBarrido.Real)), soloPremium) is null} {Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium)), soloPremium) is null}", "True False");
		c.Check("con NOTICIA nunca", Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium, noticia: true))) is null ? "nada" : "suena", "nada");
		var pn = new ParametrosV2 { NewsTimes = "09:55", NewsBlockMin = 5 };
		c.Check("dentro del bloqueo de una hora de noticia tampoco", Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium)), pn) is null ? "nada" : "suena", "nada");
		c.Check("canales por categoría: ATAS en las cinco; correo y Telegram por defecto en PREMIUM y en los gatillos A y B",
			string.Join(" ", Enum.GetValues<CategoriaAlerta>().Select(k => p.Canales(k)).Select(k => $"{(k.Atas ? 1 : 0)}{(k.Correo ? 1 : 0)}{(k.Telegram ? 1 : 0)}")), "100 111 100 111 111");

		c.Seccion("v3 S14 Mensaje (correo y Telegram): instrumento, contrato, dirección, horas, clasificación, zona, gatillo, plan, trackers y MT5");
		var gatillo = new AnalisisGatillo { Resultado = ResultadoGatillo.Valido, Tipo = TipoGatillo.Arranque, Score = 81, Direccion = 1 };
		var conIceberg = Ev(EstadoBarrido.Real);
		(conIceberg.IcebergEstado, conIceberg.IcebergModo, conIceberg.StopsEvento) = ("AGUANTANDO", "MBO", "STOP RUN 220c");
		var setup = new SetupModelo
		{
			Id = 7, Modelo = "MOD1", Etiqueta = "A - BARRIDO", Signo = 1, Barrido = conIceberg, Zona = new ZonaOperable { Tipo = "BARRIDO", Bajo = 21378, Alto = 21380, Signo = 1 },
			Estado = EstadoModelo.Entrada, Gatillo = gatillo, Entrada = 21385.25m, Stop = 21377.25m, Tp1 = 21401.25m, Riesgo = new PlanRiesgo(200, 5, 32, 5, false)
		};
		var aviso = Al(N("GATILLO VÁLIDO", conIceberg, setup));
		c.Check("gatillo VÁLIDO de un setup A: categoría EntradaA", Cat(aviso), "EntradaA");
		c.Check("asunto con instrumento, contrato, título y dirección", aviso?.Asunto ?? "", "Sessions Sweeps Pro · NQ NQZ6 · GATILLO VÁLIDO DEL MODELO A ALCISTA");
		var cuerpo = aviso?.Cuerpo ?? "";
		c.Check("cuerpo completo (15)",
			new[] { "Instrumento: NQ · contrato NQZ6 · 07:55 CDMX (09:55 ET)", "BARRIDO REAL 72/100 · certificada por iceberg (MBO)", "Modelo A: A - BARRIDO · zona BARRIDO",
				"ENTRADA 21,385.25 · SL 21,377.25 · TP 21,401.25 · R:R 2.0", "5 NQ", "iceberg AGUANTANDO MBO · stops STOP RUN 220c", "MT5: APAGADO", PoliticaAlertasV2.AvisoLegal }
				.Where(t => !cuerpo.Contains(t, StringComparison.Ordinal)).DefaultIfEmpty("completo").First(), "completo");
		setup.Modelo = "MOD2";
		c.Check("el de un setup B: EntradaB", Cat(Al(N("GATILLO VÁLIDO", conIceberg, setup))), "EntradaB");

		c.Seccion("S10 T28: gatillo manual con compuesta = una de tres respuestas + CSV MANUAL, sin alerta");
		var m = new SimMotor();
		m.Ruptura();
		m.M.TomarNovedadesV2();
		var q = new ManualTriggerRequest
		{
			Id = 31, DesdeUtc = Utc(new DateTime(2026, 10, 30, 9, 42, 0)), HastaUtc = Utc(new DateTime(2026, 10, 30, 9, 45, 0)), Alto = 21440, Bajo = 21416,
			Tipo = TipoAnalisisManual.VelaCompuesta, Direccion = DireccionManual.Auto
		};
		var res = m.M.AnalizarManual(q);
		c.Check("T28 respuesta: GATILLO POR VELA DE ARRANQUE / CONTINUIDAD o NO EJECUTABLE",
			res.Resultado is "GATILLO POR VELA DE ARRANQUE" or "GATILLO POR VELA DE CONTINUIDAD" or "NO EJECUTABLE" ? "una de tres" : res.Resultado, "una de tres");
		var fila = CsvV2.Separar(CsvV2.FilaManual(q, res, Utc(new DateTime(2026, 10, 30, 9, 46, 0))));
		var cab = CsvV2.CabeceraModelos.Split(',');
		c.Check("T28 fila del CSV de modelos con Origen MANUAL", $"{fila.Count == cab.Length} {fila[Array.IndexOf(cab, "Model")]} {fila[Array.IndexOf(cab, "Origen")]} {fila[Array.IndexOf(cab, "TradeId")]}",
			"True MANUAL MANUAL 31");
		c.Check("T28 sin alerta: el análisis manual no crea novedades", m.M.TomarNovedadesV2().Count.ToString(), "0");
	}
}
