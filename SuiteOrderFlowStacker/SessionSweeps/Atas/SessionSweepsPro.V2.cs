namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// Session Sweeps Pro v2.0 en ATAS: trades guardados entre recálculos, bus de la suite (LVN del Thermal, gatillo manual,
/// latido), panel flotante y recálculo cuando una fuente de la suite carga después que este indicador.
/// </summary>
public partial class SessionSweepsPro
{
	/// <summary>Ajustes de la sección 28 (panel y dibujo): solo dibujan, no recalculan.</summary>
	private sealed class VisualV2
	{
		public bool MostrarPanel = true;
		public bool ShowInChartPanel;
		public int InChartPanelX = 25;
		public int InChartPanelY = 60;
		public bool ShowSweepBoxes = true;
		public bool ShowModelBoxes = true;
		public bool ShowFvgV2 = true;
		public bool ShowInternalLiquidity = true;
		public bool ShowExternalLiquidity = true;
		public bool ShowLvnLines = true;
		public bool ShowManualTriggerBoxes;
		public bool ShowTrackerDiagnostics;
		public bool FvgOnlyAfterSweep = true;
		public int BreakBannerSec = 60;
	}

	private const int MaximoRecalculosPorFuentes = 3;

	private readonly VisualV2 _v = new();
	private readonly AlmacenTicks _almacen = new();
	private readonly object _syncV2 = new();
	private string _instrumentoV2 = "";
	private CanalSuite? _canal;
	private Action<EventoSuite>? _suscripcion;
	private DateTime _ultimoLatidoV2;
	private DateTime _enVivoDesdeUtc;
	private HashSet<FuenteSuite> _fuentesAlCargar = new();
	private int _recalculosPorFuentes;
	private DateTime? _recalcularEn;
	private bool _panelAbierto;
	private bool _mboVisto;
	private DateTime _ultimaPeticionVisual, _ultimoVisual;
	private (bool, bool, bool) _visualPublicado;
	private DateTime _ultimoSaldo;

	private string InstrumentoV2 => InstrumentInfo?.Instrument ?? "";

	private string ClavePanel => "SessionSweepsPro_" + InstrumentoV2;

	/// <summary>Motor nuevo (cada recálculo): trades guardados, fuentes del bus y LVN.</summary>
	private void PrepararV2(MotorSweeps motor)
	{
		var instrumento = InstrumentoV2;

		if (instrumento != _instrumentoV2)
		{
			// Otro instrumento: trades, suscripción y recálculos automáticos desde cero.
			Desuscribir();
			_almacen.Vaciar();
			_instrumentoV2 = instrumento;
			_recalculosPorFuentes = 0;
			ReiniciarHistorialV2();
		}

		motor.Almacen = _almacen;
		motor.Instrumento = PrepararInstrumento();
		motor.Fuentes = string.IsNullOrEmpty(instrumento) ? SinFuentes.Instancia : new FuentesBus(instrumento, () => DateTime.UtcNow);
		Suscribir();
		PrepararCsvV2(motor);

		lock (_syncV2)
			_cajasManuales.Clear();
	}

	/// <summary>
	/// Fin de la carga del historial: los LVN vigentes del Thermal entran ahora (no antes: su hora es la de publicación, y
	/// el historial los daría por mitigados), se pide el historial de trades y se abre el panel.
	/// </summary>
	private void AlEntrarEnVivoV2(MotorSweeps motor)
	{
		_enVivoDesdeUtc = DateTime.UtcNow;

		if (_canal is { } canal)
		{
			foreach (var z in canal.Todos<LvnZone>().Where(z => z.Estado != "BORRADO").OrderBy(z => z.RangoId).ThenBy(z => z.Utc))
				motor.LvnDesdeSuite(z);

			foreach (var z in canal.Todos<EdgeZone>().Where(z => z.Estado != "BORRADO").OrderBy(z => z.RangoId).ThenBy(z => z.Utc))
				motor.EdgeDesdeSuite(z);

			_fuentesAlCargar = Enum.GetValues<FuenteSuite>().Where(f => canal.Estado(f, DateTime.UtcNow) != EstadoFuente.NoCargado).ToHashSet();
		}

		PedirHistorialV2();
		AbrirPanelV2();
	}

	private void Suscribir()
	{
		if (_suscripcion is not null || string.IsNullOrEmpty(_instrumentoV2))
			return;

		_canal = SuiteEventBus.Para(_instrumentoV2);
		_suscripcion = AlEventoSuite;
		_canal.Suscribir(_suscripcion);
	}

	private void Desuscribir()
	{
		if (_canal is { } canal && _suscripcion is { } s)
			canal.Desuscribir(s);

		(_canal, _suscripcion) = (null, null);
	}

