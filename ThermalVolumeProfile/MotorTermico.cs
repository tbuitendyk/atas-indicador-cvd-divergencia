namespace Termico;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

// =====================================================================================================================
// Núcleo del perfil térmico. No usa ningún tipo de ATAS: así se prueba sin la plataforma y después se puede
// incorporar tal cual a IndicadorCompleto. El indicador (ThermalVolumeProfile.cs) solo le entrega velas y dibuja.
//
// Orden de cálculo obligatorio: 1. Perfil  2. POC / VA  3. EDGE  4. SPIKE  5. CLÚSTER  6. LVN.
// Convenciones: la fila 0 es el precio más bajo; el precio de una fila es su precio inferior; los parámetros en
// "ticks" se comparan en ticks (filas × ticks por fila) y los parámetros en "filas" (Rows), en filas.
// =====================================================================================================================

#region Enumeraciones (ajustes visibles en ATAS)

public enum ModosRango
{
	[Display(Name = "Fecha y hora (desde / hasta)")]
	CustomDateTime,

	[Display(Name = "Últimas N velas")]
	LastNBars,

	[Display(Name = "Rango visible en pantalla")]
	VisibleRange,

	[Display(Name = "Sesión (horario)")]
	Session,

	[Display(Name = "Día")]
	Day,

	[Display(Name = "Semana")]
	Week,

	[Display(Name = "Mes")]
	Month
}

public enum TiposPerfil
{
	[Display(Name = "Volumen")]
	Volume,

	[Display(Name = "TPO (tiempo en el precio)")]
	TPO,

	[Display(Name = "Delta (compras − ventas)")]
	Delta,

	[Display(Name = "Bid (ventas agresivas)")]
	Bid,

	[Display(Name = "Ask (compras agresivas)")]
	Ask
}

public enum PaletasTermicas
{
	[Display(Name = "Amarillo a rojo")]
	YellowRed,

	[Display(Name = "Azul a rojo")]
	BlueRed,

	[Display(Name = "Escala de grises")]
	Grayscale,

	[Display(Name = "Personalizada (4 colores)")]
	Custom
}

public enum EscalasTermicas
{
	[Display(Name = "Lineal")]
	Linear,

	[Display(Name = "Raíz cuadrada")]
	SquareRoot,

	[Display(Name = "Logarítmica")]
	Logarithmic
}

public enum AnclajesPerfil
{
	[Display(Name = "Izquierda del rango")]
	LeftOfRange,

	[Display(Name = "Derecha del rango")]
	RightOfRange,

	[Display(Name = "Izquierda de la pantalla")]
	LeftOfScreen,

	[Display(Name = "Derecha de la pantalla")]
	RightOfScreen
}

public enum DireccionesPerfil
{
	[Display(Name = "Automática (según el anclaje)")]
	Auto,

	[Display(Name = "De izquierda a derecha")]
	LeftToRight,

	[Display(Name = "De derecha a izquierda")]
	RightToLeft
}

public enum DibujosLvn
{
	[Display(Name = "Línea")]
	Line,

	[Display(Name = "Franja")]
	Band
}

public enum ZonasHorariasSesion
{
	[Display(Name = "Nueva York")]
	NewYork,

	[Display(Name = "Hora del gráfico (ATAS)")]
	Chart
}

#endregion

#region Datos de entrada

/// <summary>Volumen negociado en un precio: Bid = ventas agresivas, Ask = compras agresivas.</summary>
public readonly record struct NivelPrecio(decimal Price, decimal Volume, decimal Bid, decimal Ask);

/// <summary>Vela con su volumen por precio, tal como la entrega ATAS. La hora es la de apertura, en UTC.</summary>
public sealed class VelaPerfil
{
	public DateTime Time { get; set; }

	public decimal High { get; set; }

	public decimal Low { get; set; }

	public decimal Volume { get; set; }

	public decimal Delta { get; set; }

	public List<NivelPrecio> Niveles { get; } = new();
}

/// <summary>Zona de búsqueda: los precios se aceptan en cualquier orden.</summary>
public readonly record struct ZonaBusqueda(decimal Baja, decimal Alta)
{
	public static ZonaBusqueda Crear(decimal a, decimal b) => new(Math.Min(a, b), Math.Max(a, b));

	public bool Contiene(decimal precio) => precio >= Baja && precio <= Alta;
}

/// <summary>Color sin dependencias de dibujo (el indicador lo convierte al de ATAS).</summary>
public readonly record struct ColorArgb(byte A, byte R, byte G, byte B);

#endregion

#region Parámetros y resultados

/// <summary>Parámetros de los detectores (los nombres son los de la especificación).</summary>
public sealed class ParametrosTermicos
{
	public double ValueAreaPercent { get; set; } = 70;

	// CLÚSTER
	public bool EnableCluster { get; set; } = true;
	public bool ClusterUseZone { get; set; } = true;
	public double ClusterSpikeMinN { get; set; } = 0.55;
	public double ClusterSpikeRatio { get; set; } = 1.6;
	public int ClusterGapTicks { get; set; } = 12;
	public int MinSpikesInCluster { get; set; } = 3;
	public int MinClusterHeightTicks { get; set; } = 20;
	public int EdgeSnapRows { get; set; } = 4;
	public int MaxClusters { get; set; } = 3;

	// EDGE
	public bool EnableEdge { get; set; } = true;
	public bool EdgeUseZone { get; set; } = true;
	public double EdgeHvnThreshold { get; set; } = 0.55;
	public int EdgeMergeGapTicks { get; set; } = 3;
	public int EdgeMinHvnHeightTicks { get; set; } = 8;
	public int EdgeWindowRows { get; set; } = 4;
	public double EdgeMinStep { get; set; } = 0.30;
	public double EdgeMinSlope { get; set; } = 0.08;
	public int EdgeConfirmRows { get; set; } = 6;
	public double EdgeLowAfter { get; set; } = 0.35;
	public double EdgeSmoothSigma { get; set; } = 1.0;
	public int MaxEdges { get; set; } = 6;

	// SPIKE
	public bool EnableSpike { get; set; } = true;
	public bool SpikeUseZone { get; set; } = true;
	public double SpikeMinN { get; set; } = 0.40;
	public double SpikeRatio { get; set; } = 1.8;
	public int SpikeMaxWidthRows { get; set; } = 3;
	public bool HideSpikesInCluster { get; set; } = true;
	public int MaxSpikes { get; set; } = 5;

