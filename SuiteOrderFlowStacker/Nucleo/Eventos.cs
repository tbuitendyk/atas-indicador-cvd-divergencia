namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Registro común de eventos de la suite. Cada motor crea sus eventos aquí y los PUBLICA al nacer y en cada cambio de
/// estado: cada publicación sube la versión y deja una copia en <see cref="Novedades"/>, que la carcasa de ATAS vacía
/// para sonar la alerta (una por evento y por cambio de estado) y grabar la fila del CSV. Los cambios que no son de
/// estado (más recargas, más volumen) actualizan el texto sin publicar.
/// </summary>
public sealed class RegistroEventos
{
	private readonly Dictionary<long, EventoFlujo> _porId = new();

	public List<EventoFlujo> Todos { get; } = new();

	/// <summary>Copias de cada publicación, en orden. La carcasa las vacía con <see cref="TomarNovedades"/>.</summary>
	public List<EventoFlujo> Novedades { get; } = new();

	/// <summary>Falso mientras se recalcula el historial: entonces no hay novedades (ni alertas ni grabación).</summary>
	public bool EnVivo { get; set; }

	/// <summary>
	/// Se llama en cada publicación (también al recalcular el historial), con el evento original y bajo el cerrojo del
	/// motor: la carcasa lo usa para copiar sus eventos al bus de la suite. Debe ser rápido (solo encolar).
	/// </summary>
	public Action<EventoFlujo>? AlPublicar { get; set; }

	/// <summary>Contador de publicaciones (el Master se reevalúa cuando cambia).</summary>
	public long Publicaciones { get; private set; }

	/// <summary>Máximo de eventos en memoria (los más antiguos y terminados se descartan).</summary>
	public int Maximo { get; set; } = 3000;

	/// <summary>
	/// Identificador estable: el mismo evento recalculado (o leído del CSV) tiene el mismo Id, así no se duplica al
	/// recargar los CSV después de un recálculo.
	/// </summary>
	public static long IdEstable(string tipo, Lado lado, DateTime utc, decimal precio)
	{
		unchecked
		{
			var h = 1469598103934665603L;

			void Mezclar(long v)
			{
				h ^= v;
				h *= 1099511628211L;
			}

			foreach (var c in tipo)
				Mezclar(c);

			Mezclar((long)lado);
			Mezclar(utc.Ticks / TimeSpan.TicksPerMillisecond);
			Mezclar((long)Math.Round(precio * 10000m));
			return h & long.MaxValue;
		}
	}

	public EventoFlujo Crear(string indicador, string tipo, Lado lado, DateTime utc, decimal precio, ModoDatos modo, Sesion sesion)
	{
		var id = IdEstable(tipo, lado, utc, precio);

		// Dos eventos idénticos en el mismo milisegundo y precio: el segundo se desplaza para no chocar.
		while (_porId.ContainsKey(id))
			id = (id + 1) & long.MaxValue;

		var e = new EventoFlujo
		{
			Id = id,
			Indicador = indicador,
			Tipo = tipo,
			Lado = lado,
			Utc = utc,
			CambioUtc = utc,
			Precio = precio,
			Modo = modo,
			Sesion = sesion
		};

		_porId[id] = e;
		Todos.Add(e);
		Recortar();
		return e;
	}

	/// <summary>Publica el evento (nacimiento o cambio de estado): versión + 1 y una copia en Novedades si es en vivo.</summary>
	public void Publicar(EventoFlujo e, DateTime utc)
	{
		e.Version++;
		e.CambioUtc = utc;
		Publicaciones++;

		if (EnVivo && !e.Grabado && !e.Informativo)
			Novedades.Add(Copia(e));

		if (!e.Grabado && !e.Informativo)
			AlPublicar?.Invoke(e);
	}

	public List<EventoFlujo> TomarNovedades()
	{
		var r = Novedades.ToList();
		Novedades.Clear();
		return r;
	}

	/// <summary>Añade los eventos leídos del CSV que no estén ya en memoria (por Id).</summary>
	public int Importar(IEnumerable<EventoFlujo> grabados)
	{
		var n = 0;

		foreach (var g in grabados)
		{
			if (_porId.ContainsKey(g.Id))
				continue;

			g.Grabado = true;
			_porId[g.Id] = g;
			Todos.Add(g);
			n++;
		}

		Todos.Sort((a, b) => a.Utc.CompareTo(b.Utc));
		Recortar();
		return n;
	}

	/// <summary>Quita los eventos de un indicador (al recalcular las velas se vuelven a crear con el mismo Id).</summary>
	public void Quitar(Func<EventoFlujo, bool> condicion)
	{
		foreach (var e in Todos.Where(condicion).ToList())
		{
			Todos.Remove(e);
			_porId.Remove(e.Id);
		}
	}

	/// <summary>Los últimos eventos de un indicador (los más recientes primero) que siguen "vivos" según la vida útil.</summary>
	public List<EventoFlujo> Recientes(string indicador, DateTime ahoraUtc, int vidaMinutos, int maximo)
	{
		var limite = ahoraUtc - TimeSpan.FromMinutes(Math.Max(1, vidaMinutos));

		return Todos.Where(e => e.Indicador == indicador && !e.Informativo && e.CambioUtc >= limite)
			.OrderByDescending(e => e.CambioUtc)
			.Take(Math.Max(1, maximo))
			.ToList();
	}

	public static EventoFlujo Copia(EventoFlujo e) => new()
	{
		Id = e.Id,
		Version = e.Version,
		Indicador = e.Indicador,
		Tipo = e.Tipo,
		Lado = e.Lado,
		Utc = e.Utc,
		CambioUtc = e.CambioUtc,
		Precio = e.Precio,
		PrecioFin = e.PrecioFin,
		Volumen = e.Volumen,
		Clip = e.Clip,
		Recargas = e.Recargas,
		Score = e.Score,
		Ratio = e.Ratio,
		Estado = e.Estado,
		Sesgo = e.Sesgo,
		Sesion = e.Sesion,
		Modo = e.Modo,
		Contexto = e.Contexto,
		Texto = e.Texto,
		Grabado = e.Grabado,
		Informativo = e.Informativo,
		AltaProbabilidad = e.AltaProbabilidad,
		StopLoss = e.StopLoss,
		Referencia = e.Referencia,
		Niveles = e.Niveles,
		DuracionMs = e.DuracionMs
	};

	private void Recortar()
	{
		if (Todos.Count <= Maximo)
			return;

		foreach (var e in Todos.Take(Todos.Count - Maximo).ToList())
		{
			Todos.Remove(e);
			_porId.Remove(e.Id);
		}
	}
}
