namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

public enum ResultadoGatillo
{
	Pendiente,
	Valido,
	Invalido
}

public enum TipoGatillo
{
	Ninguno,
	Arranque,
	Continuidad
}

/// <summary>Resultado del TriggerAnalyzer (9): resultado, tipo, puntuación 0-100 (ranking, no probabilidad), razones, gestión.</summary>
public sealed class AnalisisGatillo
{
	public ResultadoGatillo Resultado = ResultadoGatillo.Pendiente;
	public TipoGatillo Tipo;
	public int Score;

	/// <summary>+1 largo, -1 corto, 0 sin dirección clara ("[-]").</summary>
	public int Direccion;

	public readonly List<string> Razones = new();
	public readonly List<string> Faltan = new();
	public readonly List<string> Hallazgos = new();
	public string Motivo = "";
	public decimal? Entrada, Stop, Tp1, RewardRisk, Poc, Delta;
	public string Subasta = "";
	public string VelaAnalizada = "";
	public int? BarraGatillo;
	public DateTime? Utc;

	/// <summary>La respuesta del documento (10.4).</summary>
	public string Respuesta => Resultado == ResultadoGatillo.Valido
		? Tipo == TipoGatillo.Arranque ? "GATILLO POR VELA DE ARRANQUE" : "GATILLO POR VELA DE CONTINUIDAD"
		: Resultado == ResultadoGatillo.Pendiente ? "PENDIENTE" : "NO EJECUTABLE";

	/// <summary>Nomenclatura del anexo: flecha verde (alcista válida), flecha roja (bajista válida) o [-] (sin direccionalidad).</summary>
	public string Flecha => Resultado == ResultadoGatillo.Valido ? Direccion > 0 ? "▲" : "▼" : "[-]";

	public string Texto => Resultado switch
	{
		ResultadoGatillo.Valido => $"VÁLIDO {(Direccion > 0 ? "ALCISTA" : "BAJISTA")} {(Tipo == TipoGatillo.Arranque ? "ARRANQUE" : "CONTINUIDAD")} {Score}/100",
		ResultadoGatillo.Pendiente => $"PENDIENTE ({string.Join(", ", Faltan.Take(3))})",
		_ => $"INVÁLIDO ({Motivo})"
	};
}

/// <summary>Lo que el analizador necesita del motor.</summary>
public sealed class ContextoGatillo
{
	public IReadOnlyList<BarraVolumen> Barras = Array.Empty<BarraVolumen>();
	public ParametrosV2 P = new();
	public decimal Tick = 0.25m;
	public int ContratosPorBarra = 500;
	public IFuentesSuite Fuentes = SinFuentes.Instancia;
	public Func<int, (double Duracion, double Rango)> Medianas = _ => (60, 8);
	public Tendencia M5, M15;
	public decimal? Ema15;
	public Func<int, bool> CvdAFavor = _ => false;

	/// <summary>Objetivo (TP1) desde una entrada en una dirección: el próximo LVN; si no hay, null (el analizador usa 1.5R).</summary>
	public Func<decimal, int, decimal?> Objetivo = (_, _) => null;

	public DateTime Ahora;
	public bool ExigirKillzone = true;
}

