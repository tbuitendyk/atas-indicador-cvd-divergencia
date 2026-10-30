using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Completo.Nucleo;

/// <summary>
/// Motor completo sobre un histórico sintético largo (unos 90 días de velas de 1M con volumen por precio, cruzando
/// los dos cambios de horario): estabilidad, no repintado y rendimiento.
/// </summary>
internal static class PruebasMotor
{
	/// <summary>Paseo aleatorio reproducible con horario de Globex (sin fines de semana ni la pausa diaria).</summary>
	public static List<EntradaVela1M> Historico(DateTime desde, int dias, int semilla = 7)
	{
		var azar = new Random(semilla);
		var velas = new List<EntradaVela1M>();
		var precio = 20000m;
		var fin = desde.AddDays(dias);

		for (var t = desde; t < fin; t = t.AddMinutes(1))
		{
			var ny = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), Zonas.NuevaYork);

			// Globex: domingo 18:00 a viernes 17:00 de NY, con pausa diaria de 17:00 a 18:00.
			if (ny.DayOfWeek == DayOfWeek.Saturday || (ny.DayOfWeek == DayOfWeek.Sunday && ny.Hour < 18)
				|| (ny.DayOfWeek == DayOfWeek.Friday && ny.Hour >= 17) || ny.Hour == 17)
			{
				continue;
			}

			var apertura = precio;
			var maximo = precio;
			var minimo = precio;

			for (var paso = 0; paso < 4; paso++)
			{
				precio += (decimal)Math.Round((azar.NextDouble() - 0.5) * 12) * 0.25m - (precio - 20000m) * 0.0005m;
				precio = Math.Round(precio / 0.25m) * 0.25m;
				maximo = Math.Max(maximo, precio);
				minimo = Math.Min(minimo, precio);
			}

