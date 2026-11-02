namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Parámetros del grupo "1. Sweeps". ReferenceLevels de la especificación = las cinco casillas Ref*.</summary>
public sealed class ParametrosBarridos
{
	public int MinSweepLevels { get; set; } = 4;
	public int MinSweepContracts { get; set; } = 50;
	public int SweepReclaimSeconds { get; set; } = 30;
	public int LevelToleranceTicks { get; set; } = 4;
	public bool RefPdhPdl { get; set; } = true;
	public bool RefIbhIbl { get; set; } = true;
	public bool RefOnhOnl { get; set; } = true;
	public bool RefSwings { get; set; } = true;
	public bool RefVahVal { get; set; } = true;

	/// <summary>Solo barridos en un nivel de referencia o contra un iceberg (si no, también "SIN NIVEL").</summary>
	public bool OnlyAtReferenceLevels { get; set; } = true;

	public bool Usa(string nombre) => nombre switch
	{
		"PDH" or "PDL" => RefPdhPdl,
		"IBH" or "IBL" => RefIbhIbl,
		"ONH" or "ONL" => RefOnhOnl,
		"swing" => RefSwings,
		"VAH" or "VAL" => RefVahVal,
		_ => false
	};
}

/// <summary>Orden agresiva reconstruida (los trozos en que el exchange la partió) que barrió varios niveles.</summary>
public sealed class Barrido
{
	public EventoFlujo Evento = null!;
	public Lado Lado;
	public decimal Desde, Hasta, Volumen;
	public int Niveles;
	public DateTime Utc;
	public NivelReferencia? Nivel;
	public Iceberg? ContraIceberg;

	/// <summary>EVALUANDO → LIQUIDEZ (vuelve dentro) / RUPTURA (sigue fuera); CONTRA ICEBERG; SIN NIVEL.</summary>
	public string Tipo = "EVALUANDO";

	public double? SegundosVuelta;
}

/// <summary>
/// SweepsTrackerPro. Con MBO agrupa los trades por AggressorExchangeOrderId (una orden = un barrido); sin MBO agrupa
/// los trades del mismo lado con el mismo milisegundo (modo INFERIDO). Clasifica: LIQUIDEZ (rompe un máximo/mínimo y
/// vuelve dentro en SweepReclaimSeconds), RUPTURA (se mantiene fuera) y CONTRA ICEBERG (choca con un iceberg vivo y no
/// avanza más de MaxDisplacementTicks: el setup de giro de máxima calidad).
/// </summary>
public sealed class MotorBarridos
{
	private sealed class Grupo
	{
		public Lado Lado;
		public DateTime Inicio, Ultimo;
		public decimal Primero, UltimoPrecio, Min, Max, Volumen;
		public bool Mbo;
	}

	private readonly RegistroEventos _eventos;
	private readonly Dictionary<long, Grupo> _porId = new();
	private Grupo? _inferido;

	public MotorBarridos(RegistroEventos eventos) => _eventos = eventos;

	public ParametrosBarridos P { get; set; } = new();

	public decimal Tick { get; set; } = 0.25m;

	public ClasificadorSesion Sesiones { get; set; } = new();

	public Func<IReadOnlyList<NivelReferencia>>? Niveles { get; set; }

	/// <summary>Icebergs vivos (los da el motor de icebergs) y su desplazamiento máximo.</summary>
	public Func<IEnumerable<Iceberg>>? Icebergs { get; set; }

	public int MaxDisplacementTicks { get; set; } = 2;

	public List<Barrido> Barridos { get; } = new();

	public decimal UltimoPrecio { get; private set; }

	/// <summary>Verdadero en cuanto llega un trade con AggressorExchangeOrderId.</summary>
	public bool ConIdAgresor { get; private set; }

	public void Reiniciar()
	{
		_porId.Clear();
		_inferido = null;
		Barridos.Clear();
		ConIdAgresor = false;
	}

	public void Trade(TradeFlujo t)
	{
		UltimoPrecio = t.Precio;

		if (t.Agresor is { } lado && t.Volumen > 0)
		{
			if (t.IdAgresor is long id && id > 0)
			{
				ConIdAgresor = true;

				if (!_porId.TryGetValue(id, out var g))
					_porId[id] = Nuevo(t, lado, true);
				else
					Sumar(g, t);
			}
			else if (!ConIdAgresor)
			{
				// INFERIDO: mismo lado y mismo milisegundo = la misma orden agresiva.
				if (_inferido is { } g && g.Lado == lado && (t.Utc - g.Ultimo).TotalMilliseconds < 1)
				{
					Sumar(g, t);
				}
				else
				{
					if (_inferido is { } cerrado)
						Cerrar(cerrado);

					_inferido = Nuevo(t, lado, false);
				}
			}
		}

		Tiempo(t.Utc);
	}

	private static Grupo Nuevo(TradeFlujo t, Lado lado, bool mbo) =>
		new() { Lado = lado, Inicio = t.Utc, Ultimo = t.Utc, Primero = t.Precio, UltimoPrecio = t.Precio, Min = t.Precio, Max = t.Precio, Volumen = t.Volumen, Mbo = mbo };

	private static void Sumar(Grupo g, TradeFlujo t)
	{
		g.Ultimo = t.Utc;
		g.UltimoPrecio = t.Precio;
		g.Min = Math.Min(g.Min, t.Precio);
		g.Max = Math.Max(g.Max, t.Precio);
		g.Volumen += t.Volumen;
	}

