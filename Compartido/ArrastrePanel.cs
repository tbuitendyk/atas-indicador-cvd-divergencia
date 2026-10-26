namespace ATAS.Indicators.Custom;

using System;

using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// Arrastre con el ratón de un panel dibujado sobre el gráfico: se agarra por su barra de título y se suelta donde
/// se quiera, siempre dentro del área del gráfico. El indicador guarda la posición en sus ajustes Posición X/Y, así
/// que se conserva con el gráfico. Lo compilan todos los proyectos (ver Directory.Build.props).
/// </summary>
internal sealed class ArrastrePanel
{
	// El rectángulo lo escribe el hilo de dibujo y lo leen los eventos del ratón.
	private readonly object _sync = new();

	private Rectangle _panel;
	private int _altoTitulo;

	// Distancia del ratón a la esquina del panel mientras se arrastra; null si no se arrastra.
	private Point? _agarre;

	public bool Arrastrando
	{
		get
		{
			lock (_sync)
				return _agarre is not null;
		}
	}

	/// <summary>
	/// Rectángulo en el que dibujar el panel: la posición configurada, corregida para que quepa en el área del gráfico.
	/// Lo recuerda para reconocer después los clics en la barra de título.
	/// </summary>
	public Rectangle Ubicar(int x, int y, int ancho, int alto, int altoTitulo, Rectangle area)
	{
		lock (_sync)
		{
			_panel = new Rectangle(Ajustar(x, area.Left, area.Right - ancho), Ajustar(y, area.Top, area.Bottom - alto), ancho, alto);
			_altoTitulo = altoTitulo;
			return _panel;
		}
	}

	/// <summary>El panel no se dibujó (por ejemplo, está desactivado): no hay nada que agarrar.</summary>
	public void Ocultar()
	{
		lock (_sync)
		{
			_panel = Rectangle.Empty;
			_agarre = null;
		}
	}

	public bool SobreTitulo(Point raton)
	{
		lock (_sync)
			return new Rectangle(_panel.X, _panel.Y, _panel.Width, _altoTitulo).Contains(raton);
	}

	/// <summary>Empieza el arrastre si el clic cae en la barra de título. Devuelve true si el clic es del panel.</summary>
	public bool Presionar(Point raton)
	{
		if (!SobreTitulo(raton))
			return false;

		lock (_sync)
			_agarre = new Point(raton.X - _panel.X, raton.Y - _panel.Y);

		return true;
	}

	/// <summary>Nueva esquina superior izquierda del panel mientras se arrastra; null si no se está arrastrando.</summary>
	public Point? Mover(Point raton, Rectangle area)
	{
		lock (_sync)
		{
			if (_agarre is not { } agarre)
				return null;

			return new Point(Ajustar(raton.X - agarre.X, area.Left, area.Right - _panel.Width),
				Ajustar(raton.Y - agarre.Y, area.Top, area.Bottom - _panel.Height));
		}
	}

	/// <summary>Termina el arrastre. Devuelve true si había uno en curso.</summary>
	public bool Soltar()
	{
		lock (_sync)
		{
			var arrastraba = _agarre is not null;
			_agarre = null;
			return arrastraba;
		}
	}

	// Si el panel no cabe en el área, queda pegado al borde superior o izquierdo.
	private static int Ajustar(int valor, int minimo, int maximo) => Math.Max(minimo, Math.Min(valor, maximo));
}
