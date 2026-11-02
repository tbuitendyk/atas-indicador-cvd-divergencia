using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using ATAS.Indicators.Custom;

/// <summary>
/// PARAMETROS.md se genera de los indicadores reales (atributos Display y Range y el valor por defecto de una instancia
/// nueva): así la tabla de la documentación no se desfasa del código. La prueba falla si el archivo no coincide;
/// para regenerarlo: ACTUALIZAR_DOCS=1 bash SuiteOrderFlowStacker/pruebas/verificar.sh
/// </summary>
internal static class Documentacion
{
	private static readonly Type[] Indicadores =
		{ typeof(StackedImbalancePro), typeof(IcebergTrackerPro), typeof(StopsTrackerPro), typeof(SweepsTrackerPro), typeof(AbsorptionMaster) };

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("Documentación: PARAMETROS.md generado de los indicadores");
		var ruta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PARAMETROS.md"));

		// Desde 'dotnet run' el directorio base es pruebas/bin/Release/net10.0/: se sube hasta SuiteOrderFlowStacker.
		if (!File.Exists(Path.Combine(Path.GetDirectoryName(ruta)!, "SuiteOrderFlowStacker.csproj")))
			ruta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "PARAMETROS.md"));

		var texto = Generar();

		if (Environment.GetEnvironmentVariable("ACTUALIZAR_DOCS") == "1")
			File.WriteAllText(ruta, texto, new UTF8Encoding(false));

		var actual = File.Exists(ruta) ? File.ReadAllText(ruta).Replace("\r\n", "\n") : "";
		c.Check("PARAMETROS.md al día (si falla: ACTUALIZAR_DOCS=1)", (actual == texto).ToString(), "True");
	}

	private static string Valor(object? v) => v switch
	{
		null => "",
		bool b => b ? "Sí" : "No",
		decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
		Enum e => e.GetType().GetField(e.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? e.ToString(),
		string s => s.Length == 0 ? "(vacío)" : s,
		_ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
	};

	private static string Tipo(Type t) => t == typeof(int) ? "entero" : t == typeof(decimal) ? "decimal" : t == typeof(bool) ? "sí/no" : t == typeof(string) ? "texto" : "lista";

	private static string Celda(string s) => s.Replace("|", "\\|").Replace("\n", " ");

	public static string Generar()
	{
		var sb = new StringBuilder();
		sb.Append("# Parámetros de la Suite Order Flow Stacker\n\n");
		sb.Append("Tabla generada automáticamente de los indicadores (atributos y valores por defecto del código): no se edita a mano.\n");
		sb.Append("Regenerar: `ACTUALIZAR_DOCS=1 bash SuiteOrderFlowStacker/pruebas/verificar.sh`. El Thermal Volume Profile + LVN tiene sus\n");
		sb.Append("parámetros en [../ThermalVolumeProfile/LEEME.md](../ThermalVolumeProfile/LEEME.md).\n\n");
		sb.Append("El nombre en inglés es el de la especificación (así se llama la propiedad); en ATAS se ve el nombre en español.\n");
		sb.Append("Las recomendaciones para NQ y MNQ están en la descripción; los umbrales de icebergs son valores iniciales: **calibrar**\n");
		sb.Append("con 1-2 semanas de CSV ([OPERATIVA.md](OPERATIVA.md#8-calibración-con-los-csv)).\n\n");

		sb.Append("## Qué grupos tiene cada indicador\n\n| Indicador | Grupos |\n|---|---|\n");

		foreach (var t in Indicadores)
		{
			var grupos = Propiedades(t).Select(p => p.Display.GroupName).Distinct();
			var nombre = t.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? t.Name;
			sb.Append($"| **{nombre}** (`{t.Name}`) | {string.Join(" · ", grupos)} |\n");
		}

		// Cada bloque una sola vez, con el orden del AbsorptionMaster (que los tiene todos).
		var instancia = Activator.CreateInstance(typeof(AbsorptionMaster))!;
		string? grupoActual = null;

		foreach (var (p, d) in Propiedades(typeof(AbsorptionMaster)))
		{
			var bloque = d.GroupName!.Substring(d.GroupName.IndexOf(' ') + 1);

			if (bloque != grupoActual)
			{
				grupoActual = bloque;
				sb.Append($"\n## {bloque}\n\n| Parámetro | En ATAS | Tipo | Defecto | Rango | Descripción y recomendación |\n|---|---|---|---|---|---|\n");
			}

			var rango = p.GetCustomAttribute<RangeAttribute>() is { } r ? $"{r.Minimum}-{r.Maximum}" : "";
			var defecto = p.Name is "InfoPanelPosition" or "InfoPanelOffsetY" ? "según el indicador (abajo)" : Valor(p.GetValue(instancia));
			sb.Append($"| `{p.Name}` | {Celda(d.Name ?? "")} | {Tipo(p.PropertyType)} | {Celda(defecto)} | {rango} | {Celda(d.Description ?? "")} |\n");
		}

		// Valores por defecto distintos en cada indicador (posición del panel).
		sb.Append("\n## Posición del panel por defecto\n\n| Indicador | Esquina | Desplazamiento vertical |\n|---|---|---|\n");

		foreach (var t in Indicadores)
		{
			var i = Activator.CreateInstance(t)!;
			sb.Append($"| `{t.Name}` | {Valor(t.GetProperty("InfoPanelPosition")!.GetValue(i))} | {t.GetProperty("InfoPanelOffsetY")!.GetValue(i)} px |\n");
		}

		return sb.ToString();
	}

	private static IEnumerable<(PropertyInfo Propiedad, DisplayAttribute Display)> Propiedades(Type t) =>
		t.GetProperties()
			.Select(p => (p, d: p.GetCustomAttribute<DisplayAttribute>()))
			.Where(x => x.d?.GroupName is not null)
			.Select(x => (x.p, x.d!))
			.OrderBy(x => int.Parse(x.Item2.GroupName!.Split('.')[0], CultureInfo.InvariantCulture))
			.ThenBy(x => x.Item2.Order);
}
