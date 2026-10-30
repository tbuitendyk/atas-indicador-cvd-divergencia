namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

internal enum EstadoReferencia
{
	Activa,
	Barrida,
	Rota,
	Retirada
}

internal enum OrigenReferencia
{
	Tokio,
	Londres,
	NuevaYork,
	SemanaAnterior,
	Swing1H,
	Pivote1M,
	Pivote5M
}

/// <summary>Nivel de liquidez identificado: máximo o mínimo de una sesión, semana, swing o pivote.</summary>
internal sealed class ReferenciaLiquidez
{
	public int Id;
	public TipoLiquidez Tipo;
	public OrigenReferencia Origen;
	public bool EsMaximo;
	public decimal Precio;
	public DateTime DisponibleUtc;
	public string Etiqueta = "";

	/// <summary>Rango interno al que pertenece (solo liquidez interna).</summary>
	public string? Rango;

	public EstadoReferencia Estado = EstadoReferencia.Activa;
	public DateTime? FinUtc;
	public string? MotivoFin;

	/// <summary>Prioridad para elegir la referencia principal de un barrido: externa antes que interna.</summary>
	public int Importancia => Tipo == TipoLiquidez.Externa ? (Origen == OrigenReferencia.SemanaAnterior ? 3 : 2) : 1;
}

/// <summary>
/// Mantiene las referencias de liquidez. Externa: máximos y mínimos de sesiones completas de Tokio, Londres y
/// Nueva York, de la semana anterior y swings confirmados de 1H. Interna: pivotes confirmados de 1M y 5M dentro
/// del rango interno de 1H (entre el swing protegido y el extremo a favor); sin rango identificado no se crean.
/// </summary>
internal sealed class GestorLiquidez
{
	private sealed class Acumulado
	{
		public VentanaSesion Ventana;
		public decimal Maximo, Minimo;
		public bool Completa;
	}

	private readonly ConfiguracionCompleto _cfg;
	private readonly Calendario _calendario;
	private readonly Dictionary<TipoSesion, Acumulado> _sesiones = new();
	private DateTime? _semana;
	private bool _semanaCompleta;
	private decimal _maxSemana, _minSemana;
	private int _siguienteId = 1;

	public GestorLiquidez(ConfiguracionCompleto cfg, Calendario calendario)
	{
		_cfg = cfg;
		_calendario = calendario;
	}

	public List<ReferenciaLiquidez> Referencias { get; } = new();

	public int PivotesSinRango { get; private set; }

	public int SesionesIncompletas { get; private set; }

	public IEnumerable<ReferenciaLiquidez> Activas(DateTime instanteUtc) =>
		Referencias.Where(r => r.Estado == EstadoReferencia.Activa && r.DisponibleUtc <= instanteUtc);

	/// <summary>Acumula sesiones y semana con una vela de 1M cerrada; publica las referencias al completarse.</summary>
	public void ProcesarVela1M(Vela vela)
	{
		foreach (var tipo in new[] { TipoSesion.Tokio, TipoSesion.Londres, TipoSesion.NuevaYork })
		{
			if (_sesiones.TryGetValue(tipo, out var abierta) && vela.InicioUtc >= abierta.Ventana.FinUtc)
				CerrarSesion(tipo, abierta);

			if (_calendario.Ventana(tipo, vela.InicioUtc) is { } ventana)
			{
				if (!_sesiones.TryGetValue(tipo, out var acumulado))
				{
					acumulado = new Acumulado
					{
						Ventana = ventana,
						Maximo = vela.Maximo,
						Minimo = vela.Minimo,
						Completa = vela.InicioUtc < ventana.InicioUtc.AddMinutes(1)
					};
					_sesiones[tipo] = acumulado;
				}

				acumulado.Maximo = Math.Max(acumulado.Maximo, vela.Maximo);
				acumulado.Minimo = Math.Min(acumulado.Minimo, vela.Minimo);

				if (vela.FinUtc >= ventana.FinUtc)
					CerrarSesion(tipo, acumulado);
			}
		}

		var semana = Calendario.SemanaGlobex(vela.InicioUtc);

		if (_semana != semana)
		{
			if (_semana is { } anterior && _semanaCompleta)
			{
				var etiqueta = $"semana del {anterior:dd/MM}";
				Agregar(TipoLiquidez.Externa, OrigenReferencia.SemanaAnterior, true, _maxSemana, vela.InicioUtc, $"Máximo de la {etiqueta}", null);
				Agregar(TipoLiquidez.Externa, OrigenReferencia.SemanaAnterior, false, _minSemana, vela.InicioUtc, $"Mínimo de la {etiqueta}", null);
			}

			// La primera semana cargada suele empezar a medias: no se publica.
			_semanaCompleta = _semana is not null;
			_semana = semana;
			_maxSemana = vela.Maximo;
			_minSemana = vela.Minimo;
		}
		else
		{
			_maxSemana = Math.Max(_maxSemana, vela.Maximo);
			_minSemana = Math.Min(_minSemana, vela.Minimo);
		}
	}

	private void CerrarSesion(TipoSesion tipo, Acumulado acumulado)
	{
		_sesiones.Remove(tipo);

		if (!acumulado.Completa)
		{
			SesionesIncompletas++;
			return;
		}

		var origen = tipo switch
		{
			TipoSesion.Tokio => OrigenReferencia.Tokio,
			TipoSesion.Londres => OrigenReferencia.Londres,
			_ => OrigenReferencia.NuevaYork
		};

		var nombre = $"de {Calendario.Nombre(tipo)} {acumulado.Ventana.FechaLocal:dd/MM}";
		Agregar(TipoLiquidez.Externa, origen, true, acumulado.Maximo, acumulado.Ventana.FinUtc, $"Máximo {nombre}", null);
		Agregar(TipoLiquidez.Externa, origen, false, acumulado.Minimo, acumulado.Ventana.FinUtc, $"Mínimo {nombre}", null);
	}

