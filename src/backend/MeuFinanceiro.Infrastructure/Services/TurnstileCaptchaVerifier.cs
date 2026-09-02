using System.Net.Http.Json;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// Verifica o token do Cloudflare Turnstile contra a API oficial de verificação
/// (docs/23-configuracao-local.md tem o passo a passo pra gerar Turnstile:SecretKey).
/// Sem Turnstile:SecretKey configurado, não bloqueia — mesmo padrão de degradação graciosa
/// já usado pelo SmtpEmailSender, pra funcionar em dev local sem precisar configurar nada.
/// </summary>
public class TurnstileCaptchaVerifier : ICaptchaVerifier
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public TurnstileCaptchaVerifier(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public async Task<bool> VerifyAsync(string token, CancellationToken ct = default)
    {
        var secret = _configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
            return true;

        var response = await _http.PostAsync(
            "https://challenges.cloudflare.com/turnstile/v0/siteverify",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["secret"] = secret, ["response"] = token }),
            ct);

        var result = await response.Content.ReadFromJsonAsync<TurnstileResult>(cancellationToken: ct);
        return result?.Success ?? false;
    }

    private record TurnstileResult(bool Success);
}
