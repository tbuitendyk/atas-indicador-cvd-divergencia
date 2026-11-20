namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Los recuadros que el usuario dibuja en el Sweep Pro (FAST) (v5).</summary>
public enum TipoRecuadro
{
	BarridoReal,
	BarridoPrime,
	Rompimiento,
	Compuesta
}

/// <summary>
/// Un recuadro manual: un barrido (REAL o PRIME) o un rompimiento marcado a mano, o el rango de una vela compuesta. Los
/// de barrido y rompimiento quedan como una evaluación MANUAL que arranca la verificación de los modelos; el de vela
/// compuesta vigila la primera ruptura de su rango (flecha verde alcista, roja bajista).
/// </summary>
public sealed class RecuadroManual
{
	public long Id;
	public TipoRecuadro Tipo;
	public DateTime DesdeUtc, HastaUtc, CreadoUtc;
	public decimal Bajo, Alto;

	/// <summary>Barrido: +1 si se barrió un máximo (giro bajista); rompimiento: dirección de la ruptura.</summary>
	public int Signo;

	public long? EvaluacionId;
	public DateTime? RompeUtc;
	public int RompeDireccion;
	public decimal RompePrecio;

	public string Texto => Tipo switch
	{
		TipoRecuadro.BarridoReal => "BARRIDO REAL",
		TipoRecuadro.BarridoPrime => "BARRIDO REAL PRIME",
		TipoRecuadro.Rompimiento => "ROMPIMIENTO",
		_ => "RANGO VELA COMPUESTA"
	};

	/// <summary>"Tipo|desde|hasta|creado|bajo|alto|signo" (ticks UTC y precios invariantes); varios separados por ';'.</summary>
	public string Serializar() => string.Join("|", Tipo, DesdeUtc.Ticks, HastaUtc.Ticks, CreadoUtc.Ticks,
		Bajo.ToString(CultureInfo.InvariantCulture), Alto.ToString(CultureInfo.InvariantCulture), Signo.ToString(CultureInfo.InvariantCulture));

	public static string Escribir(IEnumerable<RecuadroManual> recuadros) => string.Join(";", recuadros.Select(r => r.Serializar()));

