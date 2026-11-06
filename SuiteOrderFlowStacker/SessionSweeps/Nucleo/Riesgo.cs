namespace SessionSweeps.Nucleo;

using System;
using System.Globalization;

/// <summary>Plan de riesgo (11.2-6) de una entrada.</summary>
public readonly record struct PlanRiesgo(decimal RiesgoUsd, int Contratos, int StopTicks, decimal ValorTick, bool Insuficiente)
{
	public string Texto(bool mnq) => Insuficiente
		? $"RIESGO INSUFICIENTE: usar MNQ (riesgo {RiesgoUsd:0} USD, stop {StopTicks} ticks)"
		: $"Riesgo {RiesgoUsd.ToString("0", CultureInfo.InvariantCulture)} USD | {Contratos} {(mnq ? "MNQ" : "NQ")} | stop {StopTicks} ticks";
}

/// <summary>
/// RiskCalculator: contratos = floor(riesgo / (stopTicks x valor del tick)); NQ 5 USD/tick, MNQ 0.50 USD/tick. Riesgo en
/// MONTO fijo o en PORCENTAJE de AccountSize (manual por defecto; leer la cuenta de ATAS es opcional).
/// </summary>
public static class CalculadoraRiesgo
{
	public static decimal ValorTick(bool mnq) => mnq ? 0.50m : 5m;

	public static decimal Riesgo(ParametrosV2 p, decimal? saldoAtas = null)
	{
		var cuenta = p.ReadAtasAccount && saldoAtas is > 0 ? saldoAtas.Value : p.AccountSize;
		return p.RiskMode == ModoRiesgo.Monto ? p.RiskAmountUSD : Math.Round(cuenta * p.RiskPct / 100m, 2);
	}

	public static PlanRiesgo Calcular(ParametrosV2 p, decimal entrada, decimal stop, decimal tick, bool mnq, decimal? saldoAtas = null)
	{
		var stopTicks = tick > 0 ? (int)Math.Round(Math.Abs(entrada - stop) / tick) : 0;
		var riesgo = Riesgo(p, saldoAtas);
		var valor = ValorTick(mnq);
		var contratos = stopTicks > 0 ? (int)Math.Floor(riesgo / (stopTicks * valor)) : 0;
		return new PlanRiesgo(riesgo, contratos, stopTicks, valor, contratos < 1);
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
