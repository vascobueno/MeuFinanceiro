# MeuFinanceiro

Sistema financeiro pessoal para lançamentos, recorrências, parcelamentos, conciliação bancária e de cartões, investimentos e projeção financeira.

## Stack
- Angular + PrimeNG + Tailwind CSS
- ASP.NET Core / C#
- Entity Framework Core
- PostgreSQL
- Hospedagem via container/VPS + reverse proxy (sem dependência de Azure — ver `docs/19-deploy.md`)
- Segredos via variável de ambiente em produção (User Secrets em dev)
- Application Insights (opcional, ativado só se `ApplicationInsights:ConnectionString` estiver configurado)
- GitHub + GitHub Copilot
- GitHub Actions para CI (ver `docs/18-ci-cd.md`)

Lista completa de bibliotecas/pacotes com versão exata: `docs/33-versions.md`.

## Regra central
O Excel é usado somente para a migração inicial. Depois da migração, o sistema passa a ser a fonte oficial dos lançamentos.

## Status
Implementação em andamento — ver `docs/22-status-projeto.md` para o checklist detalhado do
que já está pronto (fundação, autenticação com sessão via cookie httpOnly/CSRF/lockout/
CAPTCHA, grupo multi-usuário com perfis de acesso, administração do sistema, contas, cartões,
categorias, lançamentos com parcelamento/recorrência, conciliação bancária/cartão,
investimentos, projeção, relatórios imprimíveis, auditoria e skins de cores) e do que ainda
falta (importação inicial do Excel, parsers reais de extrato/fatura, deploy de produção).

## Como rodar localmente
Ver `docs/23-configuracao-local.md` para o passo a passo completo (inclui SMTP e Turnstile
opcionais em dev). Resumo:

```bash
docker compose up -d
cp src/backend/appsettings.Development.json.example src/backend/MeuFinanceiro.Api/appsettings.Development.json
cd src/backend/MeuFinanceiro.Api && dotnet user-secrets init && dotnet user-secrets set "Jwt:Key" "sua-chave-local-de-pelo-menos-32-caracteres"
cd ../../..
dotnet ef migrations add InitialCreate --project src/backend/MeuFinanceiro.Infrastructure --startup-project src/backend/MeuFinanceiro.Api
dotnet ef database update --project src/backend/MeuFinanceiro.Infrastructure --startup-project src/backend/MeuFinanceiro.Api
dotnet run --project src/backend/MeuFinanceiro.Api

cp src/frontend/environment.development.ts.example src/frontend/meu-financeiro/src/environments/environment.development.ts
cd src/frontend/meu-financeiro && npm install && npm start
```

`npm start` já usa `proxy.config.json` (incluso no repositório) pra encaminhar `/api` pro
backend pela mesma origem — necessário pros cookies de sessão funcionarem em dev
(`docs/12-seguranca.md`).

## Deploy para produção
Ver `docs/19-deploy.md` para o detalhe completo. Resumo:

- **Domínio único** com um reverse proxy (Nginx/Caddy/Traefik) na frente: build estático do
  Angular numa rota, `/api/*` roteado pro backend .NET — necessário pros cookies de sessão
  httpOnly funcionarem sem complicação de CORS entre origens diferentes.
- Variáveis de ambiente obrigatórias: `Jwt__Key` (mínimo 32 caracteres),
  `ConnectionStrings__DefaultConnection`, `App__FrontendBaseUrl`, `Smtp__*` (sem isso ninguém
  recebe o e-mail de confirmação de cadastro), `AllowedHosts`; `Turnstile__SecretKey` é
  opcional (CAPTCHA).
- Rodar `dotnet ef database update` nas migrations pendentes antes de subir uma versão nova
  do backend.
