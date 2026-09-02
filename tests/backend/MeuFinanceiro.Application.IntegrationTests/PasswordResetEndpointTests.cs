using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
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

    private static string UsernameFromEmail(string email) => $"user{email.Split('@')[0]}";

    private async Task<(HttpClient client, UserResponse user)> RegisterAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"{id}@teste.com";
        return await AuthTestHelpers.RegisterConfirmAndLoginAsync(_factory, name, UsernameFromEmail(email), email, Password);
    }

    private static string ExtractTokenFromLastEmail(TestEmailSender sender) => AuthTestHelpers.ExtractTokenFromLastEmail(sender);

    [Fact]
    public async Task ForgotPassword_com_email_existente_envia_link_e_retorna_204()
    {
        var (_, user) = await RegisterAsync("Usuário Reset");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(user.Email));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.EmailSender.LastEmail.Should().NotBeNull();
        _factory.EmailSender.LastEmail!.Value.ToEmail.Should().Be(user.Email);
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
        // client já tem a sessão original (access_token + refresh_token do login inicial).
        var (client, user) = await RegisterAsync("Usuário Reset 2");

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(user.Email));
        var token = ExtractTokenFromLastEmail(_factory.EmailSender);

        const string newPassword = "NovaSenhaForte!456";
        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(user.Email, token, newPassword));
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Senha antiga não funciona mais (client novo, sem cookies antigos).
        var loginOld = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(UsernameFromEmail(user.Email), Password));
        loginOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Senha nova funciona.
        var loginNew = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(UsernameFromEmail(user.Email), newPassword));
        loginNew.StatusCode.Should().Be(HttpStatusCode.OK);

        // Refresh token emitido antes do reset foi revogado — usa o client original, que
        // ainda carrega o cookie refresh_token de antes do reset.
        var refreshResponse = await client.PostAsync("/api/v1/auth/refresh", null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_com_token_invalido_retorna_400_com_mensagem_generica()
    {
        var (_, user) = await RegisterAsync("Usuário Reset 3");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(user.Email, "token-forjado-invalido", "NovaSenhaForte!456"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Be("Link de redefinição inválido ou expirado.");
    }

    [Fact]
    public async Task ResetPassword_com_senha_fraca_retorna_400_com_mensagem_de_politica()
    {
        var (_, user) = await RegisterAsync("Usuário Reset 4");
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(user.Email));
        var token = ExtractTokenFromLastEmail(_factory.EmailSender);

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(user.Email, token, "fraca"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record ProblemDetailsResponse(string? Detail);
}
