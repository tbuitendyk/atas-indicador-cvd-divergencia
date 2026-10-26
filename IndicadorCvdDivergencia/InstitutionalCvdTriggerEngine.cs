namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators;
using ATAS.Indicators.Drawing;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

// Alias explícitos, como en el resto del proyecto: dibujo con System.Drawing, colores configurables con WPF.
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Motor de gatillo sobre el CVD (delta acumulado por sesión) con cuatro filtros en cascada:
/// 1. macro-divergencia CVD entre swings amplios;
/// 2. micro-divergencia CVD entre swings cortos;
/// 3. absorción en la vela extrema del swing de la micro-divergencia: tamaño de la mecha y volumen negociado en ella;
/// 4. giro de delta agresivo más allá del umbral en una vela posterior a ese swing.
/// Con los cuatro en la misma dirección se dibuja una bandera verde (compra) o roja (venta) en la vela que completa
/// la confirmación. Todo se evalúa con velas cerradas, así que las banderas no se repintan.
/// </summary>
[DisplayName("Institutional CVD Trigger & Absorption Engine")]
[Description("Motor institucional autosuficiente: Macro y Micro Divergencias CVD, Absorción cuantitativa, Delta Shift y Banderas Gatillo.")]
public class InstitutionalCvdTriggerEngine : Indicator
{
	#region Tipos

	public enum ModoVigencia
	{
		[Display(Name = "Por barras")]
		PorBarras,

		[Display(Name = "Hasta divergencia opuesta")]
		HastaOpuesta
	}

	private enum Direccion
	{
		Alcista,
		Bajista
	}

	private sealed record Swing(int Barra, decimal Precio, decimal Cvd);

	private sealed record Divergencia(Direccion Direccion, int BarraConfirmacion);

	private sealed record MicroDivergencia(Direccion Direccion, int BarraSwing, int BarraConfirmacion,
		decimal MechaPct, decimal VolumenMechaPct, bool AbsorcionOk)
	{
		/// <summary>Delta de la primera vela posterior al swing que superó el umbral; null mientras no la haya.</summary>
		public decimal? DeltaGiro { get; set; }
	}

	private sealed record Senal(int Barra, DateTime Hora, decimal Precio, Direccion Direccion,
		decimal Delta, decimal MechaPct, decimal VolumenMechaPct);

	private readonly record struct EstadoHud(Direccion? Macro, Direccion? Micro, decimal MechaPct, decimal VolumenMechaPct,
		bool AbsorcionOk, decimal DeltaActual, Senal? UltimaSenal, DateTime HoraUltimaVela);

	#endregion

	#region Campos

	private const int MaxSenales = 2000;
	private const int MaxNivelesMecha = 10000;

	private static readonly Color ColorCompra = Color.FromArgb(0, 255, 128);
	private static readonly Color ColorVenta = Color.FromArgb(255, 23, 68);
	private static readonly Color ColorAlcistaHud = Color.FromArgb(0, 230, 118);
	private static readonly Color ColorInsuficiente = Color.FromArgb(255, 152, 0);

	// OnCalculate y OnRender corren en hilos distintos.
	private readonly object _sync = new();

	private readonly CalculadoraCvd _cvd = new();
	private readonly List<Senal> _senales = new();

	private int _ultimaBarra = -1;
	private Swing? _macroMinimo;
	private Swing? _macroMaximo;
	private Swing? _microMinimo;
	private Swing? _microMaximo;
	private Divergencia? _macro;
	private MicroDivergencia? _micro;
	private Senal? _ultimaSenal;
	private decimal _deltaActual;
	private DateTime _horaUltimaVela;

	private bool _reiniciarPorSesion = true;
	private int _fuerzaMacro = 12;
	private int _fuerzaMicro = 3;
	private ModoVigencia _vigencia = ModoVigencia.PorBarras;
	private int _vigenciaMacro = 60;
	private int _vigenciaMicro = 7;
	private decimal _umbralDelta = 400m;
	private decimal _minMechaPct = 40m;
	private decimal _minVolumenMechaPct = 30m;

	private readonly RenderFont _fuenteVela = new("Arial", 10f, FontStyle.Bold);
	private readonly RenderFont _fuenteMetricas = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteTitulo = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteEtiqueta = new("Arial", 8.5f, FontStyle.Regular);
	private readonly RenderFont _fuenteValor = new("Arial", 8.5f, FontStyle.Bold);
	private readonly RenderFont _fuenteGatillo = new("Arial", 9.5f, FontStyle.Bold);

