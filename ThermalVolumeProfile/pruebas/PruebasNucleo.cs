using System;
using System.Collections.Generic;
using System.Linq;

using Termico;

using static Datos;

/// <summary>Núcleo (MotorTermico.cs): perfil, POC/VA, color, EDGE, SPIKE, CLÚSTER, LVN y rangos.</summary>
internal static class PruebasNucleo
{
	private static readonly DateTime T0 = new(2026, 10, 26, 14, 0, 0);

	public static void Ejecutar(Comprobador c)
	{
		PerfilYFilas(c);
		PocYVa(c);
		Color(c);
		Edges(c);
		Spikes(c);
		Clusters(c);
		Lvn(c);
		RangosDeTiempo(c);
	}

	private static void PerfilYFilas(Comprobador c)
	{
		c.Seccion("A) Perfil: filas, huecos a 0, AutoRowSize, velas sin niveles, TPO, Delta, vela abierta");

		var ac = new AcumuladorTicks(Tick, 30);
		ac.Agregar(VP(T0, (15000m, 10, 10), (15000.75m, 5, 5)));
		var p = Perfil.Construir(ac, null, ProfileTypes.Volume, 1, false, 300)!;
		c.Check("fila = Floor(precio / tick); filas intermedias a 0", string.Join(",", p.Valor), "20,0,0,10");
		c.Check("índice 0 = precio más bajo", Precio(p.PrecioFila(0)) + " " + Precio(p.PrecioFila(3)), "15000.00 15000.75");

		var dos = Perfil.Construir(ac, null, ProfileTypes.Volume, 2, false, 300)!;
		c.Check("RowSizeTicks 2: 15000+15000.25 | 15000.50+15000.75", string.Join(",", dos.Valor) + " desde " + Precio(dos.PrecioFila(0)), "20,10 desde 15000.00");

		var ancho = new AcumuladorTicks(Tick, 30);
		ancho.Agregar(VP(T0, (15169m, 1, 1), (15451m, 1, 1)));
		var auto = Perfil.Construir(ancho, null, ProfileTypes.Volume, 1, true, 300)!;
		c.Check("AutoRowSize: 1129 ticks y 300 filas máx → 4 ticks por fila", $"{auto.TicksPorFila} {auto.Filas}", "4 283");

		var sinNiveles = new AcumuladorTicks(Tick, 30);
		var conNiveles = sinNiveles.Agregar(new VelaPerfil { Time = T0, Low = 15000m, High = 15001m, Volume = 100, Delta = 20 });
		var reparto = Perfil.Construir(sinNiveles, null, ProfileTypes.Volume, 1, false, 300)!;
		c.Check("vela sin niveles: 100 repartidos en 5 precios (y se informa)", $"{conNiveles} {string.Join(",", reparto.Valor)}", "False 20,20,20,20,20");
		c.Check("sin niveles: Ask = (V + delta) / 2 repartido", string.Join(",", Perfil.Construir(sinNiveles, null, ProfileTypes.Ask, 1, false, 300)!.Valor), "12,12,12,12,12");

		var tpo = new AcumuladorTicks(Tick, 30);
		tpo.Agregar(VP(T0, (15000m, 1, 1), (15000.25m, 1, 1)));
		tpo.Agregar(VP(T0.AddMinutes(10), (15000m, 1, 1)));
		tpo.Agregar(VP(T0.AddMinutes(30), (15000m, 1, 1)));
		c.Check("TPO: cada periodo de 30 min cuenta una vez por precio", string.Join(",", Perfil.Construir(tpo, null, ProfileTypes.TPO, 1, false, 300)!.Valor), "2,1");

		var delta = new AcumuladorTicks(Tick, 30);
		delta.Agregar(VP(T0, (15000m, 30, 10), (15000.25m, 5, 25)));
		var pd = Perfil.Construir(delta, null, ProfileTypes.Delta, 1, false, 300)!;
		c.Check("Delta: valor absoluto para el largo, signo aparte", $"{string.Join(",", pd.Valor)} / {string.Join(",", pd.DeltaFila)}", "20,20 / -20,20");
		c.Check("Bid y Ask por separado",
			string.Join(",", Perfil.Construir(delta, null, ProfileTypes.Bid, 1, false, 300)!.Valor) + " | " + string.Join(",", Perfil.Construir(delta, null, ProfileTypes.Ask, 1, false, 300)!.Valor),
			"30,5 | 10,25");

		var abierta = ac.Aportacion(VP(T0.AddMinutes(1), (15000.25m, 2, 2)), out _);
		var vivo = Perfil.Construir(ac, abierta, ProfileTypes.Volume, 1, false, 300)!;
		c.Check("vela abierta: se suma sin tocar el acumulado de las cerradas",
			$"{string.Join(",", vivo.Valor)} | {string.Join(",", Perfil.Construir(ac, null, ProfileTypes.Volume, 1, false, 300)!.Valor)}", "20,4,0,10 | 20,0,0,10");
		c.Check("acumulado vacío: sin perfil", (Perfil.Construir(new AcumuladorTicks(Tick, 30), null, ProfileTypes.Volume, 1, true, 300) is null).ToString(), "True");
	}

