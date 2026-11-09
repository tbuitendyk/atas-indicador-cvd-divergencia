namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Recarga del historial (7.7, T16): los barridos congelados del día se restauran desde el CSV (el CSV manda). Los bloques
/// B y C dependen de icebergs, stops y sweeps que solo existen en vivo; recalcular desde el historial no los reproduce.
/// La evaluación del mismo barrido (lado, nivel a 1 tick, T0 a 2 minutos) toma las filas del CSV: queda PENDIENTE hasta
/// la hora de su congelación y después aplica cada fila (congelación y transiciones) a su hora. Pasada la última fila, el
/// clasificador sigue con lo que muestre el precio.
/// </summary>
public sealed partial class MotorSweeps
{
	private readonly HashSet<long> _csvUsados = new();

	/// <summary>Filas del CSV del día (la carcasa las pone antes de recalcular).</summary>
	public List<RegistroCsvBarrido> RestaurarDesde { get; } = new();

	private void AsignarCsv(EvaluacionBarrido e)
	{
		if (RestaurarDesde.Count == 0)
			return;

		var tick = Cfg.TamanoTick;
		var candidato = RestaurarDesde
			.Where(r => !_csvUsados.Contains(r.EventId) && r.Signo == e.Signo && Math.Abs(r.L - e.L) <= tick && Math.Abs((r.T0Utc - e.T0Utc).TotalSeconds) <= 120)
			.OrderBy(r => Math.Abs((r.T0Utc - e.T0Utc).TotalMilliseconds))
			.FirstOrDefault();

		if (candidato is null)
			return;

		_csvUsados.Add(candidato.EventId);
		e.FilasCsv = RestaurarDesde.Where(r => r.EventId == candidato.EventId).OrderBy(r => r.TimeConfirmUtc).ToList();
		e.Restaurada = true;
	}

	/// <summary>true = el CSV manda en esta barra (no se evalúa con el precio).</summary>
	private bool AplicarCsv(EvaluacionBarrido e, BarraVolumen b)
	{
		if (e.FilasCsv is not { Count: > 0 } filas)
			return false;

		while (filas.Count > 0 && filas[0].TimeConfirmUtc <= b.FinUtc)
		{
			var f = filas[0];
			filas.RemoveAt(0);
			var antes = e.Estado;
			var primera = !e.Congelada;

			(e.Estado, e.ScoreA, e.ScoreB, e.ScoreC, e.ScoreD, e.ScoreE, e.Penalties, e.Total) = (f.Estado, f.ScoreA, f.ScoreB, f.ScoreC, f.ScoreD, f.ScoreE, f.Penalties, f.Total);
			(e.PremiumGates, e.StopsEvento, e.IcebergEstado, e.IcebergModo, e.AbsorptionEvento) = (f.PremiumGates, f.StopsEvent, f.IcebergState, f.IcebergMode, f.AbsorptionEvent);
			(e.ConflictoPine, e.Noticia, e.SourcesLoaded) = (f.ConflictoPine, f.News, f.SourcesLoaded);

			if (primera)
			{
				(e.Congelada, e.TimeConfirmUtc, e.BarraConfirmacion) = (true, f.TimeConfirmUtc, b.Indice);
				e.Transiciones.Add($"{f.Estado.Texto()} (CSV)");
				Novedad(e, "CONFIRMADO", b.FinUtc, $"{e.Estado.Texto()} {e.Nombre} {Precio(e.L)} | {e.Total:0}/100 (CSV)");

				if (e.Estado.EsReal())
					RegistrarBfvgExistentes(e);

				ModelosAlEstado(e, "CONFIRMADO", b.FinUtc);
				continue;
			}

			if (f.Estado == antes)
				continue;

			var transicion = f.Estado switch
			{
				EstadoBarrido.Premium => "UPGRADE PREMIUM",
				EstadoBarrido.Fallido => "FALLIDO",
				EstadoBarrido.RupturaFallida => "RUPTURA FALLIDA",
				_ => f.Estado.Texto()
			};

			e.UpgradePremium |= f.Estado == EstadoBarrido.Premium;
			e.Transiciones.Add($"{transicion} (CSV)");
			Novedad(e, transicion, b.FinUtc, $"{transicion} {e.Nombre} {Precio(e.L)} | {e.Total:0}/100 (CSV)");
			ModelosAlEstado(e, transicion, b.FinUtc);

			if (e.Estado == EstadoBarrido.Fallido)
				NuevaEvaluacion(e.Nivel, b.FinUtc, e.Signo > 0 ? b.High : b.Low, b.Indice, e, "TRAS FALLIDO");
			else if (e.Estado == EstadoBarrido.RupturaFallida)
				Hija(e, b, "TRAS RUPTURA FALLIDA");
		}

		// Mientras queden filas por llegar, el CSV manda; en la barra que aplica la última, también.
		var aplicada = filas.Count == 0;

		if (aplicada)
			e.FilasCsv = null;

		return true;
	}
}
