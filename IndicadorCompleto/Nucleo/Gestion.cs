namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Gestión simulada de una señal (nunca hay órdenes reales). Entrada de referencia: el cierre de la vela gatillo; se
/// evalúa desde la vela siguiente. Salidas: SL, TP, cierre cruzado por la EMA en contra (nota del propietario) y
/// candidata de agotamiento seguida de un arranque contrario. Una vela que toca SL y TP se marca ambigua y cuenta como SL.
/// </summary>
internal sealed class Simulacion
{
	public Senal Senal = null!;
	public decimal StopActual;
	public bool Abierta = true;
	public DateTime? SalidaUtc;
	public decimal? PrecioSalida;
	public string MotivoSalida = "";
	public bool Ambigua;
	public decimal MfeTicks, MaeTicks;
	public Vela? Candidata;
	public int EdadCandidata;
	public string Advertencia = "";
	public decimal? StopPropuesto;
	public bool StopPorPoc;
	public decimal ResultadoTicks, ResultadoUsd, R;

	public Direccion Direccion => Senal.Direccion;

	public PlanRiesgo Plan => Senal.Plan;
}

internal sealed class Estadistica
{
	public string Nombre = "";
	public int Operaciones, Ganadoras, Perdedoras, Ambiguas, Abiertas;
	public decimal Acierto, FactorBeneficio, ExpectativaUsd, ExpectativaR, ResultadoTotal, DrawdownMaximo, MfeMedio, MaeMedio;

	public string Resumen() => Operaciones == 0
		? $"{Nombre}: sin operaciones cerradas"
		: $"{Nombre}: {Operaciones} op. · acierto {Acierto:0.0}% · PF {(Perdedoras == 0 ? "∞" : FactorBeneficio.ToString("0.00"))} · "
		+ $"expectativa {ExpectativaUsd:0.00} USD ({ExpectativaR:0.00} R) · total {ResultadoTotal:0.00} USD · DD máx. {DrawdownMaximo:0.00} USD";
}

internal sealed class GestorSimulacion
{
	private readonly ConfiguracionCompleto _cfg;

	public GestorSimulacion(ConfiguracionCompleto cfg) => _cfg = cfg;

	public List<Simulacion> Todas { get; } = new();

	public void Abrir(Senal senal)
	{
		var sim = new Simulacion { Senal = senal, StopActual = senal.Plan.Sl };
		senal.Resultado = sim;
		Todas.Add(sim);
	}

	/// <summary>Con cada vela de 1M cerrada posterior a la señal.</summary>
	public void Procesar1M(Vela vela, decimal? ema1M)
	{
		foreach (var sim in Todas.Where(s => s.Abierta && vela.InicioUtc >= s.Senal.ConfirmadaUtc))
		{
			var d = sim.Direccion;
			var signo = d.Signo();
			var plan = sim.Plan;
			var tick = _cfg.TamanoTick;

			// El stop de protección propuesto con la vela anterior rige desde esta vela.
			if (sim.StopPropuesto is { } nuevo)
			{
				sim.StopActual = nuevo;
				sim.StopPorPoc = true;
				sim.StopPropuesto = null;
			}

			// Excursiones máxima favorable y adversa, en ticks.
			sim.MfeTicks = Math.Max(sim.MfeTicks, signo * ((d == Direccion.Larga ? vela.Maximo : vela.Minimo) - plan.Entrada) / tick);
			sim.MaeTicks = Math.Max(sim.MaeTicks, signo * (plan.Entrada - (d == Direccion.Larga ? vela.Minimo : vela.Maximo)) / tick);

			var tocaSl = d == Direccion.Larga ? vela.Minimo <= sim.StopActual : vela.Maximo >= sim.StopActual;
			var tocaTp = d == Direccion.Larga ? vela.Maximo >= plan.Tp : vela.Minimo <= plan.Tp;

			if (tocaSl && tocaTp)
			{
				sim.Ambigua = true;
				Cerrar(sim, vela, sim.StopActual, "AMBIGUA: LA VELA TOCA SL Y TP (se cuenta como SL)");
				continue;
			}

			if (tocaSl)
			{
				Cerrar(sim, vela, sim.StopActual, sim.StopPorPoc ? "STOP DE PROTECCIÓN POC" : "SL INICIAL");
				continue;
			}

			if (tocaTp)
			{
				Cerrar(sim, vela, plan.Tp, $"TP EN LVN {plan.Objetivo?.Id}");
				continue;
			}

			// Cierre cruzado por la EMA en contra.
			var cruceEma = ema1M is { } ema && signo * (vela.Cierre - ema) < 0;

			if (_cfg.MarcoEma == MarcoEma.M1 && cruceEma)
			{
				if (_cfg.SalidaPorEma)
				{
					Cerrar(sim, vela, vela.Cierre, $"CIERRE CRUZADO POR LA EMA {_cfg.PeriodoEma} (1M)");
					continue;
				}

				sim.Advertencia = "ADVERTENCIA: CRUCE ADVERSO DE EMA";
			}

			if (RevisarAgotamiento(sim, vela, cruceEma))
				continue;

			// Protección POC: se propone con la candidata y se evalúa desde la vela siguiente.
			if (_cfg.ProteccionPoc && sim.Candidata == vela && vela.Huella is { } h)
			{
				var propuesto = h.Poc - signo * _cfg.Ticks(_cfg.BufferPocTicks);

				if (signo * (propuesto - sim.StopActual) > 0 && signo * (vela.Cierre - propuesto) > 0)
					sim.StopPropuesto = propuesto;
			}
		}
	}

