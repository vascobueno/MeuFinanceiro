using System.Text;
using MeuFinanceiro.Api.Filters;
using MeuFinanceiro.Api.Middleware;
using MeuFinanceiro.Infrastructure;
using MeuFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Serviços ---------------------------------------------------------

builder.Services.AddControllers(options =>
{
    options.Filters.Add<WriteAccessFilter>();
    options.Filters.Add<CsrfFilter>();
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAntiforgery(options =>
{
    // Header combinando com o suporte nativo do HttpClient do Angular
    // (withXsrfConfiguration) — o Angular não precisa de código customizado pra reenviar.
    // O cookie interno do Antiforgery carrega o "cookie token" e fica httpOnly — nunca é lido
    // por JS. O valor que o Angular de fato lê e ecoa no header (o "request token", um valor
    // DIFERENTE do cookie token, não uma cópia dele) é escrito manualmente como um cookie
    // "XSRF-TOKEN" à parte em AuthController.Csrf() — mesmo padrão documentado pela Microsoft
    // pra SPAs. Nome fixo (em vez do padrão gerado pelo framework) só pra dar pra apagar esse
    // cookie explicitamente em ClearAuthCookies() no logout.
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "mf-antiforgery";
    // SameAsRequest (não Always): GetAndStoreTokens lança exceção se configurado Always e a
    // requisição não for HTTPS — o TestServer dos testes de integração e um "dotnet run"
    // local sem certificado válido caem nesse caso. Em produção, atrás de HTTPS de verdade,
    // o cookie sai Secure normalmente porque a requisição já chega como HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "MeuFinanceiro API", Version = "v1" });

    var jwtScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Description = "Informe: {seu token JWT}"
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // O access token vem num cookie httpOnly (AuthController.SetAuthCookies), não no
        // header Authorization — sem isso o middleware de autenticação nunca acharia o token.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("access_token", out var token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// Configuração via IConfigureOptions<JwtBearerOptions> (em vez de dentro do .AddJwtBearer(...)
// acima) para ler a configuração de forma tardia (na primeira resolução das options, já com o
// IConfiguration final montado), e não no momento do registro do serviço — que roda antes de
// builder.Build() e por isso não reflete overrides de configuração feitos por
// WebApplicationFactory em testes de integração (ConfigureAppConfiguration só é mesclado
// dentro de Build()).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        // Sem fallback pra uma chave fixa aqui — se Jwt:Key estiver ausente/fraca, a
        // validação no startup (logo após builder.Build(), antes do app aceitar
        // requisições) já derrubou o processo. Nunca aceitar como válido um token assinado
        // com uma string fixa que está no código-fonte.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// Rate limiting básico (docs/12-seguranca.md: "rate limiting quando aplicável").
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Limite dedicado, mais restrito, pros endpoints de auth (login/registro/recuperação de
    // senha) — o GlobalLimiter acima (200/min) é permissivo demais pra proteger contra
    // força bruta/credential stuffing sozinho. Valores configuráveis (em vez de literais)
    // pra CustomWebApplicationFactory poder afrouxar isso nos testes de integração — o
    // TestServer não tem IP real, então muitos testes acabariam compartilhando a mesma
    // partição ("anonymous") e esbarrando no limite pensado pra um IP de verdade.
    var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 10;
    var authWindowMinutes = builder.Configuration.GetValue<int?>("RateLimiting:Auth:WindowMinutes") ?? 5;
    options.AddPolicy("auth", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(authWindowMinutes)
            }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Necessário pra cookie httpOnly ir/vir em requisições cross-origin (dev: front
            // e API em portas diferentes) — exige origem explícita, incompatível com
            // AllowAnyOrigin (WithOrigins acima já não usa wildcard).
            .AllowCredentials());
});

if (!string.IsNullOrWhiteSpace(builder.Configuration["ApplicationInsights:ConnectionString"]))
    builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Nunca subir com uma Jwt:Key ausente/fraca — sem essa checagem, um erro de config em
// produção (ex.: nome errado de variável de ambiente) faria o backend aceitar como válido
// qualquer token assinado com a antiga chave de fallback fixa no código-fonte.
var jwtKey = app.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Configuração 'Jwt:Key' ausente ou fraca (mínimo 32 caracteres) — defina antes de subir a API.");

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

if (args.Contains("--corrigir-usernames-antigos"))
{
    using var backfillScope = app.Services.CreateScope();
    await LegacyUsernameBackfiller.RunAsync(backfillScope.ServiceProvider);
    return;
}

// --- Pipeline -----------------------------------------------------------

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necessário para WebApplicationFactory em testes de integração (docs/20-testes.md).
public partial class Program { }
