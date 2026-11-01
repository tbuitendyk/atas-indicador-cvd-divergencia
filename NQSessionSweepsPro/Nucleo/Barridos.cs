namespace NqSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Métricas de Order Flow de una ventana (1, 3 o 5 minutos desde la apertura del minuto de t0).</summary>
public sealed class MetricasVentana
{
	public int Minutos;
	public decimal VolTotal, VolBeyond, AskBeyond, BidBeyond, DeltaWin, DeltaChange, DeltaBeyond;
	public int TicksBeyond;
	public decimal Eficiencia, Cierre, Alto, Bajo;
	public int Absorcion;

	/// <summary>Qué puntos de la absorción se cumplieron (para el detalle y el correo).</summary>
	public List<string> Puntos = new();

	public decimal DeltaBeyondNeto => AskBeyond - BidBeyond;
}

/// <summary>Datos de la ruptura en vivo (t0 en el tick): hora exacta, delta antes de t0 y velocidad por tramos de 5 s.</summary>
public sealed class RupturaEnVivo
{
	public DateTime T0Utc;
	public decimal Precio;
	public decimal DeltaMinutoAntes;
	public readonly Dictionary<int, decimal> AgresionPorTramo = new();

	public decimal VelocidadPico => AgresionPorTramo.Count == 0 ? 0 : AgresionPorTramo.Values.Max() / 5m;

	/// <summary>Agotamiento: el último tramo completo cae más de un 50 % respecto al pico.</summary>
	public bool Agotada(DateTime hastaUtc)
	{
		var ultimo = (int)Math.Floor((hastaUtc - T0Utc).TotalSeconds / 5) - 1;

		if (AgresionPorTramo.Count == 0 || ultimo < 0)
			return false;

		var pico = AgresionPorTramo.Values.Max();
		var final = AgresionPorTramo.TryGetValue(ultimo, out var v) ? v : 0;
		return pico > 0 && final < pico * 0.5m;
	}
}

/// <summary>
/// Caja de barrido (traducción de bzBX/szBX del Pine) con su validación de Order Flow. Buyside: desde L hacia arriba;
/// sellside: desde L hacia abajo.
/// </summary>
public sealed class ZonaBarrido
{
	public int Id;
	public NivelLiquidez Nivel = null!;
	public Lado Lado;
	public decimal L;

	/// <summary>Bordes izquierdo y derecho (índices de vela) y extremo de la caja (techo o suelo).</summary>
	public int Izquierda, Derecha;

	public decimal Extremo;

	/// <summary>Máximo (buyside) o mínimo (sellside) alcanzado por el precio desde el barrido (para el stop).</summary>
	public decimal ExtremoPrecio;

	public bool Activa = true;
	public bool FalsoPine;
	public int BarraBarrido;
	public DateTime T0Utc, MinutoT0Utc;
	public RupturaEnVivo? Vivo;

	public MetricasVentana? W1, W3, W5;
	public Clasificacion Clasificacion = Clasificacion.Pendiente;
	public int? CierreDeVueltaEn;
	public bool CierresSeguidosMasAlla;
	public decimal VelocidadPico, RatioVelocidad;
	public bool VelocidadAproximada;
	public Proyeccion? Proyeccion;

	public string Nombre => $"{Lado.Extremo()} {Nivel.Origen}";

	/// <summary>Etiqueta junto a la caja: "SWEEP REAL 8/10", "FALSO", "SIN LIQ", "DUDOSO 5/10" o "PENDIENTE".</summary>
	public string Etiqueta
	{
		get
		{
			var abs = (W5 ?? W3 ?? W1)?.Absorcion;
			return Clasificacion switch
			{
				Clasificacion.Real or Clasificacion.Dudoso when abs is not null => $"{Clasificacion.Texto()} {abs}/10",
				_ => Clasificacion.Texto()
			};
		}
	}

