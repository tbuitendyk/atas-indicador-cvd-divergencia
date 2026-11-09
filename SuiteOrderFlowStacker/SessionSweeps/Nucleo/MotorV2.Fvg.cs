namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>S5: FVG de 1m y 5m y registro de los BFVG (todos los FVG de la pierna de recuperación de una trampa).</summary>
public sealed partial class MotorSweeps
{
	private readonly HashSet<(long Barrido, long Fvg)> _bfvgRegistrados = new();
	private long _siguienteZonaV2 = 1;
	private int _velas5Vistas;

	public MotorFvgV2 Fvg2 { get; } = new();

	/// <summary>Zonas de los modelos (BFVG, MOD2, MOD4…), cada una con su análisis de gatillo.</summary>
	public List<ZonaOperable> Zonas2 { get; } = new();

	public long NuevaZonaId() => _siguienteZonaV2++;

	private void ProcesarFvgV2(Vela v)
	{
		Fvg2.AlNacer ??= f =>
		{
			foreach (var e in Evaluaciones.Where(e => e.Estado.EsReal()).ToList())
				RegistrarBfvg(e, f);
		};

		if (P2.Fvg1m)
			Fvg2.Agregar(Velas, 1, P2, Cfg.TamanoTick);

		if (P2.Fvg5m && ContextoV2.Velas5.Count > _velas5Vistas)
		{
			_velas5Vistas = ContextoV2.Velas5.Count;
			Fvg2.Agregar(ContextoV2.Velas5, 5, P2, Cfg.TamanoTick);
		}
	}

	/// <summary>Al confirmar una trampa: los FVG ya formados en su pierna de recuperación.</summary>
	private void RegistrarBfvgExistentes(EvaluacionBarrido e)
	{
		foreach (var f in Fvg2.Fvgs.ToList())
			RegistrarBfvg(e, f);
	}

	/// <summary>
	/// BFVG (6.3): un FVG en la dirección del giro, formado tras el extremo de la trampa, dentro de BfvgWindowMin y antes de
	/// un barrido nuevo, es una zona propia con su propio análisis de gatillo, contenga o no un LVN.
	/// </summary>
	private void RegistrarBfvg(EvaluacionBarrido e, FvgV2 f)
	{
		if (!e.Estado.EsReal() || f.Signo != e.SignoGiro || f.Utc <= e.ExtremoUtc || !_bfvgRegistrados.Add((e.Id, f.Id)))
			return;

		var fin = e.ExtremoUtc.AddMinutes(Math.Max(1, P2.BfvgWindowMin));
		var nuevoBarrido = Evaluaciones.Where(x => x.T0Utc > e.T0Utc && x.Padre != e.Id).Select(x => (DateTime?)x.T0Utc).Min();

		if (f.Utc > fin || (nuevoBarrido is { } nb && f.Utc > nb))
		{
			_bfvgRegistrados.Remove((e.Id, f.Id));
			return;
		}

		var z = MotorFvgV2.ZonaDeFvg(NuevaZonaId(), f, Lvn, P2, Cfg.TamanoTick, bfvg: true, e.Id, "BFVG");
		z.ExpiraUtc = fin;
		Zonas2.Add(z);

		if (Zonas2.Count > 300)
			Zonas2.RemoveRange(0, Zonas2.Count - 300);
	}
}
