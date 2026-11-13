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

	/// <summary>Recuadro desplegable al que pertenece (v3, 13): en un título, la clave que se pliega; null = siempre visible.</summary>
	public string? Grupo { get; init; }

	/// <summary>Minigráfico (velas de CVD con sus divergencias) en lugar de texto.</summary>
	public GraficoMini? Grafico { get; init; }

	public static LineaPanel De(params Segmento[] segmentos)
	{
		var l = new LineaPanel();
		l.Segmentos.AddRange(segmentos);
		return l;
	}

	public static LineaPanel Seccion(string titulo, string? grupo = null) => new LineaPanel { Titulo = true, Grupo = grupo }.Con(new Segmento(titulo, ColorPanel.Titulo, true));

	public static LineaPanel Linea() => new() { Separador = true };

	public LineaPanel Con(Segmento s)
	{
		Segmentos.Add(s);
		return this;
	}

	public string Texto => Grafico is { } g
		? $"[CVD: {g.Velas.Count} velas, {g.Tramos.Count} divergencias]"
		: string.Concat(Segmentos.ConvertAll(s => (s.Flecha == FlechaPanel.Arriba ? "▲" : s.Flecha == FlechaPanel.Abajo ? "▼" : "") + s.Texto));
}

/// <summary>Contenido de un panel flotante (lo arma el núcleo; lo dibuja la ventana o el gráfico).</summary>
public sealed class ContenidoPanel
{
	public string Titulo { get; init; } = "";
	public List<LineaPanel> Lineas { get; } = new();
	public string Pie { get; init; } = "";
}

/// <summary>Vela del minigráfico.</summary>
public sealed record VelaMini(decimal Open, decimal High, decimal Low, decimal Close);

/// <summary>Divergencia dibujada SOBRE las velas del minigráfico (índices y valores de esas velas).</summary>
public sealed record TramoMini(int I1, int I2, decimal V1, decimal V2, bool Alcista, bool Micro);

/// <summary>Minigráfico de velas de CVD con sus divergencias (v3, 6 y 13.9).</summary>
public sealed record GraficoMini(IReadOnlyList<VelaMini> Velas, IReadOnlyList<TramoMini> Tramos);

/// <summary>
/// Posición guardada de una ventana flotante (y los recuadros plegados). Version: 0 = archivo de la v3 o anterior, 4 =
/// desde la v4 (la ventana viene fijada: solo se oculta al minimizarla).
/// </summary>
public sealed record PosicionVentana(double Left, double Top, double Width, double Height, bool Topmost, double Letra, double Opacidad, bool Detalle, bool Minimizada,
	string[]? Plegadas = null, int Version = 0)
{
	public const int VersionActual = 4;

	/// <summary>v4: una posición guardada antes venía sin fijar (por eso la ventana se escondía detrás de ATAS): se fija una vez.</summary>
	public PosicionVentana Migrada() => Version >= VersionActual ? this : this with { Topmost = true, Version = VersionActual };
}

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
