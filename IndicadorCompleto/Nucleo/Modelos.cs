namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Plan de riesgo de una señal: SL, TP, cantidad de micros y RR neto, o el motivo de bloqueo.</summary>
internal sealed class PlanRiesgo
{
	public Direccion Direccion;
	public decimal Entrada, Sl, Tp;
	public string AnclajeSl = "";
	public int BufferTicks;
	public int DistanciaSlTicks;
	public ZonaLvn? Objetivo;
	public int AnticipacionTicks;
	public decimal ValorTick, CostesPorContrato, RiesgoPorContrato, BeneficioPorContrato;
	public int Cantidad;
	public decimal RiesgoTotal, RrNeto;
	public string? Bloqueo;

	public bool Admitido => Bloqueo is null;

	public static PlanRiesgo Calcular(Direccion d, decimal entrada, decimal sl, string anclaje, ZonaLvn? objetivo, ConfiguracionCompleto cfg)
	{
		var tick = cfg.TamanoTick;
		var plan = new PlanRiesgo
		{
			Direccion = d,
			Entrada = entrada,
			Sl = sl,
			AnclajeSl = anclaje,
			BufferTicks = cfg.BufferSlTicks,
			Objetivo = objetivo,
			AnticipacionTicks = cfg.AnticipacionTpTicks,
			ValorTick = cfg.ValorTick,
			CostesPorContrato = cfg.CostesPorContrato
		};

		if (objetivo is not null)
		{
			plan.Tp = d == Direccion.Larga
				? objetivo.Inferior - cfg.Ticks(cfg.AnticipacionTpTicks)
				: objetivo.Superior + cfg.Ticks(cfg.AnticipacionTpTicks);
		}

		plan.DistanciaSlTicks = (int)Math.Ceiling(Math.Abs(entrada - sl) / tick);
		plan.RiesgoPorContrato = plan.DistanciaSlTicks * cfg.ValorTick + cfg.CostesPorContrato;

		// Redondeo hacia abajo; el SL nunca se acerca para aumentar contratos.
		plan.Cantidad = plan.RiesgoPorContrato > 0 ? (int)Math.Floor(cfg.RiesgoUsd / plan.RiesgoPorContrato) : 0;
		plan.Cantidad = Math.Min(plan.Cantidad, cfg.MaximoContratos);
		plan.RiesgoTotal = plan.Cantidad * plan.RiesgoPorContrato;

		if (objetivo is not null)
		{
			plan.BeneficioPorContrato = Math.Floor(Math.Abs(plan.Tp - entrada) / tick) * cfg.ValorTick - cfg.CostesPorContrato;
			plan.RrNeto = plan.RiesgoPorContrato > 0 ? plan.BeneficioPorContrato / plan.RiesgoPorContrato : 0;
		}

		var tpDelLadoCorrecto = d == Direccion.Larga ? plan.Tp > entrada : plan.Tp < entrada;

		plan.Bloqueo =
			!cfg.ValoresMonetariosVerificados ? "VALORES MONETARIOS NO VERIFICADOS (ajuste de riesgo)"
			: !cfg.InstrumentoPermitido() ? $"INSTRUMENTO NO PERMITIDO ({cfg.Instrumento})"
			: objetivo is null ? "SIN OBJETIVO LVN AMARILLO DISPONIBLE"
			: (d == Direccion.Larga ? sl >= entrada : sl <= entrada) ? "SL DEL LADO INCORRECTO"
			: !tpDelLadoCorrecto ? "OBJETIVO LVN DEMASIADO CERCA DE LA ENTRADA"
			: plan.Cantidad == 0 ? "SEÑAL BLOQUEADA POR RIESGO (cantidad 0)"
			: plan.RrNeto < cfg.RrNetoMinimo ? $"RR NETO {plan.RrNeto:0.00} MENOR QUE EL MÍNIMO {cfg.RrNetoMinimo:0.00}"
			: null;

		return plan;
	}
}

/// <summary>Señal admitida: conserva su precio y su hora de confirmación originales.</summary>
internal sealed class Senal
{
	public int Id;
	public string Modelo = "";
	public string Variante = "";
	public Direccion Direccion;
	public TipoLiquidez Tipo;
	public int EventoId;
	public string Referencia = "";
	public Vela VelaGatillo = null!;
	public PlanRiesgo Plan = null!;
	public ZonaLvn? ZonaTrabajo;
	public Fvg? Fvg;
	public Simulacion? Resultado;

	public DateTime ConfirmadaUtc => VelaGatillo.FinUtc;

	public string Titulo => $"{Modelo} {Direccion.Texto()} {Tipo.Texto()}";
}

