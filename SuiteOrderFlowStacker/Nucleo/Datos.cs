namespace OrderFlowSuite.Nucleo;

using System;

// =====================================================================================================================
// Datos normalizados que la carcasa de ATAS pasa a los motores (sin tipos de ATAS): trades, cambios del DOM y
// mensajes MBO. Así los motores se prueban con datos sintéticos y con los CSV grabados.
// =====================================================================================================================

/// <summary>
/// Trade ejecutado. Agresor = lado que cruzó el spread (Compra = compró a mercado contra el Ask). Los identificadores
/// de orden solo llegan con MBO (Rithmic y similares): IdAgresor agrupa los trozos de una misma orden agresiva e
/// IdPasivo es la orden límite que se ejecutó.
/// </summary>
public readonly record struct TradeFlujo(DateTime Utc, decimal Precio, decimal Volumen, Lado? Agresor, long? IdAgresor = null, long? IdPasivo = null);

/// <summary>Cambio de un nivel del DOM (nivel 2): volumen visible en ese precio; Volumen = 0 = el nivel desapareció.</summary>
public readonly record struct CambioDom(DateTime Utc, decimal Precio, decimal Volumen, bool EsAsk);

public enum TipoMbo
{
	Snapshot,
	Nueva,
	Cambio,
	Borrada
}

/// <summary>
/// Mensaje MBO (nivel 3): una orden límite concreta. EsAsk = orden de venta en el ASK (si no, de compra en el BID).
/// Prioridad: número de cola del exchange; mayor = peor (detrás en la cola).
/// </summary>
public readonly record struct MensajeMbo(DateTime Utc, TipoMbo Tipo, long Id, decimal Precio, decimal Volumen, bool EsAsk, long Prioridad);
