namespace OrderFlowSuite.Atas;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using OrderFlowSuite.Comun;
using OrderFlowSuite.Nucleo;

/// <summary>Parámetros del grupo "0. General" (comunes a los cinco indicadores; nombres de la especificación).</summary>
public sealed class ParametrosGenerales
{
	public ModoVisual DisplayMode { get; set; } = ModoVisual.Minimal;
	public EsquinaPanel InfoPanelPosition { get; set; } = EsquinaPanel.TopRight;
	public int InfoPanelMaxLines { get; set; } = 5;
	public int FontSize { get; set; } = 11;
	public int EventLifetimeMin { get; set; } = 30;
	public PerfilInstrumento InstrumentProfile { get; set; } = PerfilInstrumento.Auto;
	public bool EnableAlerts { get; set; } = true;

	/// <summary>Panel de texto propio. v3: los trackers lo traen apagado (su último evento se ve en Sessions Sweeps Pro).</summary>
	public bool ShowInfoPanel { get; set; } = true;
	public string AlertSoundFile { get; set; } = "alert1";
	public bool EnableRecording { get; set; } = true;
	public bool RealTimeOnly { get; set; }
	public bool LoadRecordedEvents { get; set; } = true;
	public int RecordedDaysToLoad { get; set; } = 5;
	public int InfoPanelOffsetX { get; set; }
	public int InfoPanelOffsetY { get; set; }
}

/// <summary>
/// Lo común de las carcasas de ATAS (composición, sin clase base: el repositorio oficial no usa bases abstractas para
/// indicadores). Pasa las velas cerradas, los trades, el DOM y el MBO a SU MotorSuite; suena las alertas (solo en vivo,
/// una por evento y por cambio de estado); graba el CSV; recarga los días anteriores; y dibuja el panel de texto.
/// No usa tipos de ATAS: el indicador le pasa los datos ya convertidos y las acciones (AddAlert, RedrawChart, LogError).
/// </summary>
public sealed class CarcasaSuite : IDisposable
{
	#region Estado

	private readonly List<DateTime> _tiempos = new();
	private readonly HashSet<string> _alertadas = new();
	private readonly Stopwatch _cronometro = new();
	private DateTime _ultimoMercado;
	private int _ultimaBarra = -1;
	private bool _enVivo;
	private string? _instrumento;
	private decimal _tick;
	private GrabadorEventos? _grabador;
	private Timer? _reloj;
	private long _publicacionesDibujadas;
	private RenderFont? _fuente;
	private int _tamanoFuente;

	private readonly List<EventoSuite> _paraBus = new();
	private DateTime _ultimoLatido;
	private bool _latidoConDatos;

	public CarcasaSuite(string indicador, bool conMaster)
	{
		Indicador = indicador;
		Suite = new MotorSuite(Cfg, conMaster);

		// Bus de la suite: cada indicador publica SOLO sus eventos (aunque corra todos los motores por dentro).
		Suite.Eventos.AlPublicar = e =>
		{
			if (e.Indicador == Indicador && AEventoBus(e) is { } x)
				_paraBus.Add(x);
		};

		if (indicador == "Imbalance")
		{
			Suite.Imbalance.AlImbalances = (utc, lado, precios) => _paraBus.Add(new ImbalanceEvent
			{
				Id = RegistroEventos.IdEstable("Imbalance", lado, utc, precios[0]),
				Fuente = FuenteSuite.Imbalance,
				Utc = utc,
				CambioUtc = utc,
				Version = 1,
				Precios = precios,
				Compra = lado == Lado.Compra
			});
		}
	}

	/// <summary>Fuentes del bus que representa este indicador (para el latido).</summary>
	public IReadOnlyList<FuenteSuite> Fuentes => Indicador switch
	{
		"Imbalance" => new[] { FuenteSuite.Stacked, FuenteSuite.Imbalance },
		"Iceberg" => new[] { FuenteSuite.Iceberg },
		"Stops" => new[] { FuenteSuite.Stops },
		"Sweeps" => new[] { FuenteSuite.Sweeps },
		"Master" => new[] { FuenteSuite.Absorption },
		_ => Array.Empty<FuenteSuite>()
	};

