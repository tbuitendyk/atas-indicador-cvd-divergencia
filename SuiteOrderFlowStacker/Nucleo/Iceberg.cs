namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Parámetros de "2. Filtros Iceberg" y "3. Sesgo" (nombres de la especificación).</summary>
public sealed class ParametrosIceberg
{
	public decimal MinExecVisibleRatio { get; set; } = 3.0m;
	public decimal HighProbExecVisibleRatio { get; set; } = 5.0m;
	public int MaxDisplacementTicks { get; set; } = 2;
	public int DetectionWindowSeconds { get; set; } = 60;
	public int MinVisibleClipContracts { get; set; } = 1;
	public int MaxVisibleClipContracts { get; set; } = 50;
	public int SyntheticRefillMaxMs { get; set; } = 50;
	public int SyntheticSizeTolerancePercent { get; set; } = 20;
	public bool DetectSynthetic { get; set; } = true;
	public int HoldConfirmTicks { get; set; } = 4;
	public int BreakTicks { get; set; } = 2;

	/// <summary>ACTIVO = hubo recarga o ejecución en estos últimos segundos (la especificación dice "N segundos").</summary>
	public int ActiveRefillSeconds { get; set; } = 10;

	public ModoMbo ModoMbo { get; set; } = ModoMbo.Auto;

	// 3. Sesgo
	public int EventContextSeconds { get; set; } = 30;
	public int LevelToleranceTicks { get; set; } = 4;

	/// <summary>Órdenes MBO seguidas como máximo (límite de memoria en la apertura de NY).</summary>
	public int MaxTrackedOrders { get; set; } = 20000;
}

/// <summary>Lo que el motor de icebergs necesita saber del resto de la suite para el sesgo.</summary>
public interface IContextoIceberg
{
	/// <summary>Extremos para GIRO de un iceberg de ese lado (Compra: VAL, LVN, mínimo de sesión; Venta: VAH, LVN, máximo).</summary>
	IEnumerable<NivelReferencia> Extremos(Lado ladoIceberg);

	/// <summary>Retrocesos para CONTINUIDAD: POC, VWAP y zonas de stacked imbalance del mismo lado.</summary>
	IEnumerable<NivelReferencia> Retrocesos(Lado ladoIceberg);

	/// <summary>Pendiente de la VWAP o de la EMA: +1, -1 o 0.</summary>
	int Tendencia { get; }

	/// <summary>Último barrido o toma de stops con ese lado agresor entre esas horas.</summary>
	EventoFlujo? BarridoOStops(Lado agresor, DateTime desdeUtc, DateTime hastaUtc);
}

/// <summary>Iceberg confirmado (uno por lado y precio mientras esté vivo).</summary>
public sealed class Iceberg
{
	public EventoFlujo Evento = null!;

	/// <summary>Compra = orden pasiva en el BID (absorbe ventas agresivas); Venta = en el ASK.</summary>
	public Lado Lado;

	public decimal Precio, Ejecutado, Clip;
	public int Recargas;
	public ModoDatos Modo;
	public DateTime InicioUtc, UltimaActividadUtc;
	public string Estado = "ACTIVO", Sesgo = "NEUTRO", Contexto = "";
	public bool AltaProbabilidad;

	/// <summary>Ticks que el precio se alejó a favor (para "AGUANTANDO (+4 ticks)").</summary>
	public int TicksAFavor;

	public bool Vivo => Estado is "ACTIVO" or "AGUANTANDO";

	public decimal Ratio => Clip > 0 ? Ejecutado / Clip : 0;
}

/// <summary>
/// IcebergTrackerPro (el principal). Tres modos de detección:
/// NATIVO (MBO): la MISMA orden (ExchangeOrderId) reaparece en el mismo precio con el visible restaurado y PEOR
/// prioridad después de ejecutarse. SINTÉTICO (MBO): tras el llenado completo de una orden, aparece otra NUEVA en el
/// mismo precio y lado, de tamaño parecido, en menos de SyntheticRefillMaxMs. INFERIDO (sin MBO): el volumen agresivo
/// ejecutado contra un nivel supera MinExecVisibleRatio veces el máximo visible en el DOM sin que el precio se desplace.
/// Ningún iceberg se "prevé": todos se detectan cuando ya se están ejecutando.
/// </summary>
public sealed class MotorIceberg
{
	#region Estado