	/// <summary>Eventos del bus (fuera de su cerrojo, en el hilo de quien publica).</summary>
	private void AlEventoSuite(EventoSuite e)
	{
		try
		{
			// FVG y liquidez interna/externa pedidos desde el panel del Thermal (anexo 10).
			if (e is VisualSuite { Id: VisualSuite.IdPeticion } v && v.CambioUtc > _ultimaPeticionVisual)
			{
				_ultimaPeticionVisual = v.CambioUtc;
				(_v.ShowFvgV2, _v.ShowInternalLiquidity, _v.ShowExternalLiquidity) = (v.Fvg, v.Interna, v.Externa);
				PublicarVisual(DateTime.UtcNow, forzar: true);
				RedrawChart();
				return;
			}

			if (!_enVivo || _motor is not { } motor)
				return;

			switch (e)
			{
				case LvnZone or RangeReset:
					motor.LvnDesdeSuite(e);

					if (e is RangeReset)
						motor.EdgeDesdeSuite(e);

					RedrawChart();
					break;

				case EdgeZone:
					motor.EdgeDesdeSuite(e);
					RedrawChart();
					break;

				case ManualTriggerRequest r:
					var resultado = motor.AnalizarManual(r);

					lock (_syncV2)
					{
						_cajasManuales.RemoveAll(m => m.Solicitud.Id == r.Id);
						_cajasManuales.Add((r, resultado));

						if (_cajasManuales.Count > Math.Max(1, _cfg.V2.MaxManualBoxes))
							_cajasManuales.RemoveRange(0, _cajasManuales.Count - Math.Max(1, _cfg.V2.MaxManualBoxes));
					}

					_canal?.Publicar(resultado);
					GrabarManualV2(r, resultado);
					RedrawChart();
					break;
			}
		}
		catch (Exception ex)
		{
			this.LogError("SessionSweepsPro: error con un evento de la suite", ex);
		}
	}

	/// <summary>Un trade que llega mientras se recalcula: se guarda para el motor nuevo.</summary>
	private void GuardarTradeV2(DateTime utc, decimal precio, decimal volumen, bool? compra)
	{
		if (string.IsNullOrEmpty(_instrumentoV2) || InstrumentoV2 != _instrumentoV2)
			return;

		_almacen.Agregar(new TickV2(utc, precio, volumen, compra));
	}

	/// <summary>
	/// Cada 2 s: latido de la fuente SessionSweeps, saldo de ATAS, recorte de trades viejos y, si una fuente de la suite
	/// cargó después que este indicador, un recálculo (para que el historial vea sus eventos; como mucho 3 por instrumento).
	/// </summary>
	private void LatidoV2()
	{
		var ahora = DateTime.UtcNow;

		if (ahora - _ultimoLatidoV2 < TimeSpan.FromSeconds(2) || _canal is not { } canal)
			return;

		_ultimoLatidoV2 = ahora;
		canal.Latido(FuenteSuite.SessionSweeps, _enVivo && _motor is not null, ahora);
		PublicarVisual(ahora);
		_mboVisto = canal.Consultar<IcebergEvent>(ahora.AddHours(-12), ahora, i => i.Mbo).Count > 0;

		if (_motor is { } motor && _cfg.V2.ReadAtasAccount && ahora - _ultimoSaldo > TimeSpan.FromSeconds(5))
		{
			_ultimoSaldo = ahora;
			motor.SaldoAtas = SaldoAtas();
		}

		if (_almacen.Count > 0 && _almacen[0].Utc < ahora.AddHours(-36))
			_almacen.RecortarAntesDe(ahora.AddHours(-30));

		if (!_enVivo)
			return;

		if (_recalcularEn is { } cuando)
		{
			if (ahora >= cuando)
			{
				_recalcularEn = null;
				RecalculateValues();
			}

			return;
		}

		if (_recalculosPorFuentes >= MaximoRecalculosPorFuentes)
			return;

		var nuevas = Enum.GetValues<FuenteSuite>()
			.Where(f => f is not (FuenteSuite.SessionSweeps or FuenteSuite.Thermal) && !_fuentesAlCargar.Contains(f) && canal.Estado(f, ahora) != EstadoFuente.NoCargado)
			.ToList();

		if (nuevas.Count == 0)
			return;

		foreach (var f in nuevas)
			_fuentesAlCargar.Add(f);

		// Solo si esa fuente trae eventos del historial (anteriores a la entrada en vivo de este indicador).
		if (!HayHistorial(canal, nuevas, _enVivoDesdeUtc))
			return;

		_recalculosPorFuentes++;
		_recalcularEn = ahora.AddSeconds(3);
	}

