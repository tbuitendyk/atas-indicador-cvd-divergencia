namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Volumen real por precio de una sesión: un día de Globex o la sesión regular de Nueva York.</summary>
internal sealed class PerfilSesion
{
	public DateTime Dia;
	public bool EsNuevaYork;
	public readonly Dictionary<long, decimal> Volumen = new();
	public int Velas;

	/// <summary>Velas sin volumen por precio: su volumen no está en el perfil (no se estima).</summary>
	public int VelasSinHuella;

	public bool EmpezoCompleta;
	public bool Cerrado;
}

/// <summary>Zona LVN de un mapa: valle de volumen agrupado, con su evidencia y su clasificación.</summary>
internal sealed class ZonaLvn
{
	public int Id;
	public decimal Inferior, Superior, Centro;

	/// <summary>1 - (volumen del valle / hombro más bajo): 1 = vacío total.</summary>
	public decimal Profundidad;

	public decimal Volumen;
	public decimal HombroInferior, HombroSuperior;
	public List<string> Origenes = new();
	public DateTime DisponibleUtc;
	public DateTime ReconocidaUtc;
	public int Reacciones, ReaccionesSoporte, ReaccionesResistencia, SesionesConReaccion;
	public decimal Puntuacion;
	public int Puesto;
	public PrioridadLvn Prioridad = PrioridadLvn.Menor;

	public bool Bipolar => ReaccionesSoporte > 0 && ReaccionesResistencia > 0;

	public string Etiqueta => $"LVN {Id} · {Prioridad.Texto()} · {Puntuacion:0}";
}

/// <summary>Mapa LVN de una sesión: se crea en la apertura de NY y queda congelado hasta el siguiente.</summary>
internal sealed class MapaLvn
{
	public int Version;
	public DateTime CreadoUtc;
	public DateTime? FinUtc;
	public decimal Referencia;
	public decimal BandaInferior, BandaSuperior;
	public List<ZonaLvn> Zonas = new();

	/// <summary>Perfiles de origen dentro de la banda, para dibujarlos (solo se conservan en el mapa vigente).</summary>
	public Dictionary<PerfilVisible, SortedDictionary<long, decimal>> Perfiles = new();

	public int SesionesValidas;
	public int VelasSinHuella;

	public IEnumerable<ZonaLvn> Visibles(bool conMenores) => Zonas.Where(z => conMenores || z.Prioridad != PrioridadLvn.Menor);
}

/// <summary>
/// Construye los mapas LVN y acumula la evidencia de reacciones. La identidad de cada zona se conserva entre mapas
/// por solapamiento, así la evidencia no se pierde si sus límites cambian ligeramente.
/// </summary>
internal sealed class MotorLvn
{
	private sealed class Registro
	{
		public int Id;
		public long Inferior, Superior;
		public DateTime ReconocidaUtc;
		public DateTime UltimoMapaUtc;
		public int Soporte, Resistencia;
		public readonly HashSet<DateTime> Dias = new();
		public Pendiente? Pendiente;

		public int Reacciones => Soporte + Resistencia;
	}

	private sealed class Pendiente
	{
		public bool Soporte;
		public decimal Borde;
		public DateTime VenceUtc;
	}

	private sealed class Candidata
	{
		public long Inferior, Superior, Centro;
		public decimal Profundidad, Volumen;
		public long HombroInferior, HombroSuperior;
		public readonly List<string> Origenes = new();
	}

	private const int SesionesConservadas = 70;

	private readonly ConfiguracionCompleto _cfg;
	private readonly List<PerfilSesion> _dias = new();
	private readonly List<PerfilSesion> _sesionesNy = new();
	private readonly List<Registro> _registro = new();
	private int _siguienteId = 1;

	public MotorLvn(ConfiguracionCompleto cfg) => _cfg = cfg;

	public List<MapaLvn> Mapas { get; } = new();

	public MapaLvn? Vigente => Mapas.Count > 0 ? Mapas[^1] : null;

	public int DiasCompletos => _dias.Count(d => d.Cerrado && d.EmpezoCompleta);

