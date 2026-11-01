namespace NqSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class LvnNivel
{
	public decimal Precio;
	public bool Manual;
	public double Prominencia;

	/// <summary>1 = el mejor LVN calculado; 0 = manual.</summary>
	public int Ranking;
}

public sealed class ResultadoPerfil
{
	public decimal Poc, Vah, Val;
	public List<LvnNivel> Lvns = new();
}

/// <summary>
/// Perfil de volumen interno para los LVN (líneas amarillas) y para POC/VAH/VAL. Se acumula por día operativo, separando
/// RTH (NYOpen a 16:00 ET) del resto, así cualquier rango del ajuste LVNProfileRange se arma sumando días.
/// </summary>
public sealed class MotorPerfil
{
	private sealed class Dia
	{
		public readonly Dictionary<long, double> Rth = new(), Eth = new();
	}

	private readonly ConfiguracionSweeps _cfg;
	private readonly SortedDictionary<DateTime, Dia> _dias = new();

	public MotorPerfil(ConfiguracionSweeps cfg) => _cfg = cfg;

	public void Agregar(Vela v)
	{
		var dia = Zonas.DiaOperativo(v.InicioUtc);

		if (!_dias.TryGetValue(dia, out var d))
		{
			d = new Dia();
			_dias[dia] = d;

			while (_dias.Count > Math.Max(40, _cfg.LvnDias + 5))
				_dias.Remove(_dias.Keys.First());
		}

		var hora = Zonas.AEt(v.InicioUtc).TimeOfDay;
		var destino = hora >= _cfg.NYOpen && hora < new TimeSpan(16, 0, 0) ? d.Rth : d.Eth;
		var tick = _cfg.TamanoTick;

		if (v.Niveles is { Count: > 0 } niveles)
		{
			foreach (var (t, n) in niveles)
				destino[t] = destino.GetValueOrDefault(t) + n.Bid + n.Ask;

			return;
		}

		// Sin footprint: el volumen se reparte por igual entre el High y el Low.
		var desde = (long)Math.Round(v.Low / tick);
		var hasta = (long)Math.Round(v.High / tick);
		var porTick = (double)v.Volume / Math.Max(1, hasta - desde + 1);

		for (var t = desde; t <= hasta; t++)
			destino[t] = destino.GetValueOrDefault(t) + porTick;
	}

	/// <summary>Perfil del rango LVNProfileRange en el día operativo 'hoy': POC, VA y LVN (más los manuales).</summary>
	public ResultadoPerfil? Lvn(DateTime hoy)
	{
		var partes = new List<Dictionary<long, double>>();
		var anterior = _dias.Keys.Where(k => k < hoy).DefaultIfEmpty().Max();

		switch (_cfg.LVNProfileRange)
		{
			case RangoPerfilLvn.DiaActual:
				if (_dias.TryGetValue(hoy, out var h))
					partes.AddRange(new[] { h.Rth, h.Eth });

				break;

			case RangoPerfilLvn.UltimosNDias:
				foreach (var d in _dias.Where(kv => kv.Key <= hoy).Reverse().Take(Math.Max(1, _cfg.LvnDias)))
					partes.AddRange(new[] { d.Value.Rth, d.Value.Eth });

				break;

			case RangoPerfilLvn.Custom:
				foreach (var d in _dias.Where(kv => kv.Key >= _cfg.LvnDesdeEt.Date && kv.Key <= _cfg.LvnHastaEt.Date))
					partes.AddRange(new[] { d.Value.Rth, d.Value.Eth });

				break;

			default:
				// RTH del día anterior + overnight del día actual.
				if (anterior != default && _dias.TryGetValue(anterior, out var a))
					partes.Add(a.Rth);

				if (_dias.TryGetValue(hoy, out var actual))
					partes.Add(actual.Eth);

				break;
		}

		return Calcular(partes, true);
	}

	/// <summary>POC, VAH y VAL del día operativo anterior completo (objetivos de la proyección).</summary>
	public ResultadoPerfil? DiaAnterior(DateTime hoy)
	{
		var anterior = _dias.Keys.Where(k => k < hoy).DefaultIfEmpty().Max();
		return anterior != default && _dias.TryGetValue(anterior, out var d) ? Calcular(new() { d.Rth, d.Eth }, false) : null;
	}

