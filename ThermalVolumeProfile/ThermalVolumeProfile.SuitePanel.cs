// Solo dentro de SuiteOrderFlowStacker (símbolo SUITE): en el ThermalVolumeProfile por separado este archivo queda vacío.
#if SUITE
namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// Panel flotante del Thermal y botón GATILLO (10.1-10.4 y anexo 10): PERFIL, RANGO, ZONA LVN, LÍNEAS LVN y GATILLO; el
/// recuadro dibujado se manda a Session Sweeps Pro (ManualTriggerRequest) y la respuesta se dibuja en el gráfico y se
/// lee en el panel como vela compuesta con su flecha (verde, roja o [-]). Un recuadro que llega a la vela actual se
/// reevalúa en vivo hasta "Fijar". ESC cancela la herramienta. FVG y liquidez interna/externa cambian el dibujo de
/// Session Sweeps Pro (por el bus). Una línea de estado: LVN, rango y conexión con el motor.
/// </summary>
public partial class ThermalVolumeProfileLVN
{
	private sealed class CajaManual
	{
		public ManualTriggerRequest Solicitud = null!;
		public ManualTriggerResult? Resultado;
		public bool Fijada;
		public DateTime UltimoEnvio;
	}

	private readonly object _syncCajas = new();
	private readonly List<CajaManual> _cajas = new();
	private CanalSuite? _canalSuite;
	private Action<EventoSuite>? _suscripcionSuite;
	private string _instrumentoSuite = "";
	private bool _panelThermalAbierto;
	private TipoAnalisisManual _tipoManual = TipoAnalisisManual.Auto;
	private DireccionManual _direccionManual = DireccionManual.Auto;
	private long _siguienteCaja;

	[Display(Name = "Panel flotante del Thermal", GroupName = GSuite, Order = 30,
		Description = "Ventana movible (cualquier monitor) con PERFIL, RANGO, ZONA LVN, LÍNEAS LVN, GATILLO y la lectura de la vela compuesta.")]
	public bool ShowThermalPanel { get; set; } = true;

	[Display(Name = "Gatillo manual: tipo de análisis", GroupName = GSuite, Order = 40)]
	public TipoAnalisisManual ManualAnalysisType { get => _tipoManual; set => _tipoManual = value; }

	[Display(Name = "Gatillo manual: dirección", GroupName = GSuite, Order = 50, Description = "AUTO: la decide la vela compuesta ([-] si no hay direccionalidad clara).")]
	public DireccionManual ManualDirection { get => _direccionManual; set => _direccionManual = value; }

	[Display(Name = "Cajas de gatillo máximas", GroupName = GSuite, Order = 60)]
	[Range(1, 50)]
	public int MaxManualBoxes { get; set; } = 5;

	[Display(Name = "Reevaluar en vivo cada (s)", GroupName = GSuite, Order = 70, Description = "Un recuadro que llega a la vela actual se vuelve a analizar hasta pulsar Fijar.")]
	[Range(1, 60)]
	public int ManualRefreshSec { get; set; } = 5;

	/// <summary>Cada 2 s (desde el latido): suscripción al bus, reevaluación en vivo y panel.</summary>
	private void SuiteCiclo(DateTime ahora)
	{
		SuiteConectar();
		SuiteReevaluar(ahora);

		if (ShowThermalPanel && !_panelThermalAbierto && FabricaPaneles.Abrir is { } abrir && !string.IsNullOrEmpty(Instrumento))
		{
			_panelThermalAbierto = true;
			abrir("Thermal_" + Instrumento, $"Thermal Volume Profile + LVN · {Instrumento}", ContenidoThermal, BotonesThermal(), 300);
		}
	}

	private void SuiteConectar()
	{
		if (Instrumento == _instrumentoSuite && _suscripcionSuite is not null)
			return;

		SuiteDesconectar();

		if (string.IsNullOrEmpty(Instrumento))
			return;

		_instrumentoSuite = Instrumento;
		_canalSuite = SuiteEventBus.Para(Instrumento);
		_suscripcionSuite = AlEventoSuiteThermal;
		_canalSuite.Suscribir(_suscripcionSuite);
	}

	private void SuiteDesconectar()
	{
		if (_canalSuite is { } canal && _suscripcionSuite is { } s)
			canal.Desuscribir(s);

		(_canalSuite, _suscripcionSuite) = (null, null);
	}

	protected override void OnDispose()
	{
		SuiteDesconectar();

		if (_panelThermalAbierto && FabricaPaneles.Cerrar is { } cerrar)
			cerrar("Thermal_" + _instrumentoSuite);

		_panelThermalAbierto = false;
	}