	// LVN
	public bool EnableLVN { get; set; } = true;
	public bool LVNUseZone { get; set; } = true;
	public double LVNSmoothTicks { get; set; } = 3;
	public double LVNMinProminence { get; set; } = 20;
	public double LVNMaxPercentOfPOC { get; set; } = 35;
	public int LVNMinDistanceTicks { get; set; } = 8;
	public int MaxLVN { get; set; } = 5;
	public bool ShowManualLVN { get; set; } = true;
}

public sealed class EdgeTermico
{
	public int Fila;
	public bool Superior;
	public decimal Precio;
	public double Step, Slope, Score;

	/// <summary>Una línea de clúster se movió a este EDGE: ya no se dibuja aparte.</summary>
	public bool Absorbido;

	public string Etiqueta(Func<decimal, string> formato) => $"EDGE {(Superior ? "ARRIBA" : "ABAJO")} {formato(Precio)}";
}

public sealed class SpikeTermico
{
	/// <summary>Fila del pico; Desde/Hasta son las filas de la espiga (1 a SpikeMaxWidthRows).</summary>
	public int Fila, Desde, Hasta;

	public decimal Precio;
	public double Volumen, N, Ratio, Score;
}

public sealed class ClusterTermico
{
	public List<SpikeTermico> Espigas = new();
	public int FilaArriba, FilaAbajo, FilaPoc;
	public decimal Arriba, Abajo, Poc;
	public double Score;
	public bool ArribaEnEdge, AbajoEnEdge;
}

public sealed class LvnTermico
{
	public int Fila;

	/// <summary>Filas de la franja (modo Band): el valle hasta un 25 % de la subida hacia el menor de sus picos.</summary>
	public int Inferior, Superior;

	public decimal Precio;
	public double Prominencia, Valle, Score;

	/// <summary>1 = el mejor LVN automático; 0 = LVN manual.</summary>
	public int Ranking;

	public bool Manual;
}

public sealed class ResultadoTermico
{
	/// <summary>Perfil del rango (el que se dibuja y del que salen POC y VA).</summary>
	public Perfil Perfil = null!;

	/// <summary>Perfil de los detectores: el mismo, o el secundario del filtro de tiempo.</summary>
	public Perfil Detectores = null!;

	public int Poc, Vah, Val;
	public List<EdgeTermico> Edges = new();
	public List<SpikeTermico> Spikes = new();
	public List<ClusterTermico> Clusters = new();
	public List<LvnTermico> Lvns = new();

	public decimal PrecioPoc => Perfil.PrecioFila(Poc);

	public decimal PrecioVah => Perfil.PrecioFila(Vah);

	public decimal PrecioVal => Perfil.PrecioFila(Val);
}

#endregion

#region Perfil

/// <summary>
/// Suma, por tick, las velas cerradas del rango. El índice de tick es precio / tick; las filas se forman después,
/// cuando se conoce el rango completo (AutoRowSize). En vivo, la vela abierta se suma aparte con
/// <see cref="Aportacion"/> sin tocar este acumulado: así solo se rehace la última vela.
/// </summary>
public sealed class AcumuladorTicks
{
	private readonly Dictionary<long, Celda> _celdas = new();
	private readonly Dictionary<long, long> _periodoTpo = new();

	public AcumuladorTicks(decimal tick, int periodoTpoMinutos)
	{
		Tick = tick > 0 ? tick : 0.25m;
		PeriodoTpoMinutos = Math.Max(1, periodoTpoMinutos);
	}

	public struct Celda
	{
		public double Vol, Bid, Ask, Tpo;
	}

	public decimal Tick { get; }

	public int PeriodoTpoMinutos { get; }

	public int Velas { get; private set; }

	public int VelasSinNiveles { get; private set; }

	public IReadOnlyDictionary<long, Celda> Celdas => _celdas;

	public long ATick(decimal precio) => (long)Math.Round(precio / Tick, MidpointRounding.AwayFromZero);

	public static long PeriodoTpo(DateTime t, int minutos) => t.Ticks / TimeSpan.FromMinutes(Math.Max(1, minutos)).Ticks;

	/// <summary>Suma una vela cerrada. Devuelve false si no traía volumen por precio (se repartió entre High y Low).</summary>
	public bool Agregar(VelaPerfil v)
	{
		var conNiveles = Sumar(v, _celdas);
		var periodo = PeriodoTpo(v.Time, PeriodoTpoMinutos);

		// TPO: cada periodo cuenta una sola vez en cada precio que tocó.
		foreach (var t in TicksDeLaVela(v))
		{
			if (_periodoTpo.TryGetValue(t, out var p) && p == periodo)
				continue;

			_periodoTpo[t] = periodo;
			var c = _celdas.TryGetValue(t, out var previa) ? previa : default;
			c.Tpo++;
			_celdas[t] = c;
		}

		Velas++;

		if (!conNiveles)
			VelasSinNiveles++;

		return conNiveles;
	}

	/// <summary>Aportación de la vela abierta, sin modificar el acumulado (se rehace en cada actualización en vivo).</summary>
	public Dictionary<long, Celda> Aportacion(VelaPerfil v, out bool conNiveles)
	{
		var d = new Dictionary<long, Celda>();
		conNiveles = Sumar(v, d);
		var periodo = PeriodoTpo(v.Time, PeriodoTpoMinutos);

		foreach (var t in TicksDeLaVela(v))
		{
			if (_periodoTpo.TryGetValue(t, out var p) && p == periodo)
				continue;

			var c = d.TryGetValue(t, out var previa) ? previa : default;
			c.Tpo++;
			d[t] = c;
		}

		return d;
	}

	private IEnumerable<long> TicksDeLaVela(VelaPerfil v)
	{
		var desde = ATick(Math.Min(v.Low, v.High));
		var hasta = ATick(Math.Max(v.Low, v.High));

		for (var t = desde; t <= hasta; t++)
			yield return t;
	}

