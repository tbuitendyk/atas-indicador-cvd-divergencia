namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// v5: el Sweep Pro (FAST) en un gráfico de N minutos (5) sigue calculando con velas de 1 minuto. Las velas del día salen
/// de los trades (exactas); las demás se aproximan partiendo la vela del gráfico. El motor no se entera: recibe velas de
/// 1 minuto como en un gráfico de 1 minuto.
/// </summary>
public static class MinutosV5
{
	/// <summary>
	/// Parte una vela de N minutos en N velas de 1 minuto. Recorrido: apertura → mínimo → máximo → cierre (vela alcista) o
	/// apertura → máximo → mínimo → cierre (bajista), repartido en N tramos iguales. El footprint va a los minutos cuyo rango
	/// contiene cada precio, en partes iguales; sin footprint, volumen, delta, ask y bid en partes iguales. Los totales y
	/// los extremos son exactos.
	/// </summary>
	public static List<VelaEntrada> Partir(VelaEntrada e, int minutos, decimal tick)
	{
		var n = Math.Max(1, minutos);
		tick = tick > 0 ? tick : 0.25m;

		if (n == 1)
			return new List<VelaEntrada> { e };

		var alcista = e.Close >= e.Open;
		var puntos = alcista ? new[] { e.Open, e.Low, e.High, e.Close } : new[] { e.Open, e.High, e.Low, e.Close };
		var tramos = new decimal[3];

		for (var i = 0; i < 3; i++)
			tramos[i] = Math.Abs(puntos[i + 1] - puntos[i]);

		var total = tramos.Sum();

		// Precio en la fracción f del recorrido.
		decimal P(decimal f)
		{
			if (total == 0)
				return e.Open;

			var d = f * total;

			for (var i = 0; i < 3; i++)
			{
				if (d <= tramos[i] || i == 2)
				{
					var x = tramos[i] == 0 ? 0 : Math.Min(1, d / tramos[i]);
					return puntos[i] + (puntos[i + 1] - puntos[i]) * x;
				}

				d -= tramos[i];
			}

			return e.Close;
		}

		decimal R(decimal p) => Math.Round(p / tick) * tick;

		// Fracciones donde el recorrido gira (el mínimo y el máximo de cada minuto pueden estar dentro del tramo).
		var giros = total == 0 ? Array.Empty<decimal>() : new[] { tramos[0] / total, (tramos[0] + tramos[1]) / total };
		var r = new List<VelaEntrada>(n);

		for (var k = 0; k < n; k++)
		{
			var (f0, f1) = (k / (decimal)n, (k + 1) / (decimal)n);
			var precios = new List<decimal> { P(f0), P(f1) };
			precios.AddRange(giros.Where(g => g > f0 && g < f1).Select(P));
			var v = new VelaEntrada
			{
				InicioUtc = e.InicioUtc.AddMinutes(k),
				Open = k == 0 ? e.Open : R(P(f0)),
				Close = k == n - 1 ? e.Close : R(P(f1)),
				High = R(precios.Max()),
				Low = R(precios.Min())
			};
			(v.High, v.Low) = (Math.Max(v.High, Math.Max(v.Open, v.Close)), Math.Min(v.Low, Math.Min(v.Open, v.Close)));
			r.Add(v);
		}

		// Los extremos de la vela partida son exactamente los de la original.
		r.First(v => v.High == r.Max(x => x.High)).High = e.High;
		r.First(v => v.Low == r.Min(x => x.Low)).Low = e.Low;

		if (e.Niveles.Count > 0)
		{
			foreach (var nivel in e.Niveles)
			{
				var dentro = r.Where(v => nivel.Precio >= v.Low && nivel.Precio <= v.High).ToList();

				if (dentro.Count == 0)
					dentro.Add(r.OrderBy(v => Math.Min(Math.Abs(v.Low - nivel.Precio), Math.Abs(v.High - nivel.Precio))).First());

				foreach (var v in dentro)
					v.Niveles.Add(new NivelVela(nivel.Precio, nivel.Bid / dentro.Count, nivel.Ask / dentro.Count));
			}

			foreach (var v in r)
			{
				v.Ask = v.Niveles.Sum(x => x.Ask);
				v.Bid = v.Niveles.Sum(x => x.Bid);
				v.Volume = v.Ask + v.Bid;
				v.Delta = v.Ask - v.Bid;
			}

			// Volumen sin footprint (p. ej. trades sin lado): se reparte para que el total cuadre.
			var resto = e.Volume - r.Sum(v => v.Volume);

			if (resto > 0)
			{
				foreach (var v in r)
					v.Volume += resto / n;
			}
		}
		else
		{
			foreach (var v in r)
				(v.Volume, v.Delta, v.Ask, v.Bid) = (e.Volume / n, e.Delta / n, e.Ask / n, e.Bid / n);
		}

		return r;
	}

