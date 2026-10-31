using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using ATAS.Indicators;
using ATAS.Indicators.Custom;

using Termico;

using static Datos;

/// <summary>
/// 12. Prueba de validación (análoga, con datos sintéticos) y rendimiento. La prueba real del documento usa el gráfico
/// NQ de 60 minutos del propietario, que no está disponible aquí: este perfil se construyó para parecerse a la
/// descripción (bloque de alto volumen 15169-15451 con un peine de espigas entre 15206.25 y 15435.50 y el cPOC en
/// 15297.50) y comprueba que, con los valores por defecto, la cadena completa los encuentra con 1 a 3 ticks de margen.
/// </summary>
internal static class PruebasValidacion
{
	private static readonly DateTime T0 = new(2023, 11, 1, 0, 0, 0);

	public static void Ejecutar(Comprobador c)
	{
		Validacion(c);
		Rendimiento(c);
	}

	/// <summary>Volumen por tick del perfil sintético NQ (15165.00 a 15464.75: 1200 ticks → filas de 4 ticks).</summary>
	private static Dictionary<decimal, decimal> PerfilNq()
	{
		var espigas = new List<decimal>();

		for (var p = 15206m; p <= 15293m; p += 3)
			espigas.Add(p);

		espigas.AddRange(new[] { 15295m, 15297m, 15299m });

		for (var p = 15302m; p <= 15431m; p += 3)
			espigas.Add(p);

		espigas.AddRange(new[] { 15433m, 15435m });

		// Tick de cada espiga dentro de su fila de 1 punto: los extremos y el cPOC en los precios del documento.
		decimal TickDe(decimal fila) => fila switch { 15206m => 15206.25m, 15435m => 15435.50m, 15297m => 15297.50m, _ => fila };

		var volumen = new Dictionary<decimal, decimal>();

		for (var p = 15165m; p <= 15464.75m; p += Tick)
			volumen[p] = p >= 15169m && p <= 15451.75m ? 7.5m : 3m;

		foreach (var e in espigas)
			volumen[TickDe(e)] += e == 15297m ? 110 : 70;

		return volumen;
	}

