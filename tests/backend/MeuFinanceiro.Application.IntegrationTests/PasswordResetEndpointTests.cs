using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>Cobre o fluxo de recuperação de senha: forgot-password (sem enumeração de contas) e reset-password.</summary>
[Trait("Category", "Integration")]
public class PasswordResetEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "SenhaForte!123";
    private readonly CustomWebApplicationFactory _factory;

    public PasswordResetEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, AuthResponse auth)> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@teste.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, email, Password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static string ExtractTokenFromLastEmail(TestEmailSender sender)
    {
        var body = sender.LastEmail!.Value.HtmlBody;
        var href = System.Text.RegularExpressions.Regex.Match(body, "href=\"([^\"]+)\"").Groups[1].Value;
        var query = QueryHelpers.ParseQuery(new Uri(href).Query);
        return query["token"]!;
    }

    [Fact]
    public async Task ForgotPassword_com_email_existente_envia_link_e_retorna_204()
    {
        var (_, auth) = await RegisterAsync("Usuário Reset");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(auth.User.Email));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.EmailSender.LastEmail.Should().NotBeNull();
        _factory.EmailSender.LastEmail!.Value.ToEmail.Should().Be(auth.User.Email);
    }

    [Fact]
    public async Task ForgotPassword_com_email_inexistente_tambem_retorna_204_sem_enviar_email()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest($"{Guid.NewGuid():N}@teste.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_com_token_valido_troca_a_senha_e_revoga_refresh_tokens_antigos()
    {
        var (_, auth) = await RegisterAsync("Usuário Reset 2");
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(auth.User.Email));
        var token = ExtractTokenFromLastEmail(_factory.EmailSender);

        const string newPassword = "NovaSenhaForte!456";
        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(auth.User.Email, token, newPassword));
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Senha antiga não funciona mais.
        var loginOld = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(auth.User.Email, Password));
        loginOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Senha nova funciona.
        var loginNew = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(auth.User.Email, newPassword));
        loginNew.StatusCode.Should().Be(HttpStatusCode.OK);

        // Refresh token emitido antes do reset foi revogado.
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_com_token_invalido_retorna_400_com_mensagem_generica()
    {
        var (_, auth) = await RegisterAsync("Usuário Reset 3");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(auth.User.Email, "token-forjado-invalido", "NovaSenhaForte!456"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Be("Link de redefinição inválido ou expirado.");
    }

    [Fact]
    public async Task ResetPassword_com_senha_fraca_retorna_400_com_mensagem_de_politica()
    {
        var (_, auth) = await RegisterAsync("Usuário Reset 4");
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(auth.User.Email));
        var token = ExtractTokenFromLastEmail(_factory.EmailSender);

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(auth.User.Email, token, "fraca"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record ProblemDetailsResponse(string? Detail);
}
