namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>Tono de una línea del reporte. El color siempre acompaña a un texto; nunca lo sustituye.</summary>
public enum TonoLinea
{
	Normal,
	Titulo,
	Ok,
	Pendiente,
	Fallo,
	Aviso,
	Gris,
	Amarillo,
	Naranja,
	Blanco
}

public sealed record LineaReporte(string Texto, TonoLinea Tono = TonoLinea.Normal);

public sealed class SeccionReporte
{
	public string Titulo { get; init; } = "";

	public List<LineaReporte> Lineas { get; } = new();

	public void Add(string texto, TonoLinea tono = TonoLinea.Normal) => Lineas.Add(new LineaReporte(texto, tono));
}

/// <summary>Fotografía del estado del motor para la ventana flotante: conclusión arriba, detalle debajo.</summary>
public sealed class InstantaneaReporte
{
	public string Instrumento { get; set; } = "";

	public string Modo { get; set; } = "ANÁLISIS / SIN ÓRDENES";

	public string Sesion { get; set; } = "";

	public string Hora { get; set; } = "";

	public string Fuente { get; set; } = "";

	public string Graficos { get; set; } = "";

	public string Cabecera { get; set; } = "";

	public TonoLinea TonoCabecera { get; set; }

	public List<LineaReporte> Resumen { get; } = new();

	public List<SeccionReporte> Secciones { get; } = new();

	public int Revision { get; set; }

	public DateTime? UltimaVelaUtc { get; set; }
}

public sealed partial class MotorCompleto
{
	private sealed class Formato
	{
		private readonly MotorCompleto _m;
		private readonly ZonaPresentacion _zona;
		private readonly DateTime _hoy;

		public Formato(MotorCompleto m, ZonaPresentacion zona, DateTime referenciaUtc)
		{
			_m = m;
			_zona = zona;
			_hoy = Calendario.APresentacion(referenciaUtc, zona).Date;
		}

		public string H(DateTime utc)
		{
			var local = Calendario.APresentacion(utc, _zona);
			return (local.Date == _hoy ? local.ToString("HH:mm") : local.ToString("dd/MM HH:mm")) + " " + Calendario.Sufijo(_zona);
		}

		public string P(decimal precio) => _m.Precio(precio);

		public string Ticks(decimal precio1, decimal precio2) =>
			$"{Math.Abs(precio1 - precio2):0.##} pts / {Math.Abs(precio1 - precio2) / _m.Cfg.TamanoTick:0} ticks";
	}

	private static (string Simbolo, TonoLinea Tono) Marca(EstadoCondicion e) => e switch
	{
		EstadoCondicion.Cumplida => ("✓", TonoLinea.Ok),
		EstadoCondicion.Pendiente => ("…", TonoLinea.Pendiente),
		EstadoCondicion.Fallida => ("✗", TonoLinea.Fallo),
		EstadoCondicion.Opcional => ("—", TonoLinea.Gris),
		_ => ("?", TonoLinea.Aviso)
	};

	private static TonoLinea Tono(EstadoModelo e) => e switch
	{
		EstadoModelo.Confirmado => TonoLinea.Ok,
		EstadoModelo.EnFormacion => TonoLinea.Pendiente,
		EstadoModelo.Bloqueado or EstadoModelo.Invalidado => TonoLinea.Fallo,
		_ => TonoLinea.Gris
	};

	/// <summary>Crea la fotografía del reporte. 'fuente' y 'graficos' los describe el indicador (maestro y visores).</summary>
	public InstantaneaReporte Instantanea(DateTime ahoraUtc, ZonaPresentacion zona, string fuente, string graficos)
	{
		lock (_sync)
		{
			var f = new Formato(this, zona, UltimaVelaUtc ?? ahoraUtc);
			var r = new InstantaneaReporte
			{
				Instrumento = string.IsNullOrEmpty(Cfg.Instrumento) ? "(sin instrumento)" : Cfg.Instrumento,
				Hora = Calendario.APresentacion(ahoraUtc, zona).ToString("dd/MM HH:mm:ss") + " " + Calendario.Sufijo(zona),
				Sesion = SesionActual(UltimaVelaUtc ?? ahoraUtc),
				Fuente = fuente,
				Graficos = graficos,
				Revision = Revision,
				UltimaVelaUtc = UltimaVelaUtc
			};

			var op = Oportunidades.LastOrDefault(o => o.Viva) ?? Oportunidades.LastOrDefault(o => o.E.Estado != EstadoBarrido.Bloqueado)
				?? Oportunidades.LastOrDefault();

			Cabecera(r, op, f);
			r.Secciones.Add(SeccionContexto(f));
			r.Secciones.Add(SeccionBarrido(op, f));
			r.Secciones.Add(SeccionModelos(op, f));
			r.Secciones.Add(SeccionLvn(op, f));
			r.Secciones.Add(SeccionFootprint(op, f));
			r.Secciones.Add(SeccionRiesgo(op, f));
			r.Secciones.Add(SeccionGestion(f));
			r.Secciones.Add(SeccionCalidad(f));
			r.Secciones.Add(SeccionEstadistica());
			r.Secciones.Add(SeccionEventos(f));
			return r;
		}
	}

	private string SesionActual(DateTime utc)
	{
		var sesiones = new[] { TipoSesion.NuevaYork, TipoSesion.Londres, TipoSesion.Tokio }
			.Where(t => Calendario.Ventana(t, utc.AddSeconds(-1)) is not null)
			.Select(t => Calendario.Nombre(t).ToUpperInvariant())
			.ToList();

		return sesiones.Count > 0 ? string.Join(" + ", sesiones) : "FUERA DE LAS SESIONES DEFINIDAS";
	}