	private static void PocYVa(Comprobador c)
	{
		c.Seccion("B) POC / VA (método estándar CME)");

		c.Check("POC = fila de mayor volumen", MotorTermico.Poc(new double[] { 1, 2, 3, 10, 4, 2, 1 }).ToString(), "3");
		c.Check("empate: el más cercano al centro", MotorTermico.Poc(new double[] { 9, 1, 1, 9, 1, 1, 1, 9 }).ToString(), "3");

		// Total 23, 70 % = 16,1. POC 10; arriba 4+2=6 > abajo 3+2=5 → 16; arriba 1 < abajo 5 → 21.
		c.Check("VA: suma de 2 filas arriba contra 2 abajo hasta el 70 %", MotorTermico.AreaDeValor(new double[] { 1, 2, 3, 10, 4, 2, 1 }, 3, 70).ToString(), "(1, 5)");
		c.Check("VA al 95 %: 22 de 23 (la fila 0 queda fuera)", MotorTermico.AreaDeValor(new double[] { 1, 2, 3, 10, 4, 2, 1 }, 3, 95).ToString(), "(1, 6)");
		c.Check("porcentaje fuera de 50-95 se ajusta (40 → 50)", MotorTermico.AreaDeValor(new double[] { 1, 1, 1, 10, 1, 1, 1 }, 3, 40).ToString(), "(3, 3)");
	}

	private static void Color(Comprobador c)
	{
		c.Seccion("C) Color térmico: 4 colores, escala y caché de 256");

		var cuatro = PaletaTermica.Colores(ThermalPalettes.YellowRed, null);
		var lineal = PaletaTermica.Cache(cuatro, ThermalScales.Linear, 180);
		c.Check("256 colores precalculados con la opacidad", $"{lineal.Length} {lineal[0].A}", "256 180");
		c.Check("n = 0: amarillo claro; n = 1: rojo oscuro", $"{lineal[0]} | {lineal[255]}",
			"ColorArgb { A = 180, R = 255, G = 250, B = 160 } | ColorArgb { A = 180, R = 150, G = 0, B = 0 }");
		c.Check("lineal: n = 0,66 cae en el naranja (color medio 2)", lineal[PaletaTermica.Indice(0.66)].ToString(), "ColorArgb { A = 180, R = 255, G = 120, B = 0 }");
		c.Check("raíz cuadrada: n = 0,25 → 0,5 (más color con poco volumen)", PaletaTermica.Escalar(0.25, ThermalScales.SquareRoot).ToString("0.00"), "0.50");
		c.Check("logarítmica: n = 0,1 → log10(1,9)", PaletaTermica.Escalar(0.1, ThermalScales.Logarithmic).ToString("0.000"), "0.279");
		c.Check("paleta personalizada", PaletaTermica.Colores(ThermalPalettes.Custom, new ColorArgb[] { new(1, 1, 1, 1), new(2, 2, 2, 2), new(3, 3, 3, 3), new(4, 4, 4, 4) })[3].ToString(), "ColorArgb { A = 4, R = 4, G = 4, B = 4 }");
	}

