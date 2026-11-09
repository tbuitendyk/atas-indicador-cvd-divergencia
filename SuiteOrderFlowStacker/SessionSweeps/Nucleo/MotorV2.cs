namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Un trade guardado (histórico de los trades acumulados de ATAS o en vivo).</summary>
public readonly record struct TickV2(DateTime Utc, decimal Precio, decimal Volumen, bool? Compra);

/// <summary>
/// Trades del instrumento en orden de tiempo: los del historial (petición de trades acumulados) y los que llegan en vivo.
/// La carcasa lo conserva entre recálculos: un recálculo vuelve a construir las barras de volumen con los mismos trades.
/// Seguro entre hilos (ATAS entrega los trades y la respuesta del historial en hilos distintos del cálculo).
/// </summary>
public sealed class AlmacenTicks
{
	private readonly object _cerrojo = new();
	private readonly List<TickV2> _ticks = new();

	/// <summary>Copia de los trades (para pruebas e inspección).</summary>
	public IReadOnlyList<TickV2> Ticks
	{
		get
		{
			lock (_cerrojo)
				return _ticks.ToList();
		}
	}

	public int Count
	{
		get
		{
			lock (_cerrojo)
				return _ticks.Count;
		}
	}

	public TickV2 this[int i]
	{
		get
		{
			lock (_cerrojo)
				return _ticks[i];
		}
	}

	/// <summary>Añade un trade en vivo (si llega con hora anterior al último, se ignora: el orden se mantiene).</summary>
	public bool Agregar(TickV2 t)
	{
		lock (_cerrojo)
		{
			if (_ticks.Count > 0 && t.Utc < _ticks[^1].Utc)
				return false;

			_ticks.Add(t);
			return true;
		}
	}

	/// <summary>Trades del historial: se ponen DELANTE de los que ya hay (solo los anteriores al primero guardado).</summary>
	public int AgregarHistorial(IEnumerable<TickV2> historicos)
	{
		lock (_cerrojo)
		{
			var primero = _ticks.Count > 0 ? _ticks[0].Utc : DateTime.MaxValue;
			var nuevos = historicos.Where(t => t.Utc < primero).OrderBy(t => t.Utc).ToList();
			_ticks.InsertRange(0, nuevos);
			return nuevos.Count;
		}
	}

	/// <summary>Quita los trades anteriores a 'utc' (la carcasa guarda como mucho un par de días).</summary>
	public int RecortarAntesDe(DateTime utc)
	{
		lock (_cerrojo)
		{
			var n = PrimeroDesdeSinCerrojo(utc);
			_ticks.RemoveRange(0, n);
			return n;
		}
	}

	public void Vaciar()
	{
		lock (_cerrojo)
			_ticks.Clear();
	}

	/// <summary>Primer índice con hora >= utc.</summary>
	public int PrimeroDesde(DateTime utc)
	{
		lock (_cerrojo)
			return PrimeroDesdeSinCerrojo(utc);
	}

	private int PrimeroDesdeSinCerrojo(DateTime utc)
	{
		int bajo = 0, alto = _ticks.Count;

		while (bajo < alto)
		{
			var medio = (bajo + alto) / 2;

			if (_ticks[medio].Utc < utc)
				bajo = medio + 1;
			else
				alto = medio;
		}

		return bajo;
	}
}

/// <summary>Session Sweeps Pro v2.0 sobre el motor copiado del NQ: barras de volumen (S2) y lo que se apoya en ellas.</summary>
public sealed partial class MotorSweeps
{
	private readonly HashSet<DateTime> _minutosConTicks = new();
	private int _cursorTicks;

	public MotorBarrasVolumen BarrasV { get; } = new();

	/// <summary>Trades guardados (los pone la carcasa; null = solo APROX desde el footprint).</summary>
	public AlmacenTicks? Almacen { get; set; }

	/// <summary>
	/// Instrumento del gráfico (2.2): raíz, contrato, especificación, calibración. La carcasa siempre lo pone; el valor por
	/// defecto (NQ validado) es solo para las pruebas.
	/// </summary>
	public EstadoInstrumento Instrumento { get; set; } = EstadoInstrumento.Crear("NQ", "", 0.25m, null, 500);

	public RaizInstrumento Raiz => Instrumento.Raiz;

	/// <summary>Minutos reconstruidos desde el footprint (sin trades) en la última vela procesada.</summary>
	public bool UltimoMinutoAprox { get; private set; }

	public ParametrosV2 P2 => Cfg.V2;

	private void ConfigurarBarras()
	{
		BarrasV.AlCerrar ??= BarraCerradaV2;
		BarrasV.Tick = Cfg.TamanoTick;
		BarrasV.ContratosPorBarra = P2.ContratosPorBarra();
		BarrasV.MedianLookbackBars = P2.MedianLookbackBars;
	}

	/// <summary>Vela cerrada: los trades de su minuto (guardados) o, si no hay, la reconstrucción APROX de su footprint.</summary>
	private void AlimentarBarras(Vela v)
	{
		ConfigurarBarras();

		if (Almacen is { } a)
		{
			if (_cursorTicks < a.Count && a[_cursorTicks].Utc < v.InicioUtc)
				_cursorTicks = a.PrimeroDesde(v.InicioUtc);

			var hubo = false;

			while (_cursorTicks < a.Count && a[_cursorTicks].Utc < v.FinUtc)
			{
				var t = a[_cursorTicks++];
				BarrasV.Agregar(t.Utc, t.Precio, t.Volumen, t.Compra);
				TickV2(t.Utc, t.Precio);
				hubo = true;
			}

			if (hubo)
				_minutosConTicks.Add(v.InicioUtc);
		}

		UltimoMinutoAprox = !_minutosConTicks.Contains(v.InicioUtc);

		if (UltimoMinutoAprox)
		{
			foreach (var (utc, precio, vol, compra) in MotorBarrasVolumen.Aproximar(v, Cfg.TamanoTick))
			{
				BarrasV.Agregar(utc, precio, vol, compra, aprox: true);
				TickV2(utc, precio);
			}
		}

		if (_minutosConTicks.Count > 5000)
			_minutosConTicks.RemoveWhere(m => m < v.InicioUtc.AddDays(-3));
	}

	/// <summary>Un trade en vivo para las barras de volumen (lo llama Trade()).</summary>
	private void TradeBarras(DateTime utc, decimal precio, decimal volumen, bool? compra)
	{
		ConfigurarBarras();

		if (Almacen is { } a)
		{
			// Los trades que la carcasa guardó mientras se recalculaba (velas aún abiertas) entran primero, en orden.
			while (_cursorTicks < a.Count && a[_cursorTicks].Utc <= utc)
			{
				var t = a[_cursorTicks++];
				BarrasV.Agregar(t.Utc, t.Precio, t.Volumen, t.Compra);
				TickV2(t.Utc, t.Precio);
			}

			if (!a.Agregar(new TickV2(utc, precio, volumen, compra)))
				return;

			_cursorTicks = a.Count;
		}

		BarrasV.Agregar(utc, precio, volumen, compra);
		TickV2(utc, precio);
		_minutosConTicks.Add(new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc));
	}

	private void ProcesarV2(Vela v)
	{
		ProcesarV2Cierre(v);
	}
}
