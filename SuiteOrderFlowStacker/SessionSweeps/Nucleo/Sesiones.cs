namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Una sesión concreta (por ejemplo, Londres del 30-10): su rango mientras está activa y al cerrar.</summary>
public sealed class InstanciaSesion
{
	public string Id = "";
	public string Nombre = "";

	/// <summary>Fecha local de inicio de la sesión (en su zona).</summary>
	public DateTime Fecha;

	public int DesdeBar, HastaBar;
	public DateTime DesdeUtc, HastaUtc;
	public decimal Alto, Bajo;
	public bool Abierta = true;

	/// <summary>Barra en que dejan de extenderse sus líneas punteadas (al reiniciarse la misma sesión). Null = siguen.</summary>
	public int? FinExtension;

	/// <summary>Barra en que se barrió (o rompió) el máximo / el mínimo; ahí termina su línea si ExtendUntilSwept.</summary>
	public int? AltoBarridoEn, BajoBarridoEn;

	public decimal Medio => (Alto + Bajo) / 2;
}

/// <summary>
/// Motor de sesiones (traducción de f_gSesI / f_pSWP del Pine con horarios HH:mm y zonas con su horario de verano):
/// decide qué sesión está activa en cada vela, sigue su máximo y su mínimo y avisa cuando una sesión termina.
/// </summary>
public sealed class MotorSesiones
{
	private readonly ConfiguracionSweeps _cfg;
	private readonly Dictionary<string, InstanciaSesion> _abiertas = new();

	public MotorSesiones(ConfiguracionSweeps cfg) => _cfg = cfg;

	public List<InstanciaSesion> Instancias { get; } = new();

	public IEnumerable<InstanciaSesion> Abiertas => _abiertas.Values;

	/// <summary>¿Está 'utc' dentro de la sesión? Devuelve también la fecha local de inicio de esa sesión.</summary>
	public (bool Dentro, DateTime Fecha) Dentro(DefinicionSesion s, DateTime utc)
	{
		var (inicio, fin, zona, activa) = _cfg.Horario(s);

		if (!activa || inicio == fin)
			return (false, default);

		var local = Zonas.ALocal(utc, zona).AddHours(_cfg.AjusteManualHoras);
		var hora = local.TimeOfDay;
		var cruza = inicio > fin;
		var dentro = cruza ? hora >= inicio || hora < fin : hora >= inicio && hora < fin;
		var fecha = cruza && hora < fin ? local.Date.AddDays(-1) : local.Date;
		return (dentro, fecha);
	}

	/// <summary>Procesa una vela cerrada: sesiones que terminaron con esta vela (que ya está fuera) y las que empezaron.</summary>
	public (List<InstanciaSesion> Terminadas, List<InstanciaSesion> Iniciadas) Procesar(Vela v)
	{
		var terminadas = new List<InstanciaSesion>();
		var iniciadas = new List<InstanciaSesion>();

		foreach (var s in _cfg.Sesiones)
		{
			var (dentro, fecha) = Dentro(s, v.InicioUtc);
			_abiertas.TryGetValue(s.Id, out var abierta);

			// La misma sesión otra vez (otro día) o fuera de horario: la abierta termina.
			if (abierta is not null && (!dentro || abierta.Fecha != fecha))
			{
				abierta.Abierta = false;
				_abiertas.Remove(s.Id);
				terminadas.Add(abierta);
				abierta = null;
			}

			if (!dentro)
				continue;

			if (abierta is null)
			{
				// Al empezar otra vez, las líneas de la sesión anterior del mismo tipo dejan de extenderse.
				foreach (var previa in Instancias.Where(i => i.Id == s.Id && !i.Abierta && i.FinExtension is null))
					previa.FinExtension = v.Indice - 1;

				abierta = new InstanciaSesion
				{
					Id = s.Id,
					Nombre = s.Nombre,
					Fecha = fecha,
					DesdeBar = v.Indice,
					DesdeUtc = v.InicioUtc,
					Alto = v.High,
					Bajo = v.Low
				};

				_abiertas[s.Id] = abierta;
				Instancias.Add(abierta);
				iniciadas.Add(abierta);

				if (Instancias.Count > 600)
					Instancias.RemoveAt(0);
			}

			abierta.Alto = Math.Max(abierta.Alto, v.High);
			abierta.Bajo = Math.Min(abierta.Bajo, v.Low);
			abierta.HastaBar = v.Indice;
			abierta.HastaUtc = v.FinUtc;
		}

		return (terminadas, iniciadas);
	}

	/// <summary>
	/// Fila de la tabla de sesiones a la hora 'ahoraUtc': (abierta, "dd/MM/yyyy HH:mm:ss ET", estado). Estado:
	/// "Cierra en HH:MM:SS", "Abre en HH:MM:SS" o "Fin de semana" (sábado y domingo en Nueva York).
	/// </summary>
	public (bool Abierta, string FechaHora, string Estado) Tabla(DefinicionSesion s, DateTime ahoraUtc)
	{
		var et = Zonas.AEt(ahoraUtc);
		var fechaHora = et.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " ET";

		if (et.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
			return (false, fechaHora, "Fin de semana");

		var (inicio, fin, zona, activa) = _cfg.Horario(s);

		if (!activa || inicio == fin)
			return (false, fechaHora, "Inactiva");

		var (dentro, fecha) = Dentro(s, ahoraUtc);
		var ajuste = TimeSpan.FromHours(_cfg.AjusteManualHoras);

		if (dentro)
		{
			var finLocal = fecha.Add(fin) + (inicio > fin ? TimeSpan.FromDays(1) : TimeSpan.Zero) - ajuste;
			return (true, fechaHora, "Cierra en " + Duracion(Zonas.AUtc(finLocal, zona) - ahoraUtc));
		}

		// Próximo inicio: hoy o mañana (hora local de la sesión).
		var local = Zonas.ALocal(ahoraUtc, zona).AddHours(_cfg.AjusteManualHoras);
		var proximo = local.Date.Add(inicio);

		if (proximo <= local)
			proximo = proximo.AddDays(1);

		return (false, fechaHora, "Abre en " + Duracion(Zonas.AUtc(proximo - ajuste, zona) - ahoraUtc));
	}

	private static string Duracion(TimeSpan t)
	{
		if (t < TimeSpan.Zero)
			t = TimeSpan.Zero;

		return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
	}
}