	private void Cabecera(InstantaneaReporte r, Oportunidad? op, Formato f)
	{
		var senal = Senales.LastOrDefault();

		if (Velas1M.Count == 0)
		{
			(r.Cabecera, r.TonoCabecera) = ("SIN DATOS — ESPERANDO VELAS DE 1M DEL MAESTRO", TonoLinea.Gris);
			r.Resumen.Add(new("No hay oportunidades porque todavía no hay datos, no porque no esté calculando.", TonoLinea.Gris));
			return;
		}

		var vigente = senal is not null && UltimaVelaUtc - senal.ConfirmadaUtc <= TimeSpan.FromMinutes(Cfg.VigenciaOportunidadMinutos)
			&& (senal.Resultado is null || senal.Resultado.Abierta);

		if (vigente)
		{
			var s = senal!;
			var p = s.Plan;
			var tono = s.Tipo == TipoLiquidez.Externa ? TonoLinea.Blanco : TonoLinea.Naranja;
			(r.Cabecera, r.TonoCabecera) = ($"SEÑAL CONFIRMADA — {s.Titulo}", tono);

			var edad = (UltimaVelaUtc!.Value - s.ConfirmadaUtc).TotalMinutes;
			r.Resumen.Add(new(edad < 1 ? "SEÑAL RECIÉN CONFIRMADA" : $"SEÑAL ANTERIOR · confirmada hace {edad:0} min (no es una entrada nueva)",
				edad < 1 ? TonoLinea.Ok : TonoLinea.Aviso));
			r.Resumen.Add(new($"ID de señal: {s.Id} · evento {s.EventoId} · referencia: {s.Referencia}"));
			r.Resumen.Add(new($"Confirmada al cierre: {f.H(s.ConfirmadaUtc)} ({s.VelaGatillo.Marco.Texto()})"));
			r.Resumen.Add(new($"Variante: {s.Variante}"));
			r.Resumen.Add(new($"Entrada de referencia: {f.P(p.Entrada)}"));
			r.Resumen.Add(new($"SL propuesto: {f.P(p.Sl)} · anclaje: {p.AnclajeSl} · distancia {f.Ticks(p.Entrada, p.Sl)}"));
			r.Resumen.Add(new($"TP propuesto: {f.P(p.Tp)} · objetivo: LVN {p.Objetivo?.Id} · ALTA PRIORIDAD", TonoLinea.Amarillo));
			r.Resumen.Add(new($"Riesgo configurado: {Cfg.RiesgoUsd:0.00} USD · riesgo estimado total: {p.RiesgoTotal:0.00} USD"));
			r.Resumen.Add(new($"Cantidad propuesta: {p.Cantidad} micros · RR neto estimado: {p.RrNeto:0.00}"));
			r.Resumen.Add(new("Ejecución real: NO CONFIRMADA · órdenes enviadas por el indicador: NINGUNA", TonoLinea.Aviso));
			return;
		}

		if (Estructura.Sesgo == Sesgo.NoDeterminado)
		{
			(r.Cabecera, r.TonoCabecera) = ("CONTEXTO 1H NO DETERMINADO — ESPERAR", TonoLinea.Aviso);
			r.Resumen.Add(new($"Cambio de contexto: requiere {Estructura.CondicionCambio(Precio)}"));
			r.Resumen.Add(new("NO HAY ENTRADA CONFIRMADA.", TonoLinea.Aviso));
			return;
		}

		if (op is not null && op.Viva)
		{
			var e = op.E;
			var usarM2 = !Terminal(op.M2) && (op.ZonaM2 is not null || Terminal(op.M1));
			var estado = usarM2 ? op.M2 : op.M1;
			var motivo = usarM2 ? op.MotivoM2 : op.MotivoM1;
			var bloqueado = estado == EstadoModelo.Bloqueado;

			(r.Cabecera, r.TonoCabecera) = bloqueado
				? ("ESPERAR — SEÑAL BLOQUEADA", TonoLinea.Fallo)
				: (e.Estado == EstadoBarrido.EnEvaluacion ? "ESPERAR RECUPERACIÓN" : "ESPERAR GATILLO", TonoLinea.Pendiente);

			r.Resumen.Add(new($"Contexto 1H: {Estructura.Sesgo.Texto()}"));
			r.Resumen.Add(new($"Modelo: {(usarM2 ? "M2" : "M1")} {e.Direccion.Texto()} · LIQUIDEZ {e.Tipo.Texto()}", e.Tipo == TipoLiquidez.Externa ? TonoLinea.Blanco : TonoLinea.Naranja));
			r.Resumen.Add(new(usarM2
				? "Secuencia: barrido → recuperación → ruptura LVN → retroceso → respuesta → gatillo → riesgo"
				: "Secuencia: barrido → recuperación → gatillo → riesgo"));
			r.Resumen.Add(new($"{(bloqueado ? "BLOQUEO" : "FALTA")}: {motivo}", bloqueado ? TonoLinea.Fallo : TonoLinea.Pendiente));
			r.Resumen.Add(new("NO HAY ENTRADA CONFIRMADA.", TonoLinea.Aviso));
			return;
		}

		(r.Cabecera, r.TonoCabecera) = ("ESPERAR — SIN OPORTUNIDAD ACTIVA", TonoLinea.Gris);
		r.Resumen.Add(new($"Contexto 1H: {Estructura.Sesgo.Texto()} · buscando barridos {(Estructura.Sesgo == Sesgo.Alcista ? "de mínimos (LONG)" : "de máximos (SHORT)")}"));

		if (senal is not null)
			r.Resumen.Add(new($"Última señal: {senal.Id} {senal.Titulo} a las {f.H(senal.ConfirmadaUtc)} ({(senal.Resultado?.Abierta == false ? senal.Resultado.MotivoSalida : "fuera de vigencia")})", TonoLinea.Gris));

		r.Resumen.Add(new("NO HAY ENTRADA CONFIRMADA.", TonoLinea.Aviso));
	}

	private SeccionReporte SeccionContexto(Formato f)
	{
		var s = new SeccionReporte { Titulo = "1. CONTEXTO Y ESTRUCTURA" };
		var est = Estructura;
		var tono = est.Sesgo switch { Sesgo.Alcista => TonoLinea.Ok, Sesgo.Bajista => TonoLinea.Fallo, _ => TonoLinea.Aviso };

		s.Add($"1H: {est.Sesgo.Texto()}{(est.SwingProtegido is not null ? " · swing protegido vigente" : "")}", tono);

		if (est.SwingProtegido is { } swing)
			s.Add($"Swing protegido: {f.P(swing.Precio)} ({(swing.EsMaximo ? "máximo" : "mínimo")} de {f.H(swing.ExtremoUtc)}, confirmado {f.H(swing.ConfirmadoUtc)})");

		if (est.Eventos.LastOrDefault() is { } ev)
			s.Add($"Última confirmación estructural: {ev.Tipo} {ev.Sesgo.Texto()} al cierre de {f.H(ev.Utc)} (nivel roto {f.P(ev.NivelRoto)})");

		if (est.DesdeUtc is { } desde)
			s.Add($"Sesgo vigente desde: {f.H(desde)}");

		s.Add($"Cambio de contexto: requiere {est.CondicionCambio(Precio)}");
		s.Add($"Lectura de rango (cierre vs. vela anterior, no es estructura): 1M {Lectura1M.Texto()} | 5M {Lectura5M.Texto()} | 15M {Lectura15M.Texto()} | 1H {Lectura1H.Texto()}", TonoLinea.Gris);

		var (alineacion, tonoAlineacion) = Alineacion();
		s.Add($"Alineación con 1H: {alineacion}", tonoAlineacion);

		if (est.UltimaVelaUtc is { } h1)
			s.Add($"Última vela 1H confirmada: cierre {f.H(h1)}");

		return s;
	}

