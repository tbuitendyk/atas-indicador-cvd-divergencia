namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>Una alerta para la carcasa: ATAS, correo y Telegram según los canales de su categoría.</summary>
public sealed record AlertaV2(string Clave, CategoriaAlerta Categoria, string Texto, string Asunto, string Cuerpo);

/// <summary>Lo que el mensaje necesita además de la novedad: instrumento, contrato, formato de precio y estado de MT5.</summary>
public sealed class ContextoMensaje
{
	public string Instrumento { get; init; } = "NQ";
	public string Contrato { get; init; } = "";
	public string Mt5 { get; init; } = "MT5: APAGADO";
	public Func<decimal, string> Precio { get; init; } = p => p.ToString("#,##0.00", CultureInfo.InvariantCulture);
}

/// <summary>Lo que hace la carcasa con las novedades: en el historial se descartan; en vivo, cada evento suena una sola vez.</summary>
public sealed class DespachoAlertasV2
{
	private readonly HashSet<string> _vistas = new();

	public List<AlertaV2> Procesar(IEnumerable<NovedadV2> novedades, bool enVivo, ParametrosV2 p, ContextoMensaje ctx)
	{
		var r = new List<AlertaV2>();

		if (!enVivo)
			return r;

		if (_vistas.Count > 20000)
			_vistas.Clear();

		foreach (var n in novedades)
		{
			if (PoliticaAlertasV2.Evaluar(n, p, ctx) is { } a && _vistas.Add(a.Clave))
				r.Add(a);
		}

		return r;
	}
}

/// <summary>
/// Alertas v3 (15), solo estas cinco: BARRIDO DE LIQUIDEZ REAL; BARRIDO DE LIQUIDEZ REAL PREMIUM (un upgrade de REAL a
/// PREMIUM se etiqueta UPGRADE PREMIUM, sin repetir la de REAL); RUPTURA / ACEPTACIÓN CONFIRMADA (aviso de contexto);
/// GATILLO VÁLIDO DEL MODELO M1 / M2 / M3; v4: M3 ACTIVADO y RUPTURA DE LÍNEA DE SESIÓN. Nunca en el historial (la
/// carcasa), con NOTICIA ni dos veces.
/// </summary>
public static class PoliticaAlertasV2
{
	public static AlertaV2? Evaluar(NovedadV2 n, ParametrosV2 p, ContextoMensaje ctx)
	{
		var e = n.Evaluacion;

		if (e?.Noticia == true || TiempoV2.EnNoticia(n.Utc, p))
			return null;

		var clave = $"{n.Tipo}|{e?.Id}|{n.Setup?.Id}";

		switch (n.Tipo)
		{
			case "CONFIRMADO" when e is not null && e.Estado.EsReal() && e.Estado.Rango() >= (int)p.AlertMinState + 1:
				var premium = e.Estado == EstadoBarrido.Premium;
				return Crear(clave, premium ? CategoriaAlerta.Premium : CategoriaAlerta.Real,
					premium ? "BARRIDO DE LIQUIDEZ REAL PREMIUM" : "BARRIDO DE LIQUIDEZ REAL", n, e.SignoGiro, null, ctx);

			case "UPGRADE PREMIUM" when e is not null && p.AlertMinState <= EstadoMinimoAlerta.Premium:
				return Crear(clave, CategoriaAlerta.Premium, "UPGRADE PREMIUM", n, e.SignoGiro, null, ctx);

			case "CONFIRMADO" when e is not null && e.Estado == EstadoBarrido.Ruptura:
				return Crear(clave, CategoriaAlerta.Ruptura, "RUPTURA / ACEPTACIÓN CONFIRMADA (contexto)", n, e.Signo, null, ctx);

			case "GATILLO VÁLIDO" when n.Setup is { } s:
				return Crear(clave, s.ModeloOperativo switch { "M1" => CategoriaAlerta.EntradaM1, "M3" => CategoriaAlerta.EntradaM3, _ => CategoriaAlerta.EntradaM2 },
					$"GATILLO VÁLIDO DEL MODELO {s.ModeloOperativo}", n, s.Signo, s, ctx);

			// v4: la señal propia del Modelo 3 (iceberg rebasado con continuidad).
			case "MOD3 ACTIVADO" when p.EnableModelM3:
				return Crear(clave + "|" + n.Utc.Ticks, CategoriaAlerta.M3Activado, "M3 ACTIVADO: ROMPIMIENTO CONTINUIDAD", n, n.Texto.Contains("ALCISTA", StringComparison.Ordinal) ? 1 : -1, null, ctx);

			// v4: ruptura de una línea de sesión (sobre todo NY): sonido y aviso arriba a la derecha.
			case "LÍNEA DE SESIÓN" when n.Linea is { } l && p.AlertaDeLinea(l):
				return Crear($"LINEA|{l.Id}", CategoriaAlerta.LineaSesion, $"RUPTURA DE LÍNEA: {l.Nombre}", n, l.Alto ? 1 : -1, null, ctx);

			default:
				return null;
		}
	}

