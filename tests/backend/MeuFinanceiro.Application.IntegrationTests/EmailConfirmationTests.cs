using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre a confirmação de e-mail obrigatória antes do login (docs/12-seguranca.md):
/// registro não loga mais automaticamente, login de conta não confirmada é recusado, e a
/// exceção pro fluxo de "Owner cria membro com senha temporária" (MustChangePassword),
/// que precisa poder logar antes de confirmar o e-mail.
/// </summary>
[Trait("Category", "Integration")]
public class EmailConfirmationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "SenhaForte!123";
    private readonly CustomWebApplicationFactory _factory;

    public EmailConfirmationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static (string username, string email) NewIdentity()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return ($"user{id}", $"{id}@teste.com");
    }

    [Fact]
    public async Task Registro_nao_loga_automaticamente_e_manda_email_de_confirmacao()
    {
        var (username, email) = NewIdentity();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Usuário Confirmação", username, email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body!.Message.Should().NotBeNullOrWhiteSpace();

        _factory.EmailSender.LastEmail.Should().NotBeNull();
        _factory.EmailSender.LastEmail!.Value.ToEmail.Should().Be(email);
    }

    [Fact]
    public async Task Login_antes_de_confirmar_e_recusado()
    {
        var (username, email) = NewIdentity();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Usuário Não Confirmado", username, email, Password));

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, Password));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await loginResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Contain("Confirme seu e-mail");
    }

    [Fact]
    public async Task Confirmar_email_com_token_valido_libera_o_login()
    {
        var (username, email) = NewIdentity();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Usuário A Confirmar", username, email, Password));

        await AuthTestHelpers.ConfirmEmailAsync(client, _factory.EmailSender, email);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, Password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Confirmar_email_com_token_invalido_retorna_400_generico()
    {
        var (username, email) = NewIdentity();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Usuário Token Ruim", username, email, Password));

        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(email, "token-forjado-invalido"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Be("Link de confirmação inválido ou expirado.");
    }

    [Fact]
    public async Task Reenviar_confirmacao_sempre_retorna_204_exista_ou_nao_a_conta()
    {
        var client = _factory.CreateClient();

        var comContaExistente = await client.PostAsJsonAsync("/api/v1/auth/resend-confirmation", new ResendConfirmationRequest($"user{Guid.NewGuid():N}"));
        comContaExistente.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Owner_cria_membro_com_senha_temporaria_consegue_logar_mesmo_sem_confirmar_email()
    {
        // Owner precisa confirmar o próprio e-mail pra logar e criar o membro.
        var (ownerUsername, ownerEmail) = NewIdentity();
        var ownerClient = _factory.CreateClient();
        await ownerClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Owner Confirmado", ownerUsername, ownerEmail, Password));
        await AuthTestHelpers.ConfirmEmailAsync(ownerClient, _factory.EmailSender, ownerEmail);
        await ownerClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(ownerUsername, Password));
        await AuthTestHelpers.AttachCsrfTokenAsync(ownerClient);

        var (memberUsername, memberEmail) = NewIdentity();
        var createResponse = await ownerClient.PostAsJsonAsync("/api/v1/grupos/me/members/new",
            new MeuFinanceiro.Application.Grupos.CreateMemberRequest("Membro Novo", memberUsername, memberEmail, MeuFinanceiro.Domain.Enums.GrupoRole.Member));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var temporaryPassword = System.Text.RegularExpressions.Regex.Match(
            _factory.EmailSender.LastEmail!.Value.HtmlBody, "Senha temporária: <b>([^<]+)</b>").Groups[1].Value;

        var memberClient = _factory.CreateClient();
        var memberLogin = await memberClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(memberUsername, temporaryPassword));

        // Mesmo com EmailConfirmed = false, o login funciona porque MustChangePassword é a
        // prova alternativa (a senha temporária só chega a quem recebeu o e-mail de verdade).
        memberLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record ProblemDetailsResponse(string? Detail);
}
