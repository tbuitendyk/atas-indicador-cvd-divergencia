namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using StdCursor = OFT.Rendering.StdCursor;

/// <summary>
/// v5 Sweep Pro (FAST): recuadros marcados a mano desde el recuadro "2. Barrido" del panel: BARRIDO REAL, BARRIDO REAL
/// PRIME, ROMPIMIENTO (arrancan la verificación de los modelos) y RECUADRO DE VELA COMPUESTA (flecha en la ruptura de su
/// rango). Se pulsa el botón, se arrastra el recuadro en el gráfico y se suelta. Se guardan con el gráfico.
/// </summary>
public partial class SessionSweepsPro
{
	private TipoRecuadro? _herramientaRecuadro;
	private Point? _inicioRecuadro, _finRecuadro;
	private IReadOnlyList<BotonPanel>? _botonesRecuadro;

	[Display(Name = "Recuadros manuales (guardados; no editar)", GroupName = "26. Gatillo manual", Order = 900,
		Description = "Los recuadros de barrido, rompimiento y vela compuesta marcados en el gráfico. Se borran con BORRAR RECUADROS en el panel.")]
	public string ManualBoxes { get; set; } = "";

	/// <summary>Los botones del recuadro 2 (los mismos objetos siempre: la ventana los reutiliza).</summary>
	private IReadOnlyList<BotonPanel> BotonesRecuadro() => _botonesRecuadro ??= new[]
	{
		Herramienta("MARCAR BARRIDO REAL", TipoRecuadro.BarridoReal),
		Herramienta("MARCAR BARRIDO REAL PRIME", TipoRecuadro.BarridoPrime),
		Herramienta("MARCAR ROMPIMIENTO", TipoRecuadro.Rompimiento),
		Herramienta("RECUADRO VELA COMPUESTA", TipoRecuadro.Compuesta),
		new BotonPanel(() => "BORRAR RECUADROS", () =>
		{
			_motor?.QuitarRecuadros();
			ManualBoxes = "";
			RedrawChart();
		})
	};

	private BotonPanel Herramienta(string texto, TipoRecuadro tipo) =>
		new(() => _herramientaRecuadro == tipo ? texto + " (arrastre en el gráfico)" : texto,
			() =>
			{
				_herramientaRecuadro = _herramientaRecuadro == tipo ? null : tipo;
				RedrawChart();
			},
			() => _herramientaRecuadro == tipo);

	/// <summary>Los botones van dentro del recuadro 2, justo debajo de su título.</summary>
	private void AgregarBotonesRecuadro(ContenidoPanel c)
	{
		var titulo = c.Lineas.FindIndex(l => l.Titulo && l.Grupo == "b2");

		if (titulo >= 0)
			c.Lineas.Insert(titulo + 1, new LineaPanel { Grupo = "b2", Botones = BotonesRecuadro() });
	}

	/// <summary>Al entrar en vivo: los recuadros guardados vuelven (con su dirección de entonces).</summary>
	private void RestaurarRecuadros(MotorSweeps motor)
	{
		foreach (var r in RecuadroManual.Leer(ManualBoxes))
			motor.Marcar(r.Tipo, r.DesdeUtc, r.HastaUtc, r.Bajo, r.Alto, r.CreadoUtc, r.Signo);
	}

	public override bool ProcessMouseDown(RenderControlMouseEventArgs e)
	{
		if (_herramientaRecuadro is not null && _motor is not null)
		{
			_inicioRecuadro = _finRecuadro = e.Location;
			return true;
		}

		return base.ProcessMouseDown(e);
	}

	public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
	{
		if (_inicioRecuadro is not null)
		{
			_finRecuadro = e.Location;
			RedrawChart();
			return true;
		}

		return base.ProcessMouseMove(e);
	}

	public override bool ProcessMouseUp(RenderControlMouseEventArgs e)
	{
		if (_inicioRecuadro is not { } a || _herramientaRecuadro is not { } tipo || _motor is not { } motor)
			return base.ProcessMouseUp(e);

		var b = e.Location;
		(_inicioRecuadro, _finRecuadro, _herramientaRecuadro) = (null, null, null);

		try
		{
			if (Math.Abs(b.X - a.X) >= 3 && CurrentBar > 0)
			{
				var barA = BarraDeX(Math.Min(a.X, b.X));
				var barB = BarraDeX(Math.Max(a.X, b.X));
				var (alto, bajo) = (PrecioDeY(Math.Min(a.Y, b.Y)), PrecioDeY(Math.Max(a.Y, b.Y)));
				// v5: en 5 minutos el recuadro llega hasta el último minuto de la vela de la derecha.
				motor.Marcar(tipo, GetCandle(barA).Time, GetCandle(barB).Time.AddMinutes(Math.Max(1, _minutos) - 1), bajo, alto, DateTime.UtcNow);
				ManualBoxes = motor.Leer(m => RecuadroManual.Escribir(m.Recuadros));
			}
		}
		catch (Exception ex)
		{
			this.LogError("Sweep Pro (FAST): error al marcar el recuadro", ex);
		}

		RedrawChart();
		return true;
	}