	/// <summary>Un evento de la suite traducido al tipo del bus (null si no se publica).</summary>
	private EventoSuite? AEventoBus(EventoFlujo e)
	{
		var compra = e.Lado == Lado.Compra;

		return e.Tipo switch
		{
			"Iceberg" => new IcebergEvent
			{
				Id = e.Id, Fuente = FuenteSuite.Iceberg, Utc = e.Utc, CambioUtc = e.CambioUtc, Version = e.Version, Precio = e.Precio, Compra = compra,
				Estado = e.Estado, Ejecutado = e.Volumen, Recargas = e.Recargas ?? 0, Mbo = e.Modo is ModoDatos.MBO or ModoDatos.Sintetico,
				AltaProbabilidad = e.AltaProbabilidad
			},
			"StopRun" => new StopsEvent
			{
				Id = e.Id, Fuente = FuenteSuite.Stops, Utc = e.Utc, CambioUtc = e.CambioUtc, Version = e.Version, Desde = e.Precio,
				Extremo = e.PrecioFin ?? e.Precio, Compra = compra, Contratos = e.Volumen, Estado = e.Estado, Zona = e.Contexto
			},
			"Sweep" => new SweepTrackerEvent
			{
				Id = e.Id, Fuente = FuenteSuite.Sweeps, Utc = e.Utc, CambioUtc = e.CambioUtc, Version = e.Version, Desde = e.Precio,
				Hasta = e.PrecioFin ?? e.Precio, Niveles = e.Niveles ?? 0, Contratos = e.Volumen, DuracionMs = e.DuracionMs ?? 0, Compra = compra, Tipo = e.Estado
			},
			"StackedImb" => new StackedEvent
			{
				Id = e.Id, Fuente = FuenteSuite.Stacked, Utc = e.Utc, CambioUtc = e.CambioUtc, Version = e.Version, Bajo = e.Precio,
				Alto = e.PrecioFin ?? e.Precio, Niveles = e.Niveles ?? 0, Compra = compra, Estado = e.Estado
			},
			"Signal" => new AbsorptionEvent
			{
				Id = e.Id, Fuente = FuenteSuite.Absorption, Utc = e.Utc, CambioUtc = e.CambioUtc, Version = e.Version,
				Precio = e.Referencia ?? e.Precio, Compra = compra, Score = e.Score ?? 0, Setup = e.Contexto
			},
			_ => null
		};
	}

	/// <summary>Lleva al bus lo encolado (fuera del cerrojo del motor) y, cada 2 s, el latido.</summary>
	private void PublicarBus()
	{
		if (string.IsNullOrEmpty(_instrumento))
			return;

		List<EventoSuite> lote;
		bool conDatos;

		lock (Suite.Cerrojo)
		{
			lote = _paraBus.ToList();
			_paraBus.Clear();
			conDatos = _enVivo && _ultimoMercado != default && _cronometro.Elapsed < TimeSpan.FromSeconds(60);
		}

		var canal = SuiteEventBus.Para(_instrumento);

		foreach (var e in lote)
			canal.Publicar(e);

		var ahora = DateTime.UtcNow;

		if (ahora - _ultimoLatido >= TimeSpan.FromSeconds(2) || conDatos != _latidoConDatos)
		{
			(_ultimoLatido, _latidoConDatos) = (ahora, conDatos);

			foreach (var f in Fuentes)
				canal.Latido(f, conDatos, ahora);
		}
	}

	/// <summary>"Imbalance", "Iceberg", "Stops", "Sweeps" o "Master": los eventos que este indicador muestra, suena y graba.</summary>
	public string Indicador { get; }

	public ConfiguracionSuite Cfg { get; } = new();

	public ParametrosGenerales General { get; } = new();

	public MotorSuite Suite { get; }

	public Action<string, string>? Alerta { get; set; }

	public Action? Redibujar { get; set; }

	public Action<string, Exception?>? Error { get; set; }

