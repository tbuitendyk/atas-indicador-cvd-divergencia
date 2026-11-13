namespace OrderFlowSuite.Atas;

using System;

using OFT.Rendering.Context;

using OrderFlowSuite.Nucleo;

/// <summary>
/// v4 (Indicador Global Session Sweep): los cuatro trackers de la suite (Stacked / Imbalance, Iceberg, Stops, Sweeps)
/// corriendo dentro de otro indicador, con su motor de siempre (su lógica no cambia). Publica sus eventos en el bus del
/// instrumento como lo harían los trackers sueltos; no suena ni muestra su panel propio (el propietario: solo las
/// alertas del barrido); su dibujo original se enciende por capa.
/// </summary>
public sealed class AnfitrionTrackers : IDisposable
{
	public AnfitrionTrackers()
	{
		C.General.ShowInfoPanel = false;
		C.General.EnableAlerts = false;
		C.Alerta = null;
	}

	public CarcasaSuite C { get; } = new("Global", false);

	/// <summary>Correr los trackers dentro (apagado: el indicador usa los sueltos que haya en el gráfico).</summary>
	public bool Activo { get; set; } = true;

	public bool DibujarStacked { get; set; } = true;
	public bool DibujarIcebergs { get; set; } = true;
	public bool DibujarStops { get; set; } = true;
	public bool DibujarSweeps { get; set; } = true;

	public void Calcular(int bar, int currentBar, Func<int, VelaFlujo> vela, Func<int, DateTime> tiempo, string? instrumento, decimal tick)
	{
		if (Activo)
			C.Calcular(bar, currentBar, vela, tiempo, instrumento, tick);
	}

	public void Trade(decimal precio, decimal volumen, DateTime utc, string direccion, long? idAgresor, long? idPasivo)
	{
		if (Activo)
			C.Trade(precio, volumen, utc, direccion, idAgresor, idPasivo);
	}

	public void Dom(decimal precio, decimal volumen, string tipo)
	{
		if (Activo)
			C.Dom(precio, volumen, tipo);
	}

	public void Mbo(string tipo, string lado, decimal precio, decimal volumen, long id, long prioridad)
	{
		if (Activo)
			C.Mbo(tipo, lado, precio, volumen, id, prioridad);
	}

	/// <summary>El dibujo original de cada tracker (según el modo visual: Minimal, Markers o Full).</summary>
	public void Dibujar(RenderContext context, Lienzo l)
	{
		if (!Activo)
			return;

		if (DibujarStacked)
			DibujoSuite.Zonas(context, C, l);

		if (DibujarIcebergs)
			DibujoSuite.Icebergs(context, C, l);

		if (DibujarStops)
			DibujoSuite.Stops(context, C, l);

		if (DibujarSweeps)
			DibujoSuite.Barridos(context, C, l);
	}

	public void Dispose() => C.Dispose();
}