	/// <summary>Respuestas de Session Sweeps Pro a los recuadros de este Thermal.</summary>
	private void AlEventoSuiteThermal(EventoSuite e)
	{
		if (e is not ManualTriggerResult r)
			return;

		var mia = false;

		lock (_syncCajas)
		{
			foreach (var c in _cajas.Where(c => c.Solicitud.Id == r.SolicitudId))
				(c.Resultado, mia) = (r, true);
		}

		if (mia)
			RedrawChart();
	}

	/// <summary>Recuadro del botón GATILLO terminado: se manda a Session Sweeps Pro.</summary>
	private void SuiteGatillo(int barA, int barB, decimal alto, decimal bajo)
	{
		var ahora = DateTime.UtcNow;
		var enVivo = barB >= CurrentBar - 1;
		var r = new ManualTriggerRequest
		{
			Id = (ahora.Ticks ^ (++_siguienteCaja << 40)) & long.MaxValue,
			Fuente = FuenteSuite.Thermal,
			Utc = ahora,
			CambioUtc = ahora,
			Version = 1,
			DesdeUtc = GetCandle(barA).Time,
			HastaUtc = enVivo || barB + 1 >= CurrentBar ? ahora : GetCandle(barB + 1).Time,
			Alto = alto,
			Bajo = bajo,
			Tipo = _tipoManual,
			Direccion = _direccionManual,
			EnVivo = enVivo
		};

		lock (_syncCajas)
		{
			_cajas.Add(new CajaManual { Solicitud = r, UltimoEnvio = ahora });

			if (_cajas.Count > MaxManualBoxes)
				_cajas.RemoveRange(0, _cajas.Count - MaxManualBoxes);
		}

		SuiteConectar();
		_canalSuite?.Publicar(r);
	}

	/// <summary>Recuadros en vivo sin fijar: se vuelven a mandar con la hora de ahora.</summary>
	private void SuiteReevaluar(DateTime ahora, bool todas = false)
	{
		List<ManualTriggerRequest> enviar = new();

		lock (_syncCajas)
		{
			foreach (var c in _cajas.Where(c => !c.Fijada && (todas || (c.Solicitud.EnVivo && ahora - c.UltimoEnvio >= TimeSpan.FromSeconds(ManualRefreshSec)))))
			{
				var q = c.Solicitud;
				c.Solicitud = new ManualTriggerRequest
				{
					Id = q.Id, Fuente = q.Fuente, Utc = q.Utc, CambioUtc = ahora, Version = q.Version + 1, DesdeUtc = q.DesdeUtc,
					HastaUtc = q.EnVivo ? ahora : q.HastaUtc, Alto = q.Alto, Bajo = q.Bajo, Tipo = _tipoManual, Direccion = _direccionManual, EnVivo = q.EnVivo
				};
				c.UltimoEnvio = ahora;
				enviar.Add(c.Solicitud);
			}
		}

		foreach (var q in enviar)
			_canalSuite?.Publicar(q);
	}

