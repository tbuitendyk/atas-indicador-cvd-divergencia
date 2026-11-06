using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using SessionSweeps.Nucleo;

/// <summary>Ayudas de las pruebas de Session Sweeps Pro v2.0 (solo el espacio de nombres SessionSweeps.Nucleo).</summary>
internal static class DatosV2
{
	public const decimal Tick = 0.25m;

	public static DateTime Utc(DateTime et) => Zonas.AUtc(et, ZonaHoraria.NuevaYork);

	public static string F(decimal? v) => v?.ToString("0.##", CultureInfo.InvariantCulture) ?? "null";

	/// <summary>Una barra de volumen hecha a mano: trades en [desde, hasta] con duración 'seg' y delta 'deltaPct'.</summary>
	public static void Barra(MotorBarrasVolumen m, ref DateTime t, decimal desde, decimal hasta, double seg, decimal deltaPct, decimal volumen = 100)
	{
		var precios = new List<decimal>();
		var paso = hasta >= desde ? Tick : -Tick;

		for (var p = desde; paso > 0 ? p <= hasta : p >= hasta; p += paso)
			precios.Add(p);

		// Reparto exacto: cada precio lleva su parte redondeada y el último el resto (la barra suma justo 'volumen').
		var porPrecio = Math.Round(volumen / precios.Count, 4);
		var dt = TimeSpan.FromSeconds(seg / precios.Count);
		var restante = volumen;

		for (var i = 0; i < precios.Count; i++)
		{
			var vol = i == precios.Count - 1 ? restante : porPrecio;
			restante -= vol;
			var compra = Math.Round(vol * (1 + deltaPct) / 2, 4);
			var venta = vol - compra;

			if (compra > 0)
				m.Agregar(t, precios[i], compra, true);

			if (venta > 0)
				m.Agregar(t, precios[i], venta, false);

			t += dt;
		}
	}
}