	private sealed class Candidato
	{
		public ModoDatos Modo;
		public Lado Lado;
		public long TickPrecio;
		public decimal Ejecutado, MaxVisible, EjecDesdeSubida;
		public int Recargas;
		public readonly List<decimal> Tamanos = new();
		public DateTime Inicio, Ultima;
		public int Desplazamiento;
		public Iceberg? Confirmado;

		public void Reiniciar(DateTime utc)
		{
			(Ejecutado, MaxVisible, EjecDesdeSubida, Recargas, Desplazamiento) = (0, 0, 0, 0, 0);
			Tamanos.Clear();
			Inicio = Ultima = utc;
		}
	}

	private sealed class Orden
	{
		public long Id, Prioridad, TickPrecio;
		public Lado Lado;
		public decimal Visible;

		// Ciclo = desde la última recarga: trades atribuidos por IdPasivo, reducciones con trade y tamaño mostrado.
		public decimal CicloTrades, CicloReducciones, CicloMostrado, EjecCerrado;
		public DateTime? LlenaUtc;
		public DateTime Ultima;
		public Candidato C = null!;

		public decimal EjecCiclo => Math.Max(CicloTrades, CicloReducciones);

		public decimal Ejecutado => EjecCerrado + EjecCiclo;
	}

	private sealed class Cadena
	{
		public Candidato C = null!;
		public DateTime? LlenadoUtc;
		public decimal TamanoLlenado;
		public long? OrdenActual;
	}

	private readonly RegistroEventos _eventos;
	private readonly Dictionary<long, Orden> _ordenes = new();
	private readonly Dictionary<(Lado, long), Cadena> _cadenas = new();
	private readonly Dictionary<(Lado, long), Candidato> _inferidos = new();
	private readonly Dictionary<long, decimal> _bids = new(), _asks = new();
	private readonly List<(DateTime Utc, long Tick, Lado Agresor, decimal Vol)> _recientes = new();
	private readonly HashSet<Candidato> _seguidos = new();
	private DateTime _limpieza;

	public MotorIceberg(RegistroEventos eventos) => _eventos = eventos;

	public ParametrosIceberg P { get; set; } = new();

	public TablaUmbrales Umbrales { get; set; } = new();

	public ClasificadorSesion Sesiones { get; set; } = new();

	public bool Mnq { get; set; }

	public decimal Tick { get; set; } = 0.25m;

	public IContextoIceberg? Contexto { get; set; }

	/// <summary>Se avisa a la suite al confirmar un iceberg (etiqueta "con iceberg" de las zonas de imbalance).</summary>
	public Action<Iceberg>? AlConfirmar { get; set; }

	public List<Iceberg> Icebergs { get; } = new();

	public DateTime? UltimoMboUtc { get; private set; }

	public decimal UltimoPrecio { get; private set; }

	public DateTime Ahora { get; private set; }

	/// <summary>"MBO" si llegan mensajes MBO (y no se pidió solo inferido); si no, "Inferido (sin MBO)".</summary>
	public bool ModoMboActivo => P.ModoMbo == ModoMbo.Auto && UltimoMboUtc is not null;

	public string TextoModo => ModoMboActivo ? "MBO" : "Inferido (sin MBO)";

	public int OrdenesSeguidas => _ordenes.Count;

	public void Reiniciar()
	{
		_ordenes.Clear();
		_cadenas.Clear();
		_inferidos.Clear();
		_bids.Clear();
		_asks.Clear();
		_recientes.Clear();
		_seguidos.Clear();
		Icebergs.Clear();
		UltimoMboUtc = null;
	}