	/// <summary>Solo para las pruebas: carpeta de los CSV de todas las carcasas (null = %APPDATA%\ATAS\OrderFlowSuite).</summary>
	public static string? CarpetaForzada { get; set; }

	/// <summary>Carpeta de los CSV.</summary>
	public string CarpetaGrabacion { get; set; } = CarpetaForzada ?? GrabadorEventos.CarpetaPorDefecto;

	/// <summary>Línea de estado (errores) que se muestra en el panel.</summary>
	public string Estado { get; private set; } = "";

	public bool EnVivo => _enVivo;

	/// <summary>Familia del instrumento (Auto: por el símbolo de ATAS, como Sessions Sweeps Pro; sin reconocer: NQ).</summary>
	public PerfilInstrumento Raiz { get; private set; } = PerfilInstrumento.NQ;

	public bool Mnq => Raiz == PerfilInstrumento.MNQ;

	private PerfilInstrumento RaizDe(string simbolo)
	{
		if (General.InstrumentProfile != PerfilInstrumento.Auto)
			return General.InstrumentProfile;

		return SessionSweeps.Nucleo.Instrumentos.Detectar(simbolo).Raiz switch
		{
			SessionSweeps.Nucleo.RaizInstrumento.MNQ => PerfilInstrumento.MNQ,
			SessionSweeps.Nucleo.RaizInstrumento.ES => PerfilInstrumento.ES,
			SessionSweeps.Nucleo.RaizInstrumento.MES => PerfilInstrumento.MES,
			SessionSweeps.Nucleo.RaizInstrumento.YM => PerfilInstrumento.YM,
			SessionSweeps.Nucleo.RaizInstrumento.MYM => PerfilInstrumento.MYM,
			_ => PerfilInstrumento.NQ
		};
	}

	public string Instrumento => _instrumento ?? "";

	/// <summary>Hora de mercado: la del último trade más lo transcurrido desde él (sirve también en Market Replay).</summary>
	public DateTime Ahora => _ultimoMercado == default ? DateTime.UtcNow : _ultimoMercado + _cronometro.Elapsed;

	#endregion

	#region Cálculo

	/// <summary>
	/// OnCalculate: en la vela 0 se reinicia (ATAS recalcula desde 0 al cambiar de instrumento, temporalidad o ajuste);
	/// cada vela anterior a 'bar' ya cerró y se pasa una sola vez; al llegar a la última empieza el modo en vivo.
	/// </summary>
	public void Calcular(int bar, int currentBar, Func<int, VelaFlujo> vela, Func<int, DateTime> tiempo, string? instrumento, decimal tick)
	{
		lock (Suite.Cerrojo)
		{
			if (bar == 0)
				Reiniciar(instrumento, tick);

			while (_tiempos.Count <= bar)
				_tiempos.Add(tiempo(_tiempos.Count));

			_tiempos[bar] = tiempo(bar);

			if (bar <= _ultimaBarra)
				return;

			for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
				Suite.Vela(vela(i));

			_ultimaBarra = bar;

			if (_enVivo || bar < currentBar - 1)
				return;

			// Fin del historial: lo recalculado no suena ni se graba; desde aquí, sí.
			_enVivo = true;
			Suite.Eventos.TomarNovedades();
			Suite.Eventos.EnVivo = true;
			CargarGrabados();
		}

		PublicarBus();
		IniciarReloj();
	}

	private void Reiniciar(string? instrumento, decimal tick)
	{
		var nuevo = instrumento ?? "";

		if (nuevo != _instrumento || tick != _tick)
		{
			Suite.ReiniciarTodo();
			_grabador?.Dispose();
			_grabador = null;
			(_instrumento, _tick) = (nuevo, tick);
		}
		else
		{
			Suite.ReiniciarVelas();
		}

		Raiz = RaizDe(nuevo);
		Suite.SoloTiempoReal = General.RealTimeOnly;
		Suite.Configurar(tick, Raiz);
		_tiempos.Clear();
		_ultimaBarra = -1;
		_enVivo = false;
		Estado = "";
	}

