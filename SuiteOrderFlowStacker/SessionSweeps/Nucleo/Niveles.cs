namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Nivel de liquidez: máximo (BSL) o mínimo (SSL) de una sesión, de un día, de la semana o de pivotes iguales.</summary>
public sealed class NivelLiquidez
{
	public int Id;
	public decimal Precio;
	public Lado Lado;

	/// <summary>"Asia", "Londres", "Premercado", "NY AM", "NY PM", "Dia anterior", "Dia -2", "Semana anterior"…</summary>
	public string Origen = "";

	/// <summary>Grupo para activarlo o desactivarlo: "sesion:Asia", "dia", "semana", "igual", "pine", "pine-londres".</summary>
	public string Grupo = "";

	public DateTime CreadoUtc;
	public int CreadoBar;
	public EstadoNivel Estado = EstadoNivel.Activo;

	/// <summary>Barra en la que dejó de vigilarse (barrido, roto o retirado).</summary>
	public int? FinBar;

	/// <summary>Antigüedad en días operativos (niveles diarios): 1 = día anterior.</summary>
	public int Dias;

	/// <summary>Sesión de la que sale (para cortar sus líneas al barrerse).</summary>
	public InstanciaSesion? Sesion;

	// Estado del Pine: bsB/ssB (ya marcado) y la zona en curso (bzB).
	public bool Marcado;
	public ZonaBarrido? Zona;

	public string Nombre => $"{Lado.Extremo()} {Origen}";

	public bool Activo => Estado == EstadoNivel.Activo;
}

/// <summary>Crea, envejece y retira los niveles de liquidez.</summary>
public sealed class GestorNiveles
{
	private readonly ConfiguracionSweeps _cfg;
	private int _siguiente = 1;
	private DateTime? _dia, _semana;
	private decimal _altoDia, _bajoDia, _altoSemana, _bajoSemana;

	public GestorNiveles(ConfiguracionSweeps cfg) => _cfg = cfg;

	public List<NivelLiquidez> Niveles { get; } = new();

	/// <summary>Rangos de los días operativos completos (para el ADR).</summary>
	public List<(DateTime Dia, decimal Alto, decimal Bajo)> Dias { get; } = new();

	public (decimal Alto, decimal Bajo)? DiaActual => _dia is null ? null : (_altoDia, _bajoDia);

	public IEnumerable<NivelLiquidez> Activos => Niveles.Where(n => n.Activo);

	/// <summary>¿Se vigila este nivel con los ajustes actuales?</summary>
	public bool Vigilado(NivelLiquidez n)
	{
		if (!n.Activo)
			return false;

		// PineCompatibilityMode: solo los pares del Pine (última sesión cerrada y Londres durante NY AM).
		if (_cfg.PineCompatibilityMode)
			return n.Grupo.StartsWith("pine");

		return n.Grupo switch
		{
			"dia" => n.Dias == 1 ? _cfg.NivelesPdhPdl : _cfg.NivelesDiasAnteriores && n.Dias <= _cfg.DiasAnteriores,
			"semana" => _cfg.NivelesSemanaAnterior,
			"igual" => _cfg.NivelesIguales,
			"pine" or "pine-londres" => true,
			_ => _cfg.NivelesSesion
		};
	}

	private NivelLiquidez Crear(decimal precio, Lado lado, string origen, string grupo, Vela v, InstanciaSesion? sesion = null)
	{
		var n = new NivelLiquidez
		{
			Id = _siguiente++,
			Precio = precio,
			Lado = lado,
			Origen = origen,
			Grupo = grupo,
			CreadoUtc = v.FinUtc,
			CreadoBar = v.Indice,
			Sesion = sesion
		};

		Niveles.Add(n);

		// Límite como el Pine: los más antiguos que ya no se vigilan se borran.
		while (Niveles.Count > _cfg.MaxLines)
		{
			var viejo = Niveles.FindIndex(x => !x.Activo);

			if (viejo < 0)
				break;

			Niveles.RemoveAt(viejo);
		}

		return n;
	}

	public void Retirar(NivelLiquidez n, int bar)
	{
		if (!n.Activo)
			return;

		n.Estado = EstadoNivel.Retirado;
		n.FinBar = bar;
	}

	// --- Sesiones -------------------------------------------------------------------------------------------------

	/// <summary>Al empezar otra vez una sesión, los niveles de su vez anterior se retiran (como sus líneas en el Pine).</summary>
	public void AlEmpezarSesion(string id, int bar)
	{
		if (_cfg.PineCompatibilityMode)
			return;

		foreach (var n in Niveles.Where(n => n.Grupo == "sesion:" + id && n.Activo).ToList())
			Retirar(n, bar - 1);
	}

	/// <summary>
	/// Al terminar una sesión: su máximo (BSL) y su mínimo (SSL). En PineCompatibilityMode, como el Pine: un solo par,
	/// el de la última sesión cerrada, más el par de Londres mientras dura NY AM (se borra al terminar NY PM).
	/// </summary>
	public void AlTerminarSesion(InstanciaSesion s, Vela v)
	{
		if (!_cfg.PineCompatibilityMode)
		{
			Crear(s.Alto, Lado.Buyside, s.Nombre, "sesion:" + s.Id, v, s);
			Crear(s.Bajo, Lado.Sellside, s.Nombre, "sesion:" + s.Id, v, s);
			return;
		}

		foreach (var n in Niveles.Where(n => n.Grupo == "pine" && n.Activo).ToList())
			Retirar(n, v.Indice - 1);

		Crear(s.Alto, Lado.Buyside, s.Nombre, "pine", v, s);
		Crear(s.Bajo, Lado.Sellside, s.Nombre, "pine", v, s);

		if (s.Id == ConfiguracionSweeps.Londres && _cfg.Sesion(ConfiguracionSweeps.NyAm).Activa)
		{
			foreach (var n in Niveles.Where(n => n.Grupo == "pine-londres" && n.Activo).ToList())
				Retirar(n, v.Indice - 1);

			Crear(s.Alto, Lado.Buyside, s.Nombre, "pine-londres", v, s);
			Crear(s.Bajo, Lado.Sellside, s.Nombre, "pine-londres", v, s);
		}
		else if (s.Id == ConfiguracionSweeps.NyPm)
		{
			foreach (var n in Niveles.Where(n => n.Grupo == "pine-londres" && n.Activo).ToList())
				Retirar(n, v.Indice - 1);
		}
	}

