using System;
using System.Collections.Generic;
using System.Linq;

using Termico;

using static DatosV2;

/// <summary>v3 S8: perfil fino y LVN entre picos (pruebas 11-14 y 16 del prompt v3).</summary>
internal static class PruebasV3Thermal
{
	/// <summary>Perfil de una "vela" con el volumen por tick que se pida (precios desde 21400, tick 0.25).</summary>
	internal static AcumuladorTicks Acumulado(Func<int, decimal> volumen, int ticks = 160)
	{
		var a = new AcumuladorTicks(Tick, 30);
		var v = new VelaPerfil { Time = Utc(new DateTime(2026, 11, 2, 10, 0, 0)), Low = 21400, High = 21400 + (ticks - 1) * Tick };

		for (var t = 0; t < ticks; t++)
		{
			var vol = volumen(t);

			if (vol > 0)
				v.Niveles.Add(new NivelPrecio(21400 + t * Tick, vol, vol / 2, vol / 2));
		}

		v.Volume = v.Niveles.Sum(n => n.Volume);
		a.Agregar(v);
		return a;
	}

	/// <summary>Meseta de 1000 con colas a 0 y un valle estrecho de 2 ticks (50) en 21420.00-21420.25.</summary>
	internal static decimal ConValle(int t) => t < 8 ? t * 120 : t > 151 ? (159 - t) * 120 : t is 80 or 81 ? 50 : 1000;

	public static void Ejecutar(Comprobador c)
	{
		c.Seccion("v3 S8 Perfil fino: un LVN estrecho que el perfil de dibujo agrupa (prueba 11)");
		var a = Acumulado(ConValle);
		var p = new ParametrosTermicos { LvnMethod = MetodoLvn.ValleEntrePicos, EnableLVN = true, LVNUseZone = false };
		var grueso = Perfil.Construir(a, null, TiposPerfil.Volume, 8, false, 0)!;
		var fino = MotorTermico.PerfilFino(a, null, TiposPerfil.Volume, p)!;
		c.Check("el perfil fino tiene 1 tick por fila; el de dibujo 8", $"{fino.TicksPorFila} {grueso.TicksPorFila} {fino.Filas} {grueso.Filas}", "1 8 160 20");
		var heredado = MotorTermico.DetectarLvn(grueso, new ParametrosTermicos { LVNUseZone = false }, null, null);
		c.Check("en el perfil de dibujo (8 ticks por fila) el detector heredado no lo ve", heredado.Count(l => !l.Manual).ToString(), "0");
		c.Check("ni el nuevo, si se le da el perfil agrupado", MotorTermico.DetectarLvnValles(grueso, p, null, null).Count.ToString(), "0");
		var lvns = MotorTermico.DetectarLvnValles(fino, p, null, null);
		var l = lvns.SingleOrDefault();
		c.Check("en el perfil fino: un LVN en el valle de 21420", l is null ? $"{lvns.Count} LVN" : $"{F(l.Precio)} {l.Metodo}", "21420 VALLE ENTRE PICOS");

		c.Seccion("v3 S8 Zona con centro, extremos sin dos picos, puntuación (pruebas 12, 13)");
		c.Check("prueba 13: zona con límite bajo, alto y centro dentro; ancho estrecho",
			l is null ? "sin LVN" : $"{l.Bajo <= l.Precio && l.Precio <= l.Alto} {l.WidthTicks <= 4} {F(l.Bajo)}-{F(l.Alto)}", "True True 21420-21420.25");
		c.Check("puntuación 0-100 con la profundidad real: 38 (95 %) + 15 estrecho + 15 casi sin volumen; sin no tocado ni FVG", l is null ? "" : $"{l.Score100} {l.Depth:0.00}", "68 0.95");
		c.Check("prueba 12: las colas de los extremos no son LVN (no las enmarcan dos picos)",
			lvns.All(x => x.Precio > 21402 && x.Precio < 21438).ToString(), "True");
		var colas = Acumulado(t => t < 40 ? 100 : 1000);
		c.Check("una cola baja en un extremo, sin pico al otro lado: ningún LVN",
			MotorTermico.DetectarLvnValles(MotorTermico.PerfilFino(colas, null, TiposPerfil.Volume, p)!, p, null, null).Count.ToString(), "0");
		var heredadoMetodo = MotorTermico.Analizar(grueso, null, new ParametrosTermicos { LvnMethod = MetodoLvn.PorcentajePocHeredado, LVNUseZone = false }, null, null, fino);
		c.Check("con el método heredado se usa el detector anterior (no se mezclan)", heredadoMetodo.Lvns.All(x => x.Metodo != "VALLE ENTRE PICOS").ToString(), "True");

		c.Seccion("v3 S8 LVN manual: se ajusta al valle cercano y conserva su origen (prueba 16)");
		var manual = MotorTermico.ManualesAjustados(fino, new[] { 21421.00m }, 4).Single();
		c.Check("marcado en 21421.00, ajustado al mínimo a 4 ticks: 21420.25 (el más cercano de los dos)",
			$"{F(manual.Precio)} {F(manual.PrecioOriginal ?? 0)} {manual.Manual}", "21420.25 21421 True");
		var sinAjuste = MotorTermico.ManualesAjustados(fino, new[] { 21421.00m }, 0).Single();
		c.Check("con ajuste 0 queda donde se marcó", $"{F(sinAjuste.Precio)} {sinAjuste.PrecioOriginal is null}", "21421 True");
		var lejos = MotorTermico.ManualesAjustados(fino, new[] { 21430.00m }, 4).Single();
		c.Check("sin valle cerca (todo 1000) no se mueve", F(lejos.Precio), "21430");

		c.Seccion("v3 S8 Estabilizador: PROVISIONAL, confirmación, histéresis, PROBADO y ATRAVESADO (prueba 14)");
		var seg = new SeguidorLvn();
		var t0 = new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc);
		LvnTermico Z(decimal centro) => new() { Precio = centro, Bajo = centro - 0.25m, Alto = centro + 0.25m, Score100 = 60, WidthTicks = 3 };
		seg.Actualizar(new[] { Z(21420.25m) }, true, t0, 2, 50);
		c.Check("visto en 1 cierre de 2: PROVISIONAL", $"{seg.Zonas.Count} {seg.Zonas[0].Provisional}", "1 True");
		seg.Actualizar(new[] { Z(21420.25m) }, true, t0.AddMinutes(1), 2, 50);
		var id = seg.Zonas[0].Id;
		c.Check("en 2 cierres seguidos: confirmado", seg.Zonas[0].Provisional.ToString(), "False");
		seg.Actualizar(new[] { Z(21420.50m) }, true, t0.AddMinutes(2), 2, 50);
		c.Check("prueba 14: una fluctuación pequeña no lo mueve ni lo cambia", $"{seg.Zonas.Count} {seg.Zonas[0].Id == id} {F(seg.Zonas[0].Centro)}", "1 True 21420.25");