	/// <summary>Cambio de un parámetro que no necesita recalcular las velas (los motores en vivo conservan su estado).</summary>
	public void Aplicar()
	{
		lock (Suite.Cerrojo)
		{
			Raiz = RaizDe(Instrumento);
			Suite.Configurar(_tick > 0 ? _tick : Suite.Tick, Raiz);
		}

		Redibujar?.Invoke();
	}

	/// <summary>LoadRecordedEvents: los CSV de los últimos días de este indicador vuelven a dibujarse (sin sonar).</summary>
	private void CargarGrabados()
	{
		if (!General.LoadRecordedEvents || string.IsNullOrEmpty(_instrumento))
			return;

		try
		{
			_grabador?.Vaciar();
			// Los días se cuentan desde la última vela del gráfico (no desde el reloj del PC): así vale en Market Replay
			// y en fin de semana.
			var referencia = _tiempos.Count > 0 ? _tiempos[^1] : Ahora;
			var leidos = GrabadorEventos.Leer(CarpetaGrabacion, _instrumento, Indicador, referencia, Math.Clamp(General.RecordedDaysToLoad, 1, 60), Error);
			Suite.Eventos.Importar(leidos);
		}
		catch (Exception ex)
		{
			Error?.Invoke($"{Indicador}: no se pudieron cargar los CSV grabados", ex);
		}
	}

	#endregion

	#region Datos en vivo

	/// <summary>Trade en vivo. 'direccion' = TradeDirection.ToString() ("Buy", "Sell", "Between").</summary>
	public void Trade(decimal precio, decimal volumen, DateTime tiempoUtc, string direccion, long? idAgresor, long? idPasivo)
	{
		lock (Suite.Cerrojo)
		{
			if (!_enVivo || precio <= 0)
				return;

			var utc = DateTime.SpecifyKind(tiempoUtc, DateTimeKind.Utc);

			// El reloj de mercado nunca va hacia atrás: un trade con hora anterior usa la hora actual del reloj.
			if (utc >= _ultimoMercado)
			{
				_ultimoMercado = utc;
				_cronometro.Restart();
			}
			else
			{
				utc = Ahora;
			}

			Lado? agresor = direccion == "Buy" ? Lado.Compra : direccion == "Sell" ? Lado.Venta : null;
			Suite.Trade(new TradeFlujo(utc, precio, volumen, agresor, idAgresor is > 0 ? idAgresor : null, idPasivo is > 0 ? idPasivo : null));
		}

		Despachar();
	}

	/// <summary>Cambio del DOM. 'tipo' = MarketDataType.ToString() ("Ask" o "Bid").</summary>
	public void Dom(decimal precio, decimal volumen, string tipo)
	{
		if (tipo != "Ask" && tipo != "Bid")
			return;

		lock (Suite.Cerrojo)
		{
			if (_enVivo && precio > 0)
				Suite.Dom(new CambioDom(Ahora, precio, volumen, tipo == "Ask"));
		}
	}

	/// <summary>Mensaje MBO. 'tipo' = MarketByOrderUpdateTypes.ToString(); 'lado' = MarketDataType.ToString().</summary>
	public void Mbo(string tipo, string lado, decimal precio, decimal volumen, long id, long prioridad)
	{
		if (lado != "Ask" && lado != "Bid")
			return;

		var t = tipo switch
		{
			"New" => TipoMbo.Nueva,
			"Change" => TipoMbo.Cambio,
			"Delete" => TipoMbo.Borrada,
			"Snapshot" => TipoMbo.Snapshot,
			_ => (TipoMbo?)null
		};

		if (t is null)
			return;

		lock (Suite.Cerrojo)
		{
			if (_enVivo)
				Suite.Mbo(new MensajeMbo(Ahora, t.Value, id, precio, volumen, lado == "Ask", prioridad));
		}
	}