	/// <summary>Reloj: cierra las órdenes agresivas terminadas (100 ms sin trozos) y resuelve LIQUIDEZ / RUPTURA.</summary>
	public void Tiempo(DateTime utc)
	{
		foreach (var (id, g) in _porId.Where(p => (utc - p.Value.Ultimo).TotalMilliseconds > 100).ToList())
		{
			_porId.Remove(id);
			Cerrar(g);
		}

		if (_inferido is { } i && (utc - i.Ultimo).TotalMilliseconds > 100)
		{
			_inferido = null;
			Cerrar(i);
		}

		foreach (var b in Barridos.Where(b => b.Tipo == "EVALUANDO"))
			Resolver(b, UltimoPrecio, utc);
	}

	private void Cerrar(Grupo g)
	{
		var niveles = (int)Math.Round((g.Max - g.Min) / Tick) + 1;

		if (niveles < Math.Max(2, P.MinSweepLevels) || g.Volumen < Math.Max(1, P.MinSweepContracts))
			return;

		var b = new Barrido
		{
			Lado = g.Lado,
			Desde = g.Primero,
			Hasta = g.Lado == Lado.Compra ? g.Max : g.Min,
			Volumen = g.Volumen,
			Niveles = niveles,
			Utc = g.Inicio
		};

		// CONTRA ICEBERG: un iceberg vivo del lado contrario frena el barrido a MaxDisplacementTicks o menos.
		var maximo = Math.Max(0, MaxDisplacementTicks) * Tick;
		b.ContraIceberg = Icebergs?.Invoke().Where(i => i.Vivo && i.Lado == g.Lado.Contrario())
			.Where(i => g.Lado == Lado.Venta ? b.Hasta <= i.Precio && b.Hasta >= i.Precio - maximo : b.Hasta >= i.Precio && b.Hasta <= i.Precio + maximo)
			.OrderByDescending(i => i.Ejecutado)
			.FirstOrDefault();

		if (b.ContraIceberg is not null)
		{
			b.Tipo = "CONTRA ICEBERG";
		}
		else
		{
			b.Nivel = NivelBarrido(b);

			if (b.Nivel is null)
			{
				if (P.OnlyAtReferenceLevels)
					return;

				b.Tipo = "SIN NIVEL";
			}
		}

		var modo = g.Mbo ? ModoDatos.MBO : ModoDatos.Inferido;
		b.Evento = _eventos.Crear("Sweeps", "Sweep", g.Lado, g.Inicio, g.Primero, modo, Sesiones.De(g.Inicio));
		b.Evento.PrecioFin = b.Hasta;
		b.Evento.Volumen = g.Volumen;
		b.Evento.Contexto = b.Nivel?.Nombre ?? (b.ContraIceberg is not null ? "Iceberg" : "");
		Barridos.Add(b);
		Texto(b);
		_eventos.Publicar(b.Evento, g.Ultimo);

		if (Barridos.Count > 300)
			Barridos.RemoveRange(0, Barridos.Count - 300);
	}

	/// <summary>Máximo o mínimo relevante que el barrido rompió (empezó a LevelToleranceTicks o antes y lo cruzó).</summary>
	private NivelReferencia? NivelBarrido(Barrido b)
	{
		if (Niveles?.Invoke() is not { Count: > 0 } niveles)
			return null;

		var tolerancia = Math.Max(0, P.LevelToleranceTicks) * Tick;

		var rotos = niveles.Where(n => P.Usa(n.Nombre)).Where(n => b.Lado == Lado.Venta
			? b.Hasta < n.Precio && b.Desde >= n.Precio - tolerancia
			: b.Hasta > n.Precio && b.Desde <= n.Precio + tolerancia).ToList();

		if (rotos.Count == 0)
			return null;

		// Las ventas barren mínimos y las compras máximos: primero esos, y el más cercano al inicio.
		return rotos.OrderBy(n => n.EsMaximo == (b.Lado == Lado.Compra) ? 0 : 1).ThenBy(n => Math.Abs(n.Precio - b.Desde)).First();
	}

	private void Resolver(Barrido b, decimal precio, DateTime utc)
	{
		if (b.Nivel is not { } nivel)
			return;

		var segundos = (utc - b.Utc).TotalSeconds;
		var dentro = b.Lado == Lado.Venta ? precio > nivel.Precio : precio < nivel.Precio;

		if (dentro && segundos <= Math.Max(1, P.SweepReclaimSeconds))
		{
			b.Tipo = "LIQUIDEZ";
			b.SegundosVuelta = Math.Round(segundos);
		}
		else if (segundos > Math.Max(1, P.SweepReclaimSeconds))
		{
			b.Tipo = "RUPTURA";
		}
		else
		{
			return;
		}

		Texto(b);
		_eventos.Publicar(b.Evento, utc);
	}

	private void Texto(Barrido b)
	{
		var e = b.Evento;
		e.Estado = b.Tipo;
		var tipo = b.Tipo is "EVALUANDO" or "SIN NIVEL" ? "" : b.Tipo + " ";
		var detalle = b.ContraIceberg is { } i
			? $"frenado en {Textos.Precio(i.Precio, Tick)}"
			: b.Nivel is { } n ? $"barrió {n.Nombre} {Textos.Precio(n.Precio, Tick)}" : "sin nivel";
		var estado = b.Tipo == "EVALUANDO" ? " | EVALUANDO" : b.SegundosVuelta is { } s ? $" | volvió en {s:0} s" : "";

		e.Texto = $"BARRIDO {tipo}{b.Lado.Texto()} | {b.Niveles} niveles | {b.Volumen:0}c | {detalle}{estado} | {Textos.Hora(b.Utc)}";
	}
}
