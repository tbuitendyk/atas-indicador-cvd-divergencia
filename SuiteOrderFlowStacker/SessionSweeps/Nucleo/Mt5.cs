namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

/// <summary>Modo del puente MT5 (17): APAGADO por defecto; REAL exige confirmación escrita.</summary>
public enum ModoMt5
{
	[Display(Name = "APAGADO")]
	Apagado,

	[Display(Name = "SIMULACIÓN (sin MT5)")]
	Simulacion,

	[Display(Name = "DEMO")]
	Demo,

	[Display(Name = "REAL")]
	Real
}

/// <summary>Ficha del símbolo que informa el EA de MT5 (tick, point, digits, volumen mínimo/máximo/paso, valor del tick por lote).</summary>
public sealed record FichaMt5(string Simbolo, decimal TickSize, decimal Point, int Digits, decimal VolumenMin, decimal VolumenMax, decimal VolumenPaso, decimal ValorTick)
{
	/// <summary>USD por 1 unidad de precio y 1 lote.</summary>
	public decimal ValorPorPrecio => TickSize > 0 ? ValorTick / TickSize : 0;
}

/// <summary>Mapeo de una familia de ATAS a un símbolo de MT5 (17.1), con su ficha y la validación del usuario.</summary>
public sealed class MapeoMt5
{
	public RaizInstrumento Raiz { get; init; }
	public string Simbolo { get; set; } = "";

	/// <summary>Distancia en MT5 por cada unidad de distancia en ATAS (1 si el símbolo sigue al mismo índice).</summary>
	public decimal FactorPrecio { get; set; } = 1m;

	public FichaMt5? Ficha { get; set; }

	/// <summary>El usuario revisó la prueba de conversión y la aceptó.</summary>
	public bool ValidadoPorUsuario { get; set; }

	public bool Validado => Simbolo.Length > 0 && Ficha is { TickSize: > 0, ValorTick: > 0, VolumenPaso: > 0 } && Ficha.Simbolo == Simbolo && ValidadoPorUsuario && FactorPrecio > 0;

	/// <summary>Prueba de conversión (17.1): lo que vale un punto de ATAS y su equivalente en MT5.</summary>
	public string PruebaConversion(EspecContrato? atas) => Ficha is not { } f
		? $"{Raiz} → {(Simbolo.Length > 0 ? Simbolo : "(sin símbolo)")}: sin ficha de MT5 (el EA aún no la envió)"
		: $"{Raiz}: 1 punto = {atas?.ValorPunto.ToString("0.##", CultureInfo.InvariantCulture) ?? "?"} USD por contrato · {f.Simbolo}: 1 punto × {FactorPrecio.ToString("0.####", CultureInfo.InvariantCulture)} = " +
		  $"{(f.ValorPorPrecio * FactorPrecio).ToString("0.####", CultureInfo.InvariantCulture)} USD por lote · tick {f.TickSize.ToString(CultureInfo.InvariantCulture)} · point {f.Point.ToString(CultureInfo.InvariantCulture)} · " +
		  $"digits {f.Digits} · volumen {f.VolumenMin.ToString(CultureInfo.InvariantCulture)}-{f.VolumenMax.ToString(CultureInfo.InvariantCulture)} paso {f.VolumenPaso.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Resultado de evaluar una orden.</summary>
public sealed record ResultadoMt5(bool Enviada, bool Simulada, string Texto, decimal Lotes = 0);

/// <summary>Una posición conocida (informada por el EA o simulada).</summary>
public sealed record PosicionMt5(RaizInstrumento Raiz, string Simbolo, int Direccion, decimal Lotes, long EventId);

/// <summary>
/// Puente con MT5 (17): decide si una entrada se manda y con cuántos lotes, y mantiene la cola de comandos para el EA.
/// Nunca manda en APAGADO, en SIMULACIÓN (simula dentro), en histórico o recarga, pausado o con órdenes deshabilitadas,
/// sin mapeo validado, con instrumento o calibración sin validar (salvo prueba explícita en SIMULACIÓN), con NOTICIA,
/// pasados los límites del día, con una posición en esa dirección o en la contraria, el mismo EventId dos veces o por
/// debajo del volumen mínimo del bróker. SL y TP viajan como DISTANCIAS (nunca precios absolutos del futuro).
/// </summary>
public sealed class GestorMt5
{
	private readonly object _sync = new();
	private readonly HashSet<long> _eventos = new();
	private readonly List<string> _comandos = new();
	private readonly List<(DateTime Utc, RaizInstrumento Raiz)> _operaciones = new();
	private readonly Dictionary<RaizInstrumento, PosicionMt5> _posiciones = new();
	private readonly Dictionary<RaizInstrumento, MapeoMt5> _mapeos = Instrumentos.Referencia.Keys.ToDictionary(r => r, r => new MapeoMt5 { Raiz = r });
	private DateTime _dia;
	private decimal _perdidaDia;

