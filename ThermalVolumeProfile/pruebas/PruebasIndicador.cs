using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

using OFT.Rendering.Context;
using OFT.Rendering.Control;

using Termico;

using static Datos;

/// <summary>
/// El indicador real (ThermalVolumeProfile.cs) contra los sustitutos de ATAS: carga, tiempo real, cambio de
/// instrumento, rango vacío, zona fuera del perfil, gráfico de ticks, dibujo, etiquetas, alertas y herramientas.
/// </summary>
internal static class PruebasIndicador
{
	private static readonly DateTime T0 = new(2026, 10, 26, 14, 0, 0);

	// Escala de prueba: 15000 en y = 750 y 15020 en y = 150 (30 píxeles por punto, 7,5 por tick).
	private static int YDe(decimal precio) => (int)Math.Round((15025 - precio) * 30);

	/// <summary>20 velas de 60 min con el peine de PruebasNucleo (15000 a 15020) repartido en todas.</summary>
	private static List<IndicatorCandle> Velas(int cuantas = 20, Func<int, DateTime>? hora = null)
	{
		var peine = PruebasNucleo.Peine();
		return Enumerable.Range(0, cuantas)
			.Select(i => Vela(hora?.Invoke(i) ?? T0.AddHours(i), 15000m, 15020m, p => (decimal)peine[(int)((p - 15000m) / Tick)]))
			.ToList();
	}

	private static ThermalVolumeProfile Crear(List<IndicatorCandle> velas, decimal tick = 0.25m, Action<ThermalVolumeProfile>? ajustes = null)
	{
		var ind = new ThermalVolumeProfile
		{
			InstrumentInfo = new InstrumentInfo { TickSize = tick, TimeZoneOffset = TimeSpan.FromHours(-6) },
			ChartInfo = new ChartInfo { TestYByPrice = YDe },
			TestCandles = velas,
			FirstVisibleBarNumber = 0,
			LastVisibleBarNumber = velas.Count - 1,
			RangeMode = RangeModes.LastNBars,
			LastNBars = 1000
		};

		ajustes?.Invoke(ind);
		ind.TestCalcAll();
		return ind;
	}

	/// <summary>Etiquetas de los niveles: las que están pegadas al borde derecho (1200 − 4).</summary>
	private static List<string> Etiquetas(RenderContext r) => r.Texts.Where(t => t.Rect.Right == 1196).Select(t => t.Text).ToList();

	/// <summary>Línea de estado de la barra de herramientas (lo último que se dibuja).</summary>
	private static string Estado(RenderContext r) => r.Texts.LastOrDefault().Text ?? "";

	private static int RectangulosDelPerfil(RenderContext r) => r.Rects.Count(x => x.Color.A == 180);

	public static void Ejecutar(Comprobador c)
	{
		CargaYDibujo(c);
		Geometria(c);
		CasosLimite(c);
		TiempoReal(c);
		Alertas(c);
		Herramientas(c);
	}

