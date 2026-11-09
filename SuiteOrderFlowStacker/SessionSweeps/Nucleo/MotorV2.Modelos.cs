namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

public enum EstadoModelo
{
	Inactivo,
	Armado,
	GatilloPendiente,
	Entrada,
	Gestion,
	Cerrado,
	Invalidado
}

/// <summary>Un setup de un modelo (MOD1, BFVG, MOD2, MOD3, MOD4) sobre una zona, con su gatillo y su gestión simulada.</summary>
public sealed class SetupModelo
{
	public long Id;
	public string Modelo = "";

	/// <summary>Etiqueta visible: "MOD3 ROMPIMIENTO CONTINUIDAD", "MOD4-C RUPTURA-RETROCESO", "MOD2 tras MOD3"…</summary>
	public string Etiqueta = "";

	public int Signo;
	public EvaluacionBarrido? Barrido;
	public ZonaOperable Zona = null!;
	public EstadoModelo Estado = EstadoModelo.Armado;
	public string Motivo = "";
	public AnalisisGatillo? Gatillo;
	public DateTime ArmadoUtc, ExpiraUtc;
	public DateTime? EntradaUtc, CierreUtc;
	public decimal? Entrada, Stop, Tp1, Tp2;
	public PlanRiesgo? Riesgo;

	/// <summary>TP1, TP2, STOP, CANCEL o INVALIDADO.</summary>
	public string Resultado = "";

	public int MaeTicks, MfeTicks;
	public bool Tp1Tocado, PermiteContinuidad, DependeSoloDeLvn;
	public string Origen = "AUTO";

	/// <summary>Ruptura que arma un MOD4 (para el máximo de entradas por ruptura).</summary>
	public long? RupturaId;

	public bool Pendiente => Estado is EstadoModelo.Armado or EstadoModelo.GatilloPendiente;

	public bool Abierta => Estado is EstadoModelo.Entrada or EstadoModelo.Gestion;

	public string TextoEstado => Estado switch
	{
		EstadoModelo.Armado => "ARMADO",
		EstadoModelo.GatilloPendiente => "GATILLO PENDIENTE",
		EstadoModelo.Entrada => "ENTRADA",
		EstadoModelo.Gestion => "GESTIÓN",
		EstadoModelo.Cerrado => "CERRADO",
		EstadoModelo.Invalidado => "INVALIDADO",
		_ => "INACTIVO"
	};
}

/// <summary>
/// S7 ModelEngine (8): MOD1 (Modelo A), BFVG, MOD2 (Modelo B), MOD3 (ROMPIMIENTO CONTINUIDAD) y MOD4 (Modelo C
/// RUPTURA-RETROCESO), con el ConflictResolver (8.6), una posición por dirección y la gestión simulada (TP1, TP2, stop,
/// micro-impulso). Estadística simulada; no es un registro de operaciones reales.
/// </summary>
public sealed partial class MotorSweeps
{
	private readonly HashSet<string> _zonasModelo = new();
	private readonly HashSet<long> _icebergsMod3 = new();
	private long _siguienteSetup = 1;

	public List<SetupModelo> Setups { get; } = new();

	public MicroImpulso Micro { get; } = new();

	/// <summary>Saldo de la cuenta de ATAS (si ReadAtasAccount y la carcasa lo lee); null = AccountSize manual.</summary>
	public decimal? SaldoAtas { get; set; }

	/// <summary>MOD3 activo (su dirección y desde cuándo): invalida MOD1/MOD2 contrarios.</summary>
	public (int Signo, DateTime Utc, decimal Precio)? Mod3Activo { get; private set; }

	/// <summary>Solo para las pruebas: sesgo de MOD2 forzado (+1 / -1).</summary>
	internal int? SesgoForzado { get; set; }

