namespace OrderFlowSuite.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

using Termico;

/// <summary>Vela cerrada (o abierta, en vivo) con su footprint por tick: tick → (Bid, Ask).</summary>
public sealed class VelaFlujo
{
	public DateTime InicioUtc;
	public decimal Open, High, Low, Close, Volume, Delta;
	public SortedDictionary<long, (decimal Bid, decimal Ask)> Niveles = new();
}

/// <summary>Nivel de referencia: PDH, PDL, IBH, IBL, ONH, ONL, swings, VAH/VAL, POC, LVN o número redondo.</summary>
public readonly record struct NivelReferencia(string Nombre, decimal Precio, bool EsMaximo);

/// <summary>
/// Contexto del mercado a partir de las velas cerradas: niveles del día (PDH/PDL, overnight, Initial Balance),
/// swings, números redondos, VWAP, EMA y el perfil de la sesión (POC, VAH, VAL, LVN con el motor del Thermal Volume
/// Profile). Lo comparten los motores de stops, barridos, icebergs (sesgo) y el Absorption Master.
/// </summary>
public sealed class MotorContexto
{
	private readonly List<VelaFlujo> _velas = new();
	private DateTime? _dia;
	private decimal _altoDia, _bajoDia, _vwapPv, _vwapV;
	private decimal? _onAlto, _onBajo, _ibAlto, _ibBajo, _rthAlto, _rthBajo, _ema, _emaAnterior;
	private AcumuladorTicks? _perfilSesion;
	private DateTime? _inicioPerfil;
	private ResultadoTermico? _perfilRth;
	private bool _perfilSucio;

	public decimal Tick { get; set; } = 0.25m;
	public int IbMinutes { get; set; } = 60;
	public int SwingLengthBars { get; set; } = 20;
	public int RoundNumberStep { get; set; } = 50;
	public int TrendLengthBars { get; set; } = 50;
	public FuenteTendencia FuenteTendencia { get; set; } = FuenteTendencia.VWAP;
	public double ValueAreaPercent { get; set; } = 70;

	public decimal? Pdh { get; private set; }
	public decimal? Pdl { get; private set; }
	public decimal? Onh => _onAlto;
	public decimal? Onl => _onBajo;
	public decimal? Ibh => _ibAlto;
	public decimal? Ibl => _ibBajo;
	public decimal? Vwap => _vwapV > 0 ? _vwapPv / _vwapV : null;
	public decimal? Ema => _ema;
	public List<NivelReferencia> Swings { get; } = new();
	public decimal UltimoPrecio { get; private set; }
	public IReadOnlyList<VelaFlujo> Velas => _velas;

	/// <summary>Máximo y mínimo del día operativo en curso (18:00-17:00 ET).</summary>
	public decimal? AltoDia => _dia is null ? null : _altoDia;

	public decimal? BajoDia => _dia is null ? null : _bajoDia;

	/// <summary>
	/// Perfil de la sesión RTH (POC, VAH, VAL y LVN) con el motor del Thermal Volume Profile. Se analiza solo cuando se
	/// pide y la sesión cambió (no en cada vela del historial).
	/// </summary>
	public ResultadoTermico? PerfilRth
	{
		get
		{
			if (_perfilSucio && _perfilSesion is not null)
			{
				_perfilSucio = false;

				if (Termico.Perfil.Construir(_perfilSesion, null, TiposPerfil.Volume, 1, true, 300) is { } perfil)
					_perfilRth = MotorTermico.Analizar(perfil, null, new ParametrosTermicos { ValueAreaPercent = ValueAreaPercent }, null);
			}

			return _perfilRth;
		}
	}