	private (string Texto, TonoLinea Tono) Alineacion()
	{
		if (Estructura.Sesgo == Sesgo.NoDeterminado)
			return ("NO EVALUABLE (contexto no determinado)", TonoLinea.Aviso);

		var favor = Estructura.Sesgo == Sesgo.Alcista ? Lectura.Alcista : Lectura.Bajista;
		var contra = Estructura.Sesgo == Sesgo.Alcista ? Lectura.Bajista : Lectura.Alcista;
		var lecturas = new[] { Lectura1M, Lectura5M, Lectura15M };
		var aFavor = lecturas.Count(l => l == favor);
		var enContra = lecturas.Count(l => l == contra);

		return aFavor == 3 ? ("COMPLETA (1M, 5M y 15M a favor)", TonoLinea.Ok)
			: aFavor == 0 && enContra > 0 ? ($"CONTRARIA ({enContra} de 3 en contra)", TonoLinea.Fallo)
			: ($"PARCIAL ({aFavor} de 3 a favor, {enContra} en contra)", TonoLinea.Pendiente);
	}

	private SeccionReporte SeccionBarrido(Oportunidad? op, Formato f)
	{
		var s = new SeccionReporte { Titulo = "2. BARRIDO Y RECUPERACIÓN" };

		if (op is null)
		{
			s.Add("Sin barridos detectados todavía.", TonoLinea.Gris);
			s.Add($"Referencias vivas: {Liquidez.Activas(UltimaVelaUtc ?? DateTime.MinValue).Count()} (externas: sesiones Tokio, Londres, NY, semana anterior y swings 1H)", TonoLinea.Gris);
			return s;
		}

		var e = op.E;
		var tonoEstado = e.Estado switch
		{
			EstadoBarrido.Recuperado or EstadoBarrido.Consumido => TonoLinea.Ok,
			EstadoBarrido.EnEvaluacion => TonoLinea.Pendiente,
			EstadoBarrido.Bloqueado or EstadoBarrido.Invalidado => TonoLinea.Fallo,
			_ => TonoLinea.Gris
		};

		s.Add($"Evento: {e.Id} · {e.Motivo}", tonoEstado);
		s.Add($"Tipo: BARRIDO DE LIQUIDEZ {(e.Tipo == TipoLiquidez.Externa ? "DE ESTRUCTURA EXTERNA" : "INTERNA")} · {e.Direccion.Texto()}",
			e.Tipo == TipoLiquidez.Externa ? TonoLinea.Blanco : TonoLinea.Naranja);
		s.Add($"Referencia: {e.Principal.Etiqueta}{(e.Referencias.Count > 1 ? $" (+{e.Referencias.Count - 1} más en la misma vela)" : "")}");

		if (e.Tipo == TipoLiquidez.Interna)
			s.Add($"Rango interno: {e.Principal.Rango ?? "NO IDENTIFICADO (limitación)"}");

		s.Add($"Nivel barrido: {f.P(e.Nivel)} · extremo del barrido: {f.P(e.Extremo)} · penetración: {e.PenetracionTicks} ticks");
		s.Add($"Hora del barrido: {f.H(e.VelaBarrido.InicioUtc)} (conocido al cierre {f.H(e.ConocidoUtc)})");
		s.Add($"Recuperación 1M (≥ {Cfg.RecuperacionTicks1M} ticks): {(e.Recuperacion1MUtc is { } r1 ? $"CONFIRMADA {f.H(r1)}" : "PENDIENTE")}",
			e.Recuperacion1MUtc is null ? TonoLinea.Pendiente : TonoLinea.Ok);
		s.Add($"Recuperación 5M (≥ {Cfg.RecuperacionTicks5M} ticks): {(e.Recuperacion5MUtc is { } r5 ? $"CONFIRMADA {f.H(r5)}" : "PENDIENTE")}",
			e.Recuperacion5MUtc is null ? TonoLinea.Pendiente : TonoLinea.Ok);
		s.Add($"Regla seleccionada: {Cfg.ReglaRecuperacion switch { ReglaRecuperacion.Solo1M => "SOLO 1M", ReglaRecuperacion.Solo5M => "SOLO 5M", ReglaRecuperacion.Ambas => "AMBAS", _ => "PRIMERA RECUPERACIÓN VÁLIDA" }}");

		var ahora = UltimaVelaUtc ?? e.ConocidoUtc;

		if (e.Estado == EstadoBarrido.EnEvaluacion)
			s.Add($"Plazo de recuperación: quedan {Math.Max(0, (e.VenceRecuperacionUtc - ahora).TotalMinutes):0} min (hasta {f.H(e.VenceRecuperacionUtc)})");
		else if (e.VenceOportunidadUtc is { } vence && e.Estado == EstadoBarrido.Recuperado)
			s.Add($"Vigencia restante: {Math.Max(0, (vence - ahora).TotalMinutes):0} min · invalidación: cierre más allá de {f.P(e.Extremo)}");

		s.Add($"Absorción ({Cfg.ModoAbsorcion.ToString().ToUpperInvariant()}): {e.Absorcion.Texto()}",
			e.Absorcion.Confirmada ? TonoLinea.Ok : e.Absorcion.Pendiente ? TonoLinea.Pendiente : TonoLinea.Gris);
		s.Add($"   {e.Absorcion.Detalle()}", TonoLinea.Gris);

		var recientes = Barridos.TakeLast(6).Reverse().ToList();
		s.Add($"Barridos recientes ({Barridos.Count} registrados, también los que no terminaron en señal):", TonoLinea.Titulo);

		foreach (var b in recientes)
			s.Add($"   {f.H(b.ConocidoUtc)} · evento {b.Id} {b.Direccion.Texto()} {b.Tipo.Texto()} · {b.Principal.Etiqueta} · {b.Motivo}", TonoLinea.Gris);

		return s;
	}

	private List<(EstadoCondicion Estado, string Texto)> CondicionesComunes(Oportunidad op)
	{
		var e = op.E;
		var lista = new List<(EstadoCondicion, string)>
		{
			(Estructura.Sesgo == Sesgo.NoDeterminado ? EstadoCondicion.NoEvaluable : Compatible(e.Direccion) ? EstadoCondicion.Cumplida : EstadoCondicion.Fallida,
				$"Contexto 1H compatible ({Estructura.Sesgo.Texto()})"),
			(e.Estado == EstadoBarrido.Bloqueado ? EstadoCondicion.Fallida : EstadoCondicion.Cumplida, $"Barrido identificado ({e.Principal.Etiqueta})")
		};

		return lista;
	}

	private (EstadoCondicion, string)? CondicionAbsorcion(EventoBarrido e) => Cfg.ModoAbsorcion switch
	{
		ModoFiltro.Apagado => null,
		ModoFiltro.Informativo => (EstadoCondicion.Opcional, $"Absorción (informativa): {e.Absorcion.Texto()}"),
		_ => (e.Absorcion.Pendiente ? EstadoCondicion.Pendiente : e.Absorcion.Confirmada ? EstadoCondicion.Cumplida : EstadoCondicion.Fallida,
			$"Absorción (obligatoria): {e.Absorcion.Texto()}")
	};

