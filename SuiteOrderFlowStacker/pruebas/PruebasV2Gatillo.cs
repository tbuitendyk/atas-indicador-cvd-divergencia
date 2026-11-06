using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>S6: TriggerAnalyzer (arranque, continuidad, pendiente, inválido, no ejecutable) y gatillo manual.</summary>
internal static class PruebasV2Gatillo
{
	/// <summary>Retroceso a una zona larga [P(-4), P(0)]: llegada vendedora que toca la zona; devuelve el índice del toque.</summary>
	private static (Escenario X, ZonaOperable Z, int Toque) Retroceso()
	{
		var x = new Escenario(+1, new DateTime(2026, 11, 2, 9, 31, 0));
		var z = new ZonaOperable { Id = 1, Tipo = "FVG", Bajo = x.Pk(-4), Alto = x.Pk(0), Signo = 1 };
		var toque = x.Barra(20, Enumerable.Range(0, 10).Select(i => (8 - i, 3m, 7m)).ToArray());
		return (x, z, toque);
	}

	private static ContextoGatillo Ctx(Escenario x, Tendencia sesgo = Tendencia.Rango, decimal? objetivo = null) => new()
	{
		Barras = x.M.Barras.ToList(),
		P = x.P,
		Tick = Tick,
		ContratosPorBarra = 100,
		Fuentes = x.Fuentes,
		Medianas = _ => (20, 9),
		M5 = sesgo,
		M15 = sesgo,
		Objetivo = (_, _) => objetivo,
		Ahora = x.T
	};

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S6 Gatillo VÁLIDO por VELA DE ARRANQUE: F1 llegada, F2 freno, F3 rápida que cierra fuera de la zona");
		var (x, z, toque) = Retroceso();
		x.Barra(20, (-2, 13, 20), (-3, 14, 20), (-4, 13, 20));
		x.Barra(20, (-2, 13, 20), (-3, 14, 20), (-4, 13, 20));
		var pend = AnalizadorGatillo.Analizar(z, toque, Ctx(x), continuidad: false);
		c.Check("antes de F3: PENDIENTE (falta F3)", $"{pend.Resultado} {string.Join(",", pend.Faltan)}", "Pendiente falta F3");

		x.Barra(4, Enumerable.Range(0, 20).Select(i => (-3 + i, 4m, 1m)).ToArray());
		var a = AnalizadorGatillo.Analizar(z, toque, Ctx(x), continuidad: false);
		c.Check("VÁLIDO por vela de ARRANQUE", $"{a.Resultado} {a.Tipo} {a.Respuesta}", "Valido Arranque GATILLO POR VELA DE ARRANQUE");
		c.Check("entrada al cierre de la vela gatillo; stop 3 ticks bajo la zona; TP1 1.5R sin LVN",
			$"{F(a.Entrada)} {F(a.Stop)} {F(a.Tp1)} {F(a.RewardRisk)}", "21424 21418.25 21432.75 1.52");
		c.Check("razones: F1, compresión, delta sin progreso, F3", string.Join(" | ", a.Razones.Take(3)),
			"F1 llegada a la zona con agresión en contra | F2: 2 barras de compresión | Delta en contra sin progreso (absorción)");
		c.Check("flecha verde (anexo)", a.Flecha, "▲");

		var rr = AnalizadorGatillo.Analizar(z, toque, Ctx(x, objetivo: 21424.50m), continuidad: false);
		c.Check("TP1 a 2 ticks: R:R < 1.5 = NO EJECUTABLE", $"{rr.Resultado} {rr.Respuesta} {rr.Motivo}", "Invalido NO EJECUTABLE R:R 0.1 < 1.5");

		c.Seccion("S6 INVÁLIDO: cierre que atraviesa la zona; iceberg en contra");
		var (xi, zi, ti) = Retroceso();
		xi.Barra(20, Enumerable.Range(0, 10).Select(i => (-1 - i, 3m, 7m)).ToArray());
		var inv = AnalizadorGatillo.Analizar(zi, ti, Ctx(xi), continuidad: false);
		c.Check("cierre bajo la zona: INVÁLIDO", $"{inv.Resultado} {inv.Motivo}", "Invalido cierre que atraviesa la zona");

		var (xc, zc, tc) = Retroceso();
		xc.Fuentes.Eventos.Add(new IcebergEvent { Id = 3, Fuente = FuenteSuite.Iceberg, Utc = xc.T, CambioUtc = xc.T, Precio = xc.Pk(-2), Compra = false, Estado = "AGUANTANDO", Mbo = true, Ejecutado = 200 });
		xc.Barra(4, Enumerable.Range(0, 20).Select(i => (-3 + i, 4m, 1m)).ToArray());
		var ic = AnalizadorGatillo.Analizar(zc, tc, Ctx(xc), continuidad: false);
		c.Check("iceberg vendedor AGUANTANDO en la zona larga: INVÁLIDO", $"{ic.Resultado} {ic.Motivo}", "Invalido iceberg en contra AGUANTANDO");

