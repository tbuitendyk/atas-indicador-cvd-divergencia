namespace SessionSweeps.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

using OrderFlowSuite.Comun;

/// <summary>S4: los LVN vienen solo del Thermal Volume Profile + LVN (Suite).</summary>
public sealed partial class MotorSweeps
{
	private bool _lvnConectado;

	public RepositorioLvn Lvn { get; } = new();

	/// <summary>Thermal cargado (si no: "SIN FUENTE LVN"; MOD2/MOD4 solo con FVG, EMA o nivel).</summary>
	public bool HayFuenteLvn => Fuentes.Estado(FuenteSuite.Thermal) != EstadoFuente.NoCargado;

	/// <summary>Un evento del Thermal (LvnZone o RangeReset) llega del bus.</summary>
	public void LvnDesdeSuite(EventoSuite e)
	{
		lock (_sync)
		{
			ConectarLvn();
			Lvn.Aplicar(e);
			Revision++;
		}
	}

	private void ConectarLvn()
	{
		if (_lvnConectado)
			return;

		_lvnConectado = true;
		Lvn.AlBorrar ??= BorradasLvn;
		ObjetivosExtra ??= () => Lvn.Activas.Select(z => ($"LVN {z.Origen}", z.Centro)).ToList();
	}

	/// <summary>Al cierre de cada vela: las zonas atravesadas quedan MITIGADAS.</summary>
	private void MitigarLvn(Vela v) => Lvn.Mitigar(v.High, v.Low, v.Close);
}
