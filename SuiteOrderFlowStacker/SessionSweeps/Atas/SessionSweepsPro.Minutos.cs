namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;

using SessionSweeps.Nucleo;

/// <summary>
/// v5 Sweep Pro (FAST) en un gráfico de 5 minutos (o de 2 a 15): el motor sigue recibiendo velas de 1 minuto. Las del
/// día salen de los trades (historial de trades acumulados + en vivo: exactas); las de días anteriores, de partir la vela
/// del gráfico (MinutosV5.Partir; con su footprint en los días con detalle). En vivo, cada minuto se cierra con el primer
/// trade del siguiente. Al dibujar, cada vela de 1 minuto del motor se lleva a la vela del gráfico que la contiene: las
/// zonas de Asia, Londres, Premercado, NY mañana y tarde, los barridos y los gatillos quedan en su hora.
/// </summary>
public partial class SessionSweepsPro
{
	private const int MaximoMinutosGrafico = 15;

	private readonly ConstructorMinuto _minutoVivo = new();

	// Vela del motor (índice) → vela del gráfico. Se lee al dibujar (otro hilo): primero el arreglo, luego la cuenta.
	private int[] _mapaBarras = Array.Empty<int>();
	private int _mapaCuenta;

	private bool GraficoDeMinutos => _minutos > 1;

	private void ReiniciarMinutos()
	{
		_minutoVivo.Vaciar();
		_mapaBarras = GraficoDeMinutos ? new int[Math.Max(16, CurrentBar * _minutos + 64)] : Array.Empty<int>();
		_mapaCuenta = 0;
	}

	/// <summary>Vela del gráfico de una vela del motor (en 1 minuto son la misma).</summary>
	private int B(int indiceMotor)
	{
		if (!GraficoDeMinutos)
			return indiceMotor;

		var mapa = _mapaBarras;
		var n = Math.Min(_mapaCuenta, mapa.Length);

		if (n == 0 || indiceMotor >= n)
			return Math.Max(0, CurrentBar - 1);

		return mapa[Math.Max(0, indiceMotor)];
	}

	/// <summary>Una vela de 1 minuto al motor; si la acepta, queda anotada su vela del gráfico.</summary>
	private void ProcesarMinuto(MotorSweeps motor, VelaEntrada m, int barGrafico)
	{
		if (!motor.Procesar(m))
			return;

		if (_mapaCuenta >= _mapaBarras.Length)
		{
			var nuevo = new int[_mapaBarras.Length * 2];
			Array.Copy(_mapaBarras, nuevo, _mapaCuenta);
			_mapaBarras = nuevo;
		}

		_mapaBarras[_mapaCuenta] = barGrafico;
		_mapaCuenta++;
	}

	/// <summary>
	/// Vela cerrada del gráfico de N minutos: sus velas de 1 minuto. Con los trades del día guardados desde antes de la vela,
	/// salen exactas de ellos; si no, de partir la vela. En vivo, primero se cierra el minuto que se estaba armando.
	/// </summary>
	private void VelaDeMinutos(MotorSweeps motor, int bar)
	{
		var c = GetCandle(bar);
		var (inicio, fin) = (c.Time, c.Time.AddMinutes(_minutos));

		if (_enVivo && _minutoVivo.CerrarSiTermino(fin) is { } abierta)
			ProcesarMinuto(motor, abierta, bar);

		var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;
		var minutos = _almacen.PrimerUtc <= inicio ? MinutosV5.DesdeTicks(_almacen.Entre(inicio, fin), inicio, fin) : MinutosV5.Partir(Entrada(bar), _minutos, tick);

		// El motor ignora los minutos que ya recibió en vivo.
		foreach (var m in minutos)
			ProcesarMinuto(motor, m, bar);
	}

	/// <summary>
	/// Al entrar en vivo: los minutos ya cerrados de la vela en curso. Con los trades guardados, el armador de minutos se
	/// siembra con ellos (y queda armando el minuto actual); si no, se aproximan partiendo lo que va de la vela. El historial
	/// de trades que se pide al entrar en vivo recalcula después con todo exacto.
	/// </summary>
	private void EntrarEnVivoMinutos(MotorSweeps motor)
	{
		if (CurrentBar <= 0)
			return;

		var bar = CurrentBar - 1;
		var inicio = GetCandle(bar).Time;
		_minutoVivo.Vaciar();

		if (_almacen.PrimerUtc <= inicio)
		{
			foreach (var t in _almacen.Entre(inicio, DateTime.MaxValue))
			{
				if (_minutoVivo.Trade(t.Utc, t.Precio, t.Volumen, t.Compra) is { } cerrado)
					ProcesarMinuto(motor, cerrado, bar);
			}

			return;
		}

		var transcurridos = (int)Math.Floor((DateTime.UtcNow - inicio).TotalMinutes);

		if (transcurridos is > 0 && transcurridos < _minutos)
		{
			var tick = InstrumentInfo?.TickSize is > 0 ? InstrumentInfo.TickSize : 0.25m;

			foreach (var m in MinutosV5.Partir(Entrada(bar), transcurridos, tick))
				ProcesarMinuto(motor, m, bar);
		}
	}

	/// <summary>En vivo, gráfico de N minutos: el trade arma el minuto; el minuto que se cierra entra al motor.</summary>
	private (decimal Apertura, DateTime Inicio) TradeMinutos(MotorSweeps motor, DateTime utc, decimal precio, decimal volumen, bool? compra)
	{
		lock (_sync)
		{
			if (_minutoVivo.Trade(utc, precio, volumen, compra) is { } cerrado)
				ProcesarMinuto(motor, cerrado, BarDe(cerrado.InicioUtc));
		}

		return (_minutoVivo.Apertura, _minutoVivo.Inicio);
	}
}
