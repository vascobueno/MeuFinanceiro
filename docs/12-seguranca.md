# 12 — Segurança

- HTTPS.
- JWT emitido pelo próprio backend (não é um provedor de identidade externo) — ver detalhe
  de cookie httpOnly/CSRF abaixo.
- Ownership por **grupo**, não por usuário individual: todo dado financeiro pertence a um
  `GrupoId`, compartilhado entre os membros desse grupo. `GrupoId`/`GrupoRole`/
  `GrupoAccessProfile` são sempre derivados do JWT via `ICurrentUserService`, nunca
  confiados do cliente.
- `GrupoRole` (Owner/Member): só o Owner gerencia membros do grupo (adicionar/remover/
  mudar perfil de acesso) — checado em `GruposController.EnsureOwner()`.
- `GrupoAccessProfile` (Completo/Editor/Visualizador) controla, dentro do grupo:
  visibilidade das páginas de gestão (Configurações/Membros — só Completo/Owner vêem,
  `fullAccessGuard` no frontend + checagem em `GruposController.GetMine()`) e permissão de
  escrita (Visualizador é somente-leitura — bloqueado centralmente pelo `WriteAccessFilter`,
  um `IAsyncActionFilter` global no backend, e escondido no frontend via `AuthService.canWrite()`/
  `canWriteGuard`). Owner é sempre Completo — nunca pode ser rebaixado (evita autotravamento).
- Role de sistema `Administrator` (ASP.NET Identity role, `[Authorize(Roles="Administrator")]`)
  é ortogonal ao grupo pessoal do administrador — administra todos os grupos via
  `AdminController` (`adminGuard` no frontend), semeada só por configuração
  (`AdminSeed:Email`), sem auto-promoção.
- Recuperação de senha: token gerado pelo `DataProtectorTokenProvider` padrão do ASP.NET
  Identity (via `SecurityStamp`, sem tabela própria), validade de 1h, uso único. `POST
  /auth/forgot-password` sempre responde 204 e manda a mesma auditoria
  (`ForgotPasswordRequested`) exista ou não o e-mail — nunca revela se uma conta existe.
  Envio por SMTP configurável (`Smtp:*`); sem `Smtp:Host` configurado, o link só é logado
  (nunca exposto na resposta da API).
- Login é por nome de usuário (`UserName`), não e-mail — unicidade garantida pelo
  `UserValidator` padrão do Identity, com checagem prévia amigável em `AuthService.RegisterAsync`.
  O e-mail continua existindo na conta (recuperação de senha), só deixa de ser o identificador
  de entrada.
- Owner cria usuário direto (`POST grupos/me/members/new`), diferente do convite por e-mail
  (`POST grupos/me/members`, que só move quem já tem conta própria): o sistema cria a conta
  inteira com uma senha temporária gerada (`RandomNumberGenerator`, garantindo maiúscula/
  minúscula/dígito/símbolo) e envia por e-mail. `ApplicationUser.MustChangePassword` fica
  `true` até a pessoa trocar a senha em `POST auth/change-password` — o `authGuard` no
  frontend redireciona pra `/trocar-senha-inicial` antes de qualquer outra tela enquanto o
  flag estiver ativo. A troca de senha também confirma o e-mail (`EmailConfirmed = true`),
  usada como prova de que a pessoa recebeu a senha temporária.
- **Bloqueio de conta por tentativas de senha errada**: `SignInManager.CheckPasswordSignInAsync`
  (não mais `UserManager.CheckPasswordAsync` direto) — 5 tentativas erradas bloqueiam a conta
  por 15 min (`Lockout.MaxFailedAccessAttempts`/`DefaultLockoutTimeSpan`, `DependencyInjection.cs`).
  A auditoria distingue `LoginFailed` de `LoginLockedOut`, mas a mensagem pro usuário é a
  mesma nos dois casos — não revela que a conta existe e só está bloqueada.
- **Rate limiting dedicado pros endpoints de auth**: além do limitador global (200 req/min por
  usuário/IP), `AuthController` tem uma policy própria mais restrita (10 req/5min por IP,
  configurável via `RateLimiting:Auth:PermitLimit`/`WindowMinutes`) — o global sozinho é
  permissivo demais contra força bruta/credential stuffing.
- **Chave JWT nunca tem fallback**: `Program.cs` valida `Jwt:Key` (mínimo 32 caracteres) logo
  após `builder.Build()`, antes do app aceitar qualquer requisição — se estiver ausente ou
  fraca, o processo nem sobe. Elimina o risco de aceitar tokens assinados com uma chave fixa
  de fallback que estivesse no código-fonte.