	private ResultadoPerfil? Calcular(List<Dictionary<long, double>> partes, bool conLvn)
	{
		var porTick = new Dictionary<long, double>();

		foreach (var parte in partes)
		{
			foreach (var (t, v) in parte)
				porTick[t] = porTick.GetValueOrDefault(t) + v;
		}

		if (porTick.Count == 0)
			return null;

		long min = porTick.Keys.Min(), max = porTick.Keys.Max();
		var tpf = Math.Max(1, _cfg.RowSizeTicks);

		while ((max - min) / tpf + 1 > Math.Max(10, _cfg.MaxRows))
			tpf++;

		var primera = Div(min, tpf);
		var filas = new double[Div(max, tpf) - primera + 1];

		foreach (var (t, v) in porTick)
			filas[Div(t, tpf) - primera] += v;

		decimal Precio(int fila) => (primera + fila) * tpf * _cfg.TamanoTick;

		var poc = Array.IndexOf(filas, filas.Max());
		var (val, vah) = AreaDeValor(filas, poc, 70);
		var r = new ResultadoPerfil { Poc = Precio(poc), Val = Precio(val), Vah = Precio(vah) };

		if (!conLvn)
			return r;

		foreach (var m in _cfg.LvnManuales.Distinct())
			r.Lvns.Add(new LvnNivel { Precio = m, Manual = true });

		var s = Gauss(filas, _cfg.LvnSigma);
		var maximo = s.Max();
		var candidatos = new List<(int Fila, double Prominencia, double Score)>();

		for (var i = 1; i < s.Length - 1; i++)
		{
			var j = i;

			while (j + 1 < s.Length && s[j + 1] == s[i])
				j++;

			var esMinimo = j + 1 < s.Length && s[i] <= s[i - 1] && s[j] <= s[j + 1] && (s[i] < s[i - 1] || s[j] < s[j + 1]);
			var centro = (i + j) / 2;
			i = j;

			if (!esMinimo)
				continue;

			var valle = s[centro];
			var menor = Math.Min(Pico(centro, -1), Pico(centro, +1));

			if (menor <= valle)
				continue;

			var prominencia = (menor - valle) / menor;

			if (prominencia * 100 >= _cfg.LvnProminencia && valle <= _cfg.LvnMaxPctPoc / 100.0 * maximo)
				candidatos.Add((centro, prominencia, prominencia * (1 - valle / maximo)));
		}

		var elegidos = new List<(int Fila, double Prominencia)>();

		foreach (var c in candidatos.OrderByDescending(c => c.Score))
		{
			if (elegidos.Count >= _cfg.MaxLvn)
				break;

			var precio = Precio(c.Fila);

			if (elegidos.All(e => Math.Abs(e.Fila - c.Fila) * tpf >= _cfg.LvnSeparacionTicks)
				&& r.Lvns.All(m => Math.Abs(m.Precio - precio) >= _cfg.Ticks(_cfg.LvnSeparacionTicks)))
			{
				elegidos.Add((c.Fila, c.Prominencia));
			}
		}

		r.Lvns.AddRange(elegidos.Select((e, i) => new LvnNivel { Precio = Precio(e.Fila), Prominencia = e.Prominencia, Ranking = i + 1 }));
		return r;

		// Pico de un lado: el máximo hasta encontrar un valor más bajo que el valle (prominencia topográfica).
		double Pico(int desde, int dir)
		{
			var pico = double.MinValue;

			for (var k = desde + dir; k >= 0 && k < s.Length; k += dir)
			{
				if (s[k] < s[desde])
					break;

				pico = Math.Max(pico, s[k]);
			}

			return pico;
		}
	}

	private static long Div(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

	/// <summary>VA por el método CME: se comparan las 2 filas de arriba con las 2 de abajo y se añade la mayor.</summary>
	public static (int Val, int Vah) AreaDeValor(double[] v, int poc, double porcentaje)
	{
		var total = v.Sum();
		var objetivo = total * porcentaje / 100.0;
		var acumulado = v[poc];
		int bajo = poc, alto = poc;

		while (acumulado < objetivo && (bajo > 0 || alto < v.Length - 1))
		{
			double arriba = 0, abajo = 0;
			int nArriba = 0, nAbajo = 0;

			for (var k = 1; k <= 2; k++)
			{
				if (alto + k < v.Length)
				{
					arriba += v[alto + k];
					nArriba++;
				}

				if (bajo - k >= 0)
				{
					abajo += v[bajo - k];
					nAbajo++;
				}
			}

			if (nAbajo == 0 || (nArriba > 0 && arriba >= abajo))
			{
				alto += nArriba;
				acumulado += arriba;
			}
			else
			{
				bajo -= nAbajo;
				acumulado += abajo;
			}
		}

		return (bajo, alto);
	}

	public static double[] Gauss(double[] v, double sigma)
	{
		if (sigma < 0.3 || v.Length < 3)
			return (double[])v.Clone();

		var radio = (int)Math.Ceiling(3 * sigma);
		var pesos = Enumerable.Range(-radio, 2 * radio + 1).Select(k => Math.Exp(-(k * k) / (2 * sigma * sigma))).ToArray();
		var salida = new double[v.Length];

		for (var i = 0; i < v.Length; i++)
		{
			double suma = 0, peso = 0;

			for (var k = -radio; k <= radio; k++)
			{
				if (i + k < 0 || i + k >= v.Length)
					continue;

				suma += v[i + k] * pesos[k + radio];
				peso += pesos[k + radio];
			}

			salida[i] = suma / peso;
		}

		return salida;
	}
}
