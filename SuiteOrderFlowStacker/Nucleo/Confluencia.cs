namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

public enum ModoObjetivo
{
	[Display(Name = "Siguiente nivel (POC / VAH / VAL / LVN)")]
	NextLevel,

	[Display(Name = "R:R fijo (TargetRewardRisk)")]
	FixedRewardRisk
}

/// <summary>Parámetros del AbsorptionMaster: pesos, setups, gestión y filtros (nombres de la especificación).</summary>
public sealed class ParametrosMaster
{
	public int WeightIceberg { get; set; } = 30;
	public int WeightHighProbBonus { get; set; } = 10;
	public int WeightProfileLevel { get; set; } = 20;
	public int WeightSweepOrStops { get; set; } = 20;
	public int WeightImbalance { get; set; } = 15;
	public int WeightDeltaShift { get; set; } = 15;
	public int MinSignalScore { get; set; } = 65;
	public int ProfileTolTicks { get; set; } = 4;
	public bool EnableSetupA { get; set; } = true;
	public bool EnableSetupB { get; set; } = true;
	public bool EnableSetupC { get; set; } = true;
	public bool EnableSetupD { get; set; } = true;

	/// <summary>Setup C: antigüedad máxima de la zona de stacked imbalance en el LVN.</summary>
	public int SetupCZoneMaxAgeSeconds { get; set; } = 300;

	public int EntryConfirmTicks { get; set; } = 4;
	public int StopBeyondTicks { get; set; } = 3;
	public ModoObjetivo TargetMode { get; set; } = ModoObjetivo.NextLevel;
	public decimal TargetRewardRisk { get; set; } = 2.0m;
	public decimal MinRewardRisk { get; set; } = 1.5m;
	public int NewsBlackoutMinutes { get; set; } = 2;
	public string NewsTimes { get; set; } = "08:30;10:00;14:00";
	public int CooldownSeconds { get; set; } = 60;

	/// <summary>Sin señal de GIRO con el iceberg contra la tendencia y sin barrido previo (12.6).</summary>
	public bool BlockAgainstTrend { get; set; } = true;
}

/// <summary>Delta agresor de los trades en vivo, para el "giro del delta".</summary>
public sealed class MedidorDelta
{
	private readonly List<(DateTime Utc, decimal Delta)> _trades = new();

	public void Trade(TradeFlujo t)
	{
		if (t.Agresor is not { } lado || t.Volumen <= 0)
			return;

		_trades.Add((t.Utc, t.Volumen * lado.Signo()));
		var corte = t.Utc - TimeSpan.FromMinutes(10);

		if (_trades.Count > 0 && _trades[0].Utc < corte)
		{
			var n = _trades.FindIndex(x => x.Utc >= corte);
			_trades.RemoveRange(0, n < 0 ? _trades.Count : n);
		}
	}

	public void Reiniciar() => _trades.Clear();

	public decimal Suma(DateTime desde, DateTime hasta) => _trades.Where(x => x.Utc > desde && x.Utc <= hasta).Sum(x => x.Delta);

	/// <summary>
	/// Giro a favor de ese lado: el delta del último tercio de la ventana (10 s con EventContextSeconds = 30) va a favor y
	/// el de la ventana completa anterior iba en contra (p. ej. ventas absorbidas y después compras).
	/// </summary>
	public bool Giro(Lado lado, DateTime utc, int segundos)
	{
		var v = TimeSpan.FromSeconds(Math.Max(1, segundos));
		var corto = TimeSpan.FromTicks(v.Ticks / 3);
		var reciente = Suma(utc - corto, utc) * lado.Signo();
		var anterior = Suma(utc - corto - v, utc - corto) * lado.Signo();
		return reciente > 0 && anterior < 0;
	}
}

/// <summary>Línea del checklist: Marca "OK" (cumple), "NO" (no cumple) o ".." (pendiente).</summary>
public readonly record struct LineaChecklist(string Marca, string Texto, int Puntos);

/// <summary>Setup en evaluación (o ya emitido) con su puntuación, gestión y checklist.</summary>
public sealed class Candidata
{
	public char Setup;
	public Lado Lado;
	public string Sesgo = "GIRO";
	public decimal Referencia, Extremo;
	public string Clave = "";
	public bool Lista;
	public string Esperando = "";
	public Iceberg? Iceberg;
	public EventoFlujo? Previo;
	public ZonaImbalance? Zona;
	public NivelReferencia? Nivel;

