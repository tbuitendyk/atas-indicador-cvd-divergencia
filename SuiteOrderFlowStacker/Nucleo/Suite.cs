namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Todos los parámetros de la suite (cada indicador expone en ATAS los grupos que usa).</summary>
public sealed class ConfiguracionSuite
{
	public ParametrosImbalance Imbalance { get; } = new();
	public ParametrosIceberg Iceberg { get; } = new();
	public ParametrosStops Stops { get; } = new();
	public ParametrosBarridos Barridos { get; } = new();
	public ParametrosMaster Master { get; } = new();
	public TablaUmbrales Umbrales { get; } = new();

	/// <summary>Umbrales en contratos por familia (v3): Configurar copia los del instrumento a los parámetros efectivos.</summary>
	public ContratosPorInstrumento Contratos { get; } = new();
	public ClasificadorSesion Sesiones { get; } = new();

	// Contexto (swings, números redondos, Initial Balance, tendencia y área de valor).
	public int SwingLengthBars { get; set; } = 20;
	public int RoundNumberStep { get; set; } = 50;
	public int IbMinutes { get; set; } = 60;
	public int TrendLengthBars { get; set; } = 50;
	public FuenteTendencia TrendSource { get; set; } = FuenteTendencia.VWAP;
	public int ValueAreaPercent { get; set; } = 70;
}

/// <summary>
/// La suite completa: contexto, imbalance, icebergs, stops, barridos, delta y (si se pide) el AbsorptionMaster.
/// Cada indicador de ATAS es una carcasa que crea SU MotorSuite (los indicadores de ATAS no comparten datos de forma
/// fiable) y muestra solo sus eventos; los demás motores corren dentro para el contexto (sesgo de los icebergs,
/// "con iceberg", "contra iceberg"). Todo el acceso pasa por <see cref="Cerrojo"/>: los callbacks de trades, DOM y
/// MBO llegan en otros hilos.
/// </summary>
public sealed class MotorSuite : IContextoIceberg
{
	private long _publicacionesEvaluadas = -1;
	private DateTime _ultimaEvaluacion;

	public MotorSuite(ConfiguracionSuite cfg, bool conMaster)
	{
		Cfg = cfg;
		ConMaster = conMaster;
		Eventos = new RegistroEventos();
		Contexto = new MotorContexto();
		Imbalance = new MotorImbalance(Eventos);
		Icebergs = new MotorIceberg(Eventos) { Contexto = this };
		Stops = new MotorStops(Eventos);
		Barridos = new MotorBarridos(Eventos);
		Delta = new MedidorDelta();
		Master = new MotorConfluencia(Eventos, Contexto, Imbalance, Icebergs, Stops, Barridos, Delta);

		Icebergs.AlConfirmar = i => Imbalance.MarcarIceberg(i.Lado, i.Precio, i.UltimaActividadUtc);
		Stops.Zonas = () => Contexto.NivelesLiquidez(conRedondos: true, conPerfil: false);
		Barridos.Niveles = () => Contexto.NivelesLiquidez(conRedondos: false, conPerfil: true);
		Barridos.Icebergs = () => Icebergs.Icebergs;
		Configurar(0.25m, false);
	}

	public object Cerrojo { get; } = new();

	public ConfiguracionSuite Cfg { get; }

	public bool ConMaster { get; }

	public RegistroEventos Eventos { get; }

	public MotorContexto Contexto { get; }

	public MotorImbalance Imbalance { get; }

	public MotorIceberg Icebergs { get; }

	public MotorStops Stops { get; }

	public MotorBarridos Barridos { get; }

	public MedidorDelta Delta { get; }

	public MotorConfluencia Master { get; }

	public decimal Tick { get; private set; } = 0.25m;

	public PerfilInstrumento Raiz { get; private set; } = PerfilInstrumento.NQ;

	public bool Mnq => Raiz == PerfilInstrumento.MNQ;

