namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;

internal enum TipoSesion
{
	Tokio,
	Londres,
	NuevaYork
}

/// <summary>Zonas horarias usadas, con cambio de horario incluido (las velas de ATAS están en UTC).</summary>
internal static class Zonas
{
	public static readonly TimeZoneInfo NuevaYork = Buscar("America/New_York", "Eastern Standard Time");
	public static readonly TimeZoneInfo Londres = Buscar("Europe/London", "GMT Standard Time");
	public static readonly TimeZoneInfo Tokio = Buscar("Asia/Tokyo", "Tokyo Standard Time");
	public static readonly TimeZoneInfo CiudadDeMexico = Buscar("America/Mexico_City", "Central Standard Time (Mexico)");

	// Windows reconoce los nombres IANA desde .NET 6 (con ICU); si no, se usa el nombre de Windows.
	private static TimeZoneInfo Buscar(string iana, string windows)
	{
		foreach (var id in new[] { iana, windows })
		{
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById(id);
			}
			catch (TimeZoneNotFoundException)
			{
			}
			catch (InvalidTimeZoneException)
			{
			}
		}

		return TimeZoneInfo.Utc;
	}

	public static DateTime ALocal(DateTime utc, TimeZoneInfo zona) =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona);

	public static DateTime AUtc(DateTime local, TimeZoneInfo zona) =>
		DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zona), DateTimeKind.Unspecified);

	public static TimeZoneInfo De(TipoSesion tipo) => tipo switch
	{
		TipoSesion.Tokio => Tokio,
		TipoSesion.Londres => Londres,
		_ => NuevaYork
	};
}

internal readonly record struct VentanaSesion(TipoSesion Tipo, DateTime FechaLocal, DateTime InicioUtc, DateTime FinUtc);

/// <summary>
/// Ventanas de sesión elegidas (no todas las definiciones posibles): Tokio 09:00–15:00 de Tokio, Londres
/// 08:00–12:00 de Londres y Nueva York 09:30–16:00 de Nueva York, salvo cierre anticipado configurado.
/// </summary>
internal sealed class Calendario
{
	private readonly Dictionary<DateTime, TimeSpan> _cierresNy = new();

	public Calendario(string cierresAnticipadosNy)
	{
		foreach (var parte in (cierresAnticipadosNy ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (DateTime.TryParseExact(parte, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
				_cierresNy[fecha.Date] = fecha.TimeOfDay;
		}
	}

	public static string Nombre(TipoSesion tipo) => tipo switch
	{
		TipoSesion.Tokio => "Tokio",
		TipoSesion.Londres => "Londres",
		_ => "Nueva York"
	};

	public VentanaSesion VentanaDelDia(TipoSesion tipo, DateTime fechaLocal)
	{
		var (inicio, fin) = tipo switch
		{
			TipoSesion.Tokio => (new TimeSpan(9, 0, 0), new TimeSpan(15, 0, 0)),
			TipoSesion.Londres => (new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0)),
			_ => (new TimeSpan(9, 30, 0), _cierresNy.TryGetValue(fechaLocal.Date, out var cierre) ? cierre : new TimeSpan(16, 0, 0))
		};

		var zona = Zonas.De(tipo);
		return new(tipo, fechaLocal.Date, Zonas.AUtc(fechaLocal.Date + inicio, zona), Zonas.AUtc(fechaLocal.Date + fin, zona));
	}

	/// <summary>La ventana de la sesión que contiene este instante, si es día laborable y está dentro.</summary>
	public VentanaSesion? Ventana(TipoSesion tipo, DateTime utc)
	{
		var fecha = Zonas.ALocal(utc, Zonas.De(tipo)).Date;

		if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
			return null;

		var ventana = VentanaDelDia(tipo, fecha);
		return utc >= ventana.InicioUtc && utc < ventana.FinUtc ? ventana : null;
	}

	/// <summary>Día de negociación de Globex: empieza a las 18:00 de Nueva York del día anterior.</summary>
	public static DateTime DiaGlobex(DateTime utc) => Zonas.ALocal(utc, Zonas.NuevaYork).AddHours(6).Date;

	/// <summary>Lunes del día de negociación: identifica la semana.</summary>
	public static DateTime SemanaGlobex(DateTime utc)
	{
		var dia = DiaGlobex(utc);
		return dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
	}

	/// <summary>Instante UTC del reinicio del CVD (hora de NY) del día de Nueva York que contiene 'utc'.</summary>
	public static DateTime ReinicioCvd(DateTime utc, TimeSpan horaNy) =>
		Zonas.AUtc(Zonas.ALocal(utc, Zonas.NuevaYork).Date + horaNy, Zonas.NuevaYork);

	public static DateTime APresentacion(DateTime utc, ZonaPresentacion zona) => zona switch
	{
		ZonaPresentacion.NuevaYork => Zonas.ALocal(utc, Zonas.NuevaYork),
		ZonaPresentacion.Utc => utc,
		_ => Zonas.ALocal(utc, Zonas.CiudadDeMexico)
	};

	public static string Sufijo(ZonaPresentacion zona) => zona switch
	{
		ZonaPresentacion.NuevaYork => "NY",
		ZonaPresentacion.Utc => "UTC",
		_ => "CDMX"
	};
}
