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
		c.Seccion("S8 Panel Resumen: secciones 1-8, hora CDMX, dirección y aviso legal");
		var s = new SimMotor();
		s.Fuentes.Estados[FuenteSuite.Thermal] = EstadoFuente.NoCargado;
		s.Ruptura();
		var panel = s.M.Panel(new OpcionesPanel { ArchivoCsv = "x.csv" });
		var texto = Texto(panel);
		var secciones = panel.Lineas.Where(l => l.Titulo).Select(l => l.Texto[..2]).ToList();
		c.Check("ocho secciones en orden", string.Join("", secciones), "1.2.3.4.5.6.7.8.");
		c.Check("barrido externo con hora CDMX (T0 al cruzar 21420 a las 09:42 ET del 30-oct = 07:42 CDMX)", Linea(panel, "2. BARRIDO")?.Texto ?? "", "2. BARRIDO DE LIQUIDEZ EXTERNA (07:42 CDMX)");
		var dir = Linea(panel, "MAX Dia anterior")?.Segmentos.Last();
		c.Check("RUPTURA alcista: nueva dirección ALCISTA en verde negrita", dir is null ? "sin línea" : $"{dir.Texto} {dir.Color} {dir.Negrita}", "ALCISTA Verde True");
		c.Check("sin Thermal: SIN FUENTE LVN en amarillo", Linea(panel, "SIN FUENTE LVN")?.Segmentos[0].Color.ToString() ?? "no", "Amarillo");
		c.Check("aviso legal al pie", panel.Pie, "Estadística simulada; no es un registro de operaciones reales.");
		c.Check("recuperación 1m y 5m con el umbral del anexo (16 ticks)", texto.Contains("Recuperación 1m (>= 16 ticks)") && texto.Contains("Recuperación 5m (>= 16 ticks)") ? "sí" : "no", "sí");
		c.Check("CSV y modo de datos en trazabilidad", Linea(panel, "Datos:")?.Texto ?? "", "Datos: TICKS · INFERIDO · CSV x.csv");

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

		c.Seccion("S8 Panel: próximo LVN objetivo (anexo 4)");
		var conLvn = new SimMotor();
		conLvn.M.LvnDesdeSuite(PruebasV2Lvn.Z(1, 1, 21429.5m, 21430m, manual: false));
		conLvn.Ruptura();
		var sin = Linea(conLvn.M.Panel(new OpcionesPanel()), "Próximo LVN objetivo");
		c.Check("ruptura alcista sin LVN por encima: '(long): Sin objetivo LVN' en amarillo",
			sin is null ? "sin línea" : $"{sin.Texto} | {sin.Segmentos[1].Color} {sin.Segmentos[2].Color}", "Próximo LVN objetivo (long): Sin objetivo LVN | Naranja Amarillo");
		conLvn.M.LvnDesdeSuite(PruebasV2Lvn.Z(2, 1, 21459.75m, 21460.25m, manual: true));
		var con = Linea(conLvn.M.Panel(new OpcionesPanel()), "Próximo LVN objetivo");
		c.Check("con LVN por encima: '(alcista)' naranja y el precio con miles en verde negrita",
			con is null ? "sin línea" : $"{string.Concat(con.Segmentos.Take(4).Select(x => x.Texto))} | {con.Segmentos[1].Color} {con.Segmentos[3].Color} {con.Segmentos[3].Negrita}",
			"Próximo LVN objetivo (alcista): 21,460.00 | Naranja Verde True");
		c.Check("origen MANUAL y hora CDMX", con?.Segmentos[4].Texto.StartsWith(" (MANUAL) ", StringComparison.Ordinal) == true && con.Segmentos[4].Texto.EndsWith(" CDMX", StringComparison.Ordinal) ? "sí" : con?.Segmentos[4].Texto ?? "", "sí");

		c.Seccion("S8 Panel Detalle: bloques, puertas, desglose y fuentes");
		var detalle = Texto(s.M.Panel(new OpcionesPanel { Detalle = true }));
		c.Check("Detalle añade bloques A-E, puertas y fuentes usadas",
			detalle.Contains("DETALLE · MAX Dia anterior") && detalle.Contains("Puertas premium:") && detalle.Contains("Fuentes usadas:") ? "sí" : "no", "sí");
		c.Check("Resumen no los muestra", texto.Contains("Puertas premium:") ? "sí" : "no", "no");

		c.Seccion("S8 T30: ventana flotante fuera de pantalla → monitor principal; en el secundario se respeta");
		var escritorio2 = (-1920.0, 0.0, 3840.0, 1080.0);
		var principal = (0.0, 0.0, 1920.0, 1040.0);
		var izquierda = new PosicionVentana(-1800, 100, 520, 760, true, 11, 1, false, false);
		c.Check("en el monitor de la izquierda (coordenadas negativas) se queda donde estaba", Colocacion.Ajustar(izquierda, escritorio2, principal) == izquierda ? "igual" : "movida", "igual");
		var perdida = izquierda with { Left = -1800 };
		var ajustada = Colocacion.Ajustar(perdida, (0.0, 0.0, 1920.0, 1080.0), principal);
		c.Check("se quitó ese monitor: va al principal (+40, +40) con su tamaño", $"{ajustada.Left} {ajustada.Top} {ajustada.Width} {ajustada.Height} {ajustada.Topmost}", "40 40 520 760 True");
		var enorme = Colocacion.Ajustar(new PosicionVentana(5000, 5000, 3000, 2000, false, 11, 1, true, false), (0.0, 0.0, 1920.0, 1080.0), principal);
		c.Check("más grande que el monitor: se recorta al área de trabajo", $"{enorme.Width} {enorme.Height} {enorme.Detalle}", "1920 1040 True");

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