			var volumen = (decimal)azar.Next(60, 600);
			var delta = Math.Round((precio - apertura) * 8 + (decimal)azar.Next(-40, 41));
			velas.Add(Velas.V(t, apertura, maximo, minimo, precio, volumen, Math.Clamp(delta, -volumen, volumen),
				p => 1m + (decimal)((p * 7) % 5) / 5m));
		}

		return velas;
	}

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("Motor completo: histórico sintético de ~90 días (estabilidad, no repintado, rendimiento)");

		var historico = Historico(new DateTime(2026, 8, 10, 0, 0, 0), 91);
		var cfg = new ConfiguracionCompleto { Instrumento = "NQZ6", ValoresMonetariosVerificados = true };
		c.Check($"velas de 1M generadas ({historico.Count})", (historico.Count > 80000).ToString(), "True");

		var completo = new MotorCompleto(cfg);
		var reloj = Stopwatch.StartNew();
		Exception? error = null;

		try
		{
			foreach (var v in historico.Take(historico.Count - 1000))
				completo.Procesar(v);
		}
		catch (Exception ex)
		{
			error = ex;
		}

		var carga = reloj.Elapsed;
		reloj.Restart();

		try
		{
			foreach (var v in historico.Skip(historico.Count - 1000))
				completo.Procesar(v);
		}
		catch (Exception ex)
		{
			error ??= ex;
		}

		var porVela = reloj.Elapsed.TotalMilliseconds / 1000;
		c.Check("sin excepciones en todo el histórico", error is null ? "ninguna" : error.GetType().Name + ": " + error.Message, "ninguna");

		Console.WriteLine($"      (carga de {historico.Count - 1000:N0} velas: {carga.TotalSeconds:0.0} s; cada vela nueva después: {porVela:0.00} ms)");
		c.Check("cada vela nueva se procesa en menos de 20 ms", (porVela < 20).ToString(), "True");

		var resumen = completo.Leer(m => new
		{
			Mapas = m.Lvn.Mapas.Count,
			Barridos = m.Barridos.Count,
			Senales = m.Senales.Count,
			Eventos = m.Estructura.Eventos.Count,
			Sesgo = m.Estructura.Sesgo,
			Referencias = m.Liquidez.Referencias.Count,
			Divergencias = m.Divergencias.Divergencias.Count,
			Fvg = m.FvgDetector.Fvgs.Count,
			Descartadas = m.A5.Descartadas + m.A15.Descartadas + m.A60.Descartadas,
			Huecos = m.Huecos
		});

		Console.WriteLine($"      ({resumen})");
		c.Check("un mapa LVN por sesión de NY (unos 64 días laborables)", (resumen.Mapas >= 55).ToString(), "True");
		c.Check("cada mapa tiene amarillos y naranjas distintos",
			completo.Leer(m => m.Lvn.Mapas.Skip(5).All(mapa => mapa.Zonas.Count(z => z.Prioridad == PrioridadLvn.Alta) == 3 && mapa.Zonas.Any(z => z.Prioridad == PrioridadLvn.Secundaria))).ToString(), "True");
		c.Check("estructura de 1H con rupturas y barridos detectados", (resumen.Eventos > 10 && resumen.Barridos > 50).ToString(), "True");
		c.Check("sin huecos ni periodos descartados en un histórico continuo", $"{resumen.Huecos} {resumen.Descartadas}", "0 0");

		var r = completo.Instantanea(DateTime.UtcNow, ZonaPresentacion.CiudadDeMexico, "prueba", "prueba");
		c.Check("el reporte se genera sobre el estado final", (r.Secciones.Count == 10 && r.Cabecera.Length > 0).ToString(), "True");
		c.Check("la instantánea JSON se genera y es válida", (System.Text.Json.JsonDocument.Parse(completo.ExportarJson(DateTime.UtcNow)).RootElement.ValueKind == System.Text.Json.JsonValueKind.Object).ToString(), "True");

		// No repintado: lo calculado con el 60 % del histórico no cambia al seguir procesando.
		var parcial = new MotorCompleto(cfg);

		foreach (var v in historico.Take(historico.Count * 6 / 10))
			parcial.Procesar(v);

		string Firma(Senal s) => $"{s.Id}|{s.Modelo}|{s.ConfirmadaUtc:o}|{s.Plan.Entrada}|{s.Plan.Sl}|{s.Plan.Tp}|{s.Plan.Cantidad}";
		string FirmaMapa(MapaLvn m) => $"{m.Version}|{m.CreadoUtc:o}|{string.Join(",", m.Zonas.Select(z => $"{z.Id}:{z.Inferior}-{z.Superior}:{z.Prioridad}"))}";
		string FirmaBarrido(EventoBarrido b) => $"{b.Id}|{b.ConocidoUtc:o}|{b.Direccion}|{b.Nivel}|{b.PenetracionTicks}";

		var senalesParcial = parcial.Leer(m => m.Senales.Select(Firma).ToList());
		var senalesCompleto = completo.Leer(m => m.Senales.Select(Firma).ToHashSet());
		var mapasParcial = parcial.Leer(m => m.Lvn.Mapas.Select(FirmaMapa).ToList());
		var mapasCompleto = completo.Leer(m => m.Lvn.Mapas.Select(FirmaMapa).ToHashSet());
		var barridosParcial = parcial.Leer(m => m.Barridos.Select(FirmaBarrido).ToList());
		var barridosCompleto = completo.Leer(m => m.Barridos.Select(FirmaBarrido).ToHashSet());
		// Los dos motores conservan un número limitado de mapas y barridos: se comparan los que siguen en ambos.
		var primerMapaCompleto = completo.Leer(m => m.Lvn.Mapas.Min(x => x.Version));
		var primerBarridoCompleto = completo.Leer(m => m.Barridos.Min(b => b.Id));

		c.Check($"no repinta: las {senalesParcial.Count} señales del 60 % siguen idénticas", senalesParcial.All(senalesCompleto.Contains).ToString(), "True");
		c.Check($"no repinta: los {mapasParcial.Count} mapas LVN del 60 % siguen idénticos (congelados)",
			mapasParcial.Where(f => int.Parse(f.Split('|')[0]) >= primerMapaCompleto).All(mapasCompleto.Contains).ToString(), "True");
		c.Check("no repinta: los barridos del 60 % siguen idénticos",
			barridosParcial.Where(f => int.Parse(f.Split('|')[0]) >= primerBarridoCompleto).All(barridosCompleto.Contains).ToString(), "True");

		var repetido = new MotorCompleto(cfg);

		foreach (var v in historico.Take(2000))
			repetido.Procesar(v);

		repetido.Procesar(historico[1500]);
		c.Check("una vela repetida o desordenada se ignora y se cuenta", repetido.Leer(m => m.FueraDeOrden).ToString(), "1");
	}
}
