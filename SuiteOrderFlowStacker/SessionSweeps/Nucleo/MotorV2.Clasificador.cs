namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Una novedad del clasificador o de los modelos para alertas, correo y CSV (la carcasa la consume en vivo).</summary>
public sealed class NovedadV2
{
	public EvaluacionBarrido? Evaluacion;

	/// <summary>"CONFIRMADO", "UPGRADE PREMIUM", "FALLIDO", "RUPTURA FALLIDA", "ROMPE NIVEL" o de modelos (S7).</summary>
	public string Tipo = "";

	public DateTime Utc;
	public string Texto = "";
}

/// <summary>S3: detección de T0 en el tick, seguimiento de extremos y evaluación del clasificador en cada barra de volumen.</summary>
public sealed partial class MotorSweeps
{
	private readonly Dictionary<int, EvaluacionBarrido> _evalPorNivel = new();
	private readonly List<decimal> _historialVolBeyond = new();
	private DateTime _ahoraV2;

	public MotorContextoV2 ContextoV2 { get; } = new();

	public IFuentesSuite Fuentes { get; set; } = SinFuentes.Instancia;

	public List<EvaluacionBarrido> Evaluaciones { get; } = new();

	public List<NovedadV2> NovedadesV2 { get; } = new();

	public DateTime AhoraV2 => _ahoraV2;

	public List<NovedadV2> TomarNovedadesV2()
	{
		lock (_sync)
		{
			var r = NovedadesV2.ToList();
			NovedadesV2.Clear();
			return r;
		}
	}

	private void Novedad(EvaluacionBarrido? e, string tipo, DateTime utc, string texto) =>
		NovedadesV2.Add(new NovedadV2 { Evaluacion = e, Tipo = tipo, Utc = utc, Texto = texto });

	/// <summary>Cada trade (real o APROX): extremos de las evaluaciones abiertas y T0 de los niveles nuevos.</summary>
	private void TickV2(DateTime utc, decimal precio)
	{
		_ahoraV2 = utc;

		foreach (var e in Evaluaciones)
		{
			if (!e.Congelada && e.Signo * (precio - e.Extremo) > 0)
				(e.Extremo, e.ExtremoUtc) = (precio, utc);

			if (e.Signo * (precio - e.ExtremoPierna) > 0 && (!e.Congelada || e.Estado == EstadoBarrido.Ruptura))
				e.ExtremoPierna = precio;
		}

		if (!EnVentana(utc))
			return;

		var minimo = P2.MinPenetrationTicks * Cfg.TamanoTick;

		foreach (var n in Niveles.Niveles)
		{
			if (!n.Activo || !Niveles.Vigilado(n) || n.CreadoUtc >= utc || _evalPorNivel.ContainsKey(n.Id))
				continue;

			var s = n.Lado.Signo();

			if (s * (precio - n.Precio) >= minimo)
			{
				var e = NuevaEvaluacion(n, utc, precio, BarraActual(), null, "");
				Novedad(e, "ROMPE NIVEL", utc, $"{Simbolo} rompe {e.Nombre} {Precio(n.Precio)} a las {TiempoV2.HoraEt(utc)}");
			}
		}
	}

	private int BarraActual() => BarrasV.Actual is not null ? BarrasV.Barras.Count : Math.Max(0, BarrasV.Barras.Count - 1);

	private EvaluacionBarrido NuevaEvaluacion(NivelLiquidez n, DateTime t0, decimal extremo, int barra, EvaluacionBarrido? padre, string origen)
	{
		var e = new EvaluacionBarrido
		{
			Id = IdEstable(n, t0),
			Nivel = n,
			Signo = n.Lado.Signo(),
			L = n.Precio,
			Extremo = extremo,
			ExtremoPierna = extremo,
			T0Utc = t0,
			ExtremoUtc = t0,
			BarraT0 = barra,
			Padre = padre?.Id,
			Origen = origen
		};

		(e.PreT0Delta, e.PreT0DeltaAprox) = DeltaPrevio(t0);
		Evaluaciones.Add(e);
		_evalPorNivel[n.Id] = e;

		if (Evaluaciones.Count > 400)
			Evaluaciones.RemoveRange(0, Evaluaciones.Count - 400);

		return e;
	}

