namespace Completo.Nucleo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Tipos de alerta sonora que el motor puede emitir.</summary>
public enum TipoAviso
{
	/// <summary>Señal confirmada (M1 o M2) con su plan.</summary>
	Senal,

	/// <summary>Barrido en evaluación: aviso previo, todavía falta recuperación y gatillo.</summary>
	Barrido,

	/// <summary>Patrón completo que el control de riesgo no admitió.</summary>
	Bloqueado
}

/// <summary>Aviso numerado: el indicador hace sonar los que tienen número mayor que el último que ya sonó.</summary>
public sealed record Aviso(long Numero, TipoAviso Tipo, DateTime Utc, string Texto);

public sealed partial class MotorCompleto
{
	private readonly List<Aviso> _avisos = new();
	private long _ultimoAviso;

	/// <summary>Número del último aviso emitido (0 = ninguno).</summary>
	public long UltimoAviso
	{
		get
		{
			lock (_sync)
				return _ultimoAviso;
		}
	}

	/// <summary>Avisos emitidos después del número dado, del más antiguo al más reciente.</summary>
	public List<Aviso> AvisosDespuesDe(long numero)
	{
		lock (_sync)
			return _avisos.Where(a => a.Numero > numero).ToList();
	}

	private void Avisar(TipoAviso tipo, DateTime utc, string texto)
	{
		_avisos.Add(new Aviso(++_ultimoAviso, tipo, utc, texto));

		if (_avisos.Count > 200)
			_avisos.RemoveRange(0, _avisos.Count - 200);
	}
}