	#endregion

	#region Datos

	public void Trade(TradeFlujo t)
	{
		Ahora = t.Utc;
		UltimoPrecio = t.Precio;
		var tick = PrecioMath.Tick(t.Precio, Tick);

		if (t.Agresor is { } agresor && t.Volumen > 0)
		{
			_recientes.Add((t.Utc, tick, agresor, t.Volumen));

			// Ejecución contra la orden pasiva identificada (MBO).
			if (ModoMboActivo && t.IdPasivo is { } idPasivo && _ordenes.TryGetValue(idPasivo, out var o))
			{
				o.CicloTrades += t.Volumen;
				Actividad(o.C, o.Ejecutado, t.Utc);
			}

			// Inferido: el volumen agresivo cuenta contra el nivel pasivo del lado contrario.
			if (!ModoMboActivo)
				EjecucionInferida(agresor.Contrario(), tick, t.Volumen, t.Utc);
		}

		// Desplazamiento de los candidatos con ejecuciones y estados de los icebergs confirmados.
		foreach (var c in _seguidos)
		{
			var mas = c.Lado == Lado.Compra ? c.TickPrecio - tick : tick - c.TickPrecio;
			c.Desplazamiento = (int)Math.Max(c.Desplazamiento, mas);
		}

		foreach (var i in Icebergs.Where(i => i.Vivo).ToList())
			EstadoPorPrecio(i, t.Precio, t.Utc);

		Mantenimiento(t.Utc);
	}

	public void Dom(CambioDom d)
	{
		Ahora = d.Utc;
		var tick = PrecioMath.Tick(d.Precio, Tick);
		var libro = d.EsAsk ? _asks : _bids;
		var antes = libro.TryGetValue(tick, out var v) ? v : 0;

		if (d.Volumen > 0)
			libro[tick] = d.Volumen;
		else
			libro.Remove(tick);

		if (ModoMboActivo || !_inferidos.TryGetValue((d.EsAsk ? Lado.Venta : Lado.Compra, tick), out var c))
			return;

		c.MaxVisible = Math.Max(c.MaxVisible, d.Volumen);

		// El visible vuelve a subir después de ejecutarse: recarga estimada.
		if (d.Volumen > antes && c.EjecDesdeSubida > 0)
		{
			c.Recargas++;
			c.Tamanos.Add(d.Volumen);
			c.EjecDesdeSubida = 0;
			Actividad(c, c.Ejecutado, d.Utc);
		}

		// El nivel desaparece sin ejecuciones recientes y con el precio cerca: el iceberg se retiró.
		if (d.Volumen <= 0 && c.Confirmado is { Vivo: true } i && !HuboTrade(tick, c.Lado, d.Utc, 1000) &&
			Math.Abs(UltimoPrecio - i.Precio) <= P.HoldConfirmTicks * Tick)
		{
			CambiarEstado(i, "CANCELADO", d.Utc);
		}
	}