	/// <summary>Estado de los interruptores de dibujo para el panel del Thermal (si cambió, o cada 10 s).</summary>
	private void PublicarVisual(DateTime ahora, bool forzar = false)
	{
		var estado = (_v.ShowFvgV2, _v.ShowInternalLiquidity, _v.ShowExternalLiquidity);

		if (_canal is not { } canal || (!forzar && estado == _visualPublicado && ahora - _ultimoVisual < TimeSpan.FromSeconds(10)))
			return;

		(_visualPublicado, _ultimoVisual) = (estado, ahora);
		canal.Publicar(new VisualSuite
		{
			Id = VisualSuite.IdEstado, Fuente = FuenteSuite.SessionSweeps, Utc = ahora, CambioUtc = ahora, Version = 1,
			Fvg = estado.ShowFvgV2, Interna = estado.ShowInternalLiquidity, Externa = estado.ShowExternalLiquidity
		});
	}

	private static bool HayHistorial(CanalSuite canal, List<FuenteSuite> fuentes, DateTime hasta)
	{
		bool De<T>() where T : EventoSuite => canal.Consultar<T>(DateTime.MinValue, hasta, x => fuentes.Contains(x.Fuente)).Count > 0;

		return De<IcebergEvent>() || De<StopsEvent>() || De<SweepTrackerEvent>() || De<StackedEvent>() || De<ImbalanceEvent>() || De<AbsorptionEvent>();
	}

	private void AbrirPanelV2()
	{
		if (!_v.MostrarPanel || _panelAbierto || FabricaPaneles.Abrir is not { } abrir || string.IsNullOrEmpty(_instrumentoV2))
			return;

		_panelAbierto = true;
		abrir(ClavePanel, $"Sessions Sweeps Pro · {_instrumentoV2}", ContenidoPanelV2, BotonesPanelV2(), _cfg.V2.PanelRefreshMs);
	}

	private ContenidoPanel ContenidoPanelV2(bool detalle)
	{
		if (_motor is not { } motor)
		{
			var c = new ContenidoPanel { Titulo = "SESSIONS SWEEPS PRO", Pie = PoliticaAlertasV2.AvisoLegal };
			c.Lineas.Add(LineaPanel.De(new Segmento(_estado.Length > 0 ? _estado : "Cargando el historial…", ColorPanel.Naranja)));
			return c;
		}

		return motor.Panel(new OpcionesPanel
		{
			Detalle = detalle,
			ArchivoCsv = ArchivoCsvV2(),
			Mbo = _mboVisto,
			SaldoAtas = motor.SaldoAtas,
			CorreoConfigurado = CanalesConfigurados().Correo,
			TelegramConfigurado = CanalesConfigurados().Telegram,
			Envios = UltimosEnvios,
			Mt5 = _mt5.LineasPanel(motor.Raiz, motor.Instrumento.Spec)
		});
	}

	private (DateTime Utc, bool Correo, bool Telegram) _canales;

	/// <summary>Si hay credenciales guardadas (se mira el disco cada 10 s, no en cada refresco del panel).</summary>
	private (bool Correo, bool Telegram) CanalesConfigurados()
	{
		if (DateTime.UtcNow - _canales.Utc > TimeSpan.FromSeconds(10))
			_canales = (DateTime.UtcNow, !string.IsNullOrWhiteSpace(SmtpUser) && ExisteCredencial("smtp"), !string.IsNullOrWhiteSpace(TelegramChatId) && ExisteCredencial("telegram"));

		return (_canales.Correo, _canales.Telegram);
	}

	/// <summary>Controles del panel principal (v3, 13.16): solo actúan sobre el puente MT5; nunca abren operaciones.</summary>
	private IReadOnlyList<BotonPanel> BotonesPanelV2() => new[]
	{
		new BotonPanel(() => _mt5.Pausado ? "REANUDAR" : "PAUSAR", () => _mt5.Pausado = !_mt5.Pausado, () => _mt5.Pausado),
		new BotonPanel(() => "CERRAR TODO", () => _mt5.CerrarTodo()),
		new BotonPanel(() => _mt5.OrdenesDeshabilitadas ? "HABILITAR ÓRDENES" : "DESHABILITAR ÓRDENES", () => _mt5.OrdenesDeshabilitadas = !_mt5.OrdenesDeshabilitadas,
			() => _mt5.OrdenesDeshabilitadas)
	};

	/// <summary>Nombre del CSV del día (S10 lo escribe).</summary>
	private string ArchivoCsvV2() => $"SessionSweeps_{_instrumentoV2}_{DateTime.UtcNow:yyyy-MM-dd}.csv";

	private void CerrarV2()
	{
		Desuscribir();

		if (_panelAbierto && FabricaPaneles.Cerrar is { } cerrar)
			cerrar(ClavePanel);

		_panelAbierto = false;
	}
}
