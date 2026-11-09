namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using OrderFlowSuite.Comun;

/// <summary>Opciones del panel que vienen de la carcasa (archivo CSV, datos de la cuenta…).</summary>
public sealed class OpcionesPanel
{
	public bool Detalle;
	public string ArchivoCsv = "";
	public string Version = "Sessions Sweeps Pro v3.0";
	public bool Mbo;
	public decimal? SaldoAtas;
}

/// <summary>S8: contenido del panel flotante principal (11.2 Resumen y 11.3 Detalle), armado bajo el cerrojo del motor.</summary>
public sealed partial class MotorSweeps
{
	public ContenidoPanel Panel(OpcionesPanel o)
	{
		lock (_sync)
			return ArmarPanel(o);
	}

	private static ColorPanel ColorSigno(int s) => s > 0 ? ColorPanel.Verde : s < 0 ? ColorPanel.Rojo : ColorPanel.Blanco;

	private static FlechaPanel Flecha(int s) => s > 0 ? FlechaPanel.Arriba : s < 0 ? FlechaPanel.Abajo : FlechaPanel.Ninguna;

	private ContenidoPanel ArmarPanel(OpcionesPanel o)
	{
		var c = new ContenidoPanel { Titulo = $"SESSIONS SWEEPS PRO · {Simbolo} · {(o.Detalle ? "DETALLE" : "RESUMEN")}", Pie = "Estadística simulada; no es un registro de operaciones reales." };
		var L = c.Lineas;

		// 1) CONTEXTO TEMPORAL.
		var h1 = MotorContextoV2.DeH1(H1.Valor);
		L.Add(LineaPanel.Seccion("1. CONTEXTO TEMPORAL"));
		L.Add(LineaPanel.De(new Segmento("H1: "), new Segmento(h1.Texto(), ColorSigno(h1.Signo()), h1 != Tendencia.Rango)));
		L.Add(LineaPanel.De(new Segmento("15m: "), new Segmento(ContextoV2.M15.Texto(), ColorSigno(ContextoV2.M15.Signo()), false, Flecha(ContextoV2.M15.Signo()), ColorSigno(ContextoV2.M15.Signo()))));
		L.Add(LineaPanel.De(new Segmento("5m: "), new Segmento(ContextoV2.M5.Texto(), ColorSigno(ContextoV2.M5.Signo()), false, Flecha(ContextoV2.M5.Signo()), ColorSigno(ContextoV2.M5.Signo()))));

		// 2) BARRIDO DE LIQUIDEZ EXTERNA (el último de un nivel externo; si no hay, el último de cualquier nivel).
		var e = Evaluaciones.LastOrDefault(x => x.Nivel.Grupo is "dia" or "semana") ?? Evaluaciones.LastOrDefault();
		L.Add(LineaPanel.Linea());

		if (e is null)
		{
			L.Add(LineaPanel.Seccion("2. BARRIDO DE LIQUIDEZ EXTERNA"));
			L.Add(LineaPanel.De(new Segmento("Sin barridos hoy", ColorPanel.Gris)));
		}
		else
		{
			Barrido(L, e, o);
		}

		// 3) RESUMEN DE ABSORCIÓN Y MODELOS.
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("3. RESUMEN DE ABSORCIÓN Y MODELOS"));
		Absorcion(L, e);
		Modelos(L);