internal static class PruebasV2Barras
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("S2 Barras de volumen: reparto, delta, Bid/Ask por precio, medianas RTH");
		var m = new MotorBarrasVolumen { Tick = DatosV2.Tick, ContratosPorBarra = 500 };
		var t = DatosV2.Utc(new DateTime(2026, 10, 30, 9, 31, 0));
		m.Agregar(t, 21400m, 300, true);
		m.Agregar(t.AddSeconds(1), 21400.25m, 700, false);
		c.Check("1000 contratos = 2 barras de 500 (el trade de 700 se reparte)", $"{m.Barras.Count} {DatosV2.F(m.Barras[0].Volumen)} {DatosV2.F(m.Barras[1].Volumen)}", "2 500 500");
		c.Check("barra 1: delta +300 - 200 = +100", DatosV2.F(m.Barras[0].Delta), "100");
		c.Check("barra 1: Bid/Ask por precio", string.Join(" ", m.Barras[0].Niveles.Select(n => $"{n.Key * DatosV2.Tick}:{DatosV2.F(n.Value.Bid)}/{DatosV2.F(n.Value.Ask)}")), "21400.00:0/300 21400.25:200/0");
		c.Check("barra 2: delta -500", DatosV2.F(m.Barras[1].Delta), "-500");

		var mm = new MotorBarrasVolumen { Tick = DatosV2.Tick, ContratosPorBarra = 100, MedianLookbackBars = 30 };
		var ta = DatosV2.Utc(new DateTime(2026, 10, 30, 7, 0, 0));

		for (var i = 0; i < 5; i++)
			DatosV2.Barra(mm, ref ta, 21400, 21410, 600, 0);

		var tr = DatosV2.Utc(new DateTime(2026, 10, 30, 9, 31, 0));

		for (var i = 0; i < 9; i++)
			DatosV2.Barra(mm, ref tr, 21400, 21401, 20, 0);

		var (dur, rango) = mm.Medianas(mm.Barras.Count);
		c.Check("medianas solo con barras RTH (las de 07:00 no cuentan; duración = primer a último trade)", $"{Math.Round(dur)} {rango}", "16 4");

		c.Seccion("S2 APROX: vela de 1 minuto con footprint a trades sintéticos");
		var vela = new Vela
		{
			InicioUtc = tr, FinUtc = tr.AddMinutes(1), Open = 21400, High = 21401, Low = 21399.5m, Close = 21400.75m, Volume = 120, Delta = 20,
			Niveles = new SortedDictionary<long, (double Bid, double Ask)>
			{
				[(long)(21399.5m / DatosV2.Tick)] = (10, 0), [(long)(21399.75m / DatosV2.Tick)] = (10, 5), [(long)(21400m / DatosV2.Tick)] = (15, 15),
				[(long)(21400.25m / DatosV2.Tick)] = (5, 15), [(long)(21400.5m / DatosV2.Tick)] = (5, 10), [(long)(21400.75m / DatosV2.Tick)] = (5, 10),
				[(long)(21401m / DatosV2.Tick)] = (0, 10)
			}
		};
		var aprox = MotorBarrasVolumen.Aproximar(vela, DatosV2.Tick).ToList();
		c.Check("se conserva el volumen (115) y el delta (+15) del footprint", $"{DatosV2.F(aprox.Sum(x => x.Volumen))} {DatosV2.F(aprox.Sum(x => x.Compra == true ? x.Volumen : -x.Volumen))}", "115 15");
		c.Check("camino: apertura → extremo más cercano (mínimo) → máximo → cierre", DatosV2.F(aprox[0].Precio) + " " + DatosV2.F(aprox.Last().Precio), "21400 21401");
		c.Check("dentro del minuto", aprox.All(x => x.Utc >= vela.InicioUtc && x.Utc < vela.FinUtc).ToString(), "True");

		c.Seccion("S2 Almacén de trades: historial delante, vivo detrás, mismo resultado al recalcular");
		var a = new AlmacenTicks();
		a.Agregar(new TickV2(tr.AddSeconds(10), 21400, 50, true));
		a.Agregar(new TickV2(tr.AddSeconds(20), 21401, 50, false));
		c.Check("un trade con hora anterior en vivo se ignora", a.Agregar(new TickV2(tr.AddSeconds(5), 21400, 1, true)).ToString(), "False");
		var n = a.AgregarHistorial(new[] { new TickV2(tr.AddSeconds(1), 21399, 10, false), new TickV2(tr.AddSeconds(15), 21400, 10, true) });
		c.Check("del historial solo entra lo anterior al primero en vivo", $"{n} {a.Count} {DatosV2.F(a.Ticks[0].Precio)}", "1 3 21399");

		c.Seccion("S2 Vela compuesta: POC, VA, tercios, subasta, stacked contiguo (P11)");
		var mc = new MotorBarrasVolumen { Tick = DatosV2.Tick, ContratosPorBarra = 1000 };
		var tc = tr;
		void T(decimal p, decimal bid, decimal ask)
		{
			if (bid > 0)
				mc.Agregar(tc, p, bid, false);

			if (ask > 0)
				mc.Agregar(tc, p, ask, true);

			tc = tc.AddSeconds(1);
		}

		// Máximo 21402 con una sola venta (subasta terminada); POC abajo (21400.25).
		T(21402.00m, 1, 0); T(21401.75m, 5, 5); T(21401.50m, 5, 5); T(21401.25m, 5, 5); T(21401.00m, 10, 10);
		T(21400.75m, 10, 10); T(21400.50m, 10, 40); T(21400.25m, 10, 60); T(21400.00m, 30, 30);
		var comp = Compuesta.Construir(mc.Barras.Concat(new[] { mc.Actual! }).ToList(), 0, 0, DatosV2.Tick, 0.70m)!;
		c.Check("POC y VA", $"{DatosV2.F(comp.Poc)} {DatosV2.F(comp.VaBajo)}-{DatosV2.F(comp.VaAlto)}", "21400.25 21400-21400.5");
		c.Check("POC y VA en el tercio inferior, no en el superior", $"{comp.PocYVaEnTercio(-1)} {comp.PocYVaEnTercio(+1)}", "True False");
		c.Check("subasta terminada en el máximo (1 contrato)", comp.SubastaTerminada(+1, 2).ToString(), "True");
		c.Check("subasta sin terminar en el mínimo (30 x 30)", comp.SubastaTerminada(-1, 2).ToString(), "False");

		var contiguo = new SortedDictionary<long, (decimal Bid, decimal Ask)>
		{
			[400] = (10, 2), [401] = (10, 40), [402] = (10, 40), [403] = (10, 40), [404] = (10, 5)
		};
		c.Check("stacked de compra en 3 ticks seguidos", VelaCompuesta.StackedDe(contiguo, DatosV2.Tick, 3, 5, 3).Count(z => z.Compra).ToString(), "1");

		// T14: imbalances en 401, 403 y 405 (no contiguos): no es stacked.
		var salteado = new SortedDictionary<long, (decimal Bid, decimal Ask)>
		{
			[400] = (10, 2), [401] = (10, 40), [402] = (40, 10), [403] = (10, 40), [404] = (40, 10), [405] = (10, 40)
		};
		c.Check("T14 imbalances no contiguos: no cuentan como stacked", VelaCompuesta.StackedDe(salteado, DatosV2.Tick, 3, 5, 3).Count(z => z.Compra).ToString(), "0");

		c.Seccion("S2 Fases: F1 llegada, F2 freno en el extremo, F3 arranque (y F3 lenta)");
		var p = new ParametrosV2();
		var mf = new MotorBarrasVolumen { Tick = DatosV2.Tick, ContratosPorBarra = 100 };
		var tf = tr;

		for (var i = 0; i < 6; i++)
			DatosV2.Barra(mf, ref tf, 21400 + i * 2, 21402 + i * 2, 20, 0.3m);   // llegada alcista hasta 21412

		DatosV2.Barra(mf, ref tf, 21411.5m, 21412m, 20, 0);                    // F2
		DatosV2.Barra(mf, ref tf, 21411.25m, 21411.75m, 20, -0.1m);
		DatosV2.Barra(mf, ref tf, 21411.5m, 21411m, 20, 0);
		DatosV2.Barra(mf, ref tf, 21411m, 21407m, 4, -0.6m);                  // F3: 16 ticks, 4 s, delta -60 %
		var med = (Duracion: 20.0, Rango: 8.0);
		var f = DetectorFases.Detectar(mf.Barras, 0, mf.Barras.Count - 1, -1, 21412m, p, med, DatosV2.Tick, false);
		c.Check("F1 = 6 barras, F2 = 3 barras en el extremo, F2 válida", $"{f.F1Barras} {f.F2Barras} {f.F2EnExtremo} {f.F2Valida}", "6 3 True True");
		c.Check("F3 en la barra 9 (rápida, 16 ticks, -60 %)", $"{f.F3Indice} {f.F3RangoTicks} {DatosV2.F(f.F3DeltaPct)}", "9 16 -0.6");

		var ml = new MotorBarrasVolumen { Tick = DatosV2.Tick, ContratosPorBarra = 100 };
		var tl = tr;

		for (var i = 0; i < 6; i++)
			DatosV2.Barra(ml, ref tl, 21400 + i * 2, 21402 + i * 2, 20, 0.3m);

		DatosV2.Barra(ml, ref tl, 21411m, 21407m, 30, -0.6m);                 // misma barra pero lenta (30 s)
		var fl = DetectorFases.Detectar(ml.Barras, 0, ml.Barras.Count - 1, -1, 21412m, p, med, DatosV2.Tick, false);
		c.Check("F3 lenta: no hay F3 y se marca la penalización", $"{fl.F3} {fl.F3Lenta}", "False True");
	}
}
