namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Linq;

using Completo.Nucleo;

/// <summary>
/// Comparte el motor entre los gráficos de un mismo contrato y grupo: clave = grupo + instrumento de ATAS.
/// Es memoria del propio proceso de ATAS: no sincroniza entre ordenadores ni entre procesos de ATAS distintos.
/// Solo un maestro por clave.
/// </summary>
internal static class RegistroMotores
{
	private sealed class Entrada
	{
		public object? Maestro;
		public MotorCompleto? Motor;
		public readonly Dictionary<object, string> Visores = new();
	}

	private static readonly object Sync = new();
	private static readonly Dictionary<string, Entrada> Entradas = new();

	public static string Clave(string grupo, string instrumento) =>
		$"{(string.IsNullOrWhiteSpace(grupo) ? "A" : grupo.Trim().ToUpperInvariant())}|{instrumento}";

	/// <summary>Registra el maestro de la clave. Devuelve false si ya hay otro maestro distinto.</summary>
	public static bool RegistrarMaestro(string clave, object maestro, MotorCompleto motor)
	{
		lock (Sync)
		{
			var e = Obtener(clave);

			if (e.Maestro is not null && !ReferenceEquals(e.Maestro, maestro))
				return false;

			e.Maestro = maestro;
			e.Motor = motor;
			return true;
		}
	}

	public static bool HayOtroMaestro(string clave, object yo)
	{
		lock (Sync)
			return Entradas.TryGetValue(clave, out var e) && e.Maestro is not null && !ReferenceEquals(e.Maestro, yo);
	}

	public static void Quitar(object indicador)
	{
		lock (Sync)
		{
			foreach (var e in Entradas.Values)
			{
				if (ReferenceEquals(e.Maestro, indicador))
				{
					e.Maestro = null;
					e.Motor = null;
				}

				e.Visores.Remove(indicador);
			}
		}
	}

	public static MotorCompleto? Motor(string clave)
	{
		lock (Sync)
			return Entradas.TryGetValue(clave, out var e) ? e.Motor : null;
	}

	/// <summary>Indicador maestro de la clave (sus ajustes de alertas son los que valen), o null.</summary>
	public static object? Maestro(string clave)
	{
		lock (Sync)
			return Entradas.TryGetValue(clave, out var e) ? e.Maestro : null;
	}

	public static void Visor(string clave, object visor, string descripcion)
	{
		lock (Sync)
		{
			foreach (var otra in Entradas.Values)
				otra.Visores.Remove(visor);

			Obtener(clave).Visores[visor] = descripcion;
		}
	}

	/// <summary>"Maestro 1M · visores: 5M, CVD 1M" para el reporte.</summary>
	public static string Graficos(string clave)
	{
		lock (Sync)
		{
			if (!Entradas.TryGetValue(clave, out var e))
				return "sin gráficos";

			var visores = e.Visores.Values.OrderBy(v => v).ToList();
			return $"{(e.Maestro is null ? "SIN MAESTRO" : "Maestro 1M")} · visores: {(visores.Count == 0 ? "ninguno" : string.Join(", ", visores))}";
		}
	}

	private static Entrada Obtener(string clave)
	{
		if (!Entradas.TryGetValue(clave, out var e))
		{
			e = new Entrada();
			Entradas[clave] = e;
		}

		return e;
	}
}
