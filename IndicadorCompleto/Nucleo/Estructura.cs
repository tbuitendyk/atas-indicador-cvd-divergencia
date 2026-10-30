namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Pivote confirmado: se conoce al cerrar la vela que completa su fuerza derecha.</summary>
internal sealed record Pivote(Marco Marco, int Indice, DateTime ExtremoUtc, DateTime ConfirmadoUtc, decimal Precio, bool EsMaximo,
	decimal Cvd, int SesionCvd, bool CvdParcial);

internal static class Pivotes
{
	/// <summary>Pivotes en la vela (n - fuerza), confirmados por la última vela cerrada n.</summary>
	public static IEnumerable<Pivote> Detectar(List<Vela> velas, int fuerza)
	{
		var n = velas.Count - 1;
		var i = n - fuerza;

		if (fuerza < 1 || i - fuerza < 0)
			yield break;

		var candidata = velas[i];
		var esMaximo = true;
		var esMinimo = true;

		for (var j = i - fuerza; j <= n; j++)
		{
			if (j == i)
				continue;

			esMaximo &= velas[j].Maximo < candidata.Maximo;
			esMinimo &= velas[j].Minimo > candidata.Minimo;
		}

		var confirmado = velas[n].FinUtc;

		if (esMaximo)
		{
			yield return new Pivote(candidata.Marco, i, candidata.InicioUtc, confirmado, candidata.Maximo, true,
				candidata.CvdMaximo, candidata.SesionCvd, candidata.CvdParcial);
		}

		if (esMinimo)
		{
			yield return new Pivote(candidata.Marco, i, candidata.InicioUtc, confirmado, candidata.Minimo, false,
				candidata.CvdMinimo, candidata.SesionCvd, candidata.CvdParcial);
		}
	}

	/// <summary>Lectura del rango anterior: cierre sobre el máximo anterior, bajo el mínimo anterior o dentro.</summary>
	public static Lectura LeerRango(List<Vela> velas)
	{
		if (velas.Count < 2)
			return Lectura.SinDatos;

		var actual = velas[^1];
		var anterior = velas[^2];

		if (actual.Cierre > anterior.Maximo)
			return Lectura.Alcista;

		return actual.Cierre < anterior.Minimo ? Lectura.Bajista : Lectura.Rango;
	}
}

internal sealed record EventoEstructural(DateTime Utc, string Tipo, Sesgo Sesgo, decimal NivelRoto, decimal? SwingProtegido);

/// <summary>
/// Contexto estructural de 1H: pivotes confirmados, rupturas por cierre (BOS) y swing protegido. La pérdida del
/// swing protegido por cierre cambia el sesgo (MSS). Sin ninguna ruptura todavía, el contexto no está determinado.
/// </summary>
internal sealed class Estructura1H
{
	private readonly ConfiguracionCompleto _cfg;
	private readonly HashSet<Pivote> _rotos = new();

	public Estructura1H(ConfiguracionCompleto cfg) => _cfg = cfg;

	public Sesgo Sesgo { get; private set; } = Sesgo.NoDeterminado;

	public Pivote? SwingProtegido { get; private set; }

	public DateTime? DesdeUtc { get; private set; }

	/// <summary>Extremo alcanzado a favor del sesgo desde la última ruptura: límite del rango interno.</summary>
	public decimal? ExtremoRango { get; private set; }

	public List<Pivote> Maximos { get; } = new();

	public List<Pivote> Minimos { get; } = new();

	public List<EventoEstructural> Eventos { get; } = new();

	public DateTime? UltimaVelaUtc { get; private set; }

	public Pivote? MaximoElegible => Maximos.LastOrDefault(p => !_rotos.Contains(p));

	public Pivote? MinimoElegible => Minimos.LastOrDefault(p => !_rotos.Contains(p));

