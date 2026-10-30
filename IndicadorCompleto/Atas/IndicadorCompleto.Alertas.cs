namespace ATAS.Indicators.Custom;

using System;
using System.ComponentModel.DataAnnotations;

using Completo.Nucleo;

using OFT.Attributes.Editors;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// Alertas sonoras con el sistema de alertas de ATAS (sonido + mensaje en su ventana de alertas). Solo suenan con
/// velas en vivo, nunca durante la carga del historial, y solo desde el maestro (que es quien calcula). Cada una se
/// activa o silencia en los ajustes o con su icono de altavoz en el recuadro de estado de cualquier gráfico del grupo.
/// </summary>
public partial class IndicadorCompleto
{
	#region Ajustes: alertas sonoras

	// Sin recálculo al cambiarlas: no afectan a los cálculos.
	[Display(Name = "Alerta: señal confirmada", GroupName = "10. Alertas sonoras", Order = 200)]
	public bool AlertaSenal { get; set; } = true;

	[Display(Name = "Sonido: señal confirmada", GroupName = "10. Alertas sonoras", Order = 201)]
	[SoundComboBoxEditor]
	public string SonidoSenal { get; set; } = "alert1";

	[Display(Name = "Alerta: barrido (aviso previo)", GroupName = "10. Alertas sonoras", Order = 202,
		Description = "Barrido de liquidez que puede acabar en señal: todavía falta la recuperación y el gatillo.")]
	public bool AlertaBarrido { get; set; } = true;

	[Display(Name = "Sonido: barrido", GroupName = "10. Alertas sonoras", Order = 203)]
	[SoundComboBoxEditor]
	public string SonidoBarrido { get; set; } = "alert2";

	[Display(Name = "Alerta: setup bloqueado", GroupName = "10. Alertas sonoras", Order = 204,
		Description = "Patrón completo que el control de riesgo no admitió (RR, instrumento, valores sin verificar…).")]
	public bool AlertaBloqueado { get; set; } = true;

	[Display(Name = "Sonido: setup bloqueado", GroupName = "10. Alertas sonoras", Order = 205)]
	[SoundComboBoxEditor]
	public string SonidoBloqueado { get; set; } = "alert2";

	#endregion

	private static readonly string[] NombresAlerta = { "Señal", "Barrido", "Bloqueado" };
	private readonly Rectangle[] _iconosAlerta = new Rectangle[3];
	private bool _enVivo;
	private long _avisoSonado;

	/// <summary>Tras procesar las velas cerradas del maestro: suena lo nuevo, o marca el historial como ya oído.</summary>
	private void RevisarAlertas(int bar)
	{
		var motor = _motor!;

		if (!_enVivo)
		{
			// La última vela del historial: lo ocurrido hasta aquí no suena.
			if (bar >= CurrentBar - 1)
				_enVivo = true;

			_avisoSonado = motor.UltimoAviso;
			return;
		}

		if (motor.UltimoAviso == _avisoSonado)
			return;

		var instrumento = InstrumentInfo?.Instrument ?? "";

		foreach (var aviso in motor.AvisosDespuesDe(_avisoSonado))
		{
			_avisoSonado = aviso.Numero;
			var (activa, sonido) = aviso.Tipo switch
			{
				TipoAviso.Senal => (AlertaSenal, SonidoSenal),
				TipoAviso.Barrido => (AlertaBarrido, SonidoBarrido),
				_ => (AlertaBloqueado, SonidoBloqueado)
			};

			if (activa)
				AddAlert(string.IsNullOrWhiteSpace(sonido) ? "alert1" : sonido, $"IndicadorCompleto {instrumento}: {aviso.Texto}");
		}
	}

	/// <summary>Las alertas que valen son las del maestro: los iconos de un visor muestran y cambian las suyas.</summary>
	private IndicadorCompleto DuenoAlertas() => RegistroMotores.Maestro(_clave) as IndicadorCompleto ?? this;

	private static bool Alerta(IndicadorCompleto i, int indice) => indice switch
	{
		0 => i.AlertaSenal,
		1 => i.AlertaBarrido,
		_ => i.AlertaBloqueado
	};

	private static void Alternar(IndicadorCompleto i, int indice)
	{
		switch (indice)
		{
			case 0:
				i.AlertaSenal = !i.AlertaSenal;
				break;
			case 1:
				i.AlertaBarrido = !i.AlertaBarrido;
				break;
			default:
				i.AlertaBloqueado = !i.AlertaBloqueado;
				break;
		}
	}

	/// <summary>Fila de tres interruptores: altavoz con ondas (activa) o tachado en rojo (silenciada).</summary>
	private void DibujarIconosAlerta(RenderContext context, int x, int y, int ancho, bool hayMaestro)
	{
		var dueno = DuenoAlertas();
		var celda = (ancho - 12) / 3;

		for (var i = 0; i < 3; i++)
		{
			var caja = new Rectangle(x + 6 + i * celda, y, celda - 4, 17);
			_iconosAlerta[i] = caja;

			var activa = Alerta(dueno, i);
			var tinta = !hayMaestro ? Color.Gray : activa ? Color.FromArgb(255, 120, 230, 140) : Color.FromArgb(255, 150, 150, 150);
			context.FillRectangle(activa && hayMaestro ? Color.FromArgb(200, 22, 70, 40) : Color.FromArgb(200, 45, 45, 50), caja);

			// Altavoz: cuerpo y cono.
			var ix = caja.X + 5;
			var iy = caja.Y + 2;
			context.FillRectangle(tinta, new Rectangle(ix, iy + 4, 4, 6));
			context.FillPolygon(tinta, new[] { new Point(ix + 4, iy + 4), new Point(ix + 9, iy), new Point(ix + 9, iy + 14), new Point(ix + 4, iy + 10) });

			if (activa)
			{
				var onda = new RenderPen(tinta, 1);
				context.DrawLine(onda, ix + 11, iy + 4, ix + 12, iy + 7);
				context.DrawLine(onda, ix + 12, iy + 7, ix + 11, iy + 10);
				context.DrawLine(onda, ix + 14, iy + 2, ix + 16, iy + 7);
				context.DrawLine(onda, ix + 16, iy + 7, ix + 14, iy + 12);
			}
			else
			{
				var cruz = new RenderPen(Color.FromArgb(255, 255, 82, 82), 2);
				context.DrawLine(cruz, ix + 11, iy + 4, ix + 16, iy + 10);
				context.DrawLine(cruz, ix + 11, iy + 10, ix + 16, iy + 4);
			}

			context.DrawString(NombresAlerta[i], _fuente, tinta, new Rectangle(ix + 20, caja.Y, caja.Width - 26, caja.Height), _izquierda);
		}
	}

	private int IconoAlerta(Point raton)
	{
		for (var i = 0; i < 3; i++)
		{
			if (_iconosAlerta[i].Width > 0 && _iconosAlerta[i].Contains(raton))
				return i;
		}

		return -1;
	}

	/// <summary>Un clic en un icono activa o silencia esa alerta. Devuelve true si el clic era suyo.</summary>
	private bool ClicEnAlerta(Point raton)
	{
		var indice = IconoAlerta(raton);

		if (indice < 0)
			return false;

		Alternar(DuenoAlertas(), indice);
		RedrawChart();
		return true;
	}
}
