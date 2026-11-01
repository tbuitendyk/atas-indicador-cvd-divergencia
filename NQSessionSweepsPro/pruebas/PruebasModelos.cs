using System;
using System.Collections.Generic;
using System.Linq;

using NqSweeps.Nucleo;

using static Datos;
using static PruebasBarridos;

/// <summary>F4/F5: ruptura en el tick, cuentas atrás, velocidad, contexto H1, proyección, LVN, Modelo A y Modelo B.</summary>
internal static class PruebasModelos
{
	public static void Ejecutar(Comprobador c)
	{
		EnVivo(c);
		ContextoYProyeccion(c);
		ModeloA(c);
		ModeloB(c);
		PerfilLvn(c);
	}

	private static void EnVivo(Comprobador c)
	{
		c.Seccion("F4. En vivo: ruptura en el tick (t0), cuentas atrás de 1 y 5 min, velocidad y delta antes de t0");

		var m = Escenario(Martes.AddHours(8.75));
		var inicio = Utc(Martes.AddHours(8.75));
		void T(int segundo, decimal precio, decimal vol, bool compra) => m.Trade(inicio.AddSeconds(segundo), precio, vol, compra, 20006, inicio);

		T(5, 20009, 30, false);
		c.Check("antes de cruzar L no hay aviso", m.Avisos.Count(a => a.Tipo == TipoAviso.Ruptura).ToString(), "0");

		T(13, 20010.25m, 20, true);
		var ruptura = m.Avisos.Single(a => a.Tipo == TipoAviso.Ruptura);
		c.Check("el trade que cruza L dispara la ruptura en ese segundo (sin esperar al cierre)", ruptura.Texto, "NQ rompe MAX Londres 20010.00 a las 08:45:13 ET");
		c.Check("correo de barrido: asunto", ruptura.Asunto ?? "", "NQ BARRIDO BUYSIDE - MAX Londres 20010.00 - 08:45");
		c.Check("cuerpo: hora, nivel, contexto, contratos del minuto, delta y stop",
			string.Join(" / ", ruptura.Cuerpo!.Split('\n').Take(6)),
			"Barrido de liquidez a las 08:45:13 / Toma liquidez del MAXIMO de Londres / Contexto H1: Rango / Liquida maximo con precio 20010.00 / Contratos negociados (1m): 50 | Mas alla del nivel: 20 / Delta 1m: -30");

		T(14, 20011, 30, true);
		T(16, 20011, 20, true);
		T(25, 20011.5m, 10, true);
		T(46, 20008, 5, false);
		c.Check("de vuelta bajo L a 14 s del cierre del minuto: cuenta atrás 1m", m.Avisos.Last().Texto, "Vela gatillo 1m cierra en 14 s - precio bajo el MAX roto");

		m.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));
		var z = m.ZonasBarrido.Single();
		c.Check("al cerrar la vela: la zona usa t0 del tick, sin otro aviso de ruptura",
			$"{Zonas.AEt(z.T0Utc):HH:mm:ss} vivo {z.Vivo is not null} avisos {m.Avisos.Count(a => a.Tipo == TipoAviso.Ruptura)}", "08:45:13 vivo True avisos 1");
		c.Check("velocidad pico por tramos de 5 s (70 contratos en 08:45:13-17 = 14 c/s), no aproximada",
			$"{z.VelocidadPico:0} {z.VelocidadAproximada}", "14 False");
		c.Check("cambio de delta: delta de la ventana menos el delta del minuto antes de t0 (-30)", $"{z.W1!.DeltaChange - z.W1.DeltaWin:0}", "30");

		for (var i = 1; i <= 3; i++)
			m.Procesar(V(Martes.AddHours(8.75).AddMinutes(i), 20007, 20008, 20004, 20006));

		m.Trade(inicio.AddMinutes(4).AddSeconds(50), 20007, 5, false, 20006, inicio.AddMinutes(4));
		c.Check("cuenta atrás 5m: 10 s antes del cierre de la vela de 5 min (08:50)", m.Avisos.Last().Texto, "Vela gatillo 5m cierra en 10 s - precio bajo el MAX roto");
	}

	private static void ContextoYProyeccion(Comprobador c)
	{
		c.Seccion("F4. Contexto H1 (pivotes HH/HL y LH/LL, desempate EMA 20) y proyección T1-T3");

		static List<Vela> H1(IEnumerable<decimal> medios) => medios.Select((p, i) => new Vela { Indice = i, Open = p, High = p + 1, Low = p - 1, Close = p }).ToList();
		var subida = new decimal[] { 5, 6, 7, 10, 7, 6, 5, 4, 3, 1, 3, 4, 5, 8, 9, 13, 9, 8, 7, 5, 4, 3, 4, 5, 6 };

		string Contexto(decimal[] medios)
		{
			var h = new CalculoH1();
			var velas = H1(medios);

			for (var i = 1; i <= velas.Count; i++)
				h.Agregar(velas.Take(i).ToList(), 3);

			return h.Valor.Texto();
		}

		c.Check("máximos y mínimos crecientes: H1 Alcista", Contexto(subida), "Alcista");
		c.Check("en espejo: H1 Bajista", Contexto(subida.Select(x => 100 - x).ToArray()), "Bajista");
		c.Check("pocas velas, sin pivotes: desempate por la EMA (subida constante)", Contexto(Enumerable.Range(1, 6).Select(x => (decimal)x).ToArray()), "Alcista");

		var cfg = new ConfiguracionSweeps();
		var candidatos = new List<(string, decimal)> { ("MIN Asia", 19970), ("MIN Londres", 19990), ("LVN", 20005.5m), ("POC dia anterior", 19980) };
		var p = Proyeccion.Calcular(Lado.Buyside, 20007, 20012, candidatos, cfg, 40, (20012, 19990));
		c.Check("T1 = primer objetivo a 20 ticks o más (el LVN a 6 ticks no cuenta); T2 y T3 los siguientes",
			p.Texto(x => x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
			"T1 19990.00 (17 pts) | T2 19980.00 (27 pts) fuera de rango diario | T3 19970.00 (37 pts) fuera de rango diario");
		c.Check("stop = extremo + 8 ticks; USD (NQ 20/punto) y R:R", $"{p.Stop} {p.Objetivos[0].Usd:0} {p.Objetivos[0].Rr:0.00} restante {p.RangoRestante}", "20014.00 340 2.43 restante 18");
		cfg.Contrato = ContratoValor.MNQ;
		c.Check("MNQ: 2 USD por punto", Proyeccion.Calcular(Lado.Buyside, 20007, 20012, candidatos, cfg, null, null).Objetivos[0].Usd.ToString("0"), "34");
	}

	private static void ModeloA(Comprobador c)
	{
		c.Seccion("F5. Modelo A: vela gatillo de rechazo en un LVN (pin bar, POC y VA arriba, cierre bajo el máximo)");

		var m = Escenario(Martes.AddHours(8.75), cfg => cfg.LvnManuales = new() { 20011.5m });
		m.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));
		var s = m.Senales.Single();
		c.Check("las 5 condiciones en la vela de barrido: entrada en su cierre", $"{s.Etiqueta} {s.Entrada} stop {s.Stop} {s.Cumplidas}/{s.Confluencias.Count}", "A SHORT 20007 stop 20014.00 5/5");
		c.Check("confluencias", s.TextoConfluencias, "Pin bar SI, POC tercio superior SI, VA tercio superior SI, Cierre bajo max SI, LVN SI");
		var aviso = m.Avisos.Single(a => a.Tipo == TipoAviso.EntradaA);
		c.Check("alerta y asunto del correo", $"{aviso.Texto} / {aviso.Asunto}", "ENTRADA BAJISTA - Modelo A 20007.00 - MAX Londres 20010.00 / NQ ENTRADA BAJISTA Modelo A - 20007.00");

		var tarde = Escenario(Martes.AddHours(8.75), cfg => cfg.LvnManuales = new() { 20011.5m });
		tarde.Procesar(V(Martes.AddHours(8.75), 20006, 20012, 20005, 20011, 100, 0, (20011m, 50, 250), (20011.5m, 40, 200), (20012m, 30, 100)));
		c.Check("sin cierre bajo el máximo: todavía sin señal", tarde.Senales.Count.ToString(), "0");
		tarde.Procesar(V(Martes.AddHours(8.75).AddMinutes(1), 20011, 20011.5m, 20007, 20008));
		var s2 = tarde.Senales.Single();
		c.Check("la siguiente vela que cierra bajo L es la vela gatillo (Confluencias 4/5: sin pin bar)", $"{s2.Etiqueta} {s2.Entrada} {s2.Cumplidas}/5", "A SHORT 20008 4/5");

		var sinLvn = Escenario(Martes.AddHours(8.75));
		sinLvn.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));
		c.Check("sin LVN tocado: no hay Modelo A", sinLvn.Senales.Count.ToString(), "0");

		var h1 = Escenario(Martes.AddHours(8.75), cfg => (cfg.LvnManuales, cfg.RequireH1Alignment) = (new() { 20011.5m }, true));
		h1.Procesar(VelaBarridoMax(Martes.AddHours(8.75)));
		c.Check("RequireH1Alignment con H1 sin tendencia bajista: el corto se filtra", $"{h1.H1.Valor.Texto()} {h1.Senales.Count}", "Rango 0");
	}

	private static void ModeloB(Comprobador c)
	{
		c.Seccion("F5. Modelo B: impulso, retroceso a un LVN y cierre rompiéndolo con 3 o más confluencias");

		var m = Escenario(Martes.AddHours(8.75), cfg => cfg.LvnManuales = new() { 20011.5m, 20003m });
		var t = Martes.AddHours(8.75);
		m.Procesar(VelaBarridoMax(t));
		m.Procesar(V(t.AddMinutes(1), 20007, 20007.5m, 20000, 20001, 100, -40));
		var b = m.SecuenciasB.Single();
		c.Check("tras el Modelo A: impulso de 1,5 × ATR desde el máximo del barrido", b.Etapa.Texto(), "IMPULSO");

		m.Procesar(V(t.AddMinutes(2), 20001, 20001.5m, 19996, 19997, 100, -40));
		m.Procesar(V(t.AddMinutes(3), 19997, 20000, 19996.5m, 19999.5m, 100, 50, (19999.75m, 10, 60)));
		m.Procesar(V(t.AddMinutes(4), 19999.5m, 20002, 19999, 20001.5m, 100, 50, (20001.75m, 10, 60)));
		m.Procesar(V(t.AddMinutes(5), 20001.5m, 20004, 20001, 20002.5m, 100, 40, (20003.75m, 20, 150), (20003.5m, 15, 100)));
		c.Check("retroceso que toca el LVN 20003 dentro del rango del impulso", $"{b.Etapa.Texto()} {b.NombreNivel} {b.Nivel} techo {b.TechoRetroceso}", "RETROCESO A LVN LVN 20003 techo 20004");

		m.Procesar(V(t.AddMinutes(6), 20002.5m, 20002.75m, 19999, 19999.5m, 100, -100));
		var s = m.Senales.Last();
		c.Check("cierre bajo el LVN: B SHORT, stop = techo del retroceso + 8 ticks", $"{s.Etiqueta} {s.Entrada} stop {s.Stop} etapa {b.Etapa.Texto()}", "B SHORT 19999.5 stop 20006.00 etapa ENTRADA");
		c.Check("confluencias del Modelo B (mínimo 3)", $"{s.Cumplidas}: {s.TextoConfluencias}",
			"3: Absorcion en el techo SI, Subasta terminada NO, 3 velas con POC arriba SI, Ruptura de directriz NO, Giro de delta SI, Toco IFVG/FVG NO");

		var cancelada = Escenario(Martes.AddHours(8.75), cfg => cfg.LvnManuales = new() { 20011.5m, 20003m });
		cancelada.Procesar(VelaBarridoMax(t));
		cancelada.Procesar(V(t.AddMinutes(1), 20007, 20013, 20006, 20012.5m));
		c.Check("cancelación: cierre por encima del máximo del barrido", $"{cancelada.SecuenciasB.Single().Etapa.Texto()} ({cancelada.SecuenciasB.Single().Motivo})", "CANCELADO (cierre mas alla del barrido)");
	}

	private static void PerfilLvn(Comprobador c)
	{
		c.Seccion("F5. Perfil/LVN: RTH del día anterior + overnight actual, LVN calculados y manuales, POC/VAH/VAL");

		var cfg = new ConfiguracionSweeps { LvnManuales = new() { 20100m } };
		var m = new MotorSweeps(cfg);
		var lunes = new DateTime(2026, 11, 2);

		// Lunes RTH: mucho volumen en 20000 (más) y en 20020, poco en medio y nada en 20009.25-20010.75 (valle en 20010).
		for (var t = lunes.AddHours(9.5); t < lunes.AddHours(16); t = t.AddMinutes(1))
		{
			var alto = t.Minute % 2 == 0;
			var centro = alto ? 20020m : 20000m;
			m.Procesar(V(t, centro, centro + 2, centro - 2, centro, alto ? 300 : 400));
			m.Procesar(V(t.AddSeconds(30), centro, centro + 9, centro - 9, centro, 5));
		}

		foreach (var v in Planas(lunes.AddHours(18), lunes.AddHours(20), 20000))
			m.Procesar(v);

		var p = m.PerfilLvn!;
		c.Check("LVN calculado en el valle entre los dos nodos (y el manual se suma)",
			string.Join(" | ", p.Lvns.Select(l => $"{(l.Manual ? "manual" : "LVN " + l.Ranking)} {l.Precio}")), "manual 20100 | LVN 1 20010.00");
		c.Check("POC en el nodo más fuerte (empate dentro del nodo: el más cercano al centro) y VA alrededor",
			$"{p.Poc} {p.Val <= p.Poc && p.Poc <= p.Vah} {p.Val >= 19991 && p.Vah <= 20029}", "20002.00 True True");
	}
}