	/// <summary>Id estable (nivel, lado, T0 al milisegundo): el mismo barrido recalculado tiene el mismo Id (CSV).</summary>
	public static long IdEstable(NivelLiquidez n, DateTime t0)
	{
		unchecked
		{
			var h = 1469598103934665603L;

			foreach (var v in new[] { (long)Math.Round(n.Precio * 100), (long)n.Lado, t0.Ticks / TimeSpan.TicksPerMillisecond, 0x55AAL })
			{
				h ^= v;
				h *= 1099511628211L;
			}

			return h & long.MaxValue;
		}
	}

	/// <summary>P12: delta real de los PreT0DeltaSec anteriores a T0 (trades guardados); si no hay, de las velas ("aprox").</summary>
	private (decimal Delta, bool Aprox) DeltaPrevio(DateTime t0)
	{
		var desde = t0.AddSeconds(-Math.Max(1, P2.PreT0DeltaSec));

		if (Almacen is { Count: > 0 } a && a.Ticks[0].Utc <= desde)
		{
			decimal d = 0;

			for (var i = a.PrimeroDesde(desde); i < a.Count && a.Ticks[i].Utc < t0; i++)
				d += a.Ticks[i].Compra switch { true => a.Ticks[i].Volumen, false => -a.Ticks[i].Volumen, _ => 0 };

			return (d, false);
		}

		return (Velas.Where(v => v.FinUtc > desde && v.InicioUtc < t0).Sum(v => v.Delta), true);
	}

	/// <summary>Contexto del clasificador con los datos del motor.</summary>
	public ContextoClasificador ContextoClasificador() => new()
	{
		Barras = BarrasV.Barras,
		P = P2,
		Tick = Cfg.TamanoTick,
		ContratosPorBarra = P2.ContratosPorBarra(Mnq),
		Fuentes = Fuentes,
		Ahora = _ahoraV2,
		Medianas = i => BarrasV.Medianas(i),
		PicoVelocidad = BarrasV.PicoVelocidad5s,
		MedianaVelocidad = () => BarrasV.MedianaVelocidad5s,
		MedianaVolBeyond = () => _historialVolBeyond.Count >= 5 ? (decimal)MotorBarrasVolumen.Mediana(_historialVolBeyond.Select(x => (double)x)) : null,
		H1 = () => MotorContextoV2.DeH1(H1.Valor),
		M15 = () => ContextoV2.M15,
		DivergenciaCvd = ContextoV2.Divergencia,
		Vwap = _ => ContextoV2.Vwap,
		Pine = e => e.Nivel.Zona is { Activa: false } z && Math.Abs((z.T0Utc - e.T0Utc).TotalMinutes) <= 2 ? !z.FalsoPine : null,
		ToquesNivel = Toques
	};

	/// <summary>Toques del nivel antes del barrido (grupos de velas que llegaron a EqualLevelTolTicks sin pasarlo), contando su origen.</summary>
	private int Toques(EvaluacionBarrido e)
	{
		var tol = P2.EqualLevelTolTicks * Cfg.TamanoTick;
		var toques = 1;
		var dentro = false;

		foreach (var v in Velas.Where(v => v.InicioUtc >= e.Nivel.CreadoUtc && v.FinUtc <= e.T0Utc))
		{
			var toca = e.Signo > 0 ? v.High >= e.L - tol && v.High <= e.L : v.Low <= e.L + tol && v.Low >= e.L;

			if (toca && !dentro)
				toques++;

			dentro = toca;
		}

		return toques;
	}

