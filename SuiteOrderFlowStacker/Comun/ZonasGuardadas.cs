namespace OrderFlowSuite.Comun;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// v5: los LVN y EDGE vigentes del LVN Thermal Profile (FAST), automáticos y manuales, guardados por instrumento. El
/// Thermal los escribe cada vez que cambian; el Sweep Pro (FAST) los lee al entrar en vivo, así sigue usándolos
/// (objetivos, Modelo 2, gatillos) y dibujándolos aunque el Thermal esté cerrado o apagado. Un archivo pequeño: nada de
/// historial.
/// </summary>
public static class ZonasGuardadas
{
	private static readonly object Cerrojo = new();

	/// <summary>Carpeta (en las pruebas se cambia).</summary>
	public static string Carpeta { get; set; } =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "OrderFlowSuite", "Thermal");

	public sealed class Contenido
	{
		public string Instrumento { get; set; } = "";
		public DateTime GuardadoUtc { get; set; }
		public List<LvnZone> Lvn { get; set; } = new();
		public List<EdgeZone> Edge { get; set; } = new();
	}

	public static string Archivo(string instrumento) =>
		Path.Combine(Carpeta, string.Concat(instrumento.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)) + ".json");

	/// <summary>Escribe las zonas vigentes (sin las BORRADO). Escritura atómica (archivo temporal y reemplazo).</summary>
	public static void Guardar(string instrumento, IEnumerable<LvnZone> lvn, IEnumerable<EdgeZone> edge, DateTime ahoraUtc)
	{
		if (string.IsNullOrWhiteSpace(instrumento))
			return;

		var c = new Contenido
		{
			Instrumento = instrumento, GuardadoUtc = ahoraUtc,
			Lvn = lvn.Where(z => z.Estado != "BORRADO").ToList(), Edge = edge.Where(z => z.Estado != "BORRADO").ToList()
		};

		lock (Cerrojo)
		{
			Directory.CreateDirectory(Carpeta);
			var ruta = Archivo(instrumento);
			var temporal = ruta + ".tmp";
			File.WriteAllText(temporal, JsonSerializer.Serialize(c));
			File.Move(temporal, ruta, overwrite: true);
		}
	}

	/// <summary>Lo guardado para el instrumento (null si no hay archivo o está dañado).</summary>
	public static Contenido? Leer(string instrumento)
	{
		try
		{
			lock (Cerrojo)
			{
				var ruta = Archivo(instrumento);
				return File.Exists(ruta) ? JsonSerializer.Deserialize<Contenido>(File.ReadAllText(ruta)) : null;
			}
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
		{
			return null;
		}
	}
}