/// <summary>
/// TriggerAnalyzer (9), común al gatillo automático y al manual: zona correcta + absorción/freno + arranque con intención.
/// Con las confluencias del anexo: imbalance de intención, ruptura de la micro-tendencia del retroceso, EMA 15 de 1m,
/// CVD a favor y velas de arranque de una sola vela (pinbar/hammer con el POC en el tercio correcto, envolvente, interna).
/// </summary>
public static class AnalizadorGatillo
{
	/// <summary>Analiza una zona tocada en la barra 'toque' en la dirección z.Signo. 'continuidad' = se acepta la vela de continuidad.</summary>
	public static AnalisisGatillo Analizar(ZonaOperable z, int toque, ContextoGatillo c, bool continuidad)
	{
		var a = new AnalisisGatillo { Direccion = z.Signo };
		var b = c.Barras;
		var p = c.P;
		var tick = c.Tick;
		var s = z.Signo;
		var ultima = b.Count - 1;

		if (toque < 0 || toque > ultima)
		{
			a.Faltan.Add("zona sin tocar");
			return a;
		}

		var fin = Math.Min(ultima, toque + Math.Max(1, p.ZoneMaxBars) - 1);
		var med = c.Medianas(toque);
		var puntos = 0;

		// Compuesta de la zona (4.4 b): desde la primera barra que la toca hasta F3 o ZoneMaxBars.
		int? f3 = null;
		var f3Lenta = false;

		for (var i = toque; i <= fin; i++)
		{
			if (DetectorFases.EsF3(b[i], s, p, med, tick, out var lenta) && s * (b[i].Close - (s > 0 ? z.Alto : z.Bajo)) > 0)
			{
				f3 = i;
				break;
			}

			f3Lenta |= lenta && s * (b[i].Close - (s > 0 ? z.Alto : z.Bajo)) > 0;
		}

		var finComp = f3 ?? fin;
		var comp = Compuesta.Construir(b, toque, finComp, tick, p.ValueAreaPct)!;
		a.Poc = comp.Poc;
		a.Delta = comp.Delta;
		var extremoContra = s > 0 ? comp.Low : comp.High;
		var terminada = comp.SubastaTerminada(-s, p.FinishedAuctionMaxContracts);
		a.Subasta = terminada ? "terminada en el extremo" : "sin terminar en el extremo";

		// F1: llegada a la zona con agresión hacia ella.
		if (b[toque].Delta * s <= 0)
			Sumar(a, ref puntos, "F1 llegada a la zona con agresión en contra", 10);

		// F2: absorción / freno antes del arranque.
		var desde = b[toque].InicioUtc.AddSeconds(-p.SuitePreSec);
		var hasta = b[finComp].FinUtc;
		var margen = p.IcebergMaxDistTicks * tick;
		var factorIce = c.Fuentes.Estado(FuenteSuite.Iceberg) == EstadoFuente.NoCargado ? 0 : p.Iceberg.Factor;
		var icebergs = factorIce > 0 ? c.Fuentes.Consultar<IcebergEvent>(desde, hasta, x => z.Contiene(x.Precio, margen)) : new List<IcebergEvent>();
		var aFavor = icebergs.Where(x => x.Compra == (s > 0) && x.Estado is "AGUANTANDO" or "ACTIVO").OrderByDescending(x => x.Mbo).FirstOrDefault();
		var enContra = icebergs.FirstOrDefault(x => x.Compra == (s < 0) && x.Estado == "AGUANTANDO") ?? icebergs.FirstOrDefault(x => x.Compra == (s > 0) && x.Estado == "ROTO");
		var absorcion = c.Fuentes.Estado(FuenteSuite.Absorption) == EstadoFuente.NoCargado || p.Absorption.Factor == 0
			? null
			: c.Fuentes.Consultar<AbsorptionEvent>(desde, hasta, x => x.Compra == (s > 0) && z.Contiene(x.Precio, margen)).FirstOrDefault();
		var limite = Math.Max(1m, Math.Min((decimal)med.Rango * p.CompressionRangeMult, p.CompressionMaxTicks));
		var comprimidas = 0;
		decimal deltaFreno = 0;

		// F2 se mide en las barras DESPUÉS de la llegada (la barra del toque es F1).
		for (var i = toque + 1; i < finComp; i++)
		{
			if (b[i].RangoTicks(tick) <= limite)
				comprimidas++;

			deltaFreno += b[i].Delta;
		}

		var sinProgreso = s * (extremoContra - (s > 0 ? z.Bajo : z.Alto)) >= -2 * tick;
		var f2 = false;

		if (aFavor is not null)
		{
			Sumar(a, ref puntos, $"Iceberg {(aFavor.Mbo ? "MBO" : "INFERIDO")} {aFavor.Estado} en la zona", (int)Math.Round((aFavor.Mbo ? 20 : 10) * factorIce));
			a.Hallazgos.Add($"Iceberg {(aFavor.Compra ? "COMPRA" : "VENTA")} {aFavor.Estado} {aFavor.Ejecutado:0}c ({(aFavor.Mbo ? "MBO" : "inferido")})");
			f2 = true;
		}
		else if (absorcion is not null)
		{
			Sumar(a, ref puntos, $"Absorption Master {absorcion.Score}/100 en la zona", 10);
			a.Hallazgos.Add($"Absorción {absorcion.Score}/100 {absorcion.Setup}");
			f2 = true;
		}

		if (comprimidas >= Math.Max(1, p.F2MinBars) && comprimidas <= p.F2MaxBarsRetrace)
		{
			Sumar(a, ref puntos, $"F2: {comprimidas} barras de compresión", 10);
			f2 = true;
		}

		if (terminada)
		{
			Sumar(a, ref puntos, "Subasta terminada en el extremo", 10);
			f2 = true;
		}

		if (s * deltaFreno < 0 && sinProgreso && finComp > toque + 1)
		{
			Sumar(a, ref puntos, "Delta en contra sin progreso (absorción)", 10);
			f2 = true;
		}

		puntos = Math.Min(puntos, 60);

		// Imbalance / stacked de intención a favor (anexo) y hallazgos de la suite en la zona.
		var stacked = comp.Stacked(p.ImbalanceRatio, p.ImbalanceMinVolume, p.StackedMinLevels);
		var stackedFavor = stacked.Any(x => x.Compra == (s > 0)) ||
			(p.Stacked.Factor > 0 && c.Fuentes.Consultar<StackedEvent>(desde, hasta, x => x.Compra == (s > 0) && x.Bajo <= z.Alto + 4 * tick && x.Alto >= z.Bajo - 4 * tick).Count > 0);
		var stackedContra = stacked.Any(x => x.Compra == (s < 0) && x.Bajo <= z.Alto && x.Alto >= z.Bajo);
		var imbalanceFavor = p.Imbalance.Factor > 0 && c.Fuentes.Consultar<ImbalanceEvent>(desde, hasta, x => x.Compra == (s > 0)).Count > 0;
		var sweepFavor = p.Sweeps.Factor > 0 ? c.Fuentes.Consultar<SweepTrackerEvent>(desde, hasta, x => x.Compra == (s > 0)).FirstOrDefault() : null;
		var stops = p.Stops.Factor > 0 ? c.Fuentes.Consultar<StopsEvent>(desde, hasta, x => z.Contiene(x.Extremo, 8 * tick)).FirstOrDefault() : null;

		if (stackedFavor || imbalanceFavor)
			Sumar(a, ref puntos, stackedFavor ? "Stacked imbalance a favor (intención)" : "Imbalance a favor (intención)", 10);

		if (stackedFavor)
			a.Hallazgos.Add("Stacked a favor");

		if (stackedContra)
			a.Hallazgos.Add("Stacked en contra");

		if (imbalanceFavor)
			a.Hallazgos.Add("Imbalance a favor");

		if (sweepFavor is not null)
			a.Hallazgos.Add($"Sweep {(sweepFavor.Compra ? "COMPRA" : "VENTA")} {sweepFavor.Niveles} niveles {sweepFavor.Contratos:0}c");

		if (stops is not null)
			a.Hallazgos.Add($"Stops {(stops.Compra ? "COMPRA" : "VENTA")} {stops.Contratos:0}c {stops.Estado}");

		// La barra de reacción: F3 o la primera barra que cierra a favor fuera de la zona.
		var reaccion = f3;

		if (reaccion is null)
		{
			for (var i = toque; i <= fin; i++)
			{
				if (s * (b[i].Close - (s > 0 ? z.Alto : z.Bajo)) > 0 || b[i].CierreEnTercio(s))
				{
					reaccion = i;
					break;
				}
			}
		}

		// Confluencias del anexo sobre la barra de reacción.
		if (reaccion is { } r)
		{
			if (RompeMicroTendencia(b, toque, r, s))
				Sumar(a, ref puntos, "Ruptura de la micro-tendencia del retroceso", 5);

			if (Patron(b, r, s, tick, p) is { } patron)
				Sumar(a, ref puntos, patron, 5);
		}

		if (c.Ema15 is { } ema && (z.Contiene(ema, 4 * tick) || Math.Abs(extremoContra - ema) <= 4 * tick))
			Sumar(a, ref puntos, $"Confluencia con la EMA 15 (1m) {Clasificador.Precio(ema, tick)}", 5);

		if (c.CvdAFavor(s))
			Sumar(a, ref puntos, "CVD a favor (sin divergencia en contra)", 5);

		// ===== Resultado =====
		var u = b[Math.Min(ultima, fin)];
		var atraviesa = Enumerable.Range(toque, Math.Min(ultima, fin) - toque + 1).Any(i => s * (b[i].Close - (s > 0 ? z.Bajo : z.Alto)) < -p.ReclaimBufferTicks * tick);
		// Delta en contra sostenido CON progreso más allá de la zona (sin progreso es absorción, no invalida).
		var deltaContra = !sinProgreso && ultima - toque >= 2 && Enumerable.Range(ultima - 2, 3).All(i => i >= toque && b[i].Delta * s < 0) &&
			Enumerable.Range(ultima - 2, 3).Sum(i => b[i].Delta) * s <= -p.DeltaFlipMin * c.ContratosPorBarra;
		var vencida = ultima >= toque + Math.Max(1, p.ZoneMaxBars) - 1 && f3 is null;

		string? invalido = atraviesa ? "cierre que atraviesa la zona"
			: enContra is not null ? (enContra.Compra == (s > 0) ? "iceberg a favor ROTO" : "iceberg en contra AGUANTANDO")
			: stackedContra ? "stacked en contra"
			: deltaContra && f3 is null ? "delta en contra sostenido"
			: f3Lenta && f3 is null ? "F3 lenta"
			: TiempoV2.EnNoticia(b[toque].InicioUtc, p) ? "NOTICIA"
			: c.ExigirKillzone && !TiempoV2.EnKillzone(b[toque].InicioUtc, p) ? "fuera de killzone"
			: null;

		a.Score = Math.Clamp(puntos + (f3 is not null ? 30 : 0), 0, 100);

		if (invalido is not null)
		{
			(a.Resultado, a.Motivo) = (ResultadoGatillo.Invalido, invalido);
			return a;
		}

		// 9.1 ARRANQUE: F2 y luego F3 rápida a favor que cierra fuera de la zona.
		if (f3 is { } i3 && f2)
		{
			a.Tipo = TipoGatillo.Arranque;
			Gestion(a, z, b[i3], comp, c);
			a.Razones.Add($"F3: {b[i3].RangoTicks(tick)} ticks en {b[i3].DuracionSec:0.0} s, delta {b[i3].DeltaPct:P0}");
			return Ejecutable(a, c, i3);
		}

		// 9.2 CONTINUIDAD: sin F2 completa, con sesgo 5m/15m a favor y barra de reacción con intención.
		if (continuidad && reaccion is { } rc)
		{
			var br = b[rc];
			var sesgo = (c.M5.Signo() == s || c.M15.Signo() == s) && c.M5.Signo() != -s && c.M15.Signo() != -s;
			var rapida = DetectorFases.EsRapida(br, p, med);
			var delta = br.DeltaPct * s >= p.ContDeltaPctMin;
			var intencion = br.Niveles.Count > 0 && VelaCompuesta.StackedDe(br.Niveles, tick, p.ImbalanceRatio, p.ImbalanceMinVolume, p.StackedMinLevels).Any(x => x.Compra == (s > 0))
				|| stackedFavor || (imbalanceFavor && sweepFavor is not null);
			var tercio = br.CierreEnTercio(s);
			var bc = Compuesta.Construir(b, rc, rc, tick, p.ValueAreaPct)!;
			var sinTerminarContra = !bc.SubastaTerminada(-s, p.FinishedAuctionMaxContracts);

			if (sesgo && rapida && delta && intencion && tercio && !sinTerminarContra)
			{
				a.Tipo = TipoGatillo.Continuidad;
				a.Score = Math.Clamp(puntos + 25, 0, 100);
				Gestion(a, z, br, comp, c);
				a.Razones.Add($"Continuidad: barra rápida, delta {br.DeltaPct:P0}, cierre en el tercio a favor, sesgo 5m/15m a favor");
				return Ejecutable(a, c, rc);
			}

			if (!sesgo)
				a.Faltan.Add("falta sesgo 5m/15m a favor");

			if (!intencion)
				a.Faltan.Add("falta stacked o imbalance + ráfaga a favor");
		}

		if (!f2)
			a.Faltan.Add("falta absorción");

		if (f3 is null)
			a.Faltan.Add("falta F3");

		if (vencida)
			(a.Resultado, a.Motivo) = (ResultadoGatillo.Invalido, "vence la ventana");

		return a;
	}

