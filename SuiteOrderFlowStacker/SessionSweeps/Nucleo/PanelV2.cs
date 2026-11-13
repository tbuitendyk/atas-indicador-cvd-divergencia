namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using OrderFlowSuite.Comun;

/// <summary>Lo que el panel necesita de la carcasa: archivo CSV, MBO, saldo, envíos y MT5.</summary>
public sealed class OpcionesPanel
{
	public bool Detalle;
	public string ArchivoCsv = "";
	public string Version = "Indicador Global Session Sweep v4.0";
	public bool Mbo;
	public decimal? SaldoAtas;

	/// <summary>Canales configurados (correo / Telegram) y el último resultado de cada uno (recuadro 15).</summary>
	public bool CorreoConfigurado, TelegramConfigurado;

	public IReadOnlyCollection<ResultadoEnvio> Envios = Array.Empty<ResultadoEnvio>();

	/// <summary>Líneas del recuadro 16 (MT5): estado, prueba de conversión, registro. La primera va también en la cabecera.</summary>
	public IReadOnlyList<string> Mt5 = Array.Empty<string>();
}

/// <summary>
/// Panel general SESSIONS SWEEPS PRO (v3, 13): cabecera siempre visible (estado, gatillo, instrumento y conexiones) y 17
/// recuadros desplegables (Grupo "b1".."b17"). Volumen y delta siempre con sus unidades y el instrumento al que pertenecen.
/// </summary>
public sealed partial class MotorSweeps
{
	public ContenidoPanel Panel(OpcionesPanel o)
	{
		lock (_sync)
			return ArmarPanel(o);
	}

	private static ColorPanel ColorSigno(int s) => s > 0 ? ColorPanel.Verde : s < 0 ? ColorPanel.Rojo : ColorPanel.Blanco;

	private static FlechaPanel Flecha(int s) => s > 0 ? FlechaPanel.Arriba : s < 0 ? FlechaPanel.Abajo : FlechaPanel.Ninguna;

	private DateTime Ahora => _ahoraV2 == default ? DateTime.UtcNow : _ahoraV2;

	private decimal PrecioActual => BarrasV.Actual?.Close ?? BarrasV.Barras.LastOrDefault()?.Close ?? 0;

	/// <summary>Distancia en ticks y en puntos del instrumento (v3, 13.11).</summary>
	private string Ticks(decimal distancia) =>
		$"{Math.Abs(distancia) / Cfg.TamanoTick:0} ticks / {Math.Abs(distancia).ToString("0.##", CultureInfo.InvariantCulture)} pts";

