using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>v3 S3/S4: instrumento, perfiles por familia, calibración (pruebas 1-7, 9, 10 y 30 del prompt v3).</summary>
internal static class PruebasV3Instrumentos
{
	/// <summary>Velas de 1 minuto RTH de 'dias' sesiones (días hábiles) con volumen 'vol' y delta 'delta'.</summary>
	private static List<(DateTime, decimal, decimal)> Sesiones(int dias, Func<int, decimal> vol, decimal delta = 20)
	{
		var r = new List<(DateTime, decimal, decimal)>();
		var d = new DateTime(2026, 9, 1);

		for (var n = 0; n < dias; d = d.AddDays(1))
		{
			if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
				continue;

			for (var m = 0; m < 390; m++)
				r.Add((Utc(d.Add(new TimeSpan(9, 30, 0)).AddMinutes(m)), vol(m), m % 2 == 0 ? delta : -delta));

			n++;
		}

		return r;
	}

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v3 S3 Detección del instrumento (pruebas 1-3, 6)");
		string D(string s, string alias = "") => Instrumentos.Detectar(s, alias) is var (r, k) ? $"{r}:{k}" : "";
		c.Check("NQZ6 / ESZ6 / YMZ6", $"{D("NQZ6")} {D("ESZ6")} {D("YMZ6")}", "NQ:NQZ6 ES:ESZ6 YM:YMZ6");
		c.Check("micros: la raíz más larga gana (MES no es ES)", $"{D("MNQH7")} {D("MESZ6")} {D("MYMZ26")}", "MNQ:MNQH7 MES:MESZ6 MYM:MYMZ26");
		c.Check("con espacios y alias configurable", $"{D("NQ 12-26")} {D("ENQZ6", "ENQ=NQ;EP=ES")} {D("EPZ6", "ENQ=NQ;EP=ES")}", "NQ:NQ12-26 NQ:ENQZ6 ES:EPZ6");
		c.Check("no soportados: CL, ESTX50, vacío", $"{D("CLZ6")} {D("ESTX50").Split(':')[0]} {D("")}", "Desconocido:CLZ6 Desconocido Desconocido:");
		var cl = EstadoInstrumento.Crear("CLZ6", "", 0.01m, null, 500);
		c.Check("prueba 6: sin reconocer = bloqueo de señales", cl.Bloqueo ?? "", Instrumentos.NoSoportado);
		var sim = new SimMotor();
		sim.M.Instrumento = cl;
		sim.Ruptura();
		var setups = sim.Setups;
		c.Check("prueba 6: con el motor, la ruptura no arma nada operable (todo INVALIDADO con el aviso)",
			setups.Count > 0 && setups.All(x => x.Estado == EstadoModelo.Invalidado && x.Motivo == Instrumentos.NoSoportado) ? "bloqueado" : $"{setups.Count} setups, {setups.Count(x => x.Pendiente)} pendientes", "bloqueado");

		c.Seccion("v3 S3 Especificación y valor del contrato (prueba 5)");
		c.Check("valor del tick NQ / ES / YM / MNQ / MES / MYM (USD)",
			string.Join(" ", new[] { "NQ", "ES", "YM", "MNQ", "MES", "MYM" }.Select(s => F(EstadoInstrumento.Crear(s + "Z6", "", Instrumentos.Referencia[Enum.Parse<RaizInstrumento>(s)].Tick, null, 1).ValorTick))),
			"5 12.5 5 0.5 1.25 0.5");
		var ymMal = EstadoInstrumento.Crear("YMZ6", "", 0.25m, null, 150);
		c.Check("YM con tick de ATAS 0.25 (no coincide con 1.00): no validado, valor 0", $"{ymMal.TickValidado} {F(ymMal.ValorTick)}", "False 0");
		var plan = CalculadoraRiesgo.Calcular(new ParametrosV2 { RiskAmountUSD = 200 }, 44000, 43990, 1, ymMal.ValorTick);
		c.Check("sin valor validado: 0 contratos y el aviso", $"{plan.Contratos} {plan.Texto(RaizInstrumento.YM).StartsWith(Instrumentos.NoValidado, StringComparison.Ordinal)}", "0 True");
		var es = CalculadoraRiesgo.Calcular(new ParametrosV2 { RiskAmountUSD = 200 }, 6000, 5998, 0.25m, Instrumentos.Referencia[RaizInstrumento.ES].ValorTick);
		c.Check("prueba 34: ES, 200 USD con stop de 8 ticks = 2 ES (12,50 USD/tick)", $"{es.StopTicks} {es.Contratos} {es.Texto(RaizInstrumento.ES)}", "8 2 Riesgo 200 USD | 2 ES | stop 8 ticks");
		var poco = CalculadoraRiesgo.Calcular(new ParametrosV2 { RiskAmountUSD = 50 }, 6000, 5998, 0.25m, 12.5m);
		c.Check("riesgo insuficiente en ES: usar MES", poco.Texto(RaizInstrumento.ES).StartsWith("RIESGO INSUFICIENTE: usar MES", StringComparison.Ordinal).ToString(), "True");