	/// <summary>Procesa la última vela de 1H cerrada. Devuelve los pivotes que acaba de confirmar.</summary>
	public List<Pivote> Procesar(List<Vela> velas1H)
	{
		var nuevos = Pivotes.Detectar(velas1H, _cfg.FuerzaPivote1H).ToList();

		foreach (var pivote in nuevos)
			(pivote.EsMaximo ? Maximos : Minimos).Add(pivote);

		var vela = velas1H[^1];
		UltimaVelaUtc = vela.FinUtc;
		var margen = _cfg.Ticks(_cfg.MargenEstructuralTicks);

		var maximo = MaximoElegible;
		var minimo = MinimoElegible;

		var alcista = (maximo is not null && vela.Cierre > maximo.Precio + margen)
			|| (Sesgo == Sesgo.Bajista && SwingProtegido is not null && vela.Cierre > SwingProtegido.Precio + margen);

		var bajista = (minimo is not null && vela.Cierre < minimo.Precio - margen)
			|| (Sesgo == Sesgo.Alcista && SwingProtegido is not null && vela.Cierre < SwingProtegido.Precio - margen);

		// Una vela enorme que rompe por los dos lados se resuelve por la dirección de su cuerpo.
		if (alcista && bajista)
		{
			alcista = vela.Alcista;
			bajista = !vela.Alcista;
		}

		if (alcista)
			Romper(Sesgo.Alcista, vela, maximo?.Precio ?? SwingProtegido!.Precio);
		else if (bajista)
			Romper(Sesgo.Bajista, vela, minimo?.Precio ?? SwingProtegido!.Precio);
		else if (Sesgo == Sesgo.Alcista)
			ExtremoRango = Math.Max(ExtremoRango ?? vela.Maximo, vela.Maximo);
		else if (Sesgo == Sesgo.Bajista)
			ExtremoRango = Math.Min(ExtremoRango ?? vela.Minimo, vela.Minimo);

		return nuevos;
	}

	private void Romper(Sesgo nuevo, Vela vela, decimal nivelRoto)
	{
		var tipo = Sesgo == Sesgo.NoDeterminado ? "BOS inicial" : Sesgo == nuevo ? "BOS" : "MSS";

		// Los pivotes superados por el cierre dejan de ser elegibles.
		foreach (var p in nuevo == Sesgo.Alcista ? Maximos : Minimos)
		{
			if (nuevo == Sesgo.Alcista ? vela.Cierre > p.Precio : vela.Cierre < p.Precio)
				_rotos.Add(p);
		}

		// Swing protegido: el último pivote contrario confirmado, del lado correcto del cierre.
		SwingProtegido = nuevo == Sesgo.Alcista
			? Minimos.LastOrDefault(p => p.Precio < vela.Cierre)
			: Maximos.LastOrDefault(p => p.Precio > vela.Cierre);

		if (Sesgo != nuevo)
			DesdeUtc = vela.FinUtc;

		Sesgo = nuevo;
		ExtremoRango = nuevo == Sesgo.Alcista ? vela.Maximo : vela.Minimo;
		Eventos.Add(new EventoEstructural(vela.FinUtc, tipo, nuevo, nivelRoto, SwingProtegido?.Precio));
	}

	/// <summary>Rango interno: entre el swing protegido y el extremo a favor alcanzado desde la ruptura.</summary>
	public (decimal Inferior, decimal Superior)? RangoInterno()
	{
		if (Sesgo == Sesgo.NoDeterminado || SwingProtegido is null || ExtremoRango is not { } extremo)
			return null;

		var protegido = SwingProtegido.Precio;
		return protegido < extremo ? (protegido, extremo) : (extremo, protegido);
	}

	/// <summary>Condición concreta que cambiaría el contexto.</summary>
	public string CondicionCambio(Func<decimal, string> precio)
	{
		var margen = _cfg.Ticks(_cfg.MargenEstructuralTicks);

		return Sesgo switch
		{
			Sesgo.Alcista when SwingProtegido is not null => $"Cierre 1H < {precio(SwingProtegido.Precio - margen)} (pérdida del swing protegido)",
			Sesgo.Bajista when SwingProtegido is not null => $"Cierre 1H > {precio(SwingProtegido.Precio + margen)} (pérdida del swing protegido)",
			Sesgo.NoDeterminado => (MaximoElegible, MinimoElegible) switch
			{
				({ } max, { } min) => $"Cierre 1H > {precio(max.Precio + margen)} o < {precio(min.Precio - margen)}",
				({ } max, null) => $"Cierre 1H > {precio(max.Precio + margen)}",
				(null, { } min) => $"Cierre 1H < {precio(min.Precio - margen)}",
				_ => "Faltan pivotes confirmados de 1H"
			},
			_ => "Sin swing protegido identificado"
		};
	}
}

/// <summary>EMA del cierre, con historial por inicio de vela para dibujarla.</summary>
internal sealed class Ema
{
	private readonly int _periodo;
	private int _cuenta;
	private decimal _suma;

	public Ema(int periodo) => _periodo = Math.Max(1, periodo);

	public decimal? Valor { get; private set; }

	public Dictionary<DateTime, decimal> PorInicio { get; } = new();

	public void Agregar(Vela vela)
	{
		_cuenta++;

		if (_cuenta <= _periodo)
		{
			_suma += vela.Cierre;

			if (_cuenta == _periodo)
				Valor = _suma / _periodo;
		}
		else
		{
			Valor += (vela.Cierre - Valor!.Value) * 2m / (_periodo + 1);
		}

		if (Valor is { } valor)
			PorInicio[vela.InicioUtc] = valor;
	}
}
