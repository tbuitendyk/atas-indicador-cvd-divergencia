namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Parámetros del grupo "1. Imbalance" (nombres de la especificación).</summary>
public sealed class ParametrosImbalance
{
	public decimal ImbalanceRatioPercent { get; set; } = 300;
	public int MinStackedLevels { get; set; } = 3;
	public int MinLevelVolumeContracts { get; set; } = 15;
	public decimal OffHoursFactor { get; set; } = 0.5m;
	public bool IgnoreZeroSide { get; set; } = true;
	public int ZoneExtendBars { get; set; } = 50;
	public int MinBreakTicks { get; set; } = 2;
	public int MaxActiveZones { get; set; } = 20;
}

/// <summary>Zona de stacked imbalance: NUEVA → PROBADA (el precio vuelve y respeta) → ROTA (cierre más allá).</summary>
public sealed class ZonaImbalance
{
	public EventoFlujo Evento = null!;
	public Lado Lado;
	public decimal Bajo, Alto, Ratio;
	public int Niveles;
	public DateTime InicioUtc;

	/// <summary>Velas cerradas desde la que la creó (la zona se extiende ZoneExtendBars velas).</summary>
	public int Velas;

	/// <summary>Hora de la vela en la que dejó de extenderse (null = sigue viva).</summary>
	public DateTime? FinUtc;

	/// <summary>El precio salió de la zona (hace falta para "volver" a ella).</summary>
	public bool Salio;

	public bool ConIceberg;
	public string Estado = "NUEVA";

	public bool Activa => FinUtc is null && Estado != "ROTA";

	public bool Contiene(decimal precio, decimal margen) => precio >= Bajo - margen && precio <= Alto + margen;
}

/// <summary>
/// StackedImbalancePro: imbalance diagonal (Ask del nivel N contra Bid del nivel N-1 = compra; Bid del nivel N contra
/// Ask del nivel N+1 = venta) en varios niveles consecutivos del mismo lado, con el estado de la zona y la etiqueta
/// "con iceberg". Se calcula con cada vela CERRADA (no repinta); en vivo, el toque de una zona NUEVA se ve en el tick.
/// </summary>
public sealed class MotorImbalance
{
	private readonly RegistroEventos _eventos;

	public MotorImbalance(RegistroEventos eventos) => _eventos = eventos;

	public ParametrosImbalance P { get; set; } = new();

	public decimal Tick { get; set; } = 0.25m;

	public List<ZonaImbalance> Zonas { get; } = new();

	public void Reiniciar() => Zonas.Clear();

	/// <summary>Una vela cerrada: primero se actualizan las zonas existentes y después se buscan las nuevas.</summary>
	public void Agregar(VelaFlujo v, Sesion sesion, bool rth)
	{
		foreach (var z in Zonas.Where(z => z.Activa).ToList())
			ActualizarConVela(z, v);

		Detectar(v, sesion, rth);
	}

	/// <summary>Imbalances de compra y de venta de una vela por tick (para pruebas y para dibujar el modo Full).</summary>
	public (List<long> Compra, List<long> Venta) Imbalances(VelaFlujo v, bool rth)
	{
		var compra = new List<long>();
		var venta = new List<long>();

		if (v.Niveles.Count == 0)
			return (compra, venta);

		var minimo = P.MinLevelVolumeContracts * (rth ? 1m : P.OffHoursFactor);
		var ratio = Math.Max(1m, P.ImbalanceRatioPercent / 100m);
		var desde = v.Niveles.Keys.First();
		var hasta = v.Niveles.Keys.Last();

		(decimal Bid, decimal Ask) N(long t) => v.Niveles.TryGetValue(t, out var n) ? n : (0, 0);

		bool Es(decimal fuerte, decimal debil)
		{
			if (fuerte < minimo || fuerte <= 0)
				return false;

			// Lado débil a cero: con IgnoreZeroSide cuenta como 1 contrato (exige el volumen mínimo, sin "infinitos"
			// con 1-2 contratos); sin él, un nivel con un lado a cero no cuenta como imbalance.
			if (debil <= 0)
				return P.IgnoreZeroSide && fuerte >= ratio;

			return fuerte >= ratio * debil;
		}

		for (var t = desde; t <= hasta; t++)
		{
			if (Es(N(t).Ask, N(t - 1).Bid))
				compra.Add(t);

			if (Es(N(t).Bid, N(t + 1).Ask))
				venta.Add(t);
		}

		return (compra, venta);
	}