	public void Mbo(MensajeMbo m)
	{
		if (P.ModoMbo == ModoMbo.SoloInferido)
			return;

		Ahora = m.Utc;
		UltimoMboUtc = m.Utc;
		var tick = PrecioMath.Tick(m.Precio, Tick);
		var lado = m.EsAsk ? Lado.Venta : Lado.Compra;

		switch (m.Tipo)
		{
			case TipoMbo.Snapshot:
				// Reconexión o primera carga: se reconstruye el libro sin buscar recargas.
				if (!_ordenes.TryGetValue(m.Id, out var s) || s.TickPrecio != tick)
					_ordenes[m.Id] = NuevaOrden(m, tick, lado);
				else
					(s.Visible, s.Prioridad, s.LlenaUtc) = (m.Volumen, m.Prioridad, null);

				break;

			case TipoMbo.Nueva:
				if (_ordenes.TryGetValue(m.Id, out var previa) && previa.TickPrecio == tick && previa.Lado == lado && previa.LlenaUtc is not null)
				{
					// La misma orden reaparece tras llenarse: recarga NATIVA.
					Recarga(previa, m);
				}
				else
				{
					var nueva = NuevaOrden(m, tick, lado);
					_ordenes[m.Id] = nueva;
					RecargaSintetica(nueva, m);
				}

				break;

			case TipoMbo.Cambio:
				if (!_ordenes.TryGetValue(m.Id, out var o))
				{
					_ordenes[m.Id] = NuevaOrden(m, tick, lado);
					break;
				}

				if (o.TickPrecio != tick || o.Lado != lado)
				{
					// La orden se movió de precio: en el precio anterior se canceló.
					if (o.C.Confirmado is { Vivo: true } movida)
						CambiarEstado(movida, "CANCELADO", m.Utc);

					_ordenes[m.Id] = NuevaOrden(m, tick, lado);
					break;
				}

				if (m.Volumen <= 0)
				{
					Borrada(o, m.Utc);
					break;
				}

				var conTrade = o.EjecCiclo > 0 || HuboTrade(tick, lado, m.Utc, 500);

				if (m.Prioridad > o.Prioridad && m.Volumen > 0 && conTrade)
				{
					Recarga(o, m);
				}
				else
				{
					if (m.Volumen < o.Visible && conTrade)
					{
						o.CicloReducciones += o.Visible - m.Volumen;
						Actividad(o.C, o.Ejecutado, m.Utc);
					}

					(o.Visible, o.Prioridad, o.Ultima) = (m.Volumen, m.Prioridad, m.Utc);
				}

				break;

			case TipoMbo.Borrada:
				if (_ordenes.TryGetValue(m.Id, out var b))
					Borrada(b, m.Utc);

				break;
		}

		Mantenimiento(m.Utc);
	}

	private Orden NuevaOrden(MensajeMbo m, long tick, Lado lado)
	{
		var c = new Candidato { Modo = ModoDatos.MBO, Lado = lado, TickPrecio = tick, Inicio = m.Utc, Ultima = m.Utc };
		c.Tamanos.Add(m.Volumen);

		return new Orden
		{
			Id = m.Id,
			Lado = lado,
			TickPrecio = tick,
			Visible = m.Volumen,
			Prioridad = m.Prioridad,
			CicloMostrado = m.Volumen,
			Ultima = m.Utc,
			C = c
		};
	}

	/// <summary>Recarga NATIVA: el ciclo anterior se cierra (si no se vio ejecutar, se cuenta el tamaño mostrado).</summary>
	private void Recarga(Orden o, MensajeMbo m)
	{
		if (o.C.Ultima < m.Utc - TimeSpan.FromSeconds(Math.Max(1, P.DetectionWindowSeconds)) && o.C.Confirmado is null)
		{
			o.EjecCerrado = 0;
			o.C.Reiniciar(m.Utc);
		}

		o.EjecCerrado += Math.Max(o.EjecCiclo, o.CicloMostrado);
		(o.CicloTrades, o.CicloReducciones, o.CicloMostrado) = (0, 0, m.Volumen);
		(o.Visible, o.Prioridad, o.LlenaUtc, o.Ultima) = (m.Volumen, m.Prioridad, null, m.Utc);
		o.C.Recargas++;
		o.C.Tamanos.Add(m.Volumen);
		Actividad(o.C, o.Ejecutado, m.Utc);
	}

