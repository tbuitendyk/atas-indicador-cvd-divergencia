namespace ATAS.Indicators.Custom;

using System.ComponentModel.DataAnnotations;

// Tipos de los ajustes que aparecen en el panel de configuración de ATAS: tienen que ser públicos.

/// <summary>Cuánto dura una divergencia del CVD Trigger Engine.</summary>
public enum ModoVigencia
{
	[Display(Name = "Por barras")]
	PorBarras,

	[Display(Name = "Hasta divergencia opuesta")]
	HastaOpuesta
}

/// <summary>Cuántos días de historial usa el perfil de LVN.</summary>
public enum ModoHorizonte
{
	[Display(Name = "Automático (adaptativo)")]
	Automatico,

	[Display(Name = "Manual (días fijos)")]
	Manual
}

/// <summary>De dónde sale el volumen de cada precio del perfil de LVN.</summary>
public enum FuenteVolumen
{
	[Display(Name = "Real por nivel de precio")]
	Real,

	[Display(Name = "Repartido en el rango de la vela")]
	Uniforme
}

/// <summary>Qué cuenta como rechazo de un nivel de LVN.</summary>
public enum TipoRechazo
{
	[Display(Name = "Extremo de la vela en el nivel")]
	Extremo,

	[Display(Name = "Nivel dentro de la mecha")]
	Mecha
}
