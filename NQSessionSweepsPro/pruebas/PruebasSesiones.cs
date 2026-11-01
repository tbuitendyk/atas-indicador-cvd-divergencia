using System;
using System.Linq;

using NqSweeps.Nucleo;

using static Datos;

/// <summary>F1: sesiones (3 modos, premercado, PineOriginal, horario de verano automático, ajuste manual) y tabla.</summary>
internal static class PruebasSesiones
{
	public static void Ejecutar(Comprobador c)
	{
		Horarios(c);
		CambiosDeHorario(c);
		MotorYNiveles(c);
		Tabla(c);
	}

	private static string Dentro(ConfiguracionSweeps cfg, string id, DateTime et)
	{
		var (dentro, fecha) = new MotorSesiones(cfg).Dentro(cfg.Sesion(id), Utc(et));
		return dentro ? $"si {fecha:MM-dd}" : "no";
	}

	private static void Horarios(Comprobador c)
	{
		c.Seccion("F1. Sesiones por defecto (ET): completa, Kill Zone, premercado propio y Asia cruzando la medianoche");

		var cfg = new ConfiguracionSweeps();
		var martes = new DateTime(2026, 11, 3);
		c.Check("08:45 ET: premercado (zona propia 08:30-09:30)", $"{Dentro(cfg, "Premercado", martes.AddHours(8.75))} / NY AM {Dentro(cfg, "NY AM", martes.AddHours(8.75))}", "si 11-03 / NY AM no");
		c.Check("09:30 ET: NY AM empieza y el premercado termina", $"{Dentro(cfg, "NY AM", martes.AddHours(9.5))} / premercado {Dentro(cfg, "Premercado", martes.AddHours(9.5))}", "si 11-03 / premercado no");
		c.Check("12:30 ET: entre NY AM y NY PM", $"{Dentro(cfg, "NY AM", martes.AddHours(12.5))} {Dentro(cfg, "NY PM", martes.AddHours(12.5))}", "no no");
		c.Check("Asia 18:00-02:00: el lunes 19:00 y el martes 01:00 son la misma sesión (del lunes)",
			$"{Dentro(cfg, "Asia", martes.AddHours(-5))} / {Dentro(cfg, "Asia", martes.AddHours(1))} / 02:00 {Dentro(cfg, "Asia", martes.AddHours(2))}", "si 11-02 / si 11-02 / 02:00 no");

		cfg.Sesion("Asia").Modo = ModoSesion.KillZone;
		c.Check("Kill Zone de Asia 20:00-00:00", $"{Dentro(cfg, "Asia", martes.AddMinutes(-1))} / 00:00 {Dentro(cfg, "Asia", martes)}", "si 11-02 / 00:00 no");

		var corto = new ConfiguracionSweeps { PremarketMinutesBeforeOpen = 30 };
		c.Check("PremarketMinutesBeforeOpen 30: de 09:00 a 09:30", $"{Dentro(corto, "Premercado", martes.AddHours(8.75))} / {Dentro(corto, "Premercado", martes.AddHours(9))}", "no / si 11-03");

		var custom = new ConfiguracionSweeps();
		custom.Sesion("NY AM").Modo = ModoSesion.Custom;
		(custom.Sesion("NY AM").CustomInicio, custom.Sesion("NY AM").CustomFin) = (new TimeSpan(9, 45, 0), new TimeSpan(10, 15, 0));
		c.Check("Custom con minutos (09:45-10:15)", $"{Dentro(custom, "NY AM", martes.AddHours(9.7))} / {Dentro(custom, "NY AM", martes.AddHours(10))}", "no / si 11-03");

		var ajuste = new ConfiguracionSweeps { AjusteManualHoras = 1 };
		c.Check("ajuste manual +1 h: la vela de 08:30 cuenta como 09:30", Dentro(ajuste, "NY AM", martes.AddHours(8.5)), "si 11-03");
	}