/// <summary>Patrón completo pero no admitido por el control de riesgo: se registra y se dibuja en gris.</summary>
internal sealed record SetupBloqueado(int EventoId, string Modelo, Direccion Direccion, TipoLiquidez Tipo, Vela Vela, string Motivo);

/// <summary>Secuencia de un evento de barrido con sus dos modelos.</summary>
internal sealed class Oportunidad
{
	public EventoBarrido E = null!;
	public EstadoModelo M1 = EstadoModelo.NoActivo, M2 = EstadoModelo.NoActivo;
	public string MotivoM1 = "", MotivoM2 = "";
	public string GatilloM1Detalle = "", GatilloM2Detalle = "";
	public bool? GatilloM1, GatilloM2;
	public PlanRiesgo? PlanM1, PlanM2;
	public Directriz? DirectrizM1, DirectrizM2;
	public Senal? SenalM1, SenalM2;
	public ZonaLvn? ZonaM2;
	public Vela? VelaRuptura;

	/// <summary>Última vela evaluada como gatillo (para mostrar su footprint en el reporte).</summary>
	public Vela? VelaExaminada;
	public decimal? ExtremoRetroceso;
	public bool Retroceso, Respuesta;
	public Fvg? FvgM2;
	public string VarianteM2 = "";
	public DateTime? VenceM2Utc;
	public bool BloqueoRegistrado;

	public static bool Terminal(EstadoModelo m) =>
		m is EstadoModelo.Confirmado or EstadoModelo.Invalidado or EstadoModelo.Vencido or EstadoModelo.Consumido;

	public bool Viva => E.Estado != EstadoBarrido.Bloqueado && (!Terminal(M1) || !Terminal(M2));
}

internal static class Gatillos
{
	/// <summary>Vela direccional con POC y centro del área de valor en la parte de la vela que indica el prompt.</summary>
	public static (bool Ok, string Detalle) Evaluar(Vela vela, Direccion d, ConfiguracionCompleto cfg)
	{
		if (vela.Huella is not { } h)
			return (false, "SIN FOOTPRINT: NO EVALUABLE");

		var direccional = d == Direccion.Larga ? vela.Alcista : vela.Bajista;
		var limite = cfg.PosicionGatillo;
		var poc = d == Direccion.Larga ? h.PosPoc <= limite : h.PosPoc >= 1 - limite;
		var va = d == Direccion.Larga ? h.PosVa <= limite : h.PosVa >= 1 - limite;

		var detalle = $"{(direccional ? "vela direccional" : "vela NO direccional")} · POC rel. {h.PosPoc:0.00} {(poc ? "ok" : "no")} · "
			+ $"centro VA rel. {h.PosVa:0.00} {(va ? "ok" : "no")}";

		return (direccional && poc && va, detalle);
	}
}

public sealed partial class MotorCompleto
{
	private static readonly TimeSpan SupresionSenales = TimeSpan.FromMinutes(5);

	private int _siguienteEvento = 1;
	private int _siguienteSenal = 1;

	private bool Compatible(Direccion d) =>
		d == Direccion.Larga ? Estructura.Sesgo == Sesgo.Alcista : Estructura.Sesgo == Sesgo.Bajista;

	private bool EnVentanaAdmitida(Vela vela) =>
		(Cfg.VentanaTokio && Calendario.Ventana(TipoSesion.Tokio, vela.InicioUtc) is not null)
		|| (Cfg.VentanaLondres && Calendario.Ventana(TipoSesion.Londres, vela.InicioUtc) is not null)
		|| (Cfg.VentanaNuevaYork && Calendario.Ventana(TipoSesion.NuevaYork, vela.InicioUtc) is not null);

	private decimal Redondear(decimal precio) => Math.Round(precio / Cfg.TamanoTick, MidpointRounding.AwayFromZero) * Cfg.TamanoTick;

	private decimal MediaVolumen(int indice, int velas = 20)
	{
		var desde = Math.Max(0, indice - velas);
		return indice > desde ? Velas1M.Skip(desde).Take(indice - desde).Average(v => v.Volumen) : 0;
	}

	private decimal MediaRango(int indice, int velas = 20)
	{
		var desde = Math.Max(0, indice - velas);
		return indice > desde ? Velas1M.Skip(desde).Take(indice - desde).Average(v => v.Rango) : 0;
	}

	/// <summary>Barridos y modelos con la vela de 1M cerrada y, si acaba de cerrar, la de 5M.</summary>
	private void ProcesarBarridos(Vela vela, Vela? vela5M)
	{
		foreach (var op in Oportunidades.Where(o => o.Viva).ToList())
			ActualizarCon1M(op, vela);

		DetectarBarridos(vela);

		if (vela5M is not null)
		{
			foreach (var op in Oportunidades.Where(o => o.Viva).ToList())
				ActualizarCon5M(op, vela5M);
		}

		RevisarVencimientos(vela.FinUtc);

		if (Oportunidades.Count > 400)
			Oportunidades.RemoveAll(o => !o.Viva && o.E.ConocidoUtc < vela.FinUtc.AddDays(-3));
	}

