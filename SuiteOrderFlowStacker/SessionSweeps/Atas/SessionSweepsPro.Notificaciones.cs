namespace ATAS.Indicators.Custom;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using SessionSweeps.Nucleo;

using Utils.Common.Logging;

/// <summary>
/// v3 S14: correo (SMTP) y Telegram por una cola asíncrona con reintentos limitados y registro del último envío; las
/// credenciales (contraseña SMTP y token del bot) cifradas con DPAPI, nunca en los ajustes de ATAS, logs ni CSV.
/// </summary>
public partial class SessionSweepsPro
{
	private const string MascaraGuardado = "•••••• (guardado)";

	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

	private AlmacenCredenciales? _credenciales;
	private ColaEnvios? _envios;
	private bool _avisoSinSmtp, _avisoSinTelegram;

	private AlmacenCredenciales Credenciales => _credenciales ??= new AlmacenCredenciales(AlmacenCredenciales.CarpetaPorDefecto, new ProtectorDpapi());

	private ColaEnvios Envios => _envios ??= new ColaEnvios(texto => Credenciales.Limpiar(texto, "smtp", "telegram"));

	/// <summary>Último resultado de cada canal (recuadro 15 del panel).</summary>
	private IReadOnlyCollection<ResultadoEnvio> UltimosEnvios => _envios?.Ultimos ?? Array.Empty<ResultadoEnvio>();

	private bool ExisteCredencial(string nombre)
	{
		try
		{
			return Credenciales.Existe(nombre);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void GuardarCredencial(string nombre, string secreto)
	{
		try
		{
			Credenciales.Guardar(nombre, secreto);
			(_avisoSinSmtp, _avisoSinTelegram) = (false, false);
		}
		catch (Exception ex)
		{
			this.LogWarn($"Sessions Sweeps Pro: no se pudo guardar la credencial '{nombre}' (DPAPI): {ex.GetType().Name}");
		}
	}

	private void GuardarCredencial(string nombre, SecureString secreto)
	{
		var puntero = IntPtr.Zero;

		try
		{
			puntero = Marshal.SecureStringToGlobalAllocUnicode(secreto);
			GuardarCredencial(nombre, Marshal.PtrToStringUni(puntero) ?? "");
		}
		finally
		{
			if (puntero != IntPtr.Zero)
				Marshal.ZeroFreeGlobalAllocUnicode(puntero);
		}
	}

	private void BorrarCredencial(string nombre)
	{
		try
		{
			Credenciales.Borrar(nombre);
		}
		catch (Exception ex)
		{
			this.LogWarn($"Sessions Sweeps Pro: no se pudo borrar la credencial '{nombre}': {ex.GetType().Name}");
		}
	}

	/// <summary>Correo por la cola (sin bloquear ATAS). Sin usuario o contraseña no se intenta (un aviso en el registro).</summary>
	private void Enviar(string clave, string asunto, string cuerpo)
	{
		if (!EmailEnabled || string.IsNullOrWhiteSpace(EmailTo))
			return;

		var clavePrivada = ExisteCredencial("smtp") ? Credenciales.Leer("smtp") : null;

		if (string.IsNullOrWhiteSpace(SmtpUser) || string.IsNullOrEmpty(clavePrivada))
		{
			if (!_avisoSinSmtp)
				this.LogWarn("Sessions Sweeps Pro: correo sin configurar (usuario y contraseña SMTP en 13. Correo y Telegram); no se envía.");

			_avisoSinSmtp = true;
			return;
		}

		var (servidor, puerto, tls, usuario, para, de) = (SmtpServer, SmtpPort, UseSSL, SmtpUser, EmailTo, string.IsNullOrWhiteSpace(EmailFrom) ? SmtpUser : EmailFrom);

		Envios.Encolar("correo", clave, _ => Task.Run(() =>
		{
			using var smtp = new SmtpClient(servidor, puerto) { EnableSsl = tls, Timeout = 20000, Credentials = new NetworkCredential(usuario, clavePrivada) };
			using var mensaje = new MailMessage { From = new MailAddress(de), Subject = asunto, Body = cuerpo };

			foreach (var d in para.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				mensaje.To.Add(d);

			smtp.Send(mensaje);
		}));
	}

	/// <summary>Telegram (Bot API sendMessage) por la cola. Sin token o chat ID no se intenta.</summary>
	private void EnviarTelegram(string clave, string texto)
	{
		var token = ExisteCredencial("telegram") ? Credenciales.Leer("telegram") : null;

		if (string.IsNullOrEmpty(token) || string.IsNullOrWhiteSpace(TelegramChatId))
		{
			if (!_avisoSinTelegram)
				this.LogWarn("Sessions Sweeps Pro: Telegram sin configurar (token y chat ID en 13. Correo y Telegram); no se envía.");

			_avisoSinTelegram = true;
			return;
		}

		var chat = TelegramChatId.Trim();

		Envios.Encolar("telegram", clave, async fin =>
		{
			using var contenido = new FormUrlEncodedContent(new Dictionary<string, string> { ["chat_id"] = chat, ["text"] = texto });
			using var respuesta = await Http.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", contenido, fin).ConfigureAwait(false);

			// El mensaje de error nunca lleva la URL (lleva el token).
			if (!respuesta.IsSuccessStatusCode)
				throw new InvalidOperationException($"Telegram respondió {(int)respuesta.StatusCode} {respuesta.ReasonPhrase}");
		});
	}

	private void EnviarPrueba()
	{
		var hora = Zonas.PorNombre(EmailTimeZone);
		var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, hora).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		Enviar("prueba|" + Guid.NewGuid(), "Sessions Sweeps Pro - correo de prueba", $"Correo de prueba enviado a las {ahora}.\nSi lo recibe, el correo esta bien configurado.");
	}
}
