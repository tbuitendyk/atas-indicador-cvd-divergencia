namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;
using ATAS.Indicators.Drawing;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

// Alias explícitos, como en el resto del proyecto: dibujo con System.Drawing, colores de las series con WPF.
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Pronóstico de la siguiente vela a partir del footprint, con barridos de liquidez y contexto de mercado.
/// Compra (punto debajo de la vela): la vela barre un mínimo de swing (lo penetra y cierra de vuelta por encima),
/// tiene volumen climático, desequilibrios diagonales compradores apilados, y cierra por encima de su POC, en su mitad
/// superior y con delta positivo. La venta es el espejo. El punto es grande (verde/rojo) si el nivel barrido era
/// liquidez externa, el extremo de la ventana de pivots, y pequeño (azul/naranja) si era interna.
/// Todo se evalúa con velas cerradas, así que los puntos no se repintan.
/// </summary>
[DisplayName("Footprint Predictor Pro (Sweeps & Context)")]
[Description("Barridos de liquidez externa e interna con volumen climático, desequilibrios apilados del footprint y cierre respecto al POC: pronóstico de la siguiente vela.")]
public class FootprintPredictorPro : Indicator
{
	#region Tipos

	private enum Contexto
	{
		Rango,
		Alcista,
		Bajista
	}

	private enum TipoBarrido
	{
		Ninguno,
		Interno,
		Externo
	}

	/// <summary>Nivel de swing que todavía es liquidez: ni barrido ni roto.</summary>
	private sealed record Nivel(decimal Precio, bool Externo);

	#endregion

	#region Campos

	private const int PeriodoVolumen = 20;
	private const int MaxNiveles = 1000;
	private const int MaxVelasVentana = 5000;
	private const int MaxPreciosVela = 10000;

	// OnCalculate y OnRender corren en hilos distintos.
	private readonly object _sync = new();

	private readonly ValueDataSeries _longExterno = new("LongExterno", "Long (Barrido Externo)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 4,
		Color = MediaColor.FromArgb(255, 0, 255, 0)
	};

	private readonly ValueDataSeries _longInterno = new("LongInterno", "Long (Barrido Interno)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 2,
		Color = MediaColor.FromArgb(255, 135, 206, 250)
	};

	private readonly ValueDataSeries _shortExterno = new("ShortExterno", "Short (Barrido Externo)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 4,
		Color = MediaColor.FromArgb(255, 220, 20, 60)
	};

	private readonly ValueDataSeries _shortInterno = new("ShortInterno", "Short (Barrido Interno)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 2,
		Color = MediaColor.FromArgb(255, 255, 165, 0)
	};

	private readonly List<Nivel> _minimos = new();
	private readonly List<Nivel> _maximos = new();

	private int _ultimaBarra = -1;
	private Contexto _contexto = Contexto.Rango;
	private DateTime? _inicioContexto;

	private decimal _ratio = 300m;
	private int _nivelesApilados = 3;
	private int _ventanaMinutos = 60;
	private int _penetracionTicks = 2;
	private int _recuperacionTicks = 1;
	private decimal _multiplicadorVolumen = 2.0m;

	private readonly RenderFont _fuenteTitulo = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteCuerpo = new("Arial", 9f, FontStyle.Bold);
	private readonly RenderFont _fuenteDetalle = new("Arial", 8f, FontStyle.Regular);
	private readonly RenderStringFormat _alinearIzquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderPen _bordePanel = new(Color.FromArgb(120, 255, 255, 255));

	#endregion

	#region Configuración