	private void DetectarBarridos(Vela vela)
	{
		var anterior = vela.Indice > 0 ? Velas1M[vela.Indice - 1] : null;
		var penetracion = Cfg.Ticks(Cfg.PenetracionMinimaTicks);
		var minimos = new List<ReferenciaLiquidez>();
		var maximos = new List<ReferenciaLiquidez>();

		foreach (var r in Liquidez.Activas(vela.InicioUtc).ToList())
		{
			// Cruce nuevo: la vela anterior no había pasado el nivel (el precio venía del otro lado).
			var cruceNuevo = anterior is null || (r.EsMaximo ? anterior.Maximo <= r.Precio : anterior.Minimo >= r.Precio);
			var penetrada = r.EsMaximo ? vela.Maximo >= r.Precio + penetracion : vela.Minimo <= r.Precio - penetracion;
			var cierreMasAlla = r.EsMaximo ? vela.Cierre > r.Precio : vela.Cierre < r.Precio;

			if (penetrada && cruceNuevo)
				(r.EsMaximo ? maximos : minimos).Add(r);
			else if (penetrada)
				Terminar(r, EstadoReferencia.Rota, vela, "superada sin cruce nuevo (ya estaba cruzada)");
			else if (cierreMasAlla)
				Terminar(r, EstadoReferencia.Rota, vela, "cierre más allá sin la penetración mínima");
		}

		if (minimos.Count > 0 && maximos.Count > 0)
		{
			CrearEvento(Direccion.Larga, minimos, vela, "BARRIDO DOBLE — SECUENCIA AMBIGUA");
			CrearEvento(Direccion.Corta, maximos, vela, "BARRIDO DOBLE — SECUENCIA AMBIGUA");
		}
		else if (minimos.Count > 0)
		{
			CrearEvento(Direccion.Larga, minimos, vela, null);
		}
		else if (maximos.Count > 0)
		{
			CrearEvento(Direccion.Corta, maximos, vela, null);
		}
	}

	private static void Terminar(ReferenciaLiquidez r, EstadoReferencia estado, Vela vela, string motivo)
	{
		r.Estado = estado;
		r.FinUtc = vela.FinUtc;
		r.MotivoFin = motivo;
	}

