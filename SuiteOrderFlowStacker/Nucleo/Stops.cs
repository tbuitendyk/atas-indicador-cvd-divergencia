namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Parámetros del grupo "1. Stops" (SwingLengthBars, RoundNumberStep e IbMinutes están en el contexto).</summary>
public sealed class ParametrosStops
{
	public int MinCascadeContractsRth { get; set; } = 100;
	public int MinCascadeContractsEth { get; set; } = 50;
	public int CascadeMaxMs { get; set; } = 300;
	public int MinCascadeLevels { get; set; } = 3;
	public bool ShowProbableZones { get; set; } = true;
	public int ZoneToleranceTicks { get; set; } = 4;
	public int StopReclaimSeconds { get; set; } = 60;

	/// <summary>Solo cascadas junto a una zona probable (si no, también las de "sin zona").</summary>
	public bool RequireZone { get; set; } = true;
}

/// <summary>Cascada de stops disparada: ráfaga agresiva en una dirección, en milisegundos, atravesando niveles.</summary>
public sealed class CascadaStops
{
	public EventoFlujo Evento = null!;
	public Lado Lado;
	public decimal Desde, Hasta, Min, Max, Volumen;
	public NivelReferencia? Zona;
	public DateTime Utc;
	public string Estado = "EVALUANDO";
	public double? SegundosVuelta;

	/// <summary>Precio de referencia para "volver dentro": la zona tomada (o el inicio de la cascada sin zona).</summary>
	public decimal Referencia => Zona?.Precio ?? Desde;
}

/// <summary>
/// StopsTrackerPro. Los stops NO se ven antes de dispararse: el mapa muestra zonas PROBABLES (PDH/PDL, IB, overnight,
/// swings y números redondos) y el motor detecta la cascada cuando ya ocurrió. Después decide TRAMPA (el precio vuelve
/// dentro en StopReclaimSeconds: nuevo impulso contrario) o CONTINUACIÓN (sigue fuera: impulso a favor de la cascada).
/// </summary>
public sealed class MotorStops
{
	private sealed class Rafaga
	{
		public DateTime Inicio, Ultimo;
		public decimal Primero, UltimoPrecio, Min, Max, Volumen;
	}

	private readonly RegistroEventos _eventos;
	private readonly Dictionary<Lado, Rafaga> _rafagas = new();
	private readonly Dictionary<string, DateTime> _avisadas = new();

	public MotorStops(RegistroEventos eventos) => _eventos = eventos;

	public ParametrosStops P { get; set; } = new();

	public decimal Tick { get; set; } = 0.25m;

	public ClasificadorSesion Sesiones { get; set; } = new();

	public Func<IReadOnlyList<NivelReferencia>>? Zonas { get; set; }

	public List<CascadaStops> Cascadas { get; } = new();

	public decimal UltimoPrecio { get; private set; }

	public ModoDatos Modo { get; set; } = ModoDatos.Inferido;

	public void Reiniciar()
	{
		_rafagas.Clear();
		_avisadas.Clear();
		Cascadas.Clear();
	}

	public void Trade(TradeFlujo t)
	{
		UltimoPrecio = t.Precio;
		Tiempo(t.Utc);

		if (t.Agresor is not { } lado || t.Volumen <= 0)
			return;

		if (_rafagas.TryGetValue(lado, out var r) && (t.Utc - r.Inicio).TotalMilliseconds <= Math.Max(1, P.CascadeMaxMs))
		{
			r.Ultimo = t.Utc;
			r.UltimoPrecio = t.Precio;
			r.Min = Math.Min(r.Min, t.Precio);
			r.Max = Math.Max(r.Max, t.Precio);
			r.Volumen += t.Volumen;
		}
		else
		{
			if (r is not null)
				Evaluar(lado, r);

			_rafagas[lado] = new Rafaga { Inicio = t.Utc, Ultimo = t.Utc, Primero = t.Precio, UltimoPrecio = t.Precio, Min = t.Precio, Max = t.Precio, Volumen = t.Volumen };
		}

		Aproximacion(t.Precio, t.Utc);
	}

