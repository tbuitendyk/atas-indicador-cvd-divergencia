namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators;
using ATAS.Indicators.Drawing;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

// Alias explícitos: los tipos de dibujo vienen de System.Drawing y los colores configurables
// de WPF (es el tipo que usa el panel de configuración de ATAS clásico). Al declararlos dentro
// del namespace tienen prioridad sobre cualquier tipo homónimo de los namespaces de ATAS.
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using MediaColor = System.Windows.Media.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Panel con la tendencia de 1 hora, 15 minutos y 5 minutos, calculada a partir de las velas
/// del gráfico actual (se recomienda un gráfico de 1 minuto o menor).
/// Para cada marco temporal se agrupan las velas del gráfico en velas sintéticas y se compara
/// el cierre con la vela sintética anterior del mismo marco:
/// cierre &gt; máximo anterior = alcista (▲), cierre &lt; mínimo anterior = bajista (▼),
/// cierre dentro del rango anterior = en rango (-).
/// </summary>
[DisplayName("Indicador de Tendencia Hourly")]
[Description("Muestra la tendencia de 1 hora, 15 minutos y 5 minutos en un panel sobre el gráfico: ▲ alcista, ▼ bajista, (-) en rango.")]
public class IndicadorTendenciaHourly : Indicator
{
	#region Tipos internos

	private enum EstadoTendencia
	{
		Rango,
		Alcista,
		Bajista
	}

	private sealed class VelaSintetica
	{
		public decimal Maximo;
		public decimal Minimo;
		public decimal Cierre;
	}

	private readonly record struct Fila(string Etiqueta, EstadoTendencia Estado, DateTime? Desde);

	/// <summary>
	/// Agrupa las velas del gráfico en velas sintéticas de un marco temporal y mantiene
	/// su estado de tendencia.
	/// </summary>
	private sealed class MarcoTemporal
	{
		private readonly long _periodoTicks;

		// Vela sintética en formación: inicio del período y extremos de las velas del gráfico
		// que ya cerraron dentro de él. La vela del gráfico en curso se evalúa aparte, en cada tick.
		private DateTime? _inicioEnFormacion;
		private VelaSintetica? _cerradasEnFormacion;

		// Última vela sintética completa: es la referencia contra la que se evalúa la tendencia.
		private VelaSintetica? _anterior;

		public MarcoTemporal(string etiqueta, int minutos)
		{
			Etiqueta = etiqueta;
			_periodoTicks = TimeSpan.FromMinutes(minutos).Ticks;
		}

		public string Etiqueta { get; }

		public EstadoTendencia Estado { get; private set; }

		/// <summary>Hora del gráfico en que empezó el estado actual; null mientras no hay datos suficientes.</summary>
		public DateTime? Desde { get; private set; }

		public void Reiniciar()
		{
			_inicioEnFormacion = null;
			_cerradasEnFormacion = null;
			_anterior = null;
			Estado = EstadoTendencia.Rango;
			Desde = null;
		}

		/// <summary>Incorpora los valores finales de una vela del gráfico que ya cerró.</summary>
		public void AgregarVelaCerrada(IndicatorCandle vela)
		{
			if (_cerradasEnFormacion is null)
			{
				_cerradasEnFormacion = new VelaSintetica { Maximo = vela.High, Minimo = vela.Low, Cierre = vela.Close };
				return;
			}

			_cerradasEnFormacion.Maximo = Math.Max(_cerradasEnFormacion.Maximo, vela.High);
			_cerradasEnFormacion.Minimo = Math.Min(_cerradasEnFormacion.Minimo, vela.Low);
			_cerradasEnFormacion.Cierre = vela.Close;
		}

		/// <summary>Procesa la vela en curso del gráfico; en tiempo real se llama en cada tick.</summary>
		public void Procesar(IndicatorCandle vela, DateTime horaLocal, bool confirmarAlCierre)
		{
			var inicio = new DateTime(horaLocal.Ticks - horaLocal.Ticks % _periodoTicks);

			if (_inicioEnFormacion is null)
			{
				_inicioEnFormacion = inicio;
			}
			else if (inicio > _inicioEnFormacion.Value)
			{
				// Empezó un período nuevo: la vela sintética en formación queda completa.
				if (_cerradasEnFormacion is not null)
				{
					if (confirmarAlCierre && _anterior is not null)
						CambiarEstado(Evaluar(_cerradasEnFormacion.Cierre, _anterior), inicio);

					_anterior = _cerradasEnFormacion;
				}

				_inicioEnFormacion = inicio;
				_cerradasEnFormacion = null;
			}

			if (!confirmarAlCierre && _anterior is not null)
				CambiarEstado(Evaluar(vela.Close, _anterior), horaLocal);
		}