	private (EstadoCondicion, string)? CondicionDirectriz(Directriz? linea, Formato f)
	{
		if (Cfg.ModoDirectriz == ModoFiltro.Apagado)
			return null;

		var texto = linea is null
			? "Directriz: NO DISPONIBLE (faltan dos pivotes confirmados)"
			: linea.RotaUtc is { } rota ? $"Directriz rota al cierre de {f.H(rota)}" : "Directriz disponible, sin ruptura";

		if (Cfg.ModoDirectriz == ModoFiltro.Informativo)
			return (EstadoCondicion.Opcional, texto + " (confluencia informativa)");

		return (linea is null ? EstadoCondicion.NoEvaluable : linea.RotaUtc is not null ? EstadoCondicion.Cumplida : EstadoCondicion.Pendiente, texto + " (obligatoria)");
	}

	private static (EstadoCondicion, string) CondicionRiesgo(PlanRiesgo? plan) => plan is null
		? (EstadoCondicion.Pendiente, "Riesgo y objetivo LVN amarillo: pendiente del gatillo")
		: plan.Admitido ? (EstadoCondicion.Cumplida, $"Riesgo admitido: {plan.Cantidad} micros · RR neto {plan.RrNeto:0.00}")
		: (EstadoCondicion.Fallida, $"Riesgo: {plan.Bloqueo}");

	private static EstadoCondicion Gatillo(bool? ok, EstadoModelo modelo) =>
		ok == true ? EstadoCondicion.Cumplida : Terminal(modelo) && ok == false ? EstadoCondicion.Fallida : EstadoCondicion.Pendiente;

	private SeccionReporte SeccionModelos(Oportunidad? op, Formato f)
	{
		var s = new SeccionReporte { Titulo = "3. MODELOS Y CONFLUENCIAS" };

		if (op is null)
		{
			s.Add("M1: NO ACTIVO · sin barrido", TonoLinea.Gris);
			s.Add("M2: NO ACTIVO · sin barrido", TonoLinea.Gris);
			return s;
		}

		var e = op.E;
		s.Add($"M1: {op.M1.Texto()} · {Vacio(op.MotivoM1, "sin iniciar (espera recuperación)")}", Tono(op.M1));
		s.Add($"M2: {op.M2.Texto()} · {Vacio(op.MotivoM2, "sin iniciar (espera recuperación o rechazo fuerte)")}", Tono(op.M2));

		var m1 = CondicionesComunes(op);
		m1.Add((e.RecuperadoUtc is not null ? EstadoCondicion.Cumplida : e.Estado == EstadoBarrido.Vencido ? EstadoCondicion.Fallida : EstadoCondicion.Pendiente,
			"Recuperación según configuración"));

		if (CondicionAbsorcion(e) is { } abs)
			m1.Add(abs);

		m1.Add((Gatillo(op.GatilloM1, op.M1), $"Vela direccional y footprint{(op.GatilloM1Detalle.Length > 0 ? ": " + op.GatilloM1Detalle : "")}"));

		if (CondicionDirectriz(op.DirectrizM1, f) is { } dir1)
			m1.Add(dir1);

		m1.Add(CondicionRiesgo(op.PlanM1));
		Lista(s, "Condiciones M1:", m1);

		var m2 = CondicionesComunes(op);
		m2.Add((e.RecuperadoUtc is not null || e.RechazoFuerte ? EstadoCondicion.Cumplida : EstadoCondicion.Pendiente,
			$"Recuperación o rechazo fuerte{(e.RechazoFuerte ? " (rechazo fuerte en la vela del barrido)" : "")}"));
		m2.Add((op.ZonaM2 is not null ? EstadoCondicion.Cumplida : Terminal(op.M2) ? EstadoCondicion.Fallida : EstadoCondicion.Pendiente,
			op.ZonaM2 is { } z ? $"Ruptura del LVN {z.Id} confirmada {f.H(op.VelaRuptura!.FinUtc)}" : "Ruptura de LVN con desplazamiento"));
		m2.Add((op.Retroceso ? EstadoCondicion.Cumplida : EstadoCondicion.Pendiente, "Retroceso posterior a la ruptura"));
		m2.Add((op.Respuesta ? EstadoCondicion.Cumplida : EstadoCondicion.Pendiente, "Respuesta por cierre del lado favorable"));
		m2.Add((Gatillo(op.GatilloM2, op.M2), $"Gatillo footprint{(op.GatilloM2Detalle.Length > 0 ? ": " + op.GatilloM2Detalle : "")}"));
		m2.Add(Cfg.ExigirFvg
			? (op.FvgM2 is not null ? EstadoCondicion.Cumplida : EstadoCondicion.Pendiente, op.FvgM2 is { } fv ? $"FVG {fv.Id} (obligatorio)" : "FVG obligatorio ausente")
			: (EstadoCondicion.Opcional, op.FvgM2 is { } fo ? $"FVG {fo.Id} asociado: NO OBLIGATORIO" : "FVG ausente: NO OBLIGATORIO"));

		if (CondicionAbsorcion(e) is { } abs2)
			m2.Add(abs2);

		if (CondicionDirectriz(op.DirectrizM2, f) is { } dir2)
			m2.Add(dir2);

		m2.Add(CondicionRiesgo(op.PlanM2));
		Lista(s, "Condiciones M2:", m2);
		return s;
	}

	private static string Vacio(string texto, string alternativa) => string.IsNullOrEmpty(texto) ? alternativa : texto;

	private static void Lista(SeccionReporte s, string titulo, List<(EstadoCondicion Estado, string Texto)> condiciones)
	{
		s.Add(titulo, TonoLinea.Titulo);

		foreach (var (estado, texto) in condiciones)
		{
			var (simbolo, tono) = Marca(estado);
			s.Add($"   {simbolo} {texto} · {estado.Texto()}", tono);
		}
	}