	private void Detectar(VelaFlujo v, Sesion sesion, bool rth)
	{
		var (compra, venta) = Imbalances(v, rth);

		foreach (var (ticks, lado) in new[] { (compra, Lado.Compra), (venta, Lado.Venta) })
		{
			foreach (var (desde, hasta) in Tramos(ticks))
			{
				var niveles = (int)(hasta - desde + 1);

				if (niveles < Math.Max(2, P.MinStackedLevels))
					continue;

				var ratios = new List<decimal>();

				for (var t = desde; t <= hasta; t++)
				{
					var n = v.Niveles.TryGetValue(t, out var a) ? a : (0, 0);
					var debil = lado == Lado.Compra
						? (v.Niveles.TryGetValue(t - 1, out var b) ? b.Bid : 0)
						: (v.Niveles.TryGetValue(t + 1, out var c) ? c.Ask : 0);
					var fuerte = lado == Lado.Compra ? n.Ask : n.Bid;
					ratios.Add(fuerte / Math.Max(1, debil));
				}

				var z = new ZonaImbalance
				{
					Lado = lado,
					Bajo = desde * Tick,
					Alto = hasta * Tick,
					Niveles = niveles,
					Ratio = ratios.Average() * 100,
					InicioUtc = v.InicioUtc
				};

				z.Evento = _eventos.Crear("Imbalance", "StackedImb", lado, v.InicioUtc, z.Bajo, ModoDatos.Velas, sesion);
				z.Evento.PrecioFin = z.Alto;
				Zonas.Add(z);
				Texto(z);
				_eventos.Publicar(z.Evento, v.InicioUtc);
			}
		}

		// Zonas vivas máximas: las más antiguas dejan de extenderse.
		var vivas = Zonas.Where(z => z.Activa).ToList();

		foreach (var z in vivas.Take(Math.Max(0, vivas.Count - Math.Max(1, P.MaxActiveZones))))
			z.FinUtc = v.InicioUtc;

		if (Zonas.Count > 500)
			Zonas.RemoveRange(0, Zonas.Count - 500);
	}

	private static IEnumerable<(long Desde, long Hasta)> Tramos(List<long> ticks)
	{
		for (var i = 0; i < ticks.Count;)
		{
			var j = i;

			while (j + 1 < ticks.Count && ticks[j + 1] == ticks[j] + 1)
				j++;

			yield return (ticks[i], ticks[j]);
			i = j + 1;
		}
	}

	private void ActualizarConVela(ZonaImbalance z, VelaFlujo v)
	{
		z.Velas++;

		// ROTA: cierre de vela más allá de la zona en MinBreakTicks (o más).
		var fuera = z.Lado == Lado.Compra ? z.Bajo - v.Close : v.Close - z.Alto;

		if (fuera >= Math.Max(1, P.MinBreakTicks) * Tick)
		{
			CambiarEstado(z, "ROTA", v.InicioUtc);
			z.FinUtc = v.InicioUtc;
			return;
		}

		Tocar(z, v.High, v.Low, v.InicioUtc);

		if (z.Velas >= Math.Max(1, P.ZoneExtendBars))
			z.FinUtc = v.InicioUtc;
	}

	/// <summary>En vivo: el precio vuelve a una zona NUEVA → PROBADA (con su alerta) sin esperar al cierre.</summary>
	public void Precio(decimal precio, DateTime utc)
	{
		foreach (var z in Zonas)
		{
			if (z.Activa && z.Estado == "NUEVA")
				Tocar(z, precio, precio, utc);
		}
	}

	private void Tocar(ZonaImbalance z, decimal alto, decimal bajo, DateTime utc)
	{
		// La zona de compra queda debajo del precio: "sale" cuando el mínimo queda por encima de ella y "vuelve"
		// cuando el mínimo entra en ella (y la de venta al revés).
		var fuera = z.Lado == Lado.Compra ? bajo > z.Alto : alto < z.Bajo;
		var dentro = z.Lado == Lado.Compra ? bajo <= z.Alto : alto >= z.Bajo;

		if (!z.Salio)
		{
			z.Salio = fuera;
			return;
		}

		if (dentro && z.Estado == "NUEVA")
			CambiarEstado(z, "PROBADA", utc);
	}

	private void CambiarEstado(ZonaImbalance z, string estado, DateTime utc)
	{
		if (z.Estado == estado)
			return;

		z.Estado = estado;
		Texto(z);
		_eventos.Publicar(z.Evento, utc);
	}

	/// <summary>Etiqueta "con iceberg": un iceberg del mismo lado dentro de la zona.</summary>
	public void MarcarIceberg(Lado lado, decimal precio, DateTime utc)
	{
		foreach (var z in Zonas.Where(z => z.Activa && z.Lado == lado && !z.ConIceberg && z.Contiene(precio, Tick)))
		{
			z.ConIceberg = true;
			z.Evento.Contexto = "ConIceberg";
			Texto(z);
			_eventos.Publicar(z.Evento, utc);
		}
	}

	private void Texto(ZonaImbalance z)
	{
		var e = z.Evento;
		e.Estado = z.Estado;
		e.Ratio = Math.Round(z.Ratio, 0);
		e.Recargas = null;
		var iceberg = z.ConIceberg ? " | con iceberg" : "";
		e.Texto = $"STACKED IMB {z.Lado.Texto()} | {Textos.Precio(z.Bajo, Tick)}-{Textos.Precio(z.Alto, Tick)} | {z.Niveles} niv | " +
			$"{z.Ratio.ToString("0", CultureInfo.InvariantCulture)}% | {z.Estado}{iceberg} | {Textos.Hora(z.InicioUtc)}";
	}
}