	private static void Sumar(AnalisisGatillo a, ref int puntos, string razon, int valor)
	{
		if (valor <= 0)
			return;

		puntos += valor;
		a.Razones.Add(razon);
	}

	/// <summary>Entrada al cierre de la vela gatillo; stop más allá de la zona/estructura (o del POC de la vela) + StopBufferTicks; TP1.</summary>
	private static void Gestion(AnalisisGatillo a, ZonaOperable z, BarraVolumen gatillo, VelaCompuesta comp, ContextoGatillo c)
	{
		var s = z.Signo;
		var tick = c.Tick;
		var buffer = c.P.StopBufferTicks * tick;
		a.Entrada = gatillo.Close;

		if (c.P.StopMode == ModoStop.PocVelaArranque && Compuesta.PocYVa(gatillo.Niveles, tick, c.P.ValueAreaPct) is { Poc: > 0 } pv)
			a.Stop = pv.Poc - s * buffer;
		else
			a.Stop = (s > 0 ? Math.Min(z.Bajo, comp.Low) : Math.Max(z.Alto, comp.High)) - s * buffer;

		var riesgo = Math.Abs(a.Entrada.Value - a.Stop.Value);
		a.Tp1 = c.Objetivo(a.Entrada.Value, s) ?? Math.Round((a.Entrada.Value + s * 1.5m * riesgo) / tick, MidpointRounding.AwayFromZero) * tick;
		a.RewardRisk = riesgo > 0 ? Math.Round(Math.Abs(a.Tp1.Value - a.Entrada.Value) / riesgo, 2) : 0;
		a.Utc = gatillo.FinUtc;
	}