	public void Reiniciar()
	{
		_velas.Clear();
		Swings.Clear();
		(_dia, _perfilSesion, _inicioPerfil, _perfilRth, _perfilSucio) = (null, null, null, null, false);
		(_altoDia, _bajoDia, _vwapPv, _vwapV, UltimoPrecio) = (0, 0, 0, 0, 0);
		(_onAlto, _onBajo, _ibAlto, _ibBajo, _rthAlto, _rthBajo, _ema, _emaAnterior) = (null, null, null, null, null, null, null, null);
		(Pdh, Pdl) = (null, null);
	}

	/// <summary>Precio en vivo (trades): números redondos y tendencia al día entre cierres de vela.</summary>
	public void ActualizarPrecio(decimal precio)
	{
		if (precio > 0)
			UltimoPrecio = precio;
	}

	/// <summary>Pendiente de la tendencia (VWAP o EMA): +1 sube, -1 baja, 0 plana o sin datos.</summary>
	public int Tendencia
	{
		get
		{
			if (FuenteTendencia == FuenteTendencia.EMA)
				return _ema is { } e && _emaAnterior is { } a ? Math.Sign(e - a) : 0;

			return Vwap is { } v ? Math.Sign(UltimoPrecio - v) : 0;
		}
	}

	public void Agregar(VelaFlujo v)
	{
		_velas.Add(v);

		if (_velas.Count > 5000)
			_velas.RemoveRange(0, _velas.Count - 5000);

		UltimoPrecio = v.Close;
		var dia = Zonas.DiaOperativo(v.InicioUtc);
		var et = Zonas.AEt(v.InicioUtc);
		var hora = et.TimeOfDay;

		// Día operativo nuevo (18:00 ET): el día que termina da PDH/PDL.
		if (_dia is { } anterior && anterior != dia)
		{
			(Pdh, Pdl) = (_altoDia, _bajoDia);
			(_onAlto, _onBajo, _ibAlto, _ibBajo, _rthAlto, _rthBajo) = (null, null, null, null, null, null);
			(_vwapPv, _vwapV) = (0, 0);
		}

		(_altoDia, _bajoDia) = _dia == dia ? (Math.Max(_altoDia, v.High), Math.Min(_bajoDia, v.Low)) : (v.High, v.Low);
		_dia = dia;

		var rth = hora >= ClasificadorSesion.AperturaNy && hora < new TimeSpan(16, 0, 0);

		if (hora < ClasificadorSesion.AperturaNy || hora >= new TimeSpan(18, 0, 0))
		{
			_onAlto = Math.Max(_onAlto ?? v.High, v.High);
			_onBajo = Math.Min(_onBajo ?? v.Low, v.Low);
		}

		if (rth)
		{
			// La VWAP se reinicia a la apertura de NY (antes, la del día operativo desde las 18:00).
			if (_rthAlto is null)
				(_vwapPv, _vwapV) = (0, 0);

			_rthAlto = Math.Max(_rthAlto ?? v.High, v.High);
			_rthBajo = Math.Min(_rthBajo ?? v.Low, v.Low);

			if (hora < ClasificadorSesion.AperturaNy + TimeSpan.FromMinutes(Math.Max(1, IbMinutes)))
			{
				_ibAlto = Math.Max(_ibAlto ?? v.High, v.High);
				_ibBajo = Math.Min(_ibBajo ?? v.Low, v.Low);
			}
		}

		var tipico = (v.High + v.Low + v.Close) / 3;
		_vwapPv += tipico * v.Volume;
		_vwapV += v.Volume;

		var alfa = 2m / (Math.Max(1, TrendLengthBars) + 1);
		_emaAnterior = _ema;
		_ema = _ema is { } e ? e + alfa * (v.Close - e) : v.Close;

		Pivotes();
		ActualizarPerfil(v, rth);
	}