	[Display(Name = "Imbalance Ratio (%)", GroupName = "1. Footprint Imbalance", Order = 10,
		Description = "Porcentaje mínimo para considerar desequilibrio diagonal (300 = 3:1)")]
	[Range(100, 1000)]
	public decimal ImbalanceRatio
	{
		get => _ratio;
		set
		{
			_ratio = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Stacked Levels", GroupName = "1. Footprint Imbalance", Order = 20,
		Description = "Niveles consecutivos mínimos de desequilibrio")]
	[Range(2, 10)]
	public int StackedLevels
	{
		get => _nivelesApilados;
		set
		{
			_nivelesApilados = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Pivot Window (Minutos)", GroupName = "2. Swings de Liquidez", Order = 30,
		Description = "Un swing es liquidez externa si es el mínimo (o máximo) más extremo de estos minutos anteriores a él; si no, es liquidez interna.")]
	[Range(1, 1440)]
	public int PivotWindowMinutes
	{
		get => _ventanaMinutos;
		set
		{
			_ventanaMinutos = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Min Penetration (Ticks)", GroupName = "2. Swings de Liquidez", Order = 40,
		Description = "Ticks mínimos que el precio debe penetrar el pivot para validar el barrido")]
	[Range(1, 100)]
	public int MinPenetrationTicks
	{
		get => _penetracionTicks;
		set
		{
			_penetracionTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Reclaim Buffer (Ticks)", GroupName = "2. Swings de Liquidez", Order = 50,
		Description = "Ticks por dentro del nivel roto requeridos en el cierre para confirmar el rechazo. Si una vela cierra más allá del nivel sin recuperarlo, el nivel deja de ser liquidez.")]
	[Range(0, 50)]
	public int ReclaimBufferTicks
	{
		get => _recuperacionTicks;
		set
		{
			_recuperacionTicks = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Climatic Volume Multiplier", GroupName = "3. Volumen Climático", Order = 60,
		Description = "Multiplicador sobre la media de volumen de las 20 velas anteriores para catalogar volumen climático")]
	[Range(0, 100)]
	public decimal ClimaticVolumeMultiplier
	{
		get => _multiplicadorVolumen;
		set
		{
			_multiplicadorVolumen = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mostrar Panel de Contexto", GroupName = "4. Interfaz Visual", Order = 70)]
	public bool ShowContextPanel { get; set; } = true;

	[Display(Name = "Posición X", GroupName = "4. Interfaz Visual", Order = 80)]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "4. Interfaz Visual", Order = 90,
		Description = "Por defecto queda debajo de los paneles del Indicador de Tendencia Hourly y del CVD Trigger Engine.")]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 410;

	#endregion

	#region ctor