	private static void Edges(Comprobador c)
	{
		c.Seccion("D) EDGE: escalón brusco sí, rampa no; la línea en la última fila de alto volumen");

		var parametros = new ParametrosTermicos();

		// Bloque HVN filas 20-49 (100) entre zonas de 10.
		var escalon = PerfilDe(Tramos(70, 10, (20, 49, 100)));
		var edges = MotorTermico.DetectarEdges(escalon, parametros, null);
		c.Check("dos EDGE: arriba en la fila 49 y abajo en la 20",
			string.Join(" | ", edges.OrderByDescending(e => e.Fila).Select(e => $"{(e.Superior ? "ARRIBA" : "ABAJO")} {e.Fila}")), "ARRIBA 49 | ABAJO 20");
		c.Check("etiqueta", edges.First(e => e.Superior).Etiqueta(Precio), "EDGE ARRIBA 15012.25");
		c.Check("step y slope por encima de los mínimos", edges.All(e => e.Step >= 0.30 && e.Slope >= 0.08).ToString(), "True");

		// El mismo bloque, pero bajando en rampa (3 % del POC por fila) por arriba.
		var rampa = Tramos(100, 10, (20, 49, 100));

		for (var i = 50; i < 80; i++)
			rampa[i] = 100 - 3 * (i - 49);

		var conRampa = MotorTermico.DetectarEdges(PerfilDe(rampa), parametros, null);
		c.Check("rampa (caída lenta): sin EDGE arriba; el de abajo sigue", string.Join(",", conRampa.Select(e => e.Superior ? "ARRIBA" : "ABAJO")), "ABAJO");

		var suavizadoFuerte = MotorTermico.DetectarEdges(escalon, new ParametrosTermicos { EdgeSmoothSigma = 6 }, null);
		c.Check("un suavizado fuerte convierte el escalón en rampa", suavizadoFuerte.Count.ToString(), "0");

		var bajo = MotorTermico.DetectarEdges(PerfilDe(Tramos(70, 10, (20, 24, 100))), parametros, null);
		c.Check("HVN más bajo que EdgeMinHvnHeightTicks (5 < 8 ticks): descartado", bajo.Count.ToString(), "0");

		var unidos = MotorTermico.DetectarEdges(PerfilDe(Tramos(80, 10, (20, 39, 100), (42, 60, 100))), parametros, null);
		c.Check("dos HVN separados por 2 ticks (< 3) se unen: sin EDGE en el hueco", string.Join(",", unidos.Select(e => e.Fila).OrderBy(f => f)), "20,60");

		var zona = MotorTermico.DetectarEdges(escalon, parametros, ZonaBusqueda.Crear(15010m, 15020m));
		c.Check("zona de búsqueda: solo el EDGE de 15012.25", string.Join(",", zona.Select(e => Precio(e.Precio))), "15012.25");
	}

	private static void Spikes(Comprobador c)
	{
		c.Seccion("E) SPIKE: 1 a 3 filas muy por encima de sus 5 + 5 vecinas (vol[] sin suavizar)");

		var v = Tramos(60, 20, (30, 30, 100), (45, 46, 60));
		var perfil = PerfilDe(v);
		var espigas = MotorTermico.BuscarEspigas(perfil, 100, 0.40, 1.8, 3, null);
		c.Check("espiga de 1 fila (n 1, 5 × la media) y de 2 filas (n 0,6, 3 ×)",
			string.Join(" | ", espigas.Select(e => $"{e.Fila} {e.Desde}-{e.Hasta} n{e.N:0.00} x{e.Ratio:0.0}")), "30 30-30 n1.00 x5.0 | 45 45-46 n0.60 x3.0");
		c.Check("score = n × ratio", espigas[0].Score.ToString("0.0"), "5.0");

		var ancha = MotorTermico.BuscarEspigas(PerfilDe(Tramos(60, 20, (30, 34, 100))), 100, 0.40, 1.8, 3, null);
		c.Check("bloque de 5 filas: no es una espiga (más ancho que SpikeMaxWidthRows)", ancha.Count.ToString(), "0");

		var floja = MotorTermico.BuscarEspigas(PerfilDe(Tramos(60, 60, (30, 30, 100))), 100, 0.40, 1.8, 3, null);
		c.Check("1,67 × la media de sus vecinas (< 1,8): no", floja.Count.ToString(), "0");

		var baja = MotorTermico.BuscarEspigas(PerfilDe(Tramos(60, 5, (20, 20, 100), (30, 30, 30))), 100, 0.40, 1.8, 3, null);
		c.Check("n 0,30 (< SpikeMinN 0,40): no", string.Join(",", baja.Select(e => e.Fila)), "20");
	}

