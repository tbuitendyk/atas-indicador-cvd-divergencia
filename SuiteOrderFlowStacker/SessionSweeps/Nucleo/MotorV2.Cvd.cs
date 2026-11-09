namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// v3 S13: CVD del instrumento dentro del motor (para el minigráfico del panel) y la CVD Institutional Trigger Matrix.
/// Las divergencias se dibujan sobre las velas de CVD (panel y subpanel), nunca sobre el precio; son confluencia.
/// </summary>
public sealed partial class MotorSweeps
{
	public SeguidorCvd Cvd { get; } = new();

	public MatrizCvd Matriz { get; } = new();

	private void ProcesarCvd(VelaEntrada e)
	{
		Cvd.Reinicio = P2.CvdReset == ReinicioCvd.Rth ? new TimeSpan(9, 30, 0) : new TimeSpan(18, 0, 0);
		var vela = Cvd.Cerrar(e.InicioUtc, e.High, e.Low, e.Delta, e.MaxDelta, e.MinDelta);
		Cvd.Nuevas(P2.CvdMacroPivotBars, P2.CvdMicroPivotBars, P2.CvdShowMicro, P2.CvdMaxLookbackBars);
		Matriz.Agregar(new VelaMatriz(e.InicioUtc, e.Open, e.High, e.Low, e.Close, e.Volume, e.Delta, vela.Close, e.Niveles.ToList()), P2, Cfg.TamanoTick);
	}

	/// <summary>Las últimas 'n' velas de CVD y las divergencias que caen entre ellas (índices relativos al tramo).</summary>
	public (List<VelaCvd> Velas, List<DivergenciaCvd> Divergencias) TramoCvd(int n)
	{
		lock (_sync)
		{
			var total = Cvd.Velas.Count;
			var desde = Math.Max(0, total - n);
			var velas = Cvd.Velas.Skip(desde).ToList();
			var divs = Cvd.Todas.Where(d => d.I1 >= desde).Select(d => d with { I1 = d.I1 - desde, I2 = d.I2 - desde }).ToList();
			return (velas, divs);
		}
	}
}
