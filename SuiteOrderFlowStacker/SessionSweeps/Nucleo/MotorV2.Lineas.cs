namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>v4: líneas de sesión (máximos y mínimos de Asia, Londres, Premercado y NY), su ruptura, el BOS y el FVG relevante.</summary>
public sealed partial class MotorSweeps
{
	public MotorLineasSesion LineasSesion { get; } = new();

	private void ProcesarLineas(Vela v, IEnumerable<InstanciaSesion> terminadas)
	{
		var ny = Sesiones.Instancias.LastOrDefault(s => s.Abierta && s.Id is ConfiguracionSweeps.NyAm or ConfiguracionSweeps.NyPm);

		foreach (var (l, bos) in LineasSesion.Vela(v, Velas, terminadas, ny))
			NovedadLinea(l, bos, bos ? v.FinUtc : l.RotaUtc ?? v.FinUtc);
	}

	private void TickLineas(DateTime utc, decimal precio)
	{
		foreach (var l in LineasSesion.Tick(utc, precio, Velas.Count))
			NovedadLinea(l, false, utc);
	}

	private void NovedadLinea(LineaSesion l, bool bos, DateTime utc)
	{
		var texto = bos
			? $"BOS {(l.Alto ? "alcista" : "bajista")}: cierre más allá del {l.Nombre} {Precio(l.Precio)}"
			: $"Rompe el {l.Nombre} {Precio(l.Precio)}";
		NovedadesV2.Add(new NovedadV2 { Tipo = bos ? "BOS DE SESIÓN" : "LÍNEA DE SESIÓN", Utc = utc, Texto = texto, Linea = l });

		if (NovedadesV2.Count > 2000)
			NovedadesV2.RemoveRange(0, NovedadesV2.Count - 2000);
	}

	/// <summary>
	/// v4: un FVG es relevante si nació después de un barrido VALIDADO (REAL o PREMIUM) y a favor de su giro; con
	/// "FVG solo tras barrido validado" el gráfico solo dibuja esos.
	/// </summary>
	public bool FvgRelevante(FvgV2 f) =>
		Evaluaciones.Any(e => e.Congelada && e.Estado.EsReal() && e.SignoGiro == f.Signo && f.Utc >= e.T0Utc && f.Utc - e.T0Utc <= TimeSpan.FromHours(4));
}
