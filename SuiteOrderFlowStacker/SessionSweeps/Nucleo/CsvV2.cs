namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using OrderFlowSuite.Comun;

/// <summary>Una fila del CSV de barridos, leída para restaurar el estado congelado al recargar el historial (7.7, T16).</summary>
public sealed class RegistroCsvBarrido
{
	public long EventId;
	public DateTime T0Utc;
	public int Signo;
	public decimal L;
	public EstadoBarrido Estado;
	public DateTime TimeConfirmUtc;
	public decimal ScoreA, ScoreB, ScoreC, ScoreD, ScoreE, Penalties, Total;
	public string PremiumGates = "", StopsEvent = "", IcebergState = "", IcebergMode = "", AbsorptionEvent = "", SourcesLoaded = "", Transiciones = "";
	public bool ConflictoPine, News;
}

/// <summary>
/// CSV de Session Sweeps Pro (14): uno de barridos y otro de modelos / gatillos por día operativo, en
/// %APPDATA%\ATAS\OrderFlowSuite\SessionSweeps\{Instrumento}\yyyy-MM-dd_barridos.csv y _modelos.csv. Una fila por
/// congelación y por transición (al leer, la última de cada EventId es el estado final).
/// </summary>
public static class CsvV2
{
	public const string CabeceraBarridos =
		"TimestampUTC,EventId,TimeT0NY,TimeT0CDMX,Side,LevelType,LevelOrigin,L,ExtremoPrecio,PenetrationTicks,VolBeyond,VolBeyondRel,DeltaBeyond," +
		"BurstRatio,AggressorShare,StopsEvent,IcebergState,IcebergMode,AbsorptionEvent,F2Bars,CompositeBars,CompositePOC,FinishedAuction," +
		"UnfinishedAuction,StackedAgainst,StackedNewDir,ReclaimBars,F3DurationSec,F3RangeTicks,F3DeltaPct,DeltaFlip,RecoveryTicks,Recovery1mTime," +
		"Recovery5mTime,ScoreA,ScoreB,ScoreC,ScoreD,ScoreE,Penalties,ScoreTotal,Estado,TimeConfirmUtc,PremiumGates,ConflictoPine,News,DataMode," +
		"SourcesLoaded,Transiciones,T0Utc";

	public const string CabeceraModelos =
		"TimestampUTC,TradeId,Model,Label,Side,ParentEventId,ZoneType,ZoneLow,ZoneHigh,LvnOrigin,TriggerType,TriggerScore,TriggerReasons,Entry,Stop," +
		"TP1,TP2,Contracts,RiskUSD,Result,MAE_Ticks,MFE_Ticks,Origen";

	private const string Fecha = "yyyy-MM-dd HH:mm:ss";

	private static string D(decimal v) => v.ToString("0.####", CultureInfo.InvariantCulture);

	private static string D(decimal? v) => v is { } x ? D(x) : "";

	private static string H(DateTime? utc, Func<DateTime, DateTime> zona) => utc is { } t ? zona(t).ToString(Fecha, CultureInfo.InvariantCulture) : "";

	private static string B(bool b) => b ? "1" : "0";

