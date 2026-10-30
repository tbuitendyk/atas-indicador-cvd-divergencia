namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Una vela de 1 minuto cerrada, tal como la entrega el indicador maestro desde ATAS.</summary>
public sealed class EntradaVela1M
{
	public DateTime InicioUtc { get; set; }

	public decimal Apertura { get; set; }

	public decimal Maximo { get; set; }

	public decimal Minimo { get; set; }

	public decimal Cierre { get; set; }

	public decimal Volumen { get; set; }

	public decimal Delta { get; set; }

	public decimal DeltaMaximo { get; set; }

	public decimal DeltaMinimo { get; set; }

	/// <summary>Volumen por precio: Bid = ventas agresivas, Ask = compras agresivas. Vacío si ATAS no lo tiene.</summary>
	public List<NivelEntrada> Niveles { get; } = new();
}

public readonly record struct NivelEntrada(decimal Precio, decimal Bid, decimal Ask);

internal readonly record struct Nivel(decimal Bid, decimal Ask)
{
	public decimal Volumen => Bid + Ask;
}

/// <summary>Lectura del footprint de una vela: POC, área de valor (70 %) y extremos.</summary>
internal sealed class Huella
{
	public decimal Poc { get; init; }

	public decimal Val { get; init; }

	public decimal Vah { get; init; }

	/// <summary>(POC - mínimo) / rango.</summary>
	public decimal PosPoc { get; init; }

	/// <summary>(centro del área de valor - mínimo) / rango.</summary>
	public decimal PosVa { get; init; }

	public Nivel EnMaximo { get; init; }

	public Nivel EnMinimo { get; init; }

	public static Huella? Calcular(SortedDictionary<long, Nivel> niveles, ConfiguracionCompleto cfg, decimal maximo, decimal minimo)
	{
		if (niveles.Count == 0)
			return null;

		var ticks = niveles.Keys.ToArray();
		var volumenes = ticks.Select(t => niveles[t].Volumen).ToArray();
		var total = volumenes.Sum();

		if (total <= 0)
			return null;

		var poc = 0;

		for (var i = 1; i < volumenes.Length; i++)
		{
			if (volumenes[i] > volumenes[poc])
				poc = i;
		}

		// Área de valor: desde el POC se suma el nivel vecino de mayor volumen hasta llegar al 70 %.
		int abajo = poc, arriba = poc;
		var acumulado = volumenes[poc];

		while (acumulado < total * 0.70m && (abajo > 0 || arriba < volumenes.Length - 1))
		{
			var siguienteArriba = arriba < volumenes.Length - 1 ? volumenes[arriba + 1] : -1m;
			var siguienteAbajo = abajo > 0 ? volumenes[abajo - 1] : -1m;

			if (siguienteArriba >= siguienteAbajo)
				acumulado += volumenes[++arriba];
			else
				acumulado += volumenes[--abajo];
		}

		var precioPoc = cfg.APrecio(ticks[poc]);
		var val = cfg.APrecio(ticks[abajo]);
		var vah = cfg.APrecio(ticks[arriba]);
		var rango = maximo - minimo;

		return new Huella
		{
			Poc = precioPoc,
			Val = val,
			Vah = vah,
			PosPoc = rango > 0 ? (precioPoc - minimo) / rango : 0.5m,
			PosVa = rango > 0 ? ((val + vah) / 2 - minimo) / rango : 0.5m,
			EnMaximo = niveles.TryGetValue(cfg.ATick(maximo), out var enMaximo) ? enMaximo : default,
			EnMinimo = niveles.TryGetValue(cfg.ATick(minimo), out var enMinimo) ? enMinimo : default
		};
	}
}

/// <summary>Vela cerrada de cualquier marco (1M de ATAS o 5M/15M/1H construidas con 1M completas).</summary>
internal sealed class Vela
{
	public Marco Marco;
	public int Indice;
	public DateTime InicioUtc;
	public DateTime FinUtc;
	public decimal Apertura, Maximo, Minimo, Cierre, Volumen, Delta, DeltaMaximo, DeltaMinimo;
	public int Velas1M = 1;
	public bool FootprintCompleto;
	public SortedDictionary<long, Nivel>? Niveles;
	public Huella? Huella;

	// CVD de la sesión (reinicio configurable); Parcial si la base no empezó en un reinicio.
	public decimal CvdApertura, CvdMaximo, CvdMinimo, CvdCierre;
	public int SesionCvd;
	public bool CvdParcial;

	public decimal Rango => Maximo - Minimo;

	public decimal Cuerpo => Math.Abs(Cierre - Apertura);

	public bool Alcista => Cierre > Apertura;

