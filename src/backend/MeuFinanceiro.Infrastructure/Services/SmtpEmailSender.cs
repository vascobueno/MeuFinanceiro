using System.Net;
using System.Net.Mail;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// SMTP configurável via appsettings (`Smtp:Host`/`Port`/`User`/`Password`/`FromEmail`/
/// `FromName`) — funciona com qualquer provedor (Gmail, Outlook, SendGrid, etc.), sem
/// dependência nova (usa o SmtpClient embutido do .NET). Sem `Smtp:Host` configurado, loga o
/// e-mail inteiro em vez de tentar enviar — permite testar o fluxo de recuperação de senha
/// localmente sem precisar de credencial de e-mail.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation(
                "Smtp:Host não configurado — e-mail não enviado, só logado.\nPara: {ToEmail}\nAssunto: {Subject}\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var user = _configuration["Smtp:User"];
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"] ?? user ?? "no-reply@meufinanceiro.local";
        var fromName = _configuration["Smtp:FromName"] ?? "MeuFinanceiro";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(user) ? null : new NetworkCredential(user, password)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message, ct);
    }
}