	private void CrearEvento(Direccion d, List<ReferenciaLiquidez> referencias, Vela vela, string? bloqueo)
	{
		// Principal: la más importante (externa antes que interna) y, entre iguales, la más extrema.
		var principal = referencias.OrderByDescending(r => r.Importancia).ThenBy(r => d == Direccion.Larga ? r.Precio : -r.Precio).First();

		foreach (var r in referencias)
			Terminar(r, EstadoReferencia.Barrida, vela, $"barrida por el evento {_siguienteEvento}");

		var e = new EventoBarrido
		{
			Id = _siguienteEvento++,
			Direccion = d,
			Referencias = referencias,
			Principal = principal,
			VelaBarrido = vela,
			Extremo = vela.ExtremoContrario(d),
			PenetracionTicks = (int)Math.Round(Math.Abs(principal.Precio - vela.ExtremoContrario(d)) / Cfg.TamanoTick),
			VenceRecuperacionUtc = vela.FinUtc.AddMinutes(Cfg.PlazoRecuperacionMinutos)
		};

		bloqueo ??= !Compatible(d)
			? Estructura.Sesgo == Sesgo.NoDeterminado
				? "CONTEXTO 1H NO DETERMINADO — ESPERAR"
				: $"CONTEXTO 1H {Estructura.Sesgo.Texto()}: NO ES UN BARRIDO CONTRARIO AL CONTEXTO"
			: !EnVentanaAdmitida(vela) ? "FUERA DE LAS VENTANAS ADMITIDAS"
			: !vela.FootprintCompleto ? "FOOTPRINT INCOMPLETO EN LA VELA DEL BARRIDO"
			: null;

		e.Estado = bloqueo is null ? EstadoBarrido.EnEvaluacion : EstadoBarrido.Bloqueado;
		e.Motivo = bloqueo ?? "EN EVALUACIÓN: ESPERANDO RECUPERACIÓN";

		// Absorción en la vela del barrido; el cambio de delta se comprueba en las velas siguientes.
		var media = MediaVolumen(vela.Indice);
		e.Absorcion.VolumenClimatico = media > 0 && vela.Volumen >= media * Cfg.MultiplicadorVolumenClimatico;
		e.Absorcion.AgresionContraria = vela.Volumen > 0 && -d.Signo() * vela.Delta >= vela.Volumen * Cfg.AgresionPct / 100m;
		e.Absorcion.EscasoAvance = e.PenetracionTicks <= Cfg.AvanceMaximoTicks;

		if (Cfg.VelasCambioDelta <= 0)
			e.Absorcion.CambioDelta = false;

		// Rechazo fuerte (para M2): la vela del barrido cierra de vuelta y en su tercio favorable.
		e.RechazoFuerte = vela.Rango > 0 && (d == Direccion.Larga
			? vela.Cierre >= e.Nivel && (vela.Cierre - vela.Minimo) >= vela.Rango * 0.67m
			: vela.Cierre <= e.Nivel && (vela.Maximo - vela.Cierre) >= vela.Rango * 0.67m);

		Barridos.Add(e);

		if (Barridos.Count > 600)
			Barridos.RemoveAt(0);

		// Aviso previo solo para los barridos que pueden acabar en señal (no los bloqueados desde el inicio).
		if (e.Estado == EstadoBarrido.EnEvaluacion)
		{
			Avisar(TipoAviso.Barrido, e.ConocidoUtc,
				$"BARRIDO B{e.Id} DE {(d == Direccion.Larga ? "MÍNIMOS" : "MÁXIMOS")} {e.Tipo.Texto()} en {Precio(e.Nivel)} ({e.Principal.Etiqueta}): posible {d.Texto()}, esperar recuperación y gatillo");
		}

		var op = new Oportunidad { E = e };

		if (!Cfg.ActivarM1)
			(op.M1, op.MotivoM1) = (EstadoModelo.Consumido, "M1 DESACTIVADO");

		if (!Cfg.ActivarM2)
			(op.M2, op.MotivoM2) = (EstadoModelo.Consumido, "M2 DESACTIVADO");

		if (e.Estado == EstadoBarrido.Bloqueado)
		{
			op.MotivoM1 = op.MotivoM2 = e.Motivo;

			if (!Terminal(op.M1))
				op.M1 = EstadoModelo.Bloqueado;

			if (!Terminal(op.M2))
				op.M2 = EstadoModelo.Bloqueado;
		}

		Oportunidades.Add(op);

		if (e.Estado != EstadoBarrido.EnEvaluacion)
			return;

		// La propia vela del barrido puede recuperar (1M) y servir de gatillo; también iniciar M2 por rechazo fuerte.
		RevisarRecuperacion1M(op, vela);
		EvaluarM2(op, vela);
	}

	private static bool Terminal(EstadoModelo m) => Oportunidad.Terminal(m);

	private void ActualizarCon1M(Oportunidad op, Vela vela)
	{
		var e = op.E;

		if (vela.Indice <= e.VelaBarrido.Indice)
			return;

		if (!Compatible(e.Direccion))
		{
			Cerrar(op, EstadoModelo.Invalidado, $"CAMBIO DE CONTEXTO 1H ({Estructura.Sesgo.Texto()})", EstadoBarrido.Invalidado);
			return;
		}

		ActualizarAbsorcion(e, vela);

		var signo = e.Direccion.Signo();
		var cierreMasAllaDelExtremo = signo * (vela.Cierre - e.Extremo) < 0;

		if (e.Estado == EstadoBarrido.Recuperado && cierreMasAllaDelExtremo)
		{
			Cerrar(op, EstadoModelo.Invalidado, "CIERRE MÁS ALLÁ DEL EXTREMO DEL BARRIDO", EstadoBarrido.Invalidado);
			return;
		}

		// El extremo del rechazo se actualiza mientras se forma (anclaje del SL de M1).
		e.Extremo = e.Direccion == Direccion.Larga ? Math.Min(e.Extremo, vela.Minimo) : Math.Max(e.Extremo, vela.Maximo);

		if (e.Estado == EstadoBarrido.EnEvaluacion)
			RevisarRecuperacion1M(op, vela);
		else if (e.Estado == EstadoBarrido.Recuperado)
			EvaluarM1(op, vela);

		EvaluarM2(op, vela);
	}

	private void ActualizarCon5M(Oportunidad op, Vela vela5M)
	{
		var e = op.E;

		if (e.Estado != EstadoBarrido.EnEvaluacion || vela5M.FinUtc < e.ConocidoUtc || vela5M.FinUtc > e.VenceRecuperacionUtc)
			return;

		var margen = Cfg.Ticks(Cfg.RecuperacionTicks5M);

		if (e.Recuperacion5MUtc is null && e.Direccion.Signo() * (vela5M.Cierre - e.Nivel) >= margen)
		{
			e.Recuperacion5MUtc = vela5M.FinUtc;

			if (RecuperacionCompleta(e))
				MarcarRecuperado(op, vela5M);
		}
	}