	private SeccionReporte SeccionLvn(Oportunidad? op, Formato f)
	{
		var s = new SeccionReporte { Titulo = "4. MAPA LVN Y FVG" };
		var mapa = Lvn.Vigente;

		if (mapa is null)
		{
			s.Add($"Sin mapa LVN: se crea con la primera vela de la sesión de NY y necesita sesiones completas (hay {Lvn.DiasCompletos}).", TonoLinea.Aviso);
			return s;
		}

		var altas = mapa.Zonas.Count(z => z.Prioridad == PrioridadLvn.Alta);
		var secundarias = mapa.Zonas.Count(z => z.Prioridad == PrioridadLvn.Secundaria);
		s.Add($"Mapa: versión {mapa.Version} · creado {f.H(mapa.CreadoUtc)} · congelado para la sesión · coincidencia 1M/5M: CORRECTA (mapa compartido)");
		s.Add($"Referencia ±{Cfg.BandaLvnPuntos:0} puntos: {f.P(mapa.Referencia)} (apertura de la primera vela procesada de la sesión NY)");
		s.Add($"Zonas: {mapa.Zonas.Count} · {altas} ALTA PRIORIDAD (amarillo) · {secundarias} SECUNDARIAS (naranja) · resto menores (ocultas)");

		var zonaTrabajo = op?.ZonaM2;

		if (zonaTrabajo is not null)
		{
			s.Add($"LVN de trabajo: {zonaTrabajo.Id} · {zonaTrabajo.Prioridad.Texto()} · {f.P(zonaTrabajo.Inferior)} — {f.P(zonaTrabajo.Superior)}",
				zonaTrabajo.Prioridad == PrioridadLvn.Alta ? TonoLinea.Amarillo : TonoLinea.Naranja);
			s.Add($"   origen: {string.Join(", ", zonaTrabajo.Origenes)}", TonoLinea.Gris);
		}

		var ultimo = Velas1M.Count > 0 ? Velas1M[^1].Cierre : mapa.Referencia;
		var d = op?.E.Direccion ?? (Estructura.Sesgo == Sesgo.Bajista ? Direccion.Corta : Direccion.Larga);

		if (Lvn.SiguientePrioritario(d, ultimo, UltimaVelaUtc ?? mapa.CreadoUtc) is { } objetivo)
		{
			s.Add($"Próximo LVN objetivo ({d.Texto()}): {objetivo.Id} · ALTA PRIORIDAD · AMARILLO · {f.P(objetivo.Inferior)} — {f.P(objetivo.Superior)} · a {f.Ticks(objetivo.Centro, ultimo)}",
				TonoLinea.Amarillo);
		}
		else
		{
			s.Add($"Próximo LVN objetivo ({d.Texto()}): SIN OBJETIVO LVN AMARILLO DISPONIBLE", TonoLinea.Fallo);
		}

		if (op?.FvgM2 is { } fvg)
			s.Add($"FVG asociado: {fvg.Texto(Precio)} · variante {op.VarianteM2}");
		else if (op?.ZonaM2 is not null)
			s.Add($"FVG asociado: NINGUNO · variante M2 LVN SIN FVG ({(Cfg.ExigirFvg ? "FVG OBLIGATORIO AUSENTE" : "permitido")})");

		s.Add("Zonas por puntuación (amarillo = alta prioridad, naranja = secundaria):", TonoLinea.Titulo);

		foreach (var z in mapa.Zonas.OrderBy(z => z.Puesto).Take(10))
		{
			var tono = z.Prioridad switch { PrioridadLvn.Alta => TonoLinea.Amarillo, PrioridadLvn.Secundaria => TonoLinea.Naranja, _ => TonoLinea.Gris };
			s.Add($"   #{z.Puesto} LVN {z.Id} · {z.Prioridad.Texto()} · {f.P(z.Inferior)}–{f.P(z.Superior)} · prof. {z.Profundidad:0.00} · "
				+ $"reacciones {z.Reacciones} ({z.SesionesConReaccion} sesiones{(z.Bipolar ? ", BIPOLAR" : "")}) · perfiles {z.Origenes.Count} · puntuación {z.Puntuacion:0}", tono);
		}

		var activos = FvgDetector.Fvgs.Where(x => x.Activo).TakeLast(4).ToList();
		s.Add($"FVG activos de 1M: {FvgDetector.Fvgs.Count(x => x.Activo)}{(activos.Count > 0 ? " · últimos: " + string.Join(" | ", activos.Select(x => x.Texto(Precio))) : "")}", TonoLinea.Gris);
		return s;
	}

	private SeccionReporte SeccionFootprint(Oportunidad? op, Formato f)
	{
		var s = new SeccionReporte { Titulo = "5. FOOTPRINT, CVD Y DIRECTRIZ" };
		var vela = op?.VelaExaminada ?? (Velas1M.Count > 0 ? Velas1M[^1] : null);

		if (vela is not null)
		{
			s.Add($"Vela examinada: {vela.Marco.Texto()} {f.H(vela.InicioUtc)} · CERRADA{(op?.VelaExaminada is null ? " (última vela; ningún gatillo evaluado aún)" : "")}");

			if (vela.Huella is { } h)
			{
				s.Add($"POC: {f.P(h.Poc)} · ubicación relativa: {h.PosPoc:0.00} · VAL / VAH: {f.P(h.Val)} / {f.P(h.Vah)} · centro VA relativo: {h.PosVa:0.00}");
				s.Add($"Extremos: máximo bid {h.EnMaximo.Bid:0} / ask {h.EnMaximo.Ask:0} · mínimo bid {h.EnMinimo.Bid:0} / ask {h.EnMinimo.Ask:0}", TonoLinea.Gris);
			}
			else
			{
				s.Add("Footprint: NO DISPONIBLE para esta vela (no evaluable)", TonoLinea.Aviso);
			}

			s.Add($"Delta: {vela.Delta:0} · delta relativo: {(vela.Volumen > 0 ? 100m * vela.Delta / vela.Volumen : 0):0.0} % del volumen {vela.Volumen:0}");
		}

		var detalle = op is null ? "" : !string.IsNullOrEmpty(op.GatilloM2Detalle) ? op.GatilloM2Detalle : op.GatilloM1Detalle;
		var ok = op is null ? null : op.GatilloM2 ?? op.GatilloM1;
		s.Add($"Gatillo: {(ok == true ? "VÁLIDO" : ok == false ? "RECHAZADO" : "PENDIENTE")}{(detalle.Length > 0 ? " · " + detalle : "")}",
			ok == true ? TonoLinea.Ok : ok == false ? TonoLinea.Fallo : TonoLinea.Pendiente);

		var cvd5 = A5.Velas.LastOrDefault();
		s.Add($"CVD 1M: {Cvd.Estado} · CVD 5M: {(cvd5 is null ? "NO DISPONIBLE" : cvd5.CvdParcial ? "BASE PARCIAL" : "BASE COMPLETA")} · reinicio {Cfg.ReinicioCvdNy:hh\\:mm} NY",
			Cvd.Parcial ? TonoLinea.Aviso : TonoLinea.Normal);

		var div = Divergencias.Divergencias.LastOrDefault();
		s.Add($"Divergencia: {(div is null ? "ninguna confirmada" : div.Texto + $" (precio {f.P(div.P1.Precio)} → {f.P(div.P2.Precio)})")} · no evaluables: {Divergencias.NoEvaluables} · informativas, no obligatorias");

		var linea = op?.DirectrizM2 ?? op?.DirectrizM1;
		s.Add(linea is null
			? $"Directriz ({Cfg.ModoDirectriz.ToString().ToUpperInvariant()}): NO DISPONIBLE"
			: $"Directriz ({Cfg.ModoDirectriz.ToString().ToUpperInvariant()}): anclajes {f.P(linea.P1.Precio)} ({f.H(linea.P1.ExtremoUtc)}) y {f.P(linea.P2.Precio)} ({f.H(linea.P2.ExtremoUtc)}) · "
			+ $"disponible desde {f.H(linea.DisponibleUtc)} · {(linea.RotaUtc is { } rota ? $"ROTA {f.H(rota)}" : "SIN RUPTURA")}");

		s.Add("Absorción: detector por volumen climático, agresión contraria, escaso avance y cambio de delta (umbrales propuestos, sin validar).", TonoLinea.Gris);
		return s;
	}

