// Solo dentro de SuiteOrderFlowStacker (símbolo SUITE): en el ThermalVolumeProfile por separado este archivo queda vacío.
#if SUITE
namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using OrderFlowSuite.Comun;

using Termico;

using Utils.Common.Logging;

/// <summary>
/// Thermal Volume Profile + LVN (Suite) como fuente de Session Sweeps Pro (5.2-5.4): publica en el bus cada LVN
/// (automático y los trazados con el botón LVN), el cambio de rango (RangeReset) y su latido. Opción obligatoria
/// ClearLinesOnNewRange: al elegir un rango nuevo se borran las líneas LVN del rango anterior.
/// </summary>
public partial class ThermalVolumeProfileLVN
{
	private const string GSuite = "12. Session Sweeps Pro";
	private readonly Dictionary<long, LvnZone> _lvnPublicados = new();
	private long _rangoId = 1;
	private string? _identidadRango;
	private bool _conservarLineas;
	private DateTime _ultimoLatido;

	[Display(Name = "Borrar las líneas LVN al elegir un rango nuevo", GroupName = GSuite, Order = 10,
		Description = "Al seleccionar un rango nuevo se borran las líneas LVN manuales y las zonas del rango anterior (y se avisa a Session Sweeps Pro).")]
	public bool ClearLinesOnNewRange { get; set; } = true;

	[Display(Name = "Pedir confirmación antes de borrar", GroupName = GSuite, Order = 20)]
	public bool ConfirmBeforeClear { get; set; }

	private string Instrumento => InstrumentInfo?.Instrument ?? "";

	/// <summary>Rango del perfil (modo y fechas): si cambia, es un rango nuevo.</summary>
	private string IdentidadRango() => $"{_rangeMode}|{_startDateTime:O}|{_endDateTime:O}|{_lastNBars}|{_sessionStart}|{_sessionEnd}";

	/// <summary>Al empezar un cálculo (vela 0): si el rango cambió, se borran las líneas (opción) y se publica RangeReset.</summary>
	private void SuiteAlReiniciar()
	{
		var identidad = IdentidadRango();

		if (_identidadRango is null)
		{
			_identidadRango = identidad;
			return;
		}

		if (identidad == _identidadRango)
			return;

		_identidadRango = identidad;

		if (ClearLinesOnNewRange && !_conservarLineas)
			_manualLvnPrices = "";

		_conservarLineas = false;
		var anterior = _rangoId++;
		var canal = SuiteEventBus.Para(Instrumento);
		var ahora = DateTime.UtcNow;

		foreach (var z in _lvnPublicados.Values)
			canal.Publicar(Borrado(z, ahora));

		_lvnPublicados.Clear();
		canal.Publicar(new RangeReset { Id = anterior * 7919 + 13, Fuente = FuenteSuite.Thermal, Utc = ahora, CambioUtc = ahora, Version = 1, RangoAnteriorId = anterior, RangoNuevoId = _rangoId });
	}

	/// <summary>Con la barra RANGO: si hay líneas manuales y se pide confirmación, se pregunta antes de borrarlas.</summary>
	private void SuiteAntesDeNuevoRango()
	{
		if (!ClearLinesOnNewRange || !ConfirmBeforeClear || string.IsNullOrWhiteSpace(_manualLvnPrices))
			return;

		try
		{
			// VERIFICAR en ATAS real: un MessageBox de WPF desde el evento del ratón del gráfico.
			var r = System.Windows.MessageBox.Show("¿Borrar las líneas LVN del rango anterior?", "Thermal Volume Profile + LVN", System.Windows.MessageBoxButton.YesNo);
			_conservarLineas = r != System.Windows.MessageBoxResult.Yes;
		}
		catch (Exception ex)
		{
			this.LogError("Thermal (Suite): no se pudo pedir confirmación", ex);
		}
	}

	/// <summary>Tras cada análisis: publica los LVN nuevos y marca BORRADO los que ya no están.</summary>
	private void SuitePublicarLvn(ResultadoTermico r)
	{
		var ahora = DateTime.UtcNow;
		var actuales = new Dictionary<long, LvnZone>();

		foreach (var l in r.Lvns)
		{
			var bajo = l.Manual ? l.Precio : r.Detectores.PrecioFila(l.Inferior);
			var alto = l.Manual ? l.Precio : r.Detectores.PrecioFila(l.Superior) + r.Detectores.TicksPorFila * r.Detectores.Tick - r.Detectores.Tick;
			var id = IdLvn(l.Precio, l.Manual);

			actuales[id] = _lvnPublicados.TryGetValue(id, out var previo) && previo.Bajo == bajo && previo.Alto == alto
				? previo
				: new LvnZone
				{
					Id = id, Fuente = FuenteSuite.Thermal, Utc = previo?.Utc ?? ahora, CambioUtc = ahora, Version = (previo?.Version ?? 0) + 1,
					RangoId = _rangoId, Bajo = Math.Min(bajo, alto), Alto = Math.Max(bajo, alto), Centro = l.Precio, Manual = l.Manual, Estado = "ACTIVO"
				};
		}

		var canal = SuiteEventBus.Para(Instrumento);

		foreach (var (id, z) in actuales)
		{
			if (!_lvnPublicados.TryGetValue(id, out var previo) || !ReferenceEquals(previo, z))
				canal.Publicar(z);
		}

		foreach (var (id, z) in _lvnPublicados)
		{
			if (!actuales.ContainsKey(id))
				canal.Publicar(Borrado(z, ahora));
		}

		_lvnPublicados.Clear();

		foreach (var (id, z) in actuales)
			_lvnPublicados[id] = z;
	}

	private long IdLvn(decimal precio, bool manual) => (_rangoId * 1_000_003 + (long)Math.Round(precio * 4) * 2 + (manual ? 1 : 0)) & long.MaxValue;

	private static LvnZone Borrado(LvnZone z, DateTime ahora) => new()
	{
		Id = z.Id, Fuente = FuenteSuite.Thermal, Utc = z.Utc, CambioUtc = ahora, Version = z.Version + 1, RangoId = z.RangoId,
		Bajo = z.Bajo, Alto = z.Alto, Centro = z.Centro, Manual = z.Manual, Estado = "BORRADO"
	};

	/// <summary>Latido de la fuente Thermal (cada 2 s como mucho).</summary>
	private void SuiteLatido()
	{
		var ahora = DateTime.UtcNow;

		if (ahora - _ultimoLatido < TimeSpan.FromSeconds(2) || string.IsNullOrEmpty(Instrumento))
			return;

		_ultimoLatido = ahora;
		SuiteEventBus.Para(Instrumento).Latido(FuenteSuite.Thermal, _resultado is not null, ahora);
		OrderFlowSuite.Ui.VentanaFlotante.Registrar();
		SuiteCiclo(ahora);
	}
}
#endif