	/// <summary>Peine de espigas dentro de un HVN: filas 20-56 a 70, espigas de 120 cada 6 filas (la de 42 a 130).</summary>
	public static double[] Peine()
	{
		var v = Tramos(81, 5, (20, 56, 70));

		foreach (var f in new[] { 30, 36, 42, 48, 54 })
			v[f] = f == 42 ? 130 : 120;

		return v;
	}

	private static void Clusters(Comprobador c)
	{
		c.Seccion("F) CLÚSTER: espigas agrupadas, 2 líneas, cPOC y ajuste al EDGE");

		var perfil = PerfilDe(Peine());
		var parametros = new ParametrosTermicos();
		var edges = MotorTermico.DetectarEdges(perfil, parametros, null);
		c.Check("EDGE del HVN: arriba en la fila 56, abajo en la 20", string.Join(",", edges.Select(e => e.Fila).OrderBy(f => f)), "20,56");

		var clusters = MotorTermico.DetectarClusters(perfil, 130, parametros, null, edges);
		var k = clusters.Single();
		c.Check("un clúster de 5 espigas", $"{k.Espigas.Count} {string.Join(",", k.Espigas.Select(e => e.Fila))}", "5 30,36,42,48,54");
		c.Check("línea superior: espiga 54 ajustada al EDGE de 56 (a 2 filas); inferior: espiga 30 (el EDGE de 20 está a 10)",
			$"{k.FilaArriba} {k.ArribaEnEdge} | {k.FilaAbajo} {k.AbajoEnEdge}", "56 True | 30 False");
		c.Check("cPOC = espiga de más volumen", $"{k.FilaPoc} {Precio(k.Poc)}", "42 15010.50");
		c.Check("el EDGE absorbido ya no se dibuja aparte", string.Join(",", edges.Select(e => $"{e.Fila}:{e.Absorbido}").OrderBy(x => x)), "20:False,56:True");
		c.Check("score = número de espigas × media de n", k.Score.ToString("0.00"), (5 * (4 * 120 / 130.0 + 1) / 5).ToString("0.00"));

		var sinAjuste = MotorTermico.DetectarClusters(perfil, 130, new ParametrosTermicos { EdgeSnapRows = 1 }, null, MotorTermico.DetectarEdges(perfil, parametros, null)).Single();
		c.Check("EdgeSnapRows 1: la línea se queda en la espiga más alta", sinAjuste.FilaArriba.ToString(), "54");

		var corto = MotorTermico.DetectarClusters(perfil, 130, new ParametrosTermicos { MinClusterHeightTicks = 30 }, null, edges);
		c.Check("altura 24 ticks < MinClusterHeightTicks 30: no hay clúster", corto.Count.ToString(), "0");

		var separadas = MotorTermico.DetectarClusters(perfil, 130, new ParametrosTermicos { ClusterGapTicks = 5 }, null, edges);
		c.Check("espigas a 6 ticks con ClusterGapTicks 5: no se agrupan", separadas.Count.ToString(), "0");

		var r = MotorTermico.Analizar(perfil, null, new ParametrosTermicos { SpikeMinN = 0.4 }, null);
		c.Check("análisis completo: el clúster oculta sus espigas; EDGE de abajo dibujado aparte",
			$"clu {r.Clusters.Count} spk {r.Spikes.Count} edges {string.Join(",", r.Edges.Select(e => e.Etiqueta(Precio)))}", "clu 1 spk 0 edges EDGE ABAJO 15005.00");

		var conEspigas = MotorTermico.Analizar(perfil, null, new ParametrosTermicos { HideSpikesInCluster = false, SpikeRatio = 1.6 }, null);
		c.Check("HideSpikesInCluster apagado: las espigas del clúster también se dibujan (máx 5)", conEspigas.Spikes.Count.ToString(), "5");

		var fuera = MotorTermico.Analizar(perfil, null, parametros, ZonaBusqueda.Crear(16000m, 16100m));
		c.Check("zona de búsqueda fuera del perfil: ningún nivel detectado (POC y VA sí)",
			$"{fuera.Clusters.Count} {fuera.Edges.Count} {fuera.Spikes.Count} {fuera.Lvns.Count} POC {Precio(fuera.PrecioPoc)}", "0 0 0 0 POC 15010.50");
	}