	/// <summary>Reloj de 1 s: ráfagas vencidas, TRAMPA/LIQUIDEZ por tiempo, AGUANTANDO sin trades y el Master.</summary>
	private void IniciarReloj()
	{
		if (_reloj is not null)
			return;

		_reloj = new Timer(_ =>
		{
			try
			{
				lock (Suite.Cerrojo)
				{
					if (_enVivo)
						Suite.Tiempo(Ahora);
				}

				Despachar();
			}
			catch (Exception ex)
			{
				Error?.Invoke($"{Indicador}: error en el reloj", ex);
			}
		}, null, 1000, 1000);
	}

	/// <summary>Novedades de este indicador: alerta (una por evento y estado) y fila del CSV. Después, redibujar.</summary>
	private void Despachar()
	{
		PublicarBus();
		List<EventoFlujo> novedades;
		bool cambios;

		lock (Suite.Cerrojo)
		{
			novedades = Suite.Eventos.TomarNovedades().Where(n => n.Indicador == Indicador).ToList();
			cambios = Suite.Eventos.Publicaciones != _publicacionesDibujadas;
			_publicacionesDibujadas = Suite.Eventos.Publicaciones;

			if (General.EnableRecording && novedades.Count > 0 && !string.IsNullOrEmpty(_instrumento))
			{
				_grabador ??= new GrabadorEventos(CarpetaGrabacion, _instrumento) { Error = Error };

				foreach (var n in novedades)
					_grabador.Encolar(n);
			}
		}

		if (General.EnableAlerts)
		{
			foreach (var n in novedades)
			{
				bool nueva;

				lock (_alertadas)
				{
					nueva = _alertadas.Add(n.Clave);

					if (_alertadas.Count > 5000)
						_alertadas.Clear();
				}

				if (nueva)
					Alerta?.Invoke(string.IsNullOrWhiteSpace(General.AlertSoundFile) ? "alert1" : General.AlertSoundFile, n.Texto);
			}
		}

		if (cambios)
			Redibujar?.Invoke();
	}

	public void Dispose()
	{
		_reloj?.Dispose();
		_reloj = null;
		_grabador?.Dispose();
		_grabador = null;
	}

	#endregion

	#region Dibujo

	public static readonly Color Verde = Color.FromArgb(255, 38, 166, 154);
	public static readonly Color Rojo = Color.FromArgb(255, 239, 83, 80);
	public static readonly Color Oro = Color.FromArgb(255, 255, 193, 7);
	public static readonly Color Gris = Color.FromArgb(255, 150, 150, 150);
	public static readonly Color Azul = Color.FromArgb(255, 66, 165, 245);
	public static readonly Color Fondo = Color.FromArgb(215, 18, 20, 26);

	public static Color ColorLado(Lado lado) => lado == Lado.Compra ? Verde : Rojo;

	public static Color Transparente(Color c, int alfa) => Color.FromArgb(Math.Clamp(alfa, 0, 255), c.R, c.G, c.B);

	/// <summary>Color de una línea del panel: el del lado; gris si el evento terminó o viene del CSV.</summary>
	public static Color ColorEvento(EventoFlujo e) =>
		e.Grabado || e.Estado is "ROTO" or "CANCELADO" or "ROTA" ? Gris : e.Tipo == "Signal" ? Oro : ColorLado(e.Lado);

	public RenderFont Fuente
	{
		get
		{
			var tamano = Math.Clamp(General.FontSize, 6, 30);

			if (_fuente is null || _tamanoFuente != tamano)
				(_fuente, _tamanoFuente) = (new RenderFont("Arial", tamano), tamano);

			return _fuente;
		}
	}

	public static readonly RenderStringFormat Izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

	public static readonly RenderStringFormat Centro = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

	/// <summary>Barra de una hora (búsqueda binaria en las horas de apertura de las velas). -1 si es anterior al gráfico.</summary>
	public int Barra(DateTime utc)
	{
		lock (Suite.Cerrojo)
			return BusquedaTiempo.UltimaHasta(i => _tiempos[i], _tiempos.Count, utc);
	}

	public int Barras
	{
		get
		{
			lock (Suite.Cerrojo)
				return _tiempos.Count;
		}
	}