	public int Score;
	public decimal Entrada, Stop, Objetivo, RewardRisk;
	public string NombreObjetivo = "";
	public string? Bloqueo;
	public List<LineaChecklist> Checklist = new();

	public string Direccion => Lado == Lado.Compra ? "LARGO" : "CORTO";
}

/// <summary>
/// AbsorptionMaster: ejecuta todos los motores y solo da UNA señal cuando hay confluencia (setups A-D), con su
/// puntuación 0-100 (ponderación de reglas, NO una probabilidad), la gestión sugerida y un checklist.
/// </summary>
public sealed class MotorConfluencia
{
	private readonly RegistroEventos _eventos;
	private readonly MotorContexto _contexto;
	private readonly MotorImbalance _imbalance;
	private readonly MotorIceberg _icebergs;
	private readonly MotorStops _stops;
	private readonly MotorBarridos _barridos;
	private readonly MedidorDelta _delta;
	private readonly HashSet<string> _usadas = new();
	private readonly Dictionary<Lado, DateTime> _ultimaSenal = new();

	public MotorConfluencia(RegistroEventos eventos, MotorContexto contexto, MotorImbalance imbalance, MotorIceberg icebergs, MotorStops stops,
		MotorBarridos barridos, MedidorDelta delta)
	{
		(_eventos, _contexto, _imbalance, _icebergs, _stops, _barridos, _delta) = (eventos, contexto, imbalance, icebergs, stops, barridos, delta);
	}

	public ParametrosMaster P { get; set; } = new();

	public decimal Tick { get; set; } = 0.25m;

	public int EventContextSeconds { get; set; } = 30;

	public ClasificadorSesion Sesiones { get; set; } = new();

	/// <summary>Última señal emitida (con su checklist) y el mejor setup en espera.</summary>
	public Candidata? UltimaSenal { get; private set; }

	public EventoFlujo? EventoUltimaSenal { get; private set; }

	public Candidata? Pendiente { get; private set; }

	public void Reiniciar()
	{
		_usadas.Clear();
		_ultimaSenal.Clear();
		(UltimaSenal, EventoUltimaSenal, Pendiente) = (null, null, null);
	}

	#region Evaluación

	public void Evaluar(DateTime utc)
	{
		var candidatas = new List<Candidata>();

		foreach (var lado in new[] { Lado.Compra, Lado.Venta })
			candidatas.AddRange(Setups(lado, utc));

		foreach (var c in candidatas)
			Puntuar(c, utc);

		// Una señal por iceberg / zona (Clave): B antes que A (máxima calidad), y la de mayor puntuación.
		foreach (var c in candidatas.Where(c => c.Lista && c.Bloqueo is null && c.Score >= P.MinSignalScore)
			         .OrderBy(c => c.Setup == 'B' ? 0 : 1).ThenByDescending(c => c.Score))
		{
			if (_usadas.Contains(c.Clave))
				continue;

			if (_ultimaSenal.TryGetValue(c.Lado, out var ultima) && (utc - ultima).TotalSeconds < Math.Max(0, P.CooldownSeconds))
				continue;

			Emitir(c, utc);
		}

		Pendiente = candidatas.Where(c => !_usadas.Contains(c.Clave) && (!c.Lista || c.Bloqueo is not null || c.Score < P.MinSignalScore))
			.Where(c => !(_ultimaSenal.TryGetValue(c.Lado, out var u) && (utc - u).TotalSeconds < Math.Max(0, P.CooldownSeconds)))
			.OrderByDescending(c => c.Score)
			.FirstOrDefault();
	}