	private static void CargaYDibujo(Comprobador c)
	{
		c.Seccion("Indicador: carga del historial, niveles dibujados y etiquetas ASCII en el borde derecho");

		var ind = Crear(Velas());
		var r = ind.TestRender();
		var etiquetas = Etiquetas(r);
		c.Check("niveles del peine: clúster (arriba ajustado al EDGE), cPOC, EDGE de abajo, POC, VA",
			string.Join(" | ", etiquetas.Where(e => e.StartsWith("CLU") || e.StartsWith("cPOC") || e.StartsWith("EDGE") || e.StartsWith("POC")).OrderBy(e => e, StringComparer.Ordinal)),
			"CLU ABAJO 15007.50 | CLU ARRIBA 15014.00 | EDGE ABAJO 15005.00 | POC 15010.50 | cPOC 15010.50");
		c.Check("todas las etiquetas son ASCII (sin flechas, emojis ni acentos)", r.Texts.All(t => t.Text.All(ch => ch < 128)).ToString(), "True");
		c.Check("etiquetas en el borde derecho (x + ancho = 1196)", etiquetas.Count > 0 && r.Texts.Where(t => etiquetas.Contains(t.Text)).All(t => t.Rect.Right == 1196), $"{etiquetas.Count} etiquetas");

		var ys = r.Texts.Where(t => etiquetas.Contains(t.Text)).Select(t => t.Rect.Y).OrderBy(y => y).ToList();
		c.Check("etiquetas sin solaparse (cPOC y POC en el mismo precio quedan separadas)", ys.Zip(ys.Skip(1)).All(p => p.Second - p.First >= 16).ToString(), "True");
		c.Check("una barra por fila del perfil (81 filas, todas visibles)", RectangulosDelPerfil(r).ToString(), "81");
		c.Check("la barra del POC mide el 35 % del rango (20 velas × 10 px = 196 px → 68)", r.Rects.Where(x => x.Color.A == 180).Max(x => x.Rect.Width).ToString(), "68");
		c.Check("color térmico: el POC en rojo oscuro", r.Rects.Where(x => x.Color.A == 180).OrderByDescending(x => x.Rect.Width).First().Color.ToArgb().ToString("X8"), "B4960000");
		c.Check("el perfil queda detrás de las velas por defecto", ind.DrawAbovePrice.ToString(), "False");
		ind.ProfileInFront = true;
		c.Check("«perfil delante de las velas» activa DrawAbovePrice", ind.DrawAbovePrice.ToString(), "True");
		c.Check("se dibuja en el panel de velas", ind.Panel, "CandlesPanel");

		ind.ShowProfile = false;
		var sinPerfil = ind.TestRender();
		c.Check("perfil oculto: sin barras, las líneas y etiquetas siguen", $"{RectangulosDelPerfil(sinPerfil)} {Etiquetas(sinPerfil).Count == etiquetas.Count}", "0 True");

		ind.ShowProfile = true;
		ind.ClusterLabels = false;
		c.Check("cada tipo de nota se puede ocultar (etiquetas de clúster)", Etiquetas(ind.TestRender()).Any(e => e.StartsWith("CLU") || e.StartsWith("cPOC")).ToString(), "False");
		ind.ShowLabels = false;
		c.Check("y todas a la vez", Etiquetas(ind.TestRender()).Count.ToString(), "0");

		// Solo las filas visibles: el área cubre de 15005 a 15015.
		var parcial = Crear(Velas());
		parcial.ChartInfo!.PriceChartContainer.Region = new System.Drawing.Rectangle(0, YDe(15015m), 1200, YDe(15005m) - YDe(15015m));
		c.Check("solo se dibujan las filas visibles (unas 41 de 81)", RectangulosDelPerfil(parcial.TestRender()) is >= 40 and <= 44, $"{RectangulosDelPerfil(parcial.TestRender())}");
	}

	private static void Geometria(Comprobador c)
	{
		c.Seccion("Indicador: anclaje y dirección (izquierda a derecha o derecha a izquierda)");

		static (int Min, int Max) Bordes(RenderContext r)
		{
			var barras = r.Rects.Where(x => x.Color.A == 180).Select(x => x.Rect).ToList();
			return (barras.Min(b => b.Left), barras.Max(b => b.Right));
		}

		var izquierda = Crear(Velas());
		c.Check("anclaje izquierda del rango: las barras salen de x = 0 hacia la derecha", Bordes(izquierda.TestRender()).ToString(), "(0, 68)");

		izquierda.ProfileDirection = ProfileDirections.RightToLeft;
		c.Check("de derecha a izquierda desde el mismo anclaje", Bordes(izquierda.TestRender()).ToString(), "(-68, 0)");

		var derecha = Crear(Velas(), ajustes: i => i.ProfileAnchor = ProfileAnchors.RightOfRange);
		c.Check("anclaje derecha del rango (x = 196): hacia la izquierda", Bordes(derecha.TestRender()).ToString(), "(128, 196)");

		var pantalla = Crear(Velas(), ajustes: i => i.ProfileAnchor = ProfileAnchors.RightOfScreen);
		c.Check("anclaje derecha de la pantalla: 35 % de 1200 px", Bordes(pantalla.TestRender()).ToString(), "(780, 1200)");
	}