	private void RevisarRecuperacion1M(Oportunidad op, Vela vela)
	{
		var e = op.E;
		var margen = Cfg.Ticks(Cfg.RecuperacionTicks1M);

		if (e.Recuperacion1MUtc is null && vela.FinUtc <= e.VenceRecuperacionUtc && e.Direccion.Signo() * (vela.Cierre - e.Nivel) >= margen)
		{
			e.Recuperacion1MUtc = vela.FinUtc;

			if (RecuperacionCompleta(e))
				MarcarRecuperado(op, vela);
		}
	}

	private bool RecuperacionCompleta(EventoBarrido e) => Cfg.ReglaRecuperacion switch
	{
		ReglaRecuperacion.Solo1M => e.Recuperacion1MUtc is not null,
		ReglaRecuperacion.Solo5M => e.Recuperacion5MUtc is not null,
		ReglaRecuperacion.Ambas => e.Recuperacion1MUtc is not null && e.Recuperacion5MUtc is not null,
		_ => e.Recuperacion1MUtc is not null || e.Recuperacion5MUtc is not null
	};

	private void MarcarRecuperado(Oportunidad op, Vela vela)
	{
		var e = op.E;
		e.Estado = EstadoBarrido.Recuperado;
		e.RecuperadoUtc = vela.FinUtc;
		e.VelaRecuperacion = vela;
		e.VenceOportunidadUtc = vela.FinUtc.AddMinutes(Cfg.VigenciaOportunidadMinutos);
		e.Motivo = $"RECUPERACIÓN CONFIRMADA EN {vela.Marco.Texto()}";

		// La vela de recuperación puede actuar como gatillo de M1.
		EvaluarM1(op, vela);
	}

	private void ActualizarAbsorcion(EventoBarrido e, Vela vela)
	{
		var a = e.Absorcion;

		if (a.CambioDelta is not null)
			return;

		a.VelasRevisadas++;

		if (vela.Volumen > 0 && e.Direccion.Signo() * vela.Delta >= vela.Volumen * Cfg.AgresionPct / 100m)
			a.CambioDelta = true;
		else if (a.VelasRevisadas >= Cfg.VelasCambioDelta)
			a.CambioDelta = false;
	}

	/// <summary>Absorción obligatoria: null mientras se evalúa, false si no se confirmó.</summary>
	private bool? AbsorcionAdmite(EventoBarrido e) =>
		Cfg.ModoAbsorcion != ModoFiltro.Obligatorio ? true
		: e.Absorcion.Pendiente ? null
		: e.Absorcion.Confirmada;

	private (Directriz? Linea, bool Rota) RevisarDirectriz(Direccion d, DateTime desdeUtc, Vela hasta)
	{
		var linea = Directriz.Construir(d, Pivotes1M, desdeUtc, hasta.FinUtc);

		if (linea is null)
			return (null, false);

		var primera = Velas1M.FindIndex(v => v.InicioUtc >= linea.DisponibleUtc);

		if (primera >= 0)
		{
			for (var i = primera; i < Velas1M.Count && Velas1M[i].FinUtc <= hasta.FinUtc; i++)
				linea.Revisar(Velas1M[i]);
		}

		return (linea, linea.RotaUtc is not null);
	}

	private void EvaluarM1(Oportunidad op, Vela vela)
	{
		var e = op.E;

		if (Terminal(op.M1) || e.Estado != EstadoBarrido.Recuperado || vela.FinUtc > e.VenceOportunidadUtc)
			return;

		if (op.M1 == EstadoModelo.NoActivo)
			op.M1 = EstadoModelo.EnFormacion;

		switch (AbsorcionAdmite(e))
		{
			case null:
				op.MotivoM1 = "ESPERANDO CONFIRMACIÓN DE ABSORCIÓN (OBLIGATORIA)";
				return;
			case false:
				(op.M1, op.MotivoM1) = (EstadoModelo.Invalidado, "ABSORCIÓN NO CONFIRMADA (OBLIGATORIA)");
				return;
		}

		op.VelaExaminada = vela;
		var (ok, detalle) = Gatillos.Evaluar(vela, e.Direccion, Cfg);
		op.GatilloM1 = ok;
		op.GatilloM1Detalle = $"{vela.Marco.Texto()} {vela.InicioUtc:HH:mm} UTC: {detalle}";

		if (!ok)
		{
			op.M1 = EstadoModelo.EnFormacion;
			op.MotivoM1 = "ESPERANDO GATILLO (vela direccional y footprint)";
			return;
		}

		var (linea, rota) = RevisarDirectriz(e.Direccion, e.VelaBarrido.InicioUtc.AddMinutes(-60), vela);
		op.DirectrizM1 = linea;

		if (Cfg.ModoDirectriz == ModoFiltro.Obligatorio && !rota)
		{
			op.MotivoM1 = linea is null ? "DIRECTRIZ NO DISPONIBLE (OBLIGATORIA)" : "ESPERANDO RUPTURA DE LA DIRECTRIZ (OBLIGATORIA)";
			return;
		}

		var sl = Redondear(e.Extremo - e.Direccion.Signo() * Cfg.Ticks(Cfg.BufferSlTicks));
		var plan = PlanRiesgo.Calcular(e.Direccion, vela.Cierre, sl, $"extremo del rechazo {(e.Direccion == Direccion.Larga ? "−" : "+")} {Cfg.BufferSlTicks} ticks",
			Lvn.SiguientePrioritario(e.Direccion, vela.Cierre, vela.FinUtc), Cfg);

		op.PlanM1 = plan;
		Admitir(op, "M1", "M1", vela, plan, null, null);
	}