	private IEnumerable<Candidata> Setups(Lado lado, DateTime utc)
	{
		var w = TimeSpan.FromSeconds(Math.Max(1, EventContextSeconds));
		var tol = Math.Max(0, P.ProfileTolTicks) * Tick;
		var perfil = _contexto.NivelesPerfil();
		var espera = $"Iceberg AGUANTANDO (+{_icebergs.P.HoldConfirmTicks} ticks)";

		foreach (var i in _icebergs.Icebergs.Where(i => i.Lado == lado && i.Vivo))
		{
			// B: barrido de liquidez o toma de stops del agresor contrario + iceberg que absorbe en la ventana.
			if (P.EnableSetupB && PrevioGiro(lado, i.InicioUtc - w, i.UltimaActividadUtc + TimeSpan.FromSeconds(1)) is { } previo)
			{
				yield return new Candidata
				{
					Setup = 'B', Lado = lado, Sesgo = "GIRO", Referencia = i.Precio, Iceberg = i, Previo = previo,
					Extremo = lado == Lado.Compra ? Math.Min(i.Precio, previo.PrecioFin ?? i.Precio) : Math.Max(i.Precio, previo.PrecioFin ?? i.Precio),
					Clave = "I|" + i.Evento.Id, Lista = i.Estado == "AGUANTANDO", Esperando = espera
				};

				continue;
			}

			// A: iceberg AGUANTANDO en LVN / VAH / VAL.
			if (P.EnableSetupA && perfil.Where(n => n.Nombre is "LVN" or "VAH" or "VAL").FirstOrDefault(n => Math.Abs(n.Precio - i.Precio) <= tol) is { Nombre: not null } nivel)
			{
				yield return new Candidata
				{
					Setup = 'A', Lado = lado, Sesgo = "GIRO", Referencia = i.Precio, Extremo = i.Precio, Iceberg = i, Nivel = nivel,
					Clave = "I|" + i.Evento.Id, Lista = i.Estado == "AGUANTANDO", Esperando = espera
				};
			}
		}

		// C: el precio toca un LVN + stacked imbalance en contra del movimiento (a favor de la señal) + giro del delta.
		if (P.EnableSetupC && _contexto.UltimoPrecio > 0)
		{
			var precio = _icebergs.UltimoPrecio > 0 ? _icebergs.UltimoPrecio : _contexto.UltimoPrecio;
			var edad = TimeSpan.FromSeconds(Math.Max(1, P.SetupCZoneMaxAgeSeconds));

			foreach (var lvn in perfil.Where(n => n.Nombre == "LVN" && Math.Abs(n.Precio - precio) <= tol * 2))
			{
				foreach (var z in _imbalance.Zonas.Where(z => z.Activa && z.Lado == lado && z.Contiene(lvn.Precio, tol) && utc - z.InicioUtc <= edad))
				{
					var giro = _delta.Giro(lado, utc, EventContextSeconds);

					yield return new Candidata
					{
						Setup = 'C', Lado = lado, Sesgo = "GIRO", Referencia = lvn.Precio, Zona = z, Nivel = lvn,
						Extremo = lado == Lado.Compra ? Math.Min(z.Bajo, lvn.Precio) : Math.Max(z.Alto, lvn.Precio),
						Clave = "C|" + z.Evento.Id, Lista = giro, Esperando = "giro del delta a favor"
					};
				}
			}
		}

		// D: iceberg del lado contrario ROTO + barrido de ruptura a favor de la señal: CONTINUIDAD.
		if (P.EnableSetupD)
		{
			foreach (var i in _icebergs.Icebergs.Where(i => i.Lado == lado.Contrario() && i.Estado == "ROTO" && utc - i.Evento.CambioUtc <= w + w))
			{
				var barrido = _barridos.Barridos.Where(b => b.Lado == lado && b.Tipo is "RUPTURA" or "EVALUANDO")
					.Where(b => Math.Abs((b.Utc - i.Evento.CambioUtc).TotalSeconds) <= w.TotalSeconds)
					.OrderByDescending(b => b.Tipo == "RUPTURA").ThenByDescending(b => b.Utc).FirstOrDefault();

				yield return new Candidata
				{
					Setup = 'D', Lado = lado, Sesgo = "CONTINUIDAD", Referencia = i.Precio, Extremo = i.Precio, Iceberg = i, Previo = barrido?.Evento,
					Clave = "D|" + i.Evento.Id, Lista = barrido?.Tipo == "RUPTURA", Esperando = "barrido de RUPTURA"
				};
			}
		}
	}

