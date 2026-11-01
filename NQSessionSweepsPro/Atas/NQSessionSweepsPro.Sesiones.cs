namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using NqSweeps.Nucleo;

using OFT.Attributes;
using OFT.Attributes.Editors;

using MediaColor = System.Windows.Media.Color;

// Ajustes de las 5 sesiones (grupo "1. Sesiones"): activa, nombre, modo, horario personalizado (HH:mm) y su zona,
// color, sombreado, extensiones y nombre. Archivo generado con la misma plantilla para las 5 sesiones.
public partial class NQSessionSweepsPro
{
	#region Sesión Asia

	[Display(Name = "Asia: activa", GroupName = G1, Order = 10)]
	public bool AsiaActiva { get => S("Asia").Activa; set { S("Asia").Activa = value; Recalcular(); } }

	[Display(Name = "Asia: nombre", GroupName = G1, Order = 11)]
	public string AsiaNombre { get => S("Asia").Nombre; set { S("Asia").Nombre = string.IsNullOrWhiteSpace(value) ? "Asia" : value; Recalcular(); } }

	[Display(Name = "Asia: modo", GroupName = G1, Order = 12, Description = "18:00-02:00 / Kill Zone 20:00-00:00 (ET)")]
	public ModoSesion AsiaModo { get => S("Asia").Modo; set { S("Asia").Modo = value; Recalcular(); } }

