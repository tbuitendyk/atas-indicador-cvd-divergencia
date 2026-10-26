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
using Rectangle = System.Drawing.Rectangle;
using StringAlignment = System.Drawing.StringAlignment;

/// <summary>
/// Motor del Institutional 90-Day Deep LVN Profile Engine: nodos bajos de volumen (LVN) en el perfil de volumen de
/// hasta 90 días, limitado a una franja alrededor del precio. Un LVN es un precio con menos volumen que todos los
/// precios a 'Sensibilidad' ticks a cada lado. Se clasifica por la profundidad del valle (volumen del LVN frente al
/// mayor de esos vecinos) y por los días distintos en que el precio rechazó ese nivel.
/// Cada vela cerrada se suma una sola vez a perfiles y contadores diarios; los LVN se recalculan a partir de ellos una
/// vez por vela nueva, así el historial de 90 días no se vuelve a recorrer en cada vela.
/// </summary>
internal sealed class MotorLvn
{
	#region Tipos

	public enum Impacto
	{
		Alto,
		Mediano,
		Bajo
	}

	public sealed record Lvn(decimal Precio, decimal Volumen, decimal RatioValle, int RechazosSoporte, int RechazosResistencia,
		int DiasProbados, Impacto Impacto)
	{
		public int Rechazos => RechazosSoporte + RechazosResistencia;
	}

	/// <summary>Perfil y contadores de rechazo de un día, por índice de precio (precio / tick).</summary>
	private sealed class Dia
	{
		public readonly Dictionary<long, decimal> Volumen = new();
		public readonly Dictionary<long, int> SoporteExtremo = new();
		public readonly Dictionary<long, int> ResistenciaExtremo = new();
		public readonly Dictionary<long, int> SoporteMecha = new();
		public readonly Dictionary<long, int> ResistenciaMecha = new();
		public decimal Maximo = decimal.MinValue;
		public decimal Minimo = decimal.MaxValue;
	}

	#endregion

	#region Campos

	public const int AnchoMinimo = 350;
	public const int AltoTitulo = 25;
	public const int AltoPanel = 120;

	private const int MaxDias = 90;
	private const long MaxPreciosVela = 10000;
	private const long MaxPreciosFranja = 100000;

	private readonly IGrafico _grafico;

	// El cálculo y el dibujo corren en hilos distintos.
	private readonly object _sync = new();

	private readonly SortedDictionary<DateTime, Dia> _dias = new();

	private int _ultimaBarra = -1;
	private bool _pendiente;
	private decimal _tick;

	// Resultado del último recálculo: lo lee el dibujo (la lista se reemplaza, nunca se modifica).
	private List<Lvn> _lvns = new();
	private Lvn? _objetivo;
	private decimal _ultimoCierre;
	private string _regimen = "Iniciando análisis...";

	private readonly RenderFont _fuenteEtiquetaLinea = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteTitulo = new("Arial", 8f, FontStyle.Bold);
	private readonly RenderFont _fuenteEtiqueta = new("Arial", 8.5f, FontStyle.Regular);
	private readonly RenderFont _fuenteValor = new("Arial", 8.5f, FontStyle.Bold);

	private readonly RenderStringFormat _alinearIzquierda = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
	private readonly RenderStringFormat _alinearDerecha = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

	private readonly RenderPen _bordePanel = new(Color.FromArgb(65, 75, 90));

	#endregion

	public MotorLvn(IGrafico grafico) => _grafico = grafico;

	#region Configuración

	public ModoHorizonte Horizonte { get; set; } = ModoHorizonte.Automatico;

	public int DiasManual { get; set; } = 20;

	/// <summary>Puntos ± alrededor del precio en los que se buscan LVN.</summary>
	public decimal FranjaPuntos { get; set; } = 150m;

	public int SensibilidadTicks { get; set; } = 8;

	/// <summary>Volumen del LVN frente al mayor vecino por debajo del cual el valle es profundo (0.32 = 32 %).</summary>
	public decimal UmbralAltoImpacto { get; set; } = 0.32m;

	public FuenteVolumen Fuente { get; set; } = FuenteVolumen.Real;

	public TipoRechazo Rechazo { get; set; } = TipoRechazo.Extremo;

	/// <summary>Modo Extremo: ticks de tolerancia entre el mínimo (o máximo) de la vela y el nivel.</summary>
	public int ToleranciaTicks { get; set; } = 2;

	/// <summary>Modo Extremo: ticks mínimos entre el cierre y el nivel para contar como rechazo.</summary>
	public int DistanciaCierreTicks { get; set; } = 2;

	public MediaColor ColorAlto { get; set; } = MediaColor.FromArgb(255, 255, 235, 59);

	public MediaColor ColorMediano { get; set; } = MediaColor.FromArgb(255, 255, 179, 0);

