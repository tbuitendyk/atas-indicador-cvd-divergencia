namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Todos los parámetros del motor. Los valores iniciales son los del prompt del propietario; ninguno está
/// validado como óptimo: son propuestos para pruebas. El indicador de ATAS copia aquí sus ajustes.
/// </summary>
public sealed class ConfiguracionCompleto
{
	// --- Instrumento y riesgo -------------------------------------------------------------------------------

	/// <summary>Tamaño del tick del instrumento (lo pone el indicador desde ATAS).</summary>
	public decimal TamanoTick { get; set; } = 0.25m;

	/// <summary>Nombre del instrumento según ATAS (lo pone el indicador).</summary>
	public string Instrumento { get; set; } = "";

	/// <summary>Prefijos de instrumento admitidos para emitir señales, separados por ';'.</summary>
	public string InstrumentosPermitidos { get; set; } = "NQ;MNQ";

	/// <summary>El usuario confirma haber revisado los valores monetarios; sin esto no se admite ninguna señal.</summary>
	public bool ValoresMonetariosVerificados { get; set; }

	public decimal RiesgoUsd { get; set; } = 250m;

	/// <summary>Valor monetario de un tick por contrato (0,50 USD en MNQ).</summary>
	public decimal ValorTick { get; set; } = 0.50m;

	public decimal ComisionIdaVuelta { get; set; } = 1.50m;

	public int DeslizamientoTicks { get; set; } = 2;

	public int MaximoContratos { get; set; } = 20;

	public decimal RrNetoMinimo { get; set; } = 1m;

	// --- Estructura 1H y lecturas ---------------------------------------------------------------------------

	public int FuerzaPivote1H { get; set; } = 2;

	public int MargenEstructuralTicks { get; set; } = 4;

	/// <summary>Fuerza de los pivotes de 1M y 5M (liquidez interna, directrices y divergencias).</summary>
	public int FuerzaPivoteInterno { get; set; } = 2;

	// --- Liquidez y barridos ------------------------------------------------------------------------------

	public bool VentanaTokio { get; set; }

	public bool VentanaLondres { get; set; } = true;

	public bool VentanaNuevaYork { get; set; } = true;

	/// <summary>Cierres anticipados de la sesión de NY: "aaaa-mm-dd hh:mm; ..." en hora de Nueva York.</summary>
	public string CierresAnticipadosNy { get; set; } = "";

	public int PenetracionMinimaTicks { get; set; } = 4;

	public ReglaRecuperacion ReglaRecuperacion { get; set; } = ReglaRecuperacion.PrimeraValida;

	public int RecuperacionTicks1M { get; set; } = 16;

	public int RecuperacionTicks5M { get; set; } = 32;

	public int PlazoRecuperacionMinutos { get; set; } = 10;

	public int VigenciaOportunidadMinutos { get; set; } = 30;

	// --- Absorción ----------------------------------------------------------------------------------------

	public ModoFiltro ModoAbsorcion { get; set; } = ModoFiltro.Informativo;

	public decimal MultiplicadorVolumenClimatico { get; set; } = 1.5m;

	/// <summary>Delta mínimo, en % del volumen de la vela, para considerar agresión.</summary>
	public decimal AgresionPct { get; set; } = 10m;

	public int AvanceMaximoTicks { get; set; } = 24;

	public int VelasCambioDelta { get; set; } = 2;

	// --- Modelos y gatillo --------------------------------------------------------------------------------

	public bool ActivarM1 { get; set; } = true;

	public bool ActivarM2 { get; set; } = true;

	/// <summary>Si es falso (como en el prompt), un evento de barrido produce como mucho una señal.</summary>
	public bool ModelosIndependientes { get; set; }

	/// <summary>Gatillo largo: POC y centro del área de valor como máximo en esta posición relativa (cortos: 1 - valor).</summary>
	public decimal PosicionGatillo { get; set; } = 0.60m;

	public int MargenRupturaLvnTicks { get; set; } = 2;

	public int CuerpoMinimoRupturaTicks { get; set; } = 4;

	/// <summary>Rango mínimo de la vela de ruptura respecto a la media de las 20 anteriores (desplazamiento).</summary>
	public decimal ExpansionMinimaRuptura { get; set; } = 1.2m;

	public bool ExigirFvg { get; set; }

	public int FvgMinimoTicks { get; set; } = 2;

	public int FvgCercaniaTicks { get; set; } = 8;

	public AnclajeSlFvg AnclajeSlFvg { get; set; } = AnclajeSlFvg.Rechazo;

