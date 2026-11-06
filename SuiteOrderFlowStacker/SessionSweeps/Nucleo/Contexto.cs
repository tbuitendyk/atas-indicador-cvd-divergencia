namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ContextEngine: tendencia H1 (la del NQ, CalculoH1), 15m y 5m (si no hay otra: máximos y mínimos crecientes y cierre
/// sobre la EMA21 del marco = alcista; espejo = bajista; si no, rango), CVD de la sesión por vela de 1 minuto, VWAP y
/// EMA 15 de 1 minuto (micro-impulso).
/// </summary>
public sealed class MotorContextoV2
{
	private readonly Agregador _a5 = new(5), _a15 = new(15);
	private readonly Ema _ema5 = new(21), _ema15 = new(21);
	private readonly List<(DateTime Inicio, decimal Cvd, decimal Alto, decimal Bajo, decimal High, decimal Low, decimal Close)> _cvd = new();
	private DateTime? _diaCvd, _diaVwap;
	private decimal _cvdAcumulado, _vwapPv, _vwapV;
	private bool _vwapRth;

	public Tendencia M5 { get; private set; }

	public Tendencia M15 { get; private set; }

	public Ema Ema15 { get; private set; } = new(15);

	public decimal? Vwap => _vwapV > 0 ? _vwapPv / _vwapV : null;

	public IReadOnlyList<Vela> Velas5 => _a5.Velas;

	/// <summary>CVD por vela de 1 minuto: apertura (Cvd anterior), máximo y mínimo aproximados y cierre.</summary>
	public IReadOnlyList<(DateTime Inicio, decimal Cvd, decimal Alto, decimal Bajo, decimal High, decimal Low, decimal Close)> Cvd => _cvd;

	public void Configurar(int emaMicro)
	{
		if (Ema15.Periodo != Math.Max(1, emaMicro))
			Ema15 = new Ema(Math.Max(1, emaMicro));
	}

	public void Agregar(Vela v)
	{
		Ema15.Agregar(v.Close);

		var n5 = _a5.Velas.Count;
		_a5.Agregar(v);

		if (_a5.Velas.Count > n5)
		{
			_ema5.Agregar(_a5.Velas[^1].Close);
			M5 = Tend(_a5.Velas, _ema5.Valor);
		}

		var n15 = _a15.Velas.Count;
		_a15.Agregar(v);

		if (_a15.Velas.Count > n15)
		{
			_ema15.Agregar(_a15.Velas[^1].Close);
			M15 = Tend(_a15.Velas, _ema15.Valor);
		}

		// CVD de la sesión (día operativo de 18:00 a 17:00 ET).
		var dia = Zonas.DiaOperativo(v.InicioUtc);

		if (_diaCvd != dia)
			(_diaCvd, _cvdAcumulado) = (dia, 0);

		var apertura = _cvdAcumulado;
		_cvdAcumulado += v.Delta;
		_cvd.Add((v.InicioUtc, _cvdAcumulado, Math.Max(apertura, _cvdAcumulado), Math.Min(apertura, _cvdAcumulado), v.High, v.Low, v.Close));

		if (_cvd.Count > 6000)
			_cvd.RemoveRange(0, 1000);

		// VWAP: del día operativo; desde la apertura de NY, la de RTH.
		var rth = TiempoV2.EsRth(v.InicioUtc);

		if (_diaVwap != dia)
			(_diaVwap, _vwapRth, _vwapPv, _vwapV) = (dia, false, 0, 0);

		if (rth && !_vwapRth)
			(_vwapRth, _vwapPv, _vwapV) = (true, 0, 0);

		_vwapPv += (v.High + v.Low + v.Close) / 3 * v.Volume;
		_vwapV += v.Volume;
	}

	/// <summary>Regla por defecto del documento: máximos y mínimos crecientes y cierre &gt; EMA21 = alcista; espejo = bajista.</summary>
	public static Tendencia Tend(IReadOnlyList<Vela> velas, decimal? ema)
	{
		if (velas.Count < 3 || ema is not { } e)
			return Tendencia.Rango;

		var (a, b, c) = (velas[^3], velas[^2], velas[^1]);

		if (c.High > b.High && b.High > a.High && c.Low > b.Low && b.Low > a.Low && c.Close > e)
			return Tendencia.Alcista;

		if (c.High < b.High && b.High < a.High && c.Low < b.Low && b.Low < a.Low && c.Close < e)
			return Tendencia.Bajista;

		return Tendencia.Rango;
	}

	public static Tendencia DeH1(ContextoH1 h1) => h1 switch { ContextoH1.Alcista => Tendencia.Alcista, ContextoH1.Bajista => Tendencia.Bajista, _ => Tendencia.Rango };

	/// <summary>CVD al cierre de la vela de 1 minuto que empieza en (o antes de) 'utc'.</summary>
	public decimal? CvdEn(DateTime utc)
	{
		for (var i = _cvd.Count - 1; i >= 0; i--)
		{
			if (_cvd[i].Inicio <= utc)
				return _cvd[i].Cvd;
		}

		return null;
	}

	/// <summary>CVD a favor de 's' en las últimas 5 velas de 1 minuto (sin divergencia en contra).</summary>
	public bool CvdAFavor(int s) => _cvd.Count >= 5 && s * (_cvd[^1].Cvd - _cvd[^5].Cvd) > 0;

	/// <summary>
	/// Divergencia de CVD del barrido: el precio supera el nivel pero el CVD en el extremo no supera el CVD de cuando se
	/// formó el nivel (máximo a máximo / mínimo a mínimo).
	/// </summary>
	public bool Divergencia(EvaluacionBarrido e)
	{
		if (CvdEn(e.Nivel.CreadoUtc.AddMinutes(-1)) is not { } antes || CvdEn(e.ExtremoUtc) is not { } ahora)
			return false;

		return e.Signo > 0 ? ahora <= antes : ahora >= antes;
	}
}
