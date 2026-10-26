namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Linq;

using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Motor del Institutional CVD Trigger &amp; Absorption Engine: gatillo sobre el CVD (delta acumulado por sesión) con
/// cuatro filtros en cascada:
/// 1. macro-divergencia CVD entre swings amplios;
/// 2. micro-divergencia CVD entre swings cortos;
/// 3. absorción en la vela extrema del swing de la micro-divergencia: tamaño de la mecha y volumen negociado en ella;
/// 4. giro de delta agresivo más allá del umbral en una vela posterior a ese swing.
/// Con los cuatro en la misma dirección dibuja una bandera verde (compra) o roja (venta) en la vela que completa la
/// confirmación. Todo se evalúa con velas cerradas, así que las banderas no se repintan.
/// </summary>
internal sealed class MotorCvd
{
	#region Tipos

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

	public const int AnchoMinimo = 330;
	public const int AltoTitulo = 25;
	public const int AltoPanel = 166;

	private const int MaxSenales = 2000;
	private const int MaxNivelesMecha = 10000;

	private static readonly Color ColorCompra = Color.FromArgb(0, 255, 128);
	private static readonly Color ColorVenta = Color.FromArgb(255, 23, 68);
	private static readonly Color ColorAlcistaHud = Color.FromArgb(0, 230, 118);
	private static readonly Color ColorInsuficiente = Color.FromArgb(255, 152, 0);

	private readonly IGrafico _grafico;

	// El cálculo y el dibujo corren en hilos distintos.
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

	public MotorCvd(IGrafico grafico) => _grafico = grafico;

	#region Configuración

	public MediaColor ColorCvdAlcista { get; set; } = MediaColor.FromArgb(255, 0, 230, 118);

	public MediaColor ColorCvdBajista { get; set; } = MediaColor.FromArgb(255, 255, 23, 68);

	public bool MostrarTextoEnVela { get; set; } = true;

	public bool ReiniciarPorSesion { get; set; } = true;

	public int FuerzaMacro { get; set; } = 12;

	public int FuerzaMicro { get; set; } = 3;

	public ModoVigencia Vigencia { get; set; } = ModoVigencia.PorBarras;

	public int VigenciaMacro { get; set; } = 60;

	public int VigenciaMicro { get; set; } = 7;

	public decimal UmbralDelta { get; set; } = 400m;

	public decimal MinMechaPct { get; set; } = 40m;

	public decimal MinVolumenMechaPct { get; set; } = 30m;

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

			// Las velas anteriores a 'bar' ya cerraron: se recalcula su CVD con los valores finales y se
			// procesan (swings, divergencias, gatillo) una sola vez cada una.
			for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
			{
				CalcularCvd(i);
				ProcesarVelaCerrada(i);
			}

			// Vela en curso: solo CVD y delta para el panel; se evaluará cuando cierre.
			var vela = _grafico.Vela(bar);
			CalcularCvd(bar);
			_ultimaBarra = bar;
			_deltaActual = vela.Delta;
			_horaUltimaVela = vela.Time + _grafico.ZonaHoraria;
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
		_cvd.Calcular(bar, _grafico.Vela(bar), ReiniciarPorSesion && bar > 0 && _grafico.EsNuevaSesion(bar));