	private bool Sumar(VelaPerfil v, Dictionary<long, Celda> destino)
	{
		if (v.Niveles.Count > 0)
		{
			foreach (var n in v.Niveles)
				Anadir(destino, ATick(n.Price), (double)n.Volume, (double)n.Bid, (double)n.Ask);

			return true;
		}

		// Sin volumen por precio: el volumen de la vela se reparte por igual entre su High y su Low.
		var desde = ATick(Math.Min(v.Low, v.High));
		var hasta = ATick(Math.Max(v.Low, v.High));
		var precios = hasta - desde + 1;
		var vol = (double)v.Volume / precios;
		var ask = Math.Max(0, (double)(v.Volume + v.Delta) / 2) / precios;
		var bid = Math.Max(0, (double)(v.Volume - v.Delta) / 2) / precios;

		for (var t = desde; t <= hasta; t++)
			Anadir(destino, t, vol, bid, ask);

		return false;
	}

	private static void Anadir(Dictionary<long, Celda> destino, long tick, double vol, double bid, double ask)
	{
		var c = destino.TryGetValue(tick, out var previa) ? previa : default;
		c.Vol += vol;
		c.Bid += bid;
		c.Ask += ask;
		destino[tick] = c;
	}
}

/// <summary>Perfil por filas: vol[i] (aquí Valor[i]) con la fila 0 en el precio más bajo.</summary>
public sealed class Perfil
{
	public decimal Tick { get; init; }

	public int TicksPorFila { get; init; }

	/// <summary>Número absoluto de la fila 0: Floor(precio / (Tick × TicksPorFila)).</summary>
	public long PrimeraFila { get; init; }

	/// <summary>Valor de cada fila según el tipo de perfil (en Delta, el valor absoluto).</summary>
	public double[] Valor { get; init; } = Array.Empty<double>();

	/// <summary>Delta con signo de cada fila (Ask − Bid), para marcar el signo en el perfil Delta.</summary>
	public double[] DeltaFila { get; init; } = Array.Empty<double>();

	public TiposPerfil Tipo { get; init; }

	public int Velas { get; init; }

	public int Filas => Valor.Length;

	public decimal PrecioFila(int fila) => (PrimeraFila + fila) * TicksPorFila * Tick;

	/// <summary>Fila de un precio, aunque quede fuera del perfil (puede ser negativa o ≥ Filas).</summary>
	public int FilaDePrecio(decimal precio)
	{
		var tick = (long)Math.Round(precio / Tick, MidpointRounding.AwayFromZero);
		return (int)(Utilidades.DivisionEntera(tick, TicksPorFila) - PrimeraFila);
	}

	/// <summary>Construye el perfil de las velas cerradas más, si la hay, la aportación de la vela abierta.</summary>
	public static Perfil? Construir(AcumuladorTicks acumulado, IReadOnlyDictionary<long, AcumuladorTicks.Celda>? abierta,
		TiposPerfil tipo, int rowSizeTicks, bool autoRowSize, int maxRows)
	{
		long min = long.MaxValue, max = long.MinValue;

		foreach (var t in acumulado.Celdas.Keys)
			(min, max) = (Math.Min(min, t), Math.Max(max, t));

		if (abierta is not null)
		{
			foreach (var t in abierta.Keys)
				(min, max) = (Math.Min(min, t), Math.Max(max, t));
		}

		if (min > max)
			return null;

		// AutoRowSize: filas más gruesas para no pasar de MaxRows (evita perfiles enormes en diario y semanal).
		var tpf = Math.Max(1, rowSizeTicks);

		if (autoRowSize && maxRows > 0)
		{
			while (Utilidades.DivisionEntera(max, tpf) - Utilidades.DivisionEntera(min, tpf) + 1 > maxRows)
				tpf = Math.Max(tpf + 1, (int)Math.Ceiling((max - min + 1) / (double)maxRows));
		}

		var primera = Utilidades.DivisionEntera(min, tpf);
		var filas = (int)(Utilidades.DivisionEntera(max, tpf) - primera + 1);
		var vol = new double[filas];
		var bid = new double[filas];
		var ask = new double[filas];
		var tpo = new double[filas];

		void Sumar(IReadOnlyDictionary<long, AcumuladorTicks.Celda> celdas)
		{
			foreach (var (t, c) in celdas)
			{
				var f = (int)(Utilidades.DivisionEntera(t, tpf) - primera);
				vol[f] += c.Vol;
				bid[f] += c.Bid;
				ask[f] += c.Ask;
				tpo[f] += c.Tpo;
			}
		}

		Sumar(acumulado.Celdas);

		if (abierta is not null)
			Sumar(abierta);

		var delta = new double[filas];
		var valor = new double[filas];

		for (var i = 0; i < filas; i++)
		{
			delta[i] = ask[i] - bid[i];
			valor[i] = tipo switch
			{
				TiposPerfil.TPO => tpo[i],
				TiposPerfil.Delta => Math.Abs(delta[i]),
				TiposPerfil.Bid => bid[i],
				TiposPerfil.Ask => ask[i],
				_ => vol[i]
			};
		}

		return new Perfil
		{
			Tick = acumulado.Tick,
			TicksPorFila = tpf,
			PrimeraFila = primera,
			Valor = valor,
			DeltaFila = delta,
			Tipo = tipo,
			Velas = acumulado.Velas + (abierta is null ? 0 : 1)
		};
	}
}

#endregion

#region Motor (orden de cálculo)