		private static EstadoTendencia Evaluar(decimal cierre, VelaSintetica anterior)
		{
			if (cierre > anterior.Maximo)
				return EstadoTendencia.Alcista;

			if (cierre < anterior.Minimo)
				return EstadoTendencia.Bajista;

			return EstadoTendencia.Rango;
		}

		private void CambiarEstado(EstadoTendencia nuevo, DateTime cuando)
		{
			if (nuevo == Estado && Desde is not null)
				return;

			Estado = nuevo;
			Desde = cuando;
		}
	}

	#endregion

	#region Campos

	// OnCalculate y OnRender corren en hilos distintos.
	private readonly object _sync = new();

	private readonly MarcoTemporal[] _marcos =
	{
		new("1 Hora", 60),
		new("15 Min", 15),
		new("5 Min", 5)
	};

	private int _ultimaBarra = -1;
	private DateTime _horaUltimaVela;
	private long _periodoGraficoTicks;
	private bool _confirmarAlCierre;

	private readonly RenderFont _fuenteTitulo = new("Arial", 8.5f, FontStyle.Bold);
	private readonly RenderFont _fuenteEtiqueta = new("Arial", 9f, FontStyle.Bold);
	private readonly RenderFont _fuenteRango = new("Arial", 10f, FontStyle.Bold);
	private readonly RenderFont _fuenteDetalle = new("Arial", 7.5f, FontStyle.Regular);

	private readonly RenderStringFormat _alinearIzquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderStringFormat _alinearCentro = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

	private readonly RenderPen _bordePanel = new(Color.FromArgb(70, 80, 95));
	private readonly RenderPen _bordePastilla = new(Color.FromArgb(170, 175, 185));

	#endregion

	#region Configuración

	[Display(Name = "Confirmar al cierre", GroupName = "Cálculo", Order = 10,
		Description = "Desactivado: compara el precio actual con la vela anterior de cada marco temporal y cambia en tiempo real. "
			+ "Activado: evalúa solo al cerrar cada vela de 1H/15M/5M y mantiene el estado durante todo el período siguiente.")]
	public bool ConfirmarAlCierre
	{
		get => _confirmarAlCierre;
		set
		{
			_confirmarAlCierre = value;
			RecalculateValues();
		}
	}

	[Display(Name = "Posición X", GroupName = "Ubicación del panel", Order = 20)]
	[Range(0, 10000)]
	public int PanelX { get; set; } = 25;

	[Display(Name = "Posición Y", GroupName = "Ubicación del panel", Order = 30)]
	[Range(0, 10000)]
	public int PanelY { get; set; } = 60;

	[Display(Name = "Color alcista (▲)", GroupName = "Colores", Order = 40)]
	public MediaColor ColorAlcista { get; set; } = MediaColor.FromArgb(255, 0, 230, 118);

	[Display(Name = "Color bajista (▼)", GroupName = "Colores", Order = 50)]
	public MediaColor ColorBajista { get; set; } = MediaColor.FromArgb(255, 255, 23, 68);

	[Display(Name = "Color rango (-)", GroupName = "Colores", Order = 60)]
	public MediaColor ColorRango { get; set; } = MediaColor.FromArgb(255, 0, 0, 0);

	[Display(Name = "Fondo de la pastilla", GroupName = "Colores", Order = 70)]
	public MediaColor ColorFondoPastilla { get; set; } = MediaColor.FromArgb(255, 235, 238, 242);

	#endregion

	#region ctor

