namespace SessionSweeps.Nucleo;

using System;
using System.Linq;

using OrderFlowSuite.Comun;

/// <summary>S6: el TriggerAnalyzer con los datos del motor, y el gatillo manual del botón GATILLO del Thermal.</summary>
public sealed partial class MotorSweeps
{
	public ContextoGatillo ContextoGatillo() => new()
	{
		Barras = BarrasV.Barras,
		P = P2,
		Tick = Cfg.TamanoTick,
		ContratosPorBarra = P2.ContratosPorBarra(),
		Fuentes = Fuentes,
		Medianas = i => BarrasV.Medianas(i),
		M5 = ContextoV2.M5,
		M15 = ContextoV2.M15,
		Ema15 = ContextoV2.Ema15.Valor,
		CvdAFavor = ContextoV2.CvdAFavor,
		Objetivo = (entrada, s) => Lvn.Proximo(entrada, s, P2.TargetMinDistTicks * Cfg.TamanoTick)?.Centro,
		Ahora = _ahoraV2
	};

	/// <summary>Gatillo manual (10.3): calcula y devuelve la respuesta para el panel del Thermal. No alerta ni opera.</summary>
	public ManualTriggerResult AnalizarManual(ManualTriggerRequest r)
	{
		lock (_sync)
		{
			var a = AnalizadorGatillo.Manual(r, ContextoGatillo());
			var resultado = a.Resultado == ResultadoGatillo.Pendiente ? "NO EJECUTABLE" : a.Respuesta;
			var faltan = a.Resultado == ResultadoGatillo.Valido ? Array.Empty<string>() : a.Faltan.Concat(a.Motivo.Length > 0 ? new[] { a.Motivo } : Array.Empty<string>()).ToArray();

			return new ManualTriggerResult
			{
				Id = r.Id ^ 0x5A5A5A5A,
				Fuente = FuenteSuite.SessionSweeps,
				Utc = _ahoraV2 == default ? r.CambioUtc : _ahoraV2,
				CambioUtc = _ahoraV2 == default ? r.CambioUtc : _ahoraV2,
				Version = r.Version,
				SolicitudId = r.Id,
				Resultado = resultado,
				Direccion = a.Resultado == ResultadoGatillo.Valido ? a.Direccion : 0,
				VelaAnalizada = a.VelaAnalizada,
				Score = a.Score,
				Razones = a.Razones.Concat(faltan.Select(f => "Falta / motivo: " + f)).Take(6).ToList(),
				Hallazgos = a.Hallazgos.ToList(),
				Poc = a.Poc,
				Delta = a.Delta,
				Subasta = a.Subasta,
				Entrada = a.Entrada,
				Stop = a.Stop,
				RewardRisk = a.RewardRisk,
				Alto = r.Alto,
				Bajo = r.Bajo
			};
		}
	}
}