	/// <summary>Suma una vela de 1M a los perfiles de su día de Globex y, si está en la sesión regular, de NY.</summary>
	public void AgregarVela(Vela vela, DateTime diaGlobex, VentanaSesion? ventanaNy)
	{
		var dia = Perfil(_dias, diaGlobex, false, vela);
		Sumar(dia, vela);

		if (ventanaNy is { } ny)
		{
			var sesion = Perfil(_sesionesNy, ny.FechaLocal, true, vela, ny.InicioUtc);
			Sumar(sesion, vela);

			if (vela.FinUtc >= ny.FinUtc)
				sesion.Cerrado = true;
		}
	}

	private static PerfilSesion Perfil(List<PerfilSesion> lista, DateTime dia, bool nuevaYork, Vela vela, DateTime? inicioVentana = null)
	{
		if (lista.Count > 0 && lista[^1].Dia == dia)
			return lista[^1];

		// Un perfil nuevo cierra el anterior.
		if (lista.Count > 0 && !nuevaYork)
			lista[^1].Cerrado = true;

		var perfil = new PerfilSesion
		{
			Dia = dia,
			EsNuevaYork = nuevaYork,
			// Completo si la primera vela es la del inicio de la sesión (no empezó a cargarse a medias).
			EmpezoCompleta = nuevaYork
				? vela.InicioUtc < inicioVentana!.Value.AddMinutes(1)
				: lista.Count > 0
		};

		lista.Add(perfil);

		if (lista.Count > SesionesConservadas)
			lista.RemoveAt(0);

		return perfil;
	}

	private static void Sumar(PerfilSesion perfil, Vela vela)
	{
		perfil.Velas++;

		if (!vela.FootprintCompleto || vela.Niveles is null || vela.Niveles.Count == 0)
		{
			perfil.VelasSinHuella++;
			return;
		}

		foreach (var (tick, nivel) in vela.Niveles)
			perfil.Volumen[tick] = perfil.Volumen.TryGetValue(tick, out var previo) ? previo + nivel.Volumen : nivel.Volumen;
	}

	/// <summary>Crea el mapa de la sesión con la apertura de NY como referencia. Usa solo sesiones completas ya cerradas.</summary>
	public MapaLvn? CrearMapa(DateTime ahoraUtc, decimal referencia)
	{
		var dias = _dias.Where(d => d.Cerrado && d.EmpezoCompleta).ToList();
		var ny = _sesionesNy.Where(s => s.Cerrado && s.EmpezoCompleta).ToList();

		if (dias.Count == 0 && ny.Count == 0)
			return null;

		var banda = _cfg.ATick(_cfg.BandaLvnPuntos);
		var centro = _cfg.ATick(referencia);
		var desde = centro - banda;
		var hasta = centro + banda;

		var compuestos = new List<(string Nombre, PerfilVisible? Visible, IReadOnlyList<PerfilSesion> Sesiones)>();

		if (ny.Count > 0)
			compuestos.Add(("última sesión NY", PerfilVisible.NyAnterior, ny.TakeLast(1).ToList()));

		foreach (var (n, visible) in new[] { (5, PerfilVisible.Sesiones5), (10, PerfilVisible.Sesiones10), (15, PerfilVisible.Sesiones15), (20, PerfilVisible.Sesiones20) })
		{
			if (dias.Count >= n)
				compuestos.Add(($"últimas {n} sesiones", visible, dias.TakeLast(n).ToList()));
		}

		// Bloques históricos anteriores de 20 sesiones.
		for (var inicioBloque = 20; inicioBloque + 20 <= dias.Count && inicioBloque <= 40; inicioBloque += 20)
		{
			var bloque = dias.Skip(dias.Count - inicioBloque - 20).Take(20).ToList();
			compuestos.Add(($"bloque de 20 sesiones previo ({bloque[0].Dia:dd/MM}–{bloque[^1].Dia:dd/MM})", null, bloque));
		}

		// Con menos de 5 sesiones completas se usa lo que hay, indicándolo.
		if (dias.Count is > 0 and < 5)
			compuestos.Add(($"{dias.Count} sesiones disponibles", PerfilVisible.Sesiones5, dias));

		var mapa = new MapaLvn
		{
			Version = Mapas.Count + 1,
			CreadoUtc = ahoraUtc,
			Referencia = referencia,
			BandaInferior = _cfg.APrecio(desde),
			BandaSuperior = _cfg.APrecio(hasta),
			SesionesValidas = dias.Count,
			VelasSinHuella = dias.TakeLast(20).Sum(d => d.VelasSinHuella)
		};

		var candidatas = new List<Candidata>();

		foreach (var (nombre, visible, sesiones) in compuestos)
		{
			var perfil = new SortedDictionary<long, decimal>();

			foreach (var sesion in sesiones)
			{
				foreach (var (tick, volumen) in sesion.Volumen)
				{
					if (tick >= desde && tick <= hasta)
						perfil[tick] = perfil.TryGetValue(tick, out var previo) ? previo + volumen : volumen;
				}
			}

			if (visible is { } v && !mapa.Perfiles.ContainsKey(v))
				mapa.Perfiles[v] = perfil;

			candidatas.AddRange(Valles(perfil, nombre));
		}

		var zonas = Agrupar(candidatas);
		Registrar(zonas, mapa, ahoraUtc);
		Clasificar(mapa);

		if (Vigente is { } anterior)
		{
			anterior.FinUtc = ahoraUtc;
			anterior.Perfiles.Clear();
		}

		Mapas.Add(mapa);

		if (Mapas.Count > 120)
			Mapas.RemoveAt(0);

		return mapa;
	}