	/// <summary>Las últimas líneas del panel (eventos recientes de este indicador, el más nuevo primero).</summary>
	public List<(string Texto, Color Color)> LineasRecientes()
	{
		lock (Suite.Cerrojo)
		{
			return Suite.Eventos.Recientes(Indicador, Ahora, General.EventLifetimeMin, Math.Clamp(General.InfoPanelMaxLines, 1, 20))
				.Select(e => (e.Grabado ? "(CSV) " + e.Texto : e.Texto, ColorEvento(e)))
				.ToList();
		}
	}

	/// <summary>Panel de texto en la esquina elegida (con desplazamiento): título, líneas y la línea de estado.</summary>
	public void DibujarPanel(RenderContext context, Rectangle area, string titulo, IReadOnlyList<(string Texto, Color Color)> lineas)
	{
		if (!General.ShowInfoPanel)
			return;

		var todas = new List<(string Texto, Color Color)> { (titulo, Color.Gainsboro) };
		todas.AddRange(lineas.Count > 0 ? lineas : new[] { ("(sin eventos recientes)", Gris) });

		if (Estado.Length > 0)
			todas.Add((Estado, Color.Orange));

		var fuente = Fuente;
		var alto = Math.Clamp(General.FontSize, 6, 30) + 6;
		var ancho = todas.Max(l => context.MeasureString(l.Texto, fuente).Width) + 14;
		var altoCaja = alto * todas.Count + 6;
		var derecha = General.InfoPanelPosition is EsquinaPanel.TopRight or EsquinaPanel.BottomRight;
		var abajo = General.InfoPanelPosition is EsquinaPanel.BottomRight or EsquinaPanel.BottomLeft;

		// A la derecha deja sitio a la escala de precios; el desplazamiento separa los paneles de varios indicadores.
		var x = derecha ? area.Right - ancho - 70 - General.InfoPanelOffsetX : area.Left + 10 + General.InfoPanelOffsetX;
		var y = abajo ? area.Bottom - altoCaja - 30 - General.InfoPanelOffsetY : area.Top + 10 + General.InfoPanelOffsetY;
		var caja = new Rectangle(Math.Max(area.Left, x), Math.Max(area.Top, y), ancho, altoCaja);

		context.FillRectangle(Fondo, caja);

		for (var i = 0; i < todas.Count; i++)
			context.DrawString(todas[i].Texto, fuente, todas[i].Color, new Rectangle(caja.X + 6, caja.Y + 3 + i * alto, ancho - 8, alto), Izquierda);
	}

	/// <summary>Etiqueta pequeña con fondo (marcadores).</summary>
	public void Etiqueta(RenderContext context, string texto, int x, int y, Color color)
	{
		var fuente = Fuente;
		var tamano = context.MeasureString(texto, fuente);
		var caja = new Rectangle(x, y - (tamano.Height + 4) / 2, tamano.Width + 6, tamano.Height + 4);
		context.FillRectangle(Transparente(Fondo, 170), caja);
		context.DrawString(texto, fuente, color, caja, Centro);
	}

	/// <summary>Línea discontinua (dibujada a trozos: no depende de estilos de pluma).</summary>
	public static void Discontinua(RenderContext context, RenderPen pluma, int x1, int x2, int y)
	{
		for (var x = Math.Min(x1, x2); x < Math.Max(x1, x2); x += 10)
			context.DrawLine(pluma, x, y, Math.Min(x + 6, Math.Max(x1, x2)), y);
	}

	/// <summary>Triángulo (flecha) hacia arriba o hacia abajo con la punta en (x, y).</summary>
	public static void Flecha(RenderContext context, int x, int y, bool arriba, Color color, int tamano = 8)
	{
		var puntos = arriba
			? new[] { new Point(x, y), new Point(x - tamano, y + tamano * 2), new Point(x + tamano, y + tamano * 2) }
			: new[] { new Point(x, y), new Point(x - tamano, y - tamano * 2), new Point(x + tamano, y - tamano * 2) };

		context.FillPolygon(color, puntos);
	}

	#endregion
}