	/// <summary>Cada barra de volumen cerrada: se reevalúan las evaluaciones abiertas y las congeladas que aún pueden cambiar.</summary>
	private void BarraCerradaV2(BarraVolumen b)
	{
		_ahoraV2 = b.FinUtc;
		var ctx = ContextoClasificador();

		foreach (var e in Evaluaciones.ToList())
		{
			if (!Vigente(e, b.Indice))
				continue;

			var antes = e.Estado;
			var transicion = Clasificador.Evaluar(e, ctx);

			if (transicion is null)
				continue;

			if (!e.Congelada || antes == EstadoBarrido.Pendiente)
			{
				Novedad(e, "CONFIRMADO", b.FinUtc, $"{e.Estado.Texto()} {e.Nombre} {Precio(e.L)} | {e.Total:0}/100");

				if (e.Estado.EsTrampa())
					RegistrarBfvgExistentes(e);

				ModelosAlEstado(e, "CONFIRMADO", b.FinUtc);

				if (TiempoV2.EsRth(e.T0Utc))
				{
					_historialVolBeyond.Add(e.VolBeyond);

					if (_historialVolBeyond.Count > Math.Max(5, P2.SweepHistoryCount))
						_historialVolBeyond.RemoveAt(0);
				}

				continue;
			}

			Novedad(e, transicion, b.FinUtc, $"{transicion} {e.Nombre} {Precio(e.L)} | {e.Total:0}/100");
			ModelosAlEstado(e, transicion, b.FinUtc);

			// FALLIDO: candidato a RUPTURA (nueva evaluación); RUPTURA FALLIDA: nueva evaluación como TRAMPA.
			if (e.Estado == EstadoBarrido.Fallido)
				NuevaEvaluacion(e.Nivel, b.FinUtc, e.Signo > 0 ? b.High : b.Low, b.Indice, e, "TRAS FALLIDO");
			else if (e.Estado == EstadoBarrido.RupturaFallida)
				Hija(e, b, "TRAS RUPTURA FALLIDA");
		}

		ModelosBarra(b);
	}

	/// <summary>Evaluación hija como TRAMPA tras una RUPTURA FALLIDA: el mismo barrido (T0, extremo) con ventana nueva.</summary>
	private void Hija(EvaluacionBarrido e, BarraVolumen b, string origen)
	{
		var h = NuevaEvaluacion(e.Nivel, e.T0Utc.AddTicks(1), e.Extremo, e.BarraT0, e, origen);
		h.ExtremoUtc = e.ExtremoUtc;
		h.VentanaDesdeBarra = b.Indice;
		h.VentanaDesdeUtc = b.FinUtc;
	}

	/// <summary>¿Sigue viva? Pendiente; o congelada dentro de su ventana de transiciones (Fallido / upgrade / ruptura fallida).</summary>
	private bool Vigente(EvaluacionBarrido e, int barra)
	{
		if (!e.Congelada)
			return true;

		var desde = barra - e.BarraConfirmacion;

		return e.Estado switch
		{
			EstadoBarrido.TrampaB or EstadoBarrido.TrampaA or EstadoBarrido.TrampaPremium => desde <= Math.Max(60, P2.MaxEvaluationBars * 4),
			EstadoBarrido.Ruptura => desde <= P2.FailedBreakBars,
			_ => false
		};
	}

	/// <summary>Al cierre de cada vela de 1 minuto: contexto (5m, 15m, CVD, VWAP, EMA 15) y recuperación 1m / 5m.</summary>
	private void ProcesarV2Cierre(Vela v)
	{
		ContextoV2.Configurar(P2.MicroImpulseEmaLen);
		ContextoV2.Agregar(v);
		MitigarLvn(v);
		ProcesarFvgV2(v);
		ModelosVela(v);
		var cinco = (v.FinUtc.Ticks / TimeSpan.TicksPerMinute) % 5 == 0;
		var minimo = P2.RecoveryTicks * Cfg.TamanoTick;

		foreach (var e in Evaluaciones.Where(e => v.FinUtc > e.ExtremoUtc && v.FinUtc - e.T0Utc < TimeSpan.FromHours(2)))
		{
			var recuperado = e.Signo * (e.Extremo - v.Close) >= minimo;

			if (recuperado)
			{
				e.Recovery1mUtc ??= v.FinUtc;

				if (cinco)
					e.Recovery5mUtc ??= v.FinUtc;
			}
		}
	}
}