	/// <summary>Panel de datos del barrido (texto ASCII, 6 líneas).</summary>
	public string Panel(Func<decimal, string> precio)
	{
		static string S(decimal x) => (x >= 0 ? "+" : "") + x.ToString("0", CultureInfo.InvariantCulture);
		static string N(decimal x) => x.ToString("0", CultureInfo.InvariantCulture);
		string A(MetricasVentana? w) => w is null ? "-" : $"{w.Absorcion}/10";

		var hora = Zonas.AEt(T0Utc).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		var compra = Lado == Lado.Buyside ? "compra" : "venta";
		var lineas = new List<string>
		{
			$"{Nombre} {precio(L)} | {hora} ET",
			W1 is null ? "Vol 1m - | Beyond -" : $"Vol 1m {N(W1.VolTotal)} | Beyond {N(W1.VolBeyond)}",
			$"Delta 1m {(W1 is null ? "-" : S(W1.DeltaWin))} | Cambio 3m {(W3 is null ? "-" : S(W3.DeltaChange))} | 5m {(W5 is null ? "-" : S(W5.DeltaChange))}",
			$"Absorcion 1m {A(W1)} 3m {A(W3)} 5m {A(W5)}",
			VelocidadPico > 0
				? $"Vel {compra} {N(VelocidadPico)} c/s (x{RatioVelocidad.ToString("0.0", CultureInfo.InvariantCulture)}){(VelocidadAproximada ? " aprox" : "")}"
				: $"Vel {compra} -",
			$"Estado: {Clasificacion.Texto()}"
		};

		return string.Join("\n", lineas);
	}
}

/// <summary>Validación del barrido con Order Flow: métricas de cada ventana, absorción (0-10) y clasificación.</summary>
public static class OrderFlow
{
	/// <summary>
	/// Métricas de una ventana a partir de las velas del gráfico que empiezan dentro de ella (en 1 min, exactas; en
	/// 3 y 5 min, la ventana se ajusta a las velas completas). Velas = las de la ventana; previas = las 20 anteriores
	/// al barrido (volumen medio por tick).
	/// </summary>
	public static MetricasVentana Calcular(ZonaBarrido z, int minutos, List<Vela> ventana, Vela velaBarrido, Huella? huella,
		decimal volPorTickMedio, ConfiguracionSweeps cfg, DateTime finVentanaUtc)
	{
		var tick = cfg.TamanoTick;
		var s = z.Lado.Signo();
		var m = new MetricasVentana { Minutos = minutos };

		foreach (var v in ventana)
		{
			m.VolTotal += v.Volume;
			m.DeltaWin += v.Delta;

			if (v.Niveles is not { } niveles)
				continue;

			foreach (var (t, n) in niveles)
			{
				var p = t * tick;

				if (s * (p - z.L) <= 0)
					continue;

				m.AskBeyond += (decimal)n.Ask;
				m.BidBeyond += (decimal)n.Bid;
			}
		}

		m.VolBeyond = m.AskBeyond + m.BidBeyond;
		m.DeltaBeyond = m.AskBeyond - m.BidBeyond;
		m.DeltaChange = m.DeltaWin - (z.Vivo?.DeltaMinutoAntes ?? 0);
		m.Alto = ventana.Max(v => v.High);
		m.Bajo = ventana.Min(v => v.Low);
		m.Cierre = ventana[^1].Close;
		var avance = z.Lado == Lado.Buyside ? m.Alto - z.L : z.L - m.Bajo;
		m.TicksBeyond = Math.Max(0, (int)Math.Round(avance / tick));
		m.Eficiencia = m.VolBeyond / Math.Max(m.TicksBeyond, 1);

		// --- Absorción (0 a 10) ---
		void Punto(int puntos, string texto)
		{
			m.Absorcion += puntos;
			m.Puntos.Add($"{texto} +{puntos}");
		}

		if (m.VolBeyond >= (decimal)cfg.AbsVolMult * volPorTickMedio && volPorTickMedio > 0)
			Punto(2, "Volumen mas alla");

		var rango = m.Alto - m.Bajo;
		var mitadContraria = rango > 0 && (z.Lado == Lado.Buyside ? m.Cierre < m.Bajo + rango / 2 : m.Cierre > m.Bajo + rango / 2);
		var cierreDeVuelta = s * (m.Cierre - z.L) < 0;

		if (s * m.DeltaBeyond > 0 && (cierreDeVuelta || mitadContraria))
			Punto(2, "Divergencia de delta");

		if (cierreDeVuelta)
			Punto(2, "Cierre de vuelta");

		if (huella is { } h)
		{
			if (z.Lado == Lado.Buyside ? velaBarrido.EnTercioSuperior(h.Poc) : velaBarrido.EnTercioInferior(h.Poc))
				Punto(1, "POC en el extremo");

			if (z.Lado == Lado.Buyside ? velaBarrido.EnTercioSuperior(h.VaMedio) : velaBarrido.EnTercioInferior(h.VaMedio))
				Punto(1, "VA en el extremo");
		}

		if (m.TicksBeyond <= cfg.AbsMaxTicks && volPorTickMedio > 0 && m.Eficiencia >= 1.5m * volPorTickMedio)
			Punto(1, "Eficiencia alta");

		if ((z.Vivo?.Agotada(finVentanaUtc) ?? false) || Apilado(velaBarrido, z.Lado, tick))
			Punto(1, "Agotamiento");

		return m;
	}