	/// <summary>Valles de un perfil: precios cuyo volumen (suavizado) es como mucho UmbralValle del hombro más bajo.</summary>
	private List<Candidata> Valles(SortedDictionary<long, decimal> perfil, string origen)
	{
		var resultado = new List<Candidata>();

		if (perfil.Count == 0)
			return resultado;

		var primero = perfil.Keys.First();
		var ultimo = perfil.Keys.Last();
		var n = (int)(ultimo - primero + 1);
		var bruto = new decimal[n];

		foreach (var (tick, volumen) in perfil)
			bruto[tick - primero] = volumen;

		var r = Math.Max(0, _cfg.SuavizadoLvnTicks);
		var suave = new decimal[n];

		for (var i = 0; i < n; i++)
		{
			decimal suma = 0;
			var cuenta = 0;

			for (var j = Math.Max(0, i - r); j <= Math.Min(n - 1, i + r); j++)
			{
				suma += bruto[j];
				cuenta++;
			}

			suave[i] = suma / cuenta;
		}

		var w = Math.Max(2, _cfg.HombroLvnTicks);
		var valle = new bool[n];
		var proporcion = new decimal[n];
		var hombroIzq = new int[n];
		var hombroDer = new int[n];

		for (var i = w; i < n - w; i++)
		{
			int izq = i - 1, der = i + 1;

			for (var j = i - w; j < i; j++)
			{
				if (suave[j] > suave[izq])
					izq = j;
			}

			for (var j = i + 1; j <= i + w; j++)
			{
				if (suave[j] > suave[der])
					der = j;
			}

			var hombro = Math.Min(suave[izq], suave[der]);

			if (hombro <= 0)
				continue;

			proporcion[i] = suave[i] / hombro;
			valle[i] = proporcion[i] <= _cfg.UmbralValle;
			hombroIzq[i] = izq;
			hombroDer[i] = der;
		}

		for (var i = 0; i < n; i++)
		{
			if (!valle[i])
				continue;

			var fin = i;

			while (fin + 1 < n && valle[fin + 1])
				fin++;

			var minimo = i;

			for (var j = i; j <= fin; j++)
			{
				if (proporcion[j] < proporcion[minimo])
					minimo = j;
			}

			// Centro: el medio del fondo del valle (los precios con la proporción mínima, con tolerancia), no su primer precio.
			var desdeFondo = Enumerable.Range(i, fin - i + 1).First(j => proporcion[j] <= proporcion[minimo] + 0.01m);
			var hastaFondo = Enumerable.Range(i, fin - i + 1).Last(j => proporcion[j] <= proporcion[minimo] + 0.01m);

			var candidata = new Candidata
			{
				Inferior = primero + i,
				Superior = primero + fin,
				Centro = primero + (desdeFondo + hastaFondo) / 2,
				Profundidad = 1 - proporcion[minimo],
				Volumen = bruto[minimo],
				HombroInferior = primero + hombroIzq[minimo],
				HombroSuperior = primero + hombroDer[minimo]
			};

			candidata.Origenes.Add(origen);
			resultado.Add(candidata);
			i = fin;
		}

		return resultado;
	}

