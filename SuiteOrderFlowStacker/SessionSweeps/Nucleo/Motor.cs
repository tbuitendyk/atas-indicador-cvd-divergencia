namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Aviso numerado (alerta y/o correo). El indicador solo los emite con datos en vivo.</summary>
public sealed class Aviso
{
	public long Numero;
	public TipoAviso Tipo;
	public DateTime Utc;

	/// <summary>Texto de la alerta de ATAS (ASCII).</summary>
	public string Texto = "";

	/// <summary>Asunto y cuerpo del correo (null = este aviso no lleva correo).</summary>
	public string? Asunto, Cuerpo;

	/// <summary>Clave anti-duplicados (nivel + tipo + barra).</summary>
	public string Clave = "";
}

/// <summary>
/// Motor de Session Sweeps Pro (base copiada del NQ Session Sweeps Pro). Recibe velas cerradas (y, en vivo, cada trade) y calcula, en este orden: sesiones →
/// niveles → barridos (Pine) → ventanas de Order Flow y clasificación → FVG/IFVG y MSS → contexto H1 → perfil/LVN →
/// Modelo A y Modelo B. Es seguro entre hilos: el indicador calcula, recibe trades y dibuja desde hilos distintos.
/// </summary>
public sealed partial class MotorSweeps
{
	private readonly object _sync = new();
	private readonly Atr _atr21 = new(21), _atr144 = new(144);
	private readonly Agregador _a60 = new(60);
	private readonly Dictionary<int, (DateTime Vela, RupturaEnVivo R)> _rupturas = new();
	private readonly HashSet<string> _claves = new();
	private int _siguienteZona = 1;
	private long _ultimoAviso;
	private DateTime _minuto;
	private decimal _volMinuto, _deltaMinuto;

	public MotorSweeps(ConfiguracionSweeps cfg)
	{
		Cfg = cfg;
		Sesiones = new MotorSesiones(cfg);
		Niveles = new GestorNiveles(cfg);
	}

	public ConfiguracionSweeps Cfg { get; }

	public List<Vela> Velas { get; } = new();

	public MotorSesiones Sesiones { get; }

	public GestorNiveles Niveles { get; }

	public List<ZonaBarrido> ZonasBarrido { get; } = new();

	public DetectorFvg Fvg { get; } = new();

	public DetectorMss Mss { get; } = new();

	public CalculoH1 H1 { get; } = new();

	/// <summary>Objetivos extra de la proyección (los LVN del Thermal, desde el LvnRepository).</summary>
	public Func<IEnumerable<(string Nombre, decimal Precio)>>? ObjetivosExtra { get; set; }

	public List<Aviso> Avisos { get; } = new();

	public int Revision { get; private set; }

	/// <summary>Símbolo corto para los textos ("NQ", "MNQ").</summary>
	public string Simbolo { get; set; } = "NQ";

	/// <summary>Zona horaria de las horas de los correos (EmailTimeZone).</summary>
	public TimeZoneInfo ZonaCorreo { get; set; } = Zonas.De(ZonaHoraria.NuevaYork);

	public decimal? Atr21 => _atr21.Valor;

	public long UltimoAviso
	{
		get
		{
			lock (_sync)
				return _ultimoAviso;
		}
	}

	public T Leer<T>(Func<MotorSweeps, T> lectura)
	{
		lock (_sync)
			return lectura(this);
	}

	public List<Aviso> AvisosDespuesDe(long numero)
	{
		lock (_sync)
			return Avisos.Where(a => a.Numero > numero).ToList();
	}

	public string Precio(decimal p)
	{
		var texto = Cfg.TamanoTick.ToString(CultureInfo.InvariantCulture).TrimEnd('0');
		var decimales = texto.Contains('.') ? texto.Length - texto.IndexOf('.') - 1 : 0;
		return p.ToString("F" + decimales, CultureInfo.InvariantCulture);
	}