	/// <summary>
	/// Bid stacked en el tercio superior (barrido buyside): 3 o más precios seguidos con Bid >= 3 veces el Ask en
	/// diagonal (el del precio de encima). En espejo, Ask stacked en el tercio inferior.
	/// </summary>
	public static bool Apilado(Vela v, Lado lado, decimal tick)
	{
		if (v.Niveles is not { Count: > 3 } niveles || v.Rango <= 0)
			return false;

		var seguidos = 0;

		foreach (var (t, n) in niveles)
		{
			var precio = t * tick;
			var enZona = lado == Lado.Buyside ? v.EnTercioSuperior(precio) : v.EnTercioInferior(precio);
			var diagonal = lado == Lado.Buyside
				? niveles.TryGetValue(t + 1, out var arriba) ? arriba.Ask : (double?)null
				: niveles.TryGetValue(t - 1, out var abajo) ? abajo.Bid : (double?)null;
			var propio = lado == Lado.Buyside ? n.Bid : n.Ask;
			var desequilibrio = enZona && diagonal is { } d && propio >= 3 * Math.Max(d, 1e-9) && propio > 0;
			seguidos = desequilibrio ? seguidos + 1 : 0;

			if (seguidos >= 3)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Clasificación (se evalúa al cierre de W1 y se actualiza con W3 y W5): FALSO si el Pine lo marcó falso o hubo
	/// dos cierres seguidos más allá con el delta a favor y absorción &lt; 4; REAL con VolBeyond &gt;= MinVolBeyond,
	/// absorción de W1 o W3 &gt;= 6 y cierre de vuelta en SweepMarginLength velas; SIN LIQ con 2 ticks o menos y poco
	/// volumen; si no, DUDOSO.
	/// </summary>
	public static Clasificacion Clasificar(ZonaBarrido z, ConfiguracionSweeps cfg)
	{
		var w = z.W5 ?? z.W3 ?? z.W1;

		if (w is null)
			return Clasificacion.Pendiente;

		if (z.FalsoPine || (z.CierresSeguidosMasAlla && z.Lado.Signo() * w.DeltaChange > 0 && w.Absorcion < 4))
			return Clasificacion.Falso;

		var absorcion = Math.Max(z.W1?.Absorcion ?? 0, z.W3?.Absorcion ?? 0);

		if (w.VolBeyond >= cfg.MinVolBeyond && absorcion >= 6 && z.CierreDeVueltaEn is { } c && c - z.BarraBarrido < cfg.SweepMarginLength)
			return Clasificacion.Real;

		if (w.TicksBeyond <= 2 && w.VolBeyond < cfg.MinVolBeyond)
			return Clasificacion.SinLiquidez;

		return Clasificacion.Dudoso;
	}
}
