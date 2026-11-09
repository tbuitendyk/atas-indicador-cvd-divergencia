namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public enum ModoRiesgo
{
	[Display(Name = "Monto ($)")]
	Monto,

	[Display(Name = "Porcentaje de la cuenta (%)")]
	Porcentaje
}

public enum ModoStop
{
	[Display(Name = "Más allá de la zona o estructura")]
	ZonaEstructura,

	[Display(Name = "Más allá del POC de la vela de arranque")]
	PocVelaArranque
}

public enum EstadoMinimoAlerta
{
	[Display(Name = "TRAMPA B")]
	TrampaB,

	[Display(Name = "TRAMPA A")]
	TrampaA,

	[Display(Name = "TRAMPA PREMIUM")]
	TrampaPremium
}

/// <summary>Una fuente de la suite: encendida o no y su peso (0-2) sobre los puntos que aporta (3.3).</summary>
public sealed class PesoFuente
{
	public bool Enabled { get; set; } = true;
	public decimal Weight { get; set; } = 1.0m;

	public decimal Factor => Enabled ? Math.Clamp(Weight, 0, 2) : 0;
}

/// <summary>
/// Parámetros de Session Sweeps Pro v2.0 (sección 15 del documento maestro). Todos son VALORES INICIALES A CALIBRAR con
/// el CSV; las puntuaciones no son probabilidades.
/// </summary>
public sealed class ParametrosV2
{
	// General
	/// <summary>Efectivo: lo pone el perfil del instrumento (no se configura aquí).</summary>
	public int VolumePerBar { get; set; } = 500;

	/// <summary>Alias de símbolos (2.2): "ENQ=NQ;EP=ES".</summary>
	public string AliasSimbolos { get; set; } = "";

	/// <summary>VolumePerBar AUTO = mediana del volumen de 1 minuto en RTH × esta fracción (unas 3 barras por minuto típico).</summary>
	public decimal VolumePerBarFraction { get; set; } = 0.33m;
	public int MedianLookbackBars { get; set; } = 30;
	public int PreT0DeltaSec { get; set; } = 60;
	public int PanelRefreshMs { get; set; } = 250;
	public TimeSpan KillzoneStartNY { get; set; } = new(9, 30, 0);
	public TimeSpan KillzoneEndNY { get; set; } = new(11, 30, 0);
	public int NewsBlockMin { get; set; } = 5;
	public string NewsTimes { get; set; } = "08:30;10:00;14:00";

	// Suite
	public int SuiteBufferSize { get; set; } = 5000;
	public int DedupTicks { get; set; } = 1;
	public int DedupSec { get; set; } = 2;
	public int SuitePreSec { get; set; } = 30;
	public int SuitePriceMarginTicks { get; set; } = 8;
	public PesoFuente Iceberg { get; } = new();
	public PesoFuente Stops { get; } = new();
	public PesoFuente Sweeps { get; } = new();
	public PesoFuente Stacked { get; } = new();
	public PesoFuente Imbalance { get; } = new();
	public PesoFuente Absorption { get; } = new();

	// Nivel
	public int EqualLevelTolTicks { get; set; } = 3;
	public int EqualLevelMinTouches { get; set; } = 2;
	public int LevelMinAgeMin { get; set; } = 60;

	// Stops
	public int MinPenetrationTicks { get; set; } = 2;
	public int MaxSweepTicks { get; set; } = 24;
	public decimal BurstSpeedMult { get; set; } = 2.5m;
	public decimal FastBarDurationMult { get; set; } = 0.5m;
	public decimal AggressorShareMin { get; set; } = 0.65m;
	public int StopsEventWindowSec { get; set; } = 30;
	public decimal VolBeyondRelMult { get; set; } = 1.5m;
	public int SweepHistoryCount { get; set; } = 20;
	public int MinVolBeyondFloor { get; set; } = 50;

	// Absorción
	public decimal DivergenceDeltaPctMin { get; set; } = 0.15m;
	public int IcebergMaxDistTicks { get; set; } = 2;
	public int F2MinBars { get; set; } = 2;
	public int F2MaxBars { get; set; } = 5;
	public int F2MaxBarsRetrace { get; set; } = 7;
	public decimal CompressionRangeMult { get; set; } = 0.5m;
	public int CompressionMaxTicks { get; set; } = 8;
	public decimal ValueAreaPct { get; set; } = 0.70m;
	public int FinishedAuctionMaxContracts { get; set; } = 2;
	public int StackedMinLevels { get; set; } = 3;
	public decimal ImbalanceRatio { get; set; } = 3.0m;
	public int ImbalanceMinVolume { get; set; } = 5;