	private static void CasosLimite(Comprobador c)
	{
		c.Seccion("Indicador: rango vacío, zona fuera del perfil, cambio de instrumento, ticks, velas sin niveles, sin datos");

		var vacio = Crear(Velas(), ajustes: i =>
		{
			i.RangeMode = RangeModes.CustomDateTime;
			i.StartDateTime = new DateTime(2030, 1, 1);
			i.UseCurrentBarAsEnd = false;
			i.EndDateTime = new DateTime(2030, 2, 1);
		});
		var r = vacio.TestRender();
		c.Check("rango vacío: sin perfil ni niveles, y lo dice", $"{RectangulosDelPerfil(r)} {Etiquetas(r).Count(e => !e.StartsWith("RANGO"))} {Estado(r)}", "0 0 RANGO VACIO: no hay velas en el rango elegido");

		var zona = Crear(Velas(), ajustes: i =>
		{
			i.UseSearchZone = true;
			i.SearchZoneHigh = 16100;
			i.SearchZoneLow = 16000;
		});
		c.Check("zona de búsqueda fuera del perfil: solo POC y VA", string.Join(" | ", Etiquetas(zona.TestRender()).Select(e => e.Split(' ')[0])), "VAH | POC | VAL");

		var instrumento = Crear(Velas());
		var antes = Etiquetas(instrumento.TestRender()).First(e => e.StartsWith("POC"));
		instrumento.InstrumentInfo = new InstrumentInfo { TickSize = 0.5m, TimeZoneOffset = TimeSpan.FromHours(-6) };
		instrumento.TestCalcAll();
		c.Check("cambio de instrumento (tick 0,25 → 0,5): ATAS recalcula desde la vela 0 y todo se rehace",
			$"{antes} → {Etiquetas(instrumento.TestRender()).First(e => e.StartsWith("POC"))}", "POC 15010.50 → POC 15010.5");

		// Gráfico de ticks: 30 velas con horas repetidas (varias por segundo) y modo fecha/hora.
		var ticks = Crear(Velas(30, i => T0.AddSeconds(i / 3)), ajustes: i =>
		{
			i.RangeMode = RangeModes.CustomDateTime;
			i.StartDateTime = T0.AddSeconds(2) - TimeSpan.FromHours(6);
			i.EndDateTime = T0.AddSeconds(5) - TimeSpan.FromHours(6);
			i.UseCurrentBarAsEnd = false;
		});
		c.Check("gráfico de ticks: el rango por fecha y hora usa la hora de cada vela (velas 6 a 17)", Estado(ticks.TestRender()).Split(' ')[0], "12");

		var sinNiveles = Velas();
		sinNiveles[3].Levels.Clear();
		sinNiveles[3].Volume = 81;
		Utils.Common.Logging.Registro.Mensajes.Clear();
		var conAviso = Crear(sinNiveles);
		conAviso.TestCalcAll();
		c.Check("vela sin volumen por precio: se reparte y se avisa una sola vez en el log",
			Utils.Common.Logging.Registro.Mensajes.Count(m => m.StartsWith("WARN")).ToString(), "1");

		var sinDatos = new ThermalVolumeProfile { TestCandles = Velas() };
		Exception? error = null;

		try
		{
			sinDatos.TestCalcAll();
			sinDatos.TestRender();
		}
		catch (Exception ex)
		{
			error = ex;
		}

		c.Check("sin ChartInfo ni InstrumentInfo: no falla", error?.Message ?? "sin error", "sin error");
		c.Check("sin errores en el log", Utils.Common.Logging.Registro.Mensajes.Count(m => m.StartsWith("ERROR")).ToString(), "0");
	}