	private SetupModelo Armar(string modelo, string etiqueta, ZonaOperable z, EvaluacionBarrido? e, DateTime utc, DateTime expira, bool continuidad)
	{
		z.Modelo = modelo;
		var s = new SetupModelo
		{
			Id = _siguienteSetup++, Modelo = modelo, Etiqueta = etiqueta, Signo = z.Signo, Barrido = e, Zona = z, ArmadoUtc = utc, ExpiraUtc = expira,
			PermiteContinuidad = continuidad, DependeSoloDeLvn = z.Tipo == "LVN", Estado = EstadoModelo.Armado
		};

		// Instrumento no soportado (2.2): sin señales automáticas; el setup queda a la vista, invalidado.
		if (Instrumento.Bloqueo is { } bloqueo)
			(s.Estado, s.Motivo) = (EstadoModelo.Invalidado, bloqueo);

		Setups.Add(s);

		if (Setups.Count > 400)
			Setups.RemoveRange(0, Setups.Count - 400);

		return s;
	}

	private void Invalidar(Func<SetupModelo, bool> condicion, string motivo, DateTime utc)
	{
		foreach (var s in Setups.Where(s => s.Pendiente && condicion(s)))
		{
			(s.Estado, s.Motivo, s.Resultado, s.CierreUtc) = (EstadoModelo.Invalidado, motivo, "INVALIDADO", utc);
			Novedad(s.Barrido, "MODELO INVALIDADO", utc, $"{s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")} INVALIDADO: {motivo}", s);
		}
	}

	// =================================================================================================================
	// ConflictResolver (8.6): lo que cada estado del clasificador arma, invalida o cancela.
	// =================================================================================================================

	internal void ModelosAlEstado(EvaluacionBarrido e, string tipo, DateTime utc)
	{
		if (tipo == "CONFIRMADO" && e.Estado.EsTrampa())
		{
			// TRAMPA: arma MOD1 (entrada inicial con ARRANQUE sobre la zona del barrido) y los BFVG; invalida MOD4 contrario.
			var z = new ZonaOperable
			{
				Id = NuevaZonaId(), Tipo = "BARRIDO", Bajo = Math.Min(e.L, e.Extremo), Alto = Math.Max(e.L, e.Extremo), Signo = e.SignoGiro,
				BarridoId = e.Id, Calidad = 10, Utc = utc, BarraToque = e.BarraT0
			};

			Armar("MOD1", "MOD1 (Modelo A)", z, e, utc, utc.AddMinutes(Math.Max(1, P2.BfvgWindowMin)), continuidad: false);
			Invalidar(s => s.Modelo == "MOD4" && s.Signo == -e.SignoGiro, $"TRAMPA {e.Nombre}", utc);
			return;
		}

		if (tipo == "CONFIRMADO" && e.Estado == EstadoBarrido.Ruptura)
		{
			// RUPTURA: anula MOD1 contrario; con iceberg ROTO manda MOD3; si no, arma MOD4 (externo, o interno si se permite).
			Invalidar(s => s.Modelo is "MOD1" or "BFVG" && s.Signo == -e.Signo, $"RUPTURA {e.Nombre}", utc);

			if (e.IcebergEstado == "ROTO")
			{
				ActivarMod3(e.Signo, e.IcebergPrecio ?? e.L, utc, e, "iceberg ROTO en la ruptura");
				ZonasPiernaMod4(e, utc);
				return;
			}

			var externo = e.Nivel.Grupo is "dia" or "semana";

			if (externo || P2.Mod4AllowInternal)
				ArmarMod4(e, utc);

			return;
		}

		switch (tipo)
		{
			case "FALLIDO":
				Invalidar(s => s.Barrido?.Id == e.Id && s.Modelo is "MOD1" or "BFVG", "FALLIDO", utc);
				break;

			case "RUPTURA FALLIDA":
				Invalidar(s => (s.Barrido?.Id == e.Id && s.Modelo is "MOD3" or "MOD4") || s.Etiqueta == "MOD2 tras MOD3" || s.RupturaId == e.Id, "RUPTURA FALLIDA", utc);

				if (Mod3Activo is { } m && m.Signo == e.Signo)
					Mod3Activo = null;

				break;
		}
	}

	/// <summary>MOD4: la ruptura con aceptación arma zonas en su pierna (FVG+LVN, LVN, FVG, retest de L), cada una con su gatillo.</summary>
	private void ArmarMod4(EvaluacionBarrido e, DateTime utc)
	{
		var espera = utc.AddMinutes(Math.Max(1, P2.Mod4MaxWaitMin));
		var s = e.Signo;
		var tick = Cfg.TamanoTick;

		Novedad(e, "RUPTURA (arma MOD4)", utc, $"RUPTURA {e.Nombre} {Precio(e.L)}: MOD4-C RUPTURA-RETROCESO armado");

		// 4) retest de L (cambio de rol).
		var retest = new ZonaOperable
		{
			Id = NuevaZonaId(), Tipo = "NIVEL", Bajo = e.L - P2.ReclaimBufferTicks * tick, Alto = e.L + P2.ReclaimBufferTicks * tick, Signo = s, BarridoId = e.Id,
			Calidad = MotorFvgV2.CalidadTipo("NIVEL"), Utc = utc
		};

		Armar("MOD4", "MOD4-C RUPTURA-RETROCESO", retest, e, utc, espera, continuidad: true).RupturaId = e.Id;
		ZonasPiernaMod4(e, utc);
	}

	/// <summary>
	/// Zonas de la pierna de ruptura (entre L y el extremo): FVG (o FVG+LVN) y LVN del Thermal. Si en la ruptura se rompió un
	/// iceberg, manda MOD3 y estas zonas se operan como "MOD2 tras MOD3" (sin duplicar MOD4).
	/// </summary>
	private void ZonasPiernaMod4(EvaluacionBarrido e, DateTime utc)
	{
		var comoMod2 = e.IcebergEstado == "ROTO";
		var (modelo, etiqueta) = comoMod2 ? ("MOD2", "MOD2 tras MOD3") : ("MOD4", "MOD4-C RUPTURA-RETROCESO");
		var extremo = e.ExtremoPierna != 0 ? e.ExtremoPierna : e.Extremo;
		var (bajo, alto) = (Math.Min(e.L, extremo), Math.Max(e.L, extremo));
		var espera = e.TimeConfirmUtc?.AddMinutes(Math.Max(1, P2.Mod4MaxWaitMin)) ?? utc.AddMinutes(P2.Mod4MaxWaitMin);

		foreach (var f in Fvg2.Fvgs.Where(f => f.Vivo && f.Signo == e.Signo && f.Utc >= e.T0Utc && f.Bajo >= bajo && f.Alto <= alto))
		{
			if (!_zonasModelo.Add($"MOD4|{e.Id}|F{f.Id}"))
				continue;

			var z = MotorFvgV2.ZonaDeFvg(NuevaZonaId(), f, Lvn, P2, Cfg.TamanoTick, bfvg: false, e.Id, modelo);
			Armar(modelo, etiqueta, z, e, utc, espera, continuidad: true).RupturaId = comoMod2 ? null : e.Id;

			// Un LVN que ya tenía su zona y queda dentro de este FVG+LVN se opera aquí (una sola zona).
			if (z.LvnId is { } lid)
				Invalidar(x => x.Barrido?.Id == e.Id && x.Zona.Tipo == "LVN" && x.Zona.LvnId == lid, "incluida en la zona FVG+LVN", utc);
		}

		// La pierna atraviesa sus LVN (quedan MITIGADOS): como zona de retroceso siguen valiendo; solo un BORRADO no.
		foreach (var l in Lvn.Todas.Where(l => l.Estado != "BORRADO" && l.Centro > bajo && l.Centro < alto))
		{
			var enFvg = Setups.Any(x => x.Barrido?.Id == e.Id && x.Zona.LvnId == l.Id && x.Zona.Tipo.EndsWith("+LVN", StringComparison.Ordinal));

			if (enFvg || !_zonasModelo.Add($"MOD4|{e.Id}|L{l.Id}"))
				continue;

			var z = new ZonaOperable
			{
				Id = NuevaZonaId(), Tipo = "LVN", Bajo = l.Bajo, Alto = l.Alto, Signo = e.Signo, BarridoId = e.Id, LvnId = l.Id, LvnOrigen = l.Origen,
				Calidad = MotorFvgV2.CalidadTipo("LVN"), Utc = utc
			};

			var armado = Armar(modelo, etiqueta, z, e, utc, espera, continuidad: true);
			armado.RupturaId = comoMod2 ? null : e.Id;
			armado.DependeSoloDeLvn = true;
		}
	}

	/// <summary>MOD3 (8.4): iceberg o super big trade ROTO por agresión con imbalance, stops y aceptación. Caja fucsia.</summary>
	private void ActivarMod3(int signo, decimal precio, DateTime utc, EvaluacionBarrido? e, string motivo)
	{
		Mod3Activo = (signo, utc, precio);
		Novedad(e, "MOD3 ACTIVADO", utc, $"MOD3 ROMPIMIENTO CONTINUIDAD {(signo > 0 ? "LARGO" : "CORTO")} en {Precio(precio)} ({motivo})");

		// Invalida MOD1 y MOD2 contrarios (o todos, si se pidió); MOD2 a favor sigue permitido.
		Invalidar(s => s.Modelo is "MOD1" or "BFVG" or "MOD2" && (P2.Mod3InvalidatesAllMod1Mod2 || s.Signo == -signo), "INVALIDADO POR MOD3", utc);

		// Una MOD4 de la misma ruptura no se duplica: manda MOD3.
		if (e is not null)
			Invalidar(s => s.Modelo == "MOD4" && s.RupturaId == e.Id, "manda MOD3 (iceberg roto)", utc);

		var tick = Cfg.TamanoTick;
		var z = new ZonaOperable { Id = NuevaZonaId(), Tipo = "ICEBERG", Bajo = precio - 2 * tick, Alto = precio + 2 * tick, Signo = signo, BarridoId = e?.Id, Calidad = 20, Utc = utc };
		Armar("MOD3", "MOD3 ROMPIMIENTO CONTINUIDAD", z, e, utc, utc.AddMinutes(Math.Max(1, P2.Mod4MaxWaitMin)), continuidad: true);
	}

	/// <summary>RangeReset del Thermal (T21): se cancelan los setups PENDIENTES que dependían solo de esas zonas LVN.</summary>
	internal void BorradasLvn(IReadOnlyList<ZonaLvn> zonas)
	{
		var ids = zonas.Select(z => z.Id).ToHashSet();
		Invalidar(s => s.DependeSoloDeLvn && s.Zona.LvnId is { } id && ids.Contains(id), "rango nuevo: LVN borrado", _ahoraV2);
	}

	// =================================================================================================================
	// Cada barra de volumen: zonas nuevas (BFVG, MOD2, MOD4), MOD3, gatillos, entradas y gestión.
	// =================================================================================================================

	internal void ModelosBarra(BarraVolumen b)
	{
		var utc = b.FinUtc;

		// Setups de los BFVG registrados (6.3).
		foreach (var z in Zonas2.Where(z => z.Modelo == "BFVG" && _zonasModelo.Add($"BFVG|{z.Id}")).ToList())
		{
			var padre = Evaluaciones.FirstOrDefault(x => x.Id == z.BarridoId);
			Armar("BFVG", z.Tipo, z, padre, utc, z.ExpiraUtc, continuidad: true);
		}

		DetectarMod3(b);
		ZonasMod2(b);

		foreach (var e in Evaluaciones.Where(e => e.Estado == EstadoBarrido.Ruptura && e.Congelada &&
			         (e.IcebergEstado == "ROTO" || e.Nivel.Grupo is "dia" or "semana" || P2.Mod4AllowInternal)))
		{
			ZonasPiernaMod4(e, utc);
		}

		var ctx = ContextoGatillo();

		foreach (var s in Setups.Where(s => s.Pendiente).ToList())
			GatilloSetup(s, b, ctx);

		foreach (var s in Setups.Where(s => s.Abierta).ToList())
			Gestionar(s, b);
	}

	/// <summary>MOD2 (8.3): a favor del sesgo (15m no en contra), retroceso a LVN (AUTO o MANUAL), FVG, EMA 15 o nivel roto.</summary>
	private void ZonasMod2(BarraVolumen b)
	{
		var h1 = MotorContextoV2.DeH1(H1.Valor).Signo();
		var d = SesgoForzado ?? (h1 != 0 ? h1 : ContextoV2.M5.Signo());

		if (d == 0 || (SesgoForzado is null && ContextoV2.M15.Signo() == -d) || BarrasV.Barras.Count < 2)
			return;

		var previa = BarrasV.Barras[^2];
		var tick = Cfg.TamanoTick;
		var candidatas = new List<(string Clave, ZonaOperable Zona, bool SoloLvn)>();

		// Un LVN ya atravesado (MITIGADO) es justo el que se retestea en un retroceso: vale; un BORRADO no.
		foreach (var l in Lvn.Todas.Where(l => l.Estado != "BORRADO"))
		{
			candidatas.Add(($"L{l.Id}", new ZonaOperable { Tipo = "LVN", Bajo = l.Bajo, Alto = l.Alto, LvnId = l.Id, LvnOrigen = l.Origen }, true));
		}

		foreach (var f in Fvg2.Fvgs.Where(f => f.Vivo && f.Signo == d))
		{
			var z = MotorFvgV2.ZonaDeFvg(0, f, Lvn, P2, tick, bfvg: false, null, "MOD2");
			candidatas.Add(($"F{f.Id}", z, false));
		}

		if (ContextoV2.Ema15.Valor is { } ema)
			candidatas.Add(($"E{Math.Round(ema / tick)}", new ZonaOperable { Tipo = "EMA", Bajo = ema - 2 * tick, Alto = ema + 2 * tick }, false));

		foreach (var n in Niveles.Niveles.Where(n => n.Estado == EstadoNivel.Roto && n.Lado.Signo() == d))
			candidatas.Add(($"N{n.Id}", new ZonaOperable { Tipo = "NIVEL", Bajo = n.Precio - 2 * tick, Alto = n.Precio + 2 * tick }, false));

		foreach (var (clave, z, soloLvn) in candidatas)
		{
			// Retroceso: el precio llega a la zona desde el lado del sesgo.
			var medio = (z.Bajo + z.Alto) / 2;

			if (!z.TocadaPor(b.High, b.Low) || d * (previa.Close - medio) <= 0 || !_zonasModelo.Add($"MOD2|{clave}|{Zonas.AEt(b.FinUtc):yyyyMMddHH}"))
				continue;

			z.Id = NuevaZonaId();
			z.Signo = d;
			z.Calidad = MotorFvgV2.CalidadTipo(z.Tipo);
			z.Utc = b.FinUtc;
			z.BarraToque = b.Indice;
			var etiqueta = Mod3Activo is { } m && m.Signo == d ? "MOD2 tras MOD3" : "MOD2 (Modelo B)";
			var s = Armar("MOD2", etiqueta, z, null, b.FinUtc, b.FinUtc.AddMinutes(30), continuidad: true);
			s.DependeSoloDeLvn = soloLvn;
		}
	}

	/// <summary>MOD3 desde la suite: un iceberg ROTO (o un super big trade) con imbalance, toma de stops y aceptación (>= 2 condiciones).</summary>
	private void DetectarMod3(BarraVolumen b)
	{
		var desde = b.FinUtc.AddSeconds(-90);
		var rotos = Fuentes.Consultar<IcebergEvent>(desde, b.FinUtc, x => x.Estado == "ROTO" && x.CambioUtc >= desde).Where(x => !_icebergsMod3.Contains(x.Id)).ToList();
		var grandes = Fuentes.Consultar<SweepTrackerEvent>(desde, b.FinUtc, x => x.Contratos >= P2.SuperBigTradeMinContracts && x.Tipo == "RUPTURA")
			.Where(x => !_icebergsMod3.Contains(x.Id)).ToList();

		var casos = rotos.Select(x => (x.Id, Signo: x.Compra ? -1 : 1, x.Precio, x.CambioUtc, Que: $"iceberg {(x.Compra ? "comprador" : "vendedor")} ROTO"))
			.Concat(grandes.Select(x => (x.Id, Signo: x.Compra ? 1 : -1, Precio: x.Desde, x.CambioUtc, Que: $"super big trade {x.Contratos:0}c")));

		foreach (var (id, d, precio, cuando, que) in casos)
		{
			var ventana = (cuando.AddSeconds(-60), b.FinUtc);
			var imbalance = Fuentes.Consultar<StackedEvent>(ventana.Item1, ventana.Item2, x => x.Compra == (d > 0)).Count > 0 ||
				Fuentes.Consultar<ImbalanceEvent>(ventana.Item1, ventana.Item2, x => x.Compra == (d > 0)).Count > 0;
			var stops = Fuentes.Consultar<StopsEvent>(ventana.Item1, ventana.Item2, x => x.Compra == (d > 0)).Count > 0;
			var aceptacion = AceptacionMas(precio, d, cuando);

			if (imbalance && stops && aceptacion >= 2)
			{
				_icebergsMod3.Add(id);
				ActivarMod3(d, precio, b.FinUtc, null, que);
			}
		}
	}

	/// <summary>Condiciones de aceptación más allá de un precio (7.5 RUPTURA) con las barras desde 'desde'.</summary>
	private int AceptacionMas(decimal precio, int d, DateTime desde)
	{
		var b = BarrasV.Barras;
		var i0 = Math.Max(0, BarrasV.BarraEn(desde));

		if (i0 >= b.Count)
			return 0;

		var n = 1; // el iceberg ROTO (o el super big trade) cuenta como "sin absorción".
		var seguidos = 0;

		for (var i = b.Count - 1; i >= i0 && d * (b[i].Close - precio) > 0; i--)
			seguidos++;

		if (seguidos >= P2.AcceptanceCloses)
			n++;

		var comp = Compuesta.Construir(b, i0, b.Count - 1, Cfg.TamanoTick, P2.ValueAreaPct);

		if (comp is not null && d * (comp.Poc - precio) > 0)
			n++;

		if (comp is not null && comp.Volumen > 0 && d * comp.Delta / comp.Volumen >= P2.DivergenceDeltaPctMin)
			n++;

		return n;
	}

	/// <summary>Toque, análisis de gatillo, condiciones del modelo y, con gatillo VÁLIDO, la entrada.</summary>
	private void GatilloSetup(SetupModelo s, BarraVolumen b, ContextoGatillo ctx)
	{
		var utc = b.FinUtc;

		if (utc > s.ExpiraUtc)
		{
			Invalidar(x => x == s, s.Modelo == "MOD4" ? "vence la espera" : "vence la ventana", utc);
			return;
		}

		if (s.Modelo is "MOD1" or "BFVG" && s.Barrido is { } t && (t.Estado == EstadoBarrido.Fallido || t.Signo * (b.Close - t.Extremo) > 0))
		{
			Invalidar(x => x == s, "cierre más allá del extremo", utc);
			return;
		}

		if (s.Modelo == "MOD4" && s.Barrido is { } r && !CondicionesMod4(s, r, b))
			return;

		// Toque de retroceso: después de armar la zona y llegando desde el lado a favor (la barra anterior cerró fuera de la
		// zona del lado de la operación). Una pierna que la atraviesa al formarse no es un retroceso.
		if (s.Zona.BarraToque is null)
		{
			var previa = b.Indice > 0 ? BarrasV.Barras[b.Indice - 1] : null;
			var bordeFavor = s.Signo > 0 ? s.Zona.Alto : s.Zona.Bajo;

			if (b.FinUtc <= s.ArmadoUtc || previa is null || s.Signo * (previa.Close - bordeFavor) <= 0 || !s.Zona.TocadaPor(b.High, b.Low))
				return;

			s.Zona.BarraToque = b.Indice;
		}

		var a = AnalizadorGatillo.Analizar(s.Zona, s.Zona.BarraToque.Value, ctx, s.PermiteContinuidad);
		s.Gatillo = a;
		s.Zona.EstadoGatillo = a.Resultado switch { ResultadoGatillo.Valido => "VÁLIDO", ResultadoGatillo.Invalido => "INVÁLIDO", _ => "PENDIENTE" };

		if (a.Resultado == ResultadoGatillo.Pendiente)
		{
			s.Estado = EstadoModelo.GatilloPendiente;
			return;
		}

		if (a.Resultado == ResultadoGatillo.Invalido)
		{
			Invalidar(x => x == s, $"gatillo inválido: {a.Motivo}", utc);
			return;
		}

		Entrar(s, a, b);
	}

	/// <summary>MOD4 (8.5): retroceso dentro de Mod4MaxWaitMin, como mucho Mod4MaxRetracePct de la pierna, sin cerrar al otro lado de L.</summary>
	private bool CondicionesMod4(SetupModelo s, EvaluacionBarrido r, BarraVolumen b)
	{
		var extremo = r.ExtremoPierna != 0 ? r.ExtremoPierna : r.Extremo;
		var pierna = Math.Abs(extremo - r.L);
		var retroceso = r.Signo * (extremo - (r.Signo > 0 ? b.Low : b.High));

		if (r.Signo * (r.L - b.Close) > P2.FailedBreakTicks * Cfg.TamanoTick)
		{
			Invalidar(x => x.RupturaId == r.Id, "cierre al otro lado de L", b.FinUtc);
			return false;
		}

		if (pierna > 0 && retroceso > pierna * P2.Mod4MaxRetracePct && s.Zona.Tipo != "NIVEL")
		{
			Invalidar(x => x == s, $"retroceso > {P2.Mod4MaxRetracePct:P0} de la pierna", b.FinUtc);
			return false;
		}

		if (Setups.Count(x => x.RupturaId == r.Id && x.EntradaUtc is not null) >= P2.Mod4MaxAttempts)
		{
			Invalidar(x => x.RupturaId == r.Id, $"máximo de {P2.Mod4MaxAttempts} entradas por ruptura", b.FinUtc);
			return false;
		}

		return true;
	}

	private void Entrar(SetupModelo s, AnalisisGatillo a, BarraVolumen b)
	{
		var utc = b.FinUtc;
		var plan = CalculadoraRiesgo.Calcular(P2, a.Entrada!.Value, a.Stop!.Value, Cfg.TamanoTick, Instrumento.ValorTick, SaldoAtas);

		// Una posición por dirección; un añadido solo si el riesgo abierto total lo permite (MaxOpenRiskPct).
		var abiertas = Setups.Where(x => x.Abierta && x.Signo == s.Signo).ToList();

		if (abiertas.Count > 0 && !CalculadoraRiesgo.PermiteAnadir(P2, abiertas.Sum(x => x.Riesgo?.RiesgoUsd ?? 0), plan.RiesgoUsd, SaldoAtas))
		{
			Invalidar(x => x == s, "ya hay una posición en esa dirección", utc);
			return;
		}

		(s.Estado, s.EntradaUtc, s.Entrada, s.Stop, s.Tp1, s.Riesgo) = (EstadoModelo.Entrada, utc, a.Entrada, a.Stop, a.Tp1, plan);
		var riesgo = Math.Abs(a.Entrada.Value - a.Stop.Value);
		s.Tp2 = Niveles.Activos.Where(n => n.Grupo is "dia" or "semana" && n.Lado.Signo() == s.Signo && s.Signo * (n.Precio - (a.Tp1 ?? a.Entrada.Value)) > 0)
			.OrderBy(n => Math.Abs(n.Precio - a.Entrada.Value)).Select(n => (decimal?)n.Precio).FirstOrDefault()
			?? a.Entrada + s.Signo * 3 * riesgo;
		s.Zona.Resultado = "ENTRADA";
		Micro.Reiniciar();
		Novedad(s.Barrido, "GATILLO VÁLIDO", utc,
			$"{s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")} | {a.Respuesta} | Entrada {Precio(a.Entrada.Value)} SL {Precio(a.Stop.Value)} TP1 {Precio(a.Tp1!.Value)} | {plan.Texto(Raiz)}", s);
	}

	/// <summary>Gestión simulada: MAE/MFE, stop, TP1 (después actúa el micro-impulso) y TP2.</summary>
	private void Gestionar(SetupModelo s, BarraVolumen b)
	{
		var tick = Cfg.TamanoTick;
		var x = s.Signo;
		var favor = (int)Math.Round(x * ((x > 0 ? b.High : b.Low) - s.Entrada!.Value) / tick);
		var contra = (int)Math.Round(x * (s.Entrada.Value - (x > 0 ? b.Low : b.High)) / tick);
		(s.MfeTicks, s.MaeTicks) = (Math.Max(s.MfeTicks, favor), Math.Max(s.MaeTicks, contra));

		// Stop primero (conservador si la barra toca los dos).
		if (x * ((x > 0 ? b.Low : b.High) - s.Stop!.Value) <= 0)
		{
			Cerrar(s, s.Tp1Tocado ? "TP1" : "STOP", b.FinUtc);
			return;
		}

		if (!s.Tp1Tocado && x * ((x > 0 ? b.High : b.Low) - s.Tp1!.Value) >= 0)
		{
			s.Tp1Tocado = true;
			s.Estado = EstadoModelo.Gestion;
			s.Stop = s.Entrada;
		}

		if (s.Tp1Tocado && s.Tp2 is { } tp2 && x * ((x > 0 ? b.High : b.Low) - tp2) >= 0)
			Cerrar(s, "TP2", b.FinUtc);
	}

	private void Cerrar(SetupModelo s, string resultado, DateTime utc)
	{
		(s.Estado, s.Resultado, s.CierreUtc) = (EstadoModelo.Cerrado, resultado, utc);
		s.Zona.Resultado = resultado;
		Novedad(s.Barrido, "MODELO CERRADO", utc, $"{s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")} CERRADO: {resultado} (MAE {s.MaeTicks} / MFE {s.MfeTicks} ticks)", s);
	}

	/// <summary>Cada vela de 1 minuto: micro-impulso de la posición abierta (tras TP1, o desde la entrada si se pidió).</summary>
	private void ModelosVela(Vela v)
	{
		// Zonas de la pierna de las rupturas: también al cierre de cada vela (un FVG nace al cerrar su tercera vela).
		foreach (var e in Evaluaciones.Where(e => e.Estado == EstadoBarrido.Ruptura && e.Congelada &&
			         (e.IcebergEstado == "ROTO" || e.Nivel.Grupo is "dia" or "semana" || P2.Mod4AllowInternal)))
		{
			ZonasPiernaMod4(e, v.FinUtc);
		}

		var abierta = Setups.LastOrDefault(s => s.Abierta);
		var anterior = Velas.Count >= 2 ? Velas[^2].Close : (decimal?)null;

		if (abierta is null || !(abierta.Tp1Tocado || P2.MicroImpulseFromEntry))
		{
			Micro.Vela(v, ContextoV2.Ema15.Valor, anterior, ContextoV2.Ema15.Anterior, 0, P2);
			return;
		}

		if (Micro.Vela(v, ContextoV2.Ema15.Valor, anterior, ContextoV2.Ema15.Anterior, abierta.Signo, P2))
			Cerrar(abierta, abierta.Tp1Tocado ? "TP1" : "CANCEL", v.FinUtc);
	}
}
