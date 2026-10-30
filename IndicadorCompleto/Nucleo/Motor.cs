namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Motor compartido de IndicadorCompleto. Recibe solo velas de 1M cerradas del gráfico maestro y calcula todo lo
/// demás. Orden por vela (todo lo conocido en el instante T se aplica antes de evaluar oportunidades en T):
/// CVD → mapa LVN (apertura NY) → perfiles y reacciones → sesiones → cierres de 1H, 15M y 5M (contexto,
/// pivotes) → lecturas, EMA y pivotes de 1M → FVG → barridos y modelos → gestión simulada.
/// Es seguro entre hilos: el indicador calcula y la ventana y los visores leen.
/// </summary>
public sealed partial class MotorCompleto
{
	private readonly object _sync = new();
	private DateTime? _ultimoMapaNy;

	public MotorCompleto(ConfiguracionCompleto cfg)
	{
		Cfg = cfg.Copiar();
		Calendario = new Calendario(Cfg.CierresAnticipadosNy);
		A5 = new Agregador(Marco.M5, true, Cfg);
		A15 = new Agregador(Marco.M15, false, Cfg);
		A60 = new Agregador(Marco.H1, false, Cfg);
		Estructura = new Estructura1H(Cfg);
		Liquidez = new GestorLiquidez(Cfg, Calendario);
		Lvn = new MotorLvn(Cfg);
		FvgDetector = new DetectorFvg(Cfg);
		Ema1M = new Ema(Cfg.PeriodoEma);
		Ema5M = new Ema(Cfg.PeriodoEma);
		Cvd = new CalculadoraCvd(Cfg.ReinicioCvdNy);
		Divergencias = new DetectorDivergencias();
		Simulacion = new GestorSimulacion(Cfg);
	}

	internal ConfiguracionCompleto Cfg { get; }

	internal Calendario Calendario { get; }

	internal List<Vela> Velas1M { get; } = new();

	internal Agregador A5 { get; }

	internal Agregador A15 { get; }

	internal Agregador A60 { get; }

	internal Estructura1H Estructura { get; }

	internal GestorLiquidez Liquidez { get; }

	internal MotorLvn Lvn { get; }

	internal DetectorFvg FvgDetector { get; }

	internal Ema Ema1M { get; }

	internal Ema Ema5M { get; }

	internal CalculadoraCvd Cvd { get; }

	internal DetectorDivergencias Divergencias { get; }

	internal GestorSimulacion Simulacion { get; }

	internal List<Pivote> Pivotes1M { get; } = new();

	internal List<Pivote> Pivotes5M { get; } = new();

	internal List<EventoBarrido> Barridos { get; } = new();

	internal List<Oportunidad> Oportunidades { get; } = new();

	internal List<Senal> Senales { get; } = new();

	internal List<SetupBloqueado> SetupsBloqueados { get; } = new();

	internal Lectura Lectura1M, Lectura5M, Lectura15M, Lectura1H;

	internal int Huecos, VelasSinHuella, FueraDeOrden;

	/// <summary>Aumenta con cada vela procesada: la ventana y los visores detectan así los cambios.</summary>
	public int Revision { get; private set; }

	public DateTime? UltimaVelaUtc { get; private set; }

	/// <summary>Último momento en que el maestro recibió datos de ATAS (aunque la vela siga abierta).</summary>
	public DateTime? UltimoDatoUtc { get; set; }

	public string Descripcion { get; set; } = "";

	public int VelasProcesadas
	{
		get
		{
			lock (_sync)
				return Velas1M.Count;
		}
	}

	/// <summary>Lectura coherente del estado: se ejecuta bajo el mismo cerrojo que el cálculo.</summary>
	internal T Leer<T>(Func<MotorCompleto, T> lectura)
	{
		lock (_sync)
			return lectura(this);
	}