	public MediaColor ColorBajo { get; set; } = MediaColor.FromArgb(255, 130, 130, 120);

	public int GrosorAlto { get; set; } = 3;

	public bool ExtenderDerecha { get; set; } = true;

	public bool MostrarBajoImpacto { get; set; }

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

			// Las velas anteriores a 'bar' ya cerraron: cada una se suma una sola vez, con sus valores finales.
			for (var i = Math.Max(0, _ultimaBarra); i < bar; i++)
			{
				AgregarVela(i);
				_pendiente = true;
			}

			_ultimaBarra = bar;

			// Los LVN se recalculan una vez por vela nueva y, al cargar el histórico, solo al llegar al final.
			if (_pendiente && bar >= _grafico.CantidadVelas - 1)
			{
				Recalcular(bar - 1);
				_pendiente = false;
			}
		}
	}

	private void Reiniciar()
	{
		_dias.Clear();
		_ultimaBarra = -1;
		_pendiente = false;
		_lvns = new List<Lvn>();
		_objetivo = null;
		_ultimoCierre = 0;
		_regimen = "Iniciando análisis...";
	}

	private void AgregarVela(int bar)
	{
		var tick = _grafico.TamanoTick;

		if (tick <= 0)
			return;

		_tick = tick;

		var vela = _grafico.Vela(bar);
		var fecha = (vela.Time + _grafico.ZonaHoraria).Date;

		if (!_dias.TryGetValue(fecha, out var dia))
		{
			dia = new Dia();
			_dias[fecha] = dia;
			Podar(fecha);
		}

		dia.Maximo = Math.Max(dia.Maximo, vela.High);
		dia.Minimo = Math.Min(dia.Minimo, vela.Low);

		var desde = Indice(vela.Low);
		var hasta = Indice(vela.High);

		if (hasta < desde || hasta - desde > MaxPreciosVela)
			return;

		// Volumen por precio: el real de ATAS; si la vela no tiene datos por nivel, se reparte en su rango.
		var repartir = true;

		if (Fuente == FuenteVolumen.Real)
		{
			var encontrado = 0m;

			for (var k = desde; k <= hasta; k++)
			{
				var volumen = vela.GetPriceVolumeInfo(k * tick)?.Volume ?? 0m;

				if (volumen > 0)
				{
					Sumar(dia.Volumen, k, volumen);
					encontrado += volumen;
				}
			}

			repartir = encontrado == 0;
		}

		if (repartir && vela.Volume > 0)
		{
			var porPrecio = vela.Volume / (hasta - desde + 1);

			for (var k = desde; k <= hasta; k++)
				Sumar(dia.Volumen, k, porPrecio);
		}

		// Rechazos tipo Mecha: precios dentro de la mecha inferior (soporte) o superior (resistencia).
		var cuerpoInferior = Math.Min(vela.Open, vela.Close);
		var cuerpoSuperior = Math.Max(vela.Open, vela.Close);

		for (var k = desde; k * tick < cuerpoInferior; k++)
			Sumar(dia.SoporteMecha, k);

		for (var k = hasta; k * tick > cuerpoSuperior; k--)
			Sumar(dia.ResistenciaMecha, k);

		// Rechazos tipo Extremo: el mínimo (o máximo) de la vela toca el nivel y el cierre queda lejos de él.
		var lejos = DistanciaCierreTicks * tick;

		for (var k = desde - ToleranciaTicks; k <= desde + ToleranciaTicks; k++)
		{
			if (vela.Close - k * tick >= lejos)
				Sumar(dia.SoporteExtremo, k);
		}

		for (var k = hasta - ToleranciaTicks; k <= hasta + ToleranciaTicks; k++)
		{
			if (k * tick - vela.Close >= lejos)
				Sumar(dia.ResistenciaExtremo, k);
		}
	}

	/// <summary>Descarta los días que ya no pueden entrar en ningún horizonte.</summary>
	private void Podar(DateTime fecha)
	{
		var limite = fecha.AddDays(-MaxDias - 1);

		while (_dias.Count > 0 && _dias.Keys.First() < limite)
			_dias.Remove(_dias.Keys.First());
	}

	private void Recalcular(int barReferencia)
	{
		_lvns = new List<Lvn>();
		_objetivo = null;

		var tick = _tick;

		if (barReferencia < 0 || tick <= 0 || _dias.Count == 0)
			return;

		var referencia = _grafico.Vela(barReferencia);
		var hoy = (referencia.Time + _grafico.ZonaHoraria).Date;
		var cierre = referencia.Close;
		_ultimoCierre = cierre;

		var desdeFecha = hoy.AddDays(-DiasDeHorizonte(hoy, cierre));
		var ventana = _dias.Where(d => d.Key >= desdeFecha && d.Key <= hoy).Select(d => d.Value).ToList();

		// Perfil de la ventana dentro de la franja alrededor del precio.
		var minimo = Indice(cierre - FranjaPuntos);
		var maximo = Indice(cierre + FranjaPuntos);

		if (maximo - minimo > MaxPreciosFranja)
			return;

		var perfil = new decimal[maximo - minimo + 1];

		foreach (var dia in ventana)
		{
			foreach (var (k, volumen) in dia.Volumen)
			{
				if (k >= minimo && k <= maximo)
					perfil[k - minimo] += volumen;
			}
		}

		// Solo entre el primer y el último precio negociado de la franja.
		var primero = Array.FindIndex(perfil, v => v > 0);
		var ultimo = Array.FindLastIndex(perfil, v => v > 0);

		if (primero < 0)
			return;

		var volumenMaximo = perfil.Max();
		var s = SensibilidadTicks;
		var lista = new List<Lvn>();

		for (var i = primero + s; i <= ultimo - s; i++)
		{
			var volumen = perfil[i];
			var pico = 0m;
			var esValle = true;

			for (var o = -s; o <= s && esValle; o++)
			{
				if (o == 0)
					continue;

				var vecino = perfil[i + o];

				if (vecino <= volumen)
					esValle = false;
				else if (vecino > pico)
					pico = vecino;
			}

			if (!esValle || pico <= 0)
				continue;

			var k = minimo + i;
			var ratio = volumen / pico;
			var (soporte, resistencia, diasProbados) = ContarRechazos(ventana, k);

			var impacto = ratio <= UmbralAltoImpacto && diasProbados >= 2
				? Impacto.Alto
				: ratio <= UmbralAltoImpacto * 1.6m || pico >= volumenMaximo * 0.70m
					? Impacto.Mediano
					: Impacto.Bajo;

			lista.Add(new Lvn(k * tick, volumen, ratio, soporte, resistencia, diasProbados, impacto));
		}

		_lvns = lista;

		// Objetivo de retest: el LVN de impacto alto o mediano más cercano al precio.
		_objetivo = lista.Where(l => l.Impacto != Impacto.Bajo).OrderBy(l => Math.Abs(cierre - l.Precio)).FirstOrDefault();
	}

	private (int Soporte, int Resistencia, int Dias) ContarRechazos(List<Dia> ventana, long k)
	{
		var soporte = 0;
		var resistencia = 0;
		var dias = 0;

		foreach (var dia in ventana)
		{
			var (porSoporte, porResistencia) = Rechazo == TipoRechazo.Extremo
				? (dia.SoporteExtremo, dia.ResistenciaExtremo)
				: (dia.SoporteMecha, dia.ResistenciaMecha);

			var s = porSoporte.GetValueOrDefault(k);
			var r = porResistencia.GetValueOrDefault(k);
			soporte += s;
			resistencia += r;

			if (s + r > 0)
				dias++;
		}

		return (soporte, resistencia, dias);
	}

	/// <summary>
	/// Automático: 3 días si el precio está en el 5 % superior o inferior del rango de los últimos 3 días
	/// (descubrimiento de precio), 60 días si cotiza dentro de una estructura previa (balance).
	/// </summary>
	private int DiasDeHorizonte(DateTime hoy, decimal cierre)
	{
		if (Horizonte == ModoHorizonte.Manual)
		{
			_regimen = DiasManual == 1 ? "Manual (1 día)" : $"Manual ({DiasManual} días)";
			return DiasManual;
		}

		var desde = hoy.AddDays(-3);
		var maximo = decimal.MinValue;
		var minimo = decimal.MaxValue;

		foreach (var (fecha, dia) in _dias)
		{
			if (fecha < desde || fecha > hoy)
				continue;

			maximo = Math.Max(maximo, dia.Maximo);
			minimo = Math.Min(minimo, dia.Minimo);
		}

		var rango = maximo - minimo;

		if (rango > 0 && (cierre > maximo - rango * 0.05m || cierre < minimo + rango * 0.05m))
		{
			_regimen = "Descubrimiento / impulso (3 días)";
			return 3;
		}

		_regimen = "Balance compuesto (60 días)";
		return 60;
	}

	private long Indice(decimal precio) => (long)Math.Round(precio / _tick);

	private static void Sumar(Dictionary<long, decimal> mapa, long k, decimal valor) =>
		mapa[k] = mapa.GetValueOrDefault(k) + valor;

	private static void Sumar(Dictionary<long, int> mapa, long k) =>
		mapa[k] = mapa.GetValueOrDefault(k) + 1;

	#endregion

	#region Dibujo

	/// <summary>Líneas de los LVN sobre el gráfico, con su etiqueta a la derecha.</summary>
	public void DibujarEnGrafico(RenderContext context)
	{
		List<Lvn> lvns;
		decimal tick;

		lock (_sync)
		{
			lvns = _lvns;
			tick = _tick;
		}

		if (lvns.Count == 0)
			return;

		var area = _grafico.Area;
		var inicioX = _grafico.XVela(Math.Max(0, _grafico.PrimeraVisible));
		var finX = ExtenderDerecha ? area.Right : _grafico.XVela(Math.Max(0, _grafico.CantidadVelas - 1)) + _grafico.AnchoVela;

		var colorAlto = Formato.ADibujo(ColorAlto);
		var colorMediano = Formato.ADibujo(ColorMediano);
		var colorBajo = Formato.ADibujo(ColorBajo);
		var plumaAlto = new RenderPen(colorAlto, GrosorAlto);
		var plumaMediano = new RenderPen(colorMediano, 2);
		var plumaBajo = new RenderPen(colorBajo, 1);

		foreach (var lvn in lvns)
		{
			if (lvn.Impacto == Impacto.Bajo && !MostrarBajoImpacto)
				continue;

			var y = _grafico.YPrecio(lvn.Precio);

			if (y < area.Top || y > area.Bottom)
				continue;

			var (pluma, color, etiqueta) = lvn.Impacto switch
			{
				Impacto.Alto => (plumaAlto, colorAlto, $"LVN ALTO IMPACTO ({lvn.DiasProbados} días probados)"),
				Impacto.Mediano => (plumaMediano, colorMediano, "LVN Mediano (Borde HVN)"),
				_ => (plumaBajo, colorBajo, "LVN Local")
			};

			context.DrawLine(pluma, inicioX, y, finX, y);

			var texto = $"{etiqueta} [{Formato.Precio(lvn.Precio, tick)}] | Rechazos: {lvn.Rechazos} ({lvn.RechazosSoporte}↑ {lvn.RechazosResistencia}↓)";
			context.DrawString(texto, _fuenteEtiquetaLinea, color, new Rectangle(finX - 610, y - 14, 600, 13), _alinearDerecha);
		}
	}

	/// <summary>Panel del horizonte, los nodos activos y el objetivo de retest.</summary>
	public void DibujarPanel(RenderContext context, int x, int y, int ancho)
	{
		List<Lvn> lvns;
		Lvn? objetivo;
		decimal cierre;
		decimal tick;
		string regimen;

		lock (_sync)
		{
			lvns = _lvns;
			objetivo = _objetivo;
			cierre = _ultimoCierre;
			tick = _tick;
			regimen = _regimen;
		}

		context.FillRectangle(Color.FromArgb(235, 16, 20, 26), new Rectangle(x, y, ancho, AltoPanel));
		context.DrawRectangle(_bordePanel, new Rectangle(x, y, ancho, AltoPanel));
		context.FillRectangle(Color.FromArgb(245, 26, 32, 42), new Rectangle(x, y, ancho, AltoTitulo));
		context.DrawString("RITHMIC 90-DAY DEEP LVN MONITOR", _fuenteTitulo, Color.FromArgb(210, 220, 235),
			new Rectangle(x + 10, y, ancho - 20, AltoTitulo), _alinearIzquierda);

		var altos = lvns.Count(l => l.Impacto == Impacto.Alto);
		var medianos = lvns.Count(l => l.Impacto == Impacto.Mediano);

		DibujarFila(context, x, y + 30, ancho, "1. Horizonte Rithmic:", regimen, Color.FromArgb(0, 229, 255));
		DibujarFila(context, x, y + 54, ancho, "2. Nodos Activos:", $"{altos} Bipolares (3M) | {medianos} Bordes HVN", Color.WhiteSmoke);

		if (objetivo is null)
		{
			DibujarFila(context, x, y + 78, ancho, "3. Objetivo Retest:", "Sin LVNs en la franja activa", Color.Gray);
			return;
		}

		var distancia = Math.Abs(cierre - objetivo.Precio);
		var color = Formato.ADibujo(objetivo.Impacto == Impacto.Alto ? ColorAlto : ColorMediano);

		DibujarFila(context, x, y + 78, ancho, "3. Objetivo Retest:",
			$"{Formato.Precio(objetivo.Precio, tick)} (a {distancia:F1} pts) [{objetivo.Rechazos} Rechs]", color);
	}

	private void DibujarFila(RenderContext context, int x, int y, int ancho, string etiqueta, string valor, Color color)
	{
		context.DrawString(etiqueta, _fuenteEtiqueta, Color.Silver, new Rectangle(x + 10, y, 125, 20), _alinearIzquierda);
		context.DrawString(valor, _fuenteValor, color, new Rectangle(x + 135, y, ancho - 145, 20), _alinearIzquierda);
	}

	#endregion
}