	public int BufferSlTicks { get; set; } = 12;

	public int AnticipacionTpTicks { get; set; } = 2;

	public ModoFiltro ModoDirectriz { get; set; } = ModoFiltro.Informativo;

	// --- LVN ----------------------------------------------------------------------------------------------

	public decimal BandaLvnPuntos { get; set; } = 400m;

	public int HombroLvnTicks { get; set; } = 24;

	/// <summary>Un precio es valle si su volumen es como mucho esta fracción del hombro más bajo.</summary>
	public decimal UmbralValle { get; set; } = 0.60m;

	public int SuavizadoLvnTicks { get; set; } = 2;

	/// <summary>Distancia mínima entre centros de LVN: de dos más cercanos queda el de mayor puntuación.</summary>
	public decimal SeparacionMinimaLvnPuntos { get; set; } = 10m;

	public int CantidadLvnAltaPrioridad { get; set; } = 3;

	public int CantidadLvnSecundarios { get; set; } = 5;

	public int ReaccionAlejamientoTicks { get; set; } = 40;

	public int ReaccionPlazoMinutos { get; set; } = 15;

	public int ReaccionExcursionAdversaTicks { get; set; } = 20;

	// --- EMA y gestión simulada ---------------------------------------------------------------------------

	public int PeriodoEma { get; set; } = 15;

	public MarcoEma MarcoEma { get; set; } = MarcoEma.M1;

	/// <summary>Nota del propietario: al cerrar la vela cruzada por la EMA en contra, se cierra la operación.</summary>
	public bool SalidaPorEma { get; set; } = true;

	public bool SimularResultados { get; set; } = true;

	public decimal AgotamientoPosicion { get; set; } = 0.70m;

	public ReglaSubasta ReglaSubasta { get; set; } = ReglaSubasta.SinAgresionEnExtremo;

	public decimal VolumenMinimoExtremo { get; set; } = 5m;

	public int VigenciaCandidataVelas { get; set; } = 5;

	public int MargenSalidaTicks { get; set; } = 2;

	public decimal IniciativaDeltaPct { get; set; } = 10m;

	public bool ExigirEmaParaSalir { get; set; }

	public bool ProteccionPoc { get; set; }

	public int BufferPocTicks { get; set; } = 4;

	// --- CVD ----------------------------------------------------------------------------------------------

	/// <summary>Hora de reinicio del CVD en hora de Nueva York.</summary>
	public TimeSpan ReinicioCvdNy { get; set; } = new(9, 30, 0);

	// --- Utilidades ---------------------------------------------------------------------------------------

	public decimal Ticks(int ticks) => ticks * TamanoTick;

	public long ATick(decimal precio) => (long)Math.Round(precio / TamanoTick, MidpointRounding.AwayFromZero);

	public decimal APrecio(long tick) => tick * TamanoTick;

	public bool InstrumentoPermitido()
	{
		var prefijos = InstrumentosPermitidos.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (prefijos.Length == 0)
			return true;

		return prefijos.Any(p => Instrumento.StartsWith(p, StringComparison.OrdinalIgnoreCase)
			|| Instrumento.Contains("(" + p + ")", StringComparison.OrdinalIgnoreCase));
	}

	public ConfiguracionCompleto Copiar() => (ConfiguracionCompleto)MemberwiseClone();

	/// <summary>Costes por contrato: comisión de ida y vuelta más el deslizamiento valorado.</summary>
	public decimal CostesPorContrato => ComisionIdaVuelta + DeslizamientoTicks * ValorTick;

	public IEnumerable<string> ParametrosPropuestos() => new[]
	{
		$"Penetración mínima {PenetracionMinimaTicks} ticks", $"Recuperación 1M {RecuperacionTicks1M} / 5M {RecuperacionTicks5M} ticks",
		$"Plazo recuperación {PlazoRecuperacionMinutos} min", $"Vigencia oportunidad {VigenciaOportunidadMinutos} min",
		$"Gatillo POC/VA ≤ {PosicionGatillo:0.00}", $"Buffer SL {BufferSlTicks} ticks", $"Hombro LVN {HombroLvnTicks} ticks",
		$"Valle LVN ≤ {UmbralValle:0.00}", $"Reacción {ReaccionAlejamientoTicks} ticks en {ReaccionPlazoMinutos} min",
		$"Absorción: volumen ×{MultiplicadorVolumenClimatico}, agresión {AgresionPct}%"
	};
}