		c.Seccion("v3 S4 Perfiles por familia: solo los del instrumento abierto (pruebas 1-4, 10)");
		var perfiles = new PerfilesInstrumento();
		var p2 = new ParametrosV2();
		string Efectivos(ParametrosV2 p) => $"{p.VolumePerBar} {p.MinVolBeyondFloor} {p.SuperBigTradeMinContracts} {p.ImbalanceMinVolume} {p.MaxSweepTicks} {p.RecoveryTicks}";
		perfiles.ES.AplicarA(p2, 1500);
		c.Check("al abrir ES: los umbrales de ES", Efectivos(p2), "1500 150 400 15 8 6");
		perfiles.NQ.AplicarA(p2, 500);
		c.Check("prueba 4: de ES a NQ no queda nada de ES", Efectivos(p2), "500 50 150 5 24 16");
		perfiles.YM.AplicarA(p2, 150);
		c.Check("al abrir YM: los de YM", Efectivos(p2), "150 15 40 2 12 8");
		c.Check("prueba 10: los contratos de NQ no se reutilizan en ES ni YM",
			$"{perfiles.ES.MinVolBeyondFloor != perfiles.NQ.MinVolBeyondFloor} {perfiles.YM.SuperBigTradeMinContracts != perfiles.NQ.SuperBigTradeMinContracts} {perfiles.ES.VolumePerBar != perfiles.NQ.VolumePerBar}",
			"True True True");
		perfiles.ES.MinVolBeyondFloor = 999;
		c.Check("cambiar ES no toca NQ ni MES", $"{perfiles.NQ.MinVolBeyondFloor} {perfiles.MES.MinVolBeyondFloor}", "50 150");