	/// <summary>9.4: stop &gt; MaxStopTicks o R:R a TP1 &lt; MinRR = NO EJECUTABLE.</summary>
	private static AnalisisGatillo Ejecutable(AnalisisGatillo a, ContextoGatillo c, int barra)
	{
		a.BarraGatillo = barra;
		var stopTicks = (int)Math.Round(Math.Abs(a.Entrada!.Value - a.Stop!.Value) / c.Tick);

		if (stopTicks > c.P.MaxStopTicks)
			(a.Resultado, a.Motivo) = (ResultadoGatillo.Invalido, $"stop {stopTicks} ticks > {c.P.MaxStopTicks}");
		else if (a.RewardRisk < c.P.MinRR)
			(a.Resultado, a.Motivo) = (ResultadoGatillo.Invalido, $"R:R {a.RewardRisk:0.0} < {c.P.MinRR:0.0}");
		else
			a.Resultado = ResultadoGatillo.Valido;

		return a;
	}

	/// <summary>
	/// Micro-tendencia del retroceso (anexo): en un largo, la línea por los dos máximos descendentes de la llegada; la vela
	/// de reacción cierra por encima de su prolongación (espejo en un corto).
	/// </summary>
	public static bool RompeMicroTendencia(IReadOnlyList<BarraVolumen> b, int toque, int reaccion, int s)
	{
		var desde = Math.Max(0, toque - 6);

		if (reaccion - desde < 3)
			return false;

		decimal Punta(int i) => s > 0 ? b[i].High : b[i].Low;

		// Primer pivote: la punta más extrema de la llegada; segundo: la más extrema después, más baja (o más alta).
		var i1 = Enumerable.Range(desde, toque - desde + 1).OrderByDescending(i => s * Punta(i)).First();
		var candidatos = Enumerable.Range(i1 + 1, Math.Max(0, reaccion - i1 - 1)).Where(i => s * (Punta(i1) - Punta(i)) > 0).ToList();

		if (candidatos.Count == 0)
			return false;

		var i2 = candidatos.OrderByDescending(i => s * Punta(i)).First();
		var pendiente = (Punta(i2) - Punta(i1)) / (i2 - i1);
		var linea = Punta(i2) + pendiente * (reaccion - i2);
		return s * (b[reaccion].Close - linea) > 0;
	}