	public void AgregarSwing1H(Pivote pivote) =>
		Agregar(TipoLiquidez.Externa, OrigenReferencia.Swing1H, pivote.EsMaximo, pivote.Precio, pivote.ConfirmadoUtc,
			$"{(pivote.EsMaximo ? "Máximo" : "Mínimo")} swing 1H {pivote.ExtremoUtc:dd/MM HH:mm} UTC", null);

	/// <summary>Pivote de 1M o 5M: solo es liquidez interna si está dentro del rango interno de 1H.</summary>
	public void AgregarPivoteInterno(Pivote pivote, (decimal Inferior, decimal Superior)? rango, Func<decimal, string> precio)
	{
		if (rango is not { } r || pivote.Precio <= r.Inferior || pivote.Precio >= r.Superior)
		{
			PivotesSinRango++;
			return;
		}

		var origen = pivote.Marco == Marco.M5 ? OrigenReferencia.Pivote5M : OrigenReferencia.Pivote1M;
		Agregar(TipoLiquidez.Interna, origen, pivote.EsMaximo, pivote.Precio, pivote.ConfirmadoUtc,
			$"{(pivote.EsMaximo ? "Máximo" : "Mínimo")} pivote {pivote.Marco.Texto()} {pivote.ExtremoUtc:HH:mm} UTC",
			$"rango 1H {precio(r.Inferior)}–{precio(r.Superior)}");
	}

	private void Agregar(TipoLiquidez tipo, OrigenReferencia origen, bool esMaximo, decimal precio, DateTime disponible, string etiqueta, string? rango)
	{
		Referencias.Add(new ReferenciaLiquidez
		{
			Id = _siguienteId++,
			Tipo = tipo,
			Origen = origen,
			EsMaximo = esMaximo,
			Precio = precio,
			DisponibleUtc = disponible,
			Etiqueta = etiqueta,
			Rango = rango
		});

		// Se conserva un número limitado de referencias vivas por origen y lado: las más recientes.
		var limite = origen switch
		{
			OrigenReferencia.SemanaAnterior => 1,
			OrigenReferencia.Swing1H => 6,
			OrigenReferencia.Pivote1M or OrigenReferencia.Pivote5M => 8,
			_ => 2
		};

		var vivas = Referencias.Where(r => r.Origen == origen && r.EsMaximo == esMaximo && r.Estado == EstadoReferencia.Activa).ToList();

		for (var i = 0; i < vivas.Count - limite; i++)
		{
			vivas[i].Estado = EstadoReferencia.Retirada;
			vivas[i].FinUtc = disponible;
			vivas[i].MotivoFin = "sustituida por una más reciente";
		}

		// Historial acotado: las referencias terminadas más antiguas se descartan.
		if (Referencias.Count > 600)
			Referencias.RemoveAll(r => r.Estado != EstadoReferencia.Activa && r.FinUtc < disponible.AddDays(-5));
	}
}

internal enum EstadoBarrido
{
	EnEvaluacion,
	Recuperado,
	Bloqueado,
	Invalidado,
	Vencido,
	Consumido
}

/// <summary>Absorción en el barrido: volumen climático, agresión contraria, escaso avance y cambio de delta.</summary>
internal sealed class Absorcion
{
	public bool? VolumenClimatico;
	public bool? AgresionContraria;
	public bool? EscasoAvance;
	public bool? CambioDelta;
	public int VelasRevisadas;

	public int Cumplidas => new[] { VolumenClimatico, AgresionContraria, EscasoAvance, CambioDelta }.Count(c => c == true);

	public bool Pendiente => CambioDelta is null;

	public bool Confirmada => Cumplidas == 4;

	public string Texto() => Pendiente
		? $"EN EVALUACIÓN ({Cumplidas}/4, falta cambio de delta)"
		: Confirmada ? "CONFIRMADA (4/4)" : $"NO CONFIRMADA ({Cumplidas}/4)";

	public string Detalle() =>
		$"volumen climático {Si(VolumenClimatico)} · agresión contraria {Si(AgresionContraria)} · escaso avance {Si(EscasoAvance)} · cambio de delta {Si(CambioDelta)}";

	private static string Si(bool? valor) => valor switch
	{
		true => "sí",
		false => "no",
		_ => "pendiente"
	};
}

/// <summary>Barrido de liquidez: una vela cruza por primera vez una referencia intacta al menos la penetración mínima.</summary>
internal sealed class EventoBarrido
{
	public int Id;

	/// <summary>Larga: barrido de mínimos (busca compras). Corta: barrido de máximos.</summary>
	public Direccion Direccion;

	public List<ReferenciaLiquidez> Referencias = new();
	public ReferenciaLiquidez Principal = null!;
	public decimal Extremo;
	public int PenetracionTicks;
	public Vela VelaBarrido = null!;
	public DateTime? Recuperacion1MUtc;
	public DateTime? Recuperacion5MUtc;
	public DateTime? RecuperadoUtc;
	public Vela? VelaRecuperacion;
	public EstadoBarrido Estado;
	public string Motivo = "";
	public Absorcion Absorcion = new();
	public DateTime VenceRecuperacionUtc;
	public DateTime? VenceOportunidadUtc;
	public bool RechazoFuerte;

	public TipoLiquidez Tipo => Principal.Tipo;

	public decimal Nivel => Principal.Precio;

	public DateTime ConocidoUtc => VelaBarrido.FinUtc;

	public bool Vivo => Estado is EstadoBarrido.EnEvaluacion or EstadoBarrido.Recuperado;
}