	public override bool ProcessKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == System.Windows.Input.Key.Escape && (_herramienta != Herramienta.Ninguna || _inicioSeleccion is not null))
		{
			_herramienta = Herramienta.Ninguna;
			_inicioSeleccion = _finSeleccion = null;
			RedrawChart();
			return true;
		}

		return base.ProcessKeyDown(e);
	}

	private int BarraDeHora(DateTime utc)
	{
		int bajo = 0, alto = Math.Max(0, CurrentBar - 1);

		while (bajo < alto)
		{
			var medio = (bajo + alto + 1) / 2;

			if (GetCandle(medio).Time <= utc)
				bajo = medio;
			else
				alto = medio - 1;
		}

		return bajo;
	}

	private static Color ColorResultado(ManualTriggerResult? r) => r is null ? Color.Orange
		: r.Direccion == 0 ? Color.FromArgb(255, 138, 143, 152)
		: !r.Resultado.StartsWith("GATILLO POR", StringComparison.Ordinal) ? Color.Orange
		: r.Direccion > 0 ? Color.FromArgb(255, 0, 200, 83) : Color.FromArgb(255, 255, 82, 82);

	/// <summary>Los recuadros del GATILLO con su flecha (verde, roja o [-]) y el resultado.</summary>
	private void SuiteDibujar(RenderContext context)
	{
		List<CajaManual> cajas;

		lock (_syncCajas)
			cajas = _cajas.ToList();

		if (cajas.Count == 0 || CurrentBar == 0)
			return;

		var primera = Math.Max(0, FirstVisibleBarNumber);
		var ultima = Math.Max(primera, LastVisibleBarNumber);

		foreach (var c in cajas)
		{
			var q = c.Solicitud;
			var desde = BarraDeHora(q.DesdeUtc);
			var hasta = BarraDeHora(q.HastaUtc.AddTicks(-1));

			if (hasta < primera || desde > ultima)
				continue;

			var color = ColorResultado(c.Resultado);
			var x1 = ChartInfo!.GetXByBar(desde);
			var x2 = ChartInfo!.GetXByBar(hasta) + AnchoBarra;
			var caja = new Rectangle(x1, Y(q.Alto), Math.Max(2, x2 - x1), Math.Max(2, Y(q.Bajo) - Y(q.Alto)));
			context.FillRectangle(Color.FromArgb(30, color), caja);
			context.DrawRectangle(new RenderPen(color, c.Fijada || !q.EnVivo ? 2 : 1), caja);

			var y = caja.Top + caja.Height / 2;
			var direccion = c.Resultado?.Direccion ?? 0;

			if (direccion != 0)
			{
				const int t = 7;
				var x = caja.Right + 4;
				var puntos = direccion > 0
					? new[] { new Point(x, y + t), new Point(x + t, y - t), new Point(x + 2 * t, y + t) }
					: new[] { new Point(x, y - t), new Point(x + 2 * t, y - t), new Point(x + t, y + t) };
				context.FillPolygon(color, puntos);
			}

			var texto = c.Resultado is { } r
				? $"{(direccion == 0 ? "[-] " : "")}{r.Resultado} · {r.VelaAnalizada} · {r.Score}{(c.Fijada ? " · FIJADO" : q.EnVivo ? " · EN VIVO" : "")}"
				: EstadoMotor() == EstadoFuente.NoCargado ? "MOTOR NO CARGADO (Sessions Sweeps Pro)" : "analizando…";
			context.DrawString(texto, _fuenteEstado, color, new Rectangle(caja.Right + 22, y - 8, 420, 16), _izquierda);
		}
	}

	private EstadoFuente EstadoMotor() => _canalSuite?.Estado(FuenteSuite.SessionSweeps, DateTime.UtcNow) ?? EstadoFuente.NoCargado;

	private VisualSuite? VisualActual() => _canalSuite?.Todos<VisualSuite>().FirstOrDefault(v => v.Id == VisualSuite.IdEstado);

	/// <summary>Pide a Session Sweeps Pro un cambio de sus interruptores de dibujo.</summary>
	private void CambiarVisual(Func<VisualSuite, VisualSuite> cambio)
	{
		if (_canalSuite is not { } canal || VisualActual() is not { } actual)
			return;

		var ahora = DateTime.UtcNow;
		var nuevo = cambio(actual);
		canal.Publicar(new VisualSuite
		{
			Id = VisualSuite.IdPeticion, Fuente = FuenteSuite.Thermal, Utc = ahora, CambioUtc = ahora, Version = 1,
			Fvg = nuevo.Fvg, Interna = nuevo.Interna, Externa = nuevo.Externa
		});
	}

	private static string SiNo(bool? b) => b is null ? "?" : b.Value ? "SÍ" : "NO";

	private void Elegir(Herramienta h)
	{
		_herramienta = _herramienta == h ? Herramienta.Ninguna : h;
		RedrawChart();
	}

	/// <summary>Botones del panel Thermal (4.2): Perfil, Rango, Pierna, Zona, LVN, Borrar LVN, Deshacer, EDGE, Gatillo, Borrar gatillo.</summary>
	private IReadOnlyList<BotonPanel> BotonesThermal() => new List<BotonPanel>
	{
		new(() => "PERFIL: " + SiNo(ShowProfile), () => { ShowProfile = !ShowProfile; RedrawChart(); }, () => ShowProfile),
		new(() => "RANGO", () => Elegir(Herramienta.Rango), () => _herramienta == Herramienta.Rango),
		new(() => PiernaActiva ? "PIERNA: DESCONGELAR" : "PIERNA", () => { if (PiernaActiva) SuiteDescongelar(); else Elegir(Herramienta.Pierna); },
			() => _herramienta == Herramienta.Pierna || PiernaActiva),
		new(() => "BORRAR PIERNA", SuiteBorrarPierna),
		new(() => "ZONA", () => Elegir(Herramienta.Zona), () => _herramienta == Herramienta.Zona),
		new(() => "LVN", () => Elegir(Herramienta.Lvn), () => _herramienta == Herramienta.Lvn),
		new(() => "BORRAR LVN", SuiteBorrarLvn),
		new(() => "DESHACER", SuiteDeshacerLvn),
		new(() => "EDGE", () => Elegir(Herramienta.Edge), () => _herramienta == Herramienta.Edge),
		new(() => "GATILLO", () => Elegir(Herramienta.Gatillo), () => _herramienta == Herramienta.Gatillo),
		new(() => "BORRAR GATILLO", SuiteBorrarGatillo),
		new(() => "Tipo: " + (_tipoManual switch { TipoAnalisisManual.VelaVolumen => "VELA", TipoAnalisisManual.VelaCompuesta => "COMPUESTA", _ => "AUTO" }),
			() => _tipoManual = (TipoAnalisisManual)(((int)_tipoManual + 1) % 3)),
		new(() => "Dirección: " + (_direccionManual switch { DireccionManual.Largo => "LARGO", DireccionManual.Corto => "CORTO", _ => "AUTO" }),
			() => _direccionManual = (DireccionManual)(((int)_direccionManual + 1) % 3)),
		new(() => "Fijar", () =>
		{
			lock (_syncCajas)
				_cajas.ForEach(c => c.Fijada = true);

			RedrawChart();
		}),
		new(() => "Recalcular", () => SuiteReevaluar(DateTime.UtcNow, todas: true)),
		new(() => "FVG: " + SiNo(VisualActual()?.Fvg), () => CambiarVisual(v => new VisualSuite { Fvg = !v.Fvg, Interna = v.Interna, Externa = v.Externa })),
		new(() => "Interna: " + SiNo(VisualActual()?.Interna), () => CambiarVisual(v => new VisualSuite { Fvg = v.Fvg, Interna = !v.Interna, Externa = v.Externa })),
		new(() => "Externa: " + SiNo(VisualActual()?.Externa), () => CambiarVisual(v => new VisualSuite { Fvg = v.Fvg, Interna = v.Interna, Externa = !v.Externa }))
	};

	private string Precio(decimal p) => p.ToString("#,##0.00", CultureInfo.InvariantCulture);

	/// <summary>Contenido del panel flotante del Thermal (hilo de la ventana: todo se copia bajo cerrojo).</summary>
	private ContenidoPanel ContenidoThermal(bool detalle)
	{
		var c = new ContenidoPanel { Titulo = "THERMAL VOLUME PROFILE + LVN", Pie = "Estadística simulada; no es un registro de operaciones reales." };
		var L = c.Lineas;
		Termico.ResultadoTermico? r;
		(int Desde, int Hasta) rango;

		lock (_sync)
			(r, rango) = (_resultado, _rango);

		// Estado: LVN, rango y conexión con el motor.
		var auto = r?.Lvns.Count(l => !l.Manual) ?? 0;
		var manual = r?.Lvns.Count(l => l.Manual) ?? 0;
		var textoRango = rango.Desde >= 0 && rango.Hasta >= rango.Desde && rango.Hasta < CurrentBar
			? $"{TiempoV2.ACdmx(GetCandle(rango.Desde).Time):dd/MM HH:mm} - {TiempoV2.HoraCdmx(GetCandle(rango.Hasta).Time)}"
			: "sin rango";
		var motor = EstadoMotor();
		L.Add(LineaPanel.Seccion("ESTADO"));
		L.Add(LineaPanel.De(new Segmento($"LVN AUTO {auto} · MANUAL {manual} · rango {_rangoId}: {textoRango}")));
		L.Add(LineaPanel.De(new Segmento("Sessions Sweeps Pro: "), motor switch
		{
			EstadoFuente.Cargado => new Segmento("CONECTADO", ColorPanel.Verde, true),
			EstadoFuente.SinDatos => new Segmento("SIN DATOS", ColorPanel.Naranja),
			EstadoFuente.VersionDistinta => new Segmento("VERSIÓN DISTINTA", ColorPanel.Rojo, true),
			_ => new Segmento("NO CARGADO (añádalo a un gráfico de 1 minuto)", ColorPanel.Amarillo)
		}));

		var edges = _seguidorEdge.Zonas.Count;
		L.Add(LineaPanel.De(new Segmento($"EDGE: {(!_p.EnableEdge ? "DETECCIÓN APAGADA" : $"{edges} activos{(ShowEdgeLines ? "" : " · dibujo apagado (cálculo activo)")}")}",
			_p.EnableEdge ? ColorPanel.Blanco : ColorPanel.Naranja)));

		if (PiernaActiva)
			L.Add(LineaPanel.De(new Segmento($"PERFIL CONGELADO: pierna {TiempoV2.HoraCdmx(PiernaDesdeUtc)} a {TiempoV2.HoraCdmx(PiernaHastaUtc)}", ColorPanel.AzulClaro, true)));

		var ayuda = _herramienta switch
		{
			Herramienta.Rango => "RANGO: arrastre sobre el gráfico de inicio a fin",
			Herramienta.Zona => "ZONA LVN: arrastre un rectángulo de precios",
			Herramienta.Lvn => "LVN: clic = punto, arrastre vertical = zona, clic sobre uno = quitar (válidos para el Modelo B)",
			Herramienta.Gatillo => "GATILLO: arrastre un recuadro sobre las velas del retroceso (ESC cancela)",
			Herramienta.Pierna => "PIERNA: arrastre de inicio a fin de la pierna (se congela su perfil)",
			Herramienta.Edge => "EDGE: clic = EDGE manual (lado por el perfil); clic sobre uno = quitar",
			_ => "Elija una herramienta"
		};
		L.Add(LineaPanel.De(new Segmento(ayuda, _herramienta == Herramienta.Ninguna ? ColorPanel.Gris : ColorPanel.Naranja)));
		L.Add(LineaPanel.De(new Segmento($"Gatillo: tipo {_tipoManual} · dirección {_direccionManual}", ColorPanel.Gris)));

		// La última caja: la vela compuesta y su flecha.
		CajaManual? caja;

		lock (_syncCajas)
			caja = _cajas.LastOrDefault();

		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("VELA COMPUESTA / GATILLO MANUAL"));

		if (caja is null)
		{
			L.Add(LineaPanel.De(new Segmento("Sin recuadro: pulse GATILLO y arrastre sobre las velas del retroceso", ColorPanel.Gris)));
			return c;
		}

		var q = caja.Solicitud;
		L.Add(LineaPanel.De(new Segmento($"Recuadro {Precio(q.Bajo)} - {Precio(q.Alto)} · {TiempoV2.HoraCdmx(q.DesdeUtc)} a {TiempoV2.HoraCdmx(q.HastaUtc)}" +
			$"{(caja.Fijada ? " · FIJADO" : q.EnVivo ? " · EN VIVO" : "")}")));

		if (caja.Resultado is not { } res)
		{
			L.Add(LineaPanel.De(motor == EstadoFuente.NoCargado
				? new Segmento("MOTOR NO CARGADO: Sessions Sweeps Pro (Suite) no está en ningún gráfico de este instrumento", ColorPanel.Amarillo)
				: new Segmento("Analizando…", ColorPanel.Naranja)));
			return c;
		}

		var valido = res.Resultado.StartsWith("GATILLO POR", StringComparison.Ordinal);
		var colorDir = res.Direccion > 0 ? ColorPanel.Verde : res.Direccion < 0 ? ColorPanel.Rojo : ColorPanel.Gris;
		L.Add(res.Direccion == 0
			? new LineaPanel { Recuadro = ColorPanel.Gris }.Con(new Segmento("[-] sin direccionalidad clara: se espera nueva confirmación", ColorPanel.Gris, true))
			: new LineaPanel { Recuadro = colorDir }.Con(new Segmento($"{res.Resultado} · {(res.Direccion > 0 ? "LARGO" : "CORTO")}",
				valido ? ColorPanel.Blanco : ColorPanel.Naranja, true, res.Direccion > 0 ? FlechaPanel.Arriba : FlechaPanel.Abajo, colorDir)));
		L.Add(LineaPanel.De(new Segmento($"{res.VelaAnalizada} · score {res.Score} · POC {(res.Poc is { } poc ? Precio(poc) : "-")} · delta {res.Delta:+0;-0;0}" +
			$"{(res.Subasta.Length > 0 ? " · subasta " + res.Subasta : "")}")));

		if (res.Entrada is { } entrada)
			L.Add(LineaPanel.De(new Segmento($"Entrada {Precio(entrada)} · SL {(res.Stop is { } sl ? Precio(sl) : "-")} · R:R {res.RewardRisk:0.0}")));

		foreach (var razon in res.Razones)
			L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento(razon, razon.StartsWith("Falta", StringComparison.Ordinal) ? ColorPanel.Naranja : ColorPanel.Blanco)));

		if (detalle)
		{
			foreach (var h in res.Hallazgos)
				L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento(h, ColorPanel.Gris)));
		}

		return c;
	}
}
#endif