	// --- Días y semanas -------------------------------------------------------------------------------------------

	/// <summary>Sigue el máximo y el mínimo del día operativo (18:00-17:00 ET) y de la semana; al cambiar, crea sus niveles.</summary>
	public void Procesar(Vela v)
	{
		var dia = Zonas.DiaOperativo(v.InicioUtc);
		var semana = Zonas.SemanaOperativa(v.InicioUtc);

		if (_dia is { } anterior && anterior != dia)
		{
			Dias.Add((anterior, _altoDia, _bajoDia));

			if (Dias.Count > 60)
				Dias.RemoveAt(0);

			// Los días anteriores envejecen; pasado N, dejan de vigilarse.
			foreach (var n in Niveles.Where(n => n.Grupo == "dia" && n.Activo))
			{
				n.Dias++;
				n.Origen = $"Dia -{n.Dias}";

				if (n.Dias > Math.Max(1, _cfg.DiasAnteriores))
					Retirar(n, v.Indice - 1);
			}

			Crear(_altoDia, Lado.Buyside, "Dia anterior", "dia", v).Dias = 1;
			Crear(_bajoDia, Lado.Sellside, "Dia anterior", "dia", v).Dias = 1;
		}

		if (_semana is { } semanaAnterior && semanaAnterior != semana)
		{
			foreach (var n in Niveles.Where(n => n.Grupo == "semana" && n.Activo).ToList())
				Retirar(n, v.Indice - 1);

			Crear(_altoSemana, Lado.Buyside, "Semana anterior", "semana", v);
			Crear(_bajoSemana, Lado.Sellside, "Semana anterior", "semana", v);
		}

		(_altoDia, _bajoDia) = _dia == dia ? (Math.Max(_altoDia, v.High), Math.Min(_bajoDia, v.Low)) : (v.High, v.Low);
		(_altoSemana, _bajoSemana) = _semana == semana ? (Math.Max(_altoSemana, v.High), Math.Min(_bajoSemana, v.Low)) : (v.High, v.Low);
		_dia = dia;
		_semana = semana;
	}

	/// <summary>Los niveles que no se vigilan (desactivados) y que el precio supera quedan rotos: así no reaparecen.</summary>
	public void RomperNoVigilados(Vela v)
	{
		foreach (var n in Niveles.Where(n => n.Activo && !Vigilado(n)))
		{
			if (n.Lado == Lado.Buyside ? v.High > n.Precio : v.Low < n.Precio)
			{
				n.Estado = EstadoNivel.Roto;
				n.FinBar = v.Indice;
			}
		}
	}

	// --- Máximos y mínimos iguales --------------------------------------------------------------------------------

	private readonly List<(int Bar, decimal Precio)> _pivotesAltos = new(), _pivotesBajos = new();

	/// <summary>Dos pivotes a EqualTolTicks o menos (sin que el precio los haya superado entre medias) forman un nivel.</summary>
	public void Pivotes(List<Vela> velas)
	{
		var p = Math.Max(1, _cfg.EqualPivotLen);
		var i = velas.Count - 1 - p;

		if (i < p)
			return;

		var tol = _cfg.Ticks(_cfg.EqualTolTicks);
		var v = velas[i];
		var esAlto = true;
		var esBajo = true;

		for (var k = i - p; k <= i + p; k++)
		{
			if (k == i)
				continue;

			esAlto &= velas[k].High < v.High;
			esBajo &= velas[k].Low > v.Low;
		}

		if (esAlto)
			Igualar(_pivotesAltos, i, v.High, Lado.Buyside, velas, tol);

		if (esBajo)
			Igualar(_pivotesBajos, i, v.Low, Lado.Sellside, velas, tol);
	}

	private void Igualar(List<(int Bar, decimal Precio)> pivotes, int bar, decimal precio, Lado lado, List<Vela> velas, decimal tol)
	{
		foreach (var (barPrevio, previo) in pivotes.AsEnumerable().Reverse())
		{
			if (Math.Abs(previo - precio) > tol)
				continue;

			var nivel = lado == Lado.Buyside ? Math.Max(previo, precio) : Math.Min(previo, precio);
			var intacto = Enumerable.Range(barPrevio + 1, bar - barPrevio - 1)
				.All(k => lado == Lado.Buyside ? velas[k].High <= nivel : velas[k].Low >= nivel);

			if (intacto && !Niveles.Any(n => n.Grupo == "igual" && n.Activo && n.Lado == lado && Math.Abs(n.Precio - nivel) <= tol))
				Crear(nivel, lado, lado == Lado.Buyside ? "Equal highs" : "Equal lows", "igual", velas[^1]);

			break;
		}

		pivotes.Add((bar, precio));

		if (pivotes.Count > 50)
			pivotes.RemoveAt(0);
	}
}