	/// <summary>Barrido de liquidez (o contra iceberg) o toma de stops (no continuación) de ese agresor entre esas horas.</summary>
	private EventoFlujo? PrevioGiro(Lado ladoSenal, DateTime desde, DateTime hasta)
	{
		var agresor = ladoSenal.Contrario();

		var barrido = _barridos.Barridos.Where(b => b.Lado == agresor && b.Tipo is "LIQUIDEZ" or "CONTRA ICEBERG" or "EVALUANDO" && b.Utc >= desde && b.Utc <= hasta)
			.Select(b => b.Evento);

		var stops = _stops.Cascadas.Where(c => c.Lado == agresor && c.Estado is "TRAMPA" or "EVALUANDO" && c.Utc >= desde && c.Utc <= hasta)
			.Select(c => c.Evento);

		return barrido.Concat(stops).OrderByDescending(e => e.Utc).FirstOrDefault();
	}

	#endregion

	#region Puntuación y gestión

	private void Puntuar(Candidata c, DateTime utc)
	{
		var tol = Math.Max(0, P.ProfileTolTicks) * Tick;
		var w = TimeSpan.FromSeconds(Math.Max(1, EventContextSeconds));
		var lista = new List<LineaChecklist>();
		var puntos = 0;

		void Linea(bool ok, string texto, int peso)
		{
			lista.Add(new LineaChecklist(ok ? "OK" : "NO", texto, ok ? peso : 0));
			puntos += ok ? peso : 0;
		}

		// Iceberg válido (+bonus de alta probabilidad).
		var iceberg = c.Iceberg ?? _icebergs.Icebergs.FirstOrDefault(i => i.Vivo && i.Lado == c.Lado && Math.Abs(i.Precio - c.Referencia) <= tol);
		var bonus = iceberg?.AltaProbabilidad == true ? P.WeightHighProbBonus : 0;
		Linea(iceberg is not null, iceberg?.AltaProbabilidad == true ? "Iceberg válido (* alta prob.)" : "Iceberg válido", P.WeightIceberg + bonus);

		// Barrido / stops previos (para D: el barrido de ruptura a favor).
		var previo = c.Previo ?? (c.Setup == 'D' ? null : PrevioGiro(c.Lado, utc - w - w, utc));
		Linea(previo is not null && (c.Setup != 'D' || previo.Estado == "RUPTURA"),
			c.Setup == 'D' ? "Barrido de ruptura" : $"Barrido / stops previos{(previo is { Contexto.Length: > 0 } p ? $" ({p.Contexto})" : "")}", P.WeightSweepOrStops);

		// Stacked imbalance del mismo lado que la señal (para D, del lado del barrido).
		var zona = c.Zona ?? _imbalance.Zonas.FirstOrDefault(z => z.Activa && z.Lado == c.Lado && z.Contiene(c.Referencia, tol));
		Linea(zona is not null, $"Stacked imbalance {c.Lado.Texto().ToLowerInvariant()}", P.WeightImbalance);

		// Giro del delta a favor.
		Linea(_delta.Giro(c.Lado, utc, EventContextSeconds), "Giro del delta a favor", P.WeightDeltaShift);

		// En nivel de perfil.
		var nivel = c.Nivel ?? _contexto.NivelesPerfil().FirstOrDefault(n => Math.Abs(n.Precio - c.Referencia) <= tol);
		Linea(nivel is { Nombre: not null }, $"En nivel de perfil{(nivel is { Nombre: { } nombre } ? $" ({nombre})" : " (LVN/VAH/VAL/POC)")}", P.WeightProfileLevel);

		c.Score = Math.Min(100, puntos);
		Gestion(c);

		// Filtros (12.6).
		var blackout = EnBlackout(utc);
		lista.Add(new LineaChecklist(blackout ? "NO" : "OK", "Fuera de blackout de noticias", 0));

		var rrOk = c.RewardRisk >= P.MinRewardRisk;
		lista.Add(new LineaChecklist(rrOk ? "OK" : "NO", $"R:R >= {P.MinRewardRisk.ToString("0.0#", CultureInfo.InvariantCulture)}", 0));

		var contraTendencia = P.BlockAgainstTrend && c.Sesgo == "GIRO" && previo is null && _contexto.Tendencia == -c.Lado.Signo();

		if (P.BlockAgainstTrend && c.Sesgo == "GIRO")
			lista.Add(new LineaChecklist(contraTendencia ? "NO" : "OK", "Sin tendencia en contra (o con barrido previo)", 0));

		if (!c.Lista)
			lista.Add(new LineaChecklist("..", "Esperando: " + c.Esperando, 0));

		c.Bloqueo = blackout ? "blackout de noticias"
			: !rrOk ? $"R:R {c.RewardRisk.ToString("0.0", CultureInfo.InvariantCulture)} < {P.MinRewardRisk.ToString("0.0#", CultureInfo.InvariantCulture)}"
			: contraTendencia ? "iceberg contra la tendencia sin barrido previo"
			: null;

		c.Checklist = lista;
	}

	/// <summary>Entrada tras EntryConfirmTicks a favor, stop StopBeyondTicks más allá del iceberg/extremo, objetivo y R:R.</summary>
	private void Gestion(Candidata c)
	{
		var s = c.Lado.Signo();
		c.Entrada = c.Referencia + s * Math.Max(0, P.EntryConfirmTicks) * Tick;
		c.Stop = c.Extremo - s * Math.Max(0, P.StopBeyondTicks) * Tick;
		var riesgo = Math.Abs(c.Entrada - c.Stop);

		if (riesgo <= 0)
			riesgo = Tick;

		decimal? objetivo = null;

		if (P.TargetMode == ModoObjetivo.NextLevel)
		{
			var siguiente = _contexto.NivelesPerfil().Where(n => (n.Precio - c.Entrada) * s >= Tick).OrderBy(n => Math.Abs(n.Precio - c.Entrada)).FirstOrDefault();

			if (siguiente.Nombre is not null)
			{
				objetivo = siguiente.Precio;
				c.NombreObjetivo = siguiente.Nombre;
			}
		}

		if (objetivo is null)
		{
			var rr = Math.Max(0.5m, P.TargetRewardRisk);
			objetivo = PrecioMath.Redondear(c.Entrada + s * rr * riesgo, Tick);
			c.NombreObjetivo = $"{rr.ToString("0.#", CultureInfo.InvariantCulture)}R";
		}

		c.Objetivo = objetivo.Value;
		c.RewardRisk = Math.Round(Math.Abs(c.Objetivo - c.Entrada) / riesgo, 2);
	}

	/// <summary>±NewsBlackoutMinutes alrededor de las horas de NewsTimes (hora de Nueva York).</summary>
	public bool EnBlackout(DateTime utc)
	{
		var hora = Zonas.AEt(utc).TimeOfDay;
		var margen = TimeSpan.FromMinutes(Math.Max(0, P.NewsBlackoutMinutes));

		foreach (var texto in (P.NewsTimes ?? "").Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (TimeSpan.TryParseExact(texto.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var noticia) && (hora - noticia).Duration() <= margen)
				return true;
		}

		return false;
	}

	#endregion

	#region Emisión

	private void Emitir(Candidata c, DateTime utc)
	{
		_usadas.Add(c.Clave);
		_ultimaSenal[c.Lado] = utc;

		var e = _eventos.Crear("Master", "Signal", c.Lado, utc, c.Entrada, _icebergs.ModoMboActivo ? ModoDatos.MBO : ModoDatos.Inferido, Sesiones.De(utc));
		e.PrecioFin = c.Objetivo;
		e.StopLoss = c.Stop;
		e.Score = c.Score;
		e.Estado = "EMITIDA";
		e.Sesgo = c.Sesgo;
		e.Ratio = c.RewardRisk;
		e.Contexto = $"Setup{c.Setup}";
		e.Texto = TextoSenal(c, utc);

		(UltimaSenal, EventoUltimaSenal) = (c, e);
		_eventos.Publicar(e, utc);
	}

	public string TextoSenal(Candidata c, DateTime utc) =>
		$"SEÑAL {c.Sesgo} {c.Direccion} | {c.Score}/100 | Setup {c.Setup} | Entrada {P2(c.Entrada)} | SL {P2(c.Stop)} | " +
		$"TP {P2(c.Objetivo)} ({c.NombreObjetivo}) | R:R {c.RewardRisk.ToString("0.0", CultureInfo.InvariantCulture)} | {Textos.Hora(utc)}";

	public string TextoPendiente(Candidata c)
	{
		var motivo = c.Bloqueo is { } b ? "bloqueada: " + b
			: !c.Lista ? "esperando: " + c.Esperando
			: $"puntuación < {P.MinSignalScore}";

		return $"POSIBLE SETUP {c.Setup} {c.Direccion} | {c.Score}/100 | {motivo}";
	}

	private string P2(decimal p) => Textos.Precio(p, Tick);

	#endregion
}