	private void EvaluarM2(Oportunidad op, Vela vela)
	{
		var e = op.E;

		if (Terminal(op.M2) || e.Estado == EstadoBarrido.Bloqueado)
			return;

		var iniciado = e.Estado == EstadoBarrido.Recuperado || e.RechazoFuerte;

		if (!iniciado)
			return;

		if (op.M2 == EstadoModelo.NoActivo)
		{
			op.M2 = EstadoModelo.EnFormacion;
			op.VenceM2Utc = (e.RecuperadoUtc ?? e.ConocidoUtc).AddMinutes(Cfg.VigenciaOportunidadMinutos);
			op.MotivoM2 = "ESPERANDO RUPTURA DE LVN";
		}

		if (vela.FinUtc > op.VenceM2Utc)
			return;

		var d = e.Direccion;
		var signo = d.Signo();
		var margen = Cfg.Ticks(Cfg.MargenRupturaLvnTicks);

		if (op.ZonaM2 is null)
		{
			if (vela.Indice <= e.VelaBarrido.Indice || vela.Indice == 0)
				return;

			var mapa = Lvn.MapaEn(vela.InicioUtc);

			if (mapa is null)
			{
				op.MotivoM2 = "SIN MAPA LVN DISPONIBLE";
				return;
			}

			var anterior = Velas1M[vela.Indice - 1];
			var desplazamiento = (d == Direccion.Larga ? vela.Alcista : vela.Bajista)
				&& vela.Cuerpo >= Cfg.Ticks(Cfg.CuerpoMinimoRupturaTicks)
				&& vela.Rango >= MediaRango(vela.Indice) * Cfg.ExpansionMinimaRuptura;

			var zona = mapa.Visibles(false)
				.Where(z => d == Direccion.Larga
					? anterior.Cierre <= z.Superior && vela.Cierre > z.Superior + margen && z.Inferior > e.Extremo
					: anterior.Cierre >= z.Inferior && vela.Cierre < z.Inferior - margen && z.Superior < e.Extremo)
				.OrderBy(z => signo * z.Centro)
				.FirstOrDefault();

			if (zona is null || !desplazamiento)
			{
				op.MotivoM2 = zona is null ? "ESPERANDO RUPTURA DE LVN" : "CRUCE DE LVN SIN DESPLAZAMIENTO SUFICIENTE";
				return;
			}

			(op.ZonaM2, op.VelaRuptura) = (zona, vela);
			op.MotivoM2 = $"RUPTURA DEL LVN {zona.Id} CONFIRMADA: ESPERANDO RETROCESO";
			return;
		}

		if (vela.Indice <= op.VelaRuptura!.Indice)
			return;

		var z = op.ZonaM2;

		if (d == Direccion.Larga ? vela.Cierre < z.Inferior - margen : vela.Cierre > z.Superior + margen)
		{
			(op.M2, op.MotivoM2) = (EstadoModelo.Invalidado, $"RUPTURA FALLIDA: CIERRE AL OTRO LADO DEL LVN {z.Id}");
			return;
		}

		if (vela.Toca(z.Inferior, z.Superior))
			op.Retroceso = true;

		if (!op.Retroceso)
		{
			op.MotivoM2 = $"ESPERANDO RETROCESO AL LVN {z.Id}";
			return;
		}

		op.ExtremoRetroceso = op.ExtremoRetroceso is { } previo
			? (d == Direccion.Larga ? Math.Min(previo, vela.Minimo) : Math.Max(previo, vela.Maximo))
			: vela.ExtremoContrario(d);

		if (d == Direccion.Larga ? vela.Cierre <= z.Superior : vela.Cierre >= z.Inferior)
		{
			op.MotivoM2 = "RETROCESO EN CURSO: FALTA CIERRE DE RESPUESTA";
			return;
		}

		op.Respuesta = true;

		// FVG: se vuelve a comprobar su vigencia aquí, justo antes de emitir; el retroceso tiene que haberlo tocado.
		var fvg = FvgDetector.Cercano(d, z.Inferior, z.Superior, e.ConocidoUtc, vela.FinUtc);

		if (fvg is not null && signo * (op.ExtremoRetroceso!.Value - (d == Direccion.Larga ? fvg.Superior : fvg.Inferior)) > 0)
			fvg = null;

		if (Cfg.ExigirFvg && fvg is null)
		{
			op.MotivoM2 = "FVG OBLIGATORIO AUSENTE O SIN CONTACTO";
			return;
		}

		op.FvgM2 = fvg;
		op.VarianteM2 = fvg is null ? "M2 LVN SIN FVG" : "M2 LVN + FVG";

		op.VelaExaminada = vela;
		var (ok, detalle) = Gatillos.Evaluar(vela, d, Cfg);
		op.GatilloM2 = ok;
		op.GatilloM2Detalle = $"{vela.Marco.Texto()} {vela.InicioUtc:HH:mm} UTC: {detalle}";

		if (!ok)
		{
			op.MotivoM2 = "RESPUESTA SIN GATILLO FOOTPRINT";
			return;
		}

		switch (AbsorcionAdmite(e))
		{
			case null:
				op.MotivoM2 = "ESPERANDO CONFIRMACIÓN DE ABSORCIÓN (OBLIGATORIA)";
				return;
			case false:
				(op.M2, op.MotivoM2) = (EstadoModelo.Invalidado, "ABSORCIÓN NO CONFIRMADA (OBLIGATORIA)");
				return;
		}

		var (linea, rota) = RevisarDirectriz(d, op.VelaRuptura.InicioUtc, vela);
		op.DirectrizM2 = linea;

		if (Cfg.ModoDirectriz == ModoFiltro.Obligatorio && !rota)
		{
			op.MotivoM2 = linea is null ? "DIRECTRIZ NO DISPONIBLE (OBLIGATORIA)" : "ESPERANDO RUPTURA DE LA DIRECTRIZ (OBLIGATORIA)";
			return;
		}

		var (ancla, texto) = fvg is null || Cfg.AnclajeSlFvg == AnclajeSlFvg.Rechazo
			? (op.ExtremoRetroceso!.Value, "extremo del rechazo en el LVN")
			: Cfg.AnclajeSlFvg switch
			{
				AnclajeSlFvg.BordeFvg => (d == Direccion.Larga ? fvg.Inferior : fvg.Superior, $"borde del FVG {fvg.Id}"),
				AnclajeSlFvg.C2 => (fvg.C2.ExtremoContrario(d), $"vela C2 del FVG {fvg.Id}"),
				_ => (fvg.C1.ExtremoContrario(d), $"vela C1 del FVG {fvg.Id}")
			};

		var sl = Redondear(ancla - signo * Cfg.Ticks(Cfg.BufferSlTicks));
		var plan = PlanRiesgo.Calcular(d, vela.Cierre, sl, $"{texto} {(d == Direccion.Larga ? "−" : "+")} {Cfg.BufferSlTicks} ticks",
			Lvn.SiguientePrioritario(d, vela.Cierre, vela.FinUtc), Cfg);

		op.PlanM2 = plan;
		Admitir(op, "M2", op.VarianteM2, vela, plan, z, fvg);
	}

