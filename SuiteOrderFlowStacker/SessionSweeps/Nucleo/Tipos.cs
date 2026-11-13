namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

// =====================================================================================================================
// Tipos de NQSessionSweepsPro. Los públicos con [Display] son los de los ajustes de ATAS (nombres en español para que
// no choquen con tipos de ATAS.Indicators, que C# preferiría por estar en el espacio de nombres que nos contiene).
// =====================================================================================================================

public enum ModoSesion
{
	[Display(Name = "Sesión completa")]
	Completa,

	[Display(Name = "Kill Zone")]
	KillZone,

	[Display(Name = "Personalizado (inicio y fin)")]
	Custom
}

public enum PresetHorarios
{
	[Display(Name = "Horarios del prompt (ET)")]
	Estandar,

	[Display(Name = "PineOriginal (Tokio, Londres, ET)")]
	PineOriginal
}

public enum ZonaHoraria
{
	[Display(Name = "Nueva York (ET)")]
	NuevaYork,

	[Display(Name = "Londres")]
	Londres,

	[Display(Name = "Tokio")]
	Tokio,

	[Display(Name = "Ciudad de México")]
	CiudadDeMexico,

	[Display(Name = "UTC")]
	Utc
}

public enum VentanaBarridos
{
	[Display(Name = "NY (premercado + NY AM + NY PM)")]
	NY,

	[Display(Name = "Todas las horas")]
	Todas,

	[Display(Name = "Personalizada (inicio y fin ET)")]
	Custom
}

public enum RangoPerfilLvn
{
	[Display(Name = "Día anterior RTH + overnight actual")]
	DiaAnteriorRthMasOvernight,

	[Display(Name = "Día actual desde 18:00 ET")]
	DiaActual,

	[Display(Name = "Últimos N días")]
	UltimosNDias,

	[Display(Name = "Personalizado (fechas)")]
	Custom
}

public enum ContratoValor
{
	[Display(Name = "NQ (20 USD por punto)")]
	NQ,

	[Display(Name = "MNQ (2 USD por punto)")]
	MNQ
}

public enum TamanoTexto
{
	[Display(Name = "Pequeño")]
	Pequeno,

	[Display(Name = "Normal")]
	Normal,

	[Display(Name = "Grande")]
	Grande
}

/// <summary>BSL: liquidez por encima de un máximo (su barrido sugiere bajada). SSL: por debajo de un mínimo.</summary>
public enum Lado
{
	Buyside,
	Sellside
}

public enum EstadoNivel
{
	Activo,
	Barrido,
	Roto,
	Retirado
}

public enum Clasificacion
{
	Pendiente,
	Real,
	Falso,
	SinLiquidez,
	Dudoso
}

public enum EtapaModeloB
{
	Inactivo,
	EsperandoImpulso,
	Impulso,
	Retroceso,
	Entrada,
	Cancelado
}

public enum ContextoH1
{
	Rango,
	Alcista,
	Bajista
}

public enum TipoAviso
{
	Ruptura,
	CuentaAtras1m,
	CuentaAtras5m,
	Clasificacion,
	ActualizacionW5,
	Falso,
	EntradaA,
	EntradaB,
	Ifvg
}

public static class Textos
{
	public static string Texto(this Clasificacion c) => c switch
	{
		Clasificacion.Real => "SWEEP REAL",
		Clasificacion.Falso => "ROMPIMIENTO",
		Clasificacion.SinLiquidez => "SIN LIQ",
		Clasificacion.Dudoso => "DUDOSO",
		_ => "PENDIENTE"
	};

	public static string Texto(this ContextoH1 c) => c switch
	{
		ContextoH1.Alcista => "Alcista",
		ContextoH1.Bajista => "Bajista",
		_ => "Rango"
	};

	public static string Texto(this EtapaModeloB e) => e switch
	{
		EtapaModeloB.EsperandoImpulso => "ESPERANDO IMPULSO",
		EtapaModeloB.Impulso => "IMPULSO",
		EtapaModeloB.Retroceso => "RETROCESO A LVN",
		EtapaModeloB.Entrada => "ENTRADA",
		EtapaModeloB.Cancelado => "CANCELADO",
		_ => "SIN SECUENCIA"
	};

	/// <summary>"MAX" para un máximo (buyside), "MIN" para un mínimo (sellside).</summary>
	public static string Extremo(this Lado l) => l == Lado.Buyside ? "MAX" : "MIN";

	public static int Signo(this Lado l) => l == Lado.Buyside ? 1 : -1;
}

// --- Entrada ------------------------------------------------------------------------------------------------------

/// <summary>Volumen negociado en un precio de la vela: Bid = ventas agresivas, Ask = compras agresivas.</summary>
public readonly record struct NivelVela(decimal Precio, decimal Bid, decimal Ask);

/// <summary>Vela cerrada tal como la entrega ATAS (hora de apertura en UTC).</summary>
public sealed class VelaEntrada
{
	public DateTime InicioUtc { get; set; }

	public decimal Open { get; set; }

	public decimal High { get; set; }

	public decimal Low { get; set; }

	public decimal Close { get; set; }

	public decimal Volume { get; set; }

	public decimal Delta { get; set; }

	/// <summary>Delta máximo y mínimo dentro de la vela (mechas de las velas de CVD); 0 si no se conocen.</summary>
	public decimal MaxDelta { get; set; }

	public decimal MinDelta { get; set; }

	public decimal Ask { get; set; }

	public decimal Bid { get; set; }

	public List<NivelVela> Niveles { get; } = new();
}
