namespace ATAS.Indicators.Custom;

using System;
using System.Globalization;

using Color = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

/// <summary>Conversiones y formatos de texto comunes a todos los paneles.</summary>
internal static class Formato
{
	/// <summary>Color de los ajustes (WPF, el tipo del panel de configuración de ATAS) a color de dibujo.</summary>
	public static Color ADibujo(MediaColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

	/// <summary>Hora del gráfico; con la fecha si es de otro día que la referencia.</summary>
	public static string Hora(DateTime hora, DateTime referencia) =>
		hora.Date == referencia.Date ? hora.ToString("HH:mm") : hora.ToString("dd/MM HH:mm");

	/// <summary>Número corto: 1.2K, 3.4M; con signo + si es positivo.</summary>
	public static string Compacto(decimal valor, bool conSigno = true)
	{
		var signo = conSigno && valor > 0 ? "+" : "";

		if (Math.Abs(valor) >= 1000000)
			return $"{signo}{valor / 1000000m:F1}M";

		if (Math.Abs(valor) >= 1000)
			return $"{signo}{valor / 1000m:F1}K";

		return $"{signo}{valor:F0}";
	}

	/// <summary>Precio con los decimales del tick del instrumento (0.25 -> 2 decimales).</summary>
	public static string Precio(decimal precio, decimal tick)
	{
		// Dividir por 1.000… quita los ceros sobrantes de la escala del decimal.
		var texto = (tick / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);
		var punto = texto.IndexOf('.');
		var decimales = punto < 0 ? 0 : texto.Length - punto - 1;

		return precio.ToString("F" + decimales);
	}
}
