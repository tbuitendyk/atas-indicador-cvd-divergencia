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
	private const string GSuite = "12. Sessions Sweeps Pro";
	private readonly Dictionary<long, LvnZone> _lvnPublicados = new();
	private long _rangoId = 1;
	private string? _identidadRango;
	private bool _conservarLineas;
	private DateTime _ultimoLatido;

	[Display(Name = "Borrar las líneas LVN al elegir un rango nuevo", GroupName = GSuite, Order = 10,
		Description = "Al seleccionar un rango nuevo se borran las líneas LVN manuales y las zonas del rango anterior (y se avisa a Sessions Sweeps Pro).")]
	public bool ClearLinesOnNewRange { get; set; } = true;

	[Display(Name = "Pedir confirmación antes de borrar", GroupName = GSuite, Order = 20)]
	public bool ConfirmBeforeClear { get; set; }

	private string Instrumento => InstrumentInfo?.Instrument ?? "";

	/// <summary>Rango del perfil (modo y fechas): si cambia, es un rango nuevo.</summary>
	private string IdentidadRango() => $"{_rangeMode}|{_startDateTime:O}|{_endDateTime:O}|{_lastNBars}|{_sessionStart}|{_sessionEnd}|{PiernaActiva}|{PiernaDesdeUtc:O}|{PiernaHastaUtc:O}";

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
		_seguidorLvn.Reiniciar();
		_seguidorEdge.Reiniciar();
		(_ultimaSeguida, _seguidorCargado) = (-1, false);

		// Solo las del instrumento de este gráfico (las de otros quedan guardadas).
		if (ClearLinesOnNewRange && !_conservarLineas)
			_manualLvnPrices = LvnManualesTexto.Escribir(ManualesGuardados().Where(m => m.Instrumento != Instrumento));

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

	[Display(Name = "Confirmación: cierres de vela", GroupName = G9, Order = 220,
		Description = "Un LVN nuevo es PROVISIONAL hasta verse en estos cierres de vela seguidos (LvnConfirmUpdates).")]
	[Range(1, 50)]
	public int LvnConfirmUpdates { get; set; } = 2;

	[Display(Name = "Histéresis (% del ancho)", GroupName = G9, Order = 230,
		Description = "Un LVN confirmado no se mueve mientras el nuevo centro quede dentro de su zona más este margen (LvnHysteresisPct).")]
	[Range(0, 500)]
	public int LvnHysteresisPct { get; set; } = 50;

	private readonly SeguidorLvn _seguidorLvn = new();
	private int _ultimaSeguida = -1;
	private bool _seguidorCargado;

	/// <summary>
	/// Tras cada análisis (v3, 7.7): las velas cerradas nuevas pasan por el estabilizador (toque, rechazo, aceptación), el
	/// resultado lo confirma o lo mantiene con histéresis, y se publican las zonas estables (con PROVISIONAL, estado,
	/// puntuación, profundidad, ancho) y BORRADO las que ya no están.
	/// </summary>
	private void SuitePublicarLvn(ResultadoTermico r, int ultima)
	{
		var ahora = DateTime.UtcNow;
		var cerrada = ultima != _ultimaSeguida;

		if (_seguidorCargado && cerrada && _ultimaSeguida >= 0)
		{
			for (var i = Math.Max(0, _ultimaSeguida); i < ultima; i++)
			{
				var c = GetCandle(i);
				_seguidorLvn.Precio(c.High, c.Low, c.Close);
				_seguidorEdge.Precio(c.High, c.Low, c.Close);
			}
		}

		// La primera vez (fin de la carga) el perfil sale de velas cerradas: se confirma en el acto.
		_seguidorLvn.Actualizar(r.Lvns, cerrada, ahora, _seguidorCargado ? LvnConfirmUpdates : 1, LvnHysteresisPct);
		SuitePublicarEdges(r, cerrada, ahora, _seguidorCargado ? LvnConfirmUpdates : 1);
		(_ultimaSeguida, _seguidorCargado) = (ultima, true);

		var actuales = new Dictionary<long, LvnZone>();

		foreach (var z in _seguidorLvn.Zonas)
		{
			var id = (_rangoId * 1_000_003 + z.Id * 2 + (z.Manual ? 1 : 0)) & long.MaxValue;
			var previo = _lvnPublicados.GetValueOrDefault(id);
			var igual = previo is not null && previo.Bajo == z.Bajo && previo.Alto == z.Alto && previo.Estado == z.Estado && previo.Provisional == z.Provisional &&
				previo.Score == z.ScoreTotal && previo.WasTouched == z.Tocado;

			actuales[id] = igual
				? previo!
				: new LvnZone
				{
					Id = id, Fuente = FuenteSuite.Thermal, Utc = previo?.Utc ?? ahora, CambioUtc = ahora, Version = (previo?.Version ?? 0) + 1,
					RangoId = _rangoId, Bajo = Math.Min(z.Bajo, z.Alto), Alto = Math.Max(z.Bajo, z.Alto), Centro = z.Centro, Manual = z.Manual, Estado = z.Estado,
					Score = z.ScoreTotal, Depth = (decimal)z.Depth, WidthTicks = z.WidthTicks, WasTouched = z.Tocado, Provisional = z.Provisional,
					ConfirmedUtc = z.ConfirmadoUtc, PrecioOriginal = z.PrecioOriginal
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

	/// <summary>Lo que se dibuja en la Suite: las zonas estables (sin repintado), con su estado o PROVISIONAL.</summary>
	private List<LvnTermico> SuiteLvnsParaDibujo(ResultadoTermico r)
	{
		if (!_seguidorCargado)
			return r.Lvns;

		var ranking = 0;
		return _seguidorLvn.Zonas.OrderByDescending(z => z.ScoreTotal).Select(z => new LvnTermico
		{
			Precio = z.Centro, Bajo = z.Bajo, Alto = z.Alto, Manual = z.Manual, Ranking = z.Manual ? 0 : ++ranking, Score100 = z.ScoreTotal, WidthTicks = z.WidthTicks,
			PrecioOriginal = z.PrecioOriginal, Metodo = z.Provisional ? "PROVISIONAL" : z.Estado == "ACTIVO" ? "" : z.Estado
		}).ToList();
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