		// 4) PRÓXIMO LVN OBJETIVO.
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("4. PRÓXIMO LVN OBJETIVO"));
		ProximoLvn(L, e);

		// 5) FOOTPRINT GATILLO.
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("5. FOOTPRINT GATILLO"));
		var conGatillo = Setups.LastOrDefault(s => s.Gatillo is not null);

		if (conGatillo?.Gatillo is not { } g)
		{
			L.Add(LineaPanel.De(new Segmento("Sin zona tocada", ColorPanel.Gris)));
		}
		else
		{
			var zona = $"{conGatillo.Zona.Tipo} {Precio(conGatillo.Zona.Bajo)}-{Precio(conGatillo.Zona.Alto)}";

			L.Add(g.Resultado switch
			{
				ResultadoGatillo.Valido => LineaPanel.De(new Segmento($"VÁLIDO · {(g.Tipo == TipoGatillo.Arranque ? "ARRANQUE" : "CONTINUIDAD")} · {conGatillo.Etiqueta} · {zona}",
					ColorPanel.Blanco, true, Flecha(g.Direccion), ColorSigno(g.Direccion))),
				ResultadoGatillo.Pendiente => LineaPanel.De(new Segmento($"PENDIENTE · {conGatillo.Etiqueta} · {zona} · {string.Join(", ", g.Faltan.Take(2))}", ColorPanel.Naranja)),
				_ => LineaPanel.De(new Segmento($"INVÁLIDO · {conGatillo.Etiqueta} · {zona} · {g.Motivo}", ColorPanel.Rojo))
			});
		}

		// 6) PLAN DE RIESGO.
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("6. PLAN DE RIESGO"));
		var riesgo = CalculadoraRiesgo.Riesgo(P2, o.SaldoAtas);
		L.Add(LineaPanel.De(new Segmento(P2.RiskMode == ModoRiesgo.Monto
			? $"Modo MONTO: {riesgo:0} USD por operación"
			: $"Modo PORCENTAJE: {P2.RiskPct.ToString("0.##", CultureInfo.InvariantCulture)} % de {(P2.ReadAtasAccount && o.SaldoAtas is > 0 ? o.SaldoAtas.Value : P2.AccountSize):0} = {riesgo:0} USD")));

		if (Setups.LastOrDefault(s => s.Riesgo is not null) is { Riesgo: { } plan } con)
		{
			var rr2 = con.Tp2 is { } t2 && con.Entrada is { } en && con.Stop is { } st && en != st ? Math.Abs(t2 - en) / Math.Abs(en - st) : 0;
			L.Add(LineaPanel.De(new Segmento(plan.Texto(Raiz), plan.Insuficiente ? ColorPanel.Rojo : ColorPanel.Blanco, plan.Insuficiente)));
			L.Add(LineaPanel.De(new Segmento($"R:R TP1 {con.Gatillo?.RewardRisk:0.0} · TP2 {rr2:0.0} · Entrada {Precio(con.Entrada ?? 0)} · SL {Precio(con.Stop ?? 0)}")));
		}

		// 7) GESTIÓN DE MICRO-IMPULSO.
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("7. GESTIÓN DE MICRO-IMPULSO"));
		var ema = ContextoV2.Ema15.Valor;
		L.Add(LineaPanel.De(new Segmento($"EMA {P2.MicroImpulseEmaLen} (1m): {(ema is { } x ? PrecioMiles(x) : "-")} · "),
			new Segmento(Micro.Texto(PrecioMiles), Micro.Activa ? ColorPanel.Naranja : ColorPanel.Gris)));

		// 8) CALIDAD Y TRAZABILIDAD.
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("8. CALIDAD Y TRAZABILIDAD"));
		var hoy = Zonas.DiaOperativo(_ahoraV2 == default ? DateTime.UtcNow : _ahoraV2);
		var ins = Instrumento;
		L.Add(LineaPanel.De(new Segmento($"Instrumento: {(ins.Soportado ? $"{ins.Raiz} ({ins.Contrato})" : ins.Contrato)} · "),
			ins.Bloqueo is { } bloqueo ? new Segmento(bloqueo, ColorPanel.Rojo, true)
				: new Segmento($"tick {ins.Spec?.Tick} · {(ins.TickValidado ? $"{ins.ValorTick:0.##} USD/tick" : Instrumentos.NoValidado)} · {ins.VolumenPorBarra} contratos/barra · {ins.Perfil}" +
					$"{(ins.Calibracion is { } cal ? $" ({cal.Sesiones} sesiones)" : "")}{(ins.Provisional ? " · PROVISIONAL" : "")}",
					ins.TickValidado && !ins.Provisional ? ColorPanel.Blanco : ColorPanel.Naranja)));
		L.Add(LineaPanel.De(new Segmento($"Módulo activo: {o.Version} · eventos del día: {Evaluaciones.Count(x => Zonas.DiaOperativo(x.T0Utc) == hoy)}")));
		L.Add(LineaPanel.De(new Segmento($"Datos: {(UltimoMinutoAprox ? "APROX (footprint 1m)" : "TICKS")} · {(o.Mbo ? "MBO" : "INFERIDO")} · CSV {o.ArchivoCsv}")));
		L.Add(LineaPanel.De(new Segmento("Fuentes: " + string.Join(" · ", Enum.GetValues<FuenteSuite>().Where(f => f != FuenteSuite.SessionSweeps)
			.Select(f => $"{f} {TextoFuente(f)}")))));

		if (o.Detalle && e is not null)
			Detalle(L, e);

		return c;
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

	private void Barrido(List<LineaPanel> L, EvaluacionBarrido e, OpcionesPanel o)
	{
		var trampa = e.Estado.EsTrampa() || e.Estado is EstadoBarrido.Pendiente;
		var dir = e.Estado is EstadoBarrido.Ruptura or EstadoBarrido.Fallido ? e.Signo : e.SignoGiro;
		L.Add(LineaPanel.Seccion($"2. BARRIDO DE LIQUIDEZ EXTERNA ({TiempoV2.HoraCdmx(e.T0Utc)})"));
		L.Add(LineaPanel.De(new Segmento($"{e.Nombre} {Precio(e.L)} · Nueva dirección: "), new Segmento(dir > 0 ? "ALCISTA" : "BAJISTA", ColorSigno(dir), true)));

		var estado = e.Provisional ? $"PROVISIONAL {e.Total:0}/100" : $"{e.Estado.Texto()} {e.Total:0}/100{(e.UpgradePremium ? " · UPGRADE PREMIUM" : "")}";
		var color = e.Provisional ? ColorPanel.Naranja : e.Estado switch
		{
			EstadoBarrido.TrampaB or EstadoBarrido.TrampaA or EstadoBarrido.TrampaPremium => ColorSigno(e.SignoGiro),
			EstadoBarrido.Ruptura => ColorPanel.Azul,
			EstadoBarrido.Fallido or EstadoBarrido.RupturaFallida => ColorPanel.Morado,
			_ => ColorPanel.Gris
		};
		L.Add(LineaPanel.De(new Segmento("Clasificador: "), new Segmento(estado, color, true),
			new Segmento(e.ConflictoPine ? " · CONFLICTO PINE" : "", ColorPanel.Amarillo), new Segmento(e.Noticia ? " · NOTICIA" : "", ColorPanel.Amarillo)));

		L.Add(LineaPanel.De(new Segmento($"Stops tomados: {(e.StopsEvento.Length > 0 ? e.StopsEvento : "sin evento")} · Stops Tracker ProSuite {TextoFuente(FuenteSuite.Stops)}")));

		// Validación (1.3): "Certificada" SOLO con MBO e iceberg AGUANTANDO; inferido = "Probable".
		var (validacion, cv) = e.IcebergEstado == "AGUANTANDO" && e.IcebergModo == "MBO" ? ("Certificada como real por iceberg (MBO)", ColorPanel.Verde)
			: e.IcebergModo == "INFERIDO" ? ("Probable: iceberg inferido", ColorPanel.Naranja)
			: ("Sin iceberg", ColorPanel.Gris);
		L.Add(LineaPanel.De(new Segmento("Validación: "), new Segmento(validacion, cv, cv == ColorPanel.Verde)));

		foreach (var (marco, t) in new[] { ("1m", e.Recovery1mUtc), ("5m", e.Recovery5mUtc) })
		{
			L.Add(LineaPanel.De(new Segmento($"Recuperación {marco} (>= {P2.RecoveryTicks} ticks): "),
				t is { } r ? new Segmento($"Confirmada {TiempoV2.HoraCdmx(r)}", ColorSigno(dir), true) : new Segmento("Pendiente", ColorPanel.Naranja)));
		}

		if (trampa && e.Provisional && e.Faltan.Count > 0)
			L.Add(LineaPanel.De(new Segmento("Falta: " + string.Join(", ", e.Faltan), ColorPanel.Naranja)) );
	}

	private void Absorcion(List<LineaPanel> L, EvaluacionBarrido? e)
	{
		(string Texto, ColorPanel Color) Estado(FuenteSuite f, bool ok, bool parcial, string dato)
		{
			var fuente = TextoFuente(f);

			if (fuente is "NO CARGADO" or "APAGADO")
				return ($"{f}: {fuente}", ColorPanel.Gris);

			return ok ? ($"{f}: OK {dato}".Trim(), ColorPanel.Verde) : parcial ? ($"{f}: parcial {dato}".Trim(), ColorPanel.Naranja) : ($"{f}: no", ColorPanel.Gris);
		}

		var d = e?.Desglose ?? new List<PuntoDesglose>();
		var partes = new[]
		{
			Estado(FuenteSuite.Iceberg, e?.IcebergEstado == "AGUANTANDO", e?.IcebergEstado is "ACTIVO" or "ROTO", e is null ? "" : $"{e.IcebergEstado} {e.IcebergModo}"),
			Estado(FuenteSuite.Stops, e?.StopsEvento.Length > 0, false, e?.StopsEvento ?? ""),
			Estado(FuenteSuite.Sweeps, d.Any(x => x.Texto.StartsWith("Ráfaga del Sweeps", StringComparison.Ordinal)), d.Any(x => x.Texto.StartsWith("Ráfaga propia", StringComparison.Ordinal)), ""),
			Estado(FuenteSuite.Stacked, e?.StackedAgainst == true && e.StackedNewDir, e?.StackedAgainst == true || e?.StackedNewDir == true, ""),
			Estado(FuenteSuite.Imbalance, e is not null && Fuentes.Consultar<ImbalanceEvent>(e.T0Utc.AddSeconds(-P2.SuitePreSec), e.TimeConfirmUtc ?? _ahoraV2, x => x.Compra == (e.SignoGiro > 0)).Count > 0, false, "")
		};

		var l = new LineaPanel { Recuadro = ColorPanel.Gris };

		for (var i = 0; i < partes.Length; i++)
			l.Segmentos.Add(new Segmento((i > 0 ? " · " : "") + partes[i].Texto, partes[i].Color));

		L.Add(l);
	}

	private void Modelos(List<LineaPanel> L)
	{
		var linea = new LineaPanel();

		foreach (var m in new[] { "MOD1", "BFVG", "MOD2", "MOD3", "MOD4" })
		{
			var s = Setups.LastOrDefault(x => x.Modelo == m);
			var color = s is null ? ColorPanel.Gris : s.Estado switch
			{
				EstadoModelo.Entrada or EstadoModelo.Gestion => ColorSigno(s.Signo),
				EstadoModelo.Invalidado => ColorPanel.Rojo,
				EstadoModelo.GatilloPendiente => ColorPanel.Naranja,
				_ => m == "MOD3" ? ColorPanel.Fucsia : m == "MOD4" ? ColorPanel.AzulClaro : ColorPanel.Blanco
			};
			linea.Segmentos.Add(new Segmento($"{(linea.Segmentos.Count > 0 ? " · " : "")}{m} {(s is null ? "INACTIVO" : s.TextoEstado)}", color));
		}

		L.Add(linea);

		if (Mod3Activo is { } m3)
		{
			L.Add(new LineaPanel { Recuadro = ColorPanel.Fucsia }.Con(new Segmento($"MOD3 ROMPIMIENTO CONTINUIDAD {(m3.Signo > 0 ? "LARGO" : "CORTO")} en {Precio(m3.Precio)} · {TiempoV2.HoraCdmx(m3.Utc)}", ColorPanel.Fucsia, true)));

			foreach (var s in Setups.Where(x => x.Motivo == "INVALIDADO POR MOD3").TakeLast(3))
				L.Add(LineaPanel.De(new Segmento($"{s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")}: INVALIDADO POR MOD3", ColorPanel.Gris)));
		}
	}

	private void ProximoLvn(List<LineaPanel> L, EvaluacionBarrido? e)
	{
		var abierta = Setups.LastOrDefault(s => s.Abierta);
		var dir = abierta?.Signo ?? Setups.LastOrDefault(s => s.Pendiente)?.Signo ?? (e is null ? 0 : e.Estado == EstadoBarrido.Ruptura ? e.Signo : e.SignoGiro);
		var etiqueta = dir > 0 ? "(alcista)" : dir < 0 ? "(bajista)" : "(sin dirección)";

		if (!HayFuenteLvn && !Lvn.Activas.Any())
		{
			L.Add(LineaPanel.De(new Segmento("SIN FUENTE LVN (Thermal Volume Profile + LVN (Suite) no cargado)", ColorPanel.Amarillo)));
			return;
		}

		var precio = abierta?.Entrada ?? BarrasV.Actual?.Close ?? BarrasV.Barras.LastOrDefault()?.Close ?? 0;
		var z = dir == 0 ? null : Lvn.Proximo(precio, dir, P2.TargetMinDistTicks * Cfg.TamanoTick);

		L.Add(z is null
			? LineaPanel.De(new Segmento("Próximo LVN objetivo "), new Segmento(dir < 0 ? "(short)" : dir > 0 ? "(long)" : etiqueta, ColorPanel.Naranja), new Segmento(": Sin objetivo LVN", ColorPanel.Amarillo))
			: LineaPanel.De(new Segmento("Próximo LVN objetivo "), new Segmento(etiqueta, ColorPanel.Naranja), new Segmento(": "),
				new Segmento(PrecioMiles(z.Centro), ColorSigno(dir), true), new Segmento($" ({z.Origen}) {TiempoV2.HoraCdmx(z.Utc)}", ColorPanel.Gris)));
	}

	private void Detalle(List<LineaPanel> L, EvaluacionBarrido e)
	{
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion($"DETALLE · {e.Nombre} {Precio(e.L)} · T0 {TiempoV2.HoraEt(e.T0Utc)}"));
		L.Add(LineaPanel.De(new Segmento($"A {e.ScoreA:0}/20 · B {e.ScoreB:0}/20 · C {e.ScoreC:0}/25 · D {e.ScoreD:0}/25 · E {e.ScoreE:0}/10 · penalizaciones -{e.Penalties:0} · total {e.Total:0}/100")));
		L.Add(LineaPanel.De(new Segmento($"Puertas premium: {e.PremiumGates} · penetración {e.PenetrationTicks} ticks · VolBeyondRel {e.VolBeyondRel:0.00} · BurstRatio {e.BurstRatio:0.0}")));

		foreach (var p in e.Desglose)
			L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento($"{p.Bloque} {(p.Puntos >= 0 ? "+" : "")}{p.Puntos:0.#} {p.Texto}", p.Puntos < 0 ? ColorPanel.Rojo : ColorPanel.Blanco)));

		foreach (var t in e.Transiciones)
			L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento("Transición " + t, ColorPanel.Gris)));

		foreach (var s in Setups.Where(s => s.Modelo is "BFVG" or "MOD2" or "MOD4" or "MOD3").TakeLast(8))
		{
			L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento(
				$"{s.Etiqueta} {s.Zona.Tipo} {Precio(s.Zona.Bajo)}-{Precio(s.Zona.Alto)} · {s.TextoEstado} · gatillo {s.Gatillo?.Texto ?? "sin tocar"}{(s.Motivo.Length > 0 ? " · " + s.Motivo : "")}",
				s.Estado == EstadoModelo.Invalidado ? ColorPanel.Gris : ColorPanel.Blanco)));
		}

		L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento($"Fuentes usadas: {(e.SourcesLoaded.Length > 0 ? e.SourcesLoaded : "ninguna")}", ColorPanel.Gris)));
	}

	/// <summary>Precio con separador de miles (29,412.50), como en el documento.</summary>
	public string PrecioMiles(decimal p) => p.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
