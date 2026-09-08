using MeuFinanceiro.Application.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Access/refresh token vão em cookie httpOnly (nunca no corpo da resposta nem acessíveis
/// via JavaScript) — docs/12-seguranca.md. O refresh token fica restrito ao Path desta
/// controller (nunca é enviado de volta pro resto da API).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";
    private const string AntiforgeryCookie = "mf-antiforgery";
    private const string XsrfCookie = "XSRF-TOKEN";

    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, IWebHostEnvironment env)
    {
        _authService = authService;
        _env = env;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
        => Ok(await _authService.RegisterAsync(request, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var auth = await _authService.LoginAsync(request, ct);
        SetAuthCookies(auth);
        return Ok(auth.User);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookie, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            return Unauthorized();

        var auth = await _authService.RefreshAsync(new RefreshTokenRequest(refreshToken), ct);
        SetAuthCookies(auth);
        return Ok(auth.User);
    }

    // Chamados automaticamente pelo frontend em todo carregamento de página (app.config.ts),
    // sem ação nenhuma do usuário — não são superfície de força-bruta de credencial como
    // login/register/forgot-password são, então ficam fora do limite de "auth" (10/5min por
    // IP); continuam cobertos só pelo limitador global (200/min).
    [HttpGet("me")]
    [Authorize]
    [DisableRateLimiting]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
        => Ok(await _authService.GetCurrentUserAsync(ct));

    [HttpGet("csrf")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        // GetAndStoreTokens grava o cookie interno do Antiforgery (httpOnly, nunca lido por
        // JS) e devolve o par de tokens — RequestToken é o valor que o Angular precisa ler e
        // ecoar no header X-XSRF-TOKEN, e por isso é escrito aqui num cookie à parte,
        // legível por JS (NÃO é o mesmo valor do cookie interno — são tokens diferentes,
        // ecoar o valor errado faz o ASP.NET Core recusar com "cookie token e request token
        // foram trocados"). O frontend chama isso uma vez no bootstrap do app, antes de
        // qualquer POST/PUT/DELETE.
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = IsSecureCookie(),
            SameSite = SameSiteMode.Strict,
        });
        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshTokenCookie, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
            await _authService.RevokeRefreshTokenAsync(refreshToken, ct);

        ClearAuthCookies();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _authService.ChangePasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await _authService.ConfirmEmailAsync(request, ct);
        return NoContent();
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken ct)
    {
        await _authService.ResendConfirmationAsync(request, ct);
        return NoContent();
    }

    // Em produção, atrás de HTTPS de verdade, sai Secure normalmente. Em Development,
    // Request.IsHttps NÃO é um sinal confiável: o proxy do Angular sempre encaminha pro
    // Kestrel em https://localhost:5001 (proxy.config.json), então o backend enxerga HTTPS
    // mesmo quando o navegador (celular, PC na LAN) conectou no frontend via HTTP puro num IP
    // de rede como 192.168.x.x. Marcar o cookie Secure nesse caso faz o navegador aceitar
    // silenciosamente em localhost (exceção de "origem confiável" do Chrome/Firefox pra
    // localhost/127.0.0.1) mas DESCARTAR o cookie num IP de LAN — login "funciona" no PC e
    // falha silenciosamente no celular, sem erro nenhum no backend (log mostra tudo certo).
    // Por isso em dev usamos sempre não-Secure; em produção, sem esse proxy, IsHttps reflete a
    // conexão real do cliente.
    private bool IsSecureCookie() => !_env.IsDevelopment() && Request.IsHttps;

    private void SetAuthCookies(AuthResponse auth)
    {
        var secure = IsSecureCookie();

        Response.Cookies.Append(AccessTokenCookie, auth.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = auth.AccessTokenExpiresAt,
        });

        // Path restrito a /api/v1/auth — o refresh token nunca é enviado de volta pro resto
        // da API, só pras próprias rotas de auth (refresh/logout).
        Response.Cookies.Append(RefreshTokenCookie, auth.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = auth.RefreshTokenExpiresAt,
            Path = "/api/v1/auth",
        });
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AccessTokenCookie);
        Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions { Path = "/api/v1/auth" });
        // Sem isso, o token de CSRF da sessão que terminou continua no navegador — se a
        // próxima pessoa a logar nessa aba não recarregar a página antes de um POST/PUT/
        // DELETE, o backend recusa com AntiforgeryValidationException (token preso à
        // identidade anterior). AuthService.login()/refresh() no frontend já buscam um token
        // novo depois de autenticar, então isso aqui é só uma segunda camada de defesa.
        Response.Cookies.Delete(AntiforgeryCookie);
        Response.Cookies.Delete(XsrfCookie);
    }
}
