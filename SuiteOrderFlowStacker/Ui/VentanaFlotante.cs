namespace OrderFlowSuite.Ui;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using OrderFlowSuite.Comun;

/// <summary>
/// FloatingWindowHost (11.1 / 12.1): ventana WPF independiente del gráfico (el patrón de la ventana del reporte del
/// Indicador Completo, que ya funciona en ATAS). Se mueve a cualquier lugar o a otro monitor arrastrando la barra de
/// título; Resumen / Detalle; minimizar; siempre visible; opacidad; tamaño de letra; guarda posición, tamaño y modo; si al
/// abrir quedó fuera de pantalla, se recoloca en el monitor principal; se refresca cada PanelRefreshMs.
/// </summary>
internal sealed class VentanaFlotante : Window
{
	private static readonly object Sync = new();
	private static readonly Dictionary<string, VentanaFlotante> Abiertas = new();

	private readonly string _clave;
	private readonly DispatcherTimer _temporizador;
	private readonly StackPanel _lineas = new();
	private readonly WrapPanel _botones = new() { Margin = new Thickness(0, 0, 0, 4) };
	private readonly TextBlock _pie = new() { Margin = new Thickness(0, 6, 0, 0), FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap };
	private Func<bool, ContenidoPanel> _contenido;
	private IReadOnlyList<BotonPanel> _extra;
	private double _letra = 11;
	private bool _detalle;

	/// <summary>
	/// Los indicadores de la DLL real registran la ventana en la fábrica al construirse (las pruebas no tienen WPF y dejan
	/// la fábrica vacía o con un falso).
	/// </summary>
	internal static void Registrar()
	{
		FabricaPaneles.Abrir ??= Abrir;
		FabricaPaneles.Cerrar ??= Cerrar;
	}

	private VentanaFlotante(string clave, string titulo, Func<bool, ContenidoPanel> contenido, IReadOnlyList<BotonPanel> botones, int refrescoMs)
	{
		(_clave, _contenido, _extra) = (clave, contenido, botones);
		Title = titulo;
		Width = 520;
		Height = 760;
		Background = new SolidColorBrush(Color.FromRgb(20, 23, 28));
		Foreground = Brocha(ColorPanel.Blanco);
		ShowInTaskbar = true;

		var raiz = new DockPanel { Margin = new Thickness(8) };
		DockPanel.SetDock(_botones, Dock.Top);
		DockPanel.SetDock(_pie, Dock.Bottom);
		raiz.Children.Add(_botones);
		raiz.Children.Add(_pie);
		raiz.Children.Add(new ScrollViewer { Content = _lineas, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
		Content = raiz;

		Leer();
		ArmarBotones();
		_temporizador = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(Math.Clamp(refrescoMs, 100, 5000)) };
		_temporizador.Tick += (_, _) => Seguro(Refrescar);
		_temporizador.Start();
		Closing += (_, _) => Seguro(Guardar);
		Closed += (_, _) =>
		{
			_temporizador.Stop();

			lock (Sync)
				Abiertas.Remove(_clave);
		};

		Refrescar();
	}

	public static void Abrir(string clave, string titulo, Func<bool, ContenidoPanel> contenido, IReadOnlyList<BotonPanel> botones, int refrescoMs)
	{
		VentanaFlotante? existente;

		lock (Sync)
			Abiertas.TryGetValue(clave, out existente);

		if (existente is not null)
		{
			existente.Dispatcher.BeginInvoke(new Action(() => existente.Seguro(() =>
			{
				(existente._contenido, existente._extra) = (contenido, botones);
				existente.ArmarBotones();
				existente.Refrescar();
			})));
			return;
		}

		EnHiloDeInterfaz(() =>
		{
			lock (Sync)
			{
				if (Abiertas.ContainsKey(clave))
					return;

				var v = new VentanaFlotante(clave, titulo, contenido, botones, refrescoMs);
				Abiertas[clave] = v;
				v.Show();
			}
		});
	}

	public static void Cerrar(string clave)
	{
		VentanaFlotante? v;

		lock (Sync)
			Abiertas.TryGetValue(clave, out v);

		v?.Dispatcher.BeginInvoke(new Action(() => v.Seguro(v.Close)));
	}

	/// <summary>En ATAS clásico, el hilo de la interfaz WPF; si no lo hubiera, un hilo STA propio.</summary>
	private static void EnHiloDeInterfaz(Action accion)
	{
		if (Application.Current?.Dispatcher is { } despachador)
		{
			despachador.BeginInvoke(new Action(() =>
			{
				try
				{
					accion();
				}
				catch
				{
					// La ventana nunca debe tumbar la plataforma.
				}
			}));
			return;
		}

		var hilo = new Thread(() =>
		{
			try
			{
				accion();
				Dispatcher.Run();
			}
			catch
			{
			}
		})
		{ IsBackground = true };

		hilo.SetApartmentState(ApartmentState.STA);
		hilo.Start();
	}

	private void Seguro(Action accion)
	{
		try
		{
			accion();
		}
		catch
		{
			// Un fallo de dibujo no debe cerrar ATAS.
		}
	}

	private void ArmarBotones()
	{
		_botones.Children.Clear();
		_botones.Children.Add(Boton(() => _detalle ? "Resumen" : "Detalle", () => _detalle = !_detalle));
		_botones.Children.Add(Boton(() => Topmost ? "Fijado" : "Fijar", () => Topmost = !Topmost));
		_botones.Children.Add(Boton(() => "A−", () => _letra = Math.Max(8, _letra - 1)));
		_botones.Children.Add(Boton(() => "A+", () => _letra = Math.Min(18, _letra + 1)));
		_botones.Children.Add(Boton(() => $"Opacidad {Opacity:P0}", () => Opacity = Opacity <= 0.55 ? 1.0 : Math.Round(Opacity - 0.15, 2)));
		_botones.Children.Add(Boton(() => "Minimizar", () => WindowState = WindowState.Minimized));

		foreach (var b in _extra)
			_botones.Children.Add(Boton(b.Texto, b.Pulsar, b.Activo));
	}

	private Button Boton(Func<string> texto, Action pulsar, Func<bool>? activo = null)
	{
		var b = new Button { Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 1, 6, 1), Content = texto() };
		b.Click += (_, _) => Seguro(() =>
		{
			pulsar();
			b.Content = texto();
			Refrescar();
		});

		if (activo is not null)
			b.Tag = activo;

		return b;
	}

