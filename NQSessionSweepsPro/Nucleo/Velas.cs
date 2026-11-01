namespace NqSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Vela cerrada del gráfico (o agregada), con su footprint por tick si lo hay.</summary>
public sealed class Vela
{
	public int Indice;
	public DateTime InicioUtc, FinUtc;
	public decimal Open, High, Low, Close, Volume, Delta, Ask, Bid;

	/// <summary>Footprint: tick (precio / tamaño de tick) → (Bid, Ask). Null si no hay datos por precio.</summary>
	public SortedDictionary<long, (double Bid, double Ask)>? Niveles;

	public decimal Rango => High - Low;

	public decimal Cuerpo => Math.Abs(Close - Open);

	public bool TieneHuella => Niveles is { Count: > 0 };

	/// <summary>Posición de un precio dentro del rango (0 = Low, 1 = High).</summary>
	public decimal Posicion(decimal precio) => Rango > 0 ? (precio - Low) / Rango : 0.5m;

	public bool EnTercioSuperior(decimal precio) => Rango > 0 && precio >= Low + Rango * 2 / 3;

	public bool EnTercioInferior(decimal precio) => Rango > 0 && precio <= Low + Rango / 3;
}

/// <summary>Lectura del footprint de una vela: POC y área de valor del 70 %.</summary>
public readonly record struct Huella(decimal Poc, decimal VaBajo, decimal VaAlto)
{
	public decimal VaMedio => (VaBajo + VaAlto) / 2;

	/// <summary>POC: precio de más volumen (empate: el del medio). VA: desde el POC se añade el vecino mayor hasta el 70 %.</summary>
	public static Huella? Calcular(Vela v, decimal tick)
	{
		if (v.Niveles is not { Count: > 0 } niveles)
			return null;

		var ticks = niveles.Keys.ToArray();
		var vol = ticks.Select(t => niveles[t].Bid + niveles[t].Ask).ToArray();
		var total = vol.Sum();

		if (total <= 0)
			return null;

		var maximo = vol.Max();
		var empatados = Enumerable.Range(0, vol.Length).Where(i => vol[i] == maximo).ToList();
		var poc = empatados[(empatados.Count - 1) / 2];
		int bajo = poc, alto = poc;
		var acumulado = vol[poc];

		while (acumulado < total * 0.70 && (bajo > 0 || alto < vol.Length - 1))
		{
			var arriba = alto < vol.Length - 1 ? vol[alto + 1] : -1;
			var abajo = bajo > 0 ? vol[bajo - 1] : -1;

			if (arriba >= abajo)
				acumulado += vol[++alto];
			else
				acumulado += vol[--bajo];
		}

		return new Huella(ticks[poc] * tick, ticks[bajo] * tick, ticks[alto] * tick);
	}
}

/// <summary>ATR de Wilder como ta.atr del Pine: nulo hasta tener 'periodo' velas; semilla = media simple.</summary>
public sealed class Atr
{
	private readonly int _periodo;
	private readonly List<decimal> _iniciales = new();
	private decimal? _cierreAnterior;

	public Atr(int periodo) => _periodo = Math.Max(1, periodo);

	public decimal? Valor { get; private set; }

	public void Agregar(Vela v)
	{
		var tr = _cierreAnterior is { } c ? Math.Max(v.High - v.Low, Math.Max(Math.Abs(v.High - c), Math.Abs(v.Low - c))) : v.High - v.Low;
		_cierreAnterior = v.Close;

		if (Valor is { } atr)
		{
			Valor = (atr * (_periodo - 1) + tr) / _periodo;
			return;
		}

		_iniciales.Add(tr);

		if (_iniciales.Count == _periodo)
			Valor = _iniciales.Average();
	}
}

/// <summary>EMA clásica (semilla = primer valor).</summary>
public sealed class Ema
{
	private readonly decimal _alfa;

	public Ema(int periodo) => _alfa = 2m / (Math.Max(1, periodo) + 1);

	public decimal? Valor { get; private set; }

	public decimal? Anterior { get; private set; }

	public void Agregar(decimal x)
	{
		Anterior = Valor;
		Valor = Valor is { } v ? v + _alfa * (x - v) : x;
	}
}

/// <summary>Agrega velas del gráfico en velas de 'minutos' alineadas al reloj de Nueva York (5 min o 1 hora).</summary>
public sealed class Agregador
{
	private readonly int _minutos;
	private Vela? _actual;

	public Agregador(int minutos) => _minutos = minutos;

	public List<Vela> Velas { get; } = new();

	/// <summary>Inicio (UTC) del periodo que contiene 'utc', alineado a la hora de Nueva York.</summary>
	public DateTime Periodo(DateTime utc)
	{
		var et = Zonas.AEt(utc);
		var minutoDelDia = et.Hour * 60 + et.Minute;
		var inicioEt = et.Date.AddMinutes(minutoDelDia - minutoDelDia % _minutos);
		return Zonas.AUtc(inicioEt, ZonaHoraria.NuevaYork);
	}

	/// <summary>Suma una vela cerrada; devuelve la vela agregada que se cerró al empezar otro periodo (o null).</summary>
	public Vela? Agregar(Vela v)
	{
		var inicio = Periodo(v.InicioUtc);
		Vela? cerrada = null;

		if (_actual is not null && _actual.InicioUtc != inicio)
		{
			cerrada = _actual;
			cerrada.Indice = Velas.Count;
			Velas.Add(cerrada);
			_actual = null;
		}

		if (_actual is null)
		{
			_actual = new Vela
			{
				InicioUtc = inicio,
				FinUtc = inicio.AddMinutes(_minutos),
				Open = v.Open,
				High = v.High,
				Low = v.Low,
				Close = v.Close,
				Volume = v.Volume,
				Delta = v.Delta,
				Ask = v.Ask,
				Bid = v.Bid
			};
		}
		else
		{
			_actual.High = Math.Max(_actual.High, v.High);
			_actual.Low = Math.Min(_actual.Low, v.Low);
			_actual.Close = v.Close;
			_actual.Volume += v.Volume;
			_actual.Delta += v.Delta;
			_actual.Ask += v.Ask;
			_actual.Bid += v.Bid;
		}

		// Si la vela del gráfico termina justo al final del periodo, el periodo queda cerrado ya.
		if (v.FinUtc >= _actual.FinUtc)
		{
			cerrada = _actual;
			cerrada.Indice = Velas.Count;
			Velas.Add(cerrada);
			_actual = null;
		}

		return cerrada;
	}

	/// <summary>Periodo en curso (aún sin cerrar), para lecturas en vivo.</summary>
	public Vela? Actual => _actual;
}
