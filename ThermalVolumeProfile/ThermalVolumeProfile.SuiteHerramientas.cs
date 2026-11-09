// Solo dentro de SuiteOrderFlowStacker (símbolo SUITE): en el ThermalVolumeProfile por separado este archivo queda vacío.
#if SUITE
namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using OFT.Rendering.Context;

using Termico;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

/// <summary>
/// Herramientas v3 del Thermal (Suite): LVN manual como punto o zona (arrastre vertical) guardado con instrumento, rango,
/// origen y hora; BORRAR LVN y DESHACER; PIERNA (perfil congelado de una pierna); selección y borrado de cajas de GATILLO.
/// </summary>
public partial class ThermalVolumeProfileLVN
{
	private readonly List<string> _deshacer = new();
	private long? _cajaSeleccionada;

	[Display(Name = "Pierna: perfil congelado", GroupName = G1, Order = 900,
		Description = "Se activa con la herramienta PIERNA: el perfil y sus LVN/EDGE salen solo de esa pierna y no suman volumen nuevo.")]
	public bool PiernaActiva { get; set; }

	[Display(Name = "Pierna: inicio (UTC)", GroupName = G1, Order = 910)]
	public DateTime PiernaDesdeUtc { get; set; }

	[Display(Name = "Pierna: fin (UTC)", GroupName = G1, Order = 920)]
	public DateTime PiernaHastaUtc { get; set; }

	/// <summary>Los LVN manuales de este instrumento (los de otros se guardan pero no se usan ni se ven).</summary>
	private List<LvnManualGuardado> ManualesGuardados() =>
		LvnManualesTexto.Leer(_manualLvnPrices, Instrumento, _rangoId, DateTime.UtcNow);

	private List<(decimal Centro, decimal Bajo, decimal Alto)> SuiteManualesZona() =>
		ManualesGuardados().Where(m => m.Instrumento == Instrumento).Select(m => (m.Centro, m.Bajo, m.Alto)).ToList();

	private void GuardarManuales(IEnumerable<LvnManualGuardado> todos)
	{
		_deshacer.Add(_manualLvnPrices);

		if (_deshacer.Count > 20)
			_deshacer.RemoveAt(0);

		_manualLvnPrices = LvnManualesTexto.Escribir(todos);
		Recalcular();
	}

	/// <summary>Herramienta LVN: clic = añade un punto o quita el visible más cercano; arrastre vertical = una zona.</summary>
	private void SuiteLvnManual(Point a, Point b)
	{
		if (InstrumentInfo is not { TickSize: > 0 } info)
			return;

		var todos = ManualesGuardados();
		var area = Area;

		if (Math.Abs(b.Y - a.Y) < 4)
		{
			// Solo se quita un LVN que se ve (visibles y dentro del gráfico): nunca uno oculto.
			var cercano = !ShowManualLVN ? null : todos.Where(m => m.Instrumento == Instrumento)
				.Select(m => (m, y: Y(m.Centro), dentro: Y(m.Alto) - 5 <= a.Y && a.Y <= Y(m.Bajo) + 5))
				.Where(x => x.y >= area.Top && x.y <= area.Bottom && (x.dentro || Math.Abs(x.y - a.Y) <= 5))
				.OrderBy(x => Math.Abs(x.y - a.Y))
				.Select(x => x.m)
				.FirstOrDefault();

			if (cercano is not null)
				todos.Remove(cercano);
			else
				todos.Add(Nuevo(PrecioDeY(a.Y), PrecioDeY(a.Y)));
		}
		else
		{
			var (alto, bajo) = (PrecioDeY(Math.Min(a.Y, b.Y)), PrecioDeY(Math.Max(a.Y, b.Y)));
			todos.Add(Nuevo(bajo, alto));
		}

		GuardarManuales(todos);

		LvnManualGuardado Nuevo(decimal bajo, decimal alto) =>
			new(Math.Round((bajo + alto) / 2 / info.TickSize) * info.TickSize, bajo, alto, Instrumento, _rangoId, "MANUAL", DateTime.UtcNow);
	}

	/// <summary>BORRAR LVN: quita los LVN manuales de este instrumento (DESHACER los devuelve).</summary>
	private void SuiteBorrarLvn() => GuardarManuales(ManualesGuardados().Where(m => m.Instrumento != Instrumento).ToList());

	private void SuiteDeshacerLvn()
	{
		if (_deshacer.Count == 0)
			return;

		_manualLvnPrices = _deshacer[^1];
		_deshacer.RemoveAt(_deshacer.Count - 1);
		Recalcular();
	}

	/// <summary>PIERNA: la selección congela el perfil de esa pierna (velas cerradas de inicio a fin).</summary>
	private void SuitePierna(int barA, int barB)
	{
		var fin = Math.Min(barB, CurrentBar - 2);

		if (fin < barA)
			return;

		(PiernaDesdeUtc, PiernaHastaUtc, PiernaActiva) = (GetCandle(barA).Time, GetCandle(fin).Time, true);
		Recalcular();
	}

	private void SuiteDescongelar()
	{
		PiernaActiva = false;
		Recalcular();
	}

	private void SuiteBorrarPierna()
	{
		(PiernaActiva, PiernaDesdeUtc, PiernaHastaUtc) = (false, default, default);
		Recalcular();
	}

	/// <summary>Rango de la pierna congelada: velas fijas, así no entra volumen nuevo (prueba 15).</summary>
	private (int Desde, int Hasta)? SuiteRangoPierna(int total)
	{
		return PiernaActiva ? Rangos.Pierna(Tiempo, total, PiernaDesdeUtc, PiernaHastaUtc) : null;
	}

	private void SuiteDibujarPierna(RenderContext context, Rectangle area)
	{
		if (!PiernaActiva)
			return;

		context.DrawString($"PERFIL CONGELADO (pierna {PiernaDesdeUtc:HH:mm}-{PiernaHastaUtc:HH:mm} UTC)", _fuenteBarra, Color.FromArgb(255, 100, 200, 255),
			new Rectangle(area.Left + 8, area.Bottom - 22, 420, 16), _izquierda);
	}

	/// <summary>Sin herramienta: un clic dentro de una caja de GATILLO la selecciona (BORRAR GATILLO borra esa).</summary>
	private bool SuiteClicCaja(Point p)
	{
		lock (_syncCajas)
		{
			foreach (var c in _cajas.AsEnumerable().Reverse())
			{
				var q = c.Solicitud;
				var x1 = ChartInfo!.GetXByBar(BarraDeHora(q.DesdeUtc));
				var x2 = ChartInfo!.GetXByBar(BarraDeHora(q.HastaUtc.AddTicks(-1))) + AnchoBarra;

				if (p.X >= x1 && p.X <= x2 && p.Y >= Y(q.Alto) && p.Y <= Y(q.Bajo))
				{
					_cajaSeleccionada = q.Id;
					RedrawChart();
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>BORRAR GATILLO: la caja seleccionada o, sin selección, todas.</summary>
	private void SuiteBorrarGatillo()
	{
		lock (_syncCajas)
		{
			if (_cajaSeleccionada is { } id && _cajas.RemoveAll(c => c.Solicitud.Id == id) > 0)
				_cajaSeleccionada = null;
			else
				_cajas.Clear();
		}

		RedrawChart();
	}
}
#endif