	/// <summary>
	/// Velas de 1 minuto exactas con los trades (en orden de tiempo) de [desde, hasta): OHLC, volumen, delta, ask, bid,
	/// delta máximo y mínimo y footprint. Un minuto sin trades no tiene vela (como en un gráfico de 1 minuto).
	/// </summary>
	public static List<VelaEntrada> DesdeTicks(IEnumerable<TickV2> ticks, DateTime desde, DateTime hasta)
	{
		var r = new List<VelaEntrada>();
		var c = new ConstructorMinuto();

		foreach (var t in ticks)
		{
			if (t.Utc < desde || t.Utc >= hasta)
				continue;

			if (c.Trade(t.Utc, t.Precio, t.Volumen, t.Compra) is { } cerrada)
				r.Add(cerrada);
		}

		if (c.Cerrar() is { } ultima)
			r.Add(ultima);

		return r;
	}

	/// <summary>Minutos por vela de un gráfico de ATAS ("TimeFrame" con "M5" → 5); 0 si no es de tiempo; -1 si no se sabe.</summary>
	public static int DeTimeFrame(string? tipoGrafico, string? timeFrame)
	{
		if (tipoGrafico is not null && tipoGrafico != "TimeFrame")
			return 0;

		return timeFrame is not null && Regex.Match(timeFrame, @"^M(\d+)$") is { Success: true } m ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
	}

	public static DateTime Minuto(DateTime utc) => new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
}

/// <summary>Arma la vela de 1 minuto con los trades en vivo; la entrega cerrada cuando llega el primer trade del minuto siguiente.</summary>
public sealed class ConstructorMinuto
{
	private readonly SortedDictionary<decimal, (decimal Bid, decimal Ask)> _niveles = new();
	private VelaEntrada? _actual;
	private decimal _deltaCorrido;

	/// <summary>Inicio del minuto en curso (MinValue si no hay ninguno).</summary>
	public DateTime Inicio => _actual?.InicioUtc ?? DateTime.MinValue;

	/// <summary>Apertura del minuto en curso (0 si no hay ninguno).</summary>
	public decimal Apertura => _actual?.Open ?? 0;

	/// <summary>Un trade. Si es de un minuto posterior al que se estaba armando, devuelve ese minuto ya cerrado.</summary>
	public VelaEntrada? Trade(DateTime utc, decimal precio, decimal volumen, bool? compra)
	{
		var minuto = MinutosV5.Minuto(utc);

		if (_actual is not null && minuto < _actual.InicioUtc)
			return null;

		VelaEntrada? cerrada = null;

		if (_actual is not null && minuto > _actual.InicioUtc)
			cerrada = Cerrar();

		if (_actual is null)
		{
			_actual = new VelaEntrada { InicioUtc = minuto, Open = precio, High = precio, Low = precio, Close = precio };
			_deltaCorrido = 0;
		}

		var v = _actual;
		(v.High, v.Low, v.Close) = (Math.Max(v.High, precio), Math.Min(v.Low, precio), precio);
		v.Volume += volumen;
		var (bid, ask) = _niveles.TryGetValue(precio, out var x) ? x : default;

		if (compra == true)
		{
			v.Ask += volumen;
			_deltaCorrido += volumen;
			ask += volumen;
		}
		else if (compra == false)
		{
			v.Bid += volumen;
			_deltaCorrido -= volumen;
			bid += volumen;
		}

		_niveles[precio] = (bid, ask);
		(v.Delta, v.MaxDelta, v.MinDelta) = (_deltaCorrido, Math.Max(v.MaxDelta, _deltaCorrido), Math.Min(v.MinDelta, _deltaCorrido));
		return cerrada;
	}

	/// <summary>Cierra el minuto en curso (si lo hay) y lo devuelve.</summary>
	public VelaEntrada? Cerrar()
	{
		if (_actual is not { } v)
			return null;

		foreach (var (precio, (bid, ask)) in _niveles)
			v.Niveles.Add(new NivelVela(precio, bid, ask));

		_niveles.Clear();
		_actual = null;
		return v;
	}

	/// <summary>Cierra el minuto en curso si ya terminó antes de 'hastaUtc' (p. ej. al cerrar la vela del gráfico).</summary>
	public VelaEntrada? CerrarSiTermino(DateTime hastaUtc) => _actual is { } v && v.InicioUtc.AddMinutes(1) <= hastaUtc ? Cerrar() : null;

	public void Vaciar()
	{
		_niveles.Clear();
		_actual = null;
	}
}