	/// <summary>Orden borrada: llenado (con trade en ese precio) o cancelación.</summary>
	private void Borrada(Orden o, DateTime utc)
	{
		var llenada = o.EjecCiclo > 0 || HuboTrade(o.TickPrecio, o.Lado, utc, 500);

		if (!llenada)
		{
			if (o.C.Confirmado is { Vivo: true } i)
				CambiarEstado(i, "CANCELADO", utc);

			if (_cadenas.TryGetValue((o.Lado, o.TickPrecio), out var k) && k.OrdenActual == o.Id && k.C.Confirmado is { Vivo: true } s)
				CambiarEstado(s, "CANCELADO", utc);

			_ordenes.Remove(o.Id);
			return;
		}

		// Llenada: puede reaparecer con el mismo Id (nativo) o como orden nueva parecida (sintético).
		var ejecutado = Math.Max(o.EjecCiclo, o.CicloMostrado);
		o.LlenaUtc = utc;
		o.Visible = 0;

		if (!P.DetectSynthetic)
			return;

		var clave = (o.Lado, o.TickPrecio);

		if (!_cadenas.TryGetValue(clave, out var cadena))
		{
			cadena = new Cadena { C = new Candidato { Modo = ModoDatos.Sintetico, Lado = o.Lado, TickPrecio = o.TickPrecio, Inicio = utc, Ultima = utc } };
			_cadenas[clave] = cadena;
		}

		if (cadena.C.Confirmado is null && cadena.C.Ultima < utc - TimeSpan.FromSeconds(Math.Max(1, P.DetectionWindowSeconds)))
			cadena.C.Reiniciar(utc);

		cadena.LlenadoUtc = utc;
		cadena.TamanoLlenado = o.CicloMostrado;
		cadena.C.Ejecutado += ejecutado;

		if (cadena.C.Tamanos.Count == 0)
			cadena.C.Tamanos.Add(o.CicloMostrado);

		Actividad(cadena.C, cadena.C.Ejecutado, utc);
	}

	/// <summary>SINTÉTICO: orden nueva del mismo lado y precio, tamaño ±tolerancia, en menos de SyntheticRefillMaxMs.</summary>
	private void RecargaSintetica(Orden nueva, MensajeMbo m)
	{
		if (!P.DetectSynthetic || !_cadenas.TryGetValue((nueva.Lado, nueva.TickPrecio), out var cadena) || cadena.LlenadoUtc is not { } llenado)
			return;

		var ms = (m.Utc - llenado).TotalMilliseconds;
		var tolerancia = cadena.TamanoLlenado * Math.Max(0, P.SyntheticSizeTolerancePercent) / 100m;

		if (ms < 0 || ms > Math.Max(1, P.SyntheticRefillMaxMs) || Math.Abs(m.Volumen - cadena.TamanoLlenado) > tolerancia)
			return;

		cadena.LlenadoUtc = null;
		cadena.OrdenActual = nueva.Id;
		cadena.C.Recargas++;
		cadena.C.Tamanos.Add(m.Volumen);
		Actividad(cadena.C, cadena.C.Ejecutado, m.Utc);
	}

	private void EjecucionInferida(Lado ladoPasivo, long tick, decimal volumen, DateTime utc)
	{
		var libro = ladoPasivo == Lado.Compra ? _bids : _asks;
		var visible = libro.TryGetValue(tick, out var v) ? v : 0;

		if (!_inferidos.TryGetValue((ladoPasivo, tick), out var c))
		{
			c = new Candidato { Modo = ModoDatos.Inferido, Lado = ladoPasivo, TickPrecio = tick, Inicio = utc, Ultima = utc };
			_inferidos[(ladoPasivo, tick)] = c;
		}
		else if (c.Confirmado is null && c.Ultima < utc - TimeSpan.FromSeconds(Math.Max(1, P.DetectionWindowSeconds)))
		{
			c.Reiniciar(utc);
		}

		c.MaxVisible = Math.Max(c.MaxVisible, visible);
		c.Ejecutado += volumen;
		c.EjecDesdeSubida += volumen;
		Actividad(c, c.Ejecutado, utc);
	}

	private bool HuboTrade(long tick, Lado ladoPasivo, DateTime utc, int ms)
	{
		var desde = utc - TimeSpan.FromMilliseconds(ms);

		for (var i = _recientes.Count - 1; i >= 0; i--)
		{
			var r = _recientes[i];

			if (r.Utc < desde)
				break;

			if (r.Tick == tick && r.Agresor == ladoPasivo.Contrario() && r.Utc <= utc + TimeSpan.FromMilliseconds(ms))
				return true;
		}

		return false;
	}