	/// <summary>Comillas si hace falta (coma, comillas o salto de línea).</summary>
	public static string Celda(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";

	public static string FilaBarrido(EvaluacionBarrido e, DateTime ahoraUtc, string modoDatos)
	{
		var c = new[]
		{
			H(ahoraUtc, x => x), e.Id.ToString(CultureInfo.InvariantCulture), H(e.T0Utc, TiempoV2.AEt), H(e.T0Utc, TiempoV2.ACdmx),
			e.Signo > 0 ? "BUYSIDE" : "SELLSIDE", e.Nivel.Grupo is "dia" or "semana" ? "EXTERNA" : "INTERNA", e.Nivel.Origen, D(e.L), D(e.Extremo),
			e.PenetrationTicks.ToString(CultureInfo.InvariantCulture), D(e.VolBeyond), D(e.VolBeyondRel), D(e.DeltaBeyond), D(e.BurstRatio), D(e.AggressorShare),
			e.StopsEvento, e.IcebergEstado, e.IcebergModo, e.AbsorptionEvento, e.F2Bars.ToString(CultureInfo.InvariantCulture),
			e.CompositeBars.ToString(CultureInfo.InvariantCulture), D(e.CompositePoc), B(e.FinishedAuction), B(e.UnfinishedAuction), B(e.StackedAgainst),
			B(e.StackedNewDir), e.ReclaimBars.ToString(CultureInfo.InvariantCulture), e.F3DurationSec.ToString("0.##", CultureInfo.InvariantCulture),
			e.F3RangeTicks.ToString(CultureInfo.InvariantCulture), D(e.F3DeltaPct), B(e.DeltaFlip), e.RecoveryTicks.ToString(CultureInfo.InvariantCulture),
			H(e.Recovery1mUtc, TiempoV2.ACdmx), H(e.Recovery5mUtc, TiempoV2.ACdmx), D(e.ScoreA), D(e.ScoreB), D(e.ScoreC), D(e.ScoreD), D(e.ScoreE),
			D(e.Penalties), D(e.Total), e.Estado.ToString(), H(e.TimeConfirmUtc, x => x), e.PremiumGates, B(e.ConflictoPine), B(e.Noticia), modoDatos,
			e.SourcesLoaded, string.Join(" > ", e.Transiciones), e.T0Utc.ToString("O", CultureInfo.InvariantCulture)
		};

		return string.Join(",", c.Select(Celda));
	}

	public static string FilaModelo(SetupModelo s, DateTime ahoraUtc)
	{
		var c = new[]
		{
			H(ahoraUtc, x => x), s.Id.ToString(CultureInfo.InvariantCulture), s.Modelo, s.Etiqueta, s.Signo > 0 ? "LARGO" : "CORTO",
			s.Barrido?.Id.ToString(CultureInfo.InvariantCulture) ?? "", s.Zona.Tipo, D(s.Zona.Bajo), D(s.Zona.Alto), s.Zona.LvnOrigen,
			s.Gatillo is { Resultado: ResultadoGatillo.Valido } g ? (g.Tipo == TipoGatillo.Arranque ? "ARRANQUE" : "CONTINUIDAD") : "",
			s.Gatillo?.Score.ToString(CultureInfo.InvariantCulture) ?? "", string.Join(" | ", s.Gatillo?.Razones ?? new List<string>()),
			D(s.Entrada), D(s.Stop), D(s.Tp1), D(s.Tp2), s.Riesgo?.Contratos.ToString(CultureInfo.InvariantCulture) ?? "", D(s.Riesgo?.RiesgoUsd),
			s.Resultado.Length > 0 ? s.Resultado : s.TextoEstado, s.MaeTicks.ToString(CultureInfo.InvariantCulture), s.MfeTicks.ToString(CultureInfo.InvariantCulture),
			s.Origen
		};

		return string.Join(",", c.Select(Celda));
	}

	/// <summary>Gatillo manual (T28): Origen MANUAL, sin alerta.</summary>
	public static string FilaManual(ManualTriggerRequest q, ManualTriggerResult r, DateTime ahoraUtc)
	{
		var c = new[]
		{
			H(ahoraUtc, x => x), q.Id.ToString(CultureInfo.InvariantCulture), "MANUAL", r.VelaAnalizada, r.Direccion > 0 ? "LARGO" : r.Direccion < 0 ? "CORTO" : "[-]",
			"", "MANUAL", D(q.Bajo), D(q.Alto), "", r.Resultado.Contains("ARRANQUE", StringComparison.Ordinal) ? "ARRANQUE" : r.Resultado.Contains("CONTINUIDAD", StringComparison.Ordinal) ? "CONTINUIDAD" : "",
			r.Score.ToString(CultureInfo.InvariantCulture), string.Join(" | ", r.Razones), D(r.Entrada), D(r.Stop), "", "", "", "", r.Resultado, "", "", "MANUAL"
		};

		return string.Join(",", c.Select(Celda));
	}

	/// <summary>Separa una línea CSV (con comillas).</summary>
	public static List<string> Separar(string linea)
	{
		var r = new List<string>();
		var sb = new StringBuilder();
		var comillas = false;

		for (var i = 0; i < linea.Length; i++)
		{
			var ch = linea[i];

			if (comillas)
			{
				if (ch == '"' && i + 1 < linea.Length && linea[i + 1] == '"')
				{
					sb.Append('"');
					i++;
				}
				else if (ch == '"')
				{
					comillas = false;
				}
				else
				{
					sb.Append(ch);
				}
			}
			else if (ch == '"')
			{
				comillas = true;
			}
			else if (ch == ',')
			{
				r.Add(sb.ToString());
				sb.Clear();
			}
			else
			{
				sb.Append(ch);
			}
		}

		r.Add(sb.ToString());
		return r;
	}

	/// <summary>Filas de barridos (con cabecera); las que no se entienden se ignoran.</summary>
	public static List<RegistroCsvBarrido> LeerBarridos(IEnumerable<string> lineas)
	{
		var r = new List<RegistroCsvBarrido>();
		List<string>? cabecera = null;

		foreach (var linea in lineas)
		{
			if (string.IsNullOrWhiteSpace(linea))
				continue;

			var c = Separar(linea);

			if (cabecera is null || c[0] == "TimestampUTC")
			{
				cabecera = c;
				continue;
			}

			string V(string nombre) => cabecera.IndexOf(nombre) is var i and >= 0 && i < c.Count ? c[i] : "";
			decimal N(string nombre) => decimal.TryParse(V(nombre), NumberStyles.Number, CultureInfo.InvariantCulture, out var x) ? x : 0;
			DateTime? T(string nombre) => DateTime.TryParseExact(V(nombre), new[] { Fecha, Fecha + ".fff" }, CultureInfo.InvariantCulture,
				DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var x) ? DateTime.SpecifyKind(x, DateTimeKind.Utc) : null;

			if (!long.TryParse(V("EventId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || !Enum.TryParse<EstadoBarrido>(V("Estado"), out var estado) ||
			    !DateTime.TryParse(V("T0Utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t0) || T("TimeConfirmUtc") is not { } confirmado)
			{
				continue;
			}

			r.Add(new RegistroCsvBarrido
			{
				EventId = id, T0Utc = t0.ToUniversalTime(), Signo = V("Side") == "BUYSIDE" ? 1 : -1, L = N("L"), Estado = estado, TimeConfirmUtc = confirmado,
				ScoreA = N("ScoreA"), ScoreB = N("ScoreB"), ScoreC = N("ScoreC"), ScoreD = N("ScoreD"), ScoreE = N("ScoreE"), Penalties = N("Penalties"),
				Total = N("ScoreTotal"), PremiumGates = V("PremiumGates"), StopsEvent = V("StopsEvent"), IcebergState = V("IcebergState"),
				IcebergMode = V("IcebergMode"), AbsorptionEvent = V("AbsorptionEvent"), SourcesLoaded = V("SourcesLoaded"), Transiciones = V("Transiciones"),
				ConflictoPine = V("ConflictoPine") == "1", News = V("News") == "1"
			});
		}

		return r;
	}
}

/// <summary>Escritura asíncrona de los CSV: el hilo de datos encola; un temporizador escribe (y reintenta si Excel bloquea el archivo).</summary>
public sealed class GrabadorCsvV2 : IDisposable
{
	private readonly ConcurrentQueue<(string Ruta, string Cabecera, string Linea)> _cola = new();
	private readonly List<(string Ruta, string Cabecera, string Linea)> _reintentos = new();
	private readonly object _vaciando = new();
	private readonly Timer _temporizador;

	public GrabadorCsvV2(string carpetaBase, string instrumento)
	{
		(CarpetaBase, Instrumento) = (carpetaBase, string.IsNullOrWhiteSpace(instrumento) ? "Instrumento" : instrumento);
		_temporizador = new Timer(_ => Vaciar(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
	}

	public string CarpetaBase { get; }

	public string Instrumento { get; }

	public Action<string, Exception>? Error { get; set; }

	public static string CarpetaPorDefecto =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "OrderFlowSuite", "SessionSweeps");

	public static string Archivo(string carpetaBase, string instrumento, DateTime diaOperativo, string tipo)
	{
		var limpio = new string(instrumento.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
		return Path.Combine(carpetaBase, limpio, $"{diaOperativo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}_{tipo}.csv");
	}

	public string Archivo(DateTime diaOperativo, string tipo) => Archivo(CarpetaBase, Instrumento, diaOperativo, tipo);

	public void Barrido(EvaluacionBarrido e, DateTime ahoraUtc, string modoDatos) =>
		_cola.Enqueue((Archivo(Zonas.DiaOperativo(e.T0Utc), "barridos"), CsvV2.CabeceraBarridos, CsvV2.FilaBarrido(e, ahoraUtc, modoDatos)));

	public void Modelo(SetupModelo s, DateTime ahoraUtc) =>
		_cola.Enqueue((Archivo(Zonas.DiaOperativo(ahoraUtc), "modelos"), CsvV2.CabeceraModelos, CsvV2.FilaModelo(s, ahoraUtc)));

	public void Manual(ManualTriggerRequest q, ManualTriggerResult r, DateTime ahoraUtc) =>
		_cola.Enqueue((Archivo(Zonas.DiaOperativo(ahoraUtc), "modelos"), CsvV2.CabeceraModelos, CsvV2.FilaManual(q, r, ahoraUtc)));

	/// <summary>Barridos del día operativo (para restaurarlos al recargar). Sin archivo: lista vacía.</summary>
	public List<RegistroCsvBarrido> LeerBarridos(DateTime diaOperativo)
	{
		var ruta = Archivo(diaOperativo, "barridos");

		try
		{
			if (!File.Exists(ruta))
				return new List<RegistroCsvBarrido>();

			using var f = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var lector = new StreamReader(f, Encoding.UTF8);
			var lineas = new List<string>();

			while (lector.ReadLine() is { } l)
				lineas.Add(l);

			return CsvV2.LeerBarridos(lineas);
		}
		catch (Exception ex)
		{
			Error?.Invoke("Session Sweeps Pro: no se pudo leer " + ruta, ex);
			return new List<RegistroCsvBarrido>();
		}
	}

	public void Vaciar()
	{
		lock (_vaciando)
		{
			var lote = new List<(string Ruta, string Cabecera, string Linea)>(_reintentos);
			_reintentos.Clear();

			while (_cola.TryDequeue(out var x))
				lote.Add(x);

			foreach (var grupo in lote.GroupBy(x => x.Ruta))
			{
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(grupo.Key)!);
					var nuevo = !File.Exists(grupo.Key);
					using var f = new FileStream(grupo.Key, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
					using var w = new StreamWriter(f, new UTF8Encoding(false));

					if (nuevo)
						w.WriteLine(grupo.First().Cabecera);

					foreach (var x in grupo)
						w.WriteLine(x.Linea);
				}
				catch (Exception ex)
				{
					_reintentos.AddRange(grupo);
					Error?.Invoke("Session Sweeps Pro: CSV bloqueado, se reintenta: " + grupo.Key, ex);
				}
			}
		}
	}

	public void Dispose()
	{
		_temporizador.Dispose();
		Vaciar();
	}
}