		for (var k = 0; k < 6; k++)
			seg.Actualizar(Array.Empty<LvnTermico>(), true, t0.AddMinutes(3 + k), 2, 50);

		c.Check("confirmado y ausente en 6 cierres: sigue (histéresis)", seg.Zonas.Count.ToString(), "1");
		seg.Actualizar(Array.Empty<LvnTermico>(), true, t0.AddMinutes(9), 2, 50);
		c.Check("a la 7.ª ausencia se va", seg.Zonas.Count.ToString(), "0");
		seg.Actualizar(new[] { Z(21430m) }, true, t0.AddMinutes(10), 2, 50);
		seg.Actualizar(Array.Empty<LvnTermico>(), true, t0.AddMinutes(11), 2, 50);
		c.Check("uno PROVISIONAL que desaparece se va enseguida", seg.Zonas.Count.ToString(), "0");

		var est = new SeguidorLvn();
		est.Actualizar(new[] { Z(21420.25m) }, true, t0, 1, 50);
		est.Precio(21426, 21424, 21425);
		est.Precio(21425, 21419.75m, 21423);
		c.Check("llega desde arriba, toca y cierra arriba: PROBADO (y ya tocado: pierde los 15 puntos)", $"{est.Zonas[0].Estado} {est.Zonas[0].Tocado} {est.Zonas[0].ScoreTotal}", "PROBADO True 60");
		est.Precio(21423, 21417, 21418);
		c.Check("cierra al otro lado: ATRAVESADO", est.Zonas[0].Estado, "ATRAVESADO");
		var nuevo = new SeguidorLvn();
		nuevo.Actualizar(new[] { Z(21420.25m) }, true, t0, 1, 50);
		c.Check("confirmado y sin tocar: suma los 15 de 'no tocado'", nuevo.Zonas[0].ScoreTotal.ToString(), "75");
	}
}