	private static void Validacion(Comprobador c)
	{
		c.Seccion("12. Validación (análoga): NQ 60 min, bloque 15169-15451, valores por defecto");

		var objetivo = PerfilNq();
		var precios = objetivo.Keys.OrderBy(p => p).ToList();

		// 150 velas de 60 min de 40 puntos cada una recorriendo el rango; cada precio reparte su volumen entre las que lo cubren.
		const int n = 150;
		var tramos = Enumerable.Range(0, n).Select(i =>
		{
			var bajo = 15165m + Math.Round((decimal)i * 259.75m / (n - 1) / Tick) * Tick;
			return (Bajo: bajo, Alto: Math.Min(15464.75m, bajo + 40));
		}).ToList();
		var cobertura = precios.ToDictionary(p => p, p => tramos.Count(t => p >= t.Bajo && p <= t.Alto));
		var velas = tramos.Select((t, i) => Vela(T0.AddHours(i), t.Bajo, t.Alto, p => objetivo[p] / cobertura[p])).ToList();
		c.Check("todas las velas cubren el rango completo del perfil", cobertura.Values.All(v => v > 0).ToString(), "True");

		var ind = new ThermalVolumeProfile
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick, TimeZoneOffset = TimeSpan.FromHours(-6) },
			ChartInfo = new ChartInfo { TestYByPrice = p => (int)Math.Round((15480 - p) * 2.5m) },
			TestCandles = velas,
			FirstVisibleBarNumber = 0,
			LastVisibleBarNumber = n - 1,
			StartDateTime = T0.AddDays(-1)
		};

		ind.TestCalcAll();
		var r = ind.TestRender();
		var etiquetas = r.Texts.Where(t => t.Rect.Right == 1196).Select(t => t.Text).ToList();
		Console.WriteLine($"      (etiquetas: {string.Join(" | ", etiquetas)})");
		Console.WriteLine($"      (estado: {r.Texts.Last().Text})");

		decimal? Nivel(string prefijo) => etiquetas.Where(e => e.StartsWith(prefijo + " ")).Select(e => decimal.Parse(e[(prefijo.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture)).Cast<decimal?>().FirstOrDefault();

		void Margen(string nombre, decimal? obtenido, decimal esperado)
		{
			var ticks = obtenido is { } o ? Math.Abs(o - esperado) / Tick : -1;
			c.Check($"{nombre} {Precio(esperado)} con 1 a 3 ticks de margen", obtenido is not null && ticks <= 3, obtenido is null ? "no aparece" : $"{Precio(obtenido.Value)} ({ticks:0} ticks)");
		}

		Margen("CLU ARRIBA", Nivel("CLU ARRIBA"), 15435.50m);
		Margen("CLU ABAJO", Nivel("CLU ABAJO"), 15206.25m);
		Margen("cPOC", Nivel("cPOC"), 15297.50m);
		c.Check("filas de 4 ticks (AutoRowSize con 300 filas como máximo)", r.Texts.Last().Text.Contains("300 filas de 4 ticks").ToString(), "True");
	}

	private static void Rendimiento(Comprobador c)
	{
		c.Seccion("11. Rendimiento: 90 días de velas de 1 minuto con volumen por precio");

		// Paseo aleatorio reproducible: 90 días × 23 h × 60 min, unos 16 precios por vela.
		var azar = new Random(11);
		var velas = new List<IndicatorCandle>();
		var precio = 20000m;
		var inicio = new DateTime(2026, 8, 1);

		for (var i = 0; i < 90 * 23 * 60; i++)
		{
			var bajo = precio - azar.Next(0, 8) * Tick;
			var alto = precio + azar.Next(0, 8) * Tick;
			velas.Add(Vela(inicio.AddMinutes(i), bajo, alto, p => azar.Next(1, 40)));
			precio = Math.Round((precio + (decimal)(azar.NextDouble() - 0.5) * 6) / Tick) * Tick;
		}

		var ind = new ThermalVolumeProfile
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick },
			ChartInfo = new ChartInfo { TestYByPrice = p => (int)Math.Round((precio + 300 - p) * 1.2m) },
			TestCandles = velas,
			FirstVisibleBarNumber = velas.Count - 300,
			LastVisibleBarNumber = velas.Count - 1,
			StartDateTime = inicio.AddDays(-1)
		};

		var reloj = Stopwatch.StartNew();
		ind.TestCalcAll();
		var carga = reloj.Elapsed;
		var r = ind.TestRender();
		Console.WriteLine($"      ({velas.Count:N0} velas; carga {carga.TotalSeconds:0.00} s; {r.Texts.Last().Text})");
		c.Check("la carga completa tarda menos de 10 s", carga.TotalSeconds < 10, $"{carga.TotalSeconds:0.0} s");

		Thread.Sleep(300);
		velas[^1].Levels[precio] = (50, 50);
		reloj.Restart();
		ind.TestCalc(velas.Count - 1);
		var vivo = reloj.Elapsed.TotalMilliseconds;

		velas.Add(Vela(inicio.AddMinutes(velas.Count), precio, precio + 1, _ => 5));
		reloj.Restart();
		ind.TestCalc(velas.Count - 1);
		var nueva = reloj.Elapsed.TotalMilliseconds;
		Console.WriteLine($"      (actualización en vivo {vivo:0.0} ms; vela nueva {nueva:0.0} ms)");
		c.Check("cada actualización en vivo tarda menos de 100 ms", vivo < 100 && nueva < 100, "");

		reloj.Restart();

		for (var i = 0; i < 20; i++)
			ind.TestRender();

		var dibujo = reloj.Elapsed.TotalMilliseconds / 20;
		c.Check("cada dibujo tarda menos de 20 ms", dibujo < 20, "");
		Console.WriteLine($"      (dibujo {dibujo:0.00} ms)");

		// Diario: velas muy amplias; AutoRowSize mantiene el perfil en 300 filas como máximo.
		var diario = Enumerable.Range(0, 300).Select(i => Vela(inicio.AddDays(i), 15000m + i * 10, 15000m + i * 10 + 300, _ => 1)).ToList();
		var d = new ThermalVolumeProfile
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick },
			ChartInfo = new ChartInfo(),
			TestCandles = diario,
			RangeMode = ModosRango.LastNBars,
			LastNBars = 300
		};

		d.TestCalcAll();
		var estado = d.TestRender().Texts.Last().Text;
		c.Check("gráfico diario de 3300 puntos: AutoRowSize lo deja en 300 filas como máximo", estado.Contains(" 300 filas") || estado.Contains(" 299 filas"), estado);
	}
}