	/// <summary>Emite la señal si el riesgo la admite; si no, deja el modelo bloqueado con el motivo.</summary>
	private void Admitir(Oportunidad op, string modelo, string variante, Vela vela, PlanRiesgo plan, ZonaLvn? zona, Fvg? fvg)
	{
		var esM1 = modelo == "M1";
		var d = op.E.Direccion;

		var motivo = plan.Bloqueo
			?? (Senales.Any(s => s.Direccion == d && vela.FinUtc - s.ConfirmadaUtc < SupresionSenales)
				? "SUPRIMIDA: SEÑAL RECIENTE EN LA MISMA DIRECCIÓN"
				: null);

		if (motivo is not null)
		{
			if (esM1)
				(op.M1, op.MotivoM1) = (EstadoModelo.Bloqueado, motivo);
			else
				(op.M2, op.MotivoM2) = (EstadoModelo.Bloqueado, motivo);

			if (!op.BloqueoRegistrado)
			{
				op.BloqueoRegistrado = true;
				SetupsBloqueados.Add(new SetupBloqueado(op.E.Id, modelo, d, op.E.Tipo, vela, motivo));

				if (SetupsBloqueados.Count > 300)
					SetupsBloqueados.RemoveAt(0);

				// La supresión por una señal reciente no es un rechazo del riesgo: no suena.
				if (plan.Bloqueo is not null)
					Avisar(TipoAviso.Bloqueado, vela.FinUtc, $"{modelo} {d.Texto()} {op.E.Tipo.Texto()} BLOQUEADA: {motivo}");
			}

			return;
		}

		var senal = new Senal
		{
			Id = _siguienteSenal++,
			Modelo = modelo,
			Variante = variante,
			Direccion = d,
			Tipo = op.E.Tipo,
			EventoId = op.E.Id,
			Referencia = op.E.Principal.Etiqueta,
			VelaGatillo = vela,
			Plan = plan,
			ZonaTrabajo = zona,
			Fvg = fvg
		};

		Senales.Add(senal);
		Avisar(TipoAviso.Senal, senal.ConfirmadaUtc,
			$"SEÑAL {senal.Titulo} · entrada {Precio(plan.Entrada)} · SL {Precio(plan.Sl)} · TP {Precio(plan.Tp)} · {plan.Cantidad} micros · RR {plan.RrNeto:0.0}");

		if (esM1)
			(op.M1, op.MotivoM1, op.SenalM1) = (EstadoModelo.Confirmado, $"SEÑAL {senal.Id} CONFIRMADA", senal);
		else
			(op.M2, op.MotivoM2, op.SenalM2) = (EstadoModelo.Confirmado, $"SEÑAL {senal.Id} CONFIRMADA", senal);

		// Sin modelos independientes, el evento se consume con la primera señal.
		if (!Cfg.ModelosIndependientes)
		{
			if (!Terminal(op.M1))
				(op.M1, op.MotivoM1) = (EstadoModelo.Consumido, $"EVENTO CONSUMIDO POR LA SEÑAL {senal.Id}");

			if (!Terminal(op.M2))
				(op.M2, op.MotivoM2) = (EstadoModelo.Consumido, $"EVENTO CONSUMIDO POR LA SEÑAL {senal.Id}");
		}

		if (Terminal(op.M1) && Terminal(op.M2))
		{
			op.E.Estado = EstadoBarrido.Consumido;
			op.E.Motivo = $"CONSUMIDO POR LA SEÑAL {senal.Id}";
		}

		if (Cfg.SimularResultados)
			Simulacion.Abrir(senal);
	}

