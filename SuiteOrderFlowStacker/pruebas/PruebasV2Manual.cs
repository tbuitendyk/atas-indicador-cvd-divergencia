using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>
/// S9: el protocolo del botón GATILLO del Thermal con Session Sweeps Pro por el bus real (petición, respuesta, reevaluación
/// en vivo con el mismo Id) y los interruptores de dibujo pedidos desde el panel del Thermal.
/// </summary>
internal static class PruebasV2Manual
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S9 GATILLO por el bus: el Thermal pide, Session Sweeps responde a SU petición");
		SuiteEventBus.Reiniciar();
		var canal = SuiteEventBus.Para("NQZ6-PRUEBA");
		var s = new SimMotor();
		s.Ruptura();
		var respuestas = new List<ManualTriggerResult>();

		// Lo que hace la carcasa de Session Sweeps Pro al recibir una petición.
		canal.Suscribir(e =>
		{
			if (e is ManualTriggerRequest r)
				canal.Publicar(s.M.AnalizarManual(r));
		});

		// Lo que hace el Thermal con las respuestas.
		canal.Suscribir(e =>
		{
			if (e is ManualTriggerResult r)
				respuestas.Add(r);
		});

		var inicio = Utc(new DateTime(2026, 10, 30, 9, 42, 0));
		var ahora = Utc(new DateTime(2026, 10, 30, 9, 45, 0));
		var peticion = new ManualTriggerRequest
		{
			Id = 4242, Fuente = FuenteSuite.Thermal, Utc = ahora, CambioUtc = ahora, Version = 1, DesdeUtc = inicio, HastaUtc = ahora,
			Alto = 21440, Bajo = 21416, Tipo = TipoAnalisisManual.VelaCompuesta, Direccion = DireccionManual.Auto, EnVivo = true
		};
		canal.Publicar(peticion);
		c.Check("una respuesta para la petición 4242, con el recuadro de la petición",
			respuestas.Count == 1 ? $"{respuestas[0].SolicitudId} {F(respuestas[0].Alto)} {F(respuestas[0].Bajo)} {respuestas[0].Fuente}" : $"{respuestas.Count} respuestas", "4242 21440 21416 SessionSweeps");
		c.Check("la respuesta es una de las tres (o NO EJECUTABLE) y lee la vela compuesta",
			respuestas.Count > 0 && respuestas[0].Resultado is "GATILLO POR VELA DE ARRANQUE" or "GATILLO POR VELA DE CONTINUIDAD" or "NO EJECUTABLE" && respuestas[0].VelaAnalizada.Length > 0 ? "sí" : respuestas.FirstOrDefault()?.Resultado ?? "", "sí");
		c.Check("[-] o flecha: la dirección es +1, -1 o 0", respuestas.Count > 0 && respuestas[0].Direccion is -1 or 0 or 1 ? "sí" : "no", "sí");

		c.Seccion("S9 Reevaluación en vivo: misma petición (mismo Id, versión 2) = se actualiza, no se duplica");
		var mas = ahora.AddMinutes(1);
		canal.Publicar(new ManualTriggerRequest
		{
			Id = 4242, Fuente = FuenteSuite.Thermal, Utc = ahora, CambioUtc = mas, Version = 2, DesdeUtc = inicio, HastaUtc = mas,
			Alto = 21440, Bajo = 21416, Tipo = TipoAnalisisManual.VelaCompuesta, Direccion = DireccionManual.Auto, EnVivo = true
		});
		c.Check("dos respuestas recibidas, una sola petición y una sola respuesta guardadas en el bus",
			$"{respuestas.Count} {canal.Todos<ManualTriggerRequest>().Count} {canal.Todos<ManualTriggerResult>().Count} {canal.Todos<ManualTriggerRequest>()[0].Version}", "2 1 1 2");

		c.Seccion("S9 Recuadro vacío: sin barras en el recuadro = sin dirección, nunca un gatillo");
		var vacia = s.M.AnalizarManual(new ManualTriggerRequest
		{
			Id = 5, DesdeUtc = inicio, HastaUtc = ahora, Alto = 30010, Bajo = 30000, Tipo = TipoAnalisisManual.Auto, Direccion = DireccionManual.Auto
		});
		c.Check("recuadro lejos del precio", $"{vacia.Direccion} {vacia.Resultado.StartsWith("GATILLO", StringComparison.Ordinal)} {vacia.Razones.Any(r => r.Contains("no hay barras", StringComparison.Ordinal))}", "0 False True");

		c.Seccion("S9 Interruptores FVG / interna / externa: estado (Id 1) y petición del Thermal (Id 2) por separado");
		canal.Publicar(new VisualSuite { Id = VisualSuite.IdEstado, Fuente = FuenteSuite.SessionSweeps, Utc = ahora, CambioUtc = ahora, Version = 1, Fvg = true, Interna = true, Externa = true });
		canal.Publicar(new VisualSuite { Id = VisualSuite.IdPeticion, Fuente = FuenteSuite.Thermal, Utc = ahora, CambioUtc = mas, Version = 1, Fvg = false, Interna = true, Externa = true });
		var visuales = canal.Todos<VisualSuite>();
		c.Check("dos entradas: estado y petición", string.Join(" ", visuales.OrderBy(v => v.Id).Select(v => $"{v.Id}:{v.Fuente}:{v.Fvg}")), "1:SessionSweeps:True 2:Thermal:False");
		SuiteEventBus.Reiniciar();
	}
}