	/// <summary>Velas de arranque de una sola vela (anexo): pinbar/hammer con el POC en el tercio correcto, envolvente, interna.</summary>
	public static string? Patron(IReadOnlyList<BarraVolumen> b, int i, int s, decimal tick, ParametrosV2 p)
	{
		var v = b[i];
		var rango = v.High - v.Low;
		var cuerpo = Math.Abs(v.Close - v.Open);

		if (rango > 0)
		{
			var mecha = s > 0 ? Math.Min(v.Open, v.Close) - v.Low : v.High - Math.Max(v.Open, v.Close);
			var poc = Compuesta.PocYVa(v.Niveles, tick, p.ValueAreaPct).Poc;
			var pocTercio = s > 0 ? poc <= v.Low + rango / 3 : poc >= v.High - rango / 3;

			if (mecha >= 2 * cuerpo && mecha >= 0.6m * rango && pocTercio)
				return s > 0 ? "Vela de arranque: hammer / pinbar con el POC en el tercio inferior" : "Vela de arranque: pinbar con el POC en el tercio superior";
		}

		if (i == 0)
			return null;

		var a = b[i - 1];

		if (s * (a.Close - a.Open) < 0 && s * (v.Close - v.Open) > 0 && s * (v.Close - a.Open) >= 0 && s * (a.Close - v.Open) >= 0)
			return "Vela de arranque: envolvente";

		if (v.High <= a.High && v.Low >= a.Low)
			return "Vela de arranque: vela interna";

		return null;
	}