	/// <summary>Une los valles de distintos perfiles que se solapan y aplica la separación mínima entre zonas.</summary>
	private List<Candidata> Agrupar(List<Candidata> candidatas)
	{
		var unidas = new List<Candidata>();

		foreach (var c in candidatas.OrderBy(c => c.Inferior))
		{
			var solapada = unidas.FirstOrDefault(u => c.Inferior <= u.Superior + 2 && c.Superior >= u.Inferior - 2);

			if (solapada is null)
			{
				unidas.Add(c);
				continue;
			}

			foreach (var origen in c.Origenes.Where(o => !solapada.Origenes.Contains(o)))
				solapada.Origenes.Add(origen);

			// La zona representativa es la del valle más profundo.
			if (c.Profundidad > solapada.Profundidad)
			{
				(solapada.Inferior, solapada.Superior, solapada.Centro) = (c.Inferior, c.Superior, c.Centro);
				(solapada.Profundidad, solapada.Volumen) = (c.Profundidad, c.Volumen);
				(solapada.HombroInferior, solapada.HombroSuperior) = (c.HombroInferior, c.HombroSuperior);
			}
		}

		var separacion = _cfg.ATick(_cfg.SeparacionMinimaLvnPuntos);
		var aceptadas = new List<Candidata>();

		foreach (var c in unidas.OrderByDescending(c => c.Profundidad + 0.05m * c.Origenes.Count))
		{
			if (aceptadas.All(a => Math.Abs(a.Centro - c.Centro) >= separacion))
				aceptadas.Add(c);
		}

		return aceptadas;
	}

	private void Registrar(List<Candidata> candidatas, MapaLvn mapa, DateTime ahoraUtc)
	{
		var usados = new HashSet<Registro>();

		foreach (var c in candidatas)
		{
			// Misma zona que en un mapa anterior si se solapan o sus centros están a menos de 8 ticks;
			// cada zona anterior se asigna como mucho a una zona del mapa nuevo.
			var registro = _registro.FirstOrDefault(r => !usados.Contains(r) && c.Inferior <= r.Superior && c.Superior >= r.Inferior)
				?? _registro.FirstOrDefault(r => !usados.Contains(r) && Math.Abs((r.Inferior + r.Superior) / 2 - c.Centro) <= 8);

			if (registro is null)
			{
				registro = new Registro { Id = _siguienteId++, ReconocidaUtc = ahoraUtc };
				_registro.Add(registro);
			}

			usados.Add(registro);

			(registro.Inferior, registro.Superior, registro.UltimoMapaUtc) = (c.Inferior, c.Superior, ahoraUtc);

			mapa.Zonas.Add(new ZonaLvn
			{
				Id = registro.Id,
				Inferior = _cfg.APrecio(c.Inferior),
				Superior = _cfg.APrecio(c.Superior) + _cfg.TamanoTick,
				Centro = _cfg.APrecio(c.Centro),
				Profundidad = c.Profundidad,
				Volumen = c.Volumen,
				HombroInferior = _cfg.APrecio(c.HombroInferior),
				HombroSuperior = _cfg.APrecio(c.HombroSuperior),
				Origenes = c.Origenes.ToList(),
				DisponibleUtc = ahoraUtc,
				ReconocidaUtc = registro.ReconocidaUtc,
				Reacciones = registro.Reacciones,
				ReaccionesSoporte = registro.Soporte,
				ReaccionesResistencia = registro.Resistencia,
				SesionesConReaccion = registro.Dias.Count
			});
		}

		// Las zonas que ya no aparecen en los últimos mapas dejan de seguirse.
		_registro.RemoveAll(r => r.UltimoMapaUtc < ahoraUtc.AddDays(-15));
	}