	private void Pivotes()
	{
		var n = Math.Max(1, SwingLengthBars);
		var i = _velas.Count - 1 - n;

		if (i < n)
			return;

		bool alto = true, bajo = true;

		for (var k = i - n; k <= i + n; k++)
		{
			if (k == i)
				continue;

			alto &= _velas[k].High < _velas[i].High;
			bajo &= _velas[k].Low > _velas[i].Low;
		}

		if (alto)
			Swings.Add(new NivelReferencia("swing", _velas[i].High, true));

		if (bajo)
			Swings.Add(new NivelReferencia("swing", _velas[i].Low, false));

		// Solo los swings que el precio no ha superado, los 6 más recientes.
		Swings.RemoveAll(s => s.EsMaximo ? UltimoPrecio > s.Precio : UltimoPrecio < s.Precio);

		if (Swings.Count > 6)
			Swings.RemoveRange(0, Swings.Count - 6);
	}

	/// <summary>Perfil de la sesión RTH en curso (o de la anterior, antes de la apertura): POC, VAH, VAL y LVN.</summary>
	private void ActualizarPerfil(VelaFlujo v, bool rth)
	{
		if (!rth)
			return;

		var inicio = Zonas.AEt(v.InicioUtc).Date;

		if (_inicioPerfil != inicio || _perfilSesion is null)
		{
			_perfilSesion = new AcumuladorTicks(Tick, 30);
			_inicioPerfil = inicio;
		}

		var vp = new VelaPerfil { Time = v.InicioUtc, High = v.High, Low = v.Low, Volume = v.Volume, Delta = v.Delta };

		foreach (var (t, n) in v.Niveles)
			vp.Niveles.Add(new NivelPrecio(t * Tick, n.Bid + n.Ask, n.Bid, n.Ask));

		_perfilSesion.Agregar(vp);
		_perfilSucio = true;
	}

	/// <summary>Niveles de perfil (POC, VAH, VAL y LVN) para los setups y los objetivos.</summary>
	public List<NivelReferencia> NivelesPerfil()
	{
		var r = new List<NivelReferencia>();

		if (PerfilRth is not { } p)
			return r;

		r.Add(new NivelReferencia("POC", p.PrecioPoc, true));
		r.Add(new NivelReferencia("VAH", p.PrecioVah, true));
		r.Add(new NivelReferencia("VAL", p.PrecioVal, false));
		r.AddRange(p.Lvns.Where(l => !l.Manual).Select(l => new NivelReferencia("LVN", l.Precio, true)));
		return r;
	}

	/// <summary>Niveles de liquidez (zonas probables de stops y referencias de barridos).</summary>
	public List<NivelReferencia> NivelesLiquidez(bool conRedondos, bool conPerfil)
	{
		var r = new List<NivelReferencia>();

		void Add(string nombre, decimal? precio, bool maximo)
		{
			if (precio is { } p)
				r.Add(new NivelReferencia(nombre, p, maximo));
		}

		Add("PDH", Pdh, true);
		Add("PDL", Pdl, false);
		Add("ONH", _onAlto, true);
		Add("ONL", _onBajo, false);

		// El IB solo cuenta una vez terminado.
		if (_rthAlto is not null && _ibAlto is not null && Zonas.AEt(_velas[^1].InicioUtc).TimeOfDay >= ClasificadorSesion.AperturaNy + TimeSpan.FromMinutes(IbMinutes))
		{
			Add("IBH", _ibAlto, true);
			Add("IBL", _ibBajo, false);
		}

		r.AddRange(Swings);

		if (conPerfil && PerfilRth is { } p)
		{
			r.Add(new NivelReferencia("VAH", p.PrecioVah, true));
			r.Add(new NivelReferencia("VAL", p.PrecioVal, false));
		}

		// Números redondos: los dos de arriba y los dos de abajo del precio actual.
		if (conRedondos && RoundNumberStep > 0 && UltimoPrecio > 0)
		{
			var paso = (decimal)RoundNumberStep;
			var base0 = Math.Floor(UltimoPrecio / paso) * paso;

			for (var k = -1; k <= 2; k++)
			{
				var precio = base0 + k * paso;
				r.Add(new NivelReferencia($"{precio:0}R", precio, precio > UltimoPrecio));
			}
		}

		return r;
	}
}
