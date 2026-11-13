using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using static DatosV2;

/// <summary>v3 S11/S12: Modelo A (barrido) y Modelo B (LVN / EDGE / FVG), salidas y riesgo (pruebas 20-23 del prompt v3).</summary>
internal static class PruebasV3Modelos
{
	private static object? Llamar(MotorSweeps m, string metodo, params object?[] args) =>
		typeof(MotorSweeps).GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(m, args);

	private static EvaluacionBarrido Barrido(EstadoBarrido estado, decimal l = 21380, decimal extremo = 21378) => new()
	{
		Id = 4242, Nivel = new NivelLiquidez { Id = 7, Precio = l, Lado = Lado.Sellside, Origen = "Dia anterior", Grupo = "dia" }, Signo = -1, L = l, Extremo = extremo,
		ExtremoPierna = extremo, Estado = estado, T0Utc = Utc(new DateTime(2026, 10, 30, 9, 50, 0)), Congelada = true
	};

	public static void Ejecutar(Comprobador c)
	{
		var utc = Utc(new DateTime(2026, 10, 30, 9, 55, 0));

		c.Seccion("v3 S11 Modelo A: solo con un BARRIDO REAL o PREMIUM (prueba 22)");
		foreach (var (estado, esperado) in new[] { (EstadoBarrido.Indeterminado, "0"), (EstadoBarrido.SinLiquidez, "0"), (EstadoBarrido.Real, "1 M1 - BARRIDO M1"), (EstadoBarrido.Premium, "1 M1 - BARRIDO M1") })
		{
			var s = new SimMotor(sinBarridos: true);
			s.M.Leer(m => { m.ModelosAlEstado(Barrido(estado), "CONFIRMADO", utc); return 0; });
			var a = s.Setups.Where(x => x.Modelo == "MOD1").ToList();
			c.Check($"{estado}: Modelo 1", a.Count == 0 ? "0" : $"{a.Count} {a[0].Etiqueta} {a[0].ModeloOperativo}", esperado);
		}

		var apagado = new SimMotor(sinBarridos: true, ajustar: cfg => cfg.V2.EnableModelA = false);
		apagado.M.Leer(m => { m.ModelosAlEstado(Barrido(EstadoBarrido.Premium), "CONFIRMADO", utc); return 0; });
		c.Check("Modelo 1 apagado: el setup queda a la vista, invalidado", apagado.Setups.Single().Motivo, "MODELO M1 APAGADO");

		c.Seccion("v3 S12 Modelo B en un EDGE del Thermal; sin gatillo cumplido antes de la confirmación (prueba 21)");
		SimMotor ConEdge(bool activo)
		{
			var s = new SimMotor(sinBarridos: true, ajustar: cfg => cfg.V2.ModelBEnableEdge = activo);
			s.M.Leer(m => { m.SesgoForzado = 1; return 0; });
			var t = Utc(new DateTime(2026, 10, 30, 9, 30, 0));
			s.M.EdgeDesdeSuite(new EdgeZone { Id = 77, Fuente = FuenteSuite.Thermal, Utc = t, CambioUtc = t, Version = 1, RangoId = 1, Precio = 21404, Bajo = 21403.5m, Alto = 21404.5m, Lado = EdgeZone.PisoHvn });
			s.Minuto(new DateTime(2026, 10, 30, 9, 41, 0), SimMotor.Camino(21402, 21412, 8, 2));
			s.Minuto(new DateTime(2026, 10, 30, 9, 42, 0), SimMotor.Camino(21412, 21404, 2, 8));
			return s;
		}

		var b = ConEdge(true).Setups.Where(x => x.Zona.Tipo == "EDGE").ToList();
		c.Check("retroceso al EDGE a favor del sesgo: M2 - EDGE", b.Count > 0 ? $"{b[0].Etiqueta} {b[0].ModeloOperativo} {b[0].Zona.EdgeId}" : "sin setup", "M2 - EDGE M2 77");
		c.Check("prueba 21: sin gatillo VÁLIDO no hay entrada ni gatillo cumplido",
			b.All(x => x.Gatillo?.Resultado == ResultadoGatillo.Valido || (x.Entrada is null && x.Estado != EstadoModelo.Entrada)).ToString(), "True");
		c.Check("con ModelBEnableEdge apagado no se arma", ConEdge(false).Setups.Count(x => x.Zona.Tipo == "EDGE").ToString(), "0");

		c.Seccion("v3 S7 Cabecera del panel: gatillo (LÍNEA 2) con un Modelo B en un EDGE");
		var tocado = ConEdge(true);
		var linea2 = tocado.M.Panel(new OpcionesPanel()).Lineas[1];
		c.Check("el precio tocó el EDGE sin gatillo VÁLIDO: nunca 'CUMPLIDO'", linea2.Texto.Contains("CUMPLIDO", StringComparison.Ordinal) && !linea2.Texto.StartsWith("NO SE HA", StringComparison.Ordinal) ? linea2.Texto : "no", "no");
		var lejos = new SimMotor(sinBarridos: true);
		lejos.M.Leer(m => { m.SesgoForzado = 1; return 0; });
		var tEdge = Utc(new DateTime(2026, 10, 30, 9, 30, 0));
		lejos.M.EdgeDesdeSuite(new EdgeZone { Id = 78, Fuente = FuenteSuite.Thermal, Utc = tEdge, CambioUtc = tEdge, Version = 1, RangoId = 1, Precio = 21396, Bajo = 21395.5m, Alto = 21396.5m, Lado = EdgeZone.PisoHvn });
		lejos.Minuto(new DateTime(2026, 10, 30, 9, 41, 0), SimMotor.Camino(21402, 21412, 8, 2));
		lejos.Minuto(new DateTime(2026, 10, 30, 9, 42, 0), SimMotor.Camino(21412, 21409, 2, 8));
		// Un FVG+EDGE de Modelo B armado por debajo (los FVG se arman al formarse, antes de que el precio llegue).
		lejos.M.Leer(m =>
		{
			m.Setups.Add(new SetupModelo { Id = 999, Modelo = "MOD2", Etiqueta = "B - FVG+EDGE", Signo = 1, Zona = new ZonaOperable { Tipo = "FVG+EDGE", Bajo = 21395.5m, Alto = 21396.5m, Signo = 1, EdgeId = 78 } });
			return 0;
		});
		var setupLejos = lejos.Setups.FirstOrDefault(x => x.Id == 999);
		c.Check("zona armada sin tocar: 'NO SE HA CUMPLIDO — EN CAMINO A EDGE PISO HVN 21,396.00' con la distancia",
			$"{setupLejos?.Estado} | {lejos.M.Panel(new OpcionesPanel()).Lineas[1].Texto}", "Armado | NO SE HA CUMPLIDO — EN CAMINO A EDGE PISO HVN 21,396.00 · a 52 ticks / 13 pts");

		c.Seccion("v3 S12 FVG+LVN > FVG+EDGE > FVG; BFVG+EDGE (prueba 20)");
		var lvn = new RepositorioLvn();
		var edge = new RepositorioEdge();
		var t0 = DateTime.UtcNow;
		edge.Aplicar(new EdgeZone { Id = 1, Fuente = FuenteSuite.Thermal, Utc = t0, CambioUtc = t0, RangoId = 1, Precio = 21410, Bajo = 21409.5m, Alto = 21410.5m });
		var f = new FvgV2 { Id = 3, Marco = 1, Alcista = true, Bajo = 21409, Alto = 21411, Utc = t0 };
		var p = new ParametrosV2();
		c.Check("FVG que toca un EDGE (sin LVN): FVG+EDGE", MotorFvgV2.ZonaDeFvg(1, f, lvn, p, Tick, false, null, "MOD2", edge).Tipo, "FVG+EDGE");
		var bfvg = MotorFvgV2.ZonaDeFvg(1, f, lvn, p, Tick, true, 9, "BFVG", edge);
		c.Check("BFVG con EDGE: BFVG+EDGE y la bonificación de confluencia", $"{bfvg.Tipo} {bfvg.Calidad}", $"BFVG+EDGE {MotorFvgV2.CalidadTipo("BFVG+EDGE") + p.ConfluenceBonus}");
		lvn.Aplicar(PruebasV2Lvn.Z(5, 1, 21410, 21410.5m, manual: false));
		c.Check("con LVN también: manda FVG+LVN", MotorFvgV2.ZonaDeFvg(1, f, lvn, p, Tick, false, null, "MOD2", edge).Tipo, "FVG+LVN");
		c.Check("orden de calidad FVG+LVN > FVG+EDGE > LVN > EDGE > FVG > EMA > NIVEL",
			string.Join(" ", new[] { "FVG+LVN", "FVG+EDGE", "LVN", "EDGE", "FVG", "EMA", "NIVEL" }.Select(MotorFvgV2.CalidadTipo)), "40 35 30 25 20 15 10");
		var provisional = new RepositorioLvn();
		provisional.Aplicar(new LvnZone { Id = 8, Fuente = FuenteSuite.Thermal, Utc = t0, CambioUtc = t0, RangoId = 1, Bajo = 21410, Alto = 21410.5m, Centro = 21410.25m, Provisional = true });
		c.Check("un LVN PROVISIONAL no hace FVG+LVN", MotorFvgV2.ZonaDeFvg(1, f, provisional, p, Tick, false, null, "MOD2").Tipo, "FVG");

		c.Seccion("v3 S11 Riesgo del Modelo A: stop más allá del extremo; TP a 2R (17.2)");
		var e = new SimMotor(sinBarridos: true);
		var setup = new SetupModelo { Id = 1, Modelo = "BFVG", Etiqueta = "A - BFVG", Signo = 1, Barrido = Barrido(EstadoBarrido.Real, 21390, 21385), Zona = new ZonaOperable { Tipo = "BFVG", Bajo = 21388, Alto = 21389, Signo = 1 } };
		var gat = new AnalisisGatillo { Resultado = ResultadoGatillo.Valido, Tipo = TipoGatillo.Arranque, Entrada = 21390, Stop = 21388, Tp1 = 21394, Direccion = 1 };
		e.M.Leer(m => { m.Setups.Add(setup); Llamar(m, "Entrar", setup, gat, new BarraVolumen { FinUtc = utc }); return 0; });
		c.Check("A: stop = extremo 21385 - 3 ticks; TP = 2R desde el nuevo riesgo", $"{setup.TextoEstado} {F(setup.Stop)} {F(setup.Tp1)} {setup.Tp2 is null}", "ENTRADA 21384.25 21401.5 True");

		c.Seccion("v3 S11 Salida: TP o EMA 15, lo primero (17.2)");
		void Gestionar(MotorSweeps m, SetupModelo s, decimal alto, decimal bajo) => Llamar(m, "Gestionar", s, new BarraVolumen { FinUtc = utc, High = alto, Low = bajo });
		var abierta = new SetupModelo { Id = 2, Modelo = "MOD2", Etiqueta = "B - LVN", Signo = 1, Estado = EstadoModelo.Entrada, Entrada = 21400, Stop = 21395, Tp1 = 21410, Zona = new ZonaOperable() };
		e.M.Leer(m => { Gestionar(m, abierta, 21410.25m, 21401); return 0; });
		c.Check("toca el TP de 2R: CERRADO con TP", $"{abierta.TextoEstado} {abierta.Resultado}", "CERRADO TP");
		var soloEma = new SimMotor(sinBarridos: true, ajustar: cfg => cfg.V2.ExitMode = ModoSalida.SoloEma15);
		var sigue = new SetupModelo { Id = 3, Modelo = "MOD2", Etiqueta = "B - LVN", Signo = 1, Estado = EstadoModelo.Entrada, Entrada = 21400, Stop = 21395, Tp1 = 21410, Zona = new ZonaOperable() };
		soloEma.M.Leer(m => { Gestionar(m, sigue, 21410.25m, 21401); return 0; });
		c.Check("ExitMode solo EMA 15: el TP no la cierra", sigue.TextoEstado, "ENTRADA");
		var stop = new SetupModelo { Id = 4, Modelo = "MOD2", Etiqueta = "B - LVN", Signo = 1, Estado = EstadoModelo.Entrada, Entrada = 21400, Stop = 21395, Tp1 = 21410, Zona = new ZonaOperable() };
		e.M.Leer(m => { Gestionar(m, stop, 21411, 21394.75m); return 0; });
		c.Check("toca stop y TP en la misma barra: STOP (conservador); el stop no se movió", $"{stop.Resultado} {F(stop.Stop)}", "STOP 21395");

		c.Seccion("v3 S12 Intentos máximos por zona");
		var intentos = new SimMotor(sinBarridos: true, ajustar: cfg => cfg.V2.MaxAttemptsPerZone = 1);
		var zona = new ZonaOperable { Tipo = "LVN", Bajo = 21400, Alto = 21401, Signo = 1 };
		var resultado = intentos.M.Leer(m =>
		{
			m.Setups.Add(new SetupModelo { Id = 50, Modelo = "MOD2", Signo = 1, Zona = zona, EntradaUtc = utc, Estado = EstadoModelo.Cerrado });
			var nuevo = (SetupModelo)Llamar(m, "Armar", "MOD2", "B - LVN", new ZonaOperable { Tipo = "LVN", Bajo = 21400, Alto = 21401, Signo = 1 }, null, utc, utc.AddMinutes(30), true)!;
			return $"{nuevo.TextoEstado}:{nuevo.Motivo}";
		});
		c.Check("una entrada ya hecha en esa zona y dirección: el siguiente no se arma", resultado, "INVALIDADO:máximo de 1 intentos en la zona");
	}
}