	private static void CambiosDeHorario(Comprobador c)
	{
		c.Seccion("F1. Horario de verano automático: EE.UU. y Europa cambian en fechas distintas");

		var pine = new ConfiguracionSweeps { Preset = PresetHorarios.PineOriginal };
		c.Check("PineOriginal: Londres 08:00 hora de Londres = 03:00 ET en enero", Dentro(pine, "Londres", new DateTime(2026, 1, 13, 3, 0, 0)), "si 01-13");
		c.Check("16-03 (EE.UU. ya en verano, Londres no): Londres abre a las 04:00 ET",
			$"03:30 {Dentro(pine, "Londres", new DateTime(2026, 3, 16, 3, 30, 0))} / 04:00 {Dentro(pine, "Londres", new DateTime(2026, 3, 16, 4, 0, 0))}", "03:30 no / 04:00 si 03-16");
		c.Check("30-03 (los dos en verano): otra vez a las 03:00 ET", Dentro(pine, "Londres", new DateTime(2026, 3, 30, 3, 0, 0)), "si 03-30");
		c.Check("27-10 (Londres ya en invierno, EE.UU. no): Londres abre a las 04:00 ET", Dentro(pine, "Londres", new DateTime(2026, 10, 27, 4, 0, 0)) + " / 03:30 " + Dentro(pine, "Londres", new DateTime(2026, 10, 27, 3, 30, 0)), "si 10-27 / 03:30 no");
		c.Check("PineOriginal: Asia 09-18 hora de Tokio (Tokio sin horario de verano: 19:00 ET en noviembre)", Dentro(pine, "Asia", new DateTime(2026, 11, 2, 19, 0, 0)), "si 11-03");
		c.Check("PineOriginal no tiene premercado", Dentro(pine, "Premercado", new DateTime(2026, 11, 3, 9, 0, 0)), "no");

		var cfg = new ConfiguracionSweeps();
		c.Check("premercado en ET a ambos lados del cambio de EE.UU. (30-10 EDT y 02-11 EST)",
			$"{Utc(new DateTime(2026, 10, 30, 8, 30, 0)):HH:mm} UTC / {Utc(new DateTime(2026, 11, 2, 8, 30, 0)):HH:mm} UTC {Dentro(cfg, "Premercado", new DateTime(2026, 11, 2, 8, 30, 0))}",
			"12:30 UTC / 13:30 UTC si 11-02");
	}