	private static void Lvn(Comprobador c)
	{
		c.Seccion("G) LVN: valle entre dos picos, meseta al centro, prominencia, zona y LVN manuales");

		// Pico 100 (filas 0-19), valle 8 (20-26), pico 80 (27-49).
		var v = Tramos(50, 100, (20, 26, 8), (27, 49, 80));
		var perfil = PerfilDe(v);
		var parametros = new ParametrosTermicos();
		var lvn = MotorTermico.DetectarLvn(perfil, parametros, null, null);
		c.Check("un LVN, en el centro de la meseta (fila 23)", string.Join(",", lvn.Select(l => $"{l.Ranking}:{l.Fila}")), "1:23");
		c.Check("prominencia = (pico menor − valle) / pico menor, suavizado", lvn[0].Prominencia.ToString("0.00"), "0.88");
		c.Check("franja: hasta un 25 % de la subida hacia el pico menor", $"{lvn[0].Inferior}-{lvn[0].Superior}", "21-25");

		var poco = MotorTermico.DetectarLvn(PerfilDe(Tramos(50, 100, (20, 26, 90), (27, 49, 95))), parametros, null, null);
		c.Check("valle poco profundo (prominencia < 20 %): no", poco.Count.ToString(), "0");

		var alto = MotorTermico.DetectarLvn(PerfilDe(Tramos(50, 100, (20, 26, 50))), parametros, null, null);
		c.Check("valle al 50 % del POC (> 35 %): no", alto.Count.ToString(), "0");

		var enZona = MotorTermico.DetectarLvn(perfil, parametros, ZonaBusqueda.Crear(15005m, 15006.50m), null);
		c.Check("zona solo sobre el valle: los picos de referencia pueden quedar fuera", enZona.Count.ToString(), "1");

		var manual = MotorTermico.DetectarLvn(perfil, parametros, null, new List<decimal> { 15006.00m, 15011.00m });
		c.Check("LVN manuales: cuentan como LVN y anulan el automático a menos de 8 ticks",
			string.Join(" | ", manual.Select(l => $"{(l.Manual ? "MANUAL" : "AUTO " + l.Ranking)} {Precio(l.Precio)}")), "MANUAL 15011.00 | MANUAL 15006.00");

		var sinAuto = MotorTermico.DetectarLvn(perfil, new ParametrosTermicos { EnableLVN = false }, null, new List<decimal> { 15011.00m });
		c.Check("con los LVN automáticos apagados, los manuales siguen", sinAuto.Count.ToString(), "1");

		var varios = Tramos(120, 100, (20, 22, 5), (30, 32, 5), (60, 62, 5), (90, 92, 5));
		var lista = MotorTermico.DetectarLvn(PerfilDe(varios), new ParametrosTermicos { MaxLVN = 2, LVNMinDistanceTicks = 15 }, null, null);
		c.Check("separación mínima 15 ticks (21 y 31 chocan) y MaxLVN 2", lista.Count.ToString(), "2");
	}

