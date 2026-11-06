namespace OrderFlowSuite.Comun;

using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================================================
// Tipos compartidos por los indicadores de la suite y Session Sweeps Pro, y el bus de eventos que los une. Sin ATAS ni
// interfaz. El lado va como bool Compra (no como enumeración) para que ningún archivo tenga que importar dos "Lado"
// distintos: en icebergs es el lado PASIVO (compra = en el BID); en stops, sweeps, stacked, imbalance y absorción, el
// lado de la orden agresiva o de la señal.
// =====================================================================================================================

public enum FuenteSuite
{
	Iceberg,
	Stops,
	Sweeps,
	Stacked,
	Imbalance,
	Absorption,
	Thermal,
	SessionSweeps
}

public enum EstadoFuente
{
	NoCargado,
	SinDatos,
	Cargado
}

/// <summary>Base de todo lo que viaja por el bus. Id estable: el mismo hecho se actualiza (Version sube).</summary>
public abstract class EventoSuite
{
	public long Id { get; init; }
	public FuenteSuite Fuente { get; init; }
	public DateTime Utc { get; init; }
	public DateTime CambioUtc { get; init; }
	public int Version { get; init; }

	/// <summary>Precio de referencia para las consultas por rango de precio.</summary>
	public abstract decimal PrecioReferencia { get; }
}

public sealed class IcebergEvent : EventoSuite
{
	public decimal Precio { get; init; }

	/// <summary>Lado PASIVO: true = compra escondida en el BID (absorbe ventas).</summary>
	public bool Compra { get; init; }

	/// <summary>ACTIVO, AGUANTANDO, ROTO o CANCELADO.</summary>
	public string Estado { get; init; } = "";

	public decimal Ejecutado { get; init; }
	public int Recargas { get; init; }

	/// <summary>Detectado con MBO (nativo o sintético). False = INFERIDO (probable).</summary>
	public bool Mbo { get; init; }

	public bool AltaProbabilidad { get; init; }

	public override decimal PrecioReferencia => Precio;
}

public sealed class StopsEvent : EventoSuite
{
	/// <summary>Primer y último precio de la cascada; Extremo = el más lejano en su dirección.</summary>
	public decimal Desde { get; init; }

	public decimal Extremo { get; init; }

	/// <summary>Lado del agresor (false = cascada de ventas: stops de largos).</summary>
	public bool Compra { get; init; }

	public decimal Contratos { get; init; }
	public string Estado { get; init; } = "";
	public string Zona { get; init; } = "";

	public override decimal PrecioReferencia => Extremo;
}

public sealed class SweepTrackerEvent : EventoSuite
{
	public decimal Desde { get; init; }
	public decimal Hasta { get; init; }
	public int Niveles { get; init; }
	public decimal Contratos { get; init; }
	public double DuracionMs { get; init; }

	/// <summary>Lado del agresor.</summary>
	public bool Compra { get; init; }

	/// <summary>EVALUANDO, LIQUIDEZ, RUPTURA, CONTRA ICEBERG o SIN NIVEL.</summary>
	public string Tipo { get; init; } = "";

	public override decimal PrecioReferencia => Hasta;
}

public sealed class StackedEvent : EventoSuite
{
	public decimal Bajo { get; init; }
	public decimal Alto { get; init; }
	public int Niveles { get; init; }
	public bool Compra { get; init; }
	public string Estado { get; init; } = "";

	public override decimal PrecioReferencia => (Bajo + Alto) / 2;
}

/// <summary>Imbalances diagonales de una vela cerrada (los precios de cada nivel con imbalance).</summary>
public sealed class ImbalanceEvent : EventoSuite
{
	public IReadOnlyList<decimal> Precios { get; init; } = Array.Empty<decimal>();
	public bool Compra { get; init; }

	public override decimal PrecioReferencia => Precios.Count == 0 ? 0 : Precios.Average();
}

public sealed class AbsorptionEvent : EventoSuite
{
	public decimal Precio { get; init; }
	public bool Compra { get; init; }
	public int Score { get; init; }
	public string Setup { get; init; } = "";

	public override decimal PrecioReferencia => Precio;
}

