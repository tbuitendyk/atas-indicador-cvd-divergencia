namespace ATAS.Indicators.Custom;

using System;

using ATAS.Indicators;

using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// Lo que los motores necesitan del indicador que los usa: velas, datos del instrumento y coordenadas del gráfico.
/// Cada indicador lo implementa de forma explícita con sus miembros de ATAS; así un mismo motor sirve para el
/// indicador individual y para el combinado.
/// </summary>
internal interface IGrafico
{
	IndicatorCandle Vela(int bar);

	/// <summary>Cantidad de velas del gráfico (CurrentBar); la última, CantidadVelas - 1, es la que está en curso.</summary>
	int CantidadVelas { get; }

	bool EsNuevaSesion(int bar);

	decimal TamanoTick { get; }

	/// <summary>Diferencia entre la hora del gráfico y UTC: las velas de ATAS están en UTC.</summary>
	TimeSpan ZonaHoraria { get; }

	string? TipoGrafico { get; }

	// Coordenadas del gráfico: solo válidas mientras se dibuja.

	Rectangle Area { get; }

	int PrimeraVisible { get; }

	int UltimaVisible { get; }

	/// <summary>Borde izquierdo de la vela.</summary>
	int XVela(int bar);

	int YPrecio(decimal precio);

	int AnchoVela { get; }

	/// <summary>Distancia entre velas consecutivas: ancho más separación.</summary>
	int EspacioVela { get; }
}