	private static AlertaV2 Crear(string clave, CategoriaAlerta categoria, string titulo, NovedadV2 n, int direccion, SetupModelo? s, ContextoMensaje ctx)
	{
		var dir = direccion > 0 ? "ALCISTA" : direccion < 0 ? "BAJISTA" : "";
		var hora = $"{TiempoV2.HoraCdmx(n.Utc)} ({TiempoV2.HoraEt(n.Utc, "HH:mm")})";
		var asunto = $"Sessions Sweeps Pro · {ctx.Instrumento} {ctx.Contrato} · {titulo} {dir}".TrimEnd();
		return new AlertaV2(clave, categoria, $"{ctx.Instrumento} {hora} · {titulo} {dir} · {n.Texto}", asunto, Cuerpo(titulo, dir, hora, n, s, ctx));
	}

	/// <summary>Cuerpo del correo y de Telegram (15): instrumento, contrato, dirección, horas, clasificación, zona, gatillo, plan, trackers y MT5.</summary>
	public static string Cuerpo(string titulo, string direccion, string hora, NovedadV2 n, SetupModelo? s, ContextoMensaje ctx)
	{
		var f = ctx.Precio;
		var e = s?.Barrido ?? n.Evaluacion;
		var sb = new StringBuilder();
		sb.AppendLine($"{titulo} {direccion}".TrimEnd());
		sb.AppendLine($"Instrumento: {ctx.Instrumento} · contrato {ctx.Contrato} · {hora}");

		if (e is not null)
		{
			var validacion = e.IcebergEstado == "AGUANTANDO" && e.IcebergModo == "MBO" ? "certificada por iceberg (MBO)"
				: e.IcebergModo == "INFERIDO" ? "probable: iceberg inferido" : "sin iceberg";
			sb.AppendLine($"Barrido: {e.Nombre} {f(e.L)} · {e.Estado.Texto()} {e.Total:0}/100 · {validacion}");
		}

		if (s is not null)
		{
			var rr = s.Entrada is { } en && s.Stop is { } st && en != st && s.Tp1 is { } t1 ? Math.Abs(t1 - en) / Math.Abs(en - st) : 0;
			sb.AppendLine($"Modelo {s.ModeloOperativo}: {s.Etiqueta} · zona {s.Zona.Tipo} {f(s.Zona.Bajo)} - {f(s.Zona.Alto)}");
			sb.AppendLine($"Gatillo: {s.Gatillo?.Respuesta} (score {s.Gatillo?.Score})");
			sb.AppendLine($"ENTRADA {(s.Entrada is { } a ? f(a) : "-")} · SL {(s.Stop is { } b ? f(b) : "-")} · TP {(s.Tp1 is { } c ? f(c) : "-")} · R:R {rr.ToString("0.0", CultureInfo.InvariantCulture)}");

			if (s.Riesgo is { } plan)
				sb.AppendLine(plan.Texto(Enum.TryParse<RaizInstrumento>(ctx.Instrumento, out var r) ? r : RaizInstrumento.NQ));
		}

		if (e is not null)
		{
			sb.AppendLine($"Trackers: iceberg {(e.IcebergEstado.Length > 0 ? $"{e.IcebergEstado} {e.IcebergModo}" : "sin evento")} · stops {(e.StopsEvento.Length > 0 ? e.StopsEvento : "sin evento")}" +
				$" · absorción {(e.AbsorptionEvento.Length > 0 ? e.AbsorptionEvento : "sin evento")}");
		}

		sb.AppendLine(ctx.Mt5);
		sb.AppendLine();
		sb.AppendLine(AvisoLegal);
		return sb.ToString();
	}

	/// <summary>Aviso legal del prompt v3 (19).</summary>
	public const string AvisoLegal = "Las señales y estadísticas son informativas y deben validarse. No son un registro de operaciones reales salvo que provengan de ejecuciones confirmadas.";
}