/// <summary>LVN publicado por el Thermal Volume Profile + LVN (Suite): automático o trazado con el botón LVN.</summary>
public sealed class LvnZone : EventoSuite
{
	public long RangoId { get; init; }
	public decimal Bajo { get; init; }
	public decimal Alto { get; init; }
	public decimal Centro { get; init; }
	public bool Manual { get; init; }

	/// <summary>ACTIVO, MITIGADO o BORRADO.</summary>
	public string Estado { get; init; } = "ACTIVO";

	public override decimal PrecioReferencia => Centro;
}

/// <summary>El Thermal eligió un rango nuevo: las zonas LVN del rango anterior dejan de valer.</summary>
public sealed class RangeReset : EventoSuite
{
	public long RangoAnteriorId { get; init; }
	public long RangoNuevoId { get; init; }

	public override decimal PrecioReferencia => 0;
}

public enum TipoAnalisisManual
{
	Auto,
	VelaVolumen,
	VelaCompuesta
}

public enum DireccionManual
{
	Auto,
	Largo,
	Corto
}

/// <summary>Botón GATILLO del Thermal: el recuadro que dibujó el usuario (o el clic en un precio).</summary>
public sealed class ManualTriggerRequest : EventoSuite
{
	public DateTime DesdeUtc { get; init; }
	public DateTime HastaUtc { get; init; }
	public decimal Alto { get; init; }
	public decimal Bajo { get; init; }
	public TipoAnalisisManual Tipo { get; init; }
	public DireccionManual Direccion { get; init; }

	/// <summary>El recuadro llega hasta la barra actual: se reevalúa en vivo hasta "Fijar".</summary>
	public bool EnVivo { get; init; }

	public override decimal PrecioReferencia => (Alto + Bajo) / 2;
}

/// <summary>Respuesta de Session Sweeps Pro al gatillo manual.</summary>
public sealed class ManualTriggerResult : EventoSuite
{
	public long SolicitudId { get; init; }

	/// <summary>"GATILLO POR VELA DE ARRANQUE", "GATILLO POR VELA DE CONTINUIDAD", "NO EJECUTABLE" o "MOTOR NO CARGADO".</summary>
	public string Resultado { get; init; } = "";

	/// <summary>+1 largo, -1 corto, 0 sin dirección clara ("[-]").</summary>
	public int Direccion { get; init; }

	public string VelaAnalizada { get; init; } = "";
	public int Score { get; init; }
	public IReadOnlyList<string> Razones { get; init; } = Array.Empty<string>();
	public IReadOnlyList<string> Hallazgos { get; init; } = Array.Empty<string>();
	public decimal? Poc { get; init; }
	public decimal? Delta { get; init; }
	public string Subasta { get; init; } = "";
	public decimal? Entrada { get; init; }
	public decimal? Stop { get; init; }
	public decimal? RewardRisk { get; init; }
	public decimal Alto { get; init; }
	public decimal Bajo { get; init; }

	public override decimal PrecioReferencia => (Alto + Bajo) / 2;
}

/// <summary>
/// Bus de eventos de la suite: un canal por instrumento, un búfer circular por tipo (con reemplazo por Id: un evento
/// que cambia de estado se actualiza en su sitio), consultas por tiempo y precio, suscripciones y latidos.
/// Es un estático: todos los indicadores de la MISMA DLL (SuiteOrderFlowStacker.dll) lo comparten. Seguro entre hilos.
/// </summary>
public static class SuiteEventBus
{
	/// <summary>Versión del contrato de eventos. Si un indicador publica con otra: "VERSION SUITE DISTINTA".</summary>
	public const string Version = "2.0.0";

	private static readonly Dictionary<string, CanalSuite> Canales = new(StringComparer.OrdinalIgnoreCase);

	public static int TamanoBufer { get; set; } = 5000;

	public static CanalSuite Para(string instrumento)
	{
		lock (Canales)
		{
			var clave = string.IsNullOrWhiteSpace(instrumento) ? "?" : instrumento.Trim();

			if (!Canales.TryGetValue(clave, out var c))
				Canales[clave] = c = new CanalSuite(clave);

			return c;
		}
	}

