using System;
using System.Collections.Generic;
using System.Linq;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>v3 S13: CVD en el motor (velas y divergencias para el panel) y la CVD Institutional Trigger Matrix (prueba 26).</summary>
internal static class PruebasV3Cvd
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v3 S13 CVD Trigger Matrix: macro + micro + absorción en la mecha + giro de delta");
		var p = new ParametrosV2 { CvdMatrixMacroStrength = 2, CvdMatrixMicroStrength = 1, CvdMatrixMacroValidity = 60, CvdMatrixMicroValidity = 7, CvdMatrixDeltaFlip = 400 };
		var m = new MatrizCvd();
		var t0 = Utc(new DateTime(2026, 11, 2, 10, 0, 0));
		var filas = new (decimal O, decimal H, decimal L, decimal C, decimal Delta, decimal Cvd)[]
		{
			(101, 102, 100, 101, 0, 0), (99, 100, 98, 99, -10, -10), (96, 97, 95, 96, -40, -50), (98, 99, 97, 98, 10, -40), (100, 101, 99, 100, 10, -30),
			(98, 99, 97, 98, -5, -35), (97, 98, 94, 97.5m, 15, -20), (97, 98, 96, 97.5m, 500, -15), (98, 99, 97, 98.5m, 5, -10)
		};

		for (var i = 0; i < filas.Length; i++)
		{
			var f = filas[i];
			var niveles = new List<NivelVela>();

			// La vela 6 (el segundo mínimo) tiene la mecha de abajo con el 60 % del volumen.
			if (i == 6)
			{
				for (var precio = 94m; precio < 97; precio += Tick)
					niveles.Add(new NivelVela(precio, 2.5m, 2.5m));

				niveles.Add(new NivelVela(97.25m, 20, 20));
			}

			m.Agregar(new VelaMatriz(t0.AddMinutes(i), f.O, f.H, f.L, f.C, i == 6 ? 100 : 50, f.Delta, f.Cvd, niveles), p, Tick);

			if (i == 7)
				c.Check("vela 7: micro alcista confirmada con absorción y giro de 500; aún sin macro", m.Micro is { } u ? $"{u.Alcista} {u.Absorcion} {F(u.Giro ?? 0)} {m.Macro is null}" : "sin micro", "True True 500 True");
		}

		var s = m.UltimaSenal;
		c.Check("con la macro alcista (vela 8): señal alcista con su delta, mecha y volumen en la mecha",
			s is null ? "sin señal" : $"{s.Alcista} {F(s.Delta)} {F(s.MechaPct)} {F(s.VolumenMechaPct)}", "True 500 75 60");
		c.Check("la micro se consume con el disparo; la macro sigue", $"{m.Micro is null} {m.Macro?.Alcista}", "True True");

		var sinAbsorcion = new MatrizCvd();

		for (var i = 0; i < filas.Length; i++)
			sinAbsorcion.Agregar(new VelaMatriz(t0.AddMinutes(i), filas[i].O, filas[i].H, filas[i].L, filas[i].C, 50, filas[i].Delta, filas[i].Cvd, new List<NivelVela>()), p, Tick);

		c.Check("sin volumen en la mecha (filtro 3): no hay señal", (sinAbsorcion.UltimaSenal is null).ToString(), "True");
		var es = new ParametrosV2 { CvdMatrixMacroStrength = 2, CvdMatrixMicroStrength = 1, CvdMatrixDeltaFlip = 1000 };
		var conEs = new MatrizCvd();

		for (var i = 0; i < filas.Length; i++)
			conEs.Agregar(new VelaMatriz(t0.AddMinutes(i), filas[i].O, filas[i].H, filas[i].L, filas[i].C, i == 6 ? 100 : 50, filas[i].Delta, filas[i].Cvd,
				i == 6 ? Enumerable.Range(0, 12).Select(k => new NivelVela(94 + k * Tick, 2.5m, 2.5m)).ToList() : new List<NivelVela>()), es, Tick);

		c.Check("con el umbral de delta de ES (1000) un giro de 500 no basta (umbral por instrumento)", (conEs.UltimaSenal is null).ToString(), "True");
		c.Check("ES/YM/MNQ traen su propio umbral de giro (no el de NQ)",
			string.Join(" ", new[] { RaizInstrumento.NQ, RaizInstrumento.ES, RaizInstrumento.YM, RaizInstrumento.MNQ }.Select(r => PerfilInstrumento.PorDefecto(r).CvdMatrixDeltaFlip)), "400 1000 100 800");

		c.Seccion("v3 S13 CVD del motor para el panel: velas de CVD y divergencias en espacio CVD (prueba 26)");
		var sim = new SimMotor(sinBarridos: true);
		var (velas, divs) = sim.M.TramoCvd(40);
		c.Check("el tramo trae las últimas 40 velas de CVD", velas.Count.ToString(), "40");
		c.Check("prueba 26: las divergencias del tramo se expresan en valores de CVD de esas velas (para dibujarlas sobre ellas)",
			divs.All(d => d.I1 >= 0 && d.I2 < velas.Count && (d.Alcista ? d.Cvd1 == velas[d.I1].Low && d.Cvd2 == velas[d.I2].Low : d.Cvd1 == velas[d.I1].High && d.Cvd2 == velas[d.I2].High)).ToString(), "True");
	}
}
