namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// v5 Indicador de Vela Compuesta (FAST): velas volumétricas de N contratos (por franja horaria; 2,300 de base) con los
/// gatillos GATILLO COMP (vela compuesta) y GATILLO VELAS VOLUMÉTRICAS, y los FVG. Panel ligero con esos botones, la
/// franja (desplegable), subir y bajar los contratos y el sugerido (promedio de las últimas 20 velas). Sus gatillos van al
/// bus: el Sweep Pro (FAST) los usa como confluencia. Todo el cálculo está en Nucleo/VelaCompuestaFast.cs.
/// </summary>
[DisplayName("Indicador de Vela Compuesta (FAST)")]
[Description("Suite Order Flow Stacker v5: velas volumétricas (2,300 contratos por defecto, por franja horaria), GATILLO COMP (vela compuesta) y GATILLO VELAS VOLUMÉTRICAS, FVG y el umbral sugerido con las últimas 20 velas, en un panel ligero.")]
public class IndicadorVelaCompuesta : Indicator
{
	private const string G1 = "1. Gatillos";
	private const string G2 = "2. Contratos por franja";
	private const string G3 = "3. Panel y dibujo";

	private readonly object _sync = new();
	private readonly Dictionary<FranjaHoraria, int> _contratos = Franjas.Todas.ToDictionary(f => f, _ => 2300);
	private MotorVelaCompuesta? _motor;
	private int _ultimaBarra = -1;
	private bool _enVivo, _panelAbierto;
	private int _minutos = 1;
	private string _instrumento = "";
	private DateTime _detalleDesde = DateTime.MinValue, _ultimoLatido;
	private FranjaHoraria _franjaPanel = FranjaHoraria.NyApertura;
	private RenderFont? _fuente;
	private IReadOnlyList<BotonPanel>? _botones;
	private readonly RenderStringFormat _izquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

