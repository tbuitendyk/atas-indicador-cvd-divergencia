namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>Una alerta de v2.0 para la carcasa (sonido y ventana de ATAS; correo si procede).</summary>
public sealed record AlertaV2(string Clave, string Categoria, string Texto, bool Correo, string? Asunto, string? Cuerpo);

/// <summary>Lo que hace la carcasa con las novedades: en el historial se descartan; en vivo, cada evento suena una sola vez.</summary>
public sealed class DespachoAlertasV2
{
	private readonly HashSet<string> _vistas = new();

	public List<AlertaV2> Procesar(IEnumerable<NovedadV2> novedades, bool enVivo, ParametrosV2 p, string simbolo, Func<decimal, string> precio, RaizInstrumento raiz)
	{
		var r = new List<AlertaV2>();

		if (!enVivo)
			return r;

		if (_vistas.Count > 20000)
			_vistas.Clear();

		foreach (var n in novedades)
		{
			if (PoliticaAlertasV2.Evaluar(n, p, simbolo, precio, raiz) is { } a && _vistas.Add(a.Clave))
				r.Add(a);
		}

		return r;
	}
}

/// <summary>
/// Alertas (13): TRAMPA >= AlertMinState al congelar; UPGRADE PREMIUM; MOD3 activado; RUPTURA confirmada (arma MOD4);
/// gatillo VÁLIDO en MOD1/BFVG/MOD2/MOD3/MOD4; FALLIDO y RUPTURA FALLIDA. Nunca con NOTICIA. Una vez por evento (la clave
/// la usa la carcasa) y solo en vivo (la carcasa descarta las novedades del historial). Correo (anexo): cada toma de
/// liquidez EXTERNA válida con entrada, stop y objetivos ya calculados.
/// </summary>
public static class PoliticaAlertasV2
{
	public const string Clasificacion = "Clasificacion";
	public const string Modelos = "Modelos";
	public const string Entrada = "Entrada";
	public const string Fallidos = "Fallidos";

	public static AlertaV2? Evaluar(NovedadV2 n, ParametrosV2 p, string simbolo, Func<decimal, string> precio, RaizInstrumento raiz)
	{
		var e = n.Evaluacion;

		if (e?.Noticia == true || TiempoV2.EnNoticia(n.Utc, p))
			return null;

		var clave = $"{n.Tipo}|{e?.Id}|{n.Setup?.Id}";
		var hora = TiempoV2.HoraCdmx(n.Utc);

		switch (n.Tipo)
		{
			case "CONFIRMADO" when e is not null && e.Estado.EsReal() && e.Estado.Rango() >= (int)p.AlertMinState + 1:
				return new AlertaV2(clave, Clasificacion, $"{simbolo} {hora} · {n.Texto}", false, null, null);

			case "UPGRADE PREMIUM":
				return new AlertaV2(clave, Clasificacion, $"{simbolo} {hora} · {n.Texto}", false, null, null);

			case "MOD3 ACTIVADO":
			case "RUPTURA (arma MOD4)":
				return new AlertaV2(clave, Modelos, $"{simbolo} {hora} · {n.Texto}", false, null, null);

			case "FALLIDO":
			case "RUPTURA FALLIDA":
				return new AlertaV2(clave, Fallidos, $"{simbolo} {hora} · {n.Texto}", false, null, null);

			case "GATILLO VÁLIDO" when n.Setup is { } s:
				var externa = s.Barrido?.Nivel.Grupo is "dia" or "semana";
				return new AlertaV2(clave, Entrada, $"{simbolo} {hora} · {n.Texto}", externa, externa ? Asunto(s, simbolo) : null,
					externa ? Cuerpo(s, simbolo, precio, raiz, n.Utc) : null);

			default:
				return null;
		}
	}

	public static string Asunto(SetupModelo s, string simbolo) =>
		$"Sessions Sweeps Pro · {simbolo} · {s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")} · {s.Barrido?.Nombre ?? s.Zona.Tipo}";

	/// <summary>Correo de una toma de liquidez externa válida: contexto, entrada, SL, TP y plan de riesgo.</summary>
	public static string Cuerpo(SetupModelo s, string simbolo, Func<decimal, string> precio, RaizInstrumento raiz, DateTime utc)
	{
		var e = s.Barrido;
		var sb = new StringBuilder();
		var rr = s.Entrada is { } en && s.Stop is { } st && en != st && s.Tp1 is { } t1 ? Math.Abs(t1 - en) / Math.Abs(en - st) : 0;
		sb.AppendLine($"{simbolo} · {TiempoV2.HoraCdmx(utc)} ({TiempoV2.HoraEt(utc, "HH:mm")})");
		sb.AppendLine($"{s.Etiqueta} {(s.Signo > 0 ? "LARGO" : "CORTO")} · zona {s.Zona.Tipo} {precio(s.Zona.Bajo)} - {precio(s.Zona.Alto)}");

		if (e is not null)
		{
			var validacion = e.IcebergEstado == "AGUANTANDO" && e.IcebergModo == "MBO" ? "Certificada como real por iceberg (MBO)"
				: e.IcebergModo == "INFERIDO" ? "Probable: iceberg inferido" : "Sin iceberg";
			sb.AppendLine($"Barrido de liquidez externa: {e.Nombre} {precio(e.L)} a las {TiempoV2.HoraCdmx(e.T0Utc)} · {e.Estado.Texto()} {e.Total:0}/100 · {validacion}");
		}

		sb.AppendLine();
		sb.AppendLine($"Gatillo: {s.Gatillo?.Respuesta} (score {s.Gatillo?.Score})");
		sb.AppendLine($"ENTRADA {(s.Entrada is { } a ? precio(a) : "-")}");
		sb.AppendLine($"STOP    {(s.Stop is { } b ? precio(b) : "-")}");
		sb.AppendLine($"TP1     {(s.Tp1 is { } c ? precio(c) : "-")}  (R:R {rr.ToString("0.0", CultureInfo.InvariantCulture)})");
		sb.AppendLine($"TP2     {(s.Tp2 is { } d ? precio(d) : "-")}");

		if (s.Riesgo is { } plan)
			sb.AppendLine(plan.Texto(raiz));

		if (s.Gatillo is { Razones.Count: > 0 } g)
		{
			sb.AppendLine();
			sb.AppendLine("Razones:");

			foreach (var r in g.Razones.Take(8))
				sb.AppendLine("- " + r);
		}

		sb.AppendLine();
		sb.AppendLine("Estadística simulada; no es un registro de operaciones reales.");
		return sb.ToString();
	}
}
