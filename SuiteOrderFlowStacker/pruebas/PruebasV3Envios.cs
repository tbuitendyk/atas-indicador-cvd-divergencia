using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>v3 S14/S15: cola de envíos (29), credenciales sin secretos a la vista (36) y el puente MT5 (31-34).</summary>
internal static class PruebasV3Envios
{
	/// <summary>Protector de prueba (en Windows real se usa DPAPI): invierte y desplaza los bytes.</summary>
	private sealed class ProtectorPrueba : IProtector
	{
		public byte[] Proteger(byte[] d) => d.Reverse().Select(b => (byte)(b ^ 0x5A)).ToArray();

		public byte[] Desproteger(byte[] d) => d.Select(b => (byte)(b ^ 0x5A)).Reverse().ToArray();
	}

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v3 S14 Cola de envíos: no bloquea, reintenta y registra (prueba 29)");
		using var cola = new ColaEnvios(reintentos: 3, espera: _ => TimeSpan.FromMilliseconds(10));
		var reloj = Stopwatch.StartNew();
		cola.Encolar("correo", "a", async fin => await Task.Delay(1500, fin));
		var encolar = reloj.ElapsedMilliseconds;
		c.Check("encolar un envío lento (1,5 s) vuelve enseguida: ATAS no espera", (encolar < 200).ToString(), "True");
		var intentos = 0;
		cola.Encolar("telegram", "b", _ =>
		{
			intentos++;
			return intentos < 3 ? throw new InvalidOperationException("caído") : Task.CompletedTask;
		});
		c.Check("la misma alerta y canal no se encola dos veces", cola.Encolar("telegram", "b", _ => Task.CompletedTask).ToString(), "False");
		var limite = DateTime.UtcNow.AddSeconds(5);

		while (DateTime.UtcNow < limite && cola.Ultimos.All(r => r.Canal != "telegram" || !r.Ok))
			Thread.Sleep(20);

		var t = cola.Ultimos.FirstOrDefault(r => r.Canal == "telegram");
		c.Check("reintenta hasta lograrlo y registra el resultado", t is null ? "sin resultado" : $"{t.Ok} {t.Detalle} {intentos}", "True enviado al intento 3 3");

		c.Seccion("v3 S14 Credenciales cifradas; ningún secreto en archivos, logs ni CSV (prueba 36)");
		var carpeta = Path.Combine(Path.GetTempPath(), "ssp-cred-" + Guid.NewGuid().ToString("N"));
		var almacen = new AlmacenCredenciales(carpeta, new ProtectorPrueba());
		const string token = "123456:ABC-secreto-del-bot";
		almacen.Guardar("telegram", token);
		var bytes = File.ReadAllBytes(Directory.GetFiles(carpeta).Single());
		c.Check("el archivo no contiene el token en claro", Encoding.UTF8.GetString(bytes).Contains(token, StringComparison.Ordinal).ToString(), "False");
		c.Check("se lee de vuelta descifrado", almacen.Leer("telegram") == token ? "igual" : "distinto", "igual");
		c.Check("un error que lo contenga sale limpio", almacen.Limpiar($"POST https://api.telegram.org/bot{token}/sendMessage falló", "telegram"), "POST https://api.telegram.org/bot***/sendMessage falló");
		using var colaLimpia = new ColaEnvios(texto => almacen.Limpiar(texto, "telegram"), reintentos: 1);
		colaLimpia.Encolar("telegram", "x", _ => throw new InvalidOperationException($"fallo con {token}"));
		var limite2 = DateTime.UtcNow.AddSeconds(5);

		while (DateTime.UtcNow < limite2 && colaLimpia.Ultimos.Count == 0)
			Thread.Sleep(20);

		c.Check("el registro del envío nunca guarda el secreto", colaLimpia.Ultimos.Any(r => r.Detalle.Contains(token, StringComparison.Ordinal)).ToString(), "False");
		var sim = new SimMotor();
		sim.Ruptura();
		var filas = sim.M.TomarNovedadesV2().Where(n => n.Evaluacion is not null).Select(n => CsvV2.FilaBarrido(n.Evaluacion!, DateTime.UtcNow, "TICKS")).ToList();
		c.Check("las filas del CSV no llevan credenciales", filas.Any(f => f.Contains(token, StringComparison.Ordinal)).ToString(), "False");
		almacen.Borrar("telegram");
		c.Check("borrar la quita", almacen.Existe("telegram").ToString(), "False");
		Directory.Delete(carpeta, true);

