namespace SessionSweeps.Nucleo;

using System;
using System.Globalization;

/// <summary>
/// Horas de Session Sweeps Pro v2.0 (1.1): la lógica en Nueva York (America/New_York) y el panel en Ciudad de México
/// (America/Mexico_City), siempre con TimeZoneInfo y sus reglas de horario (nunca restando horas fijas).
/// </summary>
public static class TiempoV2
{
	public static DateTime AEt(DateTime utc) => Zonas.AEt(utc);

	public static DateTime ACdmx(DateTime utc) => Zonas.ALocal(utc, ZonaHoraria.CiudadDeMexico);

	public static string HoraCdmx(DateTime utc, string formato = "HH:mm") => ACdmx(utc).ToString(formato, CultureInfo.InvariantCulture) + " CDMX";

	public static string HoraEt(DateTime utc, string formato = "HH:mm:ss") => AEt(utc).ToString(formato, CultureInfo.InvariantCulture) + " ET";

	/// <summary>RTH: 09:30-16:00 Nueva York.</summary>
	public static bool EsRth(DateTime utc)
	{
		var h = AEt(utc).TimeOfDay;
		return h >= new TimeSpan(9, 30, 0) && h < new TimeSpan(16, 0, 0);
	}

	public static bool EnKillzone(DateTime utc, ParametrosV2 p)
	{
		var h = AEt(utc).TimeOfDay;
		return p.KillzoneStartNY <= p.KillzoneEndNY ? h >= p.KillzoneStartNY && h < p.KillzoneEndNY : h >= p.KillzoneStartNY || h < p.KillzoneEndNY;
	}

	/// <summary>NOTICIA: a NewsBlockMin minutos o menos de una de las horas de NewsTimes (Nueva York).</summary>
	public static bool EnNoticia(DateTime utc, ParametrosV2 p)
	{
		var h = AEt(utc).TimeOfDay;

		foreach (var texto in (p.NewsTimes ?? "").Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (TimeSpan.TryParseExact(texto.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var noticia) &&
				(h - noticia).Duration() <= TimeSpan.FromMinutes(Math.Max(0, p.NewsBlockMin)))
			{
				return true;
			}
		}

		return false;
	}
}