public static partial class MotorTermico
{
	/// <summary>
	/// Calcula todos los niveles en el orden obligatorio. 'detectores' es el perfil secundario del filtro de tiempo
	/// (null = el mismo perfil). Los LVN manuales cuentan como LVN y tienen prioridad sobre los automáticos.
	/// </summary>
	public static ResultadoTermico Analizar(Perfil perfil, Perfil? detectores, ParametrosTermicos c, ZonaBusqueda? zona,
		IReadOnlyList<decimal>? lvnManuales = null)
	{
		var r = new ResultadoTermico { Perfil = perfil, Detectores = detectores ?? perfil };

		// 2. POC / VA del perfil del rango.
		r.Poc = Poc(perfil.Valor);
		(r.Val, r.Vah) = AreaDeValor(perfil.Valor, r.Poc, c.ValueAreaPercent);

		var d = r.Detectores;
		ZonaBusqueda? Zona(bool usar) => usar ? zona : null;

		if (d.Filas == 0 || d.Valor.Max() <= 0)
		{
			r.Lvns = c.ShowManualLVN ? Manuales(d, lvnManuales) : new();
			return r;
		}

		// 3. EDGE: se calcula siempre, porque el clúster lo necesita para ajustar sus líneas.
		var edges = DetectarEdges(d, c, Zona(c.EdgeUseZone));

		// 4. SPIKE (vol[] sin suavizar).
		var volPoc = d.Valor[Poc(d.Valor)];
		var spikes = c.EnableSpike
			? BuscarEspigas(d, volPoc, c.SpikeMinN, c.SpikeRatio, c.SpikeMaxWidthRows, Zona(c.SpikeUseZone))
			: new List<SpikeTermico>();

		// 5. CLÚSTER (vol[] sin suavizar), con el ajuste fino a los EDGE.
		r.Clusters = c.EnableCluster ? DetectarClusters(d, volPoc, c, Zona(c.ClusterUseZone), edges) : new();

		// 6. LVN.
		r.Lvns = DetectarLvn(d, c, Zona(c.LVNUseZone), lvnManuales);

		// Lo que se dibuja: EDGE no absorbidos por un clúster y espigas fuera de los clústeres (si se pide).
		r.Edges = c.EnableEdge
			? edges.Where(e => !e.Absorbido).OrderByDescending(e => e.Score).Take(Math.Max(0, c.MaxEdges)).ToList()
			: new();

		if (c.HideSpikesInCluster)
			spikes = spikes.Where(s => !r.Clusters.Any(k => s.Fila >= k.FilaAbajo && s.Fila <= k.FilaArriba)).ToList();

		r.Spikes = spikes.OrderByDescending(s => s.Score).Take(Math.Max(0, c.MaxSpikes)).ToList();
		return r;
	}
}

#endregion

#region VA

public static partial class MotorTermico
{
	/// <summary>POC: la fila de mayor valor; si hay empate, la más cercana al centro del perfil.</summary>
	public static int Poc(double[] v)
	{
		if (v.Length == 0)
			return 0;

		var maximo = v.Max();
		var centro = (v.Length - 1) / 2.0;
		var poc = 0;
		var mejor = double.MaxValue;

		for (var i = 0; i < v.Length; i++)
		{
			if (v[i] != maximo || Math.Abs(i - centro) >= mejor)
				continue;

			mejor = Math.Abs(i - centro);
			poc = i;
		}

		return poc;
	}

	/// <summary>
	/// Área de Valor, método estándar CME: desde el POC se compara la suma de las 2 filas de arriba con la de las 2 de
	/// abajo, se añade la mayor (empate: arriba) y se repite hasta llegar al porcentaje. Devuelve (VAL, VAH) como filas.
	/// </summary>
	public static (int Val, int Vah) AreaDeValor(double[] v, int poc, double porcentaje)
	{
		var total = v.Sum();

		if (v.Length == 0 || total <= 0)
			return (poc, poc);

		var objetivo = total * Math.Clamp(porcentaje, 50, 95) / 100.0;
		var acumulado = v[poc];
		int bajo = poc, alto = poc;

		while (acumulado < objetivo && (bajo > 0 || alto < v.Length - 1))
		{
			double arriba = 0, abajo = 0;
			int nArriba = 0, nAbajo = 0;

			for (var k = 1; k <= 2; k++)
			{
				if (alto + k < v.Length)
				{
					arriba += v[alto + k];
					nArriba++;
				}

				if (bajo - k >= 0)
				{
					abajo += v[bajo - k];
					nAbajo++;
				}
			}

			if (nAbajo == 0 || (nArriba > 0 && arriba >= abajo))
			{
				alto += nArriba;
				acumulado += arriba;
			}
			else
			{
				bajo -= nAbajo;
				acumulado += abajo;
			}
		}

		return (bajo, alto);
	}
}

#endregion

#region Edge

public static partial class MotorTermico
{
	/// <summary>
	/// EDGE: borde de un bloque HVN donde el volumen cae de golpe. Usa vol[] suavizado (sigma = EdgeSmoothSigma
	/// filas). La línea va en la ÚLTIMA fila de alto volumen. Devuelve todos los válidos (sin el límite MaxEdges,
	/// que se aplica después de que los clústeres absorban los suyos).
	/// </summary>
	public static List<EdgeTermico> DetectarEdges(Perfil p, ParametrosTermicos c, ZonaBusqueda? zona)
	{
		var resultado = new List<EdgeTermico>();
		var s = Utilidades.Gauss(p.Valor, c.EdgeSmoothSigma);
		var maximo = s.Length == 0 ? 0 : s.Max();

		if (maximo <= 0)
			return resultado;

		var n = s.Select(x => x / maximo).ToArray();
		var tpf = p.TicksPorFila;

		// Bloques HVN: filas contiguas con n >= EdgeHvnThreshold; se unen los huecos menores que EdgeMergeGapTicks.
		var bloques = new List<(int A, int B)>();

		for (var i = 0; i < n.Length; i++)
		{
			if (n[i] < c.EdgeHvnThreshold)
				continue;

			var j = i;

			while (j + 1 < n.Length && n[j + 1] >= c.EdgeHvnThreshold)
				j++;

			if (bloques.Count > 0 && (i - bloques[^1].B - 1) * tpf < c.EdgeMergeGapTicks)
				bloques[^1] = (bloques[^1].A, j);
			else
				bloques.Add((i, j));

			i = j;
		}

		var w = Math.Max(1, c.EdgeWindowRows);
		var k = Math.Max(w, c.EdgeConfirmRows);

		foreach (var (a, b) in bloques.Where(x => (x.B - x.A + 1) * tpf >= c.EdgeMinHvnHeightTicks))
		{
			var mediaBloque = Utilidades.Media(n, a, b);
			Evaluar(b, +1);
			Evaluar(a, -1);

			void Evaluar(int borde, int dir)
			{
				// Dentro: las últimas W filas del bloque; fuera: las primeras W filas fuera del bloque.
				var dentro = Utilidades.Media(Enumerable.Range(0, w).Select(i => borde - dir * i).Where(i => i >= a && i <= b).Select(i => n[i]));
				var filasFuera = Enumerable.Range(1, w).Select(i => borde + dir * i).ToList();

				if (filasFuera.Any(i => i < 0 || i >= n.Length))
					return;

				var fuera = Utilidades.Media(filasFuera.Select(i => n[i]));
				var step = dentro - fuera;
				var slope = step / w;

				if (step < c.EdgeMinStep || slope < c.EdgeMinSlope)
					return;

				// Confirmación: las K filas de fuera (las que haya) deben tener poco volumen.
				var confirmacion = Enumerable.Range(1, k).Select(i => borde + dir * i).Where(i => i >= 0 && i < n.Length).Select(i => n[i]);

				if (Utilidades.Media(confirmacion) > c.EdgeLowAfter)
					return;

				// La línea: en la ventana, la fila con mayor caída respecto a su vecina de fuera (queda en la fila alta).
				var fila = borde;
				var mayorCaida = double.MinValue;

				for (var i = borde - dir * (w - 1); dir > 0 ? i <= borde + w - 1 : i >= borde - (w - 1); i += dir)
				{
					var vecina = i + dir;

					if (i < 0 || i >= n.Length || vecina < 0 || vecina >= n.Length)
						continue;

					if (n[i] - n[vecina] > mayorCaida)
						(mayorCaida, fila) = (n[i] - n[vecina], i);
				}

				var precio = p.PrecioFila(fila);

				if (zona is { } z && !z.Contiene(precio))
					return;

				resultado.Add(new EdgeTermico
				{
					Fila = fila,
					Superior = dir > 0,
					Precio = precio,
					Step = step,
					Slope = slope,
					Score = step * slope * mediaBloque
				});
			}
		}

		// Dos EDGE a menos de 10 ticks: se queda el de mayor score.
		var elegidos = new List<EdgeTermico>();

		foreach (var e in resultado.OrderByDescending(e => e.Score))
		{
			if (elegidos.All(x => Math.Abs(x.Fila - e.Fila) * tpf >= 10))
				elegidos.Add(e);
		}

		return elegidos;
	}
}