	/// <summary>Reloj: cierra las ráfagas vencidas y resuelve TRAMPA / CONTINUACIÓN.</summary>
	public void Tiempo(DateTime utc)
	{
		foreach (var (lado, r) in _rafagas.ToList())
		{
			if ((utc - r.Inicio).TotalMilliseconds > Math.Max(1, P.CascadeMaxMs))
			{
				_rafagas.Remove(lado);
				Evaluar(lado, r);
			}
		}

		foreach (var c in Cascadas.Where(c => c.Estado == "EVALUANDO"))
			Resolver(c, UltimoPrecio, utc);
	}

	private void Evaluar(Lado lado, Rafaga r)
	{
		var niveles = (int)Math.Round((r.Max - r.Min) / Tick) + 1;
		var minimo = ClasificadorSesion.Rth(r.Inicio) ? P.MinCascadeContractsRth : P.MinCascadeContractsEth;

		// La cascada avanza en su dirección (las ventas bajan, las compras suben).
		var avanza = lado == Lado.Venta ? r.UltimoPrecio < r.Primero : r.UltimoPrecio > r.Primero;

		if (niveles < Math.Max(2, P.MinCascadeLevels) || r.Volumen < minimo || !avanza)
			return;

		var zona = ZonaTomada(lado, r);

		if (zona is null && P.RequireZone)
			return;

		var c = new CascadaStops
		{
			Lado = lado,
			Desde = r.Primero,
			Hasta = r.UltimoPrecio,
			Min = r.Min,
			Max = r.Max,
			Volumen = r.Volumen,
			Zona = zona,
			Utc = r.Inicio
		};

		c.Evento = _eventos.Crear("Stops", "StopRun", lado, r.Inicio, r.Primero, Modo, Sesiones.De(r.Inicio));
		c.Evento.PrecioFin = lado == Lado.Venta ? r.Min : r.Max;
		c.Evento.Volumen = r.Volumen;
		c.Evento.Contexto = zona?.Nombre ?? "";
		Cascadas.Add(c);
		Texto(c);
		_eventos.Publicar(c.Evento, r.Ultimo);

		if (Cascadas.Count > 300)
			Cascadas.RemoveRange(0, Cascadas.Count - 300);
	}

	/// <summary>Zona probable atravesada (o tocada a ZoneToleranceTicks) por la cascada; la más cercana al inicio.</summary>
	private NivelReferencia? ZonaTomada(Lado lado, Rafaga r)
	{
		if (Zonas?.Invoke() is not { Count: > 0 } zonas)
			return null;

		var tolerancia = Math.Max(0, P.ZoneToleranceTicks) * Tick;
		var candidatas = zonas.Where(z => z.Precio >= r.Min - tolerancia && z.Precio <= r.Max + tolerancia).ToList();

		if (candidatas.Count == 0)
			return null;

		// Las ventas toman los stops de debajo de un mínimo y las compras los de encima de un máximo: primero esas.
		return candidatas.OrderBy(z => z.EsMaximo == (lado == Lado.Compra) ? 0 : 1).ThenBy(z => Math.Abs(z.Precio - r.Primero)).First();
	}

	private void Resolver(CascadaStops c, decimal precio, DateTime utc)
	{
		var segundos = (utc - c.Utc).TotalSeconds;
		var dentro = c.Lado == Lado.Venta ? precio > c.Referencia : precio < c.Referencia;

		if (dentro && segundos <= Math.Max(1, P.StopReclaimSeconds))
		{
			c.Estado = "TRAMPA";
			c.SegundosVuelta = Math.Round(segundos);
		}
		else if (segundos > Math.Max(1, P.StopReclaimSeconds))
		{
			c.Estado = "CONTINUACIÓN";
		}
		else
		{
			return;
		}

		Texto(c);
		_eventos.Publicar(c.Evento, utc);
	}

