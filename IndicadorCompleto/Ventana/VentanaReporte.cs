namespace Completo.Ventana;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

using Completo.Nucleo;

/// <summary>
/// Ventana flotante del reporte: independiente del gráfico, movible entre monitores, redimensionable, con posición
/// guardada y opción «siempre visible». Resumen fijo arriba y secciones desplegables debajo. Solo lectura del motor.
/// </summary>
internal sealed class VentanaReporte : Window
{
	private sealed record Posicion(double Left, double Top, double Width, double Height, bool Topmost);

	private static readonly object Sync = new();
	private static readonly Dictionary<string, VentanaReporte> Abiertas = new();

	private readonly string _clave;
	private readonly DispatcherTimer _temporizador;
	private readonly Dictionary<string, bool> _expandidas = new();
	private readonly TextBlock _info = Texto("", 12);
	private readonly TextBlock _aviso = Texto("", 12, Brochas.Fallo, true);
	private readonly TextBlock _cabecera = Texto("", 19, Brochas.Normal, true);
	private readonly StackPanel _resumen = new() { Margin = new Thickness(0, 4, 0, 8) };
	private readonly StackPanel _secciones = new();
	private readonly TextBlock _estado = Texto("", 11, Brochas.Gris);
	private readonly ToggleButton _fijar = new() { Content = "Fijar (siempre visible)", Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 2, 6, 2) };

	private Func<InstantaneaReporte> _instantanea;
	private Func<string> _json, _csv;
	private Func<int> _revision;
	private int _revisionMostrada = -1, _revisionVista = -1;
	private DateTime _cambioRevision = DateTime.UtcNow, _ultimoPintado = DateTime.MinValue;

	private VentanaReporte(string clave, string titulo, bool siempreVisible, Func<InstantaneaReporte> instantanea, Func<string> json, Func<string> csv, Func<int> revision)
	{
		_clave = clave;
		(_instantanea, _json, _csv, _revision) = (instantanea, json, csv, revision);

		Title = titulo;
		Width = 600;
		Height = 820;
		Background = new SolidColorBrush(Color.FromRgb(20, 23, 28));
		Foreground = Brochas.Normal;
		Topmost = siempreVisible;
		ShowActivated = false;
		Restaurar();

		_fijar.IsChecked = Topmost;
		_fijar.Checked += (_, _) => Seguro(() => Topmost = true);
		_fijar.Unchecked += (_, _) => Seguro(() => Topmost = false);

		var arriba = new StackPanel { Margin = new Thickness(10, 8, 10, 0) };
		var barra = new DockPanel();
		var marca = Texto("INDICADOR COMPLETO · análisis, sin órdenes", 13, Brochas.Titulo, true);
		DockPanel.SetDock(_fijar, Dock.Right);
		barra.Children.Add(_fijar);
		barra.Children.Add(marca);
		arriba.Children.Add(barra);
		arriba.Children.Add(_info);
		arriba.Children.Add(_aviso);
		arriba.Children.Add(new Border { Height = 1, Background = Brochas.Gris, Margin = new Thickness(0, 6, 0, 6) });
		_cabecera.HorizontalAlignment = HorizontalAlignment.Center;
		arriba.Children.Add(_cabecera);
		arriba.Children.Add(_resumen);

		var abajo = new WrapPanel { Margin = new Thickness(10, 6, 10, 8) };
		abajo.Children.Add(Boton("Resumen", () => Expandir(_ => false)));
		abajo.Children.Add(Boton("Detalle", () => Expandir(_ => true)));
		abajo.Children.Add(Boton("Eventos", () => Expandir(t => t.StartsWith("EVENTOS"))));
		abajo.Children.Add(Boton("Exportar instantánea JSON", () => Exportar("instantanea", "json", _json)));
		abajo.Children.Add(Boton("Exportar estadística CSV", () => Exportar("estadistica", "csv", _csv)));
		abajo.Children.Add(_estado);

		var desplazable = new ScrollViewer { Content = _secciones, Margin = new Thickness(10, 0, 10, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

		var raiz = new DockPanel();
		DockPanel.SetDock(arriba, Dock.Top);
		DockPanel.SetDock(abajo, Dock.Bottom);
		raiz.Children.Add(arriba);
		raiz.Children.Add(abajo);
		raiz.Children.Add(desplazable);
		Content = raiz;

		Closing += (_, _) => Seguro(() =>
		{
			Guardar();
			lock (Sync)
				Abiertas.Remove(_clave);
		});

		_temporizador = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
		_temporizador.Tick += (_, _) => Seguro(Refrescar);
		_temporizador.Start();
		Closed += (_, _) => _temporizador.Stop();
	}

	/// <summary>Abre la ventana de la clave o, si ya existe, la conecta al motor nuevo (recálculo).</summary>
	public static void Abrir(string clave, string titulo, bool siempreVisible, Func<InstantaneaReporte> instantanea, Func<string> json, Func<string> csv, Func<int> revision)
	{
		VentanaReporte? existente;

		lock (Sync)
			Abiertas.TryGetValue(clave, out existente);

		if (existente is not null)
		{
			existente.Dispatcher.BeginInvoke(new Action(() => existente.Seguro(() =>
			{
				(existente._instantanea, existente._json, existente._csv, existente._revision) = (instantanea, json, csv, revision);
				existente._revisionMostrada = -1;
				existente.Topmost = siempreVisible;
				existente._fijar.IsChecked = siempreVisible;
			})));
			return;
		}

		EnHiloDeInterfaz(() =>
		{
			lock (Sync)
			{
				if (Abiertas.ContainsKey(clave))
					return;

				var ventana = new VentanaReporte(clave, titulo, siempreVisible, instantanea, json, csv, revision);
				Abiertas[clave] = ventana;
				ventana.Show();
			}
		});
	}

	public static void Cerrar(string clave)
	{
		VentanaReporte? ventana;

		lock (Sync)
			Abiertas.TryGetValue(clave, out ventana);

		ventana?.Dispatcher.BeginInvoke(new Action(() => ventana.Seguro(ventana.Close)));
	}

	/// <summary>En ATAS clásico se usa el hilo de la interfaz WPF; si no lo hubiera, un hilo STA propio.</summary>
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
		{
			IsBackground = true
		};

		hilo.SetApartmentState(ApartmentState.STA);
		hilo.Start();
	}

	private void Seguro(Action accion)
	{
		try
		{
			accion();
		}
		catch (Exception ex)
		{
			_estado.Text = "Error de la ventana: " + ex.Message;
		}
	}

	private void Refrescar()
	{
		var revision = _revision();

		if (revision != _revisionVista)
		{
			_revisionVista = revision;
			_cambioRevision = DateTime.UtcNow;
		}

		var parado = DateTime.UtcNow - _cambioRevision;
		_aviso.Text = parado > TimeSpan.FromMinutes(3)
			? $"SIN NUEVAS REVISIONES DESDE HACE {parado.TotalMinutes:0} MIN: fuente detenida, desconectada o mercado cerrado."
			: "";
		_aviso.Visibility = _aviso.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

		// Se repinta al cambiar el motor y, como mínimo, cada 5 s (hora y antigüedad de la señal).
		if (revision == _revisionMostrada && (DateTime.UtcNow - _ultimoPintado).TotalSeconds < 5)
			return;

		_revisionMostrada = revision;
		_ultimoPintado = DateTime.UtcNow;
		Pintar(_instantanea());
	}

	private void Pintar(InstantaneaReporte r)
	{
		_info.Text = $"Instrumento: {r.Instrumento}    Modo: {r.Modo}\nSesión: {r.Sesion}    Hora: {r.Hora}\nFuente: {r.Fuente}\nGráficos: {r.Graficos}";
		_cabecera.Text = r.Cabecera;
		_cabecera.Foreground = Brochas.De(r.TonoCabecera);

		_resumen.Children.Clear();

		foreach (var linea in r.Resumen)
			_resumen.Children.Add(Linea(linea));

		_secciones.Children.Clear();

		foreach (var seccion in r.Secciones)
		{
			var titulo = seccion.Titulo;
			var contenido = new StackPanel { Margin = new Thickness(14, 2, 0, 6) };

			foreach (var linea in seccion.Lineas)
				contenido.Children.Add(Linea(linea));

			var desplegable = new Expander
			{
				Header = Texto(titulo, 13, Brochas.Titulo, true),
				Content = contenido,
				Foreground = Brochas.Normal,
				Margin = new Thickness(0, 2, 0, 2),
				IsExpanded = _expandidas.TryGetValue(titulo, out var abierta) ? abierta : titulo.StartsWith("1.") || titulo.StartsWith("2.") || titulo.StartsWith("3.")
			};

			desplegable.Expanded += (_, _) => _expandidas[titulo] = true;
			desplegable.Collapsed += (_, _) => _expandidas[titulo] = false;
			_secciones.Children.Add(desplegable);
		}
	}

	private void Expandir(Func<string, bool> abrir)
	{
		foreach (var desplegable in _secciones.Children.OfType<Expander>())
		{
			var titulo = ((TextBlock)desplegable.Header).Text;
			desplegable.IsExpanded = abrir(titulo);
			_expandidas[titulo] = desplegable.IsExpanded;
		}
	}

	private void Exportar(string nombre, string extension, Func<string> contenido)
	{
		var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IndicadorCompleto");
		Directory.CreateDirectory(carpeta);
		var ruta = Path.Combine(carpeta, $"{nombre}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
		File.WriteAllText(ruta, contenido());
		_estado.Text = "Guardado: " + ruta;
	}

	private string ArchivoPosicion()
	{
		var seguro = string.Concat(_clave.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IndicadorCompleto", $"ventana_{seguro}.json");
	}

	private void Restaurar()
	{
		try
		{
			var archivo = ArchivoPosicion();

			if (!File.Exists(archivo) || JsonSerializer.Deserialize<Posicion>(File.ReadAllText(archivo)) is not { } p)
				return;

			// Solo si la posición guardada sigue dentro del escritorio virtual (monitores actuales).
			if (p.Left >= SystemParameters.VirtualScreenLeft - 50 && p.Top >= SystemParameters.VirtualScreenTop - 50
				&& p.Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50
				&& p.Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50)
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				(Left, Top) = (p.Left, p.Top);
			}

			(Width, Height) = (Math.Max(300, p.Width), Math.Max(200, p.Height));
		}
		catch
		{
			// Sin posición guardada válida: tamaño por defecto.
		}
	}

	private void Guardar()
	{
		var archivo = ArchivoPosicion();
		Directory.CreateDirectory(Path.GetDirectoryName(archivo)!);
		File.WriteAllText(archivo, JsonSerializer.Serialize(new Posicion(Left, Top, Width, Height, Topmost)));
	}

	private static TextBlock Texto(string texto, double tamano, Brush? color = null, bool negrita = false) => new()
	{
		Text = texto,
		FontSize = tamano,
		Foreground = color ?? Brochas.Normal,
		FontWeight = negrita ? FontWeights.Bold : FontWeights.Normal,
		TextWrapping = TextWrapping.Wrap,
		FontFamily = new FontFamily("Segoe UI")
	};

	private static TextBlock Linea(LineaReporte linea)
	{
		var bloque = Texto(linea.Texto, 12, Brochas.De(linea.Tono), linea.Tono is TonoLinea.Titulo or TonoLinea.Blanco);
		bloque.FontFamily = new FontFamily("Consolas");
		return bloque;
	}

	private Button Boton(string texto, Action accion)
	{
		var boton = new Button { Content = texto, Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(8, 3, 8, 3) };
		boton.Click += (_, _) => Seguro(accion);
		return boton;
	}

	private static class Brochas
	{
		public static readonly Brush Normal = Hacer(230, 230, 230);
		public static readonly Brush Titulo = Hacer(159, 179, 200);
		public static readonly Brush Ok = Hacer(46, 204, 113);
		public static readonly Brush Pendiente = Hacer(245, 176, 65);
		public static readonly Brush Fallo = Hacer(255, 82, 82);
		public static readonly Brush Aviso = Hacer(255, 193, 7);
		public static readonly Brush Gris = Hacer(138, 143, 152);
		public static readonly Brush Amarillo = Hacer(255, 239, 0);
		public static readonly Brush Naranja = Hacer(255, 140, 0);
		public static readonly Brush Blanco = Hacer(255, 255, 255);

		private static Brush Hacer(byte r, byte g, byte b)
		{
			var brocha = new SolidColorBrush(Color.FromRgb(r, g, b));
			brocha.Freeze();
			return brocha;
		}

		public static Brush De(TonoLinea tono) => tono switch
		{
			TonoLinea.Titulo => Titulo,
			TonoLinea.Ok => Ok,
			TonoLinea.Pendiente => Pendiente,
			TonoLinea.Fallo => Fallo,
			TonoLinea.Aviso => Aviso,
			TonoLinea.Gris => Gris,
			TonoLinea.Amarillo => Amarillo,
			TonoLinea.Naranja => Naranja,
			TonoLinea.Blanco => Blanco,
			_ => Normal
		};
	}
}