	public IndicadorVelaCompuesta()
		: base(true)
	{
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		OrderFlowSuite.Ui.VentanaFlotante.Registrar();
		Panel = IndicatorDataProvider.CandlesPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	#region Parámetros

	[Display(Name = "Gatillo Comp (vela compuesta)", GroupName = G1, Order = 10, Description = "POC y área de valor en el tercio del extremo, delta que vira a favor en la última vela y cierre en el tercio contrario.")]
	public bool GatilloComp { get; set; } = true;

	[Display(Name = "Gatillo Velas Volumétricas", GroupName = G1, Order = 20, Description = "Una vela volumétrica de desplazamiento: rango, delta y rapidez respecto a sus medianas, cierre en el tercio a favor.")]
	public bool GatilloVolumetrico { get; set; } = true;

	[Display(Name = "Velas de la vela compuesta", GroupName = G1, Order = 30)]
	[Range(2, 20)]
	public int CompBarras { get; set; } = 4;

	[Display(Name = "Delta % mínimo", GroupName = G1, Order = 40)]
	[Range(0, 1)]
	public decimal DeltaPctMin { get; set; } = 0.25m;

	[Display(Name = "Rango: × mediana", GroupName = G1, Order = 50)]
	[Range(1, 10)]
	public decimal RangoMult { get; set; } = 1.5m;

	[Display(Name = "Rapidez: × duración mediana", GroupName = G1, Order = 60)]
	[Range(0.1, 2)]
	public decimal RapidaMult { get; set; } = 0.5m;

	[Display(Name = "Días con velas volumétricas (carga rápida)", GroupName = G1, Order = 70, Description = "Solo los últimos N días operativos arman velas volumétricas; los anteriores solo cuentan para el sugerido.")]
	[Range(1, 10)]
	public int HistorialDias { get; set; } = 1;

	[Display(Name = "Asia (18:00-02:00 NY)", GroupName = G2, Order = 10)]
	[Range(100, 100000)]
	public int ContratosAsia { get => _contratos[FranjaHoraria.Asia]; set => Contratos(FranjaHoraria.Asia, value); }

	[Display(Name = "Londres (02:00-08:00)", GroupName = G2, Order = 20)]
	[Range(100, 100000)]
	public int ContratosLondres { get => _contratos[FranjaHoraria.Londres]; set => Contratos(FranjaHoraria.Londres, value); }

	[Display(Name = "Premercado (08:00-09:30)", GroupName = G2, Order = 30)]
	[Range(100, 100000)]
	public int ContratosPremercado { get => _contratos[FranjaHoraria.Premercado]; set => Contratos(FranjaHoraria.Premercado, value); }

	[Display(Name = "NY apertura (09:30-10:30)", GroupName = G2, Order = 40)]
	[Range(100, 100000)]
	public int ContratosNyApertura { get => _contratos[FranjaHoraria.NyApertura]; set => Contratos(FranjaHoraria.NyApertura, value); }

	[Display(Name = "NY mañana (10:30-12:00)", GroupName = G2, Order = 50)]
	[Range(100, 100000)]
	public int ContratosNyManana { get => _contratos[FranjaHoraria.NyManana]; set => Contratos(FranjaHoraria.NyManana, value); }

	[Display(Name = "NY mediodía (12:00-14:00)", GroupName = G2, Order = 60)]
	[Range(100, 100000)]
	public int ContratosNyMediodia { get => _contratos[FranjaHoraria.NyMediodia]; set => Contratos(FranjaHoraria.NyMediodia, value); }

	[Display(Name = "NY tarde (14:00-18:00)", GroupName = G2, Order = 70)]
	[Range(100, 100000)]
	public int ContratosNyTarde { get => _contratos[FranjaHoraria.NyTarde]; set => Contratos(FranjaHoraria.NyTarde, value); }

	[Display(Name = "Mostrar FVG", GroupName = G3, Order = 10)]
	public bool MostrarFvg { get; set; } = true;

	[Display(Name = "Panel flotante", GroupName = G3, Order = 20)]
	public bool PanelFlotante { get; set; } = true;

	[Display(Name = "Tamaño de las etiquetas", GroupName = G3, Order = 30)]
	[Range(6, 20)]
	public int TamanoEtiquetas { get; set; } = 9;

	private void Contratos(FranjaHoraria f, int valor)
	{
		_contratos[f] = Math.Clamp(valor, 100, 100000);

		lock (_sync)
		{
			if (_motor is { } m)
				m.P.Contratos[f] = _contratos[f];
		}
	}

	#endregion

	#region Cálculo

	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			lock (_sync)
				Calcular(bar);
		}
		catch (Exception ex)
		{
			this.LogError("Indicador de Vela Compuesta (FAST): error de cálculo", ex);
		}
	}

	private void Calcular(int bar)
	{
		if (bar == 0)
			Reiniciar();

		if (_motor is not { } motor || bar <= _ultimaBarra)
			return;

		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;

		for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
		{
			var c = GetCandle(i);
			var detalle = c.Time >= _detalleDesde;
			var e = new VelaEntrada { InicioUtc = c.Time, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume, Delta = c.Delta };

			if (detalle && c.GetAllPriceLevels() is { } niveles)
			{
				foreach (var n in niveles)
					e.Niveles.Add(new NivelVela(n.Price, n.Bid, n.Ask));
			}

			// v5: en un gráfico de 5 minutos los datos siguen siendo de 1 minuto (sugerido, velas volumétricas, FVG).
			if (_minutos > 1)
			{
				foreach (var m in MinutosV5.Partir(e, _minutos, tick))
					motor.Vela(m, tick, conBarras: detalle);
			}
			else
			{
				motor.Vela(e, tick, conBarras: detalle);
			}
		}

		_ultimaBarra = bar;

		if (!_enVivo && bar >= CurrentBar - 1)
		{
			_enVivo = true;
			AbrirPanel();
		}

		Latido();
	}

	private void Reiniciar()
	{
		(_ultimaBarra, _enVivo) = (-1, false);
		_instrumento = InstrumentInfo?.Instrument ?? "";
		var minutos = MinutosV5.DeTimeFrame(ChartInfo?.ChartType, ChartInfo?.TimeFrame);
		_minutos = minutos is > 1 and <= 15 ? minutos : 1;
		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		_motor = new MotorVelaCompuesta(tick);
		Aplicar(_motor);
		_detalleDesde = CurrentBar > 0 ? Zonas.InicioDetalle(GetCandle(CurrentBar - 1).Time, HistorialDias) : DateTime.MinValue;
		_motor.AlSenal = Publicar;
	}

	private void Aplicar(MotorVelaCompuesta m)
	{
		foreach (var f in Franjas.Todas)
			m.P.Contratos[f] = _contratos[f];

		(m.P.GatilloComp, m.P.GatilloVolumetrico, m.P.MostrarFvg) = (GatilloComp, GatilloVolumetrico, MostrarFvg);
		(m.P.CompBarras, m.P.DeltaPctMin, m.P.RangoMult, m.P.RapidaMult) = (CompBarras, DeltaPctMin, RangoMult, RapidaMult);
	}

	/// <summary>Cada gatillo va al bus del instrumento (VolumetricTriggerEvent) para el Sweep Pro (FAST).</summary>
	private void Publicar(SenalVolumetrica s)
	{
		if (string.IsNullOrEmpty(_instrumento))
			return;

		SuiteEventBus.Para(_instrumento).Publicar(new VolumetricTriggerEvent
		{
			Id = s.Id, Fuente = FuenteSuite.VelaCompuesta, Utc = s.Utc, CambioUtc = s.Utc, Version = 1, Tipo = s.Tipo, Direccion = s.Direccion, Precio = s.Precio,
			Bajo = s.Bajo, Alto = s.Alto, Razones = s.Razones
		});
		RedrawChart();
	}

	private void Latido()
	{
		var ahora = DateTime.UtcNow;

		if (string.IsNullOrEmpty(_instrumento) || ahora - _ultimoLatido < TimeSpan.FromSeconds(2))
			return;

		_ultimoLatido = ahora;
		SuiteEventBus.Para(_instrumento).Latido(FuenteSuite.VelaCompuesta, _enVivo, ahora);
	}

	protected override void OnNewTrade(MarketDataArg trade)
	{
		try
		{
			if (!_enVivo || _motor is not { } m)
				return;

			bool? compra = trade.Direction == TradeDirection.Buy ? true : trade.Direction == TradeDirection.Sell ? false : null;
			m.Trade(trade.Time, trade.Price, trade.Volume, compra);
			Latido();
		}
		catch (Exception ex)
		{
			this.LogError("Indicador de Vela Compuesta (FAST): error en OnNewTrade", ex);
		}
	}

	protected override void OnDispose()
	{
		if (_panelAbierto && FabricaPaneles.Cerrar is { } cerrar)
			cerrar("VelaCompuesta_" + _instrumento);

		_panelAbierto = false;
	}

	#endregion

	#region Panel

	private void AbrirPanel()
	{
		if (!PanelFlotante || _panelAbierto || FabricaPaneles.Abrir is not { } abrir || string.IsNullOrEmpty(_instrumento))
			return;

		_panelAbierto = true;
		abrir("VelaCompuesta_" + _instrumento, $"Indicador de Vela Compuesta (FAST) · {_instrumento}", Contenido, Botones(), 500);
	}

	private static string SiNo(bool v) => v ? "SÍ" : "NO";

	private IReadOnlyList<BotonPanel> Botones() => _botones ??= new[]
	{
		new BotonPanel(() => "GATILLO COMP: " + SiNo(GatilloComp), () => { GatilloComp = !GatilloComp; AplicarVivo(); }, () => GatilloComp),
		new BotonPanel(() => "GATILLO VELAS VOLUMÉTRICAS: " + SiNo(GatilloVolumetrico), () => { GatilloVolumetrico = !GatilloVolumetrico; AplicarVivo(); }, () => GatilloVolumetrico),
		new BotonPanel(() => "FVG: " + SiNo(MostrarFvg), () => { MostrarFvg = !MostrarFvg; AplicarVivo(); }, () => MostrarFvg),
		new BotonPanel(() => $"FRANJA ▸ {Franjas.Texto(_franjaPanel)}", () => _franjaPanel = Franjas.Todas[((int)_franjaPanel + 1) % Franjas.Todas.Length]),
		new BotonPanel(() => "− 100", () => Contratos(_franjaPanel, _contratos[_franjaPanel] - 100)),
		new BotonPanel(() => "+ 100", () => Contratos(_franjaPanel, _contratos[_franjaPanel] + 100)),
		new BotonPanel(() => "USAR SUGERIDO", () =>
		{
			var s = _motor?.Sugerido(_franjaPanel) ?? 0;

			if (s > 0)
				Contratos(_franjaPanel, s);
		})
	};

	private void AplicarVivo()
	{
		lock (_sync)
		{
			if (_motor is { } m)
				Aplicar(m);
		}

		RedrawChart();
	}

	private ContenidoPanel Contenido(bool detalle)
	{
		var c = new ContenidoPanel { Titulo = $"INDICADOR DE VELA COMPUESTA (FAST) · {_instrumento}", Pie = PoliticaAlertasV2.AvisoLegal };

		if (_motor is not { } m)
		{
			c.Lineas.Add(LineaPanel.De(new Segmento("Cargando…", ColorPanel.Naranja)));
			return c;
		}

		var ahora = DateTime.UtcNow;
		var actual = Franjas.De(ahora);
		var (barras, senales, sugeridos, sugeridoAhora) = m.Leer(x => (x.Barras.Barras.Count, x.Senales.TakeLast(8).ToList(),
			Franjas.Todas.ToDictionary(f => f, f => x.Sugerido(f)), x.Sugerido()));
		var L = c.Lineas;
		L.Add(LineaPanel.Seccion("VELAS VOLUMÉTRICAS"));
		L.Add(LineaPanel.De(new Segmento($"Franja actual: {Franjas.Texto(actual)} · "), new Segmento($"{_contratos[actual]:#,##0} contratos por vela", ColorPanel.Blanco, true),
			new Segmento($" · sugerido {sugeridoAhora:#,##0} (promedio de las últimas 20 velas) · {barras} velas volumétricas", ColorPanel.Gris)));
		L.Add(LineaPanel.De(new Segmento($"Gatillo Comp {SiNo(GatilloComp)} · Gatillo Velas Volumétricas {SiNo(GatilloVolumetrico)} · FVG {SiNo(MostrarFvg)}", ColorPanel.Gris)));
		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("CONTRATOS POR FRANJA (▸ = la del desplegable; − / + / USAR SUGERIDO la cambian)"));

		foreach (var f in Franjas.Todas)
		{
			var marca = f == _franjaPanel ? "▸ " : "   ";
			L.Add(LineaPanel.De(new Segmento($"{marca}{Franjas.Texto(f)}: ", f == actual ? ColorPanel.Blanco : ColorPanel.Gris),
				new Segmento($"{_contratos[f]:#,##0}", f == _franjaPanel ? ColorPanel.Dorado : ColorPanel.Blanco, f == _franjaPanel),
				new Segmento(sugeridos[f] > 0 ? $" · sugerido {sugeridos[f]:#,##0}" : " · sin velas para sugerir", ColorPanel.Gris)));
		}

		L.Add(LineaPanel.Linea());
		L.Add(LineaPanel.Seccion("ÚLTIMOS GATILLOS"));

		if (senales.Count == 0)
			L.Add(LineaPanel.De(new Segmento("Sin gatillos", ColorPanel.Gris)));

		foreach (var s in Enumerable.Reverse(senales))
		{
			var color = s.Direccion > 0 ? ColorPanel.Verde : ColorPanel.Rojo;
			L.Add(LineaPanel.De(new Segmento($"{TiempoV2.HoraCdmx(s.Utc)} · ", ColorPanel.Gris),
				new Segmento($"{(s.Tipo == "COMPUESTA" ? "GATILLO COMP" : "GATILLO VOLUMÉTRICO")} {(s.Direccion > 0 ? "ALCISTA" : "BAJISTA")} {s.Precio.ToString("#,##0.00", CultureInfo.InvariantCulture)}",
					color, true, s.Direccion > 0 ? FlechaPanel.Arriba : FlechaPanel.Abajo, color)));

			if (detalle)
				L.Add(new LineaPanel { Sangria = 1 }.Con(new Segmento(s.Razones, ColorPanel.Gris)));
		}

		return c;
	}

	#endregion

	#region Dibujo

	private int BarDe(DateTime utc)
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

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		try
		{
			if (ChartInfo is null || _motor is not { } m)
				return;

			_fuente ??= new RenderFont("Arial", TamanoEtiquetas);
			var area = ChartInfo.PriceChartContainer.Region;
			var (senales, fvgs) = m.Leer(x => (x.Senales.ToList(), x.Fvg.Fvgs.Where(f => f.Vivo).Select(f => (f.Alcista, f.Bajo, f.Alto, f.Utc)).ToList()));
			var primera = FirstVisibleBarNumber;

			if (MostrarFvg)
			{
				foreach (var f in fvgs)
				{
					var x1 = ChartInfo.GetXByBar(Math.Max(primera, BarDe(f.Utc)));
					var (y1, y2) = (ChartInfo.GetYByPrice(f.Alto, false), ChartInfo.GetYByPrice(f.Bajo, false));
					context.FillRectangle(f.Alcista ? Color.FromArgb(40, 0, 150, 136) : Color.FromArgb(40, 244, 67, 54), new Rectangle(x1, y1, Math.Max(1, area.Right - x1), Math.Max(1, y2 - y1)));
				}
			}

			foreach (var s in senales)
			{
				var bar = BarDe(s.Utc);

				if (bar < primera || bar > LastVisibleBarNumber)
					continue;

				var color = s.Direccion > 0 ? Color.FromArgb(255, 0, 200, 83) : Color.FromArgb(255, 255, 82, 82);
				var x = ChartInfo.GetXByBar(bar);
				var y = ChartInfo.GetYByPrice(s.Direccion > 0 ? s.Bajo : s.Alto, false) + (s.Direccion > 0 ? 14 : -14);
				var t = 6;
				var puntos = s.Direccion > 0
					? new[] { new Point(x, y + t), new Point(x + t, y - t), new Point(x + 2 * t, y + t) }
					: new[] { new Point(x, y - t), new Point(x + 2 * t, y - t), new Point(x + t, y + t) };
				context.FillPolygon(color, puntos);
				context.DrawString(s.Tipo == "COMPUESTA" ? "COMP" : "VOL", _fuente, color, new Rectangle(x - 4, s.Direccion > 0 ? y + 8 : y - 22, 50, 14), _izquierda);
			}
		}
		catch (Exception ex)
		{
			this.LogError("Indicador de Vela Compuesta (FAST): error de dibujo", ex);
		}
	}

	#endregion
}
