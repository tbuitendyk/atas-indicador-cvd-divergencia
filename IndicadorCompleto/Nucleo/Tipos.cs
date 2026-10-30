namespace Completo.Nucleo;

using System.ComponentModel.DataAnnotations;

// ---------------------------------------------------------------------------------------------------------------
// Tipos de los ajustes visibles en ATAS: públicos y con nombre en español.
// ---------------------------------------------------------------------------------------------------------------

public enum RolIndicador
{
	[Display(Name = "Automático (maestro si el gráfico es de 1 minuto)")]
	Automatico,

	[Display(Name = "Maestro 1M (calcula)")]
	Maestro1M,

	[Display(Name = "Visor 1M")]
	Visor1M,

	[Display(Name = "Visor 5M")]
	Visor5M
}

public enum ReglaRecuperacion
{
	[Display(Name = "Solo 1M")]
	Solo1M,

	[Display(Name = "Solo 5M")]
	Solo5M,

	[Display(Name = "Primera válida (1M o 5M)")]
	PrimeraValida,

	[Display(Name = "Ambas (1M y 5M)")]
	Ambas
}

public enum ModoFiltro
{
	[Display(Name = "Apagado")]
	Apagado,

	[Display(Name = "Informativo")]
	Informativo,

	[Display(Name = "Obligatorio")]
	Obligatorio
}

public enum AnclajeSlFvg
{
	[Display(Name = "Extremo del rechazo")]
	Rechazo,

	[Display(Name = "Borde del FVG")]
	BordeFvg,

	[Display(Name = "Vela C2 del FVG")]
	C2,

	[Display(Name = "Vela C1 del FVG")]
	C1
}

public enum PerfilVisible
{
	[Display(Name = "Ninguno")]
	Ninguno,

	[Display(Name = "Última sesión NY")]
	NyAnterior,

	[Display(Name = "Últimas 5 sesiones")]
	Sesiones5,

	[Display(Name = "Últimas 10 sesiones")]
	Sesiones10,

	[Display(Name = "Últimas 15 sesiones")]
	Sesiones15,

	[Display(Name = "Últimas 20 sesiones")]
	Sesiones20
}

public enum MarcoEma
{
	[Display(Name = "1 minuto")]
	M1,

	[Display(Name = "5 minutos")]
	M5
}

public enum ZonaPresentacion
{
	[Display(Name = "Ciudad de México")]
	CiudadDeMexico,

	[Display(Name = "Nueva York")]
	NuevaYork,

	[Display(Name = "UTC")]
	Utc
}

public enum ReglaSubasta
{
	[Display(Name = "Sin compras (Ask = 0) en el extremo")]
	SinAgresionEnExtremo,

	[Display(Name = "Volumen del extremo menor que el mínimo")]
	VolumenMinimoEnExtremo
}

// ---------------------------------------------------------------------------------------------------------------
// Tipos internos del motor.
// ---------------------------------------------------------------------------------------------------------------

internal enum Direccion
{
	Larga,
	Corta
}

internal enum Sesgo
{
	NoDeterminado,
	Alcista,
	Bajista
}

internal enum Lectura
{
	SinDatos,
	Rango,
	Alcista,
	Bajista
}

internal enum Marco
{
	M1 = 1,
	M5 = 5,
	M15 = 15,
	H1 = 60
}

internal enum TipoLiquidez
{
	Externa,
	Interna
}

internal enum EstadoCondicion
{
	Cumplida,
	Pendiente,
	Fallida,
	Opcional,
	NoEvaluable
}

internal enum EstadoModelo
{
	NoActivo,
	EnFormacion,
	Bloqueado,
	Confirmado,
	Invalidado,
	Vencido,
	Consumido
}

internal enum PrioridadLvn
{
	Alta,
	Secundaria,
	Menor
}

internal static class Textos
{
	public static string Texto(this Direccion d) => d == Direccion.Larga ? "LONG" : "SHORT";

	public static string Texto(this Sesgo s) => s switch
	{
		Sesgo.Alcista => "ALCISTA",
		Sesgo.Bajista => "BAJISTA",
		_ => "NO DETERMINADO"
	};

	public static string Texto(this Lectura l) => l switch
	{
		Lectura.Alcista => "ALCISTA",
		Lectura.Bajista => "BAJISTA",
		Lectura.Rango => "RANGO",
		_ => "SIN DATOS"
	};

	public static string Texto(this Marco m) => m switch
	{
		Marco.M1 => "1M",
		Marco.M5 => "5M",
		Marco.M15 => "15M",
		_ => "1H"
	};

	public static string Texto(this TipoLiquidez t) => t == TipoLiquidez.Externa ? "EXTERNA" : "INTERNA";

	public static string Texto(this EstadoCondicion e) => e switch
	{
		EstadoCondicion.Cumplida => "Cumplida",
		EstadoCondicion.Pendiente => "Pendiente",
		EstadoCondicion.Fallida => "Fallida",
		EstadoCondicion.Opcional => "Opcional",
		_ => "No evaluable"
	};

	public static string Texto(this EstadoModelo e) => e switch
	{
		EstadoModelo.EnFormacion => "EN FORMACIÓN",
		EstadoModelo.Bloqueado => "BLOQUEADO",
		EstadoModelo.Confirmado => "CONFIRMADO",
		EstadoModelo.Invalidado => "INVALIDADO",
		EstadoModelo.Vencido => "VENCIDO",
		EstadoModelo.Consumido => "CONSUMIDO",
		_ => "NO ACTIVO"
	};

	public static string Texto(this PrioridadLvn p) => p switch
	{
		PrioridadLvn.Alta => "ALTA PRIORIDAD",
		PrioridadLvn.Secundaria => "SECUNDARIO",
		_ => "MENOR"
	};

	/// <summary>+1 para largos, -1 para cortos: convierte las comparaciones en una sola fórmula.</summary>
	public static int Signo(this Direccion d) => d == Direccion.Larga ? 1 : -1;
}
