using System.Net.Http.Json;
using MeuFinanceiro.Application.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Desde a confirmação de e-mail obrigatória (docs/12-seguranca.md), o registro não loga
/// mais automaticamente — os testes que precisam de uma sessão autenticada logo após
/// registrar têm que confirmar o e-mail primeiro, extraindo o token do TestEmailSender
/// (mesmo padrão de PasswordResetEndpointTests.ExtractTokenFromLastEmail).
/// </summary>
public static class AuthTestHelpers
{
    public static string ExtractTokenFromLastEmail(TestEmailSender sender)
    {
        var body = sender.LastEmail!.Value.HtmlBody;
        var href = System.Text.RegularExpressions.Regex.Match(body, "href=\"([^\"]+)\"").Groups[1].Value;
        var query = QueryHelpers.ParseQuery(new Uri(href).Query);
        return query["token"]!;
    }

    public static async Task ConfirmEmailAsync(HttpClient client, TestEmailSender emailSender, string email)
    {
        var token = ExtractTokenFromLastEmail(emailSender);
        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(email, token));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Busca o cookie XSRF-TOKEN (POST/PUT/DELETE fora do AuthController exigem isso desde a
    /// Fase 7 — CsrfFilter) e anexa o mesmo valor como header X-XSRF-TOKEN em todo request
    /// futuro desse client (DefaultRequestHeaders vale pra cada chamada, não só a próxima) —
    /// mesmo padrão de double-submit cookie que o HttpClient do Angular faz sozinho no app
    /// de verdade; aqui precisa ser manual porque o teste não tem um "document.cookie".
    /// </summary>
    public static async Task AttachCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();

        var setCookie = response.Headers.GetValues("Set-Cookie").First(h => h.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(setCookie.Split(';')[0]["XSRF-TOKEN=".Length..]);

        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    /// <summary>
    /// Registra, confirma o e-mail, loga e já anexa o token CSRF — devolve o client pronto
    /// pra fazer qualquer chamada autenticada, incluindo escritas (desde a Fase 7, os tokens
    /// de sessão vão em cookie httpOnly, não no corpo da resposta; o HttpClient criado por
    /// CustomWebApplicationFactory já lida com cookies automaticamente entre chamadas no
    /// mesmo client — WebApplicationFactoryClientOptions.HandleCookies é true por padrão —
    /// então não tem Authorization header pra montar manualmente).
    /// </summary>
    public static async Task<(HttpClient client, UserResponse user)> RegisterConfirmAndLoginAsync(
        CustomWebApplicationFactory factory, string name, string username, string email, string password)
    {
        var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, username, email, password));
        registerResponse.EnsureSuccessStatusCode();

        await ConfirmEmailAsync(client, factory.EmailSender, email);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password));
        loginResponse.EnsureSuccessStatusCode();

        await AttachCsrfTokenAsync(client);

        var user = (await loginResponse.Content.ReadFromJsonAsync<UserResponse>())!;
        return (client, user);
    }

    /// <summary>Loga um usuário já existente num client novo (ex.: pegar um token com claims atualizadas após mudar de grupo) — já com CSRF anexado.</summary>
    public static async Task<(HttpClient client, UserResponse user)> LoginAsync(CustomWebApplicationFactory factory, string username, string password)
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password));
        loginResponse.EnsureSuccessStatusCode();

        await AttachCsrfTokenAsync(client);

        var user = (await loginResponse.Content.ReadFromJsonAsync<UserResponse>())!;
        return (client, user);
    }
}