- **Cabeçalhos de segurança + HSTS**: `SecurityHeadersMiddleware` manda
  `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options` e `Permissions-Policy` em
  toda resposta; `app.UseHsts()` fora de Development.
- **Confirmação de e-mail obrigatória**: `POST /auth/register` não loga mais automaticamente
  — manda um link de confirmação (mesmo mecanismo de token do reset de senha) e `POST
  /auth/login` recusa contas com `EmailConfirmed == false` (`BusinessRuleException
  "email-nao-confirmado"`). Exceção: contas criadas pelo Owner com `MustChangePassword` (ver
  acima) podem logar antes de confirmar — a senha temporária recebida por e-mail já prova o
  recebimento, e a troca de senha obrigatória confirma o e-mail nesse fluxo.
- **CAPTCHA (Cloudflare Turnstile)** no cadastro e na recuperação de senha —
  `ICaptchaVerifier`/`TurnstileCaptchaVerifier` verifica o token contra a API do Cloudflare;
  sem `Turnstile:SecretKey` configurado, não bloqueia (mesmo padrão de degradação graciosa do
  SMTP — funciona em dev local sem precisar configurar nada).
- **Sessão via cookie httpOnly**, não mais `localStorage`: `POST /auth/login`/`/refresh`
  gravam `access_token` (1h) e `refresh_token` (7 dias, `Path=/api/v1/auth`) como cookies
  `HttpOnly`+`SameSite=Strict` (`Secure` acompanha o esquema da requisição — só é marcado em
  HTTPS de verdade); nenhum dos dois é acessível via JavaScript. `JwtBearerEvents.OnMessageReceived`
  ensina o middleware de autenticação a ler o token do cookie. `GET /auth/me` permite ao
  frontend rehidratar a sessão após um F5 (sem token nenhum em `localStorage` pra ler direto).
- **CSRF (double-submit cookie)**: como a sessão agora depende de cookie, todo POST/PUT/DELETE
  fora do `AuthController` passa por `CsrfFilter`, que valida o par cookie/header via
  `IAntiforgery` nativo do ASP.NET Core. `GET /auth/csrf` grava o cookie `XSRF-TOKEN`
  (legível por JS, com o *request token* — um valor diferente do cookie interno do
  Antiforgery, que fica httpOnly) — o `HttpClient` do Angular já sabe ecoar isso sozinho no
  header `X-XSRF-TOKEN` (`withXsrfConfiguration`), sem código customizado no frontend.
- Auditoria.
- EF Core/queries parametrizadas.
- Não registrar senha, tokens, connection strings ou conteúdo integral de documentos.
- Menor privilégio.
- Scan de dependências vulneráveis no CI (`dotnet list package --vulnerable`, `npm audit`) +
  Dependabot semanal (`.github/dependabot.yml`) — docs/18-ci-cd.md.
- **Upload de anexos** (lançamentos e parcelamentos — docs/06, docs/07): a allowlist é conferida
  pela **assinatura do arquivo** (magic number), não pela extensão nem pelo `Content-Type` do
  multipart. Os dois são escolhidos pelo cliente, então uma allowlist que confie neles não filtra
  nada — bastaria renomear um executável para `.pdf`. Aceitos: PDF, JPEG, PNG, WEBP, HEIC; até
  10 MB e 10 anexos por item. O `ContentType` gravado é o deduzido do conteúdo, e a extensão só é
  aceita se corresponder ao tipo detectado. Ver `FileSignatureValidator`.
- **Download de anexos**: sai sempre como `Content-Disposition: attachment` (nunca `inline`) com
  `X-Content-Type-Options: nosniff` — servir conteúdo enviado por usuário para o navegador
  renderizar é o caminho clássico de XSS armazenado. O nome original do arquivo fica só na coluna
  do banco; a chave no storage é `{grupoId}/{attachmentId}{extensão}`, para que nome vindo do
  cliente nunca vire caminho.
- **Isolamento por grupo nos anexos**: o projeto **não** tem `HasQueryFilter` global, então toda
  consulta de anexo filtra `GrupoId` explicitamente — sem isso, um GUID adivinhado bastaria para
  baixar documento de outro grupo. Anexo de outro grupo devolve **404**, não 403: um 403
  confirmaria que aquele id existe. Coberto por teste de regressão
  (`AttachmentEndpointTests.Anexo_de_outro_grupo_nao_deve_ser_baixado`).

## Número de cartão
O número completo do cartão e o CVV nunca são guardados nem trafegam para a API: o cadastro de
cartões do grupo tem só os primeiros dígitos (até 6) e os 4 últimos. Colar o número inteiro num
dos campos o divide no navegador e descarta o meio antes de qualquer requisição; a API recusa
mais do que isso (`numero-completo-nao-guardado`). Ver docs/04 (IssuedCard).