	public bool Bajista => Cierre < Apertura;

	public bool Toca(decimal inferior, decimal superior) => Minimo <= superior && Maximo >= inferior;

	/// <summary>Extremo en la dirección contraria a la operación: mínimo para largos, máximo para cortos.</summary>
	public decimal ExtremoContrario(Direccion d) => d == Direccion.Larga ? Minimo : Maximo;
}

/// <summary>Construye velas de 5M, 15M o 1H solo con periodos completos de velas de 1M.</summary>
internal sealed class Agregador
{
	private readonly Marco _marco;
	private readonly bool _conNiveles;
	private readonly ConfiguracionCompleto _cfg;
	private Vela? _actual;
	private int _cuenta;

	public Agregador(Marco marco, bool conNiveles, ConfiguracionCompleto cfg)
	{
		_marco = marco;
		_conNiveles = conNiveles;
		_cfg = cfg;
	}

	public List<Vela> Velas { get; } = new();

	/// <summary>Periodos descartados por faltar velas de 1M.</summary>
	public int Descartadas { get; private set; }

	public static DateTime Piso(DateTime t, int minutos) => new(t.Ticks - t.Ticks % TimeSpan.FromMinutes(minutos).Ticks, t.Kind);

	/// <summary>Suma una vela de 1M cerrada. Devuelve la vela del marco si este periodo acaba de completarse.</summary>
	public Vela? Agregar(Vela m1)
	{
		var minutos = (int)_marco;
		var inicio = Piso(m1.InicioUtc, minutos);

		if (_actual is not null && _actual.InicioUtc != inicio)
		{
			// El periodo anterior terminó sin su última vela: incompleto.
			Descartadas++;
			_actual = null;
		}

		if (_actual is null)
		{
			_actual = new Vela
			{
				Marco = _marco,
				InicioUtc = inicio,
				FinUtc = inicio.AddMinutes(minutos),
				Apertura = m1.Apertura,
				Maximo = m1.Maximo,
				Minimo = m1.Minimo,
				Cierre = m1.Cierre,
				Volumen = m1.Volumen,
				Delta = m1.Delta,
				DeltaMaximo = m1.DeltaMaximo,
				DeltaMinimo = m1.DeltaMinimo,
				FootprintCompleto = m1.FootprintCompleto,
				CvdApertura = m1.CvdApertura,
				CvdMaximo = m1.CvdMaximo,
				CvdMinimo = m1.CvdMinimo,
				CvdCierre = m1.CvdCierre,
				SesionCvd = m1.SesionCvd,
				CvdParcial = m1.CvdParcial,
				Niveles = _conNiveles ? new SortedDictionary<long, Nivel>() : null
			};
			_cuenta = 0;
		}
		else
		{
			var deltaPrevio = _actual.Delta;
			_actual.Maximo = Math.Max(_actual.Maximo, m1.Maximo);
			_actual.Minimo = Math.Min(_actual.Minimo, m1.Minimo);
			_actual.Cierre = m1.Cierre;
			_actual.Volumen += m1.Volumen;
			_actual.Delta += m1.Delta;
			_actual.DeltaMaximo = Math.Max(_actual.DeltaMaximo, deltaPrevio + m1.DeltaMaximo);
			_actual.DeltaMinimo = Math.Min(_actual.DeltaMinimo, deltaPrevio + m1.DeltaMinimo);
			_actual.FootprintCompleto &= m1.FootprintCompleto;
			_actual.CvdMaximo = Math.Max(_actual.CvdMaximo, m1.CvdMaximo);
			_actual.CvdMinimo = Math.Min(_actual.CvdMinimo, m1.CvdMinimo);
			_actual.CvdCierre = m1.CvdCierre;
			_actual.CvdParcial |= m1.CvdParcial;
		}

		if (_actual.Niveles is not null && m1.Niveles is not null)
		{
			foreach (var (tick, nivel) in m1.Niveles)
			{
				_actual.Niveles[tick] = _actual.Niveles.TryGetValue(tick, out var previo)
					? new Nivel(previo.Bid + nivel.Bid, previo.Ask + nivel.Ask)
					: nivel;
			}
		}

		_cuenta++;

		if (m1.FinUtc < _actual.FinUtc)
			return null;

		var vela = _actual;
		_actual = null;

		if (_cuenta != minutos)
		{
			Descartadas++;
			return null;
		}

		vela.Velas1M = _cuenta;
		vela.Indice = Velas.Count;

		if (vela.Niveles is not null)
			vela.Huella = Huella.Calcular(vela.Niveles, _cfg, vela.Maximo, vela.Minimo);

		Velas.Add(vela);
		return vela;
	}
}