#endregion

#region Spike

public static partial class MotorTermico
{
	/// <summary>
	/// SPIKE (vol[] sin suavizar): grupo de 1 a 'maxAncho' filas con n >= minN y vol >= ratio × media de las 5 filas de
	/// arriba y las 5 de abajo (sin contar la propia espiga). Se busca alrededor de cada pico local; de las anchuras
	/// válidas se queda la de mayor score (n × ratio).
	/// </summary>
	public static List<SpikeTermico> BuscarEspigas(Perfil p, double volPoc, double minN, double ratio, int maxAncho, ZonaBusqueda? zona)
	{
		var vol = p.Valor;
		var candidatas = new List<SpikeTermico>();

		if (volPoc <= 0)
			return candidatas;

		maxAncho = Math.Max(1, maxAncho);

		for (var pico = 0; pico < vol.Length; pico++)
		{
			// Pico local (en una meseta cuenta su primera fila).
			if (vol[pico] / volPoc < minN || (pico > 0 && vol[pico - 1] >= vol[pico]) || (pico + 1 < vol.Length && vol[pico + 1] > vol[pico]))
				continue;

			SpikeTermico? mejor = null;

			for (var ancho = 1; ancho <= maxAncho; ancho++)
			{
				for (var desde = pico - ancho + 1; desde <= pico; desde++)
				{
					var hasta = desde + ancho - 1;

					if (desde < 0 || hasta >= vol.Length)
						continue;

					var media = Utilidades.MediaVecinas(vol, desde, hasta, 5);

					if (media < 0)
						continue;

					var valida = true;

					for (var i = desde; i <= hasta && valida; i++)
						valida = vol[i] / volPoc >= minN && vol[i] >= ratio * media;

					// La espiga tiene que terminar: si la fila de al lado también cumple, es un bloque más ancho que
					// 'maxAncho' (por ejemplo, la fila de abajo de un HVN de 5 filas), no una espiga.
					bool Cumple(int i) => i >= 0 && i < vol.Length && vol[i] / volPoc >= minN && vol[i] >= ratio * media;

					if (!valida || Cumple(desde - 1) || Cumple(hasta + 1))
						continue;

					var r = media > 0 ? vol[pico] / media : 99;
					var nPico = vol[pico] / volPoc;

					if (mejor is null || nPico * r > mejor.Score)
					{
						mejor = new SpikeTermico
						{
							Fila = pico,
							Desde = desde,
							Hasta = hasta,
							Precio = p.PrecioFila(pico),
							Volumen = vol[pico],
							N = nPico,
							Ratio = r,
							Score = nPico * r
						};
					}
				}
			}

			if (mejor is not null && (zona is not { } z || z.Contiene(mejor.Precio)))
				candidatas.Add(mejor);
		}

		// Espigas que se solapan: se queda la de mayor score.
		var elegidas = new List<SpikeTermico>();

		foreach (var e in candidatas.OrderByDescending(e => e.Score))
		{
			if (elegidas.All(x => e.Hasta < x.Desde || e.Desde > x.Hasta))
				elegidas.Add(e);
		}

		return elegidas.OrderBy(e => e.Fila).ToList();
	}
}

#endregion

#region Cluster