	public FootprintPredictorPro()
		: base(true)
	{
		// La serie por defecto no se usa: se oculta para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		DataSeries.Add(_longExterno);
		DataSeries.Add(_longInterno);
		DataSeries.Add(_shortExterno);
		DataSeries.Add(_shortInterno);

		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	#endregion

	#region Cálculo

	protected override void OnCalculate(int bar, decimal value)
	{
		lock (_sync)
		{
			if (bar == 0)
				Reiniciar();
			else if (bar < _ultimaBarra)
				return;

			// Las velas anteriores a 'bar' ya cerraron: cada una se evalúa una sola vez, con sus valores finales.
			for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
				ProcesarVelaCerrada(i);

			_ultimaBarra = bar;
		}
	}

	private void Reiniciar()
	{
		_minimos.Clear();
		_maximos.Clear();
		_ultimaBarra = -1;
		_contexto = Contexto.Rango;
		_inicioContexto = null;
	}

	private void ProcesarVelaCerrada(int c)
	{
		// Los puntos de la vela se recalculan desde cero, por si cambió la configuración.
		_longExterno[c] = 0;
		_longInterno[c] = 0;
		_shortExterno[c] = 0;
		_shortInterno[c] = 0;

		var tick = InstrumentInfo?.TickSize ?? 0m;

		if (tick <= 0)
			return;

		var vela = GetCandle(c);

		// 1. Barridos y roturas de los niveles de liquidez vigentes.
		var barridoMinimo = EvaluarNiveles(_minimos, vela, tick, true);
		var barridoMaximo = EvaluarNiveles(_maximos, vela, tick, false);

		// 2. Nuevos swings: fractal de 3 velas centrado en c - 1, confirmado por c.
		if (c >= 2)
			DetectarSwings(c);

		// 3. Contexto: solo lo cambian los barridos de liquidez externa.
		if (barridoMinimo == TipoBarrido.Externo)
			CambiarContexto(Contexto.Alcista, vela);
		else if (barridoMaximo == TipoBarrido.Externo)
			CambiarContexto(Contexto.Bajista, vela);

		if (c < PeriodoVolumen || (barridoMinimo == TipoBarrido.Ninguno && barridoMaximo == TipoBarrido.Ninguno))
			return;

		// 4. Volumen climático frente a la media de las velas anteriores.
		var suma = 0m;

		for (var i = c - PeriodoVolumen; i < c; i++)
			suma += GetCandle(i).Volume;

		if (vela.Volume < suma / PeriodoVolumen * _multiplicadorVolumen)
			return;

		// 5. Footprint: POC, mitad de la vela y desequilibrios apilados.
		var rango = vela.High - vela.Low;

		if (vela.MaxVolumePriceInfo?.Price is not decimal poc || rango <= 0)
			return;

		var (apiladoCompra, apiladoVenta) = AnalizarDesequilibrios(vela, tick);
		var mitad = vela.Low + rango * 0.5m;

		if (barridoMinimo != TipoBarrido.Ninguno && apiladoCompra && vela.Close > poc && vela.Close >= mitad && vela.Delta > 0)
		{
			if (barridoMinimo == TipoBarrido.Externo)
				_longExterno[c] = vela.Low - tick * 3;
			else
				_longInterno[c] = vela.Low - tick * 2;
		}

		if (barridoMaximo != TipoBarrido.Ninguno && apiladoVenta && vela.Close < poc && vela.Close <= mitad && vela.Delta < 0)
		{
			if (barridoMaximo == TipoBarrido.Externo)
				_shortExterno[c] = vela.High + tick * 3;
			else
				_shortInterno[c] = vela.High + tick * 2;
		}
	}

	/// <summary>
	/// Revisa los niveles vigentes contra una vela cerrada. Barrido: la vela penetra el nivel al menos
	/// 'Min Penetration' ticks y cierra de vuelta al menos 'Reclaim Buffer' ticks por dentro. Rotura: cierra más
	/// allá del nivel, que ya no es liquidez. En ambos casos el nivel se retira. Devuelve el barrido más
	/// significativo: externo antes que interno.
	/// </summary>
	private TipoBarrido EvaluarNiveles(List<Nivel> niveles, IndicatorCandle vela, decimal tick, bool sonMinimos)
	{
		var resultado = TipoBarrido.Ninguno;
		var penetracionMinima = _penetracionTicks * tick;
		var recuperacionMinima = _recuperacionTicks * tick;

		for (var i = niveles.Count - 1; i >= 0; i--)
		{
			var nivel = niveles[i];
			var penetracion = sonMinimos ? nivel.Precio - vela.Low : vela.High - nivel.Precio;
			var cierreDentro = sonMinimos ? vela.Close - nivel.Precio : nivel.Precio - vela.Close;

			if (penetracion >= penetracionMinima && cierreDentro >= recuperacionMinima)
			{
				niveles.RemoveAt(i);

				if (nivel.Externo)
					resultado = TipoBarrido.Externo;
				else if (resultado == TipoBarrido.Ninguno)
					resultado = TipoBarrido.Interno;
			}
			else if (cierreDentro < 0)
			{
				niveles.RemoveAt(i);
			}
		}

		return resultado;
	}

	private void DetectarSwings(int c)
	{
		var anterior = GetCandle(c - 2);
		var pivote = GetCandle(c - 1);
		var siguiente = GetCandle(c);

		if (pivote.Low < anterior.Low && pivote.Low < siguiente.Low)
			Agregar(_minimos, new Nivel(pivote.Low, EsExtremoDeVentana(c - 1, true)));

		if (pivote.High > anterior.High && pivote.High > siguiente.High)
			Agregar(_maximos, new Nivel(pivote.High, EsExtremoDeVentana(c - 1, false)));
	}

	private static void Agregar(List<Nivel> niveles, Nivel nivel)
	{
		niveles.Add(nivel);

		if (niveles.Count > MaxNiveles)
			niveles.RemoveAt(0);
	}

	/// <summary>Liquidez externa: el swing es el extremo de los 'Pivot Window' minutos anteriores a él.</summary>
	private bool EsExtremoDeVentana(int barra, bool esMinimo)
	{
		var pivote = GetCandle(barra);
		var desde = pivote.Time.AddMinutes(-_ventanaMinutos);

		for (var i = barra - 1; i >= 0 && barra - i <= MaxVelasVentana; i--)
		{
			var vela = GetCandle(i);

			if (vela.Time < desde)
				break;

			if (esMinimo ? vela.Low < pivote.Low : vela.High > pivote.High)
				return false;
		}

		return true;
	}

	/// <summary>
	/// Desequilibrios diagonales apilados: compra cuando el Ask de un precio es al menos 'Imbalance Ratio' veces el
	/// Bid del precio inferior; venta cuando el Bid de un precio es esas veces el Ask del precio superior. Cuenta las
	/// rachas de precios consecutivos; un precio sin operaciones corta la racha.
	/// </summary>
	private (bool Compra, bool Venta) AnalizarDesequilibrios(IndicatorCandle vela, decimal tick)
	{
		var ratio = _ratio / 100m;
		var rachaCompra = 0;
		var maxCompra = 0;
		var rachaVenta = 0;
		var maxVenta = 0;
		var precios = 0;

		for (var precio = vela.Low; precio < vela.High && precios < MaxPreciosVela; precio += tick, precios++)
		{
			var inferior = vela.GetPriceVolumeInfo(precio);
			var superior = vela.GetPriceVolumeInfo(precio + tick);

			if (inferior is null || superior is null)
			{
				rachaCompra = 0;
				rachaVenta = 0;
				continue;
			}

			rachaCompra = inferior.Bid > 0 && superior.Ask / inferior.Bid >= ratio ? rachaCompra + 1 : 0;
			maxCompra = Math.Max(maxCompra, rachaCompra);

			rachaVenta = superior.Ask > 0 && inferior.Bid / superior.Ask >= ratio ? rachaVenta + 1 : 0;
			maxVenta = Math.Max(maxVenta, rachaVenta);
		}

		return (maxCompra >= _nivelesApilados, maxVenta >= _nivelesApilados);
	}

	private void CambiarContexto(Contexto nuevo, IndicatorCandle vela)
	{
		if (_contexto == nuevo)
			return;

		_contexto = nuevo;

		// Las velas de ATAS están en UTC; se guarda la hora del gráfico.
		_inicioContexto = vela.Time + (InstrumentInfo?.TimeZoneOffset ?? TimeSpan.Zero);
	}

	#endregion

	#region Dibujo

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (!ShowContextPanel || ChartInfo is null)
			return;

		Contexto contexto;
		DateTime? inicio;

		lock (_sync)
		{
			contexto = _contexto;
			inicio = _inicioContexto;
		}

		const int ancho = 240;
		const int alto = 70;

		var x = PanelX;
		var y = PanelY;
		var panel = new Rectangle(x, y, ancho, alto);

		context.FillRectangle(Color.FromArgb(200, 20, 20, 25), panel);
		context.DrawRectangle(_bordePanel, panel);

		var (texto, color) = contexto switch
		{
			Contexto.Alcista => ("ALCISTA", Color.LimeGreen),
			Contexto.Bajista => ("BAJISTA", Color.IndianRed),
			_ => ("RANGO", Color.Goldenrod)
		};

		context.DrawString("ESTRUCTURA DE MERCADO", _fuenteTitulo, Color.Silver,
			new Rectangle(x + 10, y + 5, ancho - 20, 18), _alinearIzquierda);
		context.DrawString($"Contexto: {texto}", _fuenteCuerpo, color,
			new Rectangle(x + 10, y + 23, ancho - 20, 20), _alinearIzquierda);
		context.DrawString(inicio is { } hora ? $"Inicio: {hora:dd/MM/yyyy HH:mm}" : "Inicio: Esperando datos...", _fuenteDetalle, Color.LightGray,
			new Rectangle(x + 10, y + 45, ancho - 20, 18), _alinearIzquierda);
	}

	#endregion
}