	/// <summary>
	/// Puntuación = 50 × profundidad + 10 por sesión con reacción (hasta 3) + 5 por perfil de origen (hasta 4)
	/// + 10 si es bipolar. Por puesto: las primeras son de alta prioridad (amarillas), las siguientes secundarias.
	/// </summary>
	private void Clasificar(MapaLvn mapa)
	{
		foreach (var z in mapa.Zonas)
		{
			z.Puntuacion = 50m * z.Profundidad + 10m * Math.Min(z.SesionesConReaccion, 3) + 5m * Math.Min(z.Origenes.Count, 4)
				+ (z.Bipolar ? 10m : 0m);
		}

		var ordenadas = mapa.Zonas.OrderByDescending(z => z.Puntuacion).ThenByDescending(z => z.Profundidad).ThenBy(z => z.Centro).ToList();

		for (var i = 0; i < ordenadas.Count; i++)
		{
			ordenadas[i].Puesto = i + 1;
			ordenadas[i].Prioridad = i < _cfg.CantidadLvnAltaPrioridad
				? PrioridadLvn.Alta
				: i < _cfg.CantidadLvnAltaPrioridad + _cfg.CantidadLvnSecundarios ? PrioridadLvn.Secundaria : PrioridadLvn.Menor;
		}

		mapa.Zonas = mapa.Zonas.OrderBy(z => z.Centro).ToList();
	}

	/// <summary>
	/// Evidencia prospectiva: contacto con la zona y cierre fuera de ella, seguido de un alejamiento de N ticks en
	/// el plazo configurado sin superar la excursión adversa. Solo cuenta desde que la zona fue reconocida.
	/// </summary>
	public void SeguirReacciones(Vela vela, DateTime diaGlobex)
	{
		var alejamiento = _cfg.Ticks(_cfg.ReaccionAlejamientoTicks);
		var adversa = _cfg.Ticks(_cfg.ReaccionExcursionAdversaTicks);

		foreach (var r in _registro)
		{
			if (r.ReconocidaUtc > vela.InicioUtc)
				continue;

			var inferior = _cfg.APrecio(r.Inferior);
			var superior = _cfg.APrecio(r.Superior) + _cfg.TamanoTick;

			if (r.Pendiente is { } p)
			{
				if (p.Soporte ? vela.Maximo >= p.Borde + alejamiento : vela.Minimo <= p.Borde - alejamiento)
				{
					if (p.Soporte)
						r.Soporte++;
					else
						r.Resistencia++;

					r.Dias.Add(diaGlobex);
					r.Pendiente = null;
				}
				else if ((p.Soporte ? vela.Minimo <= inferior - adversa : vela.Maximo >= superior + adversa) || vela.FinUtc > p.VenceUtc)
				{
					r.Pendiente = null;
				}

				continue;
			}

			if (!vela.Toca(inferior, superior))
				continue;

			if (vela.Cierre > superior)
				r.Pendiente = new Pendiente { Soporte = true, Borde = superior, VenceUtc = vela.FinUtc.AddMinutes(_cfg.ReaccionPlazoMinutos) };
			else if (vela.Cierre < inferior)
				r.Pendiente = new Pendiente { Soporte = false, Borde = inferior, VenceUtc = vela.FinUtc.AddMinutes(_cfg.ReaccionPlazoMinutos) };
		}
	}

	/// <summary>Siguiente LVN de alta prioridad en la dirección de la operación, más allá del precio de entrada.</summary>
	public ZonaLvn? SiguientePrioritario(Direccion direccion, decimal entrada, DateTime instanteUtc)
	{
		var mapa = Mapas.LastOrDefault(m => m.CreadoUtc <= instanteUtc);

		if (mapa is null)
			return null;

		var prioritarios = mapa.Zonas.Where(z => z.Prioridad == PrioridadLvn.Alta);

		return direccion == Direccion.Larga
			? prioritarios.Where(z => z.Inferior > entrada).OrderBy(z => z.Inferior).FirstOrDefault()
			: prioritarios.Where(z => z.Superior < entrada).OrderByDescending(z => z.Superior).FirstOrDefault();
	}

	public MapaLvn? MapaEn(DateTime instanteUtc) => Mapas.LastOrDefault(m => m.CreadoUtc <= instanteUtc);
}