	#endregion

	#region Detección

	/// <summary>Nueva ejecución o recarga de un candidato: se actualiza y se evalúa (o se refresca el iceberg).</summary>
	private void Actividad(Candidato c, decimal ejecutado, DateTime utc)
	{
		c.Ejecutado = ejecutado;
		c.Ultima = utc;

		if (ejecutado > 0)
			_seguidos.Add(c);

		if (c.Confirmado is { } i)
		{
			if (i.Vivo)
				Refrescar(i, c, utc);

			return;
		}

		Evaluar(c, utc);
	}

	private (decimal Clip, decimal Ratio, int Recargas) Medidas(Candidato c)
	{
		if (c.Modo == ModoDatos.Inferido)
		{
			var clip = c.MaxVisible;
			var estimadas = clip > 0 ? (int)Math.Floor(c.Ejecutado / clip) - 1 : 0;
			return (clip, clip > 0 ? c.Ejecutado / clip : 0, Math.Max(c.Recargas, estimadas));
		}

		var moda = Moda(c.Tamanos);
		return (moda, moda > 0 ? c.Ejecutado / moda : 0, c.Recargas);
	}

	private void Evaluar(Candidato c, DateTime utc)
	{
		var sesion = Sesiones.De(utc);
		var u = Umbrales.De(sesion, Mnq);
		var (clip, ratio, recargas) = Medidas(c);

		if (clip <= 0 || clip > Math.Max(1, P.MaxVisibleClipContracts) || c.Ejecutado < u.MinEjecutado || ratio < P.MinExecVisibleRatio ||
			c.Desplazamiento > Math.Max(0, P.MaxDisplacementTicks))
		{
			return;
		}

		// Nativo y sintético exigen recargas vistas; el inferido las estima (el DOM agrega muchas órdenes por nivel).
		if (c.Modo != ModoDatos.Inferido && recargas < u.MinRecargas)
			return;

		var precio = c.TickPrecio * Tick;

		// Un solo iceberg vivo por lado y precio: si ya hay uno, este candidato se le suma.
		if (Icebergs.FirstOrDefault(i => i.Vivo && i.Lado == c.Lado && i.Precio == precio) is { } existente)
		{
			c.Confirmado = existente;

			if (Rango(c.Modo) > Rango(existente.Modo))
				existente.Modo = c.Modo;

			Refrescar(existente, c, utc);
			return;
		}

		var iceberg = new Iceberg
		{
			Lado = c.Lado,
			Precio = precio,
			Modo = c.Modo,
			InicioUtc = c.Inicio,
			UltimaActividadUtc = utc
		};

		iceberg.Evento = _eventos.Crear("Iceberg", "Iceberg", c.Lado, utc, precio, c.Modo, sesion);
		c.Confirmado = iceberg;
		Icebergs.Add(iceberg);
		Medir(iceberg, c);
		iceberg.AltaProbabilidad = EsAltaProbabilidad(iceberg, u);
		Sesgo(iceberg);
		Texto(iceberg);
		_eventos.Publicar(iceberg.Evento, utc);
		AlConfirmar?.Invoke(iceberg);

		if (Icebergs.Count > 300)
			Icebergs.RemoveRange(0, Icebergs.Count - 300);
	}

	private static int Rango(ModoDatos m) => m switch { ModoDatos.MBO => 3, ModoDatos.Sintetico => 2, _ => 1 };

	private void Medir(Iceberg i, Candidato c)
	{
		var (clip, _, recargas) = Medidas(c);
		i.Ejecutado = Math.Max(i.Ejecutado, c.Ejecutado);
		i.Clip = clip;
		i.Recargas = Math.Max(i.Recargas, recargas);
	}

	private bool EsAltaProbabilidad(Iceberg i, UmbralSesion u) => i.Ejecutado >= u.AltaProbabilidad && i.Ratio >= P.HighProbExecVisibleRatio;