	[Display(Name = "Asia: inicio personalizado (HH:mm)", GroupName = G1, Order = 13)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan AsiaInicio { get => S("Asia").CustomInicio; set { S("Asia").CustomInicio = value; Recalcular(); } }

	[Display(Name = "Asia: fin personalizado (HH:mm)", GroupName = G1, Order = 14)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan AsiaFin { get => S("Asia").CustomFin; set { S("Asia").CustomFin = value; Recalcular(); } }

	[Display(Name = "Asia: zona horaria (modo personalizado)", GroupName = G1, Order = 15)]
	public ZonaHoraria AsiaZona { get => S("Asia").Zona; set { S("Asia").Zona = value; Recalcular(); } }

	[Display(Name = "Asia: color", GroupName = G1, Order = 16)]
	public MediaColor AsiaColor { get; set; } = MediaColor.FromArgb(255, 255, 235, 59);

	[Display(Name = "Asia: sombrear", GroupName = G1, Order = 17)]
	public bool AsiaSombrear { get => S("Asia").Sombrear; set => S("Asia").Sombrear = value; }

	[Display(Name = "Asia: extender máximo / mínimo", GroupName = G1, Order = 18)]
	public bool AsiaExtenderMaxMin { get => S("Asia").ExtenderMaxMin; set => S("Asia").ExtenderMaxMin = value; }

	[Display(Name = "Asia: extender línea media", GroupName = G1, Order = 19)]
	public bool AsiaExtenderMedia { get => S("Asia").ExtenderMedia; set => S("Asia").ExtenderMedia = value; }

	[Display(Name = "Asia: mostrar nombre", GroupName = G1, Order = 20)]
	public bool AsiaMostrarNombre { get => S("Asia").MostrarNombre; set => S("Asia").MostrarNombre = value; }

	#endregion

	#region Sesión Londres

	[Display(Name = "Londres: activa", GroupName = G1, Order = 20)]
	public bool LondresActiva { get => S("Londres").Activa; set { S("Londres").Activa = value; Recalcular(); } }

	[Display(Name = "Londres: nombre", GroupName = G1, Order = 21)]
	public string LondresNombre { get => S("Londres").Nombre; set { S("Londres").Nombre = string.IsNullOrWhiteSpace(value) ? "Londres" : value; Recalcular(); } }

	[Display(Name = "Londres: modo", GroupName = G1, Order = 22, Description = "02:00-08:30 / Kill Zone 02:00-05:00 (ET)")]
	public ModoSesion LondresModo { get => S("Londres").Modo; set { S("Londres").Modo = value; Recalcular(); } }

	[Display(Name = "Londres: inicio personalizado (HH:mm)", GroupName = G1, Order = 23)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan LondresInicio { get => S("Londres").CustomInicio; set { S("Londres").CustomInicio = value; Recalcular(); } }

	[Display(Name = "Londres: fin personalizado (HH:mm)", GroupName = G1, Order = 24)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan LondresFin { get => S("Londres").CustomFin; set { S("Londres").CustomFin = value; Recalcular(); } }

	[Display(Name = "Londres: zona horaria (modo personalizado)", GroupName = G1, Order = 25)]
	public ZonaHoraria LondresZona { get => S("Londres").Zona; set { S("Londres").Zona = value; Recalcular(); } }

	[Display(Name = "Londres: color", GroupName = G1, Order = 26)]
	public MediaColor LondresColor { get; set; } = MediaColor.FromArgb(255, 33, 150, 243);

	[Display(Name = "Londres: sombrear", GroupName = G1, Order = 27)]
	public bool LondresSombrear { get => S("Londres").Sombrear; set => S("Londres").Sombrear = value; }

	[Display(Name = "Londres: extender máximo / mínimo", GroupName = G1, Order = 28)]
	public bool LondresExtenderMaxMin { get => S("Londres").ExtenderMaxMin; set => S("Londres").ExtenderMaxMin = value; }

	[Display(Name = "Londres: extender línea media", GroupName = G1, Order = 29)]
	public bool LondresExtenderMedia { get => S("Londres").ExtenderMedia; set => S("Londres").ExtenderMedia = value; }

	[Display(Name = "Londres: mostrar nombre", GroupName = G1, Order = 30)]
	public bool LondresMostrarNombre { get => S("Londres").MostrarNombre; set => S("Londres").MostrarNombre = value; }

	#endregion

	#region Sesión Premercado

	[Display(Name = "Premercado: activa", GroupName = G1, Order = 30)]
	public bool PremercadoActiva { get => S("Premercado").Activa; set { S("Premercado").Activa = value; Recalcular(); } }

	[Display(Name = "Premercado: nombre", GroupName = G1, Order = 31)]
	public string PremercadoNombre { get => S("Premercado").Nombre; set { S("Premercado").Nombre = string.IsNullOrWhiteSpace(value) ? "Premercado" : value; Recalcular(); } }

	[Display(Name = "Premercado: modo", GroupName = G1, Order = 32, Description = "PremarketMinutesBeforeOpen antes de NYOpen (ET)")]
	public ModoSesion PremercadoModo { get => S("Premercado").Modo; set { S("Premercado").Modo = value; Recalcular(); } }

	[Display(Name = "Premercado: inicio personalizado (HH:mm)", GroupName = G1, Order = 33)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan PremercadoInicio { get => S("Premercado").CustomInicio; set { S("Premercado").CustomInicio = value; Recalcular(); } }

	[Display(Name = "Premercado: fin personalizado (HH:mm)", GroupName = G1, Order = 34)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan PremercadoFin { get => S("Premercado").CustomFin; set { S("Premercado").CustomFin = value; Recalcular(); } }

	[Display(Name = "Premercado: zona horaria (modo personalizado)", GroupName = G1, Order = 35)]
	public ZonaHoraria PremercadoZona { get => S("Premercado").Zona; set { S("Premercado").Zona = value; Recalcular(); } }

	[Display(Name = "Premercado: color", GroupName = G1, Order = 36)]
	public MediaColor PremercadoColor { get; set; } = MediaColor.FromArgb(255, 156, 39, 176);

	[Display(Name = "Premercado: sombrear", GroupName = G1, Order = 37)]
	public bool PremercadoSombrear { get => S("Premercado").Sombrear; set => S("Premercado").Sombrear = value; }

	[Display(Name = "Premercado: extender máximo / mínimo", GroupName = G1, Order = 38)]
	public bool PremercadoExtenderMaxMin { get => S("Premercado").ExtenderMaxMin; set => S("Premercado").ExtenderMaxMin = value; }

	[Display(Name = "Premercado: extender línea media", GroupName = G1, Order = 39)]
	public bool PremercadoExtenderMedia { get => S("Premercado").ExtenderMedia; set => S("Premercado").ExtenderMedia = value; }

	[Display(Name = "Premercado: mostrar nombre", GroupName = G1, Order = 40)]
	public bool PremercadoMostrarNombre { get => S("Premercado").MostrarNombre; set => S("Premercado").MostrarNombre = value; }

	#endregion

	#region Sesión NY AM

	[Display(Name = "NY AM: activa", GroupName = G1, Order = 40)]
	public bool NyAmActiva { get => S("NY AM").Activa; set { S("NY AM").Activa = value; Recalcular(); } }

	[Display(Name = "NY AM: nombre", GroupName = G1, Order = 41)]
	public string NyAmNombre { get => S("NY AM").Nombre; set { S("NY AM").Nombre = string.IsNullOrWhiteSpace(value) ? "NY AM" : value; Recalcular(); } }

	[Display(Name = "NY AM: modo", GroupName = G1, Order = 42, Description = "09:30-12:00 / Kill Zone 09:30-11:00 (ET)")]
	public ModoSesion NyAmModo { get => S("NY AM").Modo; set { S("NY AM").Modo = value; Recalcular(); } }

	[Display(Name = "NY AM: inicio personalizado (HH:mm)", GroupName = G1, Order = 43)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan NyAmInicio { get => S("NY AM").CustomInicio; set { S("NY AM").CustomInicio = value; Recalcular(); } }

	[Display(Name = "NY AM: fin personalizado (HH:mm)", GroupName = G1, Order = 44)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan NyAmFin { get => S("NY AM").CustomFin; set { S("NY AM").CustomFin = value; Recalcular(); } }

	[Display(Name = "NY AM: zona horaria (modo personalizado)", GroupName = G1, Order = 45)]
	public ZonaHoraria NyAmZona { get => S("NY AM").Zona; set { S("NY AM").Zona = value; Recalcular(); } }

	[Display(Name = "NY AM: color", GroupName = G1, Order = 46)]
	public MediaColor NyAmColor { get; set; } = MediaColor.FromArgb(255, 255, 152, 0);

	[Display(Name = "NY AM: sombrear", GroupName = G1, Order = 47)]
	public bool NyAmSombrear { get => S("NY AM").Sombrear; set => S("NY AM").Sombrear = value; }

	[Display(Name = "NY AM: extender máximo / mínimo", GroupName = G1, Order = 48)]
	public bool NyAmExtenderMaxMin { get => S("NY AM").ExtenderMaxMin; set => S("NY AM").ExtenderMaxMin = value; }

	[Display(Name = "NY AM: extender línea media", GroupName = G1, Order = 49)]
	public bool NyAmExtenderMedia { get => S("NY AM").ExtenderMedia; set => S("NY AM").ExtenderMedia = value; }

	[Display(Name = "NY AM: mostrar nombre", GroupName = G1, Order = 50)]
	public bool NyAmMostrarNombre { get => S("NY AM").MostrarNombre; set => S("NY AM").MostrarNombre = value; }

	#endregion

	#region Sesión NY PM

	[Display(Name = "NY PM: activa", GroupName = G1, Order = 50)]
	public bool NyPmActiva { get => S("NY PM").Activa; set { S("NY PM").Activa = value; Recalcular(); } }

	[Display(Name = "NY PM: nombre", GroupName = G1, Order = 51)]
	public string NyPmNombre { get => S("NY PM").Nombre; set { S("NY PM").Nombre = string.IsNullOrWhiteSpace(value) ? "NY PM" : value; Recalcular(); } }

	[Display(Name = "NY PM: modo", GroupName = G1, Order = 52, Description = "13:30-16:00 (ET)")]
	public ModoSesion NyPmModo { get => S("NY PM").Modo; set { S("NY PM").Modo = value; Recalcular(); } }

	[Display(Name = "NY PM: inicio personalizado (HH:mm)", GroupName = G1, Order = 53)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan NyPmInicio { get => S("NY PM").CustomInicio; set { S("NY PM").CustomInicio = value; Recalcular(); } }

	[Display(Name = "NY PM: fin personalizado (HH:mm)", GroupName = G1, Order = 54)]
	[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm")]
	public TimeSpan NyPmFin { get => S("NY PM").CustomFin; set { S("NY PM").CustomFin = value; Recalcular(); } }

	[Display(Name = "NY PM: zona horaria (modo personalizado)", GroupName = G1, Order = 55)]
	public ZonaHoraria NyPmZona { get => S("NY PM").Zona; set { S("NY PM").Zona = value; Recalcular(); } }

	[Display(Name = "NY PM: color", GroupName = G1, Order = 56)]
	public MediaColor NyPmColor { get; set; } = MediaColor.FromArgb(255, 244, 67, 54);

	[Display(Name = "NY PM: sombrear", GroupName = G1, Order = 57)]
	public bool NyPmSombrear { get => S("NY PM").Sombrear; set => S("NY PM").Sombrear = value; }

	[Display(Name = "NY PM: extender máximo / mínimo", GroupName = G1, Order = 58)]
	public bool NyPmExtenderMaxMin { get => S("NY PM").ExtenderMaxMin; set => S("NY PM").ExtenderMaxMin = value; }

	[Display(Name = "NY PM: extender línea media", GroupName = G1, Order = 59)]
	public bool NyPmExtenderMedia { get => S("NY PM").ExtenderMedia; set => S("NY PM").ExtenderMedia = value; }

	[Display(Name = "NY PM: mostrar nombre", GroupName = G1, Order = 60)]
	public bool NyPmMostrarNombre { get => S("NY PM").MostrarNombre; set => S("NY PM").MostrarNombre = value; }

	#endregion

	/// <summary>Color de dibujo de una sesión por su identificador.</summary>
	private MediaColor ColorSesion(string id) => id switch
	{
		"Asia" => AsiaColor,
		"Londres" => LondresColor,
		"Premercado" => PremercadoColor,
		"NY AM" => NyAmColor,
		_ => NyPmColor
	};
}