public static partial class MotorTermico
{
	/// <summary>
	/// CLÚSTER (vol[] sin suavizar): espigas con ClusterSpikeMinN y ClusterSpikeRatio agrupadas si están a
	/// ClusterGapTicks o menos. Válido con MinSpikesInCluster espigas y MinClusterHeightTicks de altura. Línea superior
	/// = espiga más alta; inferior = espiga más baja; cada una se mueve al EDGE que tenga a EdgeSnapRows filas o menos.
	/// </summary>
	public static List<ClusterTermico> DetectarClusters(Perfil p, double volPoc, ParametrosTermicos c, ZonaBusqueda? zona, List<EdgeTermico> edges)
	{
		var tpf = p.TicksPorFila;
		var espigas = BuscarEspigas(p, volPoc, c.ClusterSpikeMinN, c.ClusterSpikeRatio, c.SpikeMaxWidthRows, zona);
		var grupos = new List<List<SpikeTermico>>();

		foreach (var e in espigas)
		{
			if (grupos.Count > 0 && (e.Fila - grupos[^1][^1].Fila) * tpf <= c.ClusterGapTicks)
				grupos[^1].Add(e);
			else
				grupos.Add(new List<SpikeTermico> { e });
		}

		var clusters = grupos
			.Where(g => g.Count >= c.MinSpikesInCluster && (g[^1].Fila - g[0].Fila) * tpf >= c.MinClusterHeightTicks)
			.Select(g =>
			{
				var poc = g.OrderByDescending(e => e.Volumen).First();
				return new ClusterTermico
				{
					Espigas = g,
					FilaArriba = g[^1].Fila,
					FilaAbajo = g[0].Fila,
					FilaPoc = poc.Fila,
					Poc = poc.Precio,
					Score = g.Count * g.Average(e => e.N)
				};
			})
			.OrderByDescending(k => k.Score)
			.Take(Math.Max(0, c.MaxClusters))
			.ToList();

		// Ajuste fino: cada extremo se mueve al EDGE más cercano a EdgeSnapRows filas o menos; ese EDGE ya no se dibuja aparte.
		foreach (var k in clusters)
		{
			if (Cercano(k.FilaArriba) is { } arriba)
				(k.FilaArriba, k.ArribaEnEdge, arriba.Absorbido) = (arriba.Fila, true, true);

			if (Cercano(k.FilaAbajo) is { } abajo)
				(k.FilaAbajo, k.AbajoEnEdge, abajo.Absorbido) = (abajo.Fila, true, true);

			k.Arriba = p.PrecioFila(k.FilaArriba);
			k.Abajo = p.PrecioFila(k.FilaAbajo);
		}

		return clusters;

		EdgeTermico? Cercano(int fila) => edges
			.Where(e => !e.Absorbido && Math.Abs(e.Fila - fila) <= c.EdgeSnapRows)
			.OrderBy(e => Math.Abs(e.Fila - fila))
			.ThenByDescending(e => e.Score)
			.FirstOrDefault();
	}
}

#endregion

#region LVN

public static partial class MotorTermico
{
	/// <summary>
	/// LVN: mínimos locales de vol[] suavizado (sigma = LVNSmoothTicks / 2, en ticks), con su prominencia respecto al
	/// menor de sus dos picos. Los picos de referencia pueden estar fuera de la zona de búsqueda; el mínimo, no. Los LVN
	/// manuales se incluyen siempre (ranking 0) y anulan los automáticos a menos de LVNMinDistanceTicks.
	/// </summary>
	public static List<LvnTermico> DetectarLvn(Perfil p, ParametrosTermicos c, ZonaBusqueda? zona, IReadOnlyList<decimal>? manuales)
	{
		var resultado = c.ShowManualLVN ? Manuales(p, manuales) : new List<LvnTermico>();

		if (!c.EnableLVN || p.Filas < 3)
			return resultado;

		var tpf = p.TicksPorFila;
		var s = Utilidades.Gauss(p.Valor, c.LVNSmoothTicks / 2.0 / tpf);
		var poc = s.Max();

		if (poc <= 0)
			return resultado;

		var candidatos = new List<LvnTermico>();

		for (var i = 1; i < s.Length - 1; i++)
		{
			// Meseta [i, j] de valores iguales: si es un mínimo local, el LVN va en su centro.
			var j = i;

			while (j + 1 < s.Length && s[j + 1] == s[i])
				j++;

			var esMinimo = j + 1 < s.Length && s[i] <= s[i - 1] && s[j] <= s[j + 1] && (s[i] < s[i - 1] || s[j] < s[j + 1]);
			var centro = (i + j) / 2;
			i = j;

			if (!esMinimo)
				continue;

			var valle = s[centro];
			var picoIzquierdo = Pico(centro, -1);
			var picoDerecho = Pico(centro, +1);
			var menorPico = Math.Min(picoIzquierdo, picoDerecho);

			if (menorPico <= valle)
				continue;

			var prominencia = (menorPico - valle) / menorPico;
			var precio = p.PrecioFila(centro);

			if (prominencia * 100 < c.LVNMinProminence || valle > c.LVNMaxPercentOfPOC / 100.0 * poc)
				continue;

			if (zona is { } z && !z.Contiene(precio))
				continue;

			// Franja: alrededor del valle mientras el valor no suba más de un 25 % hacia el menor de sus picos.
			var techo = valle + 0.25 * (menorPico - valle);
			int inferior = centro, superior = centro;

			while (inferior - 1 >= 0 && s[inferior - 1] <= techo)
				inferior--;

			while (superior + 1 < s.Length && s[superior + 1] <= techo)
				superior++;

			candidatos.Add(new LvnTermico
			{
				Fila = centro,
				Inferior = inferior,
				Superior = superior,
				Precio = precio,
				Prominencia = prominencia,
				Valle = valle,
				Score = prominencia * (1 - valle / poc)
			});
		}

		// Distancia mínima (también respecto a los manuales) y los MaxLVN mejores.
		var elegidos = new List<LvnTermico>();

		foreach (var l in candidatos.OrderByDescending(l => l.Score))
		{
			if (elegidos.Count >= c.MaxLVN)
				break;

			if (elegidos.Concat(resultado).All(x => Math.Abs(x.Fila - l.Fila) * tpf >= c.LVNMinDistanceTicks))
				elegidos.Add(l);
		}

		for (var i = 0; i < elegidos.Count; i++)
			elegidos[i].Ranking = i + 1;

		resultado.AddRange(elegidos);
		return resultado;

		// Pico de un lado: el máximo hasta encontrar un valor más bajo que el valle (o el final del perfil).
		double Pico(int desde, int dir)
		{
			var maximo = double.MinValue;

			for (var k = desde + dir; k >= 0 && k < s.Length; k += dir)
			{
				if (s[k] < s[desde])
					break;

				maximo = Math.Max(maximo, s[k]);
			}

			return maximo;
		}
	}

	/// <summary>LVN marcados a mano: cuentan como LVN aunque el detector no los encuentre.</summary>
	public static List<LvnTermico> Manuales(Perfil p, IReadOnlyList<decimal>? manuales) =>
		(manuales ?? Array.Empty<decimal>())
		.Distinct()
		.OrderByDescending(x => x)
		.Select(precio =>
		{
			var fila = p.FilaDePrecio(precio);
			return new LvnTermico { Fila = fila, Inferior = fila, Superior = fila, Precio = precio, Manual = true };
		})
		.ToList();
}