		c.Seccion("S6 VÁLIDO por VELA DE CONTINUIDAD (y rechazada si no se permite: T27 en S7)");
		var (xk, zk, tk) = Retroceso();
		// Mínimo de la reacción por encima del de la llegada (que tiene subasta sin terminar): no hay F2.
		xk.Barra(4, Enumerable.Range(0, 20).Select(i => (i, 5m, 0m)).ToArray());
		var cont = AnalizadorGatillo.Analizar(zk, tk, Ctx(xk, Tendencia.Alcista), continuidad: true);
		c.Check("sin F2, sesgo 5m/15m alcista, barra rápida con stacked y cierre arriba: CONTINUIDAD", $"{cont.Resultado} {cont.Respuesta}", "Valido GATILLO POR VELA DE CONTINUIDAD");
		var sinCont = AnalizadorGatillo.Analizar(zk, tk, Ctx(xk, Tendencia.Alcista), continuidad: false);
		c.Check("la misma barra sin aceptar continuidad: no es válida (falta absorción)", $"{sinCont.Resultado} {sinCont.Faltan.FirstOrDefault()}", "Pendiente falta absorción");
		var sinSesgo = AnalizadorGatillo.Analizar(zk, tk, Ctx(xk, Tendencia.Bajista), continuidad: true);
		c.Check("con sesgo en contra: no hay continuidad", sinSesgo.Faltan.Contains("falta sesgo 5m/15m a favor").ToString(), "True");

		c.Seccion("S6 Confluencias del anexo: micro-tendencia, velas de arranque");
		var mb = new MotorBarrasVolumen { Tick = Tick, ContratosPorBarra = 100 };
		var t = Utc(new DateTime(2026, 11, 2, 9, 40, 0));
		Barra(mb, ref t, 21410, 21406, 20, -0.4m);
		Barra(mb, ref t, 21408, 21404, 20, -0.4m);
		Barra(mb, ref t, 21406, 21402, 20, -0.4m);
		Barra(mb, ref t, 21404, 21400, 20, -0.4m);
		Barra(mb, ref t, 21400, 21408, 4, 0.6m);
		c.Check("máximos descendentes y cierre por encima de su línea: ruptura de la micro-tendencia",
			AnalizadorGatillo.RompeMicroTendencia(mb.Barras, 3, 4, +1).ToString(), "True");

		var mp = new MotorBarrasVolumen { Tick = Tick, ContratosPorBarra = 100 };
		var tp = t;
		mp.Agregar(tp, 21402m, 10, true);
		mp.Agregar(tp.AddSeconds(1), 21400m, 60, false);
		mp.Agregar(tp.AddSeconds(2), 21400.25m, 10, true);
		mp.Agregar(tp.AddSeconds(3), 21402.25m, 10, true);
		mp.Agregar(tp.AddSeconds(4), 21402.50m, 10, true);
		c.Check("hammer con el POC en el tercio inferior", AnalizadorGatillo.Patron(mp.Barras, 0, +1, Tick, new ParametrosV2()) ?? "", "Vela de arranque: hammer / pinbar con el POC en el tercio inferior");

		c.Seccion("S6 Gatillo MANUAL: una de tres respuestas (o [-] sin dirección)");
		var (xm, _, _) = Retroceso();
		xm.Barra(20, (-2, 13, 20), (-3, 14, 20), (-4, 13, 20));
		xm.Barra(20, (-2, 13, 20), (-3, 14, 20), (-4, 13, 20));
		xm.Barra(4, Enumerable.Range(0, 20).Select(i => (-3 + i, 4m, 1m)).ToArray());
		var barras = xm.M.Barras;
		var req = new ManualTriggerRequest
		{
			Id = 7, DesdeUtc = barras[^4].InicioUtc, HastaUtc = barras[^1].FinUtc, Alto = xm.Pk(0), Bajo = xm.Pk(-4), Tipo = TipoAnalisisManual.Auto, Direccion = DireccionManual.Largo
		};
		var m = AnalizadorGatillo.Manual(req, Ctx(xm));
		c.Check("recuadro sobre llegada + freno + F3, dirección LARGO: GATILLO POR VELA DE ARRANQUE", $"{m.Respuesta} {m.VelaAnalizada}", "GATILLO POR VELA DE ARRANQUE VELA COMPUESTA");

		var sinDir = AnalizadorGatillo.Manual(new ManualTriggerRequest
		{
			Id = 8, DesdeUtc = barras[^3].InicioUtc, HastaUtc = barras[^2].FinUtc, Alto = xm.Pk(0), Bajo = xm.Pk(-4), Tipo = TipoAnalisisManual.VelaCompuesta, Direccion = DireccionManual.Auto
		}, Ctx(xm));
		c.Check("AUTO sobre el freno (POC abajo pero delta negativo): [-] sin direccionalidad = NO EJECUTABLE",
			$"{sinDir.Flecha} {sinDir.Respuesta}", "[-] NO EJECUTABLE");
	}
}