	private void Refrescar()
	{
		var c = _contenido(_detalle);
		_lineas.Children.Clear();

		foreach (var l in c.Lineas)
			_lineas.Children.Add(Linea(l));

		_pie.Text = c.Pie;
		_pie.FontSize = _letra - 1;
		_pie.Foreground = Brocha(ColorPanel.Gris);

		foreach (var hijo in _botones.Children)
		{
			if (hijo is Button { Tag: Func<bool> activo } b)
				b.FontWeight = activo() ? FontWeights.Bold : FontWeights.Normal;
		}
	}

	private UIElement Linea(LineaPanel l)
	{
		if (l.Separador)
			return new Border { Height = 1, Background = Brocha(ColorPanel.Gris), Opacity = 0.4, Margin = new Thickness(0, 4, 0, 4) };

		var t = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = l.Titulo ? _letra + 1 : _letra, Margin = new Thickness(12 * l.Sangria, 1, 0, 1) };

		foreach (var s in l.Segmentos)
		{
			if (s.Flecha != FlechaPanel.Ninguna)
				t.Inlines.Add(new InlineUIContainer(FlechaVectorial(s.Flecha, s.ColorFlecha ?? s.Color)) { BaselineAlignment = BaselineAlignment.Center });

			t.Inlines.Add(new Run(s.Texto) { Foreground = Brocha(s.Color), FontWeight = s.Negrita ? FontWeights.Bold : FontWeights.Normal });
		}

		if (l.Recuadro is not { } borde)
			return t;

		return new Border { BorderBrush = Brocha(borde), BorderThickness = new Thickness(1.5), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 2, 0, 2), Child = t };
	}

	/// <summary>Flecha como forma vectorial (sin emojis).</summary>
	private UIElement FlechaVectorial(FlechaPanel f, ColorPanel color)
	{
		var h = _letra;
		var puntos = f == FlechaPanel.Arriba
			? new PointCollection { new(h / 2, 0), new(h, h), new(0, h) }
			: new PointCollection { new(0, 0), new(h, 0), new(h / 2, h) };

		return new Polygon { Points = puntos, Fill = Brocha(color), Width = h, Height = h, Margin = new Thickness(0, 0, 4, 0) };
	}

	private static SolidColorBrush Brocha(ColorPanel c)
	{
		var (r, g, b) = c switch
		{
			ColorPanel.Verde => (0, 200, 83),
			ColorPanel.Rojo => (255, 82, 82),
			ColorPanel.Naranja => (255, 152, 0),
			ColorPanel.Amarillo => (255, 235, 59),
			ColorPanel.Gris => (138, 143, 152),
			ColorPanel.Dorado => (255, 193, 7),
			ColorPanel.Fucsia => (255, 0, 200),
			ColorPanel.AzulClaro => (100, 200, 255),
			ColorPanel.Azul => (41, 121, 255),
			ColorPanel.Morado => (171, 71, 188),
			ColorPanel.Titulo => (159, 179, 200),
			_ => (235, 235, 235)
		};

		var brocha = new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
		brocha.Freeze();
		return brocha;
	}

	private string Archivo() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "OrderFlowSuite", "ventanas",
		string.Concat(_clave.Split(System.IO.Path.GetInvalidFileNameChars())) + ".json");

	private void Leer()
	{
		try
		{
			if (!File.Exists(Archivo()) || JsonSerializer.Deserialize<PosicionVentana>(File.ReadAllText(Archivo())) is not { } p)
				return;

			var ajustada = Colocacion.Ajustar(p,
				(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight),
				(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height));

			WindowStartupLocation = WindowStartupLocation.Manual;
			(Left, Top, Width, Height, Topmost) = (ajustada.Left, ajustada.Top, ajustada.Width, ajustada.Height, ajustada.Topmost);
			(_letra, _detalle) = (Math.Clamp(ajustada.Letra, 8, 18), ajustada.Detalle);
			Opacity = Math.Clamp(ajustada.Opacidad, 0.4, 1);

			if (ajustada.Minimizada)
				WindowState = WindowState.Minimized;
		}
		catch
		{
			// Un archivo dañado no impide abrir la ventana.
		}
	}

	private void Guardar()
	{
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Archivo())!);
		var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
		File.WriteAllText(Archivo(), JsonSerializer.Serialize(new PosicionVentana(r.Left, r.Top, r.Width, r.Height, Topmost, _letra, Opacity, _detalle,
			WindowState == WindowState.Minimized)));
	}
}
