namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// Verificação de CAPTCHA (hoje Cloudflare Turnstile) no cadastro e na recuperação de senha —
/// proteção contra automação de criação de contas e envio de e-mails em massa
/// (docs/12-seguranca.md).
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, CancellationToken ct = default);
}
