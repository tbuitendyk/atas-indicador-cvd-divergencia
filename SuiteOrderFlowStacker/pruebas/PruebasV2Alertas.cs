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
		c.Check("cabecera: las 50 columnas de la v2 (EventId ... Transiciones, T0Utc), 20 de auditoría v3 y 8 de la v4 al final",
			$"{columnas.Length} {columnas[1]} {columnas[48]} {columnas[49]} {columnas[50]} {columnas[69]} {columnas[^1]}", "78 EventId Transiciones T0Utc TimestampET ModelEligible StructureConfirmed");
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
		c.Check("v4: no suenan INDETERMINADO, FALLIDO, RUPTURA FALLIDA, arma M2 ni modelos cerrados; M3 ACTIVADO sí (su señal propia)",
			string.Join(" ", new[] { ("CONFIRMADO", EstadoBarrido.Indeterminado), ("FALLIDO", EstadoBarrido.Fallido), ("RUPTURA FALLIDA", EstadoBarrido.RupturaFallida),
				("MOD3 ACTIVADO", EstadoBarrido.Ruptura), ("RUPTURA (arma MOD4)", EstadoBarrido.Ruptura), ("MODELO CERRADO", EstadoBarrido.Real) }.Select(t => Cat(Al(N(t.Item1, Ev(t.Item2)))))),
			"- - - M3Activado - -");
		var soloPremium = new ParametrosV2 { NewsTimes = "", AlertMinState = EstadoMinimoAlerta.Premium };
		c.Check("con mínimo PREMIUM, un REAL no suena y un PREMIUM sí",
			$"{Al(N("CONFIRMADO", Ev(EstadoBarrido.Real)), soloPremium) is null} {Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium)), soloPremium) is null}", "True False");
		c.Check("con NOTICIA nunca", Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium, noticia: true))) is null ? "nada" : "suena", "nada");
		var pn = new ParametrosV2 { NewsTimes = "09:55", NewsBlockMin = 5 };
		c.Check("dentro del bloqueo de una hora de noticia tampoco", Al(N("CONFIRMADO", Ev(EstadoBarrido.Premium)), pn) is null ? "nada" : "suena", "nada");
		c.Check("canales por categoría: ATAS en todas; correo y Telegram por defecto en PREMIUM y en los gatillos A y B (no en la línea de sesión)",
			string.Join(" ", Enum.GetValues<CategoriaAlerta>().Select(k => p.Canales(k)).Select(k => $"{(k.Atas ? 1 : 0)}{(k.Correo ? 1 : 0)}{(k.Telegram ? 1 : 0)}")), "100 111 100 111 111 100 111 100");

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
		c.Check("gatillo VÁLIDO de un setup M1: categoría EntradaM1", Cat(aviso), "EntradaM1");
		c.Check("asunto con instrumento, contrato, título y dirección", aviso?.Asunto ?? "", "Indicador Global Session Sweep · NQ NQZ6 · GATILLO VÁLIDO DEL MODELO M1 ALCISTA");
		var cuerpo = aviso?.Cuerpo ?? "";
		c.Check("cuerpo completo (15)",
			new[] { "Instrumento: NQ · contrato NQZ6 · 07:55 CDMX (09:55 ET)", "BARRIDO REAL 72/100 · certificada por iceberg (MBO)", "Modelo M1: A - BARRIDO · zona BARRIDO",
				"ENTRADA 21,385.25 · SL 21,377.25 · TP 21,401.25 · R:R 2.0", "5 NQ", "iceberg AGUANTANDO MBO · stops STOP RUN 220c", "MT5: APAGADO", PoliticaAlertasV2.AvisoLegal }
				.Where(t => !cuerpo.Contains(t, StringComparison.Ordinal)).DefaultIfEmpty("completo").First(), "completo");
		setup.Modelo = "MOD2";
		c.Check("el de un setup M2: EntradaM2", Cat(Al(N("GATILLO VÁLIDO", conIceberg, setup))), "EntradaM2");
		setup.Modelo = "MOD3";
		c.Check("el de un setup M3: EntradaM3", Cat(Al(N("GATILLO VÁLIDO", conIceberg, setup))), "EntradaM3");
		setup.Modelo = "MOD2";

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

		c.Seccion("v3 S16 CSV y auditoría (19): instrumento, valores, calibración, fuente, Modelo A/B, MT5; archivos _v3");
		var calibrado = new CalibracionInstrumento { Raiz = RaizInstrumento.NQ, Estado = EstadoCalibracion.Calibrado, Sesiones = 20, FechaUtc = Utc(new DateTime(2026, 11, 2, 18, 0, 0)), Metodo = "mediana 1m RTH" };
		var ctxNq = new ContextoCsv(EstadoInstrumento.Crear("NQZ6", "", 0.25m, calibrado, 500), "NO DISPONIBLE", "TimeFrame M1", "REAL", "ATAS+TELEGRAM", "MT5: SIMULACION");
		string Col(string fila, string cabecera, string nombre) => CsvV2.Separar(fila)[Array.IndexOf(cabecera.Split(','), nombre)];
		var pdhFila = CsvV2.FilaBarrido(pdh, Utc(new DateTime(2026, 10, 30, 9, 45, 0)), "TICKS", ctxNq);
		string B3(string n) => Col(pdhFila, CsvV2.CabeceraBarridos, n);
		c.Check("barrido: hora ET y CDMX, raíz, contrato, tick, valor del tick y del punto",
			$"{B3("TimestampET")} | {B3("TimestampCDMX")} | {B3("InstrumentRoot")} {B3("ContractSymbol")} {B3("TickSize")} {B3("TickValue")} {B3("PointValue")}",
			"2026-10-30 09:45:00 | 2026-10-30 07:45:00 | NQ NQZ6 0.25 5 20");
		c.Check("barrido: calibración, sesiones, versión del perfil, fuente, canales y MT5 (sin credenciales)",
			$"{B3("CalibrationStatus")} {B3("CalibrationSessions")} {B3("ProfileVersion")} {B3("DataSource")} {B3("AlertChannels")} {B3("Mt5Status")} {B3("Timeframe")}",
			"CALIBRADO 20 3.0.0/20261102-2300/mediana 1m RTH REAL ATAS+TELEGRAM MT5: SIMULACION TimeFrame M1");
		c.Check("barrido: volumen y delta en contratos, DeltaPct y VolumenRelativo; RUPTURA no habilita el Modelo A",
			$"{B3("VolumeContracts") == F(pdh.VolBeyond)} {B3("DeltaContracts") == F(pdh.DeltaBeyond)} {B3("DeltaPct").Length > 0} {B3("VolumeRel").Length > 0} [{B3("ModelEligible")}]", "True True True True []");
		c.Check("con footprint de 1 m la fuente es APROX", Col(CsvV2.FilaBarrido(pdh, DateTime.UtcNow, "APROX", ctxNq), CsvV2.CabeceraBarridos, "DataSource"), "APROX");
		var sinValidar = new ContextoCsv(EstadoInstrumento.Crear("NQZ6", "", 0.5m, null, 500));
		var filaSv = CsvV2.FilaBarrido(pdh, DateTime.UtcNow, "TICKS", sinValidar);
		c.Check("tick de ATAS distinto al del contrato: VALOR DE CONTRATO NO VALIDADO y calibración provisional",
			$"{Col(filaSv, CsvV2.CabeceraBarridos, "TickValue")} | {Col(filaSv, CsvV2.CabeceraBarridos, "CalibrationStatus")}", "VALOR DE CONTRATO NO VALIDADO | CALIBRACIÓN INSUFICIENTE (PROVISIONAL)");
		var es = new ContextoCsv(EstadoInstrumento.Crear("ESZ6", "", 0.25m, null, 500));
		var filaEs = CsvV2.FilaBarrido(pdh, DateTime.UtcNow, "TICKS", es);
		c.Check("ES con sus propios valores (12.5 USD/tick, 50 USD/punto)", $"{Col(filaEs, CsvV2.CabeceraBarridos, "InstrumentRoot")} {Col(filaEs, CsvV2.CabeceraBarridos, "TickValue")} {Col(filaEs, CsvV2.CabeceraBarridos, "PointValue")}", "ES 12.5 50");

		var setupB = new SetupModelo
		{
			Id = 12, Modelo = "MOD2", Etiqueta = "B - FVG+LVN", Signo = 1, Entrada = 21402, Stop = 21398, Tp1 = 21410,
			Zona = new ZonaOperable { Tipo = "FVG+LVN", Bajo = 21400, Alto = 21402, Signo = 1, LvnId = 5, FvgId = 9, EstadoGatillo = "VÁLIDO" }
		};
		var filaB = CsvV2.FilaModelo(setupB, Utc(new DateTime(2026, 10, 30, 9, 50, 0)), ctxNq, "TpOEma15");
		string M3(string n) => Col(filaB, CsvV2.CabeceraModelos, n);
		c.Check("modelos: tantas celdas como la cabecera (también la fila manual)", $"{CsvV2.Separar(filaB).Count == CsvV2.CabeceraModelos.Split(',').Length} {fila.Count == cab.Length}", "True True");
		c.Check("modelos: Modelo M2, centro, estado del gatillo, LVN/FVG, R:R y salida",
			$"{M3("ModelAB")} {M3("ZoneCenter")} {M3("ZoneState")} {M3("LvnId")} {M3("FvgId")} [{M3("EdgeId")}] {M3("RewardRisk")} {M3("ExitMode")} {M3("InstrumentRoot")}",
			"M2 21401 VÁLIDO 5 9 [] 2 TpOEma15 NQ");
		c.Check("MOD1 y BFVG son Modelo 1", $"{new SetupModelo { Modelo = "MOD1", Zona = setupB.Zona }.ModeloOperativo}{new SetupModelo { Modelo = "BFVG", Zona = setupB.Zona }.ModeloOperativo}", "M1M1");

		var carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssp-csv-" + Guid.NewGuid().ToString("N"));
		using (var g = new GrabadorCsvV2(carpeta, "NQZ6") { Contexto = () => ctxNq })
		{
			g.Barrido(pdh, DateTime.UtcNow, "TICKS");
			g.Vaciar();
			var dia = Zonas.DiaOperativo(pdh.T0Utc);
			c.Check("se graba en yyyy-MM-dd_barridos_v3.csv y se restaura desde él",
				$"{System.IO.File.Exists(g.Archivo(dia, "barridos_v3"))} {System.IO.File.Exists(g.Archivo(dia, "barridos"))} {g.LeerBarridos(dia).Any(x => x.EventId == pdh.Id)}", "True False True");
		}

		System.IO.Directory.Delete(carpeta, true);
	}
}