	public static List<RecuadroManual> Leer(string? texto)
	{
		var r = new List<RecuadroManual>();

		foreach (var parte in (texto ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			var c = parte.Split('|');

			if (c.Length < 6 || !Enum.TryParse<TipoRecuadro>(c[0], out var tipo) || !long.TryParse(c[1], out var d) || !long.TryParse(c[2], out var h) ||
			    !long.TryParse(c[3], out var k) || !decimal.TryParse(c[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var bajo) ||
			    !decimal.TryParse(c[5], NumberStyles.Number, CultureInfo.InvariantCulture, out var alto))
			{
				continue;
			}

			r.Add(new RecuadroManual
			{
				Tipo = tipo, DesdeUtc = new DateTime(d, DateTimeKind.Utc), HastaUtc = new DateTime(h, DateTimeKind.Utc), CreadoUtc = new DateTime(k, DateTimeKind.Utc),
				Bajo = bajo, Alto = alto, Signo = c.Length > 6 && int.TryParse(c[6], out var signo) ? signo : 0
			});
		}

		return r;
	}
}

public sealed partial class MotorSweeps
{
	private long _siguienteRecuadro = 1;

	public List<RecuadroManual> Recuadros { get; } = new();

	/// <summary>
	/// Marca un recuadro. Barrido REAL / PRIME: la dirección sale del precio actual (por debajo de la mitad = se barrió un
	/// máximo); queda una evaluación MANUAL congelada con ese estado que arma los modelos (M1; rompimiento: M2 tras
	/// rompimiento) como un barrido detectado. Vela compuesta: el rango es el máximo y el mínimo de las velas del recuadro.
	/// </summary>
	public RecuadroManual Marcar(TipoRecuadro tipo, DateTime desde, DateTime hasta, decimal bajo, decimal alto, DateTime creado, int signo = 0)
	{
		lock (_sync)
		{
			if (hasta < desde)
				(desde, hasta) = (hasta, desde);

			if (alto < bajo)
				(bajo, alto) = (alto, bajo);

			var r = new RecuadroManual { Id = _siguienteRecuadro++, Tipo = tipo, DesdeUtc = desde, HastaUtc = hasta, CreadoUtc = creado, Bajo = bajo, Alto = alto };

			if (tipo == TipoRecuadro.Compuesta)
			{
				var dentro = Velas.Where(v => v.InicioUtc >= desde && v.InicioUtc <= hasta).ToList();

				if (dentro.Count > 0)
					(r.Bajo, r.Alto) = (dentro.Min(v => v.Low), dentro.Max(v => v.High));

				// Al restaurar (recarga) la ruptura pudo ocurrir ya: se busca en las velas posteriores.
				foreach (var v in Velas.Where(v => v.InicioUtc > hasta))
				{
					if (RevisarRuptura(r, v.High, v.Low, v.InicioUtc))
						break;
				}
			}
			else
			{
				// La dirección se decide al marcarlo (con el precio de ese momento) y se guarda: al recargar no cambia.
				var precio = PrecioActual;
				var medio = (r.Bajo + r.Alto) / 2;
				r.Signo = signo != 0 ? Math.Sign(signo) : tipo == TipoRecuadro.Rompimiento ? (precio >= medio ? 1 : -1) : (precio <= medio ? 1 : -1);
				var e = EvaluacionManual(r);
				r.EvaluacionId = e.Id;
			}

			Recuadros.Add(r);
			return r;
		}
	}

	/// <summary>
	/// Al recargar el gráfico vuelven los recuadros guardados (con su dirección de entonces) sin novedades: no se graban
	/// otra vez en el CSV ni suenan.
	/// </summary>
	public void Restaurar(IEnumerable<RecuadroManual> guardados)
	{
		lock (_sync)
		{
			var antes = NovedadesV2.Count;

			foreach (var r in guardados)
				Marcar(r.Tipo, r.DesdeUtc, r.HastaUtc, r.Bajo, r.Alto, r.CreadoUtc, r.Signo);

			NovedadesV2.RemoveRange(antes, NovedadesV2.Count - antes);
		}
	}

	private EvaluacionBarrido EvaluacionManual(RecuadroManual r)
	{
		var s = r.Signo;
		var (l, extremo) = s > 0 ? (r.Bajo, r.Alto) : (r.Alto, r.Bajo);
		var estado = r.Tipo switch
		{
			TipoRecuadro.BarridoPrime => EstadoBarrido.Premium,
			TipoRecuadro.Rompimiento => EstadoBarrido.Ruptura,
			_ => EstadoBarrido.Real
		};
		var barra = Math.Max(0, BarrasV.BarraEn(r.DesdeUtc));
		var e = new EvaluacionBarrido
		{
			Id = -r.Id,
			Nivel = new NivelLiquidez { Id = -(int)r.Id, Precio = l, Lado = s > 0 ? Lado.Buyside : Lado.Sellside, Grupo = "manual", Origen = "MANUAL", CreadoUtc = r.DesdeUtc },
			Signo = s, L = l, Extremo = extremo, ExtremoPierna = extremo, T0Utc = r.DesdeUtc, ExtremoUtc = r.HastaUtc, BarraT0 = barra, BarraExtremo = barra,
			Estado = estado, Congelada = true, TimeConfirmUtc = r.CreadoUtc, Origen = "MANUAL", BarraConfirmacion = Math.Max(0, BarrasV.Barras.Count - 1)
		};
		e.Desglose.Add(new PuntoDesglose('M', $"Marcado a mano: {r.Texto}", 0));
		e.Transiciones.Add($"{TiempoV2.HoraEt(r.CreadoUtc)} {r.Texto} (MANUAL)");
		Evaluaciones.Add(e);
		Novedad(e, "CONFIRMADO", r.CreadoUtc, $"MANUAL: {r.Texto} {e.Nombre} {Precio(l)}");
		ModelosAlEstado(e, "CONFIRMADO", r.CreadoUtc);
		return e;
	}

	/// <summary>Borra los recuadros: los modelos que dependían de un barrido manual quedan invalidados.</summary>
	public void QuitarRecuadros()
	{
		lock (_sync)
		{
			Invalidar(s => s.Barrido?.Origen == "MANUAL", "recuadro manual borrado", Ahora);
			Evaluaciones.RemoveAll(e => e.Origen == "MANUAL");
			Recuadros.Clear();
		}
	}

	/// <summary>Primera ruptura del rango de una vela compuesta: flecha verde (alcista) o roja (bajista).</summary>
	private bool RevisarRuptura(RecuadroManual r, decimal alto, decimal bajo, DateTime utc)
	{
		if (r.Tipo != TipoRecuadro.Compuesta || r.RompeUtc is not null || utc <= r.HastaUtc)
			return false;

		var direccion = alto > r.Alto ? 1 : bajo < r.Bajo ? -1 : 0;

		if (direccion == 0)
			return false;

		(r.RompeUtc, r.RompeDireccion, r.RompePrecio) = (utc, direccion, direccion > 0 ? r.Alto : r.Bajo);
		Novedad(null, "RUPTURA RANGO COMPUESTO", utc, $"Rompe el rango de la vela compuesta {Precio(r.Bajo)}-{Precio(r.Alto)}: {(direccion > 0 ? "ALCISTA" : "BAJISTA")}");
		return true;
	}

	/// <summary>Cada trade (TickV2): rupturas de los rangos de vela compuesta.</summary>
	private void TickRecuadros(DateTime utc, decimal precio)
	{
		foreach (var r in Recuadros)
			RevisarRuptura(r, precio, precio, utc);
	}
}