		c.Seccion("v3 S15 MT5: apagado, simulación, mapeo, idempotencia y lotes (pruebas 31-34)");
		var nq = EstadoInstrumento.Crear("NQZ6", "", 0.25m, new CalibracionInstrumento { Raiz = RaizInstrumento.NQ, Estado = EstadoCalibracion.Calibrado }, 500);
		var ahora = Utc(new DateTime(2026, 11, 9, 10, 0, 0));
		ResultadoMt5 Orden(GestorMt5 g, long id, int dir = 1, decimal riesgo = 200) => g.Evaluar(id, RaizInstrumento.NQ, dir, 5, 10, riesgo, nq, true, false, ahora, "SSP");

		var apagado = new GestorMt5();
		c.Check("prueba 31: APAGADO no manda nada", $"{Orden(apagado, 1).Texto} {apagado.TomarComandos().Count}", "SIN ORDEN #1: MT5 APAGADO 0");
		var simulacion = new GestorMt5 { Modo = ModoMt5.Simulacion };
		var rs = Orden(simulacion, 2);
		c.Check("prueba 31: SIMULACIÓN simula dentro y no manda comandos", $"{rs.Simulada} {rs.Enviada} {simulacion.TomarComandos().Count} {simulacion.Posiciones.Count}", "True False 0 1");

		var demo = new GestorMt5 { Modo = ModoMt5.Demo, Reloj = () => ahora };
		demo.Mapeo(RaizInstrumento.NQ).Simbolo = "USTEC";
		c.Check("prueba 32: con símbolo pero sin ficha ni validación: sin orden", Orden(demo, 3).Texto, "SIN ORDEN #3: mapeo NQ → MT5 sin validar");
		demo.Reporte("INFO|USTEC|0.01|0.01|2|0.1|100|0.1|0.01");
		c.Check("con la ficha del EA pero sin la validación del usuario: sin orden", Orden(demo, 4).Texto, "SIN ORDEN #4: mapeo NQ → MT5 sin validar");
		demo.Mapeo(RaizInstrumento.NQ).ValidadoPorUsuario = true;
		c.Check("prueba de conversión visible antes de validar",
			demo.Mapeo(RaizInstrumento.NQ).PruebaConversion(Instrumentos.Referencia[RaizInstrumento.NQ]).StartsWith("NQ: 1 punto = 20 USD por contrato · USTEC: 1 punto × 1 = 1 USD por lote", StringComparison.Ordinal).ToString(), "True");
		var enviada = Orden(demo, 5);
		var comandos = demo.TomarComandos();
		c.Check("prueba 34: 200 USD con SL de 5 puntos y 1 USD por punto y lote = 40 lotes; SL/TP como distancias",
			$"{enviada.Enviada} {comandos.SingleOrDefault()}", "True ORDEN|5|USTEC|BUY|40.0|5|10|10|SSP");
		c.Check("prueba 33: el mismo EventId no se manda dos veces", $"{Orden(demo, 5).Texto} {demo.TomarComandos().Count}", "SIN ORDEN #5: EventId ya enviado (sin duplicados) 0");
		demo.Reporte("POS|USTEC|1|40");
		c.Check("una posición por dirección y sin contrarias", $"{Orden(demo, 6).Texto} | {Orden(demo, 7, -1).Texto}",
			"SIN ORDEN #6: ya hay una posición en esa dirección | SIN ORDEN #7: hay una posición contraria abierta");
		demo.Reporte("POS|USTEC|0|0");
		c.Check("volumen por debajo del mínimo del bróker: no se manda", Orden(demo, 8, riesgo: 0.2m).Texto, "SIN ORDEN #8: volumen calculado menor que el mínimo del bróker (0.1)");
		demo.Pausado = true;
		c.Check("PAUSAR bloquea", Orden(demo, 9).Texto, "SIN ORDEN #9: PAUSADO");
		demo.Pausado = false;
		c.Check("en el historial o la recarga nunca", demo.Evaluar(10, RaizInstrumento.NQ, 1, 5, 10, 200, nq, false, false, ahora, "SSP").Texto, "SIN ORDEN #10: histórico o recarga");
		var sinCalibrar = EstadoInstrumento.Crear("NQZ6", "", 0.25m, new CalibracionInstrumento { Estado = EstadoCalibracion.Insuficiente }, 500);
		c.Check("con calibración insuficiente no hay órdenes", demo.Evaluar(11, RaizInstrumento.NQ, 1, 5, 10, 200, sinCalibrar, true, false, ahora, "SSP").Texto, "SIN ORDEN #11: calibración insuficiente");
		var real = new GestorMt5 { Modo = ModoMt5.Real };
		c.Check("REAL sin la confirmación escrita: nada", Orden(real, 12).Texto, "SIN ORDEN #12: REAL sin confirmación escrita");
		demo.Reporte("CLOSED|USTEC|-600");
		c.Check("pasada la pérdida máxima del día: nada", Orden(demo, 13).Texto, "SIN ORDEN #13: pérdida máxima del día (600 USD)");
	}
}