	public ModoMt5 Modo { get; set; } = ModoMt5.Apagado;
	public bool RealConfirmado { get; set; }
	public bool Pausado { get; set; }
	public bool OrdenesDeshabilitadas { get; set; }
	public int MaxOperacionesDia { get; set; } = 3;
	public decimal MaxPerdidaDiaUsd { get; set; } = 500;
	public bool PermitirSinCalibrarEnSimulacion { get; set; }
	public int MaxSlippagePuntos { get; set; } = 10;
	public DateTime? UltimoContactoUtc { get; private set; }

	/// <summary>Reloj del gestor (en vivo, la hora actual; en las pruebas, una fija).</summary>
	public Func<DateTime> Reloj { get; set; } = () => DateTime.UtcNow;
	public List<string> Registro { get; } = new();

	public MapeoMt5 Mapeo(RaizInstrumento r) => _mapeos.TryGetValue(r, out var m) ? m : new MapeoMt5 { Raiz = r };

	public IReadOnlyCollection<PosicionMt5> Posiciones
	{
		get
		{
			lock (_sync)
				return _posiciones.Values.ToList();
		}
	}

	public decimal PerdidaDia
	{
		get
		{
			lock (_sync)
				return _perdidaDia;
		}
	}

	private void Anotar(string texto)
	{
		Registro.Add($"{Reloj():HH:mm:ss} {texto}");

		if (Registro.Count > 200)
			Registro.RemoveAt(0);
	}

	private void NuevoDia(DateTime utc)
	{
		var dia = Zonas.DiaOperativo(utc);

		if (dia == _dia)
			return;

		(_dia, _perdidaDia) = (dia, 0);
		_operaciones.Clear();
	}

	/// <summary>Una entrada del Modelo A o B: se evalúa y, si procede, se encola (DEMO/REAL) o se simula.</summary>
	public ResultadoMt5 Evaluar(long eventId, RaizInstrumento raiz, int direccion, decimal distanciaSl, decimal distanciaTp, decimal riesgoUsd,
		EstadoInstrumento ins, bool enVivo, bool noticia, DateTime utc, string comentario)
	{
		lock (_sync)
		{
			NuevoDia(utc);
			var r = Decidir(eventId, raiz, direccion, distanciaSl, distanciaTp, riesgoUsd, ins, enVivo, noticia, utc, comentario);
			Anotar(r.Texto);
			return r;
		}
	}

