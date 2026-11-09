namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// S3/S4: instrumento del gráfico (NQ, ES, YM y micros), su perfil propio y la calibración de volumen con las velas de
/// 1 minuto cargadas. Al cambiar de instrumento se usa SOLO el perfil del nuevo (los parámetros efectivos del motor se
/// rehacen desde ese perfil; el almacén de trades ya se vacía al cambiar de instrumento).
/// </summary>
public partial class SessionSweepsPro
{
	private static string CarpetaPerfiles =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "OrderFlowSuite", "SessionsSweeps", "perfiles");

	private EstadoInstrumento PrepararInstrumento()
	{
		var simbolo = InstrumentInfo?.Instrument;
		var (raiz, contrato) = Instrumentos.Detectar(simbolo, _cfg.V2.AliasSimbolos);
		var perfil = _cfg.Perfiles.De(raiz);
		CalibracionInstrumento? calibracion = null;
		var volumenPorBarra = _cfg.V2.VolumePerBar;

		if (perfil is not null)
		{
			var velas = new List<(DateTime, decimal, decimal)>(Math.Max(0, CurrentBar));

			for (var i = 0; i < CurrentBar - 1; i++)
			{
				var c = GetCandle(i);
				velas.Add((c.Time, c.Volume, c.Delta));
			}

			calibracion = Calibrador.Calcular(velas, perfil, contrato, _cfg.V2.VolumePerBarFraction, DateTime.UtcNow);
			volumenPorBarra = Calibrador.VolumenPorBarra(perfil, calibracion);
			perfil.AplicarA(_cfg.V2, volumenPorBarra);
			GuardarCalibracion(calibracion);
		}

		return EstadoInstrumento.Crear(simbolo, _cfg.V2.AliasSimbolos, InstrumentInfo?.TickSize, calibracion, volumenPorBarra);
	}

	/// <summary>Calibración por familia en %APPDATA%\ATAS\OrderFlowSuite\SessionsSweeps\perfiles\{RAIZ}.json (sin secretos).</summary>
	private void GuardarCalibracion(CalibracionInstrumento c)
	{
		try
		{
			Directory.CreateDirectory(CarpetaPerfiles);
			File.WriteAllText(Path.Combine(CarpetaPerfiles, c.Raiz + ".json"), c.ToJson());
		}
		catch (Exception ex)
		{
			this.LogWarn("Sessions Sweeps Pro: no se pudo guardar la calibración: " + ex.Message);
		}
	}
}
