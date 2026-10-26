namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Motor del Footprint Predictor Pro: pronóstico de la siguiente vela a partir del footprint, con barridos de liquidez
/// y contexto de mercado. Compra (punto debajo de la vela): la vela barre un mínimo de swing (lo penetra y cierra de
/// vuelta por encima), tiene volumen climático, desequilibrios diagonales compradores apilados, y cierra por encima de
/// su POC, en su mitad superior y con delta positivo. La venta es el espejo. El punto es grande (verde/rojo) si el
/// nivel barrido era liquidez externa, el extremo de la ventana de pivots, y pequeño (azul/naranja) si era interna.
/// Todo se evalúa con velas cerradas, así que los puntos no se repintan.
/// </summary>
internal sealed class MotorFootprint
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

	public const int AnchoMinimo = 240;
	public const int AltoTitulo = 22;
	public const int AltoPanel = 70;

	private const int PeriodoVolumen = 20;
	private const int MaxNiveles = 1000;
	private const int MaxVelasVentana = 5000;
	private const int MaxPreciosVela = 10000;

	private readonly IGrafico _grafico;

	// El cálculo y el dibujo corren en hilos distintos.
	private readonly object _sync = new();

	private readonly List<Nivel> _minimos = new();
	private readonly List<Nivel> _maximos = new();

	private int _ultimaBarra = -1;
	private Contexto _contexto = Contexto.Rango;
	private DateTime? _inicioContexto;

	private readonly RenderFont _fuenteTitulo = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteCuerpo = new("Arial", 9f, FontStyle.Bold);
	private readonly RenderFont _fuenteDetalle = new("Arial", 8f, FontStyle.Regular);
	private readonly RenderStringFormat _alinearIzquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderPen _bordePanel = new(Color.FromArgb(120, 255, 255, 255));

	#endregion

	public MotorFootprint(IGrafico grafico) => _grafico = grafico;

	#region Series de puntos

	// Las dibuja ATAS: el indicador que usa el motor las agrega a sus DataSeries.

	public ValueDataSeries LongExterno { get; } = new("LongExterno", "Long (Barrido Externo)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 4,
		Color = MediaColor.FromArgb(255, 0, 255, 0)
	};

	public ValueDataSeries LongInterno { get; } = new("LongInterno", "Long (Barrido Interno)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 2,
		Color = MediaColor.FromArgb(255, 135, 206, 250)
	};

	public ValueDataSeries ShortExterno { get; } = new("ShortExterno", "Short (Barrido Externo)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 4,
		Color = MediaColor.FromArgb(255, 220, 20, 60)
	};

	public ValueDataSeries ShortInterno { get; } = new("ShortInterno", "Short (Barrido Interno)")
	{
		VisualType = VisualMode.Dots,
		ShowZeroValue = false,
		Width = 2,
		Color = MediaColor.FromArgb(255, 255, 165, 0)
	};

	public IEnumerable<ValueDataSeries> Series => new[] { LongExterno, LongInterno, ShortExterno, ShortInterno };

	#endregion

	#region Configuración

	/// <summary>Porcentaje mínimo para considerar desequilibrio diagonal (300 = 3:1).</summary>
	public decimal ImbalanceRatio { get; set; } = 300m;

	public int StackedLevels { get; set; } = 3;

	public int PivotWindowMinutes { get; set; } = 60;

	public int MinPenetrationTicks { get; set; } = 2;

	public int ReclaimBufferTicks { get; set; } = 1;

	public decimal ClimaticVolumeMultiplier { get; set; } = 2.0m;

	public bool MostrarPanel { get; set; } = true;

	#endregion

	#region Cálculo

	public void Calcular(int bar)
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
		LongExterno[c] = 0;
		LongInterno[c] = 0;
		ShortExterno[c] = 0;
		ShortInterno[c] = 0;

		var tick = _grafico.TamanoTick;

		if (tick <= 0)
			return;

		var vela = _grafico.Vela(c);

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
			suma += _grafico.Vela(i).Volume;

		if (vela.Volume < suma / PeriodoVolumen * ClimaticVolumeMultiplier)
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
				LongExterno[c] = vela.Low - tick * 3;
			else
				LongInterno[c] = vela.Low - tick * 2;
		}

		if (barridoMaximo != TipoBarrido.Ninguno && apiladoVenta && vela.Close < poc && vela.Close <= mitad && vela.Delta < 0)
		{
			if (barridoMaximo == TipoBarrido.Externo)
				ShortExterno[c] = vela.High + tick * 3;
			else
				ShortInterno[c] = vela.High + tick * 2;
		}
	}

	/// <summary>
	/// Revisa los niveles vigentes contra una vela cerrada. Barrido: la vela penetra el nivel al menos
	/// 'Min Penetration' ticks y cierra de vuelta al menos 'Reclaim Buffer' ticks por dentro. Rotura: cierra más allá
	/// del nivel, que ya no es liquidez. En ambos casos el nivel se retira. Devuelve el barrido más significativo:
	/// externo antes que interno.
	/// </summary>
	private TipoBarrido EvaluarNiveles(List<Nivel> niveles, IndicatorCandle vela, decimal tick, bool sonMinimos)
	{
		var resultado = TipoBarrido.Ninguno;
		var penetracionMinima = MinPenetrationTicks * tick;
		var recuperacionMinima = ReclaimBufferTicks * tick;

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
		var anterior = _grafico.Vela(c - 2);
		var pivote = _grafico.Vela(c - 1);
		var siguiente = _grafico.Vela(c);

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
		var pivote = _grafico.Vela(barra);
		var desde = pivote.Time.AddMinutes(-PivotWindowMinutes);

		for (var i = barra - 1; i >= 0 && barra - i <= MaxVelasVentana; i--)
		{
			var vela = _grafico.Vela(i);

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
		var ratio = ImbalanceRatio / 100m;
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

		return (maxCompra >= StackedLevels, maxVenta >= StackedLevels);
	}

	private void CambiarContexto(Contexto nuevo, IndicatorCandle vela)
	{
		if (_contexto == nuevo)
			return;

		_contexto = nuevo;
		_inicioContexto = vela.Time + _grafico.ZonaHoraria;
	}

	#endregion

	#region Dibujo

	/// <summary>Panel de estructura de mercado: contexto y hora en que empezó.</summary>
	public void DibujarPanel(RenderContext context, int x, int y, int ancho)
	{
		Contexto contexto;
		DateTime? inicio;

		lock (_sync)
		{
			contexto = _contexto;
			inicio = _inicioContexto;
		}

		var panel = new Rectangle(x, y, ancho, AltoPanel);

		// Franja del título: es la zona por la que se arrastra el panel.
		context.FillRectangle(Color.FromArgb(200, 20, 20, 25), panel);
		context.FillRectangle(Color.FromArgb(220, 32, 34, 42), new Rectangle(x, y, ancho, AltoTitulo));
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