	internal string Precio(decimal precio)
	{
		var decimales = Cfg.TamanoTick.ToString(System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0') is var t && t.Contains('.')
			? t.Length - t.IndexOf('.') - 1
			: 0;

		return precio.ToString("F" + decimales, System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>Procesa una vela de 1M cerrada. Las repetidas o desordenadas se ignoran y se cuentan.</summary>
	public void Procesar(EntradaVela1M entrada)
	{
		lock (_sync)
			ProcesarInterno(entrada);
	}

	private void ProcesarInterno(EntradaVela1M entrada)
	{
		if (Velas1M.Count > 0 && entrada.InicioUtc <= Velas1M[^1].InicioUtc)
		{
			FueraDeOrden++;
			return;
		}

		if (Velas1M.Count > 0 && entrada.InicioUtc - Velas1M[^1].InicioUtc > TimeSpan.FromMinutes(1)
			&& Calendario.DiaGlobex(entrada.InicioUtc) == Calendario.DiaGlobex(Velas1M[^1].InicioUtc))
		{
			Huecos++;
		}

		var vela = ConstruirVela(entrada);
		Cvd.Aplicar(vela);

		// Mapa LVN: con la primera vela procesada de cada sesión de NY, antes de sumarla a los perfiles.
		var ventanaNy = Calendario.Ventana(TipoSesion.NuevaYork, vela.InicioUtc);

		if (ventanaNy is { } ny && _ultimoMapaNy != ny.FechaLocal)
		{
			_ultimoMapaNy = ny.FechaLocal;
			Lvn.CrearMapa(vela.InicioUtc, vela.Apertura);
		}

		Velas1M.Add(vela);

		var dia = Calendario.DiaGlobex(vela.InicioUtc);
		Lvn.AgregarVela(vela, dia, ventanaNy);
		Lvn.SeguirReacciones(vela, dia);
		Liquidez.ProcesarVela1M(vela);

		// Marcos mayores que cierran en este mismo instante: primero el contexto.
		var vela5M = A5.Agregar(vela);
		var vela15M = A15.Agregar(vela);
		var vela1H = A60.Agregar(vela);

		if (vela1H is not null)
		{
			Lectura1H = Pivotes.LeerRango(A60.Velas);

			foreach (var pivote in Estructura.Procesar(A60.Velas))
				Liquidez.AgregarSwing1H(pivote);
		}

		if (vela15M is not null)
			Lectura15M = Pivotes.LeerRango(A15.Velas);

		if (vela5M is not null)
		{
			Lectura5M = Pivotes.LeerRango(A5.Velas);
			Ema5M.Agregar(vela5M);
			AgregarPivotes(Pivotes.Detectar(A5.Velas, Cfg.FuerzaPivoteInterno), Pivotes5M);
		}

		Lectura1M = Pivotes.LeerRango(Velas1M);
		Ema1M.Agregar(vela);
		AgregarPivotes(Pivotes.Detectar(Velas1M, Cfg.FuerzaPivoteInterno), Pivotes1M);
		FvgDetector.Procesar(Velas1M);

		ProcesarBarridos(vela, vela5M);

		Simulacion.Procesar1M(vela, Ema1M.Valor);

		if (vela5M is not null)
			Simulacion.Procesar5M(vela5M, Ema5M.Valor);

		// El volumen por precio solo se necesita en las velas recientes (los perfiles ya lo acumularon).
		if (Velas1M.Count > 600)
			Velas1M[^601].Niveles = null;

		if (A5.Velas.Count > 150)
			A5.Velas[^151].Niveles = null;

		UltimaVelaUtc = vela.FinUtc;
		Revision++;
	}

	private void AgregarPivotes(IEnumerable<Pivote> nuevos, List<Pivote> lista)
	{
		foreach (var pivote in nuevos)
		{
			lista.Add(pivote);
			Divergencias.Agregar(pivote);
			Liquidez.AgregarPivoteInterno(pivote, Estructura.RangoInterno(), Precio);
		}

		if (lista.Count > 3000)
			lista.RemoveRange(0, lista.Count - 3000);
	}

	private Vela ConstruirVela(EntradaVela1M e)
	{
		var vela = new Vela
		{
			Marco = Marco.M1,
			Indice = Velas1M.Count,
			InicioUtc = e.InicioUtc,
			FinUtc = e.InicioUtc.AddMinutes(1),
			Apertura = e.Apertura,
			Maximo = e.Maximo,
			Minimo = e.Minimo,
			Cierre = e.Cierre,
			Volumen = e.Volumen,
			Delta = e.Delta,
			DeltaMaximo = e.DeltaMaximo,
			DeltaMinimo = e.DeltaMinimo
		};

		if (e.Niveles.Count > 0)
		{
			vela.Niveles = new SortedDictionary<long, Nivel>();

			foreach (var n in e.Niveles)
			{
				var tick = Cfg.ATick(n.Precio);
				vela.Niveles[tick] = vela.Niveles.TryGetValue(tick, out var previo) ? new Nivel(previo.Bid + n.Bid, previo.Ask + n.Ask) : new Nivel(n.Bid, n.Ask);
			}

			vela.Huella = Huella.Calcular(vela.Niveles, Cfg, vela.Maximo, vela.Minimo);
		}

		// Footprint completo: hay niveles y suman (casi) todo el volumen de la vela.
		var suma = vela.Niveles?.Values.Sum(n => n.Volumen) ?? 0;
		vela.FootprintCompleto = vela.Niveles is { Count: > 0 } && suma >= e.Volumen * 0.98m;

		if (!vela.FootprintCompleto)
			VelasSinHuella++;

		return vela;
	}

	// --- Datos para dibujar (los visores leen copias) -----------------------------------------------------

	internal DatosDibujo Dibujo(DateTime desdeUtc, DateTime hastaUtc, PerfilVisible perfil, bool mapasHistoricos)
	{
		lock (_sync)
		{
			var mapa = Lvn.MapaEn(hastaUtc);

			return new DatosDibujo
			{
				Mapas = mapasHistoricos
					? Lvn.Mapas.Where(m => (m.FinUtc ?? DateTime.MaxValue) >= desdeUtc && m.CreadoUtc <= hastaUtc).ToList()
					: mapa is null ? new List<MapaLvn>() : new List<MapaLvn> { mapa },
				Perfil = mapa is not null && perfil != PerfilVisible.Ninguno && mapa.Perfiles.TryGetValue(perfil, out var p)
					? new SortedDictionary<long, decimal>(p)
					: null,
				Senales = Senales.Where(s => s.ConfirmadaUtc >= desdeUtc && s.ConfirmadaUtc <= hastaUtc.AddMinutes(5)).ToList(),
				Bloqueados = SetupsBloqueados.Where(s => s.Vela.FinUtc >= desdeUtc && s.Vela.InicioUtc <= hastaUtc).ToList(),
				Barridos = Barridos.Where(b => b.ConocidoUtc >= desdeUtc && b.VelaBarrido.InicioUtc <= hastaUtc).ToList(),
				Fvgs = FvgDetector.Fvgs.Where(f => (f.InvalidadoUtc ?? DateTime.MaxValue) >= desdeUtc && f.C1.InicioUtc <= hastaUtc).ToList(),
				Divergencias = Divergencias.Divergencias.Where(d => d.ConfirmadaUtc >= desdeUtc && d.P1.ExtremoUtc <= hastaUtc).ToList(),
				Directrices = Oportunidades
					.SelectMany(o => new[] { o.DirectrizM1, o.DirectrizM2 })
					.Where(l => l is not null && l.P1.ExtremoUtc <= hastaUtc && (l.RotaUtc ?? DateTime.MaxValue) >= desdeUtc)
					.Distinct()
					.ToList()!,
				Ema = (Cfg.MarcoEma == MarcoEma.M1 ? Ema1M : Ema5M).PorInicio
					.Where(kv => kv.Key >= desdeUtc.AddMinutes(-5) && kv.Key <= hastaUtc)
					.OrderBy(kv => kv.Key)
					.ToList(),
				MarcoEma = Cfg.MarcoEma == MarcoEma.M1 ? Marco.M1 : Marco.M5,
				Estado = $"{Estructura.Sesgo.Texto()} · señales {Senales.Count} · rev. {Revision}",
				Tick = Cfg.TamanoTick
			};
		}
	}

	/// <summary>Velas CVD de 1M o 5M en un intervalo, para el panel CVD.</summary>
	internal List<Vela> VelasCvd(int minutos, DateTime desdeUtc, DateTime hastaUtc)
	{
		lock (_sync)
		{
			var origen = minutos == 5 ? A5.Velas : Velas1M;
			return origen.Where(v => v.InicioUtc >= desdeUtc && v.InicioUtc <= hastaUtc).ToList();
		}
	}
}

/// <summary>Copia de lo que se dibuja en los gráficos de 1M y 5M (mismo mapa, mismos IDs, misma versión).</summary>
internal sealed class DatosDibujo
{
	public List<MapaLvn> Mapas = new();
	public SortedDictionary<long, decimal>? Perfil;
	public List<Senal> Senales = new();
	public List<SetupBloqueado> Bloqueados = new();
	public List<EventoBarrido> Barridos = new();
	public List<Fvg> Fvgs = new();
	public List<Divergencia> Divergencias = new();
	public List<Directriz> Directrices = new();
	public List<KeyValuePair<DateTime, decimal>> Ema = new();
	public Marco MarcoEma;
	public string Estado = "";
	public decimal Tick;
}
