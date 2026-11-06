namespace OrderFlowSuite.Comun;

using System;
using System.Collections.Generic;

/// <summary>Colores del panel (la ventana WPF y el panel dentro del gráfico los traducen).</summary>
public enum ColorPanel
{
	Blanco,
	Verde,
	Rojo,
	Naranja,
	Amarillo,
	Gris,
	Dorado,
	Fucsia,
	AzulClaro,
	Azul,
	Morado,
	Titulo
}

/// <summary>Flecha dibujada como forma vectorial (sin emojis).</summary>
public enum FlechaPanel
{
	Ninguna,
	Arriba,
	Abajo
}

public sealed record Segmento(string Texto, ColorPanel Color = ColorPanel.Blanco, bool Negrita = false, FlechaPanel Flecha = FlechaPanel.Ninguna, ColorPanel? ColorFlecha = null);

/// <summary>Una línea del panel: segmentos, sangría y, si es un recuadro (resumen de absorción o MOD3), su borde.</summary>
public sealed class LineaPanel
{
	public List<Segmento> Segmentos { get; } = new();
	public int Sangria { get; init; }
	public bool Titulo { get; init; }
	public bool Separador { get; init; }
	public ColorPanel? Recuadro { get; init; }

	public static LineaPanel De(params Segmento[] segmentos)
	{
		var l = new LineaPanel();
		l.Segmentos.AddRange(segmentos);
		return l;
	}

	public static LineaPanel Seccion(string titulo) => new LineaPanel { Titulo = true }.Con(new Segmento(titulo, ColorPanel.Titulo, true));

	public static LineaPanel Linea() => new() { Separador = true };

	public LineaPanel Con(Segmento s)
	{
		Segmentos.Add(s);
		return this;
	}

	public string Texto => string.Concat(Segmentos.ConvertAll(s => (s.Flecha == FlechaPanel.Arriba ? "▲" : s.Flecha == FlechaPanel.Abajo ? "▼" : "") + s.Texto));
}

/// <summary>Contenido de un panel flotante (lo arma el núcleo; lo dibuja la ventana o el gráfico).</summary>
public sealed class ContenidoPanel
{
	public string Titulo { get; init; } = "";
	public List<LineaPanel> Lineas { get; } = new();
	public string Pie { get; init; } = "";
}

/// <summary>Posición guardada de una ventana flotante.</summary>
public sealed record PosicionVentana(double Left, double Top, double Width, double Height, bool Topmost, double Letra, double Opacidad, bool Detalle, bool Minimizada);

/// <summary>
/// Colocación de una ventana flotante (11.1, T30): se respeta la posición guardada en cualquier monitor; si quedó fuera
/// del escritorio virtual (se quitó un monitor), se recoloca en el área de trabajo del monitor principal.
/// </summary>
public static class Colocacion
{
	public static PosicionVentana Ajustar(PosicionVentana p, (double Left, double Top, double Width, double Height) escritorio,
		(double Left, double Top, double Width, double Height) principal)
	{
		const double Margen = 50;
		var visible = p.Left + p.Width > escritorio.Left + Margen && p.Left < escritorio.Left + escritorio.Width - Margen &&
			p.Top >= escritorio.Top - 10 && p.Top < escritorio.Top + escritorio.Height - Margen;

		if (visible)
			return p;

		var ancho = Math.Min(p.Width, principal.Width);
		var alto = Math.Min(p.Height, principal.Height);
		return p with { Left = principal.Left + 40, Top = principal.Top + 40, Width = ancho, Height = alto };
	}
}

/// <summary>
/// Puente con la ventana WPF: la registra la DLL real (inicializador de módulo); en las pruebas queda vacío o lo
/// sustituye un falso. Así el núcleo y los indicadores no dependen de WPF.
/// </summary>
public static class FabricaPaneles
{
	/// <summary>(clave, título, contenido(detalle), botones, refresco ms) → abre (o reconecta) la ventana.</summary>
	public static Action<string, string, Func<bool, ContenidoPanel>, IReadOnlyList<BotonPanel>, int>? Abrir { get; set; }

	public static Action<string>? Cerrar { get; set; }
}

/// <summary>Un botón de la barra del panel (el texto puede cambiar: "Tipo: AUTO").</summary>
public sealed record BotonPanel(Func<string> Texto, Action Pulsar, Func<bool>? Activo = null);
