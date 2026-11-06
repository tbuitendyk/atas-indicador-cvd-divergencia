namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

internal sealed record ConfiguracionCorreo(string Servidor, int Puerto, bool Ssl, string Usuario, SecureString? Clave, string Para);

/// <summary>
/// Cola de correo en segundo plano: nunca bloquea el hilo de ATAS. Un solo correo por clave (nivel + tipo + barra),
/// 2 reintentos con 10 s de espera, y los errores al log de ATAS.
/// </summary>
internal sealed class Correo : IDisposable
{
	private readonly Action<string, Exception> _log;
	private readonly BlockingCollection<(ConfiguracionCorreo Cfg, string Asunto, string Cuerpo)> _cola = new();
	private readonly HashSet<string> _claves = new();
	private readonly CancellationTokenSource _fin = new();

	public Correo(Action<string, Exception> log)
	{
		_log = log;
		Task.Run(Bucle);
	}

	/// <summary>Espera entre reintentos (las pruebas la acortan).</summary>
	public static TimeSpan EsperaReintento { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>Gancho de prueba: sustituye al envío SMTP real.</summary>
	public static Action<ConfiguracionCorreo, string, string>? Enviador { get; set; }

	/// <summary>Encola un correo. Devuelve false si esa clave ya se envió (anti-duplicados).</summary>
	public bool Encolar(string clave, ConfiguracionCorreo cfg, string asunto, string cuerpo)
	{
		lock (_claves)
		{
			if (!_claves.Add(clave))
				return false;
		}

		_cola.Add((cfg, asunto, cuerpo));
		return true;
	}

	private void Bucle()
	{
		try
		{
			foreach (var (cfg, asunto, cuerpo) in _cola.GetConsumingEnumerable(_fin.Token))
				EnviarConReintentos(cfg, asunto, cuerpo);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			_log("SessionSweepsPro: la cola de correo se detuvo", ex);
		}
	}

	private void EnviarConReintentos(ConfiguracionCorreo cfg, string asunto, string cuerpo)
	{
		for (var intento = 0; intento <= 2; intento++)
		{
			try
			{
				if (Enviador is { } prueba)
					prueba(cfg, asunto, cuerpo);
				else
					EnviarSmtp(cfg, asunto, cuerpo);

				return;
			}
			catch (Exception ex)
			{
				if (intento == 2)
				{
					_log($"SessionSweepsPro: no se pudo enviar el correo '{asunto}'", ex);
					return;
				}

				Thread.Sleep(EsperaReintento);
			}
		}
	}

	private static void EnviarSmtp(ConfiguracionCorreo cfg, string asunto, string cuerpo)
	{
		using var smtp = new SmtpClient(cfg.Servidor, cfg.Puerto)
		{
			EnableSsl = cfg.Ssl,
			Timeout = 20000,
			Credentials = cfg.Clave is null ? new NetworkCredential(cfg.Usuario, "") : new NetworkCredential(cfg.Usuario, cfg.Clave)
		};

		var remitente = string.IsNullOrWhiteSpace(cfg.Usuario) ? cfg.Para.Split(',')[0].Trim() : cfg.Usuario;
		using var mensaje = new MailMessage { From = new MailAddress(remitente), Subject = asunto, Body = cuerpo };
		mensaje.To.Add(cfg.Para);
		smtp.Send(mensaje);
	}

	public void Dispose()
	{
		_fin.Cancel();
		_cola.CompleteAdding();
	}
}