	/// <summary>Solo para las pruebas: vacía todos los canales.</summary>
	public static void Reiniciar()
	{
		lock (Canales)
			Canales.Clear();
	}
}

public sealed class CanalSuite
{
	private readonly object _cerrojo = new();
	private readonly Dictionary<Type, List<EventoSuite>> _porTipo = new();
	private readonly Dictionary<FuenteSuite, (DateTime Utc, bool ConDatos, string Version)> _latidos = new();
	private readonly List<Action<EventoSuite>> _suscriptores = new();

	public CanalSuite(string instrumento) => Instrumento = instrumento;

	public string Instrumento { get; }

	/// <summary>Sin latido en este tiempo, la fuente cuenta como NO CARGADA.</summary>
	public static TimeSpan LatidoMaximo { get; set; } = TimeSpan.FromSeconds(6);

	/// <summary>Publica (o actualiza por Id) un evento y avisa a los suscriptores fuera del cerrojo.</summary>
	public void Publicar(EventoSuite e)
	{
		List<Action<EventoSuite>> suscriptores;

		lock (_cerrojo)
		{
			if (!_porTipo.TryGetValue(e.GetType(), out var lista))
				_porTipo[e.GetType()] = lista = new List<EventoSuite>();

			var i = lista.FindIndex(x => x.Id == e.Id);

			if (i >= 0)
				lista[i] = e;
			else
				lista.Add(e);

			var maximo = Math.Max(100, SuiteEventBus.TamanoBufer);

			if (lista.Count > maximo)
				lista.RemoveRange(0, lista.Count - maximo);

			suscriptores = _suscriptores.ToList();
		}

		foreach (var s in suscriptores)
			s(e);
	}

	public void Suscribir(Action<EventoSuite> accion)
	{
		lock (_cerrojo)
			_suscriptores.Add(accion);
	}

	public void Desuscribir(Action<EventoSuite> accion)
	{
		lock (_cerrojo)
			_suscriptores.Remove(accion);
	}

	/// <summary>Eventos de un tipo cuya hora (Utc o último cambio) cae en [desde, hasta] y que cumplen el filtro.</summary>
	public List<T> Consultar<T>(DateTime desde, DateTime hasta, Func<T, bool>? filtro = null) where T : EventoSuite
	{
		lock (_cerrojo)
		{
			if (!_porTipo.TryGetValue(typeof(T), out var lista))
				return new List<T>();

			return lista.OfType<T>()
				.Where(e => e.Utc <= hasta && e.CambioUtc >= desde)
				.Where(e => filtro is null || filtro(e))
				.ToList();
		}
	}

	/// <summary>Todos los eventos de un tipo (los últimos del búfer).</summary>
	public List<T> Todos<T>() where T : EventoSuite
	{
		lock (_cerrojo)
			return _porTipo.TryGetValue(typeof(T), out var lista) ? lista.OfType<T>().ToList() : new List<T>();
	}

	public void Quitar<T>(Func<T, bool> condicion) where T : EventoSuite
	{
		lock (_cerrojo)
		{
			if (_porTipo.TryGetValue(typeof(T), out var lista))
				lista.RemoveAll(e => e is T t && condicion(t));
		}
	}

	/// <summary>Latido (cada 2 s): el indicador está cargado; conDatos = recibe datos en vivo.</summary>
	public void Latido(FuenteSuite fuente, bool conDatos, DateTime ahoraUtc, string version = SuiteEventBus.Version)
	{
		lock (_cerrojo)
			_latidos[fuente] = (ahoraUtc, conDatos, version);
	}

	public EstadoFuente Estado(FuenteSuite fuente, DateTime ahoraUtc)
	{
		lock (_cerrojo)
		{
			if (!_latidos.TryGetValue(fuente, out var l) || ahoraUtc - l.Utc > LatidoMaximo)
				return EstadoFuente.NoCargado;

			return l.ConDatos ? EstadoFuente.Cargado : EstadoFuente.SinDatos;
		}
	}

	/// <summary>Versión con la que late una fuente (null si no late).</summary>
	public string? VersionDe(FuenteSuite fuente)
	{
		lock (_cerrojo)
			return _latidos.TryGetValue(fuente, out var l) ? l.Version : null;
	}
}