	/// <summary>Aviso de panel (no suena ni se graba): el precio se acerca a una zona probable de stops.</summary>
	private void Aproximacion(decimal precio, DateTime utc)
	{
		if (!P.ShowProbableZones || Zonas?.Invoke() is not { Count: > 0 } zonas)
			return;

		var tolerancia = Math.Max(1, P.ZoneToleranceTicks) * Tick;

		// Se rearma cuando el precio se aleja tres veces la tolerancia.
		foreach (var z in zonas.Where(z => Math.Abs(precio - z.Precio) > 3 * tolerancia))
			_avisadas.Remove(Clave(z));

		var cerca = zonas.Where(z => Math.Abs(precio - z.Precio) <= tolerancia && !_avisadas.ContainsKey(Clave(z))).ToList();

		if (cerca.Count == 0)
			return;

		foreach (var z in cerca)
			_avisadas[Clave(z)] = utc;

		// Se avisa la más cercana (con empate, la que no es un número redondo).
		var zona = cerca.OrderBy(z => Math.Abs(precio - z.Precio)).ThenBy(z => EsRedondo(z) ? 1 : 0).First();
		var ticks = (int)Math.Round(Math.Abs(precio - zona.Precio) / Tick);

		// Solo se guarda el último aviso (es una línea de estado del panel, no un evento).
		_eventos.Quitar(x => x.Tipo == "StopZone");
		var e = _eventos.Crear("Stops", "StopZone", zona.EsMaximo ? Lado.Compra : Lado.Venta, utc, zona.Precio, Modo, Sesiones.De(utc));
		e.Informativo = true;
		e.Texto = $"Zona probable de stops {NombreZona(zona)} a {ticks} ticks | {Textos.Hora(utc)}";
		_eventos.Publicar(e, utc);
	}

	private static string Clave(NivelReferencia z) => $"{z.Nombre}|{z.Precio}";

	private static bool EsRedondo(NivelReferencia z) => z.Nombre.EndsWith("R", StringComparison.Ordinal);

	private string NombreZona(NivelReferencia z) => EsRedondo(z) ? z.Nombre : $"{z.Nombre} {Textos.Precio(z.Precio, Tick)}";

	private void Texto(CascadaStops c)
	{
		var e = c.Evento;
		e.Estado = c.Estado;
		var zona = c.Zona is { } z ? (c.Lado == Lado.Venta ? "bajo " : "sobre ") + z.Nombre : "sin zona";
		var rango = $"{Textos.Precio(c.Max, Tick)}-{Textos.Precio(c.Min, Tick)}";
		var vuelta = c.SegundosVuelta is { } s ? $" (volvió en {s:0} s)" : "";

		// Dirección del nuevo impulso: contraria a la cascada si fue trampa; la misma si continúa.
		var impulso = c.Estado switch
		{
			"TRAMPA" => c.Lado == Lado.Venta ? " | IMPULSO ALCISTA" : " | IMPULSO BAJISTA",
			"CONTINUACIÓN" => c.Lado == Lado.Venta ? " | IMPULSO BAJISTA" : " | IMPULSO ALCISTA",
			_ => ""
		};

		e.Sesgo = impulso.Length > 0 ? impulso[3..] : "";
		e.Texto = $"STOPS TOMADOS {c.Lado.Texto()} | {c.Volumen:0}c | {rango} | {zona} | {c.Estado}{vuelta} | {Textos.Hora(c.Utc)}{impulso}";
	}

	/// <summary>Texto de las zonas probables para la cabecera del panel.</summary>
	public string TextoZonas(int maximo)
	{
		if (Zonas?.Invoke() is not { Count: > 0 } zonas || UltimoPrecio <= 0)
			return "Zonas probables: (esperando datos)";

		var cerca = zonas.OrderBy(z => Math.Abs(z.Precio - UltimoPrecio)).Take(Math.Max(1, maximo))
			.Select(NombreZona);

		return "Zonas probables: " + string.Join(" · ", cerca);
	}
}