	private ResultadoMt5 Decidir(long eventId, RaizInstrumento raiz, int direccion, decimal distanciaSl, decimal distanciaTp, decimal riesgoUsd,
		EstadoInstrumento ins, bool enVivo, bool noticia, DateTime utc, string comentario)
	{
		ResultadoMt5 No(string motivo) => new(false, false, $"SIN ORDEN #{eventId}: {motivo}");

		if (Modo == ModoMt5.Apagado)
			return No("MT5 APAGADO");

		if (!enVivo)
			return No("histórico o recarga");

		if (Pausado || OrdenesDeshabilitadas)
			return No(Pausado ? "PAUSADO" : "ÓRDENES DESHABILITADAS");

		if (Modo == ModoMt5.Real && !RealConfirmado)
			return No("REAL sin confirmación escrita");

		if (ins.Bloqueo is { } b || !ins.TickValidado)
			return No(ins.Bloqueo ?? Instrumentos.NoValidado);

		if (ins.Provisional && !(Modo == ModoMt5.Simulacion && PermitirSinCalibrarEnSimulacion))
			return No("calibración insuficiente");

		if (noticia)
			return No("NOTICIA bloqueada");

		if (!_eventos.Add(eventId))
			return No("EventId ya enviado (sin duplicados)");

		if (_operaciones.Count >= MaxOperacionesDia)
			return No($"máximo de {MaxOperacionesDia} operaciones del día");

		if (_perdidaDia >= MaxPerdidaDiaUsd)
			return No($"pérdida máxima del día ({_perdidaDia:0} USD)");

		if (_posiciones.TryGetValue(raiz, out var abierta) && abierta.Direccion != 0)
			return No(abierta.Direccion == direccion ? "ya hay una posición en esa dirección" : "hay una posición contraria abierta");

		var mapeo = Mapeo(raiz);

		if (Modo != ModoMt5.Simulacion && !mapeo.Validado)
			return No($"mapeo {raiz} → MT5 sin validar");

		var lotes = Modo == ModoMt5.Simulacion && !mapeo.Validado ? 0 : Lotes(mapeo, distanciaSl, riesgoUsd);

		if (Modo != ModoMt5.Simulacion && lotes <= 0)
			return No($"volumen calculado menor que el mínimo del bróker ({mapeo.Ficha?.VolumenMin.ToString(CultureInfo.InvariantCulture)})");

		_operaciones.Add((utc, raiz));
		var lado = direccion > 0 ? "BUY" : "SELL";
		var sl = (distanciaSl * mapeo.FactorPrecio).ToString(CultureInfo.InvariantCulture);
		var tp = (distanciaTp * mapeo.FactorPrecio).ToString(CultureInfo.InvariantCulture);

		if (Modo == ModoMt5.Simulacion)
		{
			_posiciones[raiz] = new PosicionMt5(raiz, mapeo.Simbolo, direccion, lotes, eventId);
			return new ResultadoMt5(false, true, $"SIMULADA #{eventId}: {raiz} {lado} {lotes.ToString(CultureInfo.InvariantCulture)} lotes · SL {sl} · TP {tp}", lotes);
		}

		_comandos.Add($"ORDEN|{eventId}|{mapeo.Simbolo}|{lado}|{lotes.ToString(CultureInfo.InvariantCulture)}|{sl}|{tp}|{MaxSlippagePuntos}|{comentario}");
		return new ResultadoMt5(true, false, $"ENVIADA #{eventId} ({Modo}): {mapeo.Simbolo} {lado} {lotes.ToString(CultureInfo.InvariantCulture)} lotes · SL {sl} · TP {tp}", lotes);
	}

	/// <summary>Lotes (17.2): riesgo / (distancia del SL en MT5 × USD por precio y lote), al paso del bróker, dentro de su mínimo y máximo (0 = no llega al mínimo).</summary>
	public static decimal Lotes(MapeoMt5 m, decimal distanciaSl, decimal riesgoUsd)
	{
		if (m.Ficha is not { } f || distanciaSl <= 0 || f.ValorPorPrecio <= 0 || f.VolumenPaso <= 0)
			return 0;

		var bruto = riesgoUsd / (distanciaSl * m.FactorPrecio * f.ValorPorPrecio);
		var pasos = Math.Floor(bruto / f.VolumenPaso);
		var lotes = Math.Min(pasos * f.VolumenPaso, f.VolumenMax > 0 ? f.VolumenMax : decimal.MaxValue);
		return lotes < f.VolumenMin ? 0 : lotes;
	}

	/// <summary>Comandos pendientes para el EA (los entrega una vez).</summary>
	public List<string> TomarComandos()
	{
		lock (_sync)
		{
			UltimoContactoUtc = Reloj();
			var r = _comandos.ToList();
			_comandos.Clear();
			return r;
		}
	}

	/// <summary>Pide al EA la ficha de los símbolos mapeados.</summary>
	public void PedirFichas()
	{
		lock (_sync)
		{
			foreach (var m in _mapeos.Values.Where(m => m.Simbolo.Length > 0))
				_comandos.Add($"INFO|{m.Simbolo}");
		}
	}