	private SeccionReporte SeccionRiesgo(Oportunidad? op, Formato f)
	{
		var s = new SeccionReporte { Titulo = "6. PLAN DE RIESGO" };
		var senalVigente = op?.SenalM2 ?? op?.SenalM1;
		var plan = senalVigente?.Plan ?? op?.PlanM2 ?? op?.PlanM1;

		s.Add($"Riesgo configurado: {Cfg.RiesgoUsd:0.00} USD · valor del tick: {Cfg.ValorTick:0.00} USD · comisión ida/vuelta: {Cfg.ComisionIdaVuelta:0.00} USD · deslizamiento: {Cfg.DeslizamientoTicks} ticks");

		if (!Cfg.ValoresMonetariosVerificados)
			s.Add("VALORES MONETARIOS NO VERIFICADOS: no se admitirá ninguna señal hasta activarlos en los ajustes.", TonoLinea.Fallo);

		if (plan is null)
		{
			s.Add("Estado: PRELIMINAR · NO EJECUTABLE", TonoLinea.Pendiente);

			if (op is not null && op.Viva && op.E.Estado != EstadoBarrido.Bloqueado)
			{
				var e = op.E;
				var slPrevisto = Redondear(e.Extremo - e.Direccion.Signo() * Cfg.Ticks(Cfg.BufferSlTicks));
				var ultimo = Velas1M[^1].Cierre;
				var objetivo = Lvn.SiguientePrioritario(e.Direccion, ultimo, UltimaVelaUtc ?? e.ConocidoUtc);
				s.Add("Entrada de referencia: pendiente del cierre gatillo");
				s.Add($"SL previsto: extremo del barrido {f.P(e.Extremo)} {(e.Direccion == Direccion.Larga ? "−" : "+")} {Cfg.BufferSlTicks} ticks = {f.P(slPrevisto)} (provisional)");
				s.Add(objetivo is null ? "TP previsto: SIN OBJETIVO LVN AMARILLO DISPONIBLE" : $"TP previsto: siguiente LVN prioritario {objetivo.Id} ({f.P(objetivo.Inferior)}–{f.P(objetivo.Superior)})",
					objetivo is null ? TonoLinea.Fallo : TonoLinea.Amarillo);
			}

			s.Add("Cantidad: PENDIENTE · RR neto: PENDIENTE · bloqueos: todavía no existe gatillo confirmado", TonoLinea.Gris);
			return s;
		}

		s.Add(plan.Admitido ? $"Estado: ADMITIDO{(senalVigente is not null ? $" (señal {senalVigente.Id})" : "")}" : $"Estado: BLOQUEADO — {plan.Bloqueo}",
			plan.Admitido ? TonoLinea.Ok : TonoLinea.Fallo);
		s.Add($"Entrada de referencia: {f.P(plan.Entrada)} (cierre de la vela gatillo; no es una ejecución)");
		s.Add($"SL: {f.P(plan.Sl)} · anclaje: {plan.AnclajeSl} · distancia {f.Ticks(plan.Entrada, plan.Sl)}");
		s.Add(plan.Objetivo is null
			? "TP: SIN OBJETIVO LVN AMARILLO DISPONIBLE"
			: $"TP: {f.P(plan.Tp)} · LVN {plan.Objetivo.Id} ALTA PRIORIDAD ({f.P(plan.Objetivo.Inferior)}–{f.P(plan.Objetivo.Superior)}) · anticipación {plan.AnticipacionTicks} ticks",
			plan.Objetivo is null ? TonoLinea.Fallo : TonoLinea.Amarillo);
		s.Add($"Riesgo por contrato: {plan.RiesgoPorContrato:0.00} USD ({plan.DistanciaSlTicks} ticks × {plan.ValorTick:0.00} + costes {plan.CostesPorContrato:0.00})");
		s.Add($"Cantidad (redondeada hacia abajo, máx. {Cfg.MaximoContratos}): {plan.Cantidad} micros · riesgo total estimado: {plan.RiesgoTotal:0.00} USD");
		s.Add($"RR neto: {plan.RrNeto:0.00} (mínimo {Cfg.RrNetoMinimo:0.00})", plan.RrNeto >= Cfg.RrNetoMinimo ? TonoLinea.Ok : TonoLinea.Fallo);

		if (plan.Bloqueo is not null)
			s.Add($"Motivo de bloqueo: {plan.Bloqueo}", TonoLinea.Fallo);

		s.Add("El SL nunca se acerca para aumentar contratos.", TonoLinea.Gris);
		return s;
	}

	private SeccionReporte SeccionGestion(Formato f)
	{
		var s = new SeccionReporte { Titulo = "7. GESTIÓN DEL MICROIMPULSO" };
		var ema = Cfg.MarcoEma == MarcoEma.M1 ? Ema1M.Valor : Ema5M.Valor;

		s.Add("Posición real: NO CONSULTADA (el indicador no envía órdenes ni lee la cuenta)", TonoLinea.Aviso);
		s.Add($"Simulación: {(Cfg.SimularResultados ? "ACTIVA (resultados simulados de cada señal)" : "DESACTIVADA")}");
		s.Add($"EMA {Cfg.PeriodoEma} ({(Cfg.MarcoEma == MarcoEma.M1 ? "1M" : "5M")}): {(ema is { } e ? f.P(e) : "sin datos suficientes")} · "
			+ $"salida por cierre cruzado por la EMA: {(Cfg.SalidaPorEma ? "ACTIVADA" : "solo ADVERTENCIA")}");

		var sim = Simulacion.Todas.LastOrDefault();

		if (sim is null)
		{
			s.Add("Sin operaciones simuladas.", TonoLinea.Gris);
			s.Add("Agotamiento: SIN CANDIDATA · arranque contrario: NO CONFIRMADO · salida del microimpulso: NO CONFIRMADA", TonoLinea.Gris);
			return s;
		}

		s.Add($"Señal {sim.Senal.Id} ({sim.Senal.Titulo}): {(sim.Abierta ? "ABIERTA (simulada)" : $"CERRADA {f.H(sim.SalidaUtc!.Value)} a {f.P(sim.PrecioSalida!.Value)}")}",
			sim.Abierta ? TonoLinea.Pendiente : TonoLinea.Normal);
		s.Add($"Stop inicial: {f.P(sim.Plan.Sl)} · stop actual: {f.P(sim.StopActual)}{(sim.StopPorPoc ? " (protección POC)" : "")}"
			+ $"{(sim.StopPropuesto is { } prop ? $" · propuesto {f.P(prop)} desde la vela siguiente" : "")}");

		if (sim.Candidata is { } c)
		{
			s.Add($"Agotamiento: POSIBLE AGOTAMIENTO — FALTA ARRANQUE CONTRARIO · candidata {f.H(c.InicioUtc)}, extremo {f.P(sim.Direccion == Direccion.Larga ? c.Maximo : c.Minimo)}, "
				+ $"vigencia restante {Math.Max(0, Cfg.VigenciaCandidataVelas - sim.EdadCandidata)} velas", TonoLinea.Aviso);
		}
		else
		{
			s.Add("Agotamiento: SIN CANDIDATA", TonoLinea.Gris);
		}

		if (sim.Advertencia.Length > 0 && sim.Abierta)
			s.Add(sim.Advertencia, TonoLinea.Aviso);

		s.Add(sim.Abierta
			? "Salida del microimpulso: NO CONFIRMADA"
			: $"Motivo de salida: {sim.MotivoSalida} · resultado {sim.ResultadoTicks:0} ticks = {sim.ResultadoUsd:0.00} USD ({sim.R:0.00} R) · MFE {sim.MfeTicks:0} / MAE {sim.MaeTicks:0} ticks",
			sim.Ambigua ? TonoLinea.Aviso : TonoLinea.Normal);
		s.Add("La salida del microimpulso no cambia el contexto de 1H.", TonoLinea.Gris);
		return s;
	}