	/// <summary>
	/// Gatillo MANUAL (10): las barras dentro del recuadro (o el clic: precio ± ManualZoneTicks, últimas ManualLookbackBars).
	/// VELA DE VOLUMEN analiza la última barra; VELA COMPUESTA, todas juntas como una vela macro; AUTO, las dos y gana la
	/// más clara. Dirección AUTO: la de la estructura (POC en el tercio inferior con delta positivo = alcista).
	/// </summary>
	public static AnalisisGatillo Manual(ManualTriggerRequest r, ContextoGatillo c)
	{
		var b = c.Barras;
		var indices = Enumerable.Range(0, b.Count).Where(i => b[i].FinUtc >= r.DesdeUtc && b[i].InicioUtc <= r.HastaUtc && b[i].Low <= r.Alto && b[i].High >= r.Bajo).ToList();

		if (indices.Count == 0)
		{
			var vacio = new AnalisisGatillo { Resultado = ResultadoGatillo.Invalido, Motivo = "no hay barras en el recuadro" };
			return vacio;
		}

		var desde = indices.First();
		var hasta = indices.Last();
		var comp = Compuesta.Construir(b, desde, hasta, c.Tick, c.P.ValueAreaPct)!;
		var direccion = r.Direccion switch
		{
			DireccionManual.Largo => 1,
			DireccionManual.Corto => -1,
			_ => DireccionDe(comp)
		};

		if (direccion == 0)
		{
			var sin = new AnalisisGatillo { Resultado = ResultadoGatillo.Invalido, Motivo = "[-] sin direccionalidad clara: se espera nueva confirmación", Direccion = 0 };
			sin.Poc = comp.Poc;
			sin.Delta = comp.Delta;
			sin.VelaAnalizada = "VELA COMPUESTA";
			return sin;
		}

		var zona = new ZonaOperable { Bajo = r.Bajo, Alto = r.Alto, Signo = direccion, Tipo = "MANUAL" };
		var manual = new ContextoGatillo
		{
			Barras = b, P = c.P, Tick = c.Tick, ContratosPorBarra = c.ContratosPorBarra, Fuentes = c.Fuentes, Medianas = c.Medianas, M5 = c.M5, M15 = c.M15,
			Ema15 = c.Ema15, CvdAFavor = c.CvdAFavor, Objetivo = c.Objetivo, Ahora = c.Ahora, ExigirKillzone = false
		};

		AnalisisGatillo? volumen = null, compuesta = null;

		if (r.Tipo is TipoAnalisisManual.VelaVolumen or TipoAnalisisManual.Auto)
		{
			volumen = Analizar(zona, Math.Max(desde, hasta - 1), manual, continuidad: true);
			volumen.VelaAnalizada = "VELA DE VOLUMEN";
		}

		if (r.Tipo is TipoAnalisisManual.VelaCompuesta or TipoAnalisisManual.Auto)
		{
			compuesta = Analizar(zona, desde, manual, continuidad: true);
			compuesta.VelaAnalizada = "VELA COMPUESTA";

			// Lectura de la vela macro (anexo): POC en el tercio correcto y delta acumulado que vira a favor.
			if (comp.EnTercio(comp.Poc, -direccion))
				compuesta.Razones.Add($"POC de la compuesta en el tercio {(direccion > 0 ? "inferior" : "superior")} (subasta cerrada a favor)");

			if (comp.DeltaAcumulado.Count > 1 && direccion * comp.DeltaAcumulado[^1] > 0 && direccion * comp.DeltaAcumulado.Min(x => direccion * x) < 0)
				compuesta.Razones.Add("El delta acumulado de la compuesta vira a favor");
		}

		var elegido = (volumen, compuesta) switch
		{
			({ } v, { } k) => Rango(k) > Rango(v) || (Rango(k) == Rango(v) && k.Score >= v.Score) ? k : v,
			({ } v, null) => v,
			(null, { } k) => k,
			_ => new AnalisisGatillo()
		};

		elegido.Poc ??= comp.Poc;
		elegido.Delta ??= comp.Delta;
		return elegido;
	}

	private static int Rango(AnalisisGatillo a) => a.Resultado switch { ResultadoGatillo.Valido => 2, ResultadoGatillo.Pendiente => 1, _ => 0 };

	/// <summary>Dirección de una vela compuesta: POC abajo y delta positivo = alcista; POC arriba y delta negativo = bajista; si no, 0.</summary>
	public static int DireccionDe(VelaCompuesta comp)
	{
		if (comp.EnTercio(comp.Poc, -1) && comp.Delta > 0)
			return 1;

		if (comp.EnTercio(comp.Poc, 1) && comp.Delta < 0)
			return -1;

		return 0;
	}
}
