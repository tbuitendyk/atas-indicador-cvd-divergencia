namespace Completo.Nucleo;

using System;
using System.Collections.Generic;

/// <summary>
/// CVD por vela: apertura = acumulado anterior, cierre = acumulado + delta, extremos = base + extremos intravela del
/// delta. Se reinicia a la hora configurada de Nueva York en días laborables. La base es PARCIAL si la carga empezó
/// después del reinicio o si se perdieron velas durante la sesión.
/// </summary>
internal sealed class CalculadoraCvd
{
	private readonly TimeSpan _horaReinicio;
	private DateTime? _anterior;
	private decimal _acumulado;

	public CalculadoraCvd(TimeSpan horaReinicioNy) => _horaReinicio = horaReinicioNy;

	public int Sesion { get; private set; }

	public bool Parcial { get; private set; } = true;

	public string Estado => Sesion == 0 ? "NO DISPONIBLE" : Parcial ? "BASE PARCIAL" : "BASE COMPLETA";

	public void Aplicar(Vela vela)
	{
		var fechaNy = Zonas.ALocal(vela.InicioUtc, Zonas.NuevaYork).Date;
		var laborable = fechaNy.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
		var reinicio = Zonas.AUtc(fechaNy + _horaReinicio, Zonas.NuevaYork);

		if (_anterior is null)
		{
			Sesion = 1;
			Parcial = !(laborable && vela.InicioUtc == reinicio);
			_acumulado = 0;
		}
		else if (laborable && _anterior < reinicio && vela.InicioUtc >= reinicio)
		{
			Sesion++;
			Parcial = vela.InicioUtc != reinicio;
			_acumulado = 0;
		}
		else if (vela.InicioUtc - _anterior.Value > TimeSpan.FromMinutes(1)
			&& Calendario.DiaGlobex(vela.InicioUtc) == Calendario.DiaGlobex(_anterior.Value))
		{
			// Faltan velas dentro del mismo día de negociación: la base deja de ser íntegra.
			Parcial = true;
		}

		vela.CvdApertura = _acumulado;
		vela.CvdCierre = _acumulado + vela.Delta;
		vela.CvdMaximo = Math.Max(Math.Max(vela.CvdApertura, vela.CvdCierre), _acumulado + vela.DeltaMaximo);
		vela.CvdMinimo = Math.Min(Math.Min(vela.CvdApertura, vela.CvdCierre), _acumulado + vela.DeltaMinimo);
		vela.SesionCvd = Sesion;
		vela.CvdParcial = Parcial;

		_acumulado = vela.CvdCierre;
		_anterior = vela.InicioUtc;
	}
}

/// <summary>Divergencia entre dos pivotes del mismo tipo de una misma sesión CVD.</summary>
internal sealed record Divergencia(Marco Marco, bool Alcista, Pivote P1, Pivote P2)
{
	public DateTime ConfirmadaUtc => P2.ConfirmadoUtc;

	public string Texto => $"{(Alcista ? "alcista" : "bajista")} {Marco.Texto()} confirmada {P2.ConfirmadoUtc:HH:mm} UTC";
}

internal sealed class DetectorDivergencias
{
	private readonly Dictionary<(Marco, bool), Pivote> _ultimo = new();

	public List<Divergencia> Divergencias { get; } = new();

	public int NoEvaluables { get; private set; }

	/// <summary>Máximos con máximos, mínimos con mínimos; solo si los dos tienen CVD íntegro de la misma sesión.</summary>
	public void Agregar(Pivote pivote)
	{
		var clave = (pivote.Marco, pivote.EsMaximo);

		if (_ultimo.TryGetValue(clave, out var anterior))
		{
			if (anterior.SesionCvd != pivote.SesionCvd || anterior.CvdParcial || pivote.CvdParcial)
			{
				NoEvaluables++;
			}
			else if (pivote.EsMaximo && pivote.Precio > anterior.Precio && pivote.Cvd < anterior.Cvd)
			{
				Divergencias.Add(new Divergencia(pivote.Marco, false, anterior, pivote));
			}
			else if (!pivote.EsMaximo && pivote.Precio < anterior.Precio && pivote.Cvd > anterior.Cvd)
			{
				Divergencias.Add(new Divergencia(pivote.Marco, true, anterior, pivote));
			}
		}

		_ultimo[clave] = pivote;

		if (Divergencias.Count > 500)
			Divergencias.RemoveAt(0);
	}
}