	private void Cerrar(Oportunidad op, EstadoModelo estado, string motivo, EstadoBarrido estadoEvento)
	{
		if (!Terminal(op.M1))
			(op.M1, op.MotivoM1) = (estado, motivo);

		if (!Terminal(op.M2))
			(op.M2, op.MotivoM2) = (estado, motivo);

		op.E.Estado = estadoEvento;
		op.E.Motivo = motivo;
	}

	private void RevisarVencimientos(DateTime ahoraUtc)
	{
		foreach (var op in Oportunidades.Where(o => o.Viva))
		{
			var e = op.E;

			if (e.Estado == EstadoBarrido.EnEvaluacion && ahoraUtc >= e.VenceRecuperacionUtc)
			{
				e.Estado = EstadoBarrido.Vencido;
				e.Motivo = $"SIN RECUPERACIÓN EN {Cfg.PlazoRecuperacionMinutos} MIN";

				if (!Terminal(op.M1))
					(op.M1, op.MotivoM1) = (EstadoModelo.Vencido, e.Motivo);

				// M2 puede seguir si hubo rechazo fuerte en la vela del barrido.
				if (!Terminal(op.M2) && !e.RechazoFuerte)
					(op.M2, op.MotivoM2) = (EstadoModelo.Vencido, e.Motivo);
			}

			if (e.Estado == EstadoBarrido.Recuperado && ahoraUtc >= e.VenceOportunidadUtc)
			{
				e.Estado = EstadoBarrido.Vencido;
				e.Motivo = $"VIGENCIA DE {Cfg.VigenciaOportunidadMinutos} MIN TERMINADA";

				if (!Terminal(op.M1))
					(op.M1, op.MotivoM1) = (EstadoModelo.Vencido, e.Motivo);
			}

			if (!Terminal(op.M2) && op.VenceM2Utc is { } vence && ahoraUtc >= vence)
				(op.M2, op.MotivoM2) = (EstadoModelo.Vencido, $"VIGENCIA DE {Cfg.VigenciaOportunidadMinutos} MIN TERMINADA");
		}
	}
}