		c.Seccion("v3 S4 Calibración de volumen y delta (pruebas 7, 9, 30)");
		var nq = PerfilInstrumento.PorDefecto(RaizInstrumento.NQ);
		var veinte = Sesiones(20, m => m < 30 ? 3000 : 1500);
		var cal = Calibrador.Calcular(veinte, nq, "NQZ6", 0.33m, new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc));
		c.Check("20 sesiones: CALIBRADO; mediana 1500/min; AUTO = 1500 × 0,33 = 500 (a decenas)", $"{cal.Texto} {cal.Sesiones} {F(cal.MedianaVolumenMinuto)} {cal.VolumePerBarAuto}", "CALIBRADO 20 1500 500");
		c.Check("se usa el AUTO", Calibrador.VolumenPorBarra(nq, cal).ToString(), "500");
		var cinco = Calibrador.Calcular(Sesiones(5, _ => 1500), nq, "NQZ6", 0.33m, DateTime.UtcNow);
		c.Check("prueba 7: 5 sesiones = CALIBRACIÓN INSUFICIENTE (y nunca 'validada')", $"{cinco.Texto} {EstadoInstrumento.Crear("NQZ6", "", 0.25m, cinco, 500).Provisional}", "CALIBRACIÓN INSUFICIENTE True");
		var manual = PerfilInstrumento.PorDefecto(RaizInstrumento.NQ);
		manual.VolumePerBarMode = ModoVolumenBarra.ManualPorInstrumento;
		manual.VolumePerBar = 700;
		c.Check("modo manual: CONFIGURACIÓN MANUAL y su valor", $"{Calibrador.Calcular(veinte, manual, "NQZ6", 0.33m, DateTime.UtcNow).Texto} {Calibrador.VolumenPorBarra(manual, cal)}", "CONFIGURACIÓN MANUAL 700");
		var vacio = Calibrador.Calcular(new List<(DateTime, decimal, decimal)>(), PerfilInstrumento.PorDefecto(RaizInstrumento.ES), "ESZ6", 0.33m, DateTime.UtcNow);
		c.Check("sin datos de RTH: el provisional de ES (no el de NQ)", Calibrador.VolumenPorBarra(PerfilInstrumento.PorDefecto(RaizInstrumento.ES), vacio).ToString(), "1500");
		var limite = Calibrador.Calcular(Sesiones(20, _ => 100000), nq, "NQZ6", 0.33m, DateTime.UtcNow);
		c.Check("AUTO dentro de sus límites de seguridad", limite.VolumePerBarAuto.ToString(), "5000");

		var abre = Utc(new DateTime(2026, 9, 28, 9, 35, 0));
		c.Check("prueba 9: volumen relativo por franja (09:30 = 3000/min) y DeltaRelativo",
			$"{F(cal.VolumenRelativo(4500, abre))} {F(cal.VolumenRelativo(3000, Utc(new DateTime(2026, 9, 28, 11, 0, 0))))} {F(cal.DeltaRelativo(-40))}", "1.5 2 2");
		c.Check("prueba 9: DeltaPct y AggressorShare", $"{F(Medidas.DeltaPct(-30, 120))} {F(Medidas.AggressorShare(90, 120))}", "-0.25 0.75");
		var json = cal.ToJson();
		var vuelta = CalibracionInstrumento.DeJson(json);
		c.Check("prueba 30: la calibración de cada familia se guarda y se recupera igual",
			vuelta is null ? "nada" : $"{vuelta.Raiz} {vuelta.Contrato} {vuelta.Sesiones} {vuelta.VolumePerBarAuto} {vuelta.Texto} {vuelta.MedianaVolumenPorFranja.Count == cal.MedianaVolumenPorFranja.Count}",
			"NQ NQZ6 20 500 CALIBRADO True");
		c.Check("un JSON dañado no rompe nada", (CalibracionInstrumento.DeJson("{no") is null).ToString(), "True");

		c.Seccion("v3 S4 PARAMETROS.md: los valores por defecto de cada perfil son los del código");
		var ruta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SessionSweeps", "PARAMETROS.md"));

		if (!File.Exists(ruta))
			ruta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SessionSweeps", "PARAMETROS.md"));

		var filas = File.Exists(ruta) ? File.ReadAllLines(ruta).Where(l => l.StartsWith("| `", StringComparison.Ordinal)).Select(l => l.Split('|')).ToList() : new List<string[]>();
		var distintos = new List<string>();
		var revisados = 0;

		foreach (var raiz in new[] { RaizInstrumento.NQ, RaizInstrumento.ES, RaizInstrumento.YM, RaizInstrumento.MNQ, RaizInstrumento.MES, RaizInstrumento.MYM })
		{
			var perfil = PerfilInstrumento.PorDefecto(raiz);

			foreach (var prop in typeof(PerfilInstrumento).GetProperties().Where(x => x.CanWrite))
			{
				var fila = filas.FirstOrDefault(f => f[1].Trim() == $"`{raiz}_{prop.Name}`");

				if (fila is null)
				{
					distintos.Add($"{raiz}_{prop.Name}: falta");
					continue;
				}

				revisados++;
				var codigo = Convert.ToString(prop.GetValue(perfil), CultureInfo.InvariantCulture);

				if (codigo != fila[3].Trim())
					distintos.Add($"{raiz}_{prop.Name}: código {codigo} / doc {fila[3].Trim()}");
			}
		}

		c.Check("los 6 perfiles × 24 valores coinciden con PARAMETROS.md (si falla: generar_parametros_v2.py)",
			distintos.Count == 0 ? $"{revisados} iguales" : string.Join("; ", distintos.Take(5)), "144 iguales");
	}
}

