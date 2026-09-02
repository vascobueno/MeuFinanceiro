using System.Net;
using System.Text;
using FluentAssertions;
using MeuFinanceiro.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Testa TurnstileCaptchaVerifier isoladamente (sem precisar do WebApplicationFactory nem de
/// bater na API real do Cloudflare) — usa um HttpMessageHandler fake pra simular a resposta
/// de https://challenges.cloudflare.com/turnstile/v0/siteverify.
/// </summary>
[Trait("Category", "Integration")]
public class TurnstileCaptchaVerifierTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        public FakeHandler(string responseJson) => _responseJson = responseJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private static IConfiguration BuildConfig(string? secretKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(secretKey is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["Turnstile:SecretKey"] = secretKey })
            .Build();

    [Fact]
    public async Task Sem_secret_key_configurado_nao_bloqueia()
    {
        var verifier = new TurnstileCaptchaVerifier(new HttpClient(new FakeHandler("{}")), BuildConfig(null));

        (await verifier.VerifyAsync("qualquer-token-ou-vazio")).Should().BeTrue();
    }

    [Fact]
    public async Task Com_secret_key_e_resposta_success_true_libera()
    {
        var verifier = new TurnstileCaptchaVerifier(new HttpClient(new FakeHandler("""{"success": true}""")), BuildConfig("secret-de-teste"));

        (await verifier.VerifyAsync("token-valido")).Should().BeTrue();
    }

    [Fact]
    public async Task Com_secret_key_e_resposta_success_false_bloqueia()
    {
        var verifier = new TurnstileCaptchaVerifier(new HttpClient(new FakeHandler("""{"success": false}""")), BuildConfig("secret-de-teste"));

        (await verifier.VerifyAsync("token-invalido")).Should().BeFalse();
    }
}
