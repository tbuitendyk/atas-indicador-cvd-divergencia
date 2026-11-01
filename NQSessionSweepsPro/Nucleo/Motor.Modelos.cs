namespace NqSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class MotorSweeps
{
	private IEnumerable<decimal> Lvns => PerfilLvn?.Lvns.Select(l => l.Precio) ?? Enumerable.Empty<decimal>();

	/// <summary>Filtro RequireH1Alignment: a favor del contexto H1, o contra él si el barrido es del PDH/PDL.</summary>
	private bool AlineadoH1(ZonaBarrido z)
	{
		if (!Cfg.RequireH1Alignment || z.Nivel.Origen == "Dia anterior")
			return true;

		return z.Lado == Lado.Buyside ? H1.Valor == ContextoH1.Bajista : H1.Valor == ContextoH1.Alcista;
	}

	// =================================================================================================================
	// Modelo A: vela gatillo de rechazo
	// =================================================================================================================

	private void ProcesarModeloA(Vela v)
	{
		if (!Cfg.ModeloA)
			return;

		// La vela de barrido de esta vela: se evalúan sus 5 condiciones.
		foreach (var z in ZonasBarrido.Where(z => z.BarraBarrido == v.Indice))
		{
			var huella = Huella.Calcular(v, Cfg.TamanoTick);
			var arriba = z.Lado == Lado.Buyside;
			var lvn = Modelos.LvnTocado(v, z.Lado, Lvns, Cfg.Ticks(Cfg.LVNTouchTolTicks));
			var a = new EvaluacionA
			{
				Zona = z,
				LvnTocado = lvn,
				Lvn = lvn is not null,
				PinBar = Modelos.PinBar(v, z.Lado),
				Poc = huella is { } h && (arriba ? v.EnTercioSuperior(h.Poc) : v.EnTercioInferior(h.Poc)),
				Va = huella is { } h2 && (arriba ? v.EnTercioSuperior(h2.VaMedio) : v.EnTercioInferior(h2.VaMedio)),
				Cierre = z.Lado.Signo() * (v.Close - z.L) < 0
			};

			EvaluacionesA.Add(a);

			if (EvaluacionesA.Count > 300)
				EvaluacionesA.RemoveAt(0);

			bool Ok(bool usar, bool cumple) => !usar || cumple;
			var base134 = Ok(Cfg.ACondicionLvn, a.Lvn) && Ok(Cfg.ACondicionPoc, a.Poc) && Ok(Cfg.ACondicionVa, a.Va);

			if (base134 && Ok(Cfg.ACondicionPinBar, a.PinBar) && Ok(Cfg.ACondicionCierre, a.Cierre))
			{
				SenalA(a, v);
			}
			else if (base134 && !a.Cierre && Cfg.ACondicionCierre)
			{
				a.Espera = Cfg.TriggerLookahead;
			}
			else
			{
				a.Terminada = true;
			}
		}

		// Barridos que cumplieron 1, 3 y 4 sin cerrar de vuelta: la primera de las siguientes velas que cierre de vuelta.
		foreach (var a in EvaluacionesA.Where(a => !a.Terminada && a.Espera > 0 && a.Zona.BarraBarrido < v.Indice))
		{
			if (a.Zona.Clasificacion == Clasificacion.Falso)
			{
				a.Terminada = true;
				continue;
			}

			if (a.Zona.Lado.Signo() * (v.Close - a.Zona.L) < 0)
			{
				a.Cierre = true;
				SenalA(a, v);
				continue;
			}

			if (--a.Espera == 0)
				a.Terminada = true;
		}
	}

	private void SenalA(EvaluacionA a, Vela gatillo)
	{
		a.Terminada = true;
		var z = a.Zona;

		if (!AlineadoH1(z))
			return;

		var corto = z.Lado == Lado.Buyside;
		var s = new Senal
		{
			Id = _siguienteSenal++,
			Modelo = "A",
			Zona = z,
			Barra = gatillo.Indice,
			Utc = gatillo.FinUtc,
			Entrada = gatillo.Close,
			Stop = corto ? z.ExtremoPrecio + Cfg.Ticks(Cfg.StopBufferTicks) : z.ExtremoPrecio - Cfg.Ticks(Cfg.StopBufferTicks),
			Confluencias =
			{
				("Pin bar", a.PinBar),
				(corto ? "POC tercio superior" : "POC tercio inferior", a.Poc),
				(corto ? "VA tercio superior" : "VA tercio inferior", a.Va),
				(corto ? "Cierre bajo max" : "Cierre sobre min", a.Cierre),
				("LVN", a.Lvn)
			}
		};

		s.Proyeccion = Proyectar(z, s.Entrada);
		s.Proyeccion.Stop = s.Stop;
		AnadirSenal(s);
		IniciarModeloB(z, gatillo);
	}

	// =================================================================================================================
	// Modelo B: continuación tras el retroceso a un LVN
	// =================================================================================================================

	private void IniciarModeloB(ZonaBarrido z, Vela v)
	{
		if (!Cfg.ModeloB || SecuenciasB.Any(b => b.Zona == z))
			return;

		SecuenciasB.Add(new SecuenciaB { Zona = z, Inicio = v.Indice, ExtremoImpulso = z.Lado == Lado.Buyside ? v.Low : v.High, BarraImpulso = v.Indice });

		if (SecuenciasB.Count > 300)
			SecuenciasB.RemoveAt(0);
	}

	private void ProcesarModeloB(Vela v)
	{
		foreach (var b in SecuenciasB.Where(b => b.Viva && b.Inicio < v.Indice))
		{
			var z = b.Zona;
			var corto = z.Lado == Lado.Buyside;
			var s = corto ? 1 : -1;

			// Cancelación: cierre más allá del extremo del barrido o demasiadas velas.
			if (s * (v.Close - z.ExtremoPrecio) > 0 || v.Indice - b.Inicio > Cfg.MaxBarsModelB)
			{
				(b.Etapa, b.Motivo) = (EtapaModeloB.Cancelado, s * (v.Close - z.ExtremoPrecio) > 0 ? "cierre mas alla del barrido" : $"{Cfg.MaxBarsModelB} velas sin entrada");
				continue;
			}

			switch (b.Etapa)
			{
				case EtapaModeloB.EsperandoImpulso:
				{
					b.ExtremoImpulso = corto ? Math.Min(b.ExtremoImpulso, v.Low) : Math.Max(b.ExtremoImpulso, v.High);
					var desplazamiento = s * (z.ExtremoPrecio - b.ExtremoImpulso);
					var fvg = Fvg.Fvgs.Any(f => f.PostSweep && f.Alcista == !corto && f.Izquierda >= z.BarraBarrido);

					if ((_atr21.Valor is { } atr && desplazamiento >= (decimal)Cfg.ImpulseATRMult * atr) || fvg)
					{
						b.Etapa = EtapaModeloB.Impulso;
						b.BarraImpulso = v.Indice;
					}

					break;
				}

				case EtapaModeloB.Impulso:
				{
					var nuevoExtremo = corto ? v.Low < b.ExtremoImpulso : v.High > b.ExtremoImpulso;
					b.ExtremoImpulso = corto ? Math.Min(b.ExtremoImpulso, v.Low) : Math.Max(b.ExtremoImpulso, v.High);

					if (nuevoExtremo)
					{
						b.BarraImpulso = v.Indice;
						break;
					}

					// Retroceso: el precio vuelve y toca un LVN (o un IFVG / FVG en contra) dentro del rango del impulso.
					var bajo = Math.Min(b.ExtremoImpulso, z.ExtremoPrecio);
					var alto = Math.Max(b.ExtremoImpulso, z.ExtremoPrecio);
					var extremo = corto ? v.High : v.Low;
					var lvn = Lvns.Where(p => p > bajo && p < alto && s * (extremo - p) >= 0)
						.OrderBy(p => Math.Abs(p - extremo)).Cast<decimal?>().FirstOrDefault();
					var zona = Fvg.Fvgs.FirstOrDefault(f => f.AlcistaAhora == !corto && f.Abajo < alto && f.Arriba > bajo
						&& (corto ? v.High >= f.Abajo : v.Low <= f.Arriba));

					if (lvn is null && zona is null)
						break;

					b.Etapa = EtapaModeloB.Retroceso;
					b.Nivel = lvn ?? (corto ? zona!.Abajo : zona!.Arriba);
					b.NombreNivel = lvn is null ? (zona!.Ifvg ? "IFVG" : "FVG") : "LVN";
					b.TocoFvg = zona is not null;
					b.TechoRetroceso = extremo;
					b.BarraTecho = v.Indice;
					break;
				}

				case EtapaModeloB.Retroceso:
				{
					var extremo = corto ? v.High : v.Low;

					if (s * (extremo - b.TechoRetroceso) > 0)
						(b.TechoRetroceso, b.BarraTecho) = (extremo, v.Indice);

					b.TocoFvg |= Fvg.Fvgs.Any(f => f.AlcistaAhora == !corto && (corto ? v.High >= f.Abajo && v.Low <= f.Arriba : v.Low <= f.Arriba && v.High >= f.Abajo)
						&& f.Abajo < Math.Max(b.ExtremoImpulso, z.ExtremoPrecio) && f.Arriba > Math.Min(b.ExtremoImpulso, z.ExtremoPrecio));

					// Entrada (obligatoria): una vela cierra rompiendo el nivel (1 tick más allá).
					if (b.Nivel is { } nivel && s * (nivel - v.Close) >= Cfg.TamanoTick)
						EntradaB(b, v, nivel);

					break;
				}
			}
		}
	}

	private void EntradaB(SecuenciaB b, Vela v, decimal nivel)
	{
		var z = b.Zona;
		var corto = z.Lado == Lado.Buyside;
		var techo = Velas[b.BarraTecho];

		// a) Absorción en el techo del retroceso (la sección 8.2 aplicada a esa vela con L = nivel del retroceso).
		var zonaTecho = new ZonaBarrido { Nivel = z.Nivel, Lado = z.Lado, L = nivel, BarraBarrido = techo.Indice, MinutoT0Utc = techo.InicioUtc };
		var absorcion = OrderFlow.Calcular(zonaTecho, 1, new List<Vela> { techo }, techo, Huella.Calcular(techo, Cfg.TamanoTick), VolPorTick(techo.Indice), Cfg, techo.FinUtc).Absorcion;

		// b) Subasta terminada en el extremo de la vela del techo: casi nada de Ask en su High (Bid en su Low si es largo).
		var tickExtremo = (long)Math.Round((corto ? techo.High : techo.Low) / Cfg.TamanoTick);
		var subasta = techo.Niveles is { } niveles && niveles.TryGetValue(tickExtremo, out var n)
			&& (decimal)(corto ? n.Ask : n.Bid) <= Cfg.FinishedAuctionMaxContracts;

		// c) Las 3 últimas velas del retroceso con su POC en el tercio del extremo.
		var ultimas = Velas.Where(x => x.Indice > b.BarraImpulso && x.Indice < v.Indice).TakeLast(3).ToList();
		var tresPoc = ultimas.Count == 3 && ultimas.All(x => Huella.Calcular(x, Cfg.TamanoTick) is { } h && (corto ? x.EnTercioSuperior(h.Poc) : x.EnTercioInferior(h.Poc)));

		// d) Ruptura de la directriz del retroceso (2 últimos pivotes bajos de longitud 2).
		b.Directriz = Modelos.Directriz(Velas, b.BarraImpulso, v.Indice, corto);
		var directriz = b.Directriz is { } d && (corto ? v.Close < Modelos.ValorDirectriz(d, v.Indice) : v.Close > Modelos.ValorDirectriz(d, v.Indice));

		// e) Giro fuerte de delta en contra, tras velas con delta a favor en el retroceso.
		var previas = Velas.Skip(Math.Max(0, v.Indice - 20)).Take(Math.Min(20, v.Indice)).ToList();
		var media = previas.Count == 0 ? 0 : previas.Average(x => Math.Abs(x.Delta));
		var aFavor = Velas.Where(x => x.Indice > b.BarraImpulso && x.Indice < v.Indice).Any(x => corto ? x.Delta > 0 : x.Delta < 0);
		var giro = aFavor && media > 0 && (corto ? v.Delta <= -(decimal)Cfg.DeltaFlipMult * media : v.Delta >= (decimal)Cfg.DeltaFlipMult * media);

		var confluencias = new List<(string, bool)>
		{
			("Absorcion en el techo", absorcion >= 6),
			("Subasta terminada", subasta),
			("3 velas con POC arriba", tresPoc),
			("Ruptura de directriz", directriz),
			("Giro de delta", giro),
			("Toco IFVG/FVG", b.TocoFvg)
		};

		if (confluencias.Count(c => c.Item2) < Cfg.MinConfluencesB || !AlineadoH1(z))
			return;

		var s = new Senal
		{
			Id = _siguienteSenal++,
			Modelo = "B",
			Zona = z,
			Barra = v.Indice,
			Utc = v.FinUtc,
			Entrada = v.Close,
			Stop = corto ? b.TechoRetroceso + Cfg.Ticks(Cfg.StopBufferTicks) : b.TechoRetroceso - Cfg.Ticks(Cfg.StopBufferTicks),
			Confluencias = confluencias
		};

		s.Proyeccion = Proyectar(z, s.Entrada);
		s.Proyeccion.Stop = s.Stop;
		b.Senal = s;
		b.Etapa = EtapaModeloB.Entrada;
		AnadirSenal(s);
	}

	private void AnadirSenal(Senal s)
	{
		Senales.Add(s);

		if (Senales.Count > 500)
			Senales.RemoveAt(0);

		var riesgo = Math.Abs(s.Stop - s.Entrada);
		var objetivos = s.Proyeccion.Objetivos.Select((o, i) => $"T{i + 1} {Precio(o.Precio)} ({o.Puntos:0.##} pts, R:R {(riesgo > 0 ? o.Puntos / riesgo : 0):0.0}){(o.FueraDeRango ? " fuera de rango diario" : "")}");
		var w = s.Zona.W5 ?? s.Zona.W3 ?? s.Zona.W1;
		var cuerpo = string.Join("\n", new[]
		{
			$"Modelo {s.Modelo} - {(s.Corto ? "corto" : "largo")} tras barrer {s.Zona.Nombre} {Precio(s.Zona.L)}",
			$"Entrada: {Precio(s.Entrada)}",
			$"Stop: {Precio(s.Stop)}",
			objetivos.Any() ? string.Join("\n", objetivos) : "Objetivos: sin objetivos",
			$"Confluencias {s.Cumplidas}/{s.Confluencias.Count}: {s.TextoConfluencias}",
			$"Absorcion: {(w is null ? "-" : $"{w.Absorcion}/10")} | Contexto H1: {H1.Valor.Texto()}"
		});

		Avisar(s.Modelo == "A" ? TipoAviso.EntradaA : TipoAviso.EntradaB, s.Utc, $"entrada|{s.Modelo}|{s.Zona.Id}|{s.Barra}",
			$"{s.Titulo} {Precio(s.Entrada)} - {s.Zona.Nombre} {Precio(s.Zona.L)}",
			$"{Simbolo} {s.Titulo.Replace(" - ", " ")} - {Precio(s.Entrada)}", cuerpo);
	}
}