#endregion

#region Color térmico

public static class PaletaTermica
{
	/// <summary>Los 4 colores de cada paleta (en n = 0, 0,33, 0,66 y 1).</summary>
	public static ColorArgb[] Colores(PaletasTermicas paleta, ColorArgb[]? personalizada) => paleta switch
	{
		PaletasTermicas.BlueRed => new ColorArgb[] { new(255, 30, 60, 200), new(255, 0, 200, 230), new(255, 255, 200, 0), new(255, 190, 0, 0) },
		PaletasTermicas.Grayscale => new ColorArgb[] { new(255, 70, 70, 70), new(255, 130, 130, 130), new(255, 190, 190, 190), new(255, 255, 255, 255) },
		PaletasTermicas.Custom when personalizada is { Length: 4 } => personalizada,
		_ => new ColorArgb[] { new(255, 255, 250, 160), new(255, 255, 205, 0), new(255, 255, 120, 0), new(255, 150, 0, 0) }
	};

	public static double Escalar(double n, EscalasTermicas escala)
	{
		n = Math.Clamp(n, 0, 1);

		return escala switch
		{
			EscalasTermicas.SquareRoot => Math.Sqrt(n),
			EscalasTermicas.Logarithmic => Math.Log10(1 + 9 * n),
			_ => n
		};
	}

	/// <summary>256 colores precalculados: el índice es n × 255 (antes de la escala), con la opacidad del perfil.</summary>
	public static ColorArgb[] Cache(ColorArgb[] cuatro, EscalasTermicas escala, byte opacidad)
	{
		var paradas = new[] { 0.0, 0.33, 0.66, 1.0 };
		var cache = new ColorArgb[256];

		for (var i = 0; i < 256; i++)
		{
			var t = Escalar(i / 255.0, escala);
			var tramo = t <= paradas[1] ? 0 : t <= paradas[2] ? 1 : 2;
			var f = (t - paradas[tramo]) / (paradas[tramo + 1] - paradas[tramo]);
			var a = cuatro[tramo];
			var b = cuatro[tramo + 1];
			cache[i] = new ColorArgb(opacidad, Mezclar(a.R, b.R, f), Mezclar(a.G, b.G, f), Mezclar(a.B, b.B, f));
		}

		return cache;

		static byte Mezclar(byte x, byte y, double f) => (byte)Math.Round(x + (y - x) * Math.Clamp(f, 0, 1));
	}

	/// <summary>Índice de la caché para un valor normalizado.</summary>
	public static int Indice(double n) => (int)Math.Round(Math.Clamp(n, 0, 1) * 255);
}

#endregion

#region Rango del perfil

/// <summary>Datos para elegir las velas del perfil según RangeMode. Las fechas del usuario están en hora del gráfico.</summary>
public sealed class ParametrosRango
{
	public ModosRango Modo { get; set; } = ModosRango.CustomDateTime;

	/// <summary>Desde / hasta en hora del gráfico (como los muestra ATAS).</summary>
	public DateTime Desde { get; set; }

	public DateTime Hasta { get; set; }

	public int LastNBars { get; set; } = 200;

	public TimeSpan SessionStart { get; set; } = new(9, 30, 0);

	public TimeSpan SessionEnd { get; set; } = new(16, 0, 0);

	public ZonasHorariasSesion ZonaSesion { get; set; } = ZonasHorariasSesion.NewYork;

	/// <summary>Diferencia de la hora del gráfico con UTC (InstrumentInfo.TimeZoneOffset de ATAS).</summary>
	public TimeSpan OffsetGrafico { get; set; }

	public bool UseCurrentBarAsEnd { get; set; } = true;

	public int PrimeraVisible { get; set; }

	public int UltimaVisible { get; set; }
}

public static class Rangos
{
	private static TimeZoneInfo? _nuevaYork;

	/// <summary>Nueva York con su horario de verano (nombre IANA, o el de Windows si no lo encuentra).</summary>
	public static TimeZoneInfo NuevaYork => _nuevaYork ??= Buscar("America/New_York", "Eastern Standard Time");