/// <summary>v3 S5: bus por instrumento y contrato, latido con versión, deduplicación (prueba 27).</summary>
internal static class PruebasV3Bus
{
	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v3 S5 Bus: aislado por contrato, VERSIÓN DISTINTA, deduplicación (prueba 27)");
		SuiteEventBus.Reiniciar();
		var t = new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);
		var nq = SuiteEventBus.Para("NQZ6");
		var es = SuiteEventBus.Para("ESZ6");
		nq.Publicar(new IcebergEvent { Id = 1, Fuente = FuenteSuite.Iceberg, Utc = t, CambioUtc = t, Version = 1, Precio = 21400, Compra = true, Estado = "AGUANTANDO" });
		c.Check("un evento de NQZ6 no aparece en ESZ6", $"{nq.Todos<IcebergEvent>().Count} {es.Todos<IcebergEvent>().Count}", "1 0");

		nq.Latido(FuenteSuite.Iceberg, true, t);
		nq.Latido(FuenteSuite.Stops, true, t, "1.9.0");
		c.Check("latido con la versión del bus: CARGADO; con otra: VERSIÓN DISTINTA",
			$"{nq.Estado(FuenteSuite.Iceberg, t.AddSeconds(1))} {nq.Estado(FuenteSuite.Stops, t.AddSeconds(1))}", "Cargado VersionDistinta");

		// El mismo iceberg publicado por dos gráficos (Id distinto): cuenta una vez.
		nq.Publicar(new IcebergEvent { Id = 2, Fuente = FuenteSuite.Iceberg, Utc = t.AddMilliseconds(300), CambioUtc = t.AddSeconds(2), Version = 3, Precio = 21400, Compra = true, Estado = "ROTO" });
		nq.Publicar(new IcebergEvent { Id = 3, Fuente = FuenteSuite.Iceberg, Utc = t.AddSeconds(5), CambioUtc = t.AddSeconds(5), Version = 1, Precio = 21400, Compra = true, Estado = "ACTIVO" });
		nq.Publicar(new IcebergEvent { Id = 4, Fuente = FuenteSuite.Iceberg, Utc = t, CambioUtc = t, Version = 1, Precio = 21400, Compra = false, Estado = "ACTIVO" });
		var fuentes = new FuentesBus("NQZ6", () => t.AddSeconds(3));
		var vistos = fuentes.Consultar<IcebergEvent>(t.AddMinutes(-1), t.AddMinutes(1));
		c.Check("prueba 27: mismo precio, dirección y segundo = uno (el más reciente); otro segundo u otra dirección = distintos",
			string.Join(" ", vistos.Select(e => $"{e.Id}:{e.Estado}")), "4:ACTIVO 2:ROTO 3:ACTIVO");
		nq.Publicar(new StopsEvent { Id = 9, Fuente = FuenteSuite.Stops, Utc = t, CambioUtc = t, Version = 1, Extremo = 21410, Compra = true });
		c.Check("los eventos de una fuente con otra versión no se usan", fuentes.Consultar<StopsEvent>(t.AddMinutes(-1), t.AddMinutes(1)).Count.ToString(), "0");

		var edge = new EdgeZone { Id = 5, Fuente = FuenteSuite.Thermal, Utc = t, CambioUtc = t, Version = 1, RangoId = 1, Precio = 21450, Bajo = 21449.5m, Alto = 21450.5m, Lado = EdgeZone.TechoHvn };
		nq.Publicar(edge);
		c.Check("EdgeZone viaja por el bus", $"{nq.Todos<EdgeZone>().Single().Lado} {nq.Todos<EdgeZone>().Single().Estado}", "TECHO_HVN ACTIVO");
		SuiteEventBus.Reiniciar();
	}
}
