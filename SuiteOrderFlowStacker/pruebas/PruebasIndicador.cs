using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ATAS.DataFeedsCore;
using ATAS.Indicators;
using ATAS.Indicators.Custom;

using OrderFlowSuite.Atas;
using OrderFlowSuite.Nucleo;

using static Datos;

/// <summary>
/// Los indicadores reales (Atas/) contra los sustitutos de ATAS: carga sin alertas, datos en vivo (trades, DOM y MBO),
/// una alerta por estado, CSV, recarga, dibujo en los tres modos, cambio de parámetros y de instrumento.
/// </summary>
internal static class PruebasIndicador
{
	private static IndicatorCandle Vela(VelaFlujo v)
	{
		var c = new IndicatorCandle { Time = v.InicioUtc, Open = v.Open, High = v.High, Low = v.Low, Close = v.Close, Volume = v.Volume, Delta = v.Delta };

		foreach (var (t, n) in v.Niveles)
			c.Levels[t * Tick] = (n.Bid, n.Ask);

		return c;
	}

	/// <summary>El mismo historial de las pruebas del núcleo (PDL 21398.50) con la vela abierta de las 09:42 del 30-oct.</summary>
	private static List<IndicatorCandle> Historia()
	{
		var velas = new List<IndicatorCandle>();
		velas.AddRange(Planas(new DateTime(2026, 10, 29, 9, 30, 0), new DateTime(2026, 10, 29, 10, 31, 0), 21420).Select(Vela));
		velas.Add(Vela(V(new DateTime(2026, 10, 29, 10, 31, 0), 21420, 21421, 21398.50m, 21419)));
		velas.AddRange(Planas(new DateTime(2026, 10, 29, 18, 0, 0), new DateTime(2026, 10, 29, 18, 6, 0), 21410).Select(Vela));
		velas.AddRange(Planas(new DateTime(2026, 10, 30, 9, 30, 0), new DateTime(2026, 10, 30, 9, 43, 0), 21410).Select(Vela));
		return velas;
	}

	private static T Crear<T>(string instrumento = "NQZ6", Action<T>? ajustes = null) where T : Indicator, new()
	{
		var velas = Historia();
		var ind = new T
		{
			InstrumentInfo = new InstrumentInfo { TickSize = Tick, Instrument = instrumento },
			ChartInfo = new ChartInfo { TestYByPrice = p => (int)Math.Round((21430 - p) * 20) },
			TestCandles = velas,
			FirstVisibleBarNumber = 0,
			LastVisibleBarNumber = velas.Count - 1
		};

		ajustes?.Invoke(ind);
		ind.TestInitialize();
		ind.TestCalcAll();
		return ind;
	}

	private static MarketDataArg Tr(DateTime utc, decimal precio, decimal vol, bool compra, long? idAgresor = null) =>
		new() { Time = utc, Price = precio, Volume = vol, Direction = compra ? ATAS.Indicators.TradeDirection.Buy : ATAS.Indicators.TradeDirection.Sell, AggressorExchangeOrderId = idAgresor };

	private static MarketDataArg Bid(decimal precio, decimal vol) => new() { Price = precio, Volume = vol, DataType = ATAS.Indicators.MarketDataType.Bid };

	/// <summary>Ventas de 10 contra un BID de 20 que se repone (iceberg inferido de compra).</summary>
	private static void Absorber(Indicator ind, DateTime t, decimal precio, int veces)
	{
		ind.TestDepth(Bid(precio, 20));

		for (var i = 0; i < veces; i++)
		{
			ind.TestTrade(Tr(t.AddSeconds(i * 0.3), precio, 10, false));
			ind.TestDepth(Bid(precio, 10));
			ind.TestDepth(Bid(precio, 20));
		}
	}