	public override StdCursor GetCursor(RenderControlMouseEventArgs e) => _herramientaRecuadro is not null ? StdCursor.Hand : base.GetCursor(e);

	/// <summary>Vela bajo una coordenada X (la más cercana de las visibles).</summary>
	private int BarraDeX(int x)
	{
		var primera = Math.Max(0, FirstVisibleBarNumber);
		var ultima = Math.Min(CurrentBar - 1, Math.Max(primera, LastVisibleBarNumber));
		var (mejor, distancia) = (ultima, int.MaxValue);

		for (var bar = primera; bar <= ultima; bar++)
		{
			var d = Math.Abs(ChartInfo!.GetXByBar(bar) + AnchoVela / 2 - x);

			if (d < distancia)
				(distancia, mejor) = (d, bar);
		}

		return mejor;
	}

	/// <summary>Precio de una coordenada Y: se invierte la conversión de ATAS (vale con escalas no lineales).</summary>
	private decimal PrecioDeY(int y)
	{
		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		return Termico.Pantalla.PrecioDeY(y, Y, GetCandle(CurrentBar - 1).Close, tick);
	}

	/// <summary>Los recuadros manuales y de vela compuesta (con su flecha de ruptura), y el que se está arrastrando.</summary>
	private void DibujarRecuadros(RenderContext context, MotorSweeps motor)
	{
		var recuadros = motor.Leer(m => m.Recuadros.Select(r => (r.Tipo, r.DesdeUtc, r.HastaUtc, r.Bajo, r.Alto, r.Signo, r.Texto, r.RompeUtc, r.RompeDireccion, r.RompePrecio)).ToList());

		foreach (var r in recuadros)
		{
			var x1 = X(BarDe(r.DesdeUtc));
			var x2 = X(BarDe(r.HastaUtc)) + AnchoVela;
			var (y1, y2) = (Y(r.Alto), Y(r.Bajo));
			var giro = r.Tipo == TipoRecuadro.Rompimiento ? r.Signo : -r.Signo;
			var color = r.Tipo switch
			{
				TipoRecuadro.Compuesta => Color.FromArgb(255, 180, 180, 180),
				TipoRecuadro.Rompimiento => Color.FromArgb(255, 41, 121, 255),
				_ => giro > 0 ? Color.FromArgb(255, 0, 200, 83) : Color.FromArgb(255, 255, 82, 82)
			};
			var caja = new Rectangle(x1, y1, Math.Max(2, x2 - x1), Math.Max(2, y2 - y1));
			context.FillRectangle(Color.FromArgb(r.Tipo == TipoRecuadro.Compuesta ? 25 : 45, color), caja);
			context.DrawRectangle(new RenderPen(r.Tipo == TipoRecuadro.BarridoPrime ? Color.FromArgb(255, 255, 193, 7) : color, r.Tipo == TipoRecuadro.BarridoPrime ? 2 : 1), caja);
			context.DrawString(r.Tipo == TipoRecuadro.Compuesta ? "VELA COMPUESTA" : "MANUAL: " + r.Texto, _fuente!, color, new Rectangle(x1, y1 - 14, 220, 13), _izquierda);

			if (r.RompeUtc is { } t)
			{
				var verde = r.RompeDireccion > 0;
				var flecha = verde ? Color.FromArgb(255, 0, 200, 83) : Color.FromArgb(255, 255, 82, 82);
				var y = Y(r.RompePrecio);
				Flecha(context, X(BarDe(t)), verde ? y - 14 : y + 14, r.RompeDireccion, flecha);
			}
		}

		if (_inicioRecuadro is { } a && _finRecuadro is { } b)
		{
			var vista = new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
			context.DrawRectangle(new RenderPen(Color.FromArgb(255, 255, 193, 7), 1), vista);
		}
	}
}