	private static void MotorYNiveles(Comprobador c)
	{
		c.Seccion("F1/F2. Motor de sesiones: máximo y mínimo de cada sesión, niveles al cerrar y al volver a empezar");

		var m = new MotorSweeps(new ConfiguracionSweeps());
		var lunes18 = new DateTime(2026, 11, 2, 18, 0, 0);

		foreach (var v in Planas(lunes18, lunes18.AddHours(16), 20000))
		{
			var et = Zonas.AEt(v.InicioUtc);

			if (et.Hour == 23 && et.Minute == 0)
				(v.High, v.Close) = (20050, 20048);

			if (et.Hour == 1 && et.Minute == 30)
				(v.Low, v.Close) = (19950, 19952);

			m.Procesar(v);
		}

		var asia = m.Sesiones.Instancias.First(i => i.Id == "Asia");
		c.Check("Asia del lunes: 18:00 a 01:59 ET, máximo 20050 y mínimo 19950",
			$"{Zonas.AEt(asia.DesdeUtc):HH:mm}-{Zonas.AEt(asia.HastaUtc).AddMinutes(-1):HH:mm} {asia.Alto} {asia.Bajo} abierta {asia.Abierta}", "18:00-01:59 20050 19950 abierta False");
		c.Check("al cerrar: niveles MAX Asia y MIN Asia",
			string.Join(" | ", m.Niveles.Niveles.Where(n => n.Grupo == "sesion:Asia").Select(n => $"{n.Nombre} {n.Precio}")), "MAX Asia 20050 | MIN Asia 19950");
		c.Check("sesiones vistas hasta las 10:00 ET", string.Join(",", m.Sesiones.Instancias.Select(i => i.Id)), "Asia,Londres,Premercado,NY AM");
		c.Check("el premercado deja sus propios niveles",
			string.Join(" | ", m.Niveles.Niveles.Where(n => n.Grupo == "sesion:Premercado").Select(n => n.Nombre)), "MAX Premercado | MIN Premercado");

		foreach (var v in Planas(lunes18.AddHours(16), lunes18.AddHours(25), 20000))
			m.Procesar(v);

		var niveles = m.Niveles.Niveles;
		c.Check("Asia vuelve a empezar (martes 18:00): sus niveles anteriores se retiran y sus líneas dejan de extenderse",
			$"{string.Join(",", niveles.Where(n => n.Grupo == "sesion:Asia").Select(n => n.Estado))} ext {asia.FinExtension is not null}", "Retirado,Retirado ext True");
		c.Check("día operativo nuevo a las 18:00 ET: PDH/PDL = máximo y mínimo del martes (18:00 a 17:00)",
			string.Join(" | ", niveles.Where(n => n.Grupo == "dia").Select(n => $"{n.Nombre} {n.Precio} {n.Dias}")), "MAX Dia anterior 20050 1 | MIN Dia anterior 19950 1");

		var dias = new MotorSweeps(new ConfiguracionSweeps());

		foreach (var v in Planas(lunes18, lunes18.AddDays(4).AddMinutes(1), 20000))
			dias.Procesar(v);

		c.Check("4 cambios de día con N = 3: Dia anterior, Dia -2, Dia -3; el cuarto se retira",
			string.Join(",", dias.Niveles.Niveles.Where(n => n.Grupo == "dia" && n.Lado == Lado.Buyside).Select(n => n.Activo ? n.Origen : "retirado")), "retirado,Dia -3,Dia -2,Dia anterior");

		var pine = new MotorSweeps(new ConfiguracionSweeps { PineCompatibilityMode = true, Preset = PresetHorarios.PineOriginal });

		foreach (var v in Planas(lunes18, lunes18.AddHours(22), 20000))
			pine.Procesar(v);

		c.Check("PineCompatibilityMode: un par (última sesión cerrada, NY AM) más el de Londres durante NY AM",
			string.Join(" | ", pine.Niveles.Activos.Where(pine.Niveles.Vigilado).Select(n => $"{n.Grupo} {n.Nombre}")), "pine-londres MAX Londres | pine-londres MIN Londres | pine MAX NY AM | pine MIN NY AM");

		foreach (var v in Planas(lunes18.AddHours(22), lunes18.AddHours(26), 20000))
			pine.Procesar(v);

		c.Check("al terminar NY PM, el par de Londres se borra (fSHp = 10e6, fSLp = 0 en el Pine)",
			string.Join(" | ", pine.Niveles.Activos.Where(pine.Niveles.Vigilado).Select(n => $"{n.Grupo} {n.Nombre}")), "pine MAX NY PM | pine MIN NY PM");
	}

	private static void Tabla(Comprobador c)
	{
		c.Seccion("F1. Tabla de sesiones: abre en / cierra en / fin de semana");

		var cfg = new ConfiguracionSweeps();
		var m = new MotorSesiones(cfg);
		var ahora = Utc(new DateTime(2026, 11, 3, 10, 0, 0));
		string Fila(string id, DateTime utc) => m.Tabla(cfg.Sesion(id), utc) is var (abierta, fecha, estado) ? $"{(abierta ? "verde" : "rojo")} {estado}" : "";

		c.Check("martes 10:00 ET: NY AM abierta (cierra a las 12:00)", Fila("NY AM", ahora), "verde Cierra en 02:00:00");
		c.Check("premercado: abre mañana a las 08:30", Fila("Premercado", ahora), "rojo Abre en 22:30:00");
		c.Check("Asia: abre a las 18:00", Fila("Asia", ahora), "rojo Abre en 08:00:00");
		c.Check("fecha y hora en ET", m.Tabla(cfg.Sesion("Asia"), ahora).FechaHora, "03/11/2026 10:00:00 ET");
		c.Check("Asia a las 23:30: cierra a las 02:00 del día siguiente", Fila("Asia", Utc(new DateTime(2026, 11, 3, 23, 30, 0))), "verde Cierra en 02:30:00");
		c.Check("sábado: fin de semana", Fila("NY AM", Utc(new DateTime(2026, 11, 7, 10, 0, 0))), "rojo Fin de semana");
	}
}