	/// <summary>La historia del documento: cascada + barrido del PDL, absorción en 21396.25 y subida a +4 ticks.</summary>
	private static void HistoriaB(Indicator ind)
	{
		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 11));
		var cascada = new[] { (21398.25m, 60m), (21398.00m, 50m), (21397.75m, 50m), (21397.50m, 50m), (21397.00m, 50m), (21396.50m, 50m) };

		for (var i = 0; i < cascada.Length; i++)
			ind.TestTrade(Tr(t.AddMilliseconds(i * 40), cascada[i].Item1, cascada[i].Item2, false));

		var vols = new[] { 25m, 25, 25, 20, 20, 25, 25, 20 };

		for (var i = 0; i < 8; i++)
			ind.TestTrade(Tr(t.AddMilliseconds(207), 21398.50m - i * Tick, vols[i], false, 777));

		Absorber(ind, t.AddSeconds(1), 21396.25m, 43);
		var subida = new[] { 21396.50m, 21396.75m, 21397.00m, 21397.25m };

		for (var i = 0; i <= 10; i++)
			ind.TestTrade(Tr(t.AddSeconds(14 + i), subida[Math.Min(i, 3)], 40, true));
	}

	private static string Textos(OFT.Rendering.Context.RenderContext r) => string.Join("\n", r.Texts.Select(x => x.Text));

	public static void Ejecutar(Comprobador c)
	{
		var carpeta = Path.Combine(Path.GetTempPath(), "suite-indicador-" + Guid.NewGuid().ToString("N"));
		CarcasaSuite.CarpetaForzada = carpeta;

		try
		{
			Iceberg(c, carpeta);
			Master(c);
			Mbo(c);
			Parametros(c);
			Otros(c);
		}
		finally
		{
			CarcasaSuite.CarpetaForzada = null;

			if (Directory.Exists(carpeta))
				Directory.Delete(carpeta, true);
		}
	}

	private static void Iceberg(Comprobador c, string carpeta)
	{
		c.Seccion("IcebergTrackerPro real: historial sin alertas, vivo, una alerta por estado, CSV, recarga, modos");
		var ind = Crear<IcebergTrackerPro>();
		c.Check("se suscribe al MBO al iniciar", ind.SuscripcionesMbo.ToString(), "1");
		c.Check("el historial no suena", ind.Alertas.Count.ToString(), "0");

		var t = Utc(new DateTime(2026, 10, 30, 9, 42, 12));
		Absorber(ind, t, 21396.25m, 16);
		c.Check("una alerta al nacer el iceberg (foto del momento: 150c)", string.Join(" / ", ind.Alertas), "alert1|ICEBERG COMPRA INFERIDO | 21396.25 | 150c | 14 recargas | 8:1 | ACTIVO | NEUTRO | 09:42:16");

		Absorber(ind, t.AddSeconds(5), 21396.25m, 3);
		c.Check("más recargas sin cambio de estado: sin alerta nueva", ind.Alertas.Count.ToString(), "1");

		// +4 ticks y 11 s sin recargas: AGUANTANDO (otra alerta).
		ind.TestTrade(Tr(t.AddSeconds(17), 21397.25m, 5, true));
		c.Check("AGUANTANDO: segunda alerta", ind.Alertas.LastOrDefault()?.Contains("| AGUANTANDO (+4 ticks) |") ?? false, "");

		var minimo = ind.TestRender();
		c.Check("Minimal: título con modo y sesión", Textos(minimo).Contains("ICEBERG TRACKER · Modo: Inferido (sin MBO) · Sesión: Apertura NY").ToString(), "True");
		c.Check("Minimal: el dato en el panel", Textos(minimo).Contains("ICEBERG COMPRA INFERIDO | 21396.25 | 190c").ToString(), "True");
		c.Check("Minimal: sin líneas en el gráfico", minimo.Lines.Count.ToString(), "0");

		ind.DisplayMode = ModoVisual.Markers;
		c.Check("cambiar el modo visual no recalcula", ind.Recalculos.ToString(), "0");
		var marcas = ind.TestRender();
		c.Check("Markers: línea del iceberg y etiqueta corta", $"{marcas.Lines.Count} {Textos(marcas).Contains("I 190c")}", "1 True");

		ind.DisplayMode = ModoVisual.Full;
		c.Check("Full: etiqueta larga", Textos(ind.TestRender()).Contains("ICEBERG 190c 19r AGUANTANDO NEUTRO").ToString(), "True");

		ind.TestDispose();
		var csv = Path.Combine(carpeta, "NQZ6", "2026-10-30.csv");
		c.Check("CSV del día de NY", File.Exists(csv).ToString(), "True");
		var filas = File.Exists(csv) ? File.ReadAllLines(csv).Skip(1).Select(GrabadorEventos.Campos).ToList() : new();
		c.Check("dos filas (ACTIVO y AGUANTANDO), solo del Iceberg", string.Join(",", filas.Select(f => $"{f[3]}:{f[12]}")), "Iceberg:ACTIVO,Iceberg:AGUANTANDO");

		// Otro gráfico (o mañana): los eventos grabados se vuelven a dibujar en gris, sin sonar.
		var otro = Crear<IcebergTrackerPro>(ajustes: i => i.DisplayMode = ModoVisual.Markers);
		var r = otro.TestRender();
		c.Check("recarga del CSV: dibujado con (CSV), sin alertas", $"{Textos(r).Contains("I 190c (CSV)")} {otro.Alertas.Count}", "True 0");
		otro.TestDispose();
	}

	private static void Master(Comprobador c)
	{
		c.Seccion("AbsorptionMaster real: la historia del documento");
		var ind = Crear<AbsorptionMaster>(ajustes: i => i.EnableRecording = false);
		HistoriaB(ind);

		c.Check("solo suena la señal (no los eventos de los otros motores)", string.Join(" / ", ind.Alertas),
			"alert1|SEÑAL GIRO LARGO | 75/100 | Setup B | Entrada 21397.25 | SL 21395.50 | TP 21409.00 (VAL) | R:R 6.7 | 09:42:35");

		var r = ind.TestRender();
		var texto = Textos(r);
		c.Check("panel: título", texto.Contains("ABSORPTION MASTER · Modo: Inferido (sin MBO)").ToString(), "True");
		c.Check("panel: entrada, SL y TP", texto.Contains("Entrada 21397.25 | SL 21395.50 | TP 21409.00 (VAL)").ToString(), "True");
		c.Check("panel: riesgo y beneficio", texto.Contains("Riesgo 1.75 pts | Beneficio 11.75 pts | R:R 6.7 | 09:42:35").ToString(), "True");
		c.Check("panel: checklist", texto.Contains("  OK  Iceberg válido (* alta prob.)  +40").ToString(), "True");
		c.Check("Minimal: la flecha sí se dibuja", r.Polygons.Count.ToString(), "1");
		c.Check("Minimal: sin líneas de entrada / SL / TP", r.Lines.Count.ToString(), "0");

		ind.DisplayMode = ModoVisual.Markers;
		var m = ind.TestRender();
		c.Check("Markers: entrada, SL y TP", $"{m.Lines.Count} {Textos(m).Contains("TP 21409.00")}", "3 True");

		ind.DisplayMode = ModoVisual.Full;
		var f = Textos(ind.TestRender());
		c.Check("Full: también icebergs, stops y barridos", $"{f.Contains("ICEBERG*")} {f.Contains("STOPS 495c PDL")} {f.Contains("BARRIDO")}", "True True True");
		ind.TestDispose();
	}

	private static void Mbo(Comprobador c)
	{
		c.Seccion("IcebergTrackerPro real con MBO (Rithmic): nativo");
		var ind = Crear<IcebergTrackerPro>(ajustes: i => i.EnableRecording = false);
		var t = Utc(new DateTime(2026, 10, 30, 9, 45, 0));
		ind.TestMbo(new MarketByOrder { Type = MarketByOrderUpdateTypes.New, Side = ATAS.DataFeedsCore.MarketDataType.Bid, Price = 21400m, Volume = 10, ExchangeOrderId = 55, Priority = 1 });

		for (var i = 1; i <= 16; i++)
		{
			ind.TestTrade(new MarketDataArg { Time = t.AddSeconds(i), Price = 21400m, Volume = 10, Direction = ATAS.Indicators.TradeDirection.Sell, ExchangeOrderId = 55 });
			ind.TestMbo(new MarketByOrder { Type = MarketByOrderUpdateTypes.Change, Side = ATAS.DataFeedsCore.MarketDataType.Bid, Price = 21400m, Volume = 10, ExchangeOrderId = 55, Priority = 1 + i });
		}

		c.Check("NATIVO al llegar a 150c (Apertura NY)", ind.Alertas.FirstOrDefault() ?? "ninguna", "alert1|ICEBERG COMPRA NATIVO | 21400.00 | 150c | 14 recargas | 15:1 | ACTIVO | NEUTRO | 09:45:15");
		c.Check("título: Modo MBO", Textos(ind.TestRender()).Contains("Modo: MBO").ToString(), "True");
		ind.TestDispose();

		var solo = Crear<IcebergTrackerPro>(ajustes: i => i.MboMode = ModoMbo.SoloInferido);
		c.Check("Solo inferido: no se suscribe al MBO", solo.SuscripcionesMbo.ToString(), "0");
		solo.TestDispose();
	}

	private static void Parametros(Comprobador c)
	{
		c.Seccion("Parámetros: rangos, recálculo solo si hace falta, eventos en vivo conservados");
		var ind = Crear<AbsorptionMaster>(ajustes: i => i.EnableRecording = false);
		Absorber(ind, Utc(new DateTime(2026, 10, 30, 9, 42, 12)), 21396.25m, 16);

		ind.MinSignalScore = 500;
		c.Check("Math.Clamp en el setter (0-100)", ind.MinSignalScore.ToString(), "100");
		ind.WeightIceberg = 25;
		ind.MinExecVisibleRatio = 4;
		c.Check("parámetros en vivo: sin RecalculateValues", ind.Recalculos.ToString(), "0");
		ind.ImbalanceRatioPercent = 400;
		ind.SwingLengthBars = 10;
		c.Check("parámetros de velas: RecalculateValues", ind.Recalculos.ToString(), "2");

		// ATAS recalcula desde la vela 0: el iceberg en vivo se conserva (el historial no lo puede reconstruir).
		ind.TestCalcAll();
		ind.DisplayMode = ModoVisual.Full;
		c.Check("recálculo: el iceberg en vivo sigue", Textos(ind.TestRender()).Contains("ICEBERG 160c").ToString(), "True");
		ind.TestDispose();

		var nombres = typeof(AbsorptionMaster).GetProperties().Select(p => p.Name).ToHashSet();

		foreach (var nombre in new[] { "DisplayMode", "InfoPanelPosition", "InfoPanelMaxLines", "FontSize", "EventLifetimeMin", "InstrumentProfile", "EnableAlerts",
			         "AlertSoundFile", "EnableRecording", "RealTimeOnly", "ImbalanceRatioPercent", "MinStackedLevels", "MinLevelVolumeContracts", "OffHoursFactor",
			         "IgnoreZeroSide", "ZoneExtendBars", "MinBreakTicks", "MaxActiveZones", "AsiaMinExecutedContracts", "AsiaHighProbContracts", "AsiaMinRefills",
			         "LondonMinExecutedContracts", "PremarketMinutes", "MnqNyOpenMinExecutedContracts", "MinExecVisibleRatio", "HighProbExecVisibleRatio",
			         "MaxDisplacementTicks", "DetectionWindowSeconds", "MinVisibleClipContracts", "MaxVisibleClipContracts", "SyntheticRefillMaxMs",
			         "SyntheticSizeTolerancePercent", "DetectSynthetic", "HoldConfirmTicks", "BreakTicks", "EventContextSeconds", "TrendLengthBars", "TrendSource",
			         "MinCascadeContractsRth", "MinCascadeContractsEth", "CascadeMaxMs", "MinCascadeLevels", "SwingLengthBars", "RoundNumberStep", "ShowProbableZones",
			         "ZoneToleranceTicks", "StopReclaimSeconds", "IbMinutes", "MinSweepLevels", "MinSweepContracts", "SweepReclaimSeconds", "LevelToleranceTicks",
			         "WeightIceberg", "WeightProfileLevel", "WeightSweepOrStops", "WeightImbalance", "WeightDeltaShift", "MinSignalScore", "ProfileTolTicks",
			         "EnableSetupA", "EnableSetupB", "EnableSetupC", "EnableSetupD", "EntryConfirmTicks", "StopBeyondTicks", "TargetMode", "MinRewardRisk",
			         "NewsBlackoutMinutes", "NewsTimes", "CooldownSeconds", "LoadRecordedEvents", "RecordedDaysToLoad" })
		{
			if (!nombres.Contains(nombre))
				c.Check("falta el parámetro " + nombre, false);
		}

		c.Check("todos los parámetros de la especificación están en el AbsorptionMaster", true);
		c.Check("IcebergTrackerPro no muestra los de stops", typeof(IcebergTrackerPro).GetProperty("MinCascadeContractsRth") is null ? "no" : "sí", "no");
	}

	private static void Otros(Comprobador c)
	{
		c.Seccion("Robustez: sin ChartInfo, sin InstrumentInfo, MNQ, los cinco indicadores");
		var sin = Crear<StopsTrackerPro>(ajustes: i => { i.ChartInfo = null; i.InstrumentInfo = null; });
		var r = sin.TestRender();
		c.Check("sin ChartInfo ni InstrumentInfo: sin dibujo y sin errores", $"{r.Texts.Count} {Utils.Common.Logging.Registro.Mensajes.Count(m => m.StartsWith("ERROR"))}", "0 0");
		sin.TestDispose();

		// MNQ: umbrales de Apertura NY a la mitad (75 contratos).
		var mnq = Crear<IcebergTrackerPro>("MNQZ6", i => i.EnableRecording = false);
		Absorber(mnq, Utc(new DateTime(2026, 10, 30, 9, 42, 12)), 21396.25m, 8);
		c.Check("MNQ (Auto por el nombre): iceberg con 80c", mnq.Alertas.FirstOrDefault()?.Contains("| 80c |") ?? false, "");
		mnq.TestDispose();

		foreach (var ind in new Indicator[] { Crear<StackedImbalancePro>(), Crear<StopsTrackerPro>(), Crear<SweepsTrackerPro>() })
		{
			HistoriaB(ind);
			var texto = Textos(ind.TestRender());
			c.Check($"{ind.GetType().Name}: dibuja su panel", (texto.Length > 0).ToString(), "True");

			if (ind is StopsTrackerPro)
				c.Check("StopsTrackerPro: zonas probables y la cascada", $"{texto.Contains("Zonas probables:")} {ind.Alertas.Any(a => a.Contains("STOPS TOMADOS VENTA | 495c"))}", "True True");

			if (ind is SweepsTrackerPro)
				c.Check("SweepsTrackerPro: el barrido del PDL", ind.Alertas.Any(a => a.Contains("BARRIDO VENTA | 8 niveles | 185c | barrió PDL 21398.50")).ToString(), "True");

			c.Check($"{ind.GetType().Name}: solo sus eventos suenan", ind.Alertas.All(a => ind switch
			{
				StackedImbalancePro => a.Contains("STACKED IMB"),
				StopsTrackerPro => a.Contains("STOPS"),
				_ => a.Contains("BARRIDO")
			}).ToString(), "True");

			ind.TestDispose();
		}
	}
}