	public IndicadorTendenciaHourly()
		: base(true)
	{
		// El indicador solo dibuja el panel: se oculta la serie por defecto para que no
		// trace una línea en 0 que deforme la escala de precios.
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

			// Al pasar a una vela nueva, las anteriores ya cerraron: se incorporan con sus
			// valores finales (releídos aquí, así no se pierde el último tick de cada vela).
			while (_ultimaBarra < bar)
			{
				if (_ultimaBarra >= 0)
					CerrarVela(_ultimaBarra);

				_ultimaBarra++;

				if (_ultimaBarra < bar)
					ProcesarVela(_ultimaBarra);
			}

			ProcesarVela(bar);
		}
	}

	private void Reiniciar()
	{
		foreach (var marco in _marcos)
			marco.Reiniciar();

		_ultimaBarra = -1;
		_horaUltimaVela = default;
		_periodoGraficoTicks = 0;
	}

	private void CerrarVela(int indice)
	{
		var vela = GetCandle(indice);

		foreach (var marco in _marcos)
			marco.AgregarVelaCerrada(vela);

		if (indice == 0)
			return;

		// La menor separación entre velas consecutivas es el período del gráfico.
		var separacion = (vela.Time - GetCandle(indice - 1).Time).Ticks;

		if (separacion > 0 && (_periodoGraficoTicks == 0 || separacion < _periodoGraficoTicks))
			_periodoGraficoTicks = separacion;
	}

	private void ProcesarVela(int indice)
	{
		var vela = GetCandle(indice);

		// Las velas de ATAS están en UTC; se pasan a la hora del gráfico.
		var horaLocal = vela.Time + (InstrumentInfo?.TimeZoneOffset ?? TimeSpan.Zero);
		_horaUltimaVela = horaLocal;

		foreach (var marco in _marcos)
			marco.Procesar(vela, horaLocal, _confirmarAlCierre);
	}

	/// <summary>
	/// true si el gráfico es de tiempo y su período no divide exactamente 5 minutos
	/// (p. ej. 3, 15 o 60 minutos): las filas de 5M/15M no serían exactas.
	/// Los gráficos de ticks, volumen o rango se aceptan como aproximación.
	/// </summary>
	private bool GraficoNoCompatible()
	{
		var tipo = ChartInfo?.ChartType;

		if (tipo != "TimeFrame" && tipo != "Seconds")
			return false;

		return _periodoGraficoTicks > 0 && TimeSpan.FromMinutes(5).Ticks % _periodoGraficoTicks != 0;
	}

	#endregion

	#region Dibujo

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		Fila[] filas;
		DateTime horaUltimaVela;
		bool advertencia;

		lock (_sync)
		{
			filas = Array.ConvertAll(_marcos, m => new Fila(m.Etiqueta, m.Estado, m.Desde));
			horaUltimaVela = _horaUltimaVela;
			advertencia = GraficoNoCompatible();
		}

		const int ancho = 240;
		const int altoTitulo = 26;
		const int altoFila = 34;

		var x = PanelX;
		var y = PanelY;
		var alto = altoTitulo + 8 + filas.Length * altoFila + (advertencia ? 20 : 0);

		var panel = new Rectangle(x, y, ancho, alto);
		context.FillRectangle(Color.FromArgb(230, 22, 25, 30), panel);
		context.DrawRectangle(_bordePanel, panel);

		context.FillRectangle(Color.FromArgb(240, 32, 36, 44), new Rectangle(x, y, ancho, altoTitulo));
		context.DrawString("TENDENCIA MULTITEMPORAL", _fuenteTitulo, Color.FromArgb(210, 215, 225),
			new Rectangle(x + 12, y, ancho - 24, altoTitulo), _alinearIzquierda);

		for (var i = 0; i < filas.Length; i++)
			DibujarFila(context, filas[i], x, y + altoTitulo + 8 + i * altoFila, horaUltimaVela);

		if (advertencia)
		{
			context.DrawString("Aviso: usar un gráfico de 5 min o menor", _fuenteDetalle, Color.FromArgb(255, 193, 7),
				new Rectangle(x + 12, y + alto - 22, ancho - 24, 18), _alinearIzquierda);
		}
	}

	private void DibujarFila(RenderContext context, Fila fila, int x, int y, DateTime horaUltimaVela)
	{
		const int altoContenido = 24;

		context.DrawString(fila.Etiqueta, _fuenteEtiqueta, Color.WhiteSmoke,
			new Rectangle(x + 12, y, 60, altoContenido), _alinearIzquierda);

		// Pastilla de fondo claro: da contraste a los tres símbolos, incluido el (-) negro.
		var pastilla = new Rectangle(x + 76, y + 1, 32, 22);
		context.FillRectangle(ADibujo(ColorFondoPastilla), pastilla);
		context.DrawRectangle(_bordePastilla, pastilla);

		var cx = pastilla.X + pastilla.Width / 2;
		var cy = pastilla.Y + pastilla.Height / 2;

		// Las flechas se dibujan como triángulos (no como caracteres ▲▼) para que se vean
		// iguales con cualquier fuente o tema.
		switch (fila.Estado)
		{
			case EstadoTendencia.Alcista:
				context.FillPolygon(ADibujo(ColorAlcista), new[] { new Point(cx, cy - 6), new Point(cx + 7, cy + 5), new Point(cx - 7, cy + 5) });
				break;
			case EstadoTendencia.Bajista:
				context.FillPolygon(ADibujo(ColorBajista), new[] { new Point(cx - 7, cy - 5), new Point(cx + 7, cy - 5), new Point(cx, cy + 6) });
				break;
			default:
				context.DrawString("(-)", _fuenteRango, ADibujo(ColorRango), pastilla, _alinearCentro);
				break;
		}

		context.DrawString("Desde: " + FormatearDesde(fila.Desde, horaUltimaVela), _fuenteDetalle, Color.Silver,
			new Rectangle(x + 118, y, 115, altoContenido), _alinearIzquierda);
	}

	private static string FormatearDesde(DateTime? desde, DateTime horaUltimaVela)
	{
		if (desde is null)
			return "--:--";

		// Si el estado empezó otro día, se muestra también la fecha.
		return desde.Value.Date == horaUltimaVela.Date
			? desde.Value.ToString("HH:mm")
			: desde.Value.ToString("dd/MM HH:mm");
	}

	private static Color ADibujo(MediaColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

	#endregion
}