	private static void TiempoReal(Comprobador c)
	{
		c.Seccion("Indicador: en tiempo real solo se rehace la última vela");

		var velas = Velas();
		var ind = Crear(velas);
		var lecturas = velas.Select(v => v.Lecturas).ToArray();

		// La vela abierta recibe mucho volumen en 15019 (más que el POC de todo el rango).
		velas[^1].Levels[15019m] = (3000, 3000);
		Thread.Sleep(300);
		ind.TestCalc(velas.Count - 1);

		c.Check("solo se vuelve a leer la vela abierta", string.Join(",", velas.Select((v, i) => v.Lecturas - lecturas[i]).Distinct()) + " / última +" + (velas[^1].Lecturas - lecturas[^1]), "0,1 / última +1");
		c.Check("y el perfil cambia en vivo (nuevo POC)", Etiquetas(ind.TestRender()).First(e => e.StartsWith("POC")), "POC 15019.00");

		var antes = velas.Select(v => v.Lecturas).ToArray();
		velas.Add(Vela(T0.AddHours(20), 15000m, 15001m, _ => 1));
		ind.TestCalc(velas.Count - 1);
		c.Check("vela nueva: la anterior se suma con sus valores finales (una lectura) y la nueva se lee",
			$"{velas[^2].Lecturas - antes[^1]} {velas[^1].Lecturas} resto {velas.Take(velas.Count - 2).Sum(v => v.Lecturas) - antes.Take(antes.Length - 1).Sum()}", "1 1 resto 0");

		var limitado = Crear(Velas(), ajustes: i => i.UseCurrentBarAsEnd = false);
		var abierta = limitado.TestCandles[^1].Lecturas;
		Thread.Sleep(300);
		limitado.TestCalc(limitado.TestCandles.Count - 1);
		c.Check("sin «hasta la vela actual»: la vela abierta no entra en el perfil", (limitado.TestCandles[^1].Lecturas - abierta).ToString(), "0");
	}

	private static void Alertas(Comprobador c)
	{
		c.Seccion("Indicador: alertas una vez por nivel y por vela, nunca durante la carga");

		var velas = Velas();
		var ind = Crear(velas, ajustes: i => i.AlertOnPOC = true);
		c.Check("la vela actual ya tocaba el POC al cargar: no suena", ind.Alertas.Count.ToString(), "0");

		velas.Add(Vela(T0.AddHours(20), 15010m, 15011m, _ => 1));
		ind.TestCalc(velas.Count - 1);
		c.Check("vela nueva que toca el POC: una alerta con el tipo y el precio", string.Join(" | ", ind.Alertas), "alert1|Precio toca POC 15010.50");

		Thread.Sleep(300);
		ind.TestCalc(velas.Count - 1);
		c.Check("la misma vela otra vez: no se repite", ind.Alertas.Count.ToString(), "1");

		velas.Add(Vela(T0.AddHours(21), 15016m, 15017m, _ => 1));
		ind.TestCalc(velas.Count - 1);
		velas.Add(Vela(T0.AddHours(22), 15010m, 15011m, _ => 1));
		ind.TestCalc(velas.Count - 1);
		c.Check("una vela que no lo toca no suena; la siguiente que lo toca, sí", ind.Alertas.Count.ToString(), "2");

		var todas = Crear(Velas(), ajustes: i =>
		{
			i.AlertOnCluster = i.AlertOnEdge = i.AlertOnVA = true;
			i.AlertSoundFile = "alert2";
		});
		todas.TestCandles.Add(Vela(T0.AddHours(20), 15000m, 15020m, _ => 1));
		todas.TestCalc(todas.TestCandles.Count - 1);
		c.Check("varios niveles tocados por la misma vela: una alerta por nivel, con el sonido elegido",
			string.Join(",", todas.Alertas.Select(a => a.Split('|')[1].Replace("Precio toca ", "").Split(' ')[0]).OrderBy(x => x)), "CLU,CLU,cPOC,EDGE,VAH,VAL");
		c.Check("sonido configurable", todas.Alertas.All(a => a.StartsWith("alert2|")).ToString(), "True");
	}