	// Recuperación
	public int ReclaimBufferTicks { get; set; } = 1;
	public int ReclaimMaxBars { get; set; } = 5;
	public int CompositeMaxBars { get; set; } = 7;
	public int ZoneMaxBars { get; set; } = 10;
	public decimal F3RangeMult { get; set; } = 1.5m;
	public decimal F3DeltaPctMin { get; set; } = 0.25m;
	public decimal ContDeltaPctMin { get; set; } = 0.30m;
	public decimal DeltaFlipMin { get; set; } = 0.20m;
	public int RecoveryTicks { get; set; } = 16;
	public int StopBufferTicks { get; set; } = 3;

	// Estados
	public int ScoreTrapB { get; set; } = 55;
	public int ScoreTrapA { get; set; } = 70;
	public int ScorePremium { get; set; } = 85;
	public int PremiumMinLevelScore { get; set; } = 12;
	public int PremiumMaxReclaimBars { get; set; } = 3;
	public int PremiumUpgradeMaxBars { get; set; } = 10;
	public int RuptureMinConditions { get; set; } = 3;
	public int AcceptanceCloses { get; set; } = 2;
	public int RuptureMaxAbsorptionScore { get; set; } = 8;
	public int FailedBreakTicks { get; set; } = 4;
	public int FailedBreakBars { get; set; } = 5;
	public int MaxEvaluationBars { get; set; } = 15;
	public int MaxEvaluationSec { get; set; } = 300;
	public EstadoMinimoAlerta AlertMinState { get; set; } = EstadoMinimoAlerta.TrampaA;

	// FVG / LVN
	public int FvgMinTicks { get; set; } = 4;
	public int FvgMaxActive { get; set; } = 10;
	public bool Fvg1m { get; set; } = true;
	public bool Fvg5m { get; set; } = true;
	public int BfvgWindowMin { get; set; } = 30;
	public int ConfluenceBonus { get; set; } = 10;
	public int TargetMinDistTicks { get; set; } = 8;

	// Modelos
	public bool Mod3InvalidatesAllMod1Mod2 { get; set; }
	public bool Mod4AllowInternal { get; set; }
	public int Mod4MaxWaitMin { get; set; } = 45;
	public decimal Mod4MaxRetracePct { get; set; } = 0.618m;
	public int Mod4MaxAttempts { get; set; } = 2;
	public decimal MinRR { get; set; } = 1.5m;
	public int MaxStopTicks { get; set; } = 40;
	public decimal MaxOpenRiskPct { get; set; } = 1.0m;

	/// <summary>MOD3: "super big trade" = una orden agresiva (o cascada) de al menos estos contratos (el documento no fija el valor).</summary>
	public int SuperBigTradeMinContracts { get; set; } = 150;

	public ModoStop StopMode { get; set; } = ModoStop.ZonaEstructura;

	// Riesgo
	public ModoRiesgo RiskMode { get; set; } = ModoRiesgo.Monto;
	public decimal RiskAmountUSD { get; set; } = 200;
	public decimal RiskPct { get; set; } = 0.5m;
	public decimal AccountSize { get; set; } = 50000;
	public bool ReadAtasAccount { get; set; }
	public int MicroImpulseEmaLen { get; set; } = 15;
	public bool MicroImpulseFromEntry { get; set; }

	// Manual
	public int ManualZoneTicks { get; set; } = 4;
	public int ManualLookbackBars { get; set; } = 10;
	public int MaxManualBoxes { get; set; } = 5;

	// CVD (anexo)
	public int CvdMacroPivotBars { get; set; } = 10;
	public int CvdMicroPivotBars { get; set; } = 3;
	public bool CvdShowMicro { get; set; } = true;
	public int CvdMaxLookbackBars { get; set; } = 120;

	/// <summary>Contratos por barra efectivos (los pone el perfil del instrumento: manual, AUTO o provisional).</summary>
	public int ContratosPorBarra() => Math.Max(10, VolumePerBar);
}
