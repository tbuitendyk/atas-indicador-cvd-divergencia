namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;

using ATAS.Indicators;

/// <summary>Vela del CVD (delta acumulado) correspondiente a una vela del gráfico.</summary>
internal readonly record struct VelaCvd(decimal Apertura, decimal Maximo, decimal Minimo, decimal Cierre);

/// <summary>
/// CVD por vela, calculado como el indicador Cumulative Delta de ATAS: el CVD de una vela es el cierre
/// del CVD de la vela anterior más su delta, y vuelve a cero al empezar cada sesión. Se recalcula a partir
/// de la vela anterior en cada llamada, así los ticks repetidos de la vela en curso no suman su delta
/// varias veces. Lo comparten los dos indicadores del proyecto para que muestren siempre el mismo CVD.
/// </summary>
internal sealed class CalculadoraCvd
{
	private readonly List<decimal> _cierres = new();

	/// <summary>Cantidad de velas con CVD calculado.</summary>
	public int Cantidad => _cierres.Count;

	public decimal Cierre(int bar) => _cierres[bar];

	public void Reiniciar() => _cierres.Clear();

	/// <summary>
	/// Calcula o recalcula la vela <paramref name="bar"/>. Las velas se calculan en orden:
	/// <paramref name="bar"/> no puede ser mayor que <see cref="Cantidad"/>.
	/// </summary>
	public VelaCvd Calcular(int bar, IndicatorCandle vela, bool nuevaSesion)
	{
		var apertura = bar == 0 || nuevaSesion ? 0m : _cierres[bar - 1];
		var cierre = apertura + vela.Delta;

		if (bar < _cierres.Count)
			_cierres[bar] = cierre;
		else
			_cierres.Add(cierre);

		// Extremos intravela del delta, como el Cumulative Delta de ATAS.
		var maximo = Math.Max(apertura + vela.MaxDelta, Math.Max(apertura, cierre));
		var minimo = Math.Min(apertura + vela.MinDelta, Math.Min(apertura, cierre));

		return new VelaCvd(apertura, maximo, minimo, cierre);
	}
}