	private readonly RenderStringFormat _alinearIzquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderStringFormat _alinearCentro = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

	private readonly RenderPen _bordePanel = new(Color.FromArgb(65, 75, 90));
	private readonly RenderPen _bordeCompra = new(ColorCompra);
	private readonly RenderPen _bordeVenta = new(ColorVenta);
	private readonly RenderPen _bordeEspera = new(Color.Gold);

	#endregion

	#region Configuración

	[Display(Name = "Color CVD alcista", GroupName = "1. Velas CVD", Order = 10)]
	public MediaColor ColorCvdAlcista { get; set; } = MediaColor.FromArgb(255, 0, 230, 118);

	[Display(Name = "Color CVD bajista", GroupName = "1. Velas CVD", Order = 20)]
	public MediaColor ColorCvdBajista { get; set; } = MediaColor.FromArgb(255, 255, 23, 68);

	[Display(Name = "Mostrar CVD en cada vela (Arial 10)", GroupName = "1. Velas CVD", Order = 30,
		Description = "Escribe el CVD de la sesión en el centro de cada vela. Solo aparece cuando las velas son lo bastante anchas para que los textos no se superpongan: acercar el gráfico.")]
	public bool MostrarTextoEnVela { get; set; } = true;

	[Display(Name = "Reiniciar CVD en cada sesión", GroupName = "1. Velas CVD", Order = 40,
		Description = "Activado: el CVD vuelve a cero al empezar cada sesión del instrumento. Desactivado: acumula todo el histórico cargado.")]
	public bool ReiniciarPorSesion
	{
		get => _reiniciarPorSesion;
		set
		{
			_reiniciarPorSesion = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuerza macro-divergencia (barras)", GroupName = "2. Divergencias", Order = 50,
		Description = "Velas a cada lado necesarias para confirmar un swing macro. Con 12 en un gráfico de 1 minuto, cada swing abarca unos 25 minutos.")]
	[Range(1, 200)]
	public int FuerzaMacro
	{
		get => _fuerzaMacro;
		set
		{
			_fuerzaMacro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Fuerza micro-divergencia (barras)", GroupName = "2. Divergencias", Order = 60,
		Description = "Velas a cada lado necesarias para confirmar un swing micro (el fractal del gatillo).")]
	[Range(1, 50)]
	public int FuerzaMicro
	{
		get => _fuerzaMicro;
		set
		{
			_fuerzaMicro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia de las divergencias", GroupName = "2. Divergencias", Order = 70,
		Description = "Por barras: cada divergencia vence tras las barras indicadas abajo, contadas desde que se confirma. "
			+ "Hasta divergencia opuesta: sigue activa hasta que aparece una en sentido contrario.")]
	public ModoVigencia Vigencia
	{
		get => _vigencia;
		set
		{
			_vigencia = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia macro (barras)", GroupName = "2. Divergencias", Order = 80,
		Description = "Solo en modo Por barras. 60 = una hora en un gráfico de 1 minuto.")]
	[Range(1, 100000)]
	public int VigenciaMacro
	{
		get => _vigenciaMacro;
		set
		{
			_vigenciaMacro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Vigencia micro (barras)", GroupName = "2. Divergencias", Order = 90,
		Description = "Solo en modo Por barras. Barras disponibles para que llegue el giro de delta tras confirmarse la micro-divergencia.")]
	[Range(1, 10000)]
	public int VigenciaMicro
	{
		get => _vigenciaMicro;
		set
		{
			_vigenciaMicro = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Umbral delta gatillo (contratos)", GroupName = "3. Filtros de gatillo", Order = 100,
		Description = "Delta mínimo, en valor absoluto, de una vela posterior al swing para validar el giro agresivo (positivo para compra, negativo para venta).")]
	[Range(0, 10000000)]
	public decimal UmbralDelta
	{
		get => _umbralDelta;
		set
		{
			_umbralDelta = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mínimo mecha (% del rango)", GroupName = "3. Filtros de gatillo", Order = 110,
		Description = "Mecha del lado del swing (inferior en compras, superior en ventas) respecto al rango total de la vela del swing.")]
	[Range(0, 100)]
	public decimal MinMechaPct
	{
		get => _minMechaPct;
		set
		{
			_minMechaPct = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Mínimo volumen en la mecha (% de la vela)", GroupName = "3. Filtros de gatillo", Order = 120,
		Description = "Volumen negociado en los precios de esa mecha respecto al volumen total de la vela del swing (datos por nivel de precio de ATAS).")]
	[Range(0, 100)]
	public decimal MinVolumenMechaPct
	{
		get => _minVolumenMechaPct;
		set
		{
			_minVolumenMechaPct = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Posición X", GroupName = "4. Panel HUD", Order = 130)]
	[Range(0, 10000)]
	public int HudX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "4. Panel HUD", Order = 140,
		Description = "Por defecto queda debajo del panel del Indicador de Tendencia Hourly.")]
	[Range(0, 10000)]
	public int HudY { get; set; } = 230;