	public void CerrarTodo()
	{
		lock (_sync)
		{
			_comandos.Add("CERRAR_TODO|*");

			if (Modo == ModoMt5.Simulacion)
				_posiciones.Clear();

			Anotar("CERRAR TODO");
		}
	}

	/// <summary>Una línea del EA: INFO, FILL, POS, CLOSED o ERROR.</summary>
	public void Reporte(string linea)
	{
		var c = linea.Trim().Split('|');

		if (c.Length < 2)
			return;

		decimal D(int i) => i < c.Length && decimal.TryParse(c[i], NumberStyles.Number, CultureInfo.InvariantCulture, out var x) ? x : 0;

		lock (_sync)
		{
			UltimoContactoUtc = Reloj();

			switch (c[0])
			{
				case "INFO" when c.Length >= 9:
					foreach (var m in _mapeos.Values.Where(m => m.Simbolo == c[1]))
						m.Ficha = new FichaMt5(c[1], D(2), D(3), (int)D(4), D(5), D(6), D(7), D(8));

					break;

				case "POS" when c.Length >= 4:
					foreach (var m in _mapeos.Values.Where(m => m.Simbolo == c[1]))
					{
						var dir = (int)D(2);

						if (dir == 0)
							_posiciones.Remove(m.Raiz);
						else
							_posiciones[m.Raiz] = new PosicionMt5(m.Raiz, c[1], dir, D(3), 0);
					}

					break;

				case "CLOSED" when c.Length >= 3:
					NuevoDia(Reloj());
					_perdidaDia += Math.Max(0, -D(2));

					foreach (var m in _mapeos.Values.Where(m => m.Simbolo == c[1]))
						_posiciones.Remove(m.Raiz);

					Anotar($"CERRADA {c[1]}: {D(2).ToString("0.##", CultureInfo.InvariantCulture)} USD");
					break;

				case "FILL":
				case "ERROR":
					Anotar(string.Join(" ", c));
					break;
			}
		}
	}

	/// <summary>Recuadro 16 del panel: estado, prueba de conversión, límites del día y las últimas líneas del registro.</summary>
	public List<string> LineasPanel(RaizInstrumento raiz, EspecContrato? spec)
	{
		lock (_sync)
		{
			var l = new List<string> { Estado(raiz) };

			if (Modo == ModoMt5.Apagado)
				return l;

			var hoy = Zonas.DiaOperativo(Reloj());
			l.Add(Mapeo(raiz).PruebaConversion(spec));
			l.Add($"Hoy: {_operaciones.Count(o => Zonas.DiaOperativo(o.Utc) == hoy)}/{MaxOperacionesDia} operaciones · pérdida " +
				$"{(_dia == hoy ? _perdidaDia : 0).ToString("0", CultureInfo.InvariantCulture)} de {MaxPerdidaDiaUsd.ToString("0", CultureInfo.InvariantCulture)} USD");
			l.AddRange(Registro.Skip(Math.Max(0, Registro.Count - 3)));
			return l;
		}
	}

	public string Estado(RaizInstrumento raiz)
	{
		lock (_sync)
		{
			var m = Mapeo(raiz);
			var conexion = Modo is ModoMt5.Demo or ModoMt5.Real
				? UltimoContactoUtc is { } u && Reloj() - u < TimeSpan.FromSeconds(10) ? "EA CONECTADO" : "EA SIN CONTACTO"
				: "";
			var pos = _posiciones.TryGetValue(raiz, out var p) ? $"posición {(p.Direccion > 0 ? "LARGA" : "CORTA")} {p.Lotes.ToString(CultureInfo.InvariantCulture)}" : "sin posición";
			var bloqueo = Pausado ? " · PAUSADO" : OrdenesDeshabilitadas ? " · ÓRDENES DESHABILITADAS" : "";
			return $"MT5: {Modo.ToString().ToUpperInvariant()}{(Modo == ModoMt5.Apagado ? "" : $" · {(m.Simbolo.Length > 0 ? m.Simbolo : "sin símbolo")} {(m.Validado ? "VALIDADO" : "SIN VALIDAR")} · {conexion} · {pos}")}{bloqueo}"
				.Replace(" ·  ·", " ·", StringComparison.Ordinal);
		}
	}
}