	/// <summary>RealTimeOnly: no se reconstruyen eventos con las velas del historial (el contexto sí).</summary>
	public bool SoloTiempoReal { get; set; }

	/// <summary>Pasa los parámetros y el tick a los motores (se llama al empezar cada cálculo).</summary>
	public void Configurar(decimal tick, bool mnq) => Configurar(tick, mnq ? PerfilInstrumento.MNQ : PerfilInstrumento.NQ);

	/// <summary>Pasa los parámetros, el tick y los umbrales en contratos de la familia del instrumento a los motores.</summary>
	public void Configurar(decimal tick, PerfilInstrumento raiz)
	{
		Tick = tick > 0 ? tick : 0.25m;
		Raiz = raiz is PerfilInstrumento.Auto ? PerfilInstrumento.NQ : raiz;
		var k = Cfg.Contratos.De(Raiz);
		Cfg.Imbalance.MinLevelVolumeContracts = k.MinLevelVolumeContracts;
		(Cfg.Iceberg.MinVisibleClipContracts, Cfg.Iceberg.MaxVisibleClipContracts) = (k.MinVisibleClipContracts, k.MaxVisibleClipContracts);
		(Cfg.Stops.MinCascadeContractsRth, Cfg.Stops.MinCascadeContractsEth) = (k.MinCascadeContractsRth, k.MinCascadeContractsEth);
		Cfg.Barridos.MinSweepContracts = k.MinSweepContracts;

		Contexto.Tick = Tick;
		Contexto.IbMinutes = Cfg.IbMinutes;
		Contexto.SwingLengthBars = Cfg.SwingLengthBars;
		Contexto.RoundNumberStep = Cfg.RoundNumberStep;
		Contexto.TrendLengthBars = Cfg.TrendLengthBars;
		Contexto.FuenteTendencia = Cfg.TrendSource;
		Contexto.ValueAreaPercent = Cfg.ValueAreaPercent;

		Imbalance.P = Cfg.Imbalance;
		Imbalance.Tick = Tick;

		Icebergs.P = Cfg.Iceberg;
		Icebergs.Umbrales = Cfg.Umbrales;
		Icebergs.Sesiones = Cfg.Sesiones;
		Icebergs.Raiz = Raiz;
		Icebergs.Tick = Tick;

		Stops.P = Cfg.Stops;
		Stops.Tick = Tick;
		Stops.Sesiones = Cfg.Sesiones;

		Barridos.P = Cfg.Barridos;
		Barridos.Tick = Tick;
		Barridos.Sesiones = Cfg.Sesiones;
		Barridos.MaxDisplacementTicks = Cfg.Iceberg.MaxDisplacementTicks;

		Master.P = Cfg.Master;
		Master.Tick = Tick;
		Master.Sesiones = Cfg.Sesiones;
		Master.EventContextSeconds = Cfg.Iceberg.EventContextSeconds;
	}

	/// <summary>
	/// Recálculo de las velas (cambio de parámetros o de gráfico): se rehacen contexto e imbalance; los eventos en vivo
	/// (icebergs, stops, barridos, señales) se conservan porque el historial no los puede reconstruir.
	/// </summary>
	public void ReiniciarVelas()
	{
		Contexto.Reiniciar();
		Imbalance.Reiniciar();
		Eventos.Quitar(e => e.Indicador == "Imbalance" && !e.Grabado);
		Eventos.Quitar(e => e.Grabado);
		Eventos.EnVivo = false;
	}

	/// <summary>Cambio de instrumento: todo desde cero.</summary>
	public void ReiniciarTodo()
	{
		ReiniciarVelas();
		Icebergs.Reiniciar();
		Stops.Reiniciar();
		Barridos.Reiniciar();
		Delta.Reiniciar();
		Master.Reiniciar();
		Eventos.Quitar(_ => true);
		Eventos.Novedades.Clear();
	}

	#region Datos