	#endregion

	#region ctor

	public InstitutionalCvdTriggerEngine()
		: base(true)
	{
		// Solo dibuja (texto, banderas y panel): se oculta la serie por defecto para que no deforme la escala.
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

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

			// Las velas anteriores a 'bar' ya cerraron: se recalcula su CVD con los valores finales y se
			// procesan (swings, divergencias, gatillo) una sola vez cada una.
			for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
			{
				CalcularCvd(i);
				ProcesarVelaCerrada(i);
			}

			// Vela en curso: solo CVD y delta para el panel; se evaluará cuando cierre.
			var vela = GetCandle(bar);
			CalcularCvd(bar);
			_ultimaBarra = bar;
			_deltaActual = vela.Delta;
			_horaUltimaVela = HoraLocal(vela);
		}
	}

	private void Reiniciar()
	{
		_cvd.Reiniciar();
		_senales.Clear();
		_ultimaBarra = -1;
		_macroMinimo = _macroMaximo = _microMinimo = _microMaximo = null;
		_macro = null;
		_micro = null;
		_ultimaSenal = null;
		_deltaActual = 0;
		_horaUltimaVela = default;
	}

	private void CalcularCvd(int bar) =>
		_cvd.Calcular(bar, GetCandle(bar), _reiniciarPorSesion && bar > 0 && IsNewSession(bar));

	private void ProcesarVelaCerrada(int c)
	{
		DetectarSwings(c, _fuerzaMacro, true);
		DetectarSwings(c, _fuerzaMicro, false);

		if (_vigencia == ModoVigencia.PorBarras)
		{
			if (_macro is not null && c - _macro.BarraConfirmacion > _vigenciaMacro)
				_macro = null;

			if (_micro is not null && c - _micro.BarraConfirmacion > _vigenciaMicro)
				_micro = null;
		}

		// Filtro 4: giro de delta en una vela posterior al swing de la micro-divergencia.
		if (_micro is not null && c > _micro.BarraConfirmacion)
			RegistrarGiro(_micro, c);

		EvaluarGatillo(c);
	}

	/// <summary>Un swing en la vela c - fuerza queda confirmado cuando cierran 'fuerza' velas a cada lado.</summary>
	private void DetectarSwings(int c, int fuerza, bool macro)
	{
		var e = c - fuerza;

		if (fuerza < 1 || e - fuerza < 0)
			return;

		var candidata = GetCandle(e);
		var esMaximo = true;
		var esMinimo = true;

		for (var j = e - fuerza; j <= c && (esMaximo || esMinimo); j++)
		{
			if (j == e)
				continue;

			var vela = GetCandle(j);
			esMaximo &= vela.High < candidata.High;
			esMinimo &= vela.Low > candidata.Low;
		}

		if (esMinimo)
			NuevoSwing(new Swing(e, candidata.Low, _cvd.Cierre(e)), false, macro, c);

		if (esMaximo)
			NuevoSwing(new Swing(e, candidata.High, _cvd.Cierre(e)), true, macro, c);
	}

	private void NuevoSwing(Swing swing, bool esMaximo, bool macro, int c)
	{
		Swing? anterior;

		if (macro)
		{
			anterior = esMaximo ? _macroMaximo : _macroMinimo;

			if (esMaximo)
				_macroMaximo = swing;
			else
				_macroMinimo = swing;
		}
		else
		{
			anterior = esMaximo ? _microMaximo : _microMinimo;

			if (esMaximo)
				_microMaximo = swing;
			else
				_microMinimo = swing;
		}

		if (anterior is null)
			return;

		// Cada mínimo se compara con el mínimo anterior y cada máximo con el máximo anterior.
		// Macro exige un precio más extremo; micro acepta también un doble suelo o techo (como el documento).
		if (esMaximo)
		{
			var precioMasAlto = macro ? swing.Precio > anterior.Precio : swing.Precio >= anterior.Precio;

			if (precioMasAlto && swing.Cvd < anterior.Cvd)
				ActivarDivergencia(Direccion.Bajista, swing, macro, c);
		}
		else
		{
			var precioMasBajo = macro ? swing.Precio < anterior.Precio : swing.Precio <= anterior.Precio;

			if (precioMasBajo && swing.Cvd > anterior.Cvd)
				ActivarDivergencia(Direccion.Alcista, swing, macro, c);
		}
	}

	private void ActivarDivergencia(Direccion direccion, Swing swing, bool macro, int c)
	{
		if (macro)
		{
			_macro = new Divergencia(direccion, c);
			return;
		}

		// Filtro 3: absorción medida en la vela extrema del swing.
		var (mechaPct, volumenPct) = MedirAbsorcion(GetCandle(swing.Barra), direccion);

		_micro = new MicroDivergencia(direccion, swing.Barra, c, mechaPct, volumenPct,
			mechaPct >= _minMechaPct && volumenPct >= _minVolumenMechaPct);

		// El giro de delta puede haber llegado ya, entre el swing y su confirmación.
		for (var k = swing.Barra + 1; k <= c && _micro.DeltaGiro is null; k++)
			RegistrarGiro(_micro, k);
	}

	private void RegistrarGiro(MicroDivergencia micro, int barra)
	{
		if (micro.DeltaGiro is not null)
			return;

		var delta = GetCandle(barra).Delta;

		if (micro.Direccion == Direccion.Alcista ? delta >= _umbralDelta : delta <= -_umbralDelta)
			micro.DeltaGiro = delta;
	}

	private void EvaluarGatillo(int c)
	{
		if (_macro is null || _micro is null || _macro.Direccion != _micro.Direccion
			|| !_micro.AbsorcionOk || _micro.DeltaGiro is null)
			return;

		var vela = GetCandle(c);
		var compra = _micro.Direccion == Direccion.Alcista;

		var senal = new Senal(c, HoraLocal(vela), compra ? vela.Low : vela.High, _micro.Direccion,
			_micro.DeltaGiro.Value, _micro.MechaPct, _micro.VolumenMechaPct);

		_senales.Add(senal);

		if (_senales.Count > MaxSenales)
			_senales.RemoveAt(0);

		_ultimaSenal = senal;

		// La micro-divergencia se consume con el disparo; la macro sigue vigente.
		_micro = null;
	}

	/// <summary>
	/// Mecha del lado del swing (inferior en un mínimo, superior en un máximo) como % del rango de la vela,
	/// y volumen negociado en los precios de esa mecha como % del volumen total de la vela.
	/// </summary>
	private (decimal MechaPct, decimal VolumenPct) MedirAbsorcion(IndicatorCandle vela, Direccion direccion)
	{
		var rango = vela.High - vela.Low;

		if (rango <= 0)
			return (0m, 0m);

		var cuerpoInferior = Math.Min(vela.Open, vela.Close);
		var cuerpoSuperior = Math.Max(vela.Open, vela.Close);
		var mecha = direccion == Direccion.Alcista ? cuerpoInferior - vela.Low : vela.High - cuerpoSuperior;
		var mechaPct = mecha / rango * 100m;

		var tick = InstrumentInfo?.TickSize ?? 0m;

		if (tick <= 0 || vela.Volume <= 0)
			return (mechaPct, 0m);

		// Volumen por nivel de precio (datos de clúster de ATAS); un nivel sin operaciones devuelve null.
		var volumen = 0m;
		var niveles = 0;

		if (direccion == Direccion.Alcista)
		{
			for (var precio = vela.Low; precio < cuerpoInferior && niveles < MaxNivelesMecha; precio += tick, niveles++)
				volumen += vela.GetPriceVolumeInfo(precio)?.Volume ?? 0m;
		}
		else
		{
			for (var precio = vela.High; precio > cuerpoSuperior && niveles < MaxNivelesMecha; precio -= tick, niveles++)
				volumen += vela.GetPriceVolumeInfo(precio)?.Volume ?? 0m;
		}

		return (mechaPct, volumen / vela.Volume * 100m);
	}

	private DateTime HoraLocal(IndicatorCandle vela) =>
		vela.Time + (InstrumentInfo?.TimeZoneOffset ?? TimeSpan.Zero);

	#endregion

	#region Dibujo

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		var primera = Math.Max(0, FirstVisibleBarNumber);
		var ultima = LastVisibleBarNumber;

		decimal[] cvdVisible;
		Senal[] senalesVisibles;
		EstadoHud estado;

		lock (_sync)
		{
			var hasta = Math.Min(ultima, _cvd.Cantidad - 1);
			cvdVisible = new decimal[Math.Max(0, hasta - primera + 1)];

			for (var i = 0; i < cvdVisible.Length; i++)
				cvdVisible[i] = _cvd.Cierre(primera + i);

			senalesVisibles = _senales.Where(s => s.Barra >= primera && s.Barra <= ultima).ToArray();

			estado = new EstadoHud(_macro?.Direccion, _micro?.Direccion, _micro?.MechaPct ?? 0m, _micro?.VolumenMechaPct ?? 0m,
				_micro?.AbsorcionOk ?? false, _deltaActual, _ultimaSenal, _horaUltimaVela);
		}

		if (MostrarTextoEnVela)
			DibujarCvdEnVelas(context, primera, cvdVisible);

		DibujarBanderas(context, senalesVisibles);
		DibujarHud(context, estado);
	}

	private void DibujarCvdEnVelas(RenderContext context, int primera, decimal[] cvd)
	{
		if (cvd.Length == 0)
			return;

		// Solo si el texto cabe en el espacio de cada vela; si no, se superpondrían.
		var espacioVela = (int)(ChartInfo.PriceChartContainer.BarSpacing + ChartInfo.PriceChartContainer.BarsWidth);
		var ancho = context.MeasureString("-888.8K", _fuenteVela).Width + 6;

		if (ancho > espacioVela)
			return;

		var alto = context.MeasureString("0", _fuenteVela).Height + 2;

		for (var i = 0; i < cvd.Length; i++)
		{
			var bar = primera + i;
			var vela = GetCandle(bar);
			var x = ChartInfo.GetXByBar(bar) + (int)((ChartInfo.PriceChartContainer.BarsWidth - ancho) / 2);
			var y = ChartInfo.GetYByPrice((vela.High + vela.Low) / 2m, false) - alto / 2;
			var caja = new Rectangle(x, y, ancho, alto);

			context.FillRectangle(Color.FromArgb(170, 18, 20, 25), caja);
			context.DrawString(FormatoCompacto(cvd[i]), _fuenteVela, ADibujo(cvd[i] >= 0 ? ColorCvdAlcista : ColorCvdBajista), caja, _alinearCentro);
		}
	}

	private void DibujarBanderas(RenderContext context, Senal[] senales)
	{
		const int altoLinea = 12;

		foreach (var senal in senales)
		{
			var compra = senal.Direccion == Direccion.Alcista;
			var color = compra ? ColorCompra : ColorVenta;
			var x = ChartInfo.GetXByBar(senal.Barra) + (int)(ChartInfo.PriceChartContainer.BarsWidth / 2);
			var y = ChartInfo.GetYByPrice(senal.Precio, false);

			// Bandera de compra debajo del mínimo de la vela; de venta, encima del máximo.
			var arriba = compra ? y + 8 : y - 30;
			DibujarBandera(context, color, x, arriba);

			var lineas = new[]
			{
				compra ? "GATILLO LONG" : "GATILLO SHORT",
				"Δ " + FormatoCompacto(senal.Delta),
				$"Abs {senal.MechaPct:F0}% · Vol {senal.VolumenMechaPct:F0}%"
			};

			var inicio = compra ? arriba + 26 : arriba - 4 - lineas.Length * altoLinea;

			for (var i = 0; i < lineas.Length; i++)
				context.DrawString(lineas[i], _fuenteMetricas, color, new Rectangle(x - 60, inicio + i * altoLinea, 120, altoLinea), _alinearCentro);
		}
	}

	/// <summary>Bandera dibujada con formas (mástil y paño), igual con cualquier fuente o tema.</summary>
	private static void DibujarBandera(RenderContext context, Color color, int x, int arriba)
	{
		context.FillRectangle(color, new Rectangle(x - 1, arriba, 2, 22));
		context.FillPolygon(color, new[] { new Point(x + 1, arriba), new Point(x + 14, arriba + 5), new Point(x + 1, arriba + 10) });
	}

	private void DibujarHud(RenderContext context, EstadoHud estado)
	{
		const int ancho = 330;
		const int alto = 166;

		var x = HudX;
		var y = HudY;

		context.FillRectangle(Color.FromArgb(240, 16, 18, 24), new Rectangle(x, y, ancho, alto));
		context.DrawRectangle(_bordePanel, new Rectangle(x, y, ancho, alto));
		context.FillRectangle(Color.FromArgb(245, 26, 32, 42), new Rectangle(x, y, ancho, 25));
		context.DrawString("CVD INSTITUTIONAL TRIGGER MATRIX", _fuenteTitulo, Color.FromArgb(210, 220, 235),
			new Rectangle(x + 10, y, ancho - 20, 25), _alinearIzquierda);

		var (macroTexto, macroColor) = estado.Macro switch
		{
			Direccion.Alcista => ("ALCISTA (Absorción)", ColorAlcistaHud),
			Direccion.Bajista => ("BAJISTA (Absorción)", ColorVenta),
			_ => ("NEUTRAL", Color.Gray)
		};

		var (microTexto, microColor) = estado.Micro switch
		{
			Direccion.Alcista => ("ALCISTA ACTIVA", ColorAlcistaHud),
			Direccion.Bajista => ("BAJISTA ACTIVA", ColorVenta),
			_ => ("SIN AGOTAMIENTO", Color.Gray)
		};

		var (absorcionTexto, absorcionColor) = estado.Micro is null
			? ("—", Color.Gray)
			: ($"Mecha {estado.MechaPct:F0}% · Vol {estado.VolumenMechaPct:F0}%", estado.AbsorcionOk ? ColorAlcistaHud : ColorInsuficiente);

		var deltaColor = estado.DeltaActual >= _umbralDelta
			? ColorAlcistaHud
			: estado.DeltaActual <= -_umbralDelta ? ColorVenta : Color.WhiteSmoke;

		DibujarFila(context, x, y + 32, "1. Macro CVD (5M+):", macroTexto, macroColor);
		DibujarFila(context, x, y + 54, "2. Micro-divergencia:", microTexto, microColor);
		DibujarFila(context, x, y + 76, "3. Absorción en mecha:", absorcionTexto, absorcionColor);
		DibujarFila(context, x, y + 98, "4. Delta actual / umbral:",
			$"{FormatoCompacto(estado.DeltaActual)} / ±{FormatoCompacto(_umbralDelta, false)}", deltaColor);

		// Estado del gatillo: último disparo o espera.
		var banner = new Rectangle(x + 8, y + 126, ancho - 16, 30);
		context.FillRectangle(Color.FromArgb(180, 25, 30, 38), banner);

		if (estado.UltimaSenal is { } senal)
		{
			var compra = senal.Direccion == Direccion.Alcista;
			var color = compra ? ColorCompra : ColorVenta;
			var hora = FormatearHora(senal.Hora, estado.HoraUltimaVela);

			context.DrawRectangle(compra ? _bordeCompra : _bordeVenta, banner);
			DibujarBandera(context, color, banner.X + 12, banner.Y + 4);
			context.DrawString(compra ? $"DISPARO COMPRA [LONG] [{hora}]" : $"DISPARO VENTA [SHORT] [{hora}]",
				_fuenteGatillo, color, banner, _alinearCentro);
		}
		else
		{
			context.DrawRectangle(_bordeEspera, banner);
			context.DrawString("ESPERANDO GATILLO...", _fuenteGatillo, Color.Gold, banner, _alinearCentro);
		}
	}

	private void DibujarFila(RenderContext context, int x, int y, string etiqueta, string valor, Color color)
	{
		context.DrawString(etiqueta, _fuenteEtiqueta, Color.Silver, new Rectangle(x + 10, y, 150, 20), _alinearIzquierda);
		context.DrawString(valor, _fuenteValor, color, new Rectangle(x + 160, y, 165, 20), _alinearIzquierda);
	}

	private static string FormatearHora(DateTime hora, DateTime horaUltimaVela) =>
		hora.Date == horaUltimaVela.Date ? hora.ToString("HH:mm") : hora.ToString("dd/MM HH:mm");

	private static string FormatoCompacto(decimal valor, bool conSigno = true)
	{
		var signo = conSigno && valor > 0 ? "+" : "";

		if (Math.Abs(valor) >= 1000000)
			return $"{signo}{valor / 1000000m:F1}M";

		if (Math.Abs(valor) >= 1000)
			return $"{signo}{valor / 1000m:F1}K";

		return $"{signo}{valor:F0}";
	}

	private static Color ADibujo(MediaColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

	#endregion
}