	/// <summary>Más ejecución o recargas en un iceberg confirmado: texto al día; publica solo si pasa a "*".</summary>
	private void Refrescar(Iceberg i, Candidato c, DateTime utc)
	{
		Medir(i, c);
		i.UltimaActividadUtc = utc;

		if (!i.AltaProbabilidad && EsAltaProbabilidad(i, Umbrales.De(i.Evento.Sesion, Mnq)))
		{
			i.AltaProbabilidad = true;
			Texto(i);
			_eventos.Publicar(i.Evento, utc);
			return;
		}

		Texto(i);
	}

	private static decimal Moda(List<decimal> tamanos) =>
		tamanos.Count == 0 ? 0 : tamanos.GroupBy(t => t).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;

	#endregion

	#region Estados y sesgo

	/// <summary>ROTO si el precio atraviesa el nivel BreakTicks; AGUANTANDO si dejó de recargarse y se alejó a favor.</summary>
	private void EstadoPorPrecio(Iceberg i, decimal precio, DateTime utc)
	{
		var signo = i.Lado.Signo();
		var ticks = (int)Math.Round((precio - i.Precio) / Tick * signo);
		i.TicksAFavor = Math.Max(i.TicksAFavor, ticks);

		if (ticks <= -Math.Max(1, P.BreakTicks))
		{
			CambiarEstado(i, "ROTO", utc);
			return;
		}

		if (i.Estado == "ACTIVO" && ticks >= Math.Max(1, P.HoldConfirmTicks) && utc - i.UltimaActividadUtc >= TimeSpan.FromSeconds(Math.Max(0, P.ActiveRefillSeconds)))
			CambiarEstado(i, "AGUANTANDO", utc);
	}

	/// <summary>Reloj (la carcasa lo llama cada segundo): AGUANTANDO sin trades nuevos y sesgo al día.</summary>
	public void Tiempo(DateTime utc)
	{
		Ahora = utc;

		foreach (var i in Icebergs.Where(i => i.Vivo).ToList())
		{
			if (UltimoPrecio > 0)
				EstadoPorPrecio(i, UltimoPrecio, utc);

			if (!i.Vivo)
				continue;

			var antes = i.Sesgo;
			Sesgo(i);

			if (i.Sesgo != antes)
			{
				Texto(i);
				_eventos.Publicar(i.Evento, utc);
			}
		}

		Mantenimiento(utc);
	}

	private void CambiarEstado(Iceberg i, string estado, DateTime utc)
	{
		if (i.Estado == estado || !i.Vivo)
			return;

		i.Estado = estado;
		Sesgo(i);
		Texto(i);
		_eventos.Publicar(i.Evento, utc);
	}

	/// <summary>
	/// Sesgo con reglas objetivas. RUPTURA: iceberg ROTO. GIRO: en un extremo (VAL/VAH, LVN, mínimo/máximo de sesión)
	/// o en la ventana de un barrido o toma de stops del lado contrario (el iceberg absorbe a ese agresor).
	/// CONTINUIDAD: a favor de la tendencia, en un retroceso a POC, VWAP o stacked imbalance del mismo lado.
	/// </summary>
	public void Sesgo(Iceberg i)
	{
		if (i.Estado == "ROTO")
		{
			(i.Sesgo, i.Contexto) = ("RUPTURA", "Roto");
			return;
		}

		if (i.Estado == "CANCELADO" || Contexto is null)
		{
			if (Contexto is null)
				(i.Sesgo, i.Contexto) = ("NEUTRO", "");

			return;
		}

		var tolerancia = Math.Max(0, P.LevelToleranceTicks) * Tick;
		var ventana = TimeSpan.FromSeconds(Math.Max(1, P.EventContextSeconds));

		if (Contexto.BarridoOStops(i.Lado.Contrario(), i.InicioUtc - ventana, i.UltimaActividadUtc) is { } barrido)
		{
			(i.Sesgo, i.Contexto) = ("GIRO", barrido.Tipo == "Sweep" ? "PostSweep" : "PostStops");

			if (!string.IsNullOrEmpty(barrido.Contexto))
				i.Contexto += " " + barrido.Contexto;

			return;
		}

		if (Contexto.Extremos(i.Lado).FirstOrDefault(n => Math.Abs(n.Precio - i.Precio) <= tolerancia) is { Nombre: not null } extremo)
		{
			(i.Sesgo, i.Contexto) = ("GIRO", extremo.Nombre);
			return;
		}

		if (Contexto.Tendencia == i.Lado.Signo() &&
			Contexto.Retrocesos(i.Lado).FirstOrDefault(n => Math.Abs(n.Precio - i.Precio) <= tolerancia) is { Nombre: not null } retroceso)
		{
			(i.Sesgo, i.Contexto) = ("CONTINUIDAD", "Tendencia " + retroceso.Nombre);
			return;
		}

		(i.Sesgo, i.Contexto) = ("NEUTRO", "");
	}