	/// <summary>Con la EMA de 5M la salida se confirma solo con el cierre de las velas de 5M.</summary>
	public void Procesar5M(Vela vela5M, decimal? ema5M)
	{
		if (_cfg.MarcoEma != MarcoEma.M5 || ema5M is not { } ema)
			return;

		foreach (var sim in Todas.Where(s => s.Abierta && vela5M.FinUtc > s.Senal.ConfirmadaUtc))
		{
			if (sim.Direccion.Signo() * (vela5M.Cierre - ema) >= 0)
				continue;

			if (_cfg.SalidaPorEma)
				Cerrar(sim, vela5M, vela5M.Cierre, $"CIERRE CRUZADO POR LA EMA {_cfg.PeriodoEma} (5M)");
			else
				sim.Advertencia = "ADVERTENCIA: CRUCE ADVERSO DE EMA (5M)";
		}
	}

	/// <summary>Candidata de agotamiento y arranque contrario. Devuelve true si la operación se cerró.</summary>
	private bool RevisarAgotamiento(Simulacion sim, Vela vela, bool cruceEma)
	{
		var d = sim.Direccion;
		var signo = d.Signo();

		if (sim.Candidata is { } candidata && candidata != vela)
		{
			sim.EdadCandidata++;

			var contraria = d == Direccion.Larga ? vela.Bajista : vela.Alcista;
			var cierreMasAlla = d == Direccion.Larga
				? vela.Cierre < candidata.Minimo - _cfg.Ticks(_cfg.MargenSalidaTicks)
				: vela.Cierre > candidata.Maximo + _cfg.Ticks(_cfg.MargenSalidaTicks);
			var huella = vela.Huella is { } h && (d == Direccion.Larga
				? h.PosPoc >= 1 - _cfg.PosicionGatillo && h.PosVa >= 1 - _cfg.PosicionGatillo
				: h.PosPoc <= _cfg.PosicionGatillo && h.PosVa <= _cfg.PosicionGatillo);
			var delta = vela.Volumen > 0 && -signo * vela.Delta >= vela.Volumen * _cfg.IniciativaDeltaPct / 100m;
			var ema = !_cfg.ExigirEmaParaSalir || cruceEma;

			if (contraria && cierreMasAlla && huella && delta && ema)
			{
				Cerrar(sim, vela, vela.Cierre, "SALIDA CONFIRMADA: AGOTAMIENTO + ARRANQUE CONTRARIO");
				return true;
			}

			if (sim.EdadCandidata >= _cfg.VigenciaCandidataVelas)
				sim.Candidata = null;
		}

		if (sim.Candidata is null && vela.Huella is { } huellaVela && EsCandidata(vela, huellaVela, d))
		{
			sim.Candidata = vela;
			sim.EdadCandidata = 0;
			sim.Advertencia = "POSIBLE AGOTAMIENTO — FALTA ARRANQUE CONTRARIO";
		}

		return false;
	}

	private bool EsCandidata(Vela vela, Huella h, Direccion d)
	{
		var limite = _cfg.AgotamientoPosicion;
		var posicion = d == Direccion.Larga ? h.PosPoc >= limite && h.PosVa >= limite : h.PosPoc <= 1 - limite && h.PosVa <= 1 - limite;
		var extremo = d == Direccion.Larga ? h.EnMaximo : h.EnMinimo;

		// Subasta terminada en el extremo según la regla elegida.
		var terminada = _cfg.ReglaSubasta == ReglaSubasta.SinAgresionEnExtremo
			? (d == Direccion.Larga ? extremo.Ask : extremo.Bid) == 0
			: extremo.Volumen <= _cfg.VolumenMinimoExtremo;

		return posicion && terminada;
	}