	private ContenidoPanel ArmarPanel(OpcionesPanel o)
	{
		var ins = Instrumento;
		var c = new ContenidoPanel { Titulo = $"INDICADOR GLOBAL SESSION SWEEP · {(ins.Soportado ? ins.Contrato : Simbolo)}", Pie = PoliticaAlertasV2.AvisoLegal };
		var L = c.Lineas;
		var hoy = Zonas.DiaOperativo(Ahora);
		var e = Evaluaciones.LastOrDefault(x => x.Nivel.Grupo is "dia" or "semana") ?? Evaluaciones.LastOrDefault();

		Cabecera(L, e, o);

		// 1) CONTEXTO Y ESTRUCTURA (con lo que mostraba la tabla de la esquina: H1 y cada sesión).
		Recuadro(L, "1. CONTEXTO Y ESTRUCTURA", "b1");
		var h1 = MotorContextoV2.DeH1(H1.Valor);
		L.Add(Linea("b1", new Segmento("Estructura 1H: "), new Segmento(h1.Texto(), ColorSigno(h1.Signo()), true),
			new Segmento(" · 15m "), Alineacion(ContextoV2.M15), new Segmento(" · 5m "), Alineacion(ContextoV2.M5)));
		var kz = TiempoV2.EnKillzone(Ahora, P2);
		var noticia = TiempoV2.EnNoticia(Ahora, P2);

		foreach (var def in Cfg.Sesiones.Where(d => Cfg.Horario(d).Activa))
		{
			var (abierta, _, estadoSesion) = Sesiones.Tabla(def, Ahora);
			var ultima = Sesiones.Instancias.LastOrDefault(s => s.Id == def.Id);
			var lineas = LineasSesion.Lineas.Where(l => l.SesionId == def.Id && !l.Reciente).TakeLast(2).ToList();
			var rotas = lineas.Count(l => !l.Viva);
			L.Add(Linea("b1", new Segmento($"{def.Id}: ", ColorPanel.Gris), new Segmento(estadoSesion, abierta ? ColorPanel.Verde : ColorPanel.Rojo),
				new Segmento(ultima is null ? "" : $" · máx {PrecioMiles(ultima.Alto)} · mín {PrecioMiles(ultima.Bajo)}{(lineas.Count > 0 ? $" · líneas rotas {rotas}/{lineas.Count}" : "")}", ColorPanel.Gris)));
		}

		L.Add(Linea("b1", new Segmento($"{TiempoV2.HoraCdmx(Ahora)} · {TiempoV2.HoraEt(Ahora, "HH:mm")} · killzone "), new Segmento(kz ? "ACTIVA" : "inactiva", kz ? ColorPanel.Verde : ColorPanel.Gris, kz),
			new Segmento(noticia ? " · NOTICIA AHORA" : $" · noticias {P2.NewsTimes}", noticia ? ColorPanel.Amarillo : ColorPanel.Gris, noticia)));

		// 2) BARRIDO DE LIQUIDEZ EXTERNA Y RECUPERACIÓN.
		Recuadro(L, "2. BARRIDO DE LIQUIDEZ EXTERNA Y RECUPERACIÓN", "b2");

		if (e is null)
			L.Add(Linea("b2", new Segmento("Sin barridos hoy", ColorPanel.Gris)));
		else
			Barrido(L, e, "b2");

		// 3) RESUMEN DE ABSORCIÓN Y MODELOS.
		Recuadro(L, "3. RESUMEN DE ABSORCIÓN Y MODELOS", "b3");
		ResumenAbsorcion(L, e);
		Modelos(L);

		// 4) PRÓXIMO LVN OBJETIVO Y/O FVG.
		Recuadro(L, "4. PRÓXIMO LVN OBJETIVO Y/O FVG", "b4");
		ProximoObjetivoPanel(L, e, "b4");

		// 5) FOOTPRINT GATILLO.
		Recuadro(L, "5. FOOTPRINT GATILLO", "b5");
		var conGatillo = Setups.LastOrDefault(s => s.Gatillo is not null);

		if (conGatillo?.Gatillo is not { } gat)
		{
			L.Add(Linea("b5", new Segmento("Sin zona tocada", ColorPanel.Gris)));
		}
		else
		{
			var zona = $"{conGatillo.Zona.Tipo} {Precio(conGatillo.Zona.Bajo)}-{Precio(conGatillo.Zona.Alto)}";

			L.Add(gat.Resultado switch
			{
				ResultadoGatillo.Valido => Linea("b5", new Segmento($"VÁLIDO CUMPLIDO {(gat.Direccion > 0 ? "ALCISTA" : "BAJISTA")} · {(gat.Tipo == TipoGatillo.Arranque ? "vela de arranque" : "vela de continuidad")} · {conGatillo.Etiqueta} · {zona}",
					ColorPanel.Blanco, true, Flecha(gat.Direccion), ColorSigno(gat.Direccion))),
				ResultadoGatillo.Pendiente => Linea("b5", new Segmento($"PENDIENTE · {conGatillo.Etiqueta} · {zona} · falta: {string.Join(", ", gat.Faltan.Take(2))}", ColorPanel.Naranja)),
				_ => Linea("b5", new Segmento($"INVÁLIDO · {conGatillo.Etiqueta} · {zona} · {gat.Motivo}", ColorPanel.Rojo))
			});
		}

		// 6) PLAN DE RIESGO.
		Recuadro(L, "6. PLAN DE RIESGO", "b6");
		var riesgo = CalculadoraRiesgo.Riesgo(P2, o.SaldoAtas);
		L.Add(Linea("b6", new Segmento(P2.RiskMode == ModoRiesgo.Monto
			? $"Monto: {riesgo:0} USD por operación"
			: $"Porcentaje: {P2.RiskPct.ToString("0.##", CultureInfo.InvariantCulture)} % de {(P2.ReadAtasAccount && o.SaldoAtas is > 0 ? o.SaldoAtas.Value : P2.AccountSize):0} = {riesgo:0} USD")));

		if (Setups.LastOrDefault(s => s.Riesgo is not null) is { Riesgo: { } plan } con)
		{
			var rr = con.Tp1 is { } t1 && con.Entrada is { } en && con.Stop is { } st && en != st ? Math.Abs(t1 - en) / Math.Abs(en - st) : 0;
			var riesgoTicks = con.Entrada is { } e1 && con.Stop is { } s1 ? Math.Abs(e1 - s1) / Cfg.TamanoTick : 0;
			L.Add(Linea("b6", new Segmento($"Entrada {Precio(con.Entrada ?? 0)} · SL {Precio(con.Stop ?? 0)} · TP {Precio(con.Tp1 ?? 0)} · R:R {rr:0.0}")));
			L.Add(Linea("b6", new Segmento(plan.Texto(Raiz), plan.Insuficiente ? ColorPanel.Rojo : ColorPanel.Blanco, plan.Insuficiente),
				new Segmento($" · en riesgo {riesgoTicks:0} ticks ({ins.Raiz})", ColorPanel.Gris)));
		}

		// 7) GESTIÓN DE MICRO-IMPULSO.
		Recuadro(L, "7. GESTIÓN DE MICRO-IMPULSO", "b7");
		var ema = ContextoV2.Ema15.Valor;
		L.Add(Linea("b7", new Segmento($"EMA {P2.MicroImpulseEmaLen} (1m): {(ema is { } x ? PrecioMiles(x) : "-")} · "),
			new Segmento(Micro.Texto(PrecioMiles), Micro.Activa ? ColorPanel.Naranja : ColorPanel.Gris)));
		L.Add(Linea("b7", new Segmento($"Se activa por cierre cruzado de la EMA o una vela de cambio de dirección verificada · salida: {SalidaTexto}", ColorPanel.Gris)));

		// 8) CVD: minigráfico de velas de CVD con sus divergencias (nunca sobre el precio).
		Recuadro(L, "8. CVD", "b8");
		var (velas, divs) = TramoCvd(40);
		var cvd = velas.Count > 0 ? velas[^1].Close : 0;
		L.Add(Linea("b8", new Segmento($"CVD {cvd:+#,##0;-#,##0;0} contratos · delta última vela {Matriz.DeltaActual:+#,##0;-#,##0;0} contratos · 1m · " +
			$"reinicio {(P2.CvdReset == ReinicioCvd.Rth ? "09:30" : "18:00")} NY")));
		L.Add(new LineaPanel
		{
			Grupo = "b8",
			Grafico = new GraficoMini(velas.Select(v => new VelaMini(v.Open, v.High, v.Low, v.Close)).ToList(),
				divs.Select(d => new TramoMini(d.I1, d.I2, d.Cvd1, d.Cvd2, d.Alcista, d.Micro)).ToList())
		});
		var mx = Matriz;
		var mi = mx.Micro;
		L.Add(Linea("b8", new Segmento("Trigger Matrix: macro "), Direccion(mx.Macro?.Alcista), new Segmento(" · micro "), Direccion(mi?.Alcista),
			new Segmento(mi is { } u
				? $" · absorción {u.MechaPct:0}% mecha / {u.VolumenMechaPct:0}% vol {(u.Absorcion ? "OK" : "no")} · giro {(u.Giro is { } g ? g.ToString("+#,##0;-#,##0", CultureInfo.InvariantCulture) : "-")}"
				: "", ColorPanel.Gris)));

		if (mx.UltimaSenal is { } sm)
			L.Add(Linea("b8", new Segmento($"Último disparo: {(sm.Alcista ? "COMPRA" : "VENTA")} {Precio(sm.Precio)} · {TiempoV2.HoraCdmx(sm.Utc)} (confluencia; no crea operación)",
				ColorSigno(sm.Alcista ? 1 : -1))));

		// 9) INSTRUMENTO Y CALIBRACIÓN.
		Recuadro(L, "9. INSTRUMENTO Y CALIBRACIÓN", "b9");
		var cal = ins.Calibracion;

		if (ins.Bloqueo is { } bloqueo)
		{
			L.Add(Linea("b9", new Segmento(bloqueo, ColorPanel.Rojo, true)));
		}
		else
		{
			L.Add(Linea("b9", new Segmento($"{ins.Raiz} · contrato {ins.Contrato} · tick {ins.Spec?.Tick} · "),
				ins.TickValidado
					? new Segmento($"{ins.ValorTick:0.##} USD/tick · {ins.Spec?.ValorPunto:0.##} USD/punto")
					: new Segmento(Instrumentos.NoValidado, ColorPanel.Naranja, true)));
			L.Add(Linea("b9", new Segmento($"VolumePerBar {ins.VolumenPorBarra} contratos de {ins.Raiz} " +
				$"({(cal?.Estado == EstadoCalibracion.Manual ? "manual" : cal is { VolumePerBarAuto: > 0 } ? "AUTO" : "provisional")})")));
			L.Add(Linea("b9", new Segmento($"Mediana por minuto: volumen {cal?.MedianaVolumenMinuto ?? 0:0} contratos · |delta| {cal?.MedianaDeltaAbsMinuto ?? 0:0} contratos · " +
				$"DeltaPct mínimo F3 {P2.F3DeltaPctMin:P0}")));
			L.Add(Linea("b9", new Segmento("Perfil: "), new Segmento(ins.Perfil, ins.Provisional ? ColorPanel.Naranja : ColorPanel.Verde, true),
				new Segmento(cal is null ? " · sin historial suficiente" : $" · {cal.Sesiones} sesiones · {TiempoV2.HoraCdmx(cal.FechaUtc, "dd/MM HH:mm")}{(o.Detalle ? " · " + cal.Metodo : "")}",
					ColorPanel.Gris)));
		}

		// 10) ALERTAS.
		Recuadro(L, "10. ALERTAS", "b10");
		L.Add(Linea("b10", new Segmento($"Correo {(o.CorreoConfigurado ? "configurado" : "SIN CONFIGURAR")} · Telegram {(o.TelegramConfigurado ? "configurado" : "SIN CONFIGURAR")}",
			o.CorreoConfigurado || o.TelegramConfigurado ? ColorPanel.Blanco : ColorPanel.Naranja)));

		foreach (var r in o.Envios.OrderBy(x => x.Canal, StringComparer.Ordinal))
			L.Add(Linea("b10", new Segmento($"Último envío {r.Canal}: {TiempoV2.HoraCdmx(r.Utc)} · {(r.Ok ? "OK" : "ERROR")}{(r.Detalle.Length > 0 ? " · " + r.Detalle : "")}",
				r.Ok ? ColorPanel.Verde : ColorPanel.Rojo)));

		// 11) MT5.
		Recuadro(L, "11. MT5", "b11");

		foreach (var linea in o.Mt5.DefaultIfEmpty("MT5: APAGADO"))
			L.Add(Linea("b11", new Segmento(linea, linea.Contains("SIN", StringComparison.Ordinal) || linea.Contains("ERROR", StringComparison.Ordinal) ? ColorPanel.Naranja : ColorPanel.Blanco)));

		// 12) CALIDAD Y TRAZABILIDAD.
		Recuadro(L, "12. CALIDAD Y TRAZABILIDAD", "b12");
		L.Add(Linea("b12", new Segmento($"Módulo activo: {o.Version} · eventos del día: {Evaluaciones.Count(x => Zonas.DiaOperativo(x.T0Utc) == hoy)} · CSV {o.ArchivoCsv}")));
		L.Add(Linea("b12", new Segmento($"Datos: {(UltimoMinutoAprox ? "APROX (footprint 1m)" : "TICKS")} · {(o.Mbo ? "MBO" : "INFERIDO")} · volumen y delta en contratos de {ins.Raiz}")));
		L.Add(Linea("b12", new Segmento("Fuentes: " + string.Join(" · ", Enum.GetValues<FuenteSuite>().Where(f => f != FuenteSuite.SessionSweeps)
			.Select(f => $"{f} {TextoFuente(f)}")))));

		// 13-16) TRACKERS al final (el propietario: los recuadros de icebergs, stops… pasan al final).
		Tracker<IcebergEvent>(L, "13. ICEBERG", "b13", FuenteSuite.Iceberg,
			x => $"{(x.Compra ? "comprador" : "vendedor")} {Precio(x.Precio)} · {x.Estado} · {x.Ejecutado:0} contratos · {x.Recargas} recargas · {(x.Mbo ? "MBO" : "INFERIDO")}", o);
		Tracker<StackedEvent>(L, "14. STACKED / IMBALANCE", "b14", FuenteSuite.Stacked,
			x => $"{(x.Compra ? "compra" : "venta")} {Precio(x.Bajo)}-{Precio(x.Alto)} · {x.Niveles} niveles · {x.Estado}", o,
			Fuentes.Consultar<ImbalanceEvent>(Ahora.AddHours(-24), Ahora).LastOrDefault() is { } imb
				? $"Último imbalance: {(imb.Compra ? "compra" : "venta")} · {imb.Precios.Count} precios · {TiempoV2.HoraCdmx(imb.Utc)}"
				: null);
		Tracker<StopsEvent>(L, "15. STOPS", "b15", FuenteSuite.Stops,
			x => $"{(x.Compra ? "compra" : "venta")} {Precio(x.Desde)} → {Precio(x.Extremo)} · {x.Contratos:0} contratos · {x.Estado} {x.Zona}".TrimEnd(), o);
		Tracker<SweepTrackerEvent>(L, "16. SWEEPS", "b16", FuenteSuite.Sweeps,
			x => $"{(x.Compra ? "compra" : "venta")} {Precio(x.Desde)} → {Precio(x.Hasta)} · {x.Contratos:0} contratos · {x.Niveles} niveles · {x.DuracionMs:0} ms · {x.Tipo}", o);

		if (o.Detalle && e is not null)
			Detalle(L, e);

		return c;
	}

	/// <summary>15m / 5m: flecha verde ▲ alcista, roja ▼ bajista, "(-)" en rango (el documento del propietario).</summary>
	private static Segmento Alineacion(Tendencia t) => t.Signo() switch
	{
		> 0 => new Segmento("", ColorPanel.Verde, true, FlechaPanel.Arriba, ColorPanel.Verde),
		< 0 => new Segmento("", ColorPanel.Rojo, true, FlechaPanel.Abajo, ColorPanel.Rojo),
		_ => new Segmento("(-)", ColorPanel.Gris)
	};

	/// <summary>
	/// Recuadro sumario de la absorción (Iceberg, Stops, Sweeps, Stacked, Imbalance) con el resultado de cada uno, las
	/// confluencias activas y la sumatoria: BARRIDO DE LIQUIDEZ REAL / NO COMPLETADO / ROTO.
	/// </summary>
	private void ResumenAbsorcion(List<LineaPanel> L, EvaluacionBarrido? e)
	{
		if (e is null)
		{
			L.Add(Linea("b3", new Segmento("Sin barrido que resumir", ColorPanel.Gris)));
			return;
		}

		var confluencias = new List<string>();

		if (e.ScoreC > 0 || e.AbsorcionConsolidacion)
			confluencias.Add("Absorción detectada");

		if (e.Bos)
			confluencias.Add($"BOS confirmado {(e.Nivel.Grupo is "dia" or "semana" ? "externo" : "interno")}");

		if (e.FvgAFavor)
			confluencias.Add("FVG activo");

		if (e.LvnObjetivo)
			confluencias.Add("LVN objetivo");

		L.Add(Linea("b3", new Segmento("Confluencias: "), new Segmento(confluencias.Count > 0 ? string.Join(" · ", confluencias) : "ninguna aún",
			confluencias.Count > 0 ? ColorPanel.Verde : ColorPanel.Gris)));

		(string Texto, ColorPanel Color) Estado(FuenteSuite f, bool ok, bool parcial, string dato)
		{
			var fuente = TextoFuente(f);

			if (fuente is "NO CARGADO" or "APAGADO")
				return ($"{f}: {fuente}", ColorPanel.Gris);

			return ok ? ($"{f}: OK {dato}".Trim(), ColorPanel.Verde) : parcial ? ($"{f}: parcial {dato}".Trim(), ColorPanel.Naranja) : ($"{f}: no", ColorPanel.Gris);
		}

		var d = e.Desglose;
		var partes = new[]
		{
			Estado(FuenteSuite.Iceberg, e.IcebergEstado == "AGUANTANDO", e.IcebergEstado is "ACTIVO" or "ROTO", $"{e.IcebergEstado} {e.IcebergModo}"),
			Estado(FuenteSuite.Stops, e.StopsEvento.Length > 0, false, e.StopsEvento),
			Estado(FuenteSuite.Sweeps, d.Any(x => x.Texto.StartsWith("Ráfaga del Sweeps", StringComparison.Ordinal)), d.Any(x => x.Texto.StartsWith("Ráfaga propia", StringComparison.Ordinal)), ""),
			Estado(FuenteSuite.Stacked, e.StackedAgainst && e.StackedNewDir, e.StackedAgainst || e.StackedNewDir, ""),
			Estado(FuenteSuite.Imbalance, Fuentes.Consultar<ImbalanceEvent>(e.T0Utc.AddSeconds(-P2.SuitePreSec), e.TimeConfirmUtc ?? Ahora, x => x.Compra == (e.SignoGiro > 0)).Count > 0, false, "")
		};

		var recuadro = new LineaPanel { Grupo = "b3", Recuadro = ColorPanel.Gris };

		for (var i = 0; i < partes.Length; i++)
			recuadro.Segmentos.Add(new Segmento((i > 0 ? " · " : "") + partes[i].Texto, partes[i].Color));

		L.Add(recuadro);

		var (veredicto, color) = e.Estado switch
		{
			EstadoBarrido.Real or EstadoBarrido.Premium => (e.Estado == EstadoBarrido.Premium ? "BARRIDO DE LIQUIDEZ REAL PREMIUM" : "BARRIDO DE LIQUIDEZ REAL", ColorSigno(e.SignoGiro)),
			EstadoBarrido.Ruptura or EstadoBarrido.Fallido or EstadoBarrido.RupturaFallida => ("ROTO", ColorPanel.Morado),
			_ => ("NO COMPLETADO", ColorPanel.Naranja)
		};
		L.Add(new LineaPanel { Grupo = "b3", Recuadro = color }.Con(new Segmento("Sumatoria: "))
			.Con(new Segmento(veredicto, color, true)).Con(new Segmento($" · {e.Total:0}/100", ColorPanel.Gris)));
	}

	/// <summary>Los tres modelos (M1, M2, M3) en una línea cada uno; M3 activo en su recuadro fucsia con lo que invalidó.</summary>
	private void Modelos(List<LineaPanel> L)
	{
		foreach (var (m, nombre, activo) in new[] { ("M1", "M1 barrido", P2.EnableModelA), ("M2", "M2 LVN / EDGE / FVG", P2.EnableModelB), ("M3", "M3 iceberg rebasado", P2.EnableModelM3) })
		{
			var s = Setups.LastOrDefault(x => x.ModeloOperativo == m && (x.Pendiente || x.Abierta)) ?? Setups.LastOrDefault(x => x.ModeloOperativo == m);
			var texto = !activo ? "APAGADO" : s is null ? "sin setup" : $"{s.Etiqueta} · {s.Zona.Tipo} {Precio(s.Zona.Bajo)}-{Precio(s.Zona.Alto)} · {s.TextoEstado}" +
				$"{(s.Pendiente ? $" · a {Ticks(PrecioActual - (s.Zona.Bajo + s.Zona.Alto) / 2)}" : "")}{(s.Motivo.Length > 0 ? " · " + s.Motivo : "")}";
			L.Add(Linea("b3", new Segmento($"{nombre}: ", ColorPanel.Gris), new Segmento(texto, s is null || !activo ? ColorPanel.Gris : ColorModelo(s), s?.Abierta == true)));
		}

		if (Mod3Activo is { } m3 && P2.EnableModelM3)
		{
			L.Add(new LineaPanel { Grupo = "b3", Recuadro = ColorPanel.Fucsia }.Con(new Segmento(
				$"M3 ROMPIMIENTO CONTINUIDAD {(m3.Signo > 0 ? "LARGO" : "CORTO")} · iceberg rebasado en {Precio(m3.Precio)} · {TiempoV2.HoraCdmx(m3.Utc)}", ColorPanel.Fucsia, true)));

			foreach (var s in Setups.Where(x => x.Motivo.StartsWith("INVALIDADO POR M3", StringComparison.Ordinal)).TakeLast(3))
				L.Add(Linea("b3", new Segmento($"{s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")}: INVALIDADO POR M3", ColorPanel.Gris)));
		}
	}

	private string SalidaTexto => P2.ExitMode switch
	{
		ModoSalida.SoloTp => $"TP {P2.TpRMultiple:0.#}R",
		ModoSalida.SoloEma15 => "cierre al otro lado de la EMA 15",
		_ => $"TP {P2.TpRMultiple:0.#}R o EMA 15 (lo primero)"
	};

	private static LineaPanel Linea(string grupo, params Segmento[] s)
	{
		var l = new LineaPanel { Grupo = grupo };
		l.Segmentos.AddRange(s);
		return l;
	}

	private static void Recuadro(List<LineaPanel> L, string titulo, string grupo)
	{
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion(titulo, grupo));
	}

	private static Segmento Direccion(bool? alcista) => alcista is { } x
		? new Segmento(x ? "ALCISTA" : "BAJISTA", ColorSigno(x ? 1 : -1), true)
		: new Segmento("-", ColorPanel.Gris);

	private static ColorPanel ColorModelo(SetupModelo s) => s.Estado switch
	{
		EstadoModelo.Entrada or EstadoModelo.Gestion => ColorSigno(s.Signo),
		EstadoModelo.Invalidado => ColorPanel.Gris,
		EstadoModelo.GatilloPendiente => ColorPanel.Naranja,
		_ => ColorPanel.Blanco
	};

	/// <summary>
	/// Cabecera (v4, siempre visible): estado del indicador y activo; resumen ejecutivo del contexto; estado del barrido;
	/// gatillo (CUMPLIDO solo con vela cerrada; si no, a dónde va el precio).
	/// </summary>
	private void Cabecera(List<LineaPanel> L, EvaluacionBarrido? e, OpcionesPanel o)
	{
		// Estado del indicador y activo detectado (NQ, MNQ…).
		var ins = Instrumento;
		var trackers = new[] { FuenteSuite.Iceberg, FuenteSuite.Stacked, FuenteSuite.Stops, FuenteSuite.Sweeps }.Count(f => Fuentes.Estado(f) == EstadoFuente.Cargado);
		var perfil = ins.Bloqueo ?? (ins.Calibracion?.Estado switch
		{
			EstadoCalibracion.Calibrado => "CALIBRADO",
			EstadoCalibracion.Manual => "MANUAL",
			_ => "PROVISIONAL"
		});
		L.Add(LineaPanel.De(new Segmento(perfil, ins.Bloqueo is not null ? ColorPanel.Rojo : ins.Provisional ? ColorPanel.Naranja : ColorPanel.Verde, true),
			new Segmento(ins.Bloqueo is null ? " · ACTIVO · " : " · ", ColorPanel.Blanco, ins.Bloqueo is null),
			new Segmento(ins.Soportado ? $"{ins.Raiz} ({ins.Contrato})" : Simbolo, ColorPanel.Blanco, true),
			new Segmento($" · datos {(UltimoMinutoAprox ? "APROX" : "TICKS")} · Thermal {TextoFuente(FuenteSuite.Thermal)} · trackers {trackers}/4 · {o.Mt5.FirstOrDefault() ?? "MT5: APAGADO"}",
				ColorPanel.Gris)));

		// Resumen ejecutivo del contexto: H1, 15m, 5m, sesión y killzone.
		var h1 = MotorContextoV2.DeH1(H1.Valor);
		var sesion = Sesiones.Instancias.LastOrDefault(s => s.Abierta)?.Nombre ?? "fuera de sesión";
		L.Add(LineaPanel.De(new Segmento("CONTEXTO: H1 "), new Segmento(h1.Texto(), ColorSigno(h1.Signo()), true), new Segmento(" · 15m "), Alineacion(ContextoV2.M15),
			new Segmento(" · 5m "), Alineacion(ContextoV2.M5), new Segmento($" · {sesion}{(TiempoV2.EnKillzone(Ahora, P2) ? " · killzone" : "")}", ColorPanel.Gris)));

		var (estado, color) = e switch
		{
			null => ("SIN OPERACIÓN", ColorPanel.Gris),
			{ Provisional: true } => ("PENDIENTE DE CUMPLIMIENTO DE BARRIDO DE LIQUIDEZ", ColorPanel.Naranja),
			{ Estado: EstadoBarrido.Premium } => ("BARRIDO DE LIQUIDEZ REAL PREMIUM", ColorPanel.Dorado),
			{ Estado: EstadoBarrido.Real } => ("BARRIDO DE LIQUIDEZ REAL", ColorSigno(e.SignoGiro)),
			{ Estado: EstadoBarrido.Ruptura } => ("ROMPIMIENTO / ACEPTACIÓN (contexto)", ColorPanel.Azul),
			_ => ("SIN OPERACIÓN", ColorPanel.Gris)
		};
		L.Add(LineaPanel.De(new Segmento("ESTADO: "), new Segmento(estado, color, true)));

		var valido = Setups.LastOrDefault(s => s.Abierta);
		var pendiente = Setups.LastOrDefault(s => s.Estado == EstadoModelo.GatilloPendiente);
		var camino = Setups.Where(s => s.Estado == EstadoModelo.Armado)
			.OrderByDescending(s => s.ModeloOperativo is "M2" or "M3")
			.ThenBy(s => Math.Abs(PrecioActual - (s.Zona.Bajo + s.Zona.Alto) / 2))
			.FirstOrDefault();

		L.Add(valido is not null
			? LineaPanel.De(new Segmento(valido.Signo > 0 ? "GATILLO ALCISTA CUMPLIDO" : "GATILLO BAJISTA CUMPLIDO", ColorSigno(valido.Signo), true, Flecha(valido.Signo), ColorSigno(valido.Signo)),
				new Segmento($" · {valido.Etiqueta}", ColorPanel.Gris))
			: pendiente is not null
				? LineaPanel.De(new Segmento($"GATILLO PENDIENTE · {pendiente.Etiqueta} · falta: {string.Join(", ", pendiente.Gatillo?.Faltan.Take(2) ?? Enumerable.Empty<string>())}", ColorPanel.Naranja))
				: camino is not null
					? LineaPanel.De(new Segmento($"NO SE HA CUMPLIDO — EN CAMINO A {Destino(camino.Zona)}"),
						new Segmento($" · a {Ticks(PrecioActual - (camino.Zona.Bajo + camino.Zona.Alto) / 2)}", ColorPanel.Gris))
					: LineaPanel.De(new Segmento("NO SE HA CUMPLIDO — sin zona armada", ColorPanel.Gris)));
	}

	/// <summary>"LVN 21,412.50", "EDGE PISO HVN 21,404.00", "FVG 21,410.00"… para "EN CAMINO A".</summary>
	private string Destino(ZonaOperable z)
	{
		if (z.EdgeId is { } id && Edge.Todas.FirstOrDefault(x => x.Id == id) is { } g)
			return $"EDGE {g.Texto} {PrecioMiles(g.Precio)}";

		return $"{z.Tipo} {PrecioMiles((z.Bajo + z.Alto) / 2)}";
	}

	private void Tracker<T>(List<LineaPanel> L, string titulo, string grupo, FuenteSuite fuente, Func<T, string> texto, OpcionesPanel o, string? extra = null)
		where T : EventoSuite
	{
		Recuadro(L, titulo, grupo);
		var estado = TextoFuente(fuente);
		var eventos = Fuentes.Consultar<T>(Ahora.AddHours(-24), Ahora);
		var ultimo = eventos.LastOrDefault();
		L.Add(Linea(grupo, new Segmento(estado, estado == "CARGADO" ? ColorPanel.Verde : ColorPanel.Gris),
			new Segmento(ultimo is null ? " · sin eventos" : $" · {TiempoV2.HoraCdmx(ultimo.Utc)} · {texto(ultimo)}", ultimo is null ? ColorPanel.Gris : ColorPanel.Blanco)));

		if (extra is not null)
			L.Add(Linea(grupo, new Segmento(extra, ColorPanel.Gris)));

		if (o.Detalle)
			L.Add(Linea(grupo, new Segmento($"Conexión {estado} · eventos de hoy: {eventos.Count(x => Zonas.DiaOperativo(x.Utc) == Zonas.DiaOperativo(Ahora))}", ColorPanel.Gris)));
	}

	private string TextoFuente(FuenteSuite f)
	{
		var peso = f switch
		{
			FuenteSuite.Iceberg => P2.Iceberg,
			FuenteSuite.Stops => P2.Stops,
			FuenteSuite.Sweeps => P2.Sweeps,
			FuenteSuite.Stacked => P2.Stacked,
			FuenteSuite.Imbalance => P2.Imbalance,
			FuenteSuite.Absorption => P2.Absorption,
			_ => null
		};

		if (peso is { Enabled: false })
			return "APAGADO";

		return Fuentes.Estado(f) switch
		{
			EstadoFuente.Cargado => "CARGADO",
			EstadoFuente.SinDatos => "SIN DATOS",
			EstadoFuente.VersionDistinta => "VERSIÓN DISTINTA",
			_ => "NO CARGADO"
		};
	}

	private void Barrido(List<LineaPanel> L, EvaluacionBarrido e, string grupo)
	{
		var dir = e.Estado is EstadoBarrido.Ruptura or EstadoBarrido.Fallido ? e.Signo : e.SignoGiro;
		L.Add(Linea(grupo, new Segmento($"{e.Nombre} {Precio(e.L)} · {TiempoV2.HoraCdmx(e.T0Utc, "dd/MM/yyyy HH:mm:ss")} · nueva dirección "),
			new Segmento(dir > 0 ? "ALCISTA" : "BAJISTA", ColorSigno(dir), true)));

		// v4: el detalle del nivel con su precio y su hora (MIN Día anterior, por ejemplo).
		L.Add(Linea(grupo, new Segmento($"Nivel: {e.Nombre} {PrecioMiles(e.L)} · formado {TiempoV2.HoraCdmx(e.Nivel.CreadoUtc, "dd/MM HH:mm")} · extremo {PrecioMiles(e.Extremo)} " +
			$"a las {TiempoV2.HoraCdmx(e.ExtremoUtc, "HH:mm:ss")}", ColorPanel.Gris)));

		var estado = e.Provisional ? $"PROVISIONAL {e.Total:0}/100" : $"{e.Estado.Texto()} {e.Total:0}/100{(e.UpgradePremium ? " · UPGRADE PREMIUM" : "")}";
		var color = e.Provisional ? ColorPanel.Naranja : e.Estado switch
		{
			EstadoBarrido.Real or EstadoBarrido.Premium => ColorSigno(e.SignoGiro),
			EstadoBarrido.Ruptura => ColorPanel.Azul,
			EstadoBarrido.Fallido or EstadoBarrido.RupturaFallida => ColorPanel.Morado,
			_ => ColorPanel.Gris
		};
		L.Add(Linea(grupo, new Segmento("Clasificación: "), new Segmento(estado, color, true),
			new Segmento(e.ConflictoPine ? " · CONFLICTO PINE" : "", ColorPanel.Amarillo), new Segmento(e.Noticia ? " · NOTICIA" : "", ColorPanel.Amarillo)));
		L.Add(Linea(grupo, new Segmento($"Penetración {e.PenetrationTicks} ticks · volumen más allá {e.VolBeyond:0} contratos ({e.VolBeyondRel:0.0}× la mediana)")));
		L.Add(Linea(grupo, new Segmento($"Stops tomados: {(e.StopsEvento.Length > 0 ? e.StopsEvento : "sin evento")} · Stops Tracker ProSuite {TextoFuente(FuenteSuite.Stops)}")));

		// v4: el rango de ruptura como vela compuesta (compras contra ventas), la consolidación y el CVD desde T0.
		L.Add(Linea(grupo, new Segmento($"Rango de ruptura: compras {e.VolumenCompra:#,##0} / ventas {e.VolumenVenta:#,##0} contratos · consolidación {e.ConsolidacionVelas} velas · CVD "),
			new Segmento(e.CvdGiro ? "GIRA" : e.CvdDiverge ? "DIVERGE" : "sin giro", e.CvdGiro || e.CvdDiverge ? ColorSigno(e.SignoGiro) : ColorPanel.Gris, e.CvdGiro || e.CvdDiverge),
			new Segmento(e.AbsorcionConsolidacion ? " · ABSORCIÓN EN CONSOLIDACIÓN" : "", ColorPanel.Verde, true)));
		L.Add(Linea(grupo, new Segmento("Estructura: "), new Segmento($"BOS {(e.Bos ? "sí" : "no")} · FVG {(e.FvgAFavor ? "sí" : "no")} · LVN {(e.LvnObjetivo ? "sí" : "no")}",
			ColorPanel.Gris), new Segmento(e.EstructuraConfirmada ? " → ESTRUCTURA CONFIRMADA" : "", ColorSigno(e.SignoGiro), true)));

		// Validación (v3, 1.3): "Certificada" SOLO con MBO e iceberg AGUANTANDO; inferido = "Probable".
		var (validacion, cv) = e.IcebergEstado == "AGUANTANDO" && e.IcebergModo == "MBO" ? ("Certificada como real por iceberg (MBO)", ColorPanel.Verde)
			: e.IcebergModo == "INFERIDO" ? ("Probable: iceberg inferido", ColorPanel.Naranja)
			: ("Sin iceberg", ColorPanel.Gris);
		L.Add(Linea(grupo, new Segmento("Validación: "), new Segmento(validacion, cv, cv == ColorPanel.Verde)));

		foreach (var (marco, t) in new[] { ("1m", e.Recovery1mUtc), ("5m", e.Recovery5mUtc) })
		{
			L.Add(Linea(grupo, new Segmento($"Recuperación {marco} (>= {Ticks(P2.RecoveryTicks * Cfg.TamanoTick)}): "),
				t is { } r ? new Segmento($"Confirmada {TiempoV2.HoraCdmx(r)}", ColorSigno(dir), true) : new Segmento("Pendiente", ColorPanel.Naranja)));
		}

		if (e.Provisional && e.Faltan.Count > 0)
			L.Add(Linea(grupo, new Segmento("Falta: " + string.Join(", ", e.Faltan), ColorPanel.Naranja)));
	}

	/// <summary>Próximo objetivo LVN o EDGE (el más cercano en la dirección) y conteo de zonas activas.</summary>
	private void ProximoObjetivoPanel(List<LineaPanel> L, EvaluacionBarrido? e, string grupo)
	{
		var abierta = Setups.LastOrDefault(s => s.Abierta);
		var dir = abierta?.Signo ?? Setups.LastOrDefault(s => s.Pendiente)?.Signo ?? (e is null ? 0 : e.Estado == EstadoBarrido.Ruptura ? e.Signo : e.SignoGiro);
		var etiqueta = dir > 0 ? "(alcista)" : dir < 0 ? "(bajista)" : "(sin dirección)";

		if (!HayFuenteLvn && !Lvn.Activas.Any() && !Edge.Activas.Any())
		{
			L.Add(Linea(grupo, new Segmento("SIN FUENTE LVN (Thermal Volume Profile + LVN (Suite) no cargado)", ColorPanel.Amarillo)));
			return;
		}

		var precio = abierta?.Entrada ?? PrecioActual;
		var minimo = P2.TargetMinDistTicks * Cfg.TamanoTick;
		var l = dir == 0 ? null : Lvn.Proximo(precio, dir, minimo);
		var g = dir == 0 ? null : Edge.Proximo(precio, dir, minimo);

		if (l is null && g is null)
		{
			L.Add(Linea(grupo, new Segmento("Próximo objetivo "), new Segmento(dir < 0 ? "(short)" : dir > 0 ? "(long)" : etiqueta, ColorPanel.Naranja),
				new Segmento(": Sin objetivo LVN / EDGE", ColorPanel.Amarillo)));
		}
		else if (l is not null && (g is null || Math.Abs(l.Centro - precio) <= Math.Abs(g.Precio - precio)))
		{
			L.Add(Linea(grupo, new Segmento("Próximo LVN objetivo "), new Segmento(etiqueta, ColorPanel.Naranja), new Segmento(": "),
				new Segmento(PrecioMiles(l.Centro), ColorSigno(dir), true),
				new Segmento($" ({l.Origen}) {l.Estado} · {TiempoV2.HoraCdmx(l.Utc)} · a {Ticks(l.Centro - precio)}", ColorPanel.Gris)));
		}
		else if (g is not null)
		{
			L.Add(Linea(grupo, new Segmento("Próximo EDGE objetivo "), new Segmento(etiqueta, ColorPanel.Naranja), new Segmento(": "),
				new Segmento($"{g.Texto} {PrecioMiles(g.Precio)}", ColorSigno(dir), true),
				new Segmento($" ({g.Origen}) {g.Estado} · a {Ticks(g.Precio - precio)}", ColorPanel.Gris)));
		}

		// v4: y/o el próximo FVG en la dirección (sin rellenar), y qué operación esperar.
		if (dir != 0 && Fvg2.Fvgs.Where(f => f.Vivo && dir * ((f.Bajo + f.Alto) / 2 - precio) >= minimo).OrderBy(f => Math.Abs((f.Bajo + f.Alto) / 2 - precio)).FirstOrDefault() is { } fvg)
		{
			L.Add(Linea(grupo, new Segmento("Próximo FVG "), new Segmento(etiqueta, ColorPanel.Naranja), new Segmento(": "),
				new Segmento($"{PrecioMiles(fvg.Bajo)}-{PrecioMiles(fvg.Alto)}", ColorSigno(dir), true), new Segmento($" ({fvg.Marco}m) · a {Ticks((fvg.Bajo + fvg.Alto) / 2 - precio)}", ColorPanel.Gris)));
		}

		if (dir != 0)
			L.Add(Linea(grupo, new Segmento("Esperar operación "), new Segmento(dir > 0 ? "ALCISTA" : "BAJISTA", ColorSigno(dir), true)));

		L.Add(Linea(grupo, new Segmento($"LVN activos {Lvn.Activas.Count()} · EDGE activos {Edge.Activas.Count()} · fuente Thermal {TextoFuente(FuenteSuite.Thermal)}", ColorPanel.Gris)));
	}

	private void Detalle(List<LineaPanel> L, EvaluacionBarrido e)
	{
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion($"DETALLE · {e.Nombre} {Precio(e.L)} · T0 {TiempoV2.HoraEt(e.T0Utc)}", "det"));
		L.Add(Linea("det", new Segmento($"A {e.ScoreA:0}/20 · B {e.ScoreB:0}/20 · C {e.ScoreC:0}/25 · D {e.ScoreD:0}/25 · E {e.ScoreE:0}/10 · penalizaciones -{e.Penalties:0} · total {e.Total:0}/100")));
		L.Add(Linea("det", new Segmento($"Puertas premium: {e.PremiumGates} · penetración {e.PenetrationTicks} ticks · VolBeyondRel {e.VolBeyondRel:0.00} · BurstRatio {e.BurstRatio:0.0}")));

		foreach (var p in e.Desglose)
			L.Add(new LineaPanel { Sangria = 1, Grupo = "det" }.Con(new Segmento($"{p.Bloque} {(p.Puntos >= 0 ? "+" : "")}{p.Puntos:0.#} {p.Texto}", p.Puntos < 0 ? ColorPanel.Rojo : ColorPanel.Blanco)));

		foreach (var t in e.Transiciones)
			L.Add(new LineaPanel { Sangria = 1, Grupo = "det" }.Con(new Segmento("Transición " + t, ColorPanel.Gris)));

		foreach (var s in Setups.TakeLast(8))
		{
			L.Add(new LineaPanel { Sangria = 1, Grupo = "det" }.Con(new Segmento(
				$"{s.Etiqueta} {s.Zona.Tipo} {Precio(s.Zona.Bajo)}-{Precio(s.Zona.Alto)} · {s.TextoEstado} · gatillo {s.Gatillo?.Texto ?? "sin tocar"}{(s.Motivo.Length > 0 ? " · " + s.Motivo : "")}",
				s.Estado == EstadoModelo.Invalidado ? ColorPanel.Gris : ColorPanel.Blanco)));
		}

		L.Add(new LineaPanel { Sangria = 1, Grupo = "det" }.Con(new Segmento($"Fuentes usadas: {(e.SourcesLoaded.Length > 0 ? e.SourcesLoaded : "ninguna")}", ColorPanel.Gris)));
	}

	/// <summary>Precio con separador de miles (29,412.50), como en el documento.</summary>
	public string PrecioMiles(decimal p) => p.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
