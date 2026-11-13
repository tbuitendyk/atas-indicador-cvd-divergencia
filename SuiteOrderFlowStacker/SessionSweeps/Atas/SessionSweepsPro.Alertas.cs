namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using OrderFlowSuite.Comun;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// S10: alertas y correo de v2.0 (PoliticaAlertasV2: solo en vivo, una vez por evento, nunca con NOTICIA), CSV de barridos
/// y de modelos / gatillos (también los manuales, Origen MANUAL, sin alerta) y restauración de los barridos congelados del
/// día desde el CSV al recargar el historial (T16: mismo resultado, cero alertas).
/// </summary>
public partial class SessionSweepsPro
{
	private const string G29 = "29. CSV";

	private readonly DespachoAlertasV2 _despacho = new();
	private readonly Dictionary<long, string> _manualesGrabados = new();
	private GrabadorCsvV2? _grabador;

	[Display(Name = "Grabar CSV de barridos y modelos", GroupName = G29, Order = 10,
		Description = "%APPDATA%\\ATAS\\OrderFlowSuite\\SessionSweeps\\{Instrumento}\\yyyy-MM-dd_barridos_v3.csv y _modelos_v3.csv (solo en vivo; con instrumento, calibración, fuente, canales y MT5, sin credenciales). Para calibrar.")]
	public bool RecordCsv { get; set; } = true;

	[Display(Name = "Restaurar los barridos del día desde el CSV", GroupName = G29, Order = 20,
		Description = "Al recargar el historial, los barridos ya congelados toman su estado del CSV (los icebergs y stops de la suite solo existen en vivo).")]
	public bool RestoreFromCsv { get; set; } = true;

	/// <summary>Motor nuevo: grabador del instrumento y filas del CSV del día para restaurar.</summary>
	private void PrepararCsvV2(MotorSweeps motor)
	{
		if (string.IsNullOrEmpty(_instrumentoV2))
			return;

		if (_grabador?.Instrumento != _instrumentoV2)
		{
			_grabador?.Dispose();
			_grabador = new GrabadorCsvV2(GrabadorCsvV2.CarpetaPorDefecto, _instrumentoV2)
			{
				Error = (mensaje, error) => this.LogWarn(mensaje + ": " + error.Message),
				Contexto = ContextoCsvV2,
				Salida = () => _cfg.V2.ExitMode.ToString()
			};
		}

		if (RestoreFromCsv)
		{
			// Lo encolado aún sin escribir también cuenta.
			_grabador.Vaciar();
			motor.RestaurarDesde.AddRange(_grabador.LeerBarridos(Zonas.DiaOperativo(DateTime.UtcNow)));
		}
	}

	/// <summary>
	/// Auditoría del CSV (19): instrumento, timeframe del gráfico, fuente de datos, canales y MT5, sin credenciales. ATAS no
	/// expone el nombre del proveedor de datos en la API usada aquí: "NO DISPONIBLE". // VERIFICAR en ATAS real.
	/// </summary>
	private ContextoCsv ContextoCsvV2()
	{
		var canales = CanalesConfigurados();
		var texto = string.Join("+", new[] { "ATAS", canales.Correo ? "CORREO" : "", canales.Telegram ? "TELEGRAM" : "" }.Where(x => x.Length > 0));
		return new ContextoCsv(_motor?.Instrumento ?? new EstadoInstrumento(), "NO DISPONIBLE", $"{ChartInfo?.ChartType} {ChartInfo?.TimeFrame}".Trim(),
			_mboVisto ? "MBO" : "REAL", texto, EstadoMt5Texto());
	}

	/// <summary>Novedades del motor: en el historial se descartan; en vivo, CSV, alerta y correo.</summary>
	private void ProcesarNovedadesV2()
	{
		if (_motor is not { } motor)
			return;

		var novedades = motor.TomarNovedadesV2();

		// En el historial (también al recalcular) no se graba ni suena nada: T16.
		if (!_enVivo || novedades.Count == 0)
			return;

		var ahora = DateTime.UtcNow;
		var modo = motor.UltimoMinutoAprox ? "APROX" : "TICKS";

		foreach (var n in novedades)
		{
			if (RecordCsv && _grabador is { } g)
			{
				if (n.Evaluacion is { } e && n.Tipo is "CONFIRMADO" or "UPGRADE PREMIUM" or "FALLIDO" or "RUPTURA FALLIDA")
					g.Barrido(e, ahora, modo);

				if (n.Setup is { } s && n.Tipo is "GATILLO VÁLIDO" or "MODELO CERRADO" or "MODELO INVALIDADO")
					g.Modelo(s, ahora);
			}

			// Puente MT5 (17): solo las entradas válidas en vivo (el gestor decide si se manda, se simula o no).
			if (n.Tipo == "GATILLO VÁLIDO" && n.Setup is { } entrada && motor.Instrumento.Bloqueo is null)
				Mt5Entrada(motor, entrada, n.Utc);
		}

		var ctx = new ContextoMensaje { Instrumento = motor.Raiz.ToString(), Contrato = motor.Instrumento.Contrato, Mt5 = EstadoMt5Texto(), Precio = motor.Precio };

		foreach (var a in _despacho.Procesar(novedades, _enVivo && motor.Instrumento.Bloqueo is null, _cfg.V2, ctx))
		{
			var canales = _cfg.V2.Canales(a.Categoria);

			if (canales.Atas)
				AddAlert(string.IsNullOrWhiteSpace(SweepAlertSoundFile) ? "alert1" : SweepAlertSoundFile, a.Texto);

			// v4: la ruptura de una línea de sesión también se avisa arriba a la derecha del gráfico.
			if (a.Categoria == CategoriaAlerta.LineaSesion && canales.Atas)
				AvisoLinea(a.Texto);

			if (canales.Correo)
				Enviar(a.Clave, a.Asunto, a.Cuerpo);

			if (canales.Telegram)
				EnviarTelegram(a.Clave, a.Asunto + "\n\n" + a.Cuerpo);
		}
	}

	/// <summary>Gatillo manual (T28): una fila con Origen MANUAL cada vez que cambia la respuesta; nunca alerta.</summary>
	private void GrabarManualV2(ManualTriggerRequest q, ManualTriggerResult r)
	{
		if (!RecordCsv || _grabador is not { } g)
			return;

		var resumen = $"{r.Resultado}|{r.Direccion}|{r.Score}";

		lock (_manualesGrabados)
		{
			if (_manualesGrabados.TryGetValue(q.Id, out var previo) && previo == resumen)
				return;

			_manualesGrabados[q.Id] = resumen;
		}

		g.Manual(q, r, DateTime.UtcNow);
	}
}