	private void ProcesarVelaCerrada(int c)
	{
		DetectarSwings(c, FuerzaMacro, true);
		DetectarSwings(c, FuerzaMicro, false);

		if (Vigencia == ModoVigencia.PorBarras)
		{
			if (_macro is not null && c - _macro.BarraConfirmacion > VigenciaMacro)
				_macro = null;

			if (_micro is not null && c - _micro.BarraConfirmacion > VigenciaMicro)
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

		var candidata = _grafico.Vela(e);
		var esMaximo = true;
		var esMinimo = true;

		for (var j = e - fuerza; j <= c && (esMaximo || esMinimo); j++)
		{
			if (j == e)
				continue;

			var vela = _grafico.Vela(j);
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
		var (mechaPct, volumenPct) = MedirAbsorcion(_grafico.Vela(swing.Barra), direccion);

		_micro = new MicroDivergencia(direccion, swing.Barra, c, mechaPct, volumenPct,
			mechaPct >= MinMechaPct && volumenPct >= MinVolumenMechaPct);

		// El giro de delta puede haber llegado ya, entre el swing y su confirmación.
		for (var k = swing.Barra + 1; k <= c && _micro.DeltaGiro is null; k++)
			RegistrarGiro(_micro, k);
	}

	private void RegistrarGiro(MicroDivergencia micro, int barra)
	{
		if (micro.DeltaGiro is not null)
			return;

		var delta = _grafico.Vela(barra).Delta;

		if (micro.Direccion == Direccion.Alcista ? delta >= UmbralDelta : delta <= -UmbralDelta)
			micro.DeltaGiro = delta;
	}

	private void EvaluarGatillo(int c)
	{
		if (_macro is null || _micro is null || _macro.Direccion != _micro.Direccion
			|| !_micro.AbsorcionOk || _micro.DeltaGiro is null)
			return;

		var vela = _grafico.Vela(c);
		var compra = _micro.Direccion == Direccion.Alcista;

		var senal = new Senal(c, vela.Time + _grafico.ZonaHoraria, compra ? vela.Low : vela.High, _micro.Direccion,
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

		var tick = _grafico.TamanoTick;

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

	#endregion

	#region Dibujo

	/// <summary>Dibujo sobre el gráfico: CVD en cada vela y banderas de gatillo.</summary>
	public void DibujarEnGrafico(RenderContext context)
	{
		var primera = Math.Max(0, _grafico.PrimeraVisible);
		var ultima = _grafico.UltimaVisible;

		decimal[] cvdVisible;
		Senal[] senalesVisibles;

		lock (_sync)
		{
			var hasta = Math.Min(ultima, _cvd.Cantidad - 1);
			cvdVisible = new decimal[Math.Max(0, hasta - primera + 1)];

			for (var i = 0; i < cvdVisible.Length; i++)
				cvdVisible[i] = _cvd.Cierre(primera + i);

			senalesVisibles = _senales.Where(s => s.Barra >= primera && s.Barra <= ultima).ToArray();
		}

		if (MostrarTextoEnVela)
			DibujarCvdEnVelas(context, primera, cvdVisible);

		DibujarBanderas(context, senalesVisibles);
	}

	private void DibujarCvdEnVelas(RenderContext context, int primera, decimal[] cvd)
	{
		if (cvd.Length == 0)
			return;

		// Solo si el texto cabe en el espacio de cada vela; si no, se superpondrían.
		var ancho = context.MeasureString("-888.8K", _fuenteVela).Width + 6;

		if (ancho > _grafico.EspacioVela)
			return;

		var alto = context.MeasureString("0", _fuenteVela).Height + 2;

		for (var i = 0; i < cvd.Length; i++)
		{
			var bar = primera + i;
			var vela = _grafico.Vela(bar);
			var x = _grafico.XVela(bar) + (_grafico.AnchoVela - ancho) / 2;
			var y = _grafico.YPrecio((vela.High + vela.Low) / 2m) - alto / 2;
			var caja = new Rectangle(x, y, ancho, alto);

			context.FillRectangle(Color.FromArgb(170, 18, 20, 25), caja);
			context.DrawString(Formato.Compacto(cvd[i]), _fuenteVela, Formato.ADibujo(cvd[i] >= 0 ? ColorCvdAlcista : ColorCvdBajista),
				caja, _alinearCentro);
		}
	}

	private void DibujarBanderas(RenderContext context, Senal[] senales)
	{
		const int altoLinea = 12;

		foreach (var senal in senales)
		{
			var compra = senal.Direccion == Direccion.Alcista;
			var color = compra ? ColorCompra : ColorVenta;
			var x = _grafico.XVela(senal.Barra) + _grafico.AnchoVela / 2;
			var y = _grafico.YPrecio(senal.Precio);

			// Bandera de compra debajo del mínimo de la vela; de venta, encima del máximo.
			var arriba = compra ? y + 8 : y - 30;
			DibujarBandera(context, color, x, arriba);

			var lineas = new[]
			{
				compra ? "GATILLO LONG" : "GATILLO SHORT",
				"Δ " + Formato.Compacto(senal.Delta),
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

	/// <summary>Panel de los cuatro filtros y del último disparo.</summary>
	public void DibujarPanel(RenderContext context, int x, int y, int ancho)
	{
		EstadoHud estado;

		lock (_sync)
		{
			estado = new EstadoHud(_macro?.Direccion, _micro?.Direccion, _micro?.MechaPct ?? 0m, _micro?.VolumenMechaPct ?? 0m,
				_micro?.AbsorcionOk ?? false, _deltaActual, _ultimaSenal, _horaUltimaVela);
		}

		context.FillRectangle(Color.FromArgb(240, 16, 18, 24), new Rectangle(x, y, ancho, AltoPanel));
		context.DrawRectangle(_bordePanel, new Rectangle(x, y, ancho, AltoPanel));
		context.FillRectangle(Color.FromArgb(245, 26, 32, 42), new Rectangle(x, y, ancho, AltoTitulo));
		context.DrawString("CVD INSTITUTIONAL TRIGGER MATRIX", _fuenteTitulo, Color.FromArgb(210, 220, 235),
			new Rectangle(x + 10, y, ancho - 20, AltoTitulo), _alinearIzquierda);

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

		var deltaColor = estado.DeltaActual >= UmbralDelta
			? ColorAlcistaHud
			: estado.DeltaActual <= -UmbralDelta ? ColorVenta : Color.WhiteSmoke;

		DibujarFila(context, x, y + 32, ancho, "1. Macro CVD (5M+):", macroTexto, macroColor);
		DibujarFila(context, x, y + 54, ancho, "2. Micro-divergencia:", microTexto, microColor);
		DibujarFila(context, x, y + 76, ancho, "3. Absorción en mecha:", absorcionTexto, absorcionColor);
		DibujarFila(context, x, y + 98, ancho, "4. Delta actual / umbral:",
			$"{Formato.Compacto(estado.DeltaActual)} / ±{Formato.Compacto(UmbralDelta, false)}", deltaColor);

		// Estado del gatillo: último disparo o espera.
		var banner = new Rectangle(x + 8, y + 126, ancho - 16, 30);
		context.FillRectangle(Color.FromArgb(180, 25, 30, 38), banner);

		if (estado.UltimaSenal is { } senal)
		{
			var compra = senal.Direccion == Direccion.Alcista;
			var color = compra ? ColorCompra : ColorVenta;
			var hora = Formato.Hora(senal.Hora, estado.HoraUltimaVela);

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

	private void DibujarFila(RenderContext context, int x, int y, int ancho, string etiqueta, string valor, Color color)
	{
		context.DrawString(etiqueta, _fuenteEtiqueta, Color.Silver, new Rectangle(x + 10, y, 150, 20), _alinearIzquierda);
		context.DrawString(valor, _fuenteValor, color, new Rectangle(x + 160, y, ancho - 165, 20), _alinearIzquierda);
	}

	#endregion
}