	private static TimeZoneInfo Buscar(string iana, string windows)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(iana);
		}
		catch (Exception)
		{
			return TimeZoneInfo.FindSystemTimeZoneById(windows);
		}
	}

	public static DateTime ANuevaYork(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NuevaYork);

	/// <summary>
	/// Fecha operativa de una vela: la de Nueva York 7 horas después. Así la sesión de Globex que abre a las 18:00 de
	/// Nueva York cuenta para el día siguiente, y una sesión diurna (09:30–16:00) para su propio día.
	/// </summary>
	public static DateTime FechaOperativa(DateTime utc) => ANuevaYork(utc).AddHours(7).Date;

	/// <summary>Primera vela con hora >= t (búsqueda binaria; las velas están en orden de tiempo).</summary>
	public static int PrimeraDesde(Func<int, DateTime> tiempo, int total, DateTime t)
	{
		int bajo = 0, alto = total;

		while (bajo < alto)
		{
			var medio = (bajo + alto) / 2;

			if (tiempo(medio) < t)
				bajo = medio + 1;
			else
				alto = medio;
		}

		return bajo;
	}

	/// <summary>Última vela con hora <= t (búsqueda binaria); -1 si no hay ninguna.</summary>
	public static int UltimaHasta(Func<int, DateTime> tiempo, int total, DateTime t) => PrimeraDesde(tiempo, total, t.AddTicks(1)) - 1;

	/// <summary>
	/// Velas del perfil [desde, hasta] (UTC en 'tiempo'). Sin UseCurrentBarAsEnd, los modos que llegan al presente se
	/// quedan en la última vela cerrada (el perfil no cambia en vivo). Devuelve null si el rango queda vacío.
	/// </summary>
	public static (int Desde, int Hasta)? Resolver(ParametrosRango r, Func<int, DateTime> tiempo, int total)
	{
		if (total <= 0)
			return null;

		var ultima = total - 1;
		var fin = r.UseCurrentBarAsEnd ? ultima : ultima - 1;
		int desde, hasta;

		switch (r.Modo)
		{
			case ModosRango.LastNBars:
				hasta = fin;
				desde = hasta - Math.Max(1, r.LastNBars) + 1;
				break;

			case ModosRango.VisibleRange:
				desde = r.PrimeraVisible;
				hasta = Math.Min(r.UltimaVisible, fin);
				break;

			case ModosRango.Session:
				return Sesion(r, tiempo, fin);

			case ModosRango.Day:
			case ModosRango.Week:
			case ModosRango.Month:
				if (fin < 0)
					return null;

				var clave = Periodo(r.Modo, tiempo(fin));
				desde = fin;

				while (desde - 1 >= 0 && Periodo(r.Modo, tiempo(desde - 1)) == clave)
					desde--;

				hasta = fin;
				break;

			default:
				// Fechas del usuario en hora del gráfico → UTC.
				desde = PrimeraDesde(tiempo, total, r.Desde - r.OffsetGrafico);
				hasta = r.UseCurrentBarAsEnd ? ultima : UltimaHasta(tiempo, total, r.Hasta - r.OffsetGrafico);
				break;
		}

		desde = Math.Max(0, desde);
		hasta = Math.Min(ultima, hasta);
		return desde <= hasta ? (desde, hasta) : null;
	}

	private static DateTime Periodo(ModosRango modo, DateTime utc)
	{
		var fecha = FechaOperativa(utc);

		return modo switch
		{
			ModosRango.Week => fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7)),
			ModosRango.Month => new DateTime(fecha.Year, fecha.Month, 1),
			_ => fecha
		};
	}

	/// <summary>La sesión más reciente con velas: horario SessionStart–SessionEnd (puede cruzar la medianoche).</summary>
	private static (int Desde, int Hasta)? Sesion(ParametrosRango r, Func<int, DateTime> tiempo, int fin)
	{
		(bool Dentro, DateTime Fecha) Leer(int bar)
		{
			var utc = DateTime.SpecifyKind(tiempo(bar), DateTimeKind.Utc);
			var local = r.ZonaSesion == ZonasHorariasSesion.NewYork ? ANuevaYork(utc) : utc + r.OffsetGrafico;
			var hora = local.TimeOfDay;
			var cruza = r.SessionStart > r.SessionEnd;
			var dentro = cruza ? hora >= r.SessionStart || hora < r.SessionEnd : hora >= r.SessionStart && hora < r.SessionEnd;
			var fecha = cruza && hora < r.SessionEnd ? local.Date.AddDays(-1) : local.Date;
			return (dentro, fecha);
		}

		var hasta = fin;

		while (hasta >= 0 && !Leer(hasta).Dentro)
			hasta--;

		if (hasta < 0)
			return null;

		var sesion = Leer(hasta).Fecha;
		var desde = hasta;

		while (desde - 1 >= 0 && Leer(desde - 1) is { Dentro: true } previa && previa.Fecha == sesion)
			desde--;

		return (desde, hasta);
	}
}

#endregion

#region Utilidades

public static class Utilidades
{
	/// <summary>División entera hacia abajo (también para precios negativos).</summary>
	public static long DivisionEntera(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

	public static double Media(double[] v, int desde, int hasta)
	{
		double suma = 0;
		var n = 0;

		for (var i = Math.Max(0, desde); i <= Math.Min(v.Length - 1, hasta); i++, n++)
			suma += v[i];

		return n == 0 ? 0 : suma / n;
	}

	public static double Media(IEnumerable<double> valores)
	{
		double suma = 0;
		var n = 0;

		foreach (var x in valores)
		{
			suma += x;
			n++;
		}

		return n == 0 ? 0 : suma / n;
	}

	/// <summary>Media de las 'cuantas' filas de abajo y las 'cuantas' de arriba de [desde, hasta]; -1 si no hay vecinas.</summary>
	public static double MediaVecinas(double[] v, int desde, int hasta, int cuantas)
	{
		double suma = 0;
		var n = 0;

		for (var i = desde - cuantas; i < desde; i++)
		{
			if (i >= 0)
			{
				suma += v[i];
				n++;
			}
		}

		for (var i = hasta + 1; i <= hasta + cuantas; i++)
		{
			if (i < v.Length)
			{
				suma += v[i];
				n++;
			}
		}

		return n == 0 ? -1 : suma / n;
	}

	/// <summary>Suavizado gaussiano (sigma en filas). En los extremos se normaliza con los pesos que caen dentro.</summary>
	public static double[] Gauss(double[] v, double sigma)
	{
		if (sigma < 0.3 || v.Length < 3)
			return (double[])v.Clone();

		var radio = (int)Math.Ceiling(3 * sigma);
		var pesos = new double[2 * radio + 1];

		for (var i = 0; i < pesos.Length; i++)
			pesos[i] = Math.Exp(-((i - radio) * (i - radio)) / (2 * sigma * sigma));

		var salida = new double[v.Length];

		for (var i = 0; i < v.Length; i++)
		{
			double suma = 0, pesoTotal = 0;

			for (var k = -radio; k <= radio; k++)
			{
				var j = i + k;

				if (j < 0 || j >= v.Length)
					continue;

				suma += v[j] * pesos[k + radio];
				pesoTotal += pesos[k + radio];
			}

			salida[i] = pesoTotal > 0 ? suma / pesoTotal : 0;
		}

		return salida;
	}

	/// <summary>Precio con los decimales del tick y punto decimal (texto ASCII para las etiquetas).</summary>
	public static string Formato(decimal precio, decimal tick)
	{
		var texto = tick.ToString(System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');
		var decimales = texto.Contains('.') ? texto.Length - texto.IndexOf('.') - 1 : 0;
		return precio.ToString("F" + Math.Max(0, decimales), System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>Lista de precios "15297.5;15206.25" (también admite coma decimal y espacios).</summary>
	public static List<decimal> LeerPrecios(string? texto)
	{
		var precios = new List<decimal>();

		foreach (var parte in (texto ?? "").Split(new[] { ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (decimal.TryParse(parte.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var p))
				precios.Add(p);
		}

		return precios;
	}

	public static string EscribirPrecios(IEnumerable<decimal> precios) =>
		string.Join(";", precios.Distinct().OrderBy(p => p).Select(p => p.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}

#endregion