	private static void Herramientas(Comprobador c)
	{
		c.Seccion("Indicador: barra de herramientas (PERFIL, RANGO, ZONA, LVN manual) con el ratón");

		var ind = Crear(Velas());
		ind.TestRender();

		// Barra en (25, 130): botones de 58 px en y 149-165, desde x = 31 cada 62 px.
		static RenderControlMouseEventArgs Raton(int x, int y) => new(x, y);
		var boton = (Func<int, RenderControlMouseEventArgs>)(i => Raton(31 + i * 62 + 29, 157));

		ind.ProcessMouseDown(boton(0));
		c.Check("PERFIL oculta y muestra el perfil", ind.ShowProfile.ToString(), "False");
		ind.ProcessMouseDown(boton(0));

		// LVN manual: clic en el botón y luego en el precio 15016.
		ind.ProcessMouseDown(boton(3));
		ind.ProcessMouseDown(Raton(600, YDe(15016m)));
		c.Check("LVN: un clic en el gráfico añade una línea amarilla en ese precio", ind.ManualLVNPrices, "15016.00");
		ind.TestCalcAll();
		c.Check("y cuenta como LVN (etiqueta LVN MANUAL)", Etiquetas(ind.TestRender()).Contains("LVN MANUAL 15016.00").ToString(), "True");
		ind.ProcessMouseDown(Raton(700, YDe(15016m) + 3));
		c.Check("un clic sobre la línea la quita", ind.ManualLVNPrices, "");
		ind.ProcessMouseDown(boton(3));

		// RANGO: arrastre de la vela 5 a la 12 (x = 53 a 123).
		ind.ProcessMouseDown(boton(1));
		ind.ProcessMouseDown(Raton(53, 400));
		ind.ProcessMouseMove(Raton(123, 400));
		ind.ProcessMouseUp(Raton(123, 400));
		c.Check("RANGO: fechas de las velas 5 y 12 en hora del gráfico (UTC-6) y fin fijo",
			$"{ind.RangeMode} {ind.StartDateTime:dd HH:mm} {ind.EndDateTime:dd HH:mm} {ind.UseCurrentBarAsEnd}", "CustomDateTime 26 13:00 26 20:00 False");
		ind.TestCalcAll();
		c.Check("el perfil pasa a tener 8 velas", Estado(ind.TestRender()).Split(' ')[0], "8");

		// ZONA: rectángulo de 15020 a 15010.
		ind.ProcessMouseDown(boton(2));
		ind.ProcessMouseDown(Raton(300, YDe(15020m)));
		ind.ProcessMouseMove(Raton(500, YDe(15010m)));
		ind.ProcessMouseUp(Raton(500, YDe(15010m)));
		c.Check("ZONA: precios del rectángulo y zona activada", $"{ind.UseSearchZone} {ind.SearchZoneHigh} {ind.SearchZoneLow}", "True 15020.00 15010.00");

		var antes = (ind.ToolbarX, ind.ToolbarY);
		ind.ProcessMouseDown(Raton(60, 135));
		ind.ProcessMouseMove(Raton(360, 435));
		ind.ProcessMouseUp(Raton(360, 435));
		c.Check("la barra se mueve arrastrando su título", $"{antes} → {(ind.ToolbarX, ind.ToolbarY)}", "(25, 130) → (325, 430)");

		var visible = Crear(Velas(), ajustes: i =>
		{
			i.RangeMode = RangeModes.VisibleRange;
			i.FirstVisibleBarNumber = 5;
			i.LastVisibleBarNumber = 9;
		});
		var conCinco = Estado(visible.TestRender()).Split(' ')[0];
		visible.FirstVisibleBarNumber = 0;
		visible.LastVisibleBarNumber = 19;
		c.Check("rango visible: al desplazar el gráfico el perfil sigue a la pantalla", $"{conCinco} → {Estado(visible.TestRender()).Split(' ')[0]}", "5 → 20");

		var filtro = Crear(Velas(), ajustes: i =>
		{
			i.UseSearchZone = true;
			i.SearchZoneHigh = 15020;
			i.SearchZoneLow = 15000;
			i.UseTimeFilter = true;
			i.SearchStartTime = new DateTime(2030, 1, 1);
			i.SearchEndTime = new DateTime(2030, 1, 2);
		});
		c.Check("filtro de tiempo sin velas: los detectores no ven nada y se avisa",
			$"{Etiquetas(filtro.TestRender()).Count(e => e.StartsWith("CLU"))} {Estado(filtro.TestRender()).EndsWith("FILTRO DE TIEMPO SIN VELAS")}", "0 True");
	}
}