	private static void RangosDeTiempo(Comprobador c)
	{
		c.Seccion("A.1) Rango del perfil: búsqueda binaria, modos y hora del gráfico (cualquier temporalidad)");

		// 492 velas de 30 min desde el jueves 22-10-2026 00:00 UTC hasta el domingo 01-11 05:30 UTC (cruza el cambio de
		// horario de Nueva York); hora del gráfico = UTC-6 (Ciudad de México).
		var inicio = new DateTime(2026, 10, 22, 0, 0, 0);
		const int n = 492;
		var tiempos = Enumerable.Range(0, n).Select(i => inicio.AddMinutes(30 * i)).ToArray();
		DateTime T(int i) => tiempos[i];
		var offset = TimeSpan.FromHours(-6);

		c.Check("búsqueda binaria: primera desde y última hasta",
			$"{Rangos.PrimeraDesde(T, n, inicio.AddMinutes(45))} {Rangos.UltimaHasta(T, n, inicio.AddMinutes(45))} {Rangos.UltimaHasta(T, n, inicio.AddDays(-1))}", "2 1 -1");

		var custom = new ParametrosRango { Desde = new DateTime(2026, 10, 26, 0, 0, 0), Hasta = new DateTime(2026, 10, 26, 3, 0, 0), OffsetGrafico = offset, UseCurrentBarAsEnd = false };
		c.Check("fecha y hora en hora del gráfico (UTC-6): 00:00-03:00 → velas de 06:00-09:00 UTC", Rangos.Resolver(custom, T, n).ToString(), "(204, 210)");
		custom.UseCurrentBarAsEnd = true;
		c.Check("«hasta la vela actual»: hasta la última", Rangos.Resolver(custom, T, n).ToString(), "(204, 491)");

		var futuro = new ParametrosRango { Desde = new DateTime(2027, 1, 1), Hasta = new DateTime(2027, 2, 1), UseCurrentBarAsEnd = false };
		c.Check("rango vacío (fechas sin velas): null", (Rangos.Resolver(futuro, T, n) is null).ToString(), "True");

		c.Check("últimas 200 velas", Rangos.Resolver(new ParametrosRango { Modo = RangeModes.LastNBars }, T, n).ToString(), "(292, 491)");
		c.Check("sin la vela actual: el perfil no cambia en vivo", Rangos.Resolver(new ParametrosRango { Modo = RangeModes.LastNBars, UseCurrentBarAsEnd = false }, T, n).ToString(), "(291, 490)");
		c.Check("rango visible", Rangos.Resolver(new ParametrosRango { Modo = RangeModes.VisibleRange, PrimeraVisible = 40, UltimaVisible = 80 }, T, n).ToString(), "(40, 80)");

		// Última vela = 01-11-2026 05:30 UTC = domingo 01:30 en Nueva York (aún EDT; el cambio es a las 06:00 UTC).
		var dia = Rangos.Resolver(new ParametrosRango { Modo = RangeModes.Day }, T, n)!.Value;
		c.Check("día operativo (Nueva York + 7 h)", $"{dia} {T(dia.Desde):MM-dd HH:mm}", "(474, 491) 10-31 21:00");

		var semana = Rangos.Resolver(new ParametrosRango { Modo = RangeModes.Week }, T, n)!.Value;
		c.Check("semana operativa (empieza el lunes 26, es decir, el domingo 25 a las 17:00 NY)", $"{T(semana.Desde):MM-dd HH:mm}", "10-25 21:00");

		var mes = Rangos.Resolver(new ParametrosRango { Modo = RangeModes.Month }, T, n)!.Value;
		c.Check("mes operativo (noviembre empieza el 31-10 a las 17:00 de Nueva York)", $"{T(mes.Desde):MM-dd HH:mm}", "10-31 21:00");

		// Sesión 09:30-16:00 de Nueva York (las velas sintéticas también tienen fin de semana): el sábado 31-10 (EDT).
		var sesion = Rangos.Resolver(new ParametrosRango { Modo = RangeModes.Session }, T, n)!.Value;
		c.Check("sesión NY más reciente con velas (09:30-16:00 EDT = 13:30-20:00 UTC)", $"{T(sesion.Desde):MM-dd HH:mm}-{T(sesion.Hasta):HH:mm}", "10-31 13:30-19:30");

		var nocturna = Rangos.Resolver(new ParametrosRango { Modo = RangeModes.Session, SessionStart = new TimeSpan(18, 0, 0), SessionEnd = new TimeSpan(2, 0, 0) }, T, n)!.Value;
		c.Check("sesión que cruza la medianoche (18:00-02:00 NY)", $"{T(nocturna.Desde):MM-dd HH:mm}-{T(nocturna.Hasta):MM-dd HH:mm}", "10-31 22:00-11-01 05:30");

		// Gráfico de ticks: muchas velas con la misma hora; la búsqueda sigue funcionando.
		var ticks = new[] { inicio, inicio, inicio, inicio.AddSeconds(1), inicio.AddSeconds(1), inicio.AddSeconds(5) };
		c.Check("gráfico de ticks (horas repetidas)",
			Rangos.Resolver(new ParametrosRango { Desde = inicio.AddSeconds(1), Hasta = inicio.AddSeconds(4), UseCurrentBarAsEnd = false }, i => ticks[i], ticks.Length).ToString(), "(3, 4)");
	}
}