	private SeccionReporte SeccionCalidad(Formato f)
	{
		var s = new SeccionReporte { Titulo = "8. CALIDAD Y TRAZABILIDAD" };
		var sinHuella = Velas1M.Count > 0 ? 100m * VelasSinHuella / Velas1M.Count : 0;

		s.Add($"Velas 1M: {Velas1M.Count} procesadas · huecos detectados: {Huecos} · sin footprint completo: {VelasSinHuella} ({sinHuella:0.0} %)",
			Huecos > 0 || VelasSinHuella > 0 ? TonoLinea.Aviso : TonoLinea.Ok);
		s.Add($"Periodos incompletos descartados: 5M {A5.Descartadas} · 15M {A15.Descartadas} · 1H {A60.Descartadas} (solo se usan periodos completos)", TonoLinea.Gris);
		s.Add($"CVD: {Cvd.Estado}", Cvd.Parcial ? TonoLinea.Aviso : TonoLinea.Ok);
		s.Add($"Histórico LVN: {Lvn.DiasCompletos} sesiones completas (se usan hasta 60; perfiles de 20 necesitan 20) · velas sin footprint en el mapa: {Lvn.Vigente?.VelasSinHuella ?? 0} (no se estiman)");
		s.Add($"Último dato recibido: {(UltimoDatoUtc is { } dato ? f.H(dato) : "—")} · última vela cerrada procesada: {(UltimaVelaUtc is { } u ? f.H(u) : "—")}");

		var avisos = new List<string>();

		if (FueraDeOrden > 0)
			avisos.Add($"{FueraDeOrden} velas repetidas o fuera de orden ignoradas");

		if (Liquidez.SesionesIncompletas > 0)
			avisos.Add($"{Liquidez.SesionesIncompletas} sesiones incompletas no usadas como referencia");

		if (Liquidez.PivotesSinRango > 0)
			avisos.Add($"{Liquidez.PivotesSinRango} pivotes internos sin rango 1H identificado (no usados)");

		s.Add($"Advertencias: {(avisos.Count == 0 ? "ninguna" : string.Join(" · ", avisos))}", avisos.Count == 0 ? TonoLinea.Ok : TonoLinea.Aviso);
		s.Add("Umbrales propuestos (no validados): " + string.Join(" · ", Cfg.ParametrosPropuestos()), TonoLinea.Gris);
		return s;
	}

	private SeccionReporte SeccionEstadistica()
	{
		var s = new SeccionReporte { Titulo = "ESTADÍSTICA (SIMULADA, NO ES UN REGISTRO DE OPERACIONES REALES)" };

		if (!Cfg.SimularResultados)
		{
			s.Add("Simulación desactivada en los ajustes.", TonoLinea.Gris);
			return s;
		}

		foreach (var e in Simulacion.Desglose())
			s.Add(e.Resumen(), e.Nombre == "TOTAL" ? TonoLinea.Titulo : TonoLinea.Normal);

		var total = Simulacion.Calcular("TOTAL");

		if (total.Operaciones > 0)
			s.Add($"Ambiguas (SL y TP en la misma vela, contadas como SL): {total.Ambiguas} · abiertas: {total.Abiertas} · MFE medio {total.MfeMedio:0.0} / MAE medio {total.MaeMedio:0.0} ticks", TonoLinea.Gris);

		s.Add("Una lista de condiciones cumplidas no es una probabilidad de ganar.", TonoLinea.Gris);
		return s;
	}

	private SeccionReporte SeccionEventos(Formato f)
	{
		var s = new SeccionReporte { Titulo = "EVENTOS" };
		var eventos = new List<(DateTime Utc, string Texto, TonoLinea Tono)>();

		eventos.AddRange(Estructura.Eventos.TakeLast(5).Select(e => (e.Utc, $"Estructura 1H: {e.Tipo} {e.Sesgo.Texto()} (nivel {Precio(e.NivelRoto)})", TonoLinea.Normal)));
		eventos.AddRange(Lvn.Mapas.TakeLast(3).Select(m => (m.CreadoUtc, $"Mapa LVN versión {m.Version}: {m.Zonas.Count(z => z.Prioridad == PrioridadLvn.Alta)} amarillos, {m.Zonas.Count(z => z.Prioridad == PrioridadLvn.Secundaria)} naranjas", TonoLinea.Amarillo)));
		eventos.AddRange(Barridos.TakeLast(8).Select(b => (b.ConocidoUtc, $"Barrido {b.Id} {b.Direccion.Texto()} {b.Tipo.Texto()} · {b.Principal.Etiqueta} · {b.Motivo}", TonoLinea.Gris)));
		eventos.AddRange(Senales.TakeLast(5).Select(x => (x.ConfirmadaUtc, $"SEÑAL {x.Id} {x.Titulo} · {x.Variante} · entrada {Precio(x.Plan.Entrada)}", x.Tipo == TipoLiquidez.Externa ? TonoLinea.Blanco : TonoLinea.Naranja)));
		eventos.AddRange(SetupsBloqueados.TakeLast(5).Select(b => (b.Vela.FinUtc, $"Setup {b.Modelo} {b.Direccion.Texto()} bloqueado: {b.Motivo}", TonoLinea.Fallo)));

		foreach (var (utc, texto, tono) in eventos.OrderByDescending(e => e.Utc).Take(15))
			s.Add($"{f.H(utc)} · {texto}", tono);

		if (eventos.Count == 0)
			s.Add("Sin eventos todavía.", TonoLinea.Gris);

		return s;
	}

