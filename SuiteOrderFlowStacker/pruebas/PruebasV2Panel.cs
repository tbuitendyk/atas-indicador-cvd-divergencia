using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>S8: contenido del panel (11.2/11.3 y anexo), colocación de la ventana (T30), CVD y almacén de trades.</summary>
internal static class PruebasV2Panel
{
	private static string Texto(ContenidoPanel c) => string.Join("\n", c.Lineas.Select(l => l.Texto));

	private static LineaPanel? Linea(ContenidoPanel c, string empieza) => c.Lineas.FirstOrDefault(l => l.Texto.StartsWith(empieza, StringComparison.Ordinal));

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v3 S7 Panel: cabecera de 3 líneas, 17 recuadros, hora CDMX, dirección y aviso legal");
		var s = new SimMotor();
		s.Fuentes.Estados[FuenteSuite.Thermal] = EstadoFuente.NoCargado;
		s.Ruptura();
		var panel = s.M.Panel(new OpcionesPanel { ArchivoCsv = "x.csv" });
		var texto = Texto(panel);
		var secciones = panel.Lineas.Where(l => l.Titulo).Select(l => l.Texto.Split('.')[0]).ToList();
		c.Check("17 recuadros en orden", string.Join(",", secciones), string.Join(",", Enumerable.Range(1, 17)));
		c.Check("cada recuadro con su clave desplegable b1..b17", string.Join(",", panel.Lineas.Where(l => l.Titulo).Select(l => l.Grupo)),
			string.Join(",", Enumerable.Range(1, 17).Select(i => "b" + i)));
		var sinGrupo = panel.Lineas.TakeWhile(l => !l.Titulo).Where(l => !l.Separador).ToList();
		c.Check("cabecera: 3 líneas siempre visibles (estado, gatillo, instrumento)", $"{sinGrupo.Count} {sinGrupo.All(l => l.Grupo is null)} {sinGrupo[0].Texto.StartsWith("ESTADO: ", StringComparison.Ordinal)} " +
			$"{sinGrupo[1].Texto.StartsWith("NO SE HA CUMPLIDO", StringComparison.Ordinal) || sinGrupo[1].Texto.StartsWith("GATILLO", StringComparison.Ordinal)} {sinGrupo[2].Texto.StartsWith("NQ · ", StringComparison.Ordinal)}",
			"3 True True True True");
		var contenido = panel.Lineas.SkipWhile(l => !l.Titulo).Where(l => !l.Titulo && !l.Separador).ToList();
		c.Check("todo el contenido de los recuadros pertenece a uno (se puede plegar)", contenido.All(l => l.Grupo is not null) ? "sí" : "no", "sí");
		c.Check("barrido externo con hora CDMX (T0 al cruzar 21420 a las 09:42 ET del 30-oct = 07:42 CDMX)",
			Linea(panel, "MAX Dia anterior")?.Texto.Contains("T0 07:42 CDMX", StringComparison.Ordinal) == true ? "sí" : Linea(panel, "MAX Dia anterior")?.Texto ?? "", "sí");
		var dir = Linea(panel, "MAX Dia anterior")?.Segmentos.Last();
		c.Check("RUPTURA alcista: nueva dirección ALCISTA en verde negrita", dir is null ? "sin línea" : $"{dir.Texto} {dir.Color} {dir.Negrita}", "ALCISTA Verde True");
		c.Check("sin Thermal: SIN FUENTE LVN en amarillo", Linea(panel, "SIN FUENTE LVN")?.Segmentos[0].Color.ToString() ?? "no", "Amarillo");
		c.Check("aviso legal v3 al pie", panel.Pie, PoliticaAlertasV2.AvisoLegal);
		c.Check("recuperación 1m y 5m con el umbral (16 ticks = 4 puntos de NQ)",
			texto.Contains("Recuperación 1m (>= 16 ticks / 4 pts)") && texto.Contains("Recuperación 5m (>= 16 ticks / 4 pts)") ? "sí" : "no", "sí");
		c.Check("CSV y modo de datos en trazabilidad, con las unidades del instrumento",
			$"{texto.Contains("CSV x.csv")} {Linea(panel, "Datos:")?.Texto}", "True Datos: TICKS · INFERIDO · volumen y delta en contratos de NQ");
		var grafico = panel.Lineas.FirstOrDefault(l => l.Grafico is not null);
		var velasCvd = s.M.Leer(m => m.Cvd.Velas.Count);
		c.Check("recuadro 9: minigráfico de velas de CVD (las últimas 40) dentro del recuadro CVD",
			grafico is null ? "sin gráfico" : $"{grafico.Grupo} {grafico.Grafico!.Velas.Count == Math.Min(40, velasCvd)} {grafico.Texto.StartsWith("[CVD:", StringComparison.Ordinal)}", "b9 True True");
		c.Check("recuadro 9: CVD y delta con unidades (contratos)", Linea(panel, "CVD ")?.Texto.Contains(" contratos · delta última vela ", StringComparison.Ordinal) == true ? "sí" : "no", "sí");
		c.Check("recuadro 1: valor del contrato validado (NQ 5 USD/tick, 20 USD/punto)", Linea(panel, "NQ · contrato")?.Texto.Contains("5 USD/tick · 20 USD/punto", StringComparison.Ordinal) == true ? "sí" : Linea(panel, "NQ · contrato")?.Texto ?? "sin línea", "sí");
		c.Check("recuadro 16 sin MT5 configurado: APAGADO (también en la cabecera)", $"{Linea(panel, "MT5: APAGADO")?.Grupo} {sinGrupo[2].Texto.EndsWith("MT5: APAGADO", StringComparison.Ordinal)}", "b16 True");
		c.Check("recuadro 15: canales sin configurar en naranja", Linea(panel, "Correo SIN CONFIGURAR")?.Segmentos[0].Color.ToString() ?? "sin línea", "Naranja");
		var conEnvios = s.M.Panel(new OpcionesPanel
		{
			CorreoConfigurado = true,
			Envios = new[] { new ResultadoEnvio("telegram", Utc(new DateTime(2026, 10, 30, 9, 45, 0)), false, "timeout") },
			Mt5 = new[] { "MT5: SIMULACION · USTEC VALIDADO · sin posición" }
		});
		c.Check("recuadro 15: último envío por canal con hora CDMX y resultado", Linea(conEnvios, "Último envío telegram")?.Texto ?? "sin línea", "Último envío telegram: 07:45 CDMX · ERROR · timeout");
		c.Check("recuadro 16 y cabecera: el estado de MT5 que da la carcasa", $"{Linea(conEnvios, "MT5: SIMULACION")?.Grupo} {conEnvios.Lineas[2].Texto.EndsWith("MT5: SIMULACION · USTEC VALIDADO · sin posición", StringComparison.Ordinal)}", "b16 True");

		c.Seccion("S8 Panel: validación por iceberg (T3: 'Probable' si es inferido; 'Certificada' solo con MBO y AGUANTANDO)");
		var e = s.Pdh!;
		string Validacion()
		{
			var l = Linea(s.M.Panel(new OpcionesPanel()), "Validación:");
			return l is null ? "sin línea" : $"{l.Segmentos[1].Texto} | {l.Segmentos[1].Color}";
		}

		s.M.Leer(m => (e.IcebergEstado, e.IcebergModo) = ("AGUANTANDO", "INFERIDO"));
		c.Check("T3 iceberg inferido = Probable (naranja)", Validacion(), "Probable: iceberg inferido | Naranja");
		s.M.Leer(m => (e.IcebergEstado, e.IcebergModo) = ("AGUANTANDO", "MBO"));
		c.Check("MBO y AGUANTANDO = Certificada (verde)", Validacion(), "Certificada como real por iceberg (MBO) | Verde");
		s.M.Leer(m => (e.IcebergEstado, e.IcebergModo) = ("ROTO", "MBO"));
		c.Check("MBO pero ROTO: nunca 'Certificada'", Validacion().StartsWith("Certificada", StringComparison.Ordinal) ? "Certificada" : "no", "no");
		s.M.Leer(m => (e.IcebergEstado, e.IcebergModo) = ("", ""));
		c.Check("sin iceberg", Validacion(), "Sin iceberg | Gris");

		c.Seccion("v3 S7 Panel: próximo objetivo LVN / EDGE (recuadro 12)");
		var conLvn = new SimMotor();
		conLvn.M.LvnDesdeSuite(PruebasV2Lvn.Z(1, 1, 21429.5m, 21430m, manual: false));
		conLvn.Ruptura();
		var sin = Linea(conLvn.M.Panel(new OpcionesPanel()), "Próximo objetivo");
		c.Check("ruptura alcista sin LVN ni EDGE por encima: '(long): Sin objetivo LVN / EDGE' en amarillo",
			sin is null ? "sin línea" : $"{sin.Texto} | {sin.Segmentos[1].Color} {sin.Segmentos[2].Color}", "Próximo objetivo (long): Sin objetivo LVN / EDGE | Naranja Amarillo");
		conLvn.M.LvnDesdeSuite(PruebasV2Lvn.Z(2, 1, 21459.75m, 21460.25m, manual: true));
		var con = Linea(conLvn.M.Panel(new OpcionesPanel()), "Próximo LVN objetivo");
		c.Check("con LVN por encima: '(alcista)' naranja y el precio con miles en verde negrita",
			con is null ? "sin línea" : $"{string.Concat(con.Segmentos.Take(4).Select(x => x.Texto))} | {con.Segmentos[1].Color} {con.Segmentos[3].Color} {con.Segmentos[3].Negrita}",
			"Próximo LVN objetivo (alcista): 21,460.00 | Naranja Verde True");
		c.Check("origen MANUAL, hora CDMX y distancia en ticks y puntos",
			con?.Segmentos[4].Texto is { } d4 && d4.StartsWith(" (MANUAL) ", StringComparison.Ordinal) && d4.Contains(" CDMX", StringComparison.Ordinal) && d4.EndsWith("a 80 ticks / 20 pts", StringComparison.Ordinal) ? "sí" : con?.Segmentos[4].Texto ?? "", "sí");

		c.Seccion("S8 Panel Detalle: bloques, puertas, desglose y fuentes");
		var detalle = Texto(s.M.Panel(new OpcionesPanel { Detalle = true }));
		c.Check("Detalle añade bloques A-E, puertas y fuentes usadas",
			detalle.Contains("DETALLE · MAX Dia anterior") && detalle.Contains("Puertas premium:") && detalle.Contains("Fuentes usadas:") ? "sí" : "no", "sí");
		c.Check("Resumen no los muestra", texto.Contains("Puertas premium:") ? "sí" : "no", "no");

		c.Seccion("Prueba 35 (T30): ventana flotante en varios monitores; recuadros plegados guardados");
		var escritorio2 = (-1920.0, 0.0, 3840.0, 1080.0);
		var principal = (0.0, 0.0, 1920.0, 1040.0);
		var izquierda = new PosicionVentana(-1800, 100, 520, 760, true, 11, 1, false, false);
		c.Check("en el monitor de la izquierda (coordenadas negativas) se queda donde estaba", Colocacion.Ajustar(izquierda, escritorio2, principal) == izquierda ? "igual" : "movida", "igual");
		var perdida = izquierda with { Left = -1800 };
		var ajustada = Colocacion.Ajustar(perdida, (0.0, 0.0, 1920.0, 1080.0), principal);
		c.Check("se quitó ese monitor: va al principal (+40, +40) con su tamaño", $"{ajustada.Left} {ajustada.Top} {ajustada.Width} {ajustada.Height} {ajustada.Topmost}", "40 40 520 760 True");
		var enorme = Colocacion.Ajustar(new PosicionVentana(5000, 5000, 3000, 2000, false, 11, 1, true, false), (0.0, 0.0, 1920.0, 1080.0), principal);
		c.Check("más grande que el monitor: se recorta al área de trabajo", $"{enorme.Width} {enorme.Height} {enorme.Detalle}", "1920 1040 True");
		var guardada = System.Text.Json.JsonSerializer.Serialize(izquierda with { Plegadas = new[] { "b5", "b16" } });
		var leida = System.Text.Json.JsonSerializer.Deserialize<PosicionVentana>(guardada)!;
		c.Check("los recuadros plegados se guardan y se leen con la posición", $"{string.Join(",", leida.Plegadas ?? Array.Empty<string>())} {leida.Left}", "b5,b16 -1800");
		var vieja = System.Text.Json.JsonSerializer.Deserialize<PosicionVentana>("{\"Left\":10,\"Top\":10,\"Width\":520,\"Height\":760,\"Topmost\":false,\"Letra\":11,\"Opacidad\":1,\"Detalle\":false,\"Minimizada\":false}")!;
		c.Check("un archivo de posición de la v2 (sin plegados) sigue abriendo", vieja.Plegadas is null ? "sin plegados" : "otro", "sin plegados");

		c.Seccion("S8 CVD: velas del delta acumulado y divergencias macro y micro (anexo)");
		var et0 = new DateTime(2026, 10, 29, 17, 58, 0);
		var velas = CalculoCvd.Velas(new[]
		{
			(Utc(et0), 10m, 9m, 50m, 60m, -5m),
			(Utc(et0.AddMinutes(1)), 10m, 9m, -20m, 5m, -30m),
			(Utc(et0.AddMinutes(2)), 10m, 9m, 15m, 20m, 0m)
		}, new TimeSpan(18, 0, 0));
		c.Check("apertura = cierre anterior; máximo y mínimo con el delta máximo/mínimo", $"{F(velas[1].Open)} {F(velas[1].High)} {F(velas[1].Low)} {F(velas[1].Close)}", "50 55 20 30");
		c.Check("a las 18:00 NY vuelve a cero", $"{F(velas[2].Open)} {F(velas[2].Close)}", "0 15");

		var altos = new decimal[] { 100, 101, 102, 110, 103, 104, 112, 105, 113, 115, 106, 105, 104, 103, 102 };
		var cvdAltos = new Dictionary<int, decimal> { [3] = 50, [6] = 45, [9] = 40 };
		var serie = altos.Select((h, i) => new VelaCvd(Utc(new DateTime(2026, 10, 30, 10, 0, 0).AddMinutes(i)), 0, cvdAltos.GetValueOrDefault(i), 0, 0, h, 90)).ToList();
		var divs = CalculoCvd.Divergencias(serie, 2, 1, true, 100);
		c.Check("máximo a máximo: precio más alto y CVD más bajo = bajista; macro 3→9 y micro 3→6, 6→9 dentro de ella",
			string.Join(" ", divs.Select(d => $"{(d.Micro ? "micro" : "macro")}{(d.Alcista ? "+" : "-")}{d.I1}-{d.I2}")), "macro-3-9 micro-3-6 micro-6-9");
		c.Check("sin micro si se apaga", CalculoCvd.Divergencias(serie, 2, 1, false, 100).Count.ToString(), "1");
		var bajos = altos.Select(h => 200 - h).ToArray();
		var espejo = bajos.Select((l, i) => new VelaCvd(serie[i].InicioUtc, 0, 0, -cvdAltos.GetValueOrDefault(i), 0, 300, l)).ToList();
		c.Check("mínimo a mínimo (espejo): alcista", string.Join(" ", CalculoCvd.Divergencias(espejo, 2, 1, false, 100).Select(d => $"{(d.Alcista ? "+" : "-")}{d.I1}-{d.I2}")), "+3-9");
		var micros = CalculoCvd.Divergencias(serie.Take(8).ToList(), 2, 1, true, 100);
		c.Check("una micro sin macro que la contenga no se muestra", micros.Count.ToString(), "0");

		var seguidor = new SeguidorCvd();
		var vistas = new List<string>();

		for (var i = 0; i < altos.Length; i++)
		{
			seguidor.Cerrar(serie[i].InicioUtc, altos[i], 90, 0, cvdAltos.GetValueOrDefault(i), 0);
			vistas.AddRange(seguidor.Nuevas(2, 1, true, 100).Select(d => $"{i}:{(d.Micro ? "micro" : "macro")}{d.I1}-{d.I2}"));
		}

		c.Check("seguidor incremental: cada una una sola vez; las micro salen con la macro que las contiene", string.Join(" ", vistas), "11:macro3-9 11:micro3-6 11:micro6-9");
		c.Check("seguidor: las tres quedan en Todas", seguidor.Todas.Count.ToString(), "3");

		c.Seccion("S8 Almacén: trades guardados durante un recálculo entran antes del primer trade en vivo");
		var r = new SimMotor(sinBarridos: true);
		var inicio = Utc(new DateTime(2026, 10, 30, 9, 41, 0));
		var volAntes = r.M.Leer(m => m.BarrasV.Barras.Sum(b => b.Volumen) + (m.BarrasV.Actual?.Volumen ?? 0));

		for (var k = 0; k < 10; k++)
			r.M.Almacen!.Agregar(new TickV2(inicio.AddSeconds(k), 21401, 3, true));

		r.M.Trade(inicio.AddSeconds(30), 21401.25m, 5, false, 21401, inicio);
		var volDespues = r.M.Leer(m => m.BarrasV.Barras.Sum(b => b.Volumen) + (m.BarrasV.Actual?.Volumen ?? 0));
		c.Check("los 30 contratos guardados + los 5 en vivo llegan a las barras", F(volDespues - volAntes), "35");
		c.Check("y quedan una sola vez en el almacén (10 + 1)", r.M.Almacen.PrimeroDesde(inicio) is var p0 ? (r.M.Almacen.Count - p0).ToString() : "", "11");
		var recortados = r.M.Almacen.RecortarAntesDe(inicio.AddSeconds(5));
		c.Check("recorte de trades viejos", $"{recortados >= 5} {r.M.Almacen[0].Utc == inicio.AddSeconds(5)}", "True True");
	}
}