	private void Texto(Iceberg i)
	{
		var e = i.Evento;
		e.Modo = i.Modo;
		e.Volumen = Math.Round(i.Ejecutado, 0);
		e.Clip = (int)Math.Round(i.Clip);
		e.Recargas = i.Recargas;
		e.Ratio = Math.Round(i.Ratio, 1);
		e.Estado = i.Estado;
		e.Sesgo = i.Sesgo;
		e.Contexto = i.Contexto;
		e.AltaProbabilidad = i.AltaProbabilidad;

		var estrella = i.AltaProbabilidad ? "* " : "";
		var estado = i.Estado == "AGUANTANDO" ? $"AGUANTANDO (+{i.TicksAFavor} ticks)" : i.Estado;
		var ratio = i.Ratio.ToString("0", CultureInfo.InvariantCulture);

		e.Texto = $"{estrella}ICEBERG {i.Lado.Texto()} {i.Modo.Texto()} | {Textos.Precio(i.Precio, Tick)} | {e.Volumen:0}c | " +
			$"{i.Recargas} recargas | {ratio}:1 | {estado} | {i.Sesgo} | {Textos.Hora(i.UltimaActividadUtc)}";
	}

	#endregion

	#region Mantenimiento

	/// <summary>Limpieza cada segundo: trades recientes, órdenes muertas y límite de memoria (MaxTrackedOrders).</summary>
	private void Mantenimiento(DateTime utc)
	{
		if (utc - _limpieza < TimeSpan.FromSeconds(1))
			return;

		_limpieza = utc;
		var corte = utc - TimeSpan.FromSeconds(2);
		var n = _recientes.FindIndex(r => r.Utc >= corte);
		_recientes.RemoveRange(0, n < 0 ? _recientes.Count : n);

		var ventana = TimeSpan.FromSeconds(Math.Max(1, P.DetectionWindowSeconds));

		foreach (var o in _ordenes.Values.Where(o => o.LlenaUtc is { } l && utc - l > TimeSpan.FromSeconds(1)).ToList())
			_ordenes.Remove(o.Id);

		if (_ordenes.Count > Math.Max(1000, P.MaxTrackedOrders))
		{
			foreach (var o in _ordenes.Values.Where(o => o.C.Confirmado is null).OrderBy(o => o.Ultima).Take(_ordenes.Count - P.MaxTrackedOrders).ToList())
				_ordenes.Remove(o.Id);
		}

		foreach (var k in _cadenas.Where(k => k.Value.C.Confirmado is not { Vivo: true } && utc - k.Value.C.Ultima > ventana).Select(k => k.Key).ToList())
			_cadenas.Remove(k);

		foreach (var k in _inferidos.Where(k => k.Value.Confirmado is not { Vivo: true } && utc - k.Value.Ultima > ventana).Select(k => k.Key).ToList())
			_inferidos.Remove(k);

		_seguidos.RemoveWhere(c => c.Confirmado is not { Vivo: true } && utc - c.Ultima > ventana);
	}

	#endregion
}