	/// <summary>Vela cerrada.</summary>
	public void Vela(VelaFlujo v)
	{
		Contexto.Agregar(v);

		if (!SoloTiempoReal || Eventos.EnVivo)
			Imbalance.Agregar(v, Cfg.Sesiones.De(v.InicioUtc), ClasificadorSesion.Rth(v.InicioUtc));
	}

	public void Trade(TradeFlujo t)
	{
		Contexto.ActualizarPrecio(t.Precio);
		Delta.Trade(t);
		Imbalance.Precio(t.Precio, t.Utc);
		Icebergs.Trade(t);
		Barridos.Trade(t);

		var mbo = Icebergs.ModoMboActivo || Barridos.ConIdAgresor;
		Stops.Modo = mbo ? ModoDatos.MBO : ModoDatos.Inferido;
		Stops.Trade(t);
		EvaluarMaster(t.Utc, false);
	}

	public void Dom(CambioDom d) => Icebergs.Dom(d);

	public void Mbo(MensajeMbo m)
	{
		Icebergs.Mbo(m);
		EvaluarMaster(m.Utc, false);
	}

	/// <summary>Reloj (cada segundo desde la carcasa): ráfagas vencidas, TRAMPA/LIQUIDEZ, AGUANTANDO, sesgos y Master.</summary>
	public void Tiempo(DateTime utc)
	{
		Stops.Tiempo(utc);
		Barridos.Tiempo(utc);
		Icebergs.Tiempo(utc);
		EvaluarMaster(utc, true);
	}

	/// <summary>El Master se evalúa cuando se publica algo nuevo y, como mucho, una vez por segundo con el reloj.</summary>
	private void EvaluarMaster(DateTime utc, bool reloj)
	{
		if (!ConMaster)
			return;

		var cambios = Eventos.Publicaciones != _publicacionesEvaluadas;

		if (!cambios && !(reloj && utc - _ultimaEvaluacion >= TimeSpan.FromSeconds(1)))
			return;

		_publicacionesEvaluadas = Eventos.Publicaciones;
		_ultimaEvaluacion = utc;
		Master.Evaluar(utc);
	}

	#endregion

	#region Contexto para el sesgo de los icebergs

	public IEnumerable<NivelReferencia> Extremos(Lado ladoIceberg)
	{
		var compra = ladoIceberg == Lado.Compra;

		foreach (var n in Contexto.NivelesPerfil())
		{
			if (n.Nombre == "LVN" || n.Nombre == (compra ? "VAL" : "VAH"))
				yield return n;
		}

		if ((compra ? Contexto.BajoDia : Contexto.AltoDia) is { } extremo)
			yield return new NivelReferencia(compra ? "mínimo de sesión" : "máximo de sesión", extremo, !compra);
	}

	public IEnumerable<NivelReferencia> Retrocesos(Lado ladoIceberg)
	{
		foreach (var n in Contexto.NivelesPerfil().Where(n => n.Nombre == "POC"))
			yield return n;

		if (Contexto.Vwap is { } vwap)
			yield return new NivelReferencia("VWAP", vwap, false);

		foreach (var z in Imbalance.Zonas.Where(z => z.Activa && z.Lado == ladoIceberg))
		{
			yield return new NivelReferencia("Imbalance", z.Bajo, false);
			yield return new NivelReferencia("Imbalance", z.Alto, true);
		}
	}

	public int Tendencia => Contexto.Tendencia;

	public EventoFlujo? BarridoOStops(Lado agresor, DateTime desdeUtc, DateTime hastaUtc)
	{
		var barridos = Barridos.Barridos.Where(b => b.Lado == agresor && b.Tipo != "RUPTURA" && b.Utc >= desdeUtc && b.Utc <= hastaUtc).Select(b => b.Evento);
		var stops = Stops.Cascadas.Where(c => c.Lado == agresor && c.Estado != "CONTINUACIÓN" && c.Utc >= desdeUtc && c.Utc <= hastaUtc).Select(c => c.Evento);
		return barridos.Concat(stops).OrderByDescending(e => e.Utc).FirstOrDefault();
	}

	#endregion
}
