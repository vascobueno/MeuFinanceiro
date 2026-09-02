using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Grupos;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Cobre a criação direta de usuário pelo Owner (POST me/members/new): conta criada com
/// senha temporária enviada por e-mail, MustChangePassword forçando a troca no primeiro
/// login, e as validações de e-mail/username já em uso.
/// </summary>
[Trait("Category", "Integration")]
public class CreateMemberEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CreateMemberEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string UsernameFromEmail(string email) => $"user{email.Split('@')[0]}";

    private async Task<(HttpClient client, AuthResponse auth)> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"{id}@teste.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, UsernameFromEmail(email), email, "SenhaForte!123"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static string ExtractTemporaryPasswordFromLastEmail(TestEmailSender sender)
    {
        var body = sender.LastEmail!.Value.HtmlBody;
        return Regex.Match(body, "Senha temporária: <b>([^<]+)</b>").Groups[1].Value;
    }

    [Fact]
    public async Task Member_nao_pode_criar_novo_usuario()
    {
        var (clientOwner, ownerAuth) = await RegisterAsync("Owner");
        var (_, memberAuth) = await RegisterAsync("Futuro Member");

        await clientOwner.PostAsJsonAsync("/api/v1/grupos/me/members", new AddMemberRequest(memberAuth.User.Email, GrupoRole.Member));

        var memberClient = _factory.CreateClient();
        var memberLogin = await memberClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(UsernameFromEmail(memberAuth.User.Email), "SenhaForte!123"));
        var memberAuthAfter = (await memberLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberAuthAfter.AccessToken);

        var response = await memberClient.PostAsJsonAsync("/api/v1/grupos/me/members/new",
            new CreateMemberRequest("Novo", "novo.usuario", $"{Guid.NewGuid():N}@teste.com", GrupoRole.Member));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nao_pode_criar_com_email_ja_cadastrado()
    {
        var (clientOwner, _) = await RegisterAsync("Owner Email Dup");
        var (_, outroAuth) = await RegisterAsync("Já Cadastrado");

        var response = await clientOwner.PostAsJsonAsync("/api/v1/grupos/me/members/new",
            new CreateMemberRequest("Novo", "novo.usuario.a", outroAuth.User.Email, GrupoRole.Member));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Contain("e-mail");
    }

    [Fact]
    public async Task Nao_pode_criar_com_username_ja_em_uso()
    {
        var (clientOwner, ownerAuth) = await RegisterAsync("Owner Username Dup");

        var response = await clientOwner.PostAsJsonAsync("/api/v1/grupos/me/members/new",
            new CreateMemberRequest("Novo", UsernameFromEmail(ownerAuth.User.Email), $"{Guid.NewGuid():N}@teste.com", GrupoRole.Member));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Contain("usuário");
    }

    [Fact]
    public async Task Criacao_bem_sucedida_envia_senha_temporaria_e_forca_troca_no_primeiro_login()
    {
        var (clientOwner, _) = await RegisterAsync("Owner Criador");

        var newUsername = $"novo{Guid.NewGuid():N}"[..16];
        var newEmail = $"{Guid.NewGuid():N}@teste.com";
        var createResponse = await clientOwner.PostAsJsonAsync("/api/v1/grupos/me/members/new",
            new CreateMemberRequest("Pessoa Nova", newUsername, newEmail, GrupoRole.Member));

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.EmailSender.LastEmail.Should().NotBeNull();
        _factory.EmailSender.LastEmail!.Value.ToEmail.Should().Be(newEmail);
        var temporaryPassword = ExtractTemporaryPasswordFromLastEmail(_factory.EmailSender);
        temporaryPassword.Should().NotBeNullOrWhiteSpace();

        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(newUsername, temporaryPassword));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        auth.User.MustChangePassword.Should().BeTrue();

        loginClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Senha atual errada -> 400.
        var wrongChange = await loginClient.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest("senha-errada-qualquer", "NovaSenhaForte!456"));
        wrongChange.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Senha atual certa -> 204.
        var changeResponse = await loginClient.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest(temporaryPassword, "NovaSenhaForte!456"));
        changeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Login antigo (senha temporária) não funciona mais.
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(newUsername, temporaryPassword));
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Login novo, sem MustChangePassword.
        var newLogin = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(newUsername, "NovaSenhaForte!456"));
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuth = (await newLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        newAuth.User.MustChangePassword.Should().BeFalse();
    }

    private record ProblemDetailsResponse(string? Detail);
}