	// --- Exportación ---------------------------------------------------------------------------------------

	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	/// <summary>Instantánea JSON del motor (señales y motivos). No es un backtest ni un registro de operaciones reales.</summary>
	public string ExportarJson(DateTime ahoraUtc)
	{
		lock (_sync)
		{
			object Vela(Vela v) => new
			{
				inicioUtc = v.InicioUtc, v.Apertura, v.Maximo, v.Minimo, v.Cierre, v.Volumen, v.Delta,
				cvd = v.CvdCierre, cvdParcial = v.CvdParcial, footprintCompleto = v.FootprintCompleto,
				poc = v.Huella?.Poc, val = v.Huella?.Val, vah = v.Huella?.Vah
			};

			object Plan(PlanRiesgo p) => new
			{
				p.Entrada, p.Sl, p.Tp, p.AnclajeSl, p.DistanciaSlTicks, objetivoLvn = p.Objetivo?.Id, p.Cantidad,
				p.RiesgoPorContrato, p.RiesgoTotal, p.RrNeto, bloqueo = p.Bloqueo
			};

			var datos = new
			{
				aviso = "Instantánea del motor de análisis: no es un backtest ni un registro de operaciones reales.",
				generadoUtc = ahoraUtc,
				instrumento = Cfg.Instrumento,
				tickSize = Cfg.TamanoTick,
				revision = Revision,
				ultimaVelaCerradaUtc = UltimaVelaUtc,
				ultimoDatoUtc = UltimoDatoUtc,
				calidad = new
				{
					velas1M = Velas1M.Count, huecos = Huecos, sinFootprint = VelasSinHuella, fueraDeOrden = FueraDeOrden,
					descartadas5M = A5.Descartadas, descartadas15M = A15.Descartadas, descartadas1H = A60.Descartadas, cvd = Cvd.Estado
				},
				estructura = new
				{
					sesgo = Estructura.Sesgo.Texto(),
					swingProtegido = Estructura.SwingProtegido?.Precio,
					desdeUtc = Estructura.DesdeUtc,
					condicionCambio = Estructura.CondicionCambio(Precio),
					eventos = Estructura.Eventos.TakeLast(20).Select(e => new { e.Utc, e.Tipo, sesgo = e.Sesgo.Texto(), e.NivelRoto, e.SwingProtegido }),
					lecturas = new { m1 = Lectura1M.Texto(), m5 = Lectura5M.Texto(), m15 = Lectura15M.Texto(), h1 = Lectura1H.Texto() }
				},
				mapasLvn = Lvn.Mapas.TakeLast(5).Select(m => new
				{
					m.Version, m.CreadoUtc, m.FinUtc, m.Referencia, m.BandaInferior, m.BandaSuperior,
					zonas = m.Zonas.Select(z => new
					{
						z.Id, prioridad = z.Prioridad.Texto(), z.Puesto, z.Inferior, z.Superior, z.Centro, z.Profundidad, z.Puntuacion,
						z.Reacciones, z.ReaccionesSoporte, z.ReaccionesResistencia, z.SesionesConReaccion, z.Origenes, z.ReconocidaUtc
					})
				}),
				fvg = FvgDetector.Fvgs.TakeLast(30).Select(x => new { x.Id, direccion = x.Direccion.Texto(), x.Inferior, x.Superior, formadoUtc = x.FormadoUtc, x.Activo, x.InvalidadoUtc }),
				senales = Senales.Select(x => new
				{
					x.Id, x.Modelo, x.Variante, direccion = x.Direccion.Texto(), liquidez = x.Tipo.Texto(), x.EventoId, x.Referencia,
					confirmadaUtc = x.ConfirmadaUtc, marcoGatillo = x.VelaGatillo.Marco.Texto(), zonaTrabajo = x.ZonaTrabajo?.Id, fvg = x.Fvg?.Id,
					plan = Plan(x.Plan),
					resultadoSimulado = x.Resultado is { } r ? new { r.Abierta, r.SalidaUtc, r.PrecioSalida, r.MotivoSalida, r.Ambigua, r.ResultadoUsd, r.R } : null
				}),
				setupsBloqueados = SetupsBloqueados.TakeLast(50).Select(b => new { b.EventoId, b.Modelo, direccion = b.Direccion.Texto(), liquidez = b.Tipo.Texto(), velaUtc = b.Vela.InicioUtc, b.Motivo }),
				oportunidades = Oportunidades.TakeLast(30).Select(o => new
				{
					evento = o.E.Id, direccion = o.E.Direccion.Texto(), liquidez = o.E.Tipo.Texto(), referencia = o.E.Principal.Etiqueta,
					nivel = o.E.Nivel, extremo = o.E.Extremo, o.E.PenetracionTicks, barridoUtc = o.E.VelaBarrido.InicioUtc,
					estadoEvento = o.E.Estado.ToString(), motivoEvento = o.E.Motivo, absorcion = o.E.Absorcion.Texto(),
					m1 = o.M1.Texto(), motivoM1 = o.MotivoM1, m2 = o.M2.Texto(), motivoM2 = o.MotivoM2
				}),
				velas1M = Velas1M.TakeLast(300).Select(Vela),
				velas5M = A5.Velas.TakeLast(120).Select(Vela),
				ema = new { periodo = Cfg.PeriodoEma, marco = Cfg.MarcoEma.ToString(), m1 = Ema1M.Valor, m5 = Ema5M.Valor },
				divergencias = Divergencias.Divergencias.TakeLast(20).Select(d => new { marco = d.Marco.Texto(), d.Alcista, p1 = d.P1.Precio, p2 = d.P2.Precio, confirmadaUtc = d.ConfirmadaUtc }),
				directrices = Oportunidades.TakeLast(30).SelectMany(o => new[] { o.DirectrizM1, o.DirectrizM2 }).Where(l => l is not null).Distinct()
					.Select(l => new { p1 = l!.P1.Precio, p1Utc = l.P1.ExtremoUtc, p2 = l.P2.Precio, p2Utc = l.P2.ExtremoUtc, disponibleUtc = l.DisponibleUtc, l.RotaUtc }),
				gestion = Simulacion.Todas.TakeLast(10).Select(s => new { senal = s.Senal.Id, s.Abierta, s.StopActual, s.MotivoSalida, s.Advertencia }),
				estadistica = Simulacion.Desglose().Select(e => e.Resumen())
			};

			return JsonSerializer.Serialize(datos, OpcionesJson);
		}
	}

	/// <summary>Diario de las operaciones simuladas, para calcular estadística fuera del indicador.</summary>
	public string ExportarCsv(ZonaPresentacion zona)
	{
		lock (_sync)
			return Simulacion.Csv(utc => Calendario.APresentacion(utc, zona).ToString("yyyy-MM-dd HH:mm"));
	}
}