	private static string Hora(DateTime utc, TimeZoneInfo zona, string formato) =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona).ToString(formato, CultureInfo.InvariantCulture);

	private void Avisar(TipoAviso tipo, DateTime utc, string clave, string texto, string? asunto = null, string? cuerpo = null)
	{
		if (!_claves.Add(clave))
			return;

		Avisos.Add(new Aviso { Numero = ++_ultimoAviso, Tipo = tipo, Utc = utc, Clave = clave, Texto = texto, Asunto = asunto, Cuerpo = cuerpo });

		if (Avisos.Count > 300)
			Avisos.RemoveRange(0, Avisos.Count - 300);
	}

	// =================================================================================================================
	// Velas cerradas
	// =================================================================================================================

	public void Procesar(VelaEntrada e)
	{
		lock (_sync)
			ProcesarInterno(e);
	}

	private void ProcesarInterno(VelaEntrada e)
	{
		if (Velas.Count > 0 && e.InicioUtc <= Velas[^1].InicioUtc)
			return;

		var v = Construir(e);
		Velas.Add(v);
		ProcesarCvd(e);

		// El footprint solo hace falta en las velas recientes (ventanas, absorción, Modelo B).
		if (Velas.Count > 400)
			Velas[^401].Niveles = null;

		_atr21.Agregar(v);
		_atr144.Agregar(v);

		// 1. Sesiones y sus niveles.
		var (terminadas, iniciadas) = Sesiones.Procesar(v);

		foreach (var s in iniciadas)
			Niveles.AlEmpezarSesion(s.Id, v.Indice);

		foreach (var s in terminadas)
			Niveles.AlTerminarSesion(s, v);

		// 2. Días, semanas y máximos/mínimos iguales.
		Niveles.Procesar(v);

		if (Cfg.NivelesIguales)
			Niveles.Pivotes(Velas);

		// v2.0: los trades del minuto (o su reconstrucción APROX) entran ahora, con los niveles al día y antes del Pine,
		// igual que en vivo (donde llegan durante el minuto).
		AlimentarBarras(v);

		// 4. Contexto H1.
		var antes = _a60.Velas.Count;
		_a60.Agregar(v);

		if (_a60.Velas.Count > antes)
			H1.Agregar(_a60.Velas, Cfg.H1PivotLen);

		// 5. Barridos (Pine), 6. ventanas de Order Flow y clasificación.
		ProcesarBarridos(v);
		ProcesarVentanas(v);

		// 7. FVG / IFVG y MSS.
		ProcesarFvg(v);
		Mss.Procesar(Velas, Cfg.MSSLength, Cfg.Mss, Cfg.MaxLines);

		// 8. Session Sweeps v2.0: barras de volumen, clasificador, FVG/BFVG, modelos (MotorV2).
		ProcesarV2(v);

		Niveles.RomperNoVigilados(v);

		// v4: líneas de sesión (ruptura que no vio el tick, BOS, líneas nuevas).
		ProcesarLineas(v, terminadas);

		// Las rupturas en vivo de la vela que acaba de cerrar ya quedaron en sus zonas.
		foreach (var id in _rupturas.Where(r => r.Value.Vela <= v.InicioUtc).Select(r => r.Key).ToList())
			_rupturas.Remove(id);

		Revision++;
	}

	private Vela Construir(VelaEntrada e)
	{
		var v = new Vela
		{
			Indice = Velas.Count,
			InicioUtc = e.InicioUtc,
			FinUtc = e.InicioUtc.AddMinutes(Math.Max(1, Cfg.MinutosGrafico)),
			Open = e.Open,
			High = e.High,
			Low = e.Low,
			Close = e.Close,
			Volume = e.Volume,
			Delta = e.Delta,
			Ask = e.Ask,
			Bid = e.Bid
		};

		if (e.Niveles.Count > 0)
		{
			v.Niveles = new SortedDictionary<long, (double Bid, double Ask)>();

			foreach (var n in e.Niveles)
			{
				var t = (long)Math.Round(n.Precio / Cfg.TamanoTick);
				var previo = v.Niveles.TryGetValue(t, out var p) ? p : default;
				v.Niveles[t] = (previo.Bid + (double)n.Bid, previo.Ask + (double)n.Ask);
			}
		}

		return v;
	}

	/// <summary>¿Se detectan barridos a esta hora? NY = premercado, NY AM y NY PM (sus horarios); Custom = ET.</summary>
	public bool EnVentana(DateTime utc)
	{
		if (Cfg.PineCompatibilityMode || Cfg.SweepDetectionWindow == VentanaBarridos.Todas)
			return true;

		if (Cfg.SweepDetectionWindow == VentanaBarridos.Custom)
		{
			var h = Zonas.AEt(utc).TimeOfDay;
			return Cfg.VentanaInicio <= Cfg.VentanaFin ? h >= Cfg.VentanaInicio && h < Cfg.VentanaFin : h >= Cfg.VentanaInicio || h < Cfg.VentanaFin;
		}

		foreach (var id in new[] { ConfiguracionSweeps.Premercado, ConfiguracionSweeps.NyAm, ConfiguracionSweeps.NyPm })
		{
			var s = Cfg.Sesion(id);
			var (inicio, fin, zona, _) = Cfg.Horario(s);

			if (inicio == fin)
				continue;

			var h = Zonas.ALocal(utc, zona).AddHours(Cfg.AjusteManualHoras).TimeOfDay;

			if (inicio < fin ? h >= inicio && h < fin : h >= inicio || h < fin)
				return true;
		}

		return false;
	}

	// =================================================================================================================
	// Barridos: traducción fiel de f_pSWP (bloques swpBZ / swpSZ)
	// =================================================================================================================

	private void ProcesarBarridos(Vela v)
	{
		if (_atr21.Valor is not { } atr)
			return;

		var enVentana = EnVentana(v.InicioUtc);

		foreach (var n in Niveles.Niveles.ToList())
		{
			var zonaActiva = n.Zona is { Activa: true };

			if (!zonaActiva && (!n.Activo || !Niveles.Vigilado(n)))
				continue;

			var compra = n.Lado == Lado.Buyside;

			if (!(compra ? Cfg.BuysideZones : Cfg.SellsideZones))
				continue;

			var ma = (decimal)(compra ? Cfg.BuysideMargin : Cfg.SellsideMargin) * atr;
			var l = n.Precio;

			if (!n.Marcado)
			{
				var barrido = compra ? v.High > l && v.Open < l : v.Low < l && v.Open > l;

				if (n.Activo && enVentana && barrido)
					NuevaZona(n, v, ma);

				continue;
			}

			if (n.Zona is not { Activa: true } z)
				continue;

			// Seguimiento para el stop, la clasificación y los cierres de vuelta.
			z.ExtremoPrecio = compra ? Math.Max(z.ExtremoPrecio, v.High) : Math.Min(z.ExtremoPrecio, v.Low);
			SeguirCierres(z, v);

			if (v.Low > l - ma && v.High < l + ma)
			{
				if (compra)
				{
					z.Extremo = Math.Max(v.High, z.Extremo);

					if (v.High > l)
						z.Derecha = v.Indice + 1;
				}
				else
				{
					z.Extremo = Math.Min(v.Low, z.Extremo);

					if (v.Low < l)
						z.Derecha = v.Indice + 1;
				}

				continue;
			}

			// Fuera del margen: cierre muy más allá = FALSO (aceptación); o pasadas SweepMarginLength velas, se cierra.
			if (compra ? v.Close > l + ma * 1.5m : v.Close < l - ma * 1.5m)
			{
				z.FalsoPine = true;
				z.Derecha = compra ? v.Indice : v.Indice + 1;
				CerrarZona(z, v);
			}
			else if (v.Indice - z.Derecha >= Cfg.SweepMarginLength)
			{
				if (compra ? v.High > l + ma : v.Low < l - ma)
				{
					z.FalsoPine = true;
					z.Derecha = v.Indice + 1;
				}

				CerrarZona(z, v);
			}

			// Pine: fuera del margen, si no es "una vez por sesión", el nivel vuelve a poder detectar barridos.
			if (Cfg.PineCompatibilityMode && !Cfg.DetectOncePerSession)
				n.Marcado = false;
		}

		while (ZonasBarrido.Count > Cfg.MaxSweepBoxes)
		{
			var vieja = ZonasBarrido.FindIndex(z => !z.Activa);
			ZonasBarrido.RemoveAt(vieja < 0 ? 0 : vieja);
		}
	}

	private void NuevaZona(NivelLiquidez n, Vela v, decimal ma)
	{
		var compra = n.Lado == Lado.Buyside;

		// Un barrido nuevo de un nivel deja congelada su zona anterior (como la caja que el Pine reemplaza).
		if (n.Zona is { Activa: true } previa)
			previa.Activa = false;

		var vivo = _rupturas.TryGetValue(n.Id, out var r) && r.Vela == v.InicioUtc ? r.R : null;
		var t0 = vivo?.T0Utc ?? v.InicioUtc;
		var z = new ZonaBarrido
		{
			Id = _siguienteZona++,
			Nivel = n,
			Lado = n.Lado,
			L = n.Precio,
			Izquierda = v.Indice - 1,
			Derecha = v.Indice + 1,
			Extremo = compra ? Math.Min(n.Precio + ma, v.High) : Math.Max(n.Precio - ma, v.Low),
			ExtremoPrecio = compra ? v.High : v.Low,
			BarraBarrido = v.Indice,
			T0Utc = t0,
			MinutoT0Utc = new DateTime(t0.Ticks - t0.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc),
			Vivo = vivo
		};

		n.Marcado = true;
		n.Zona = z;
		ZonasBarrido.Add(z);
		SeguirCierres(z, v);

		if (n.Sesion is { } s)
		{
			if (compra)
				s.AltoBarridoEn ??= v.Indice;
			else
				s.BajoBarridoEn ??= v.Indice;
		}

		z.Proyeccion = Proyectar(z, v.Close);

		// Si no se vio en vivo (histórico, o sin trades), el aviso de ruptura sale al cierre de la vela.
		if (vivo is null)
			AvisoRuptura(n, t0, v.High, v.Low, v.Close, v.Volume, v.Delta, 0, 0, z.Proyeccion, v.Indice);
	}

	private void SeguirCierres(ZonaBarrido z, Vela v)
	{
		var mas = z.Lado.Signo() * (v.Close - z.L) > 0;

		if (!mas)
			z.CierreDeVueltaEn ??= v.Indice;

		// Dos cierres seguidos más allá del nivel (en las velas siguientes al barrido).
		if (mas && v.Indice > z.BarraBarrido && Velas.Count >= 2 && z.Lado.Signo() * (Velas[^2].Close - z.L) > 0 && Velas[^2].Indice >= z.BarraBarrido)
			z.CierresSeguidosMasAlla = true;
	}

	private void CerrarZona(ZonaBarrido z, Vela v)
	{
		z.Activa = false;

		if (!Cfg.PineCompatibilityMode)
		{
			z.Nivel.Estado = z.FalsoPine ? EstadoNivel.Roto : EstadoNivel.Barrido;
			z.Nivel.FinBar = v.Indice;
		}

		Reclasificar(z, v, null);
	}

	// =================================================================================================================
	// Order Flow: ventanas W1 / W3 / W5 y clasificación
	// =================================================================================================================

	private void ProcesarVentanas(Vela v)
	{
		if (!Cfg.OrderFlowValidation)
			return;

		foreach (var z in ZonasBarrido.Where(z => z.W5 is null && v.Indice - z.BarraBarrido <= 10).ToList())
		{
			foreach (var minutos in new[] { 1, 3, 5 })
			{
				var fin = z.MinutoT0Utc.AddMinutes(minutos);

				if (v.FinUtc < fin || (minutos == 1 ? z.W1 : minutos == 3 ? z.W3 : z.W5) is not null)
					continue;

				var ventana = Velas.Where(x => x.Indice >= z.BarraBarrido && x.InicioUtc < fin).ToList();
				var barrido = Velas[z.BarraBarrido];
				var m = OrderFlow.Calcular(z, minutos, ventana, barrido, Huella.Calcular(barrido, Cfg.TamanoTick), VolPorTick(z.BarraBarrido), Cfg, fin);

				switch (minutos)
				{
					case 1:
						z.W1 = m;
						Velocidad(z, m);
						break;
					case 3:
						z.W3 = m;
						break;
					default:
						z.W5 = m;
						break;
				}

				Reclasificar(z, v, minutos);
			}
		}
	}

	/// <summary>Volumen medio por tick de las 20 velas anteriores a la del barrido.</summary>
	private decimal VolPorTick(int barra)
	{
		var previas = Velas.Skip(Math.Max(0, barra - 20)).Take(Math.Min(20, barra)).ToList();
		return previas.Count == 0 ? 0 : previas.Average(x => x.Volume / (x.Rango / Cfg.TamanoTick + 1));
	}

	/// <summary>Velocidad pico (en vivo, por tramos de 5 s) o aproximada (agresión de W1 / 60 s); ratio = / media de 20 min.</summary>
	private void Velocidad(ZonaBarrido z, MetricasVentana w1)
	{
		var desde = z.MinutoT0Utc.AddMinutes(-20);
		var previas = Velas.Where(x => x.InicioUtc >= desde && x.InicioUtc < z.MinutoT0Utc).ToList();
		var agresion = previas.Sum(x => z.Lado == Lado.Buyside ? x.Ask : x.Bid);
		var media = previas.Count == 0 ? 0 : agresion / (decimal)(previas.Count * Math.Max(1, Cfg.MinutosGrafico) * 60);

		if (z.Vivo is { } vivo && vivo.AgresionPorTramo.Count > 0)
		{
			z.VelocidadPico = vivo.VelocidadPico;
		}
		else
		{
			z.VelocidadPico = (z.Lado == Lado.Buyside ? w1.AskBeyond : w1.BidBeyond) / 60m;
			z.VelocidadAproximada = true;
		}

		z.RatioVelocidad = media > 0 ? z.VelocidadPico / media : 0;
	}

	/// <summary>
	/// W1 / W3 / W5 y la clasificación del NQ anterior quedan SOLO como datos informativos (P9): la decisión la toma el
	/// clasificador nuevo (Clasificador.cs) sobre la vela compuesta de barras de volumen.
	/// </summary>
	private void Reclasificar(ZonaBarrido z, Vela v, int? minutos)
	{
		if (Cfg.OrderFlowValidation)
			z.Clasificacion = OrderFlow.Clasificar(z, Cfg);
	}

	// =================================================================================================================
	// FVG / IFVG
	// =================================================================================================================

	private void ProcesarFvg(Vela v)
	{
		var (nuevos, invertidos) = Fvg.Procesar(Velas, _atr144.Valor, Cfg.FVGWidthFilter, Cfg.MaxSweepBoxes);

		// FVG de desplazamiento en contra del barrido (bajista tras uno buyside) en las PostSweepFVGWindow velas.
		foreach (var f in nuevos)
		{
			f.PostSweep = ZonasBarrido.Any(z => v.Indice - z.BarraBarrido <= Cfg.PostSweepFVGWindow && v.Indice > z.BarraBarrido
				&& f.Alcista == (z.Lado == Lado.Sellside));
		}

		foreach (var f in invertidos)
		{
			var lado = f.AlcistaAhora ? "ALCISTA" : "BAJISTA";
			Avisar(TipoAviso.Ifvg, v.FinUtc, $"ifvg|{f.Id}", $"IFVG {lado} {Precio(f.Abajo)}-{Precio(f.Arriba)}");
		}
	}

	// =================================================================================================================
	// Proyección
	// =================================================================================================================

	/// <summary>Objetivos al otro lado: niveles opuestos sin barrer, POC/VA del día anterior, LVN y bordes de FVG sin mitigar.</summary>
	private Proyeccion Proyectar(ZonaBarrido z, decimal entrada)
	{
		var bajista = z.Lado == Lado.Buyside;
		var candidatos = new List<(string, decimal)>();

		candidatos.AddRange(Niveles.Activos.Where(n => n.Lado != z.Lado && Niveles.Vigilado(n)).Select(n => (n.Nombre, n.Precio)));

		if (ObjetivosExtra?.Invoke() is { } extra)
			candidatos.AddRange(extra);

		candidatos.AddRange(Fvg.Fvgs.Where(f => !f.Mitigado).Select(f => ("FVG", bajista ? f.Abajo : f.Arriba)));

		decimal? adr = Niveles.Dias.Count == 0 ? null : Niveles.Dias.TakeLast(Math.Max(1, Cfg.AdrDias)).Average(d => d.Alto - d.Bajo);
		return Proyeccion.Calcular(z.Lado, entrada, z.ExtremoPrecio, candidatos, Cfg, adr, Niveles.DiaActual);
	}

	// =================================================================================================================
	// Avisos y correos de barrido
	// =================================================================================================================

	private void AvisoRuptura(NivelLiquidez n, DateTime t0, decimal alto, decimal bajo, decimal precio, decimal volMinuto, decimal deltaMinuto,
		decimal beyond, decimal velocidad, Proyeccion? p, int barra)
	{
		var compra = n.Lado == Lado.Buyside;
		var nombre = $"{n.Nombre} {Precio(n.Precio)}";
		var stop = compra ? alto + Cfg.Ticks(Cfg.StopBufferTicks) : bajo - Cfg.Ticks(Cfg.StopBufferTicks);

		var cuerpo = string.Join("\n", new[]
		{
			$"Barrido de liquidez a las {Hora(t0, ZonaCorreo, "HH:mm:ss")}",
			$"Toma liquidez del {(compra ? "MAXIMO" : "MINIMO")} de {n.Origen}",
			$"Contexto H1: {H1.Valor.Texto()}",
			$"Liquida {(compra ? "maximo" : "minimo")} con precio {Precio(n.Precio)}",
			$"Contratos negociados (1m): {volMinuto:0} | Mas alla del nivel: {beyond:0}",
			$"Delta 1m: {(deltaMinuto >= 0 ? "+" : "")}{deltaMinuto:0}",
			velocidad > 0 ? $"Velocidad de {(compra ? "compra" : "venta")}: {velocidad:0} contratos/s" : $"Velocidad de {(compra ? "compra" : "venta")}: -",
			"Absorcion preliminar: se calcula al cierre del minuto (correo de actualizacion)",
			$"Proyeccion: {(p is null || p.Objetivos.Count == 0 ? "sin objetivos" : p.Texto(Precio))}",
			$"Stop teorico: {Precio(stop)}"
		});

		Avisar(TipoAviso.Ruptura, t0, $"ruptura|{n.Id}|{barra}",
			$"{Simbolo} rompe {nombre} a las {Hora(t0, Zonas.De(ZonaHoraria.NuevaYork), "HH:mm:ss")} ET",
			$"{Simbolo} BARRIDO {(compra ? "BUYSIDE" : "SELLSIDE")} - {nombre} - {Hora(t0, ZonaCorreo, "HH:mm")}", cuerpo);
	}

	// =================================================================================================================
	// En vivo: cada trade
	// =================================================================================================================

	/// <summary>
	/// Un trade en vivo: detecta la ruptura en el tick exacto (t0), mide la velocidad por tramos de 5 s y lanza las
	/// cuentas atrás antes del cierre de la vela gatillo de 1 y de 5 minutos. 'compra' = agresor comprador (null si no se sabe).
	/// </summary>
	public void Trade(DateTime utc, decimal precio, decimal volumen, bool? compra, decimal aperturaVela, DateTime inicioVela)
	{
		lock (_sync)
		{
			if (Velas.Count == 0)
				return;

			TradeBarras(utc, precio, volumen, compra);
			var minuto = new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

			if (minuto != _minuto)
				(_minuto, _volMinuto, _deltaMinuto) = (minuto, 0, 0);

			// Ruptura en el tick.
			if (EnVentana(utc))
			{
				foreach (var n in Niveles.Niveles.Where(n => n.Activo && !n.Marcado && Niveles.Vigilado(n) && !_rupturas.ContainsKey(n.Id)))
				{
					var buy = n.Lado == Lado.Buyside;

					if (!(buy ? Cfg.BuysideZones : Cfg.SellsideZones) || !(buy ? precio > n.Precio && aperturaVela < n.Precio : precio < n.Precio && aperturaVela > n.Precio))
						continue;

					var r = new RupturaEnVivo { T0Utc = utc, Precio = precio, DeltaMinutoAntes = _deltaMinuto };
					_rupturas[n.Id] = (inicioVela, r);
					var z = new ZonaBarrido { Nivel = n, Lado = n.Lado, L = n.Precio, ExtremoPrecio = precio };
					AvisoRuptura(n, utc, precio, precio, precio, _volMinuto + volumen, _deltaMinuto, volumen, 0, Proyectar(z, precio), Velas.Count);
				}
			}

			_volMinuto += volumen;
			_deltaMinuto += compra == true ? volumen : compra == false ? -volumen : 0;

			// Velocidad: agresión en el sentido de la ruptura más allá del nivel, por tramos de 5 s, durante 5 minutos.
			foreach (var (n, r) in RupturasRecientes(utc))
			{
				var buy = n.Lado == Lado.Buyside;

				if (buy ? precio > n.Precio && compra == true : precio < n.Precio && compra == false)
				{
					var tramo = (int)Math.Floor((utc - r.T0Utc).TotalSeconds / 5);
					r.AgresionPorTramo[tramo] = r.AgresionPorTramo.GetValueOrDefault(tramo) + volumen;
				}

				// Cuentas atrás: el precio está de vuelta del lado correcto del nivel roto.
				if (buy ? precio >= n.Precio : precio <= n.Precio)
					continue;

				var lado = buy ? $"bajo el {n.Lado.Extremo()} roto" : $"sobre el {n.Lado.Extremo()} roto";

				foreach (var minutos in new[] { 1, 5 })
				{
					var periodo = TimeSpan.FromMinutes(minutos).Ticks;
					var inicioT0 = new DateTime(r.T0Utc.Ticks - r.T0Utc.Ticks % periodo, DateTimeKind.Utc);
					var cierre = inicioT0.AddMinutes(minutos);
					var faltan = (cierre - utc).TotalSeconds;

					if (faltan > 0 && faltan <= Cfg.TriggerCountdownSec)
					{
						Avisar(minutos == 1 ? TipoAviso.CuentaAtras1m : TipoAviso.CuentaAtras5m, utc, $"cuenta{minutos}|{n.Id}|{inicioT0:O}",
							$"Vela gatillo {minutos}m cierra en {Math.Ceiling(faltan):0} s - precio {lado}");
					}
				}
			}
		}
	}

	private IEnumerable<(NivelLiquidez Nivel, RupturaEnVivo R)> RupturasRecientes(DateTime utc)
	{
		foreach (var n in Niveles.Niveles)
		{
			var r = _rupturas.TryGetValue(n.Id, out var x) ? x.R : n.Zona is { Vivo: { } vivo } && n.Zona.Clasificacion != Clasificacion.Falso ? vivo : null;

			if (r is not null && utc - r.T0Utc < TimeSpan.FromMinutes(5))
				yield return (n, r);
		}
	}
}
