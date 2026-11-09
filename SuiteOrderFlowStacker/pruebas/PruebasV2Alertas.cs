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
		c.Check("cero alertas al recargar: las novedades del historial se descartan",
			despacho.Procesar(novedadesB, enVivo: false, p0, "NQ", b.M.Precio, false).Count.ToString(), "0");
		var vivas = despacho.Procesar(novedadesA, enVivo: true, p0, "NQ", a.M.Precio, false);
		c.Check("en vivo, la misma ruptura sí avisa (RUPTURA arma MOD4)", vivas.Any(x => x.Texto.Contains("MOD4-C RUPTURA-RETROCESO armado", StringComparison.Ordinal)) ? "sí" : string.Join(" / ", vivas.Select(x => x.Texto)), "sí");
		c.Check("una vez por evento: repetir las mismas novedades no vuelve a sonar", despacho.Procesar(novedadesA, enVivo: true, p0, "NQ", a.M.Precio, false).Count.ToString(), "0");

		c.Seccion("S10 Alertas (13): estado mínimo, NOTICIA, una vez por evento");
		var p = new ParametrosV2 { NewsTimes = "" };
		var nivel = new NivelLiquidez { Id = 1, Precio = 21380, Lado = Lado.Sellside, Origen = "Dia anterior", Grupo = "dia" };
		EvaluacionBarrido Ev(EstadoBarrido estado, bool noticia = false) => new() { Id = 99, Nivel = nivel, Signo = -1, L = 21380, Estado = estado, Total = 72, Noticia = noticia, T0Utc = Utc(new DateTime(2026, 10, 30, 9, 50, 0)) };
		NovedadV2 N(string tipo, EvaluacionBarrido e, SetupModelo? s = null) => new() { Tipo = tipo, Evaluacion = e, Setup = s, Utc = Utc(new DateTime(2026, 10, 30, 9, 55, 0)), Texto = tipo };
		AlertaV2? Al(NovedadV2 n) => PoliticaAlertasV2.Evaluar(n, p, "NQ", x => x.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture), false);

		c.Check("TRAMPA B con mínimo TRAMPA A: no suena", Al(N("CONFIRMADO", Ev(EstadoBarrido.TrampaB))) is null ? "no" : "sí", "no");
		c.Check("TRAMPA A: suena (clasificación)", Al(N("CONFIRMADO", Ev(EstadoBarrido.TrampaA)))?.Categoria ?? "no", "Clasificacion");
		c.Check("RUPTURA confirmada sola no suena (suena 'RUPTURA (arma MOD4)')", $"{Al(N("CONFIRMADO", Ev(EstadoBarrido.Ruptura))) is null} {Al(N("RUPTURA (arma MOD4)", Ev(EstadoBarrido.Ruptura)))?.Categoria}", "True Modelos");
		c.Check("UPGRADE PREMIUM, MOD3, FALLIDO y RUPTURA FALLIDA suenan",
			string.Join(" ", new[] { "UPGRADE PREMIUM", "MOD3 ACTIVADO", "FALLIDO", "RUPTURA FALLIDA" }.Select(t => Al(N(t, Ev(EstadoBarrido.TrampaA)))?.Categoria ?? "-")),
			"Clasificacion Modelos Fallidos Fallidos");
		c.Check("con NOTICIA nunca", Al(N("CONFIRMADO", Ev(EstadoBarrido.TrampaPremium, noticia: true))) is null ? "nada" : "suena", "nada");
		var pn = new ParametrosV2 { NewsTimes = "09:55", NewsBlockMin = 5 };
		c.Check("dentro del bloqueo de una hora de noticia tampoco", PoliticaAlertasV2.Evaluar(N("CONFIRMADO", Ev(EstadoBarrido.TrampaPremium)), pn, "NQ", x => "", false) is null ? "nada" : "suena", "nada");
		c.Check("la clave es la misma para el mismo evento (una vez por evento)", Al(N("FALLIDO", Ev(EstadoBarrido.Fallido)))?.Clave == Al(N("FALLIDO", Ev(EstadoBarrido.Fallido)))?.Clave ? "sí" : "no", "sí");
		c.Check("modelos invalidados o cerrados: solo panel y CSV", $"{Al(N("MODELO CERRADO", Ev(EstadoBarrido.TrampaA))) is null} {Al(N("MODELO INVALIDADO", Ev(EstadoBarrido.TrampaA))) is null}", "True True");

		c.Seccion("S10 Correo: cada toma de liquidez EXTERNA válida con entrada, SL y TP");
		var gatillo = new AnalisisGatillo { Resultado = ResultadoGatillo.Valido, Tipo = TipoGatillo.Arranque, Score = 81, Direccion = 1 };
		gatillo.Razones.Add("F2 absorción en la zona");
		var setup = new SetupModelo
		{
			Id = 7, Modelo = "MOD1", Etiqueta = "MOD1", Signo = 1, Barrido = Ev(EstadoBarrido.TrampaA), Zona = new ZonaOperable { Tipo = "FVG+LVN", Bajo = 21381, Alto = 21384, Signo = 1 },
			Estado = EstadoModelo.Entrada, Gatillo = gatillo, Entrada = 21385.25m, Stop = 21379.75m, Tp1 = 21393.50m, Tp2 = 21420m, Riesgo = new PlanRiesgo(200, 7, 22, 5, false)
		};
		var correo = Al(N("GATILLO VÁLIDO", setup.Barrido!, setup));
		c.Check("gatillo VÁLIDO sobre el PDL: alerta de entrada con correo", correo is null ? "nada" : $"{correo.Categoria} {correo.Correo}", "Entrada True");
		c.Check("asunto", correo?.Asunto ?? "", "Sessions Sweeps Pro · NQ · MOD1 LARGO · MIN Dia anterior");
		var cuerpo = correo?.Cuerpo ?? "";
		c.Check("el cuerpo trae entrada, stop, TP1 con R:R, TP2, contratos y el aviso legal",
			new[] { "ENTRADA 21,385.25", "STOP    21,379.75", "TP1     21,393.50  (R:R 1.5)", "TP2     21,420.00", "7 NQ", "Estadística simulada; no es un registro de operaciones reales." }
				.All(cuerpo.Contains) ? "sí" : cuerpo, "sí");
		var interna = new NivelLiquidez { Id = 2, Precio = 21390, Lado = Lado.Sellside, Origen = "Londres", Grupo = "sesion:Londres" };
		setup.Barrido = new EvaluacionBarrido { Id = 98, Nivel = interna, Signo = -1, L = 21390, Estado = EstadoBarrido.TrampaA };
		var sinCorreo = Al(N("GATILLO VÁLIDO", setup.Barrido, setup));
		c.Check("sobre liquidez interna: alerta sin correo", sinCorreo is null ? "nada" : $"{sinCorreo.Categoria} {sinCorreo.Correo}", "Entrada False");

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
