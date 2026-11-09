namespace SessionSweeps.Nucleo;

using System;
using System.Globalization;

/// <summary>Plan de riesgo (11.2-6) de una entrada.</summary>
public readonly record struct PlanRiesgo(decimal RiesgoUsd, int Contratos, int StopTicks, decimal ValorTick, bool Insuficiente)
{
	/// <summary>Sin valor del tick validado (2.3) no se calculan contratos.</summary>
	public bool NoValidado => ValorTick <= 0;

	public string Texto(RaizInstrumento raiz)
	{
		if (NoValidado)
			return $"{Instrumentos.NoValidado}: sin cálculo de contratos (stop {StopTicks} ticks)";

		var micro = Instrumentos.Micro(raiz);

		if (Insuficiente)
			return micro != raiz
				? $"RIESGO INSUFICIENTE: usar {micro} (riesgo {RiesgoUsd:0} USD, stop {StopTicks} ticks)"
				: $"RIESGO INSUFICIENTE para 1 {raiz} (riesgo {RiesgoUsd:0} USD, stop {StopTicks} ticks)";

		return $"Riesgo {RiesgoUsd.ToString("0", CultureInfo.InvariantCulture)} USD | {Contratos} {raiz} | stop {StopTicks} ticks";
	}
}

/// <summary>
/// RiskCalculator: contratos = floor(riesgo / (stopTicks x valor del tick)); NQ 5 USD/tick, MNQ 0.50 USD/tick. Riesgo en
/// MONTO fijo o en PORCENTAJE de AccountSize (manual por defecto; leer la cuenta de ATAS es opcional).
/// </summary>
public static class CalculadoraRiesgo
{

	public static decimal Riesgo(ParametrosV2 p, decimal? saldoAtas = null)
	{
		var cuenta = p.ReadAtasAccount && saldoAtas is > 0 ? saldoAtas.Value : p.AccountSize;
		return p.RiskMode == ModoRiesgo.Monto ? p.RiskAmountUSD : Math.Round(cuenta * p.RiskPct / 100m, 2);
	}

	/// <summary>Contratos con el valor del tick del instrumento (0 = no validado: sin contratos).</summary>
	public static PlanRiesgo Calcular(ParametrosV2 p, decimal entrada, decimal stop, decimal tick, decimal valorTick, decimal? saldoAtas = null)
	{
		var stopTicks = tick > 0 ? (int)Math.Round(Math.Abs(entrada - stop) / tick) : 0;
		var riesgo = Riesgo(p, saldoAtas);
		var contratos = stopTicks > 0 && valorTick > 0 ? (int)Math.Floor(riesgo / (stopTicks * valorTick)) : 0;
		return new PlanRiesgo(riesgo, contratos, stopTicks, Math.Max(0, valorTick), valorTick > 0 && contratos < 1);
	}

	/// <summary>¿Se permite un añadido? El riesgo abierto total no puede pasar de MaxOpenRiskPct de la cuenta.</summary>
	public static bool PermiteAnadir(ParametrosV2 p, decimal riesgoAbiertoUsd, decimal riesgoNuevoUsd, decimal? saldoAtas = null)
	{
		var cuenta = p.ReadAtasAccount && saldoAtas is > 0 ? saldoAtas.Value : p.AccountSize;
		return riesgoAbiertoUsd + riesgoNuevoUsd <= cuenta * p.MaxOpenRiskPct / 100m;
	}
}

/// <summary>
/// Gestión de micro-impulso (11.2-7): EMA de MicroImpulseEmaLen velas de 1 minuto. Se ACTIVA por un cierre que cruza la
/// EMA o por una vela de cambio de dirección verificada (DeltaPct en contra &gt;= F3DeltaPctMin y cierre en el tercio en
/// contra) en el precio X. Actúa tras TP1 (o desde la entrada si MicroImpulseFromEntry).
/// </summary>
public sealed class MicroImpulso
{
	public decimal? Ema { get; private set; }

	public DateTime? ActivaDesdeUtc { get; private set; }

	public decimal? PrecioActivacion { get; private set; }

	public string Motivo { get; private set; } = "";

	public bool Activa => ActivaDesdeUtc is not null;

	public void Reiniciar() => (ActivaDesdeUtc, PrecioActivacion, Motivo) = (null, null, "");

	/// <summary>Vela de 1 minuto cerrada con la EMA ya actualizada; 'signo' = dirección de la operación abierta.</summary>
	public bool Vela(Vela v, decimal? ema, decimal? cierreAnterior, decimal? emaAnterior, int signo, ParametrosV2 p)
	{
		Ema = ema;

		if (Activa || ema is not { } e || signo == 0)
			return false;

		var cruza = emaAnterior is { } ea && cierreAnterior is { } ca && signo * (v.Close - e) < 0 && signo * (ca - ea) >= 0;
		var rango = v.High - v.Low;
		var deltaPct = v.Volume > 0 ? v.Delta / v.Volume : 0;
		var tercio = rango > 0 && (signo > 0 ? v.Close <= v.Low + rango / 3 : v.Close >= v.High - rango / 3);
		var cambio = -signo * deltaPct >= p.F3DeltaPctMin && tercio;

		if (!cruza && !cambio)
			return false;

		(ActivaDesdeUtc, PrecioActivacion, Motivo) = (v.FinUtc, v.Close, cruza ? "cierre que cruza la EMA" : "vela de cambio de dirección verificada");
		return true;
	}

	public string Texto(Func<decimal, string> precio) => Activa
		? $"ACTIVA desde {TiempoV2.HoraCdmx(ActivaDesdeUtc!.Value)} en {precio(PrecioActivacion!.Value)} ({Motivo})"
		: "INACTIVA";
}