	private void Cerrar(Simulacion sim, Vela vela, decimal precio, string motivo)
	{
		var plan = sim.Plan;
		sim.Abierta = false;
		sim.SalidaUtc = vela.FinUtc;
		sim.PrecioSalida = precio;
		sim.MotivoSalida = motivo;
		sim.ResultadoTicks = sim.Direccion.Signo() * (precio - plan.Entrada) / _cfg.TamanoTick;
		sim.ResultadoUsd = plan.Cantidad * (sim.ResultadoTicks * plan.ValorTick - plan.CostesPorContrato);
		sim.R = plan.RiesgoTotal > 0 ? sim.ResultadoUsd / plan.RiesgoTotal : 0;
	}

	public Estadistica Calcular(string nombre, Func<Simulacion, bool>? filtro = null)
	{
		var todas = Todas.Where(s => filtro?.Invoke(s) ?? true).ToList();
		var cerradas = todas.Where(s => !s.Abierta).OrderBy(s => s.SalidaUtc).ToList();
		var e = new Estadistica { Nombre = nombre, Operaciones = cerradas.Count, Abiertas = todas.Count - cerradas.Count };

		if (cerradas.Count == 0)
			return e;

		e.Ganadoras = cerradas.Count(s => s.ResultadoUsd > 0);
		e.Perdedoras = cerradas.Count(s => s.ResultadoUsd <= 0);
		e.Ambiguas = cerradas.Count(s => s.Ambigua);
		e.Acierto = 100m * e.Ganadoras / cerradas.Count;

		var ganado = cerradas.Where(s => s.ResultadoUsd > 0).Sum(s => s.ResultadoUsd);
		var perdido = -cerradas.Where(s => s.ResultadoUsd <= 0).Sum(s => s.ResultadoUsd);
		e.FactorBeneficio = perdido > 0 ? ganado / perdido : 0;
		e.ResultadoTotal = cerradas.Sum(s => s.ResultadoUsd);
		e.ExpectativaUsd = e.ResultadoTotal / cerradas.Count;
		e.ExpectativaR = cerradas.Average(s => s.R);
		e.MfeMedio = cerradas.Average(s => s.MfeTicks);
		e.MaeMedio = cerradas.Average(s => s.MaeTicks);

		// Drawdown máximo de la curva de resultados acumulados.
		decimal acumulado = 0, pico = 0;

		foreach (var s in cerradas)
		{
			acumulado += s.ResultadoUsd;
			pico = Math.Max(pico, acumulado);
			e.DrawdownMaximo = Math.Max(e.DrawdownMaximo, pico - acumulado);
		}

		return e;
	}

	public List<Estadistica> Desglose() => new()
	{
		Calcular("TOTAL"),
		Calcular("M1", s => s.Senal.Modelo == "M1"),
		Calcular("M2", s => s.Senal.Modelo == "M2"),
		Calcular("EXTERNA", s => s.Senal.Tipo == TipoLiquidez.Externa),
		Calcular("INTERNA", s => s.Senal.Tipo == TipoLiquidez.Interna)
	};

	/// <summary>Diario de operaciones simuladas en CSV (separador ';', decimales con punto).</summary>
	public string Csv(Func<DateTime, string> hora)
	{
		var ci = CultureInfo.InvariantCulture;
		var sb = new StringBuilder();
		sb.AppendLine("senal;modelo;variante;direccion;liquidez;evento;referencia;confirmada;entrada;sl;tp;cantidad;riesgo_usd;rr_neto;"
			+ "salida;precio_salida;motivo_salida;ambigua;resultado_ticks;resultado_usd;r;mfe_ticks;mae_ticks;minutos");

		foreach (var s in Todas)
		{
			var p = s.Plan;
			var minutos = s.SalidaUtc is { } salida ? (salida - s.Senal.ConfirmadaUtc).TotalMinutes.ToString("0", ci) : "";

			sb.AppendLine(string.Join(";",
				s.Senal.Id, s.Senal.Modelo, s.Senal.Variante, s.Direccion.Texto(), s.Senal.Tipo.Texto(), s.Senal.EventoId,
				Csv(s.Senal.Referencia), hora(s.Senal.ConfirmadaUtc), p.Entrada.ToString(ci), p.Sl.ToString(ci), p.Tp.ToString(ci), p.Cantidad,
				p.RiesgoTotal.ToString("0.00", ci), p.RrNeto.ToString("0.00", ci),
				s.SalidaUtc is { } t ? hora(t) : "ABIERTA", s.PrecioSalida?.ToString(ci) ?? "", Csv(s.MotivoSalida), s.Ambigua ? "si" : "no",
				s.ResultadoTicks.ToString("0.##", ci), s.ResultadoUsd.ToString("0.00", ci), s.R.ToString("0.00", ci),
				s.MfeTicks.ToString("0.##", ci), s.MaeTicks.ToString("0.##", ci), minutos));
		}

		return sb.ToString();
	}

	private static string Csv(string texto) => "\"" + texto.Replace("\"", "\"\"") + "\"";
}
