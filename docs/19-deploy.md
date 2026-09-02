# 19 — Deploy

Sem dependência de Azure — qualquer VPS, PaaS (Railway, Render, Fly.io) ou container
funciona. A única exigência real é a topologia abaixo, por causa dos cookies de sessão.

## Topologia recomendada

```text
                    domínio único (ex.: app.meufinanceiro.com)
                              |
                     reverse proxy (Nginx/Caddy/Traefik)
                     /                              \
        build estático do Angular              /api/* -> backend .NET
```

Frontend e API **atrás de um único domínio**, com o proxy roteando `/api/*` pro backend e
tudo mais pro build estático do Angular — é o que faz os cookies `httpOnly`/`SameSite=Strict`
(docs/12-seguranca.md) funcionarem sem precisar lidar com CORS entre origens diferentes.
Alternativa que também funciona: subdomínios do mesmo domínio-base (`app.meufinanceiro.com` +
`api.meufinanceiro.com`) — `SameSite=Strict` considera subdomínios do mesmo domínio-base como
"mesmo site". Duas origens completamente diferentes (domínios ou até portas diferentes sem
proxy) **não** funcionam pra isso.

## Variáveis de ambiente obrigatórias

Convenção do .NET pra configuração aninhada via variável de ambiente: dois underscores no
lugar de `:` (ex.: `Jwt:Key` vira `Jwt__Key`).

| Variável | O que é |
|---|---|
| `Jwt__Key` | Chave de assinatura JWT, mínimo 32 caracteres (ex.: `openssl rand -base64 48`) — o backend recusa subir sem isso (docs/12-seguranca.md). |
| `ConnectionStrings__DefaultConnection` | Connection string do PostgreSQL de produção. |
| `Cors__AllowedOrigins__0` | Domínio real do frontend (só relevante se front/API não estiverem atrás do mesmo proxy). |
| `App__FrontendBaseUrl` | Base usada nos links de e-mail (confirmação, reset de senha). |
| `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password`, `Smtp__FromEmail` | Provedor de e-mail real — sem isso, ninguém recebe o link de confirmação de cadastro nem consegue se registrar. |
| `Turnstile__SecretKey` | Opcional — sem isso, o CAPTCHA não bloqueia nada. |
| `AllowedHosts` | Domínio real (ex.: `app.meufinanceiro.com`) — `appsettings.json` comitado tem `"*"`, aceitável só em dev. |

## Banco de dados

PostgreSQL gerenciado (qualquer provedor) ou self-hosted num container — sem exigência de
Azure Database for PostgreSQL especificamente. Rodar `dotnet ef database update` nas
migrations pendentes antes de subir uma versão nova do backend (nunca aplicar migration
automaticamente num pipeline sem revisão — mesma prática já usada localmente neste projeto).

## Pendências antes de produção

RPO/RTO e estratégia de backup do banco ainda precisam ser definidos. `docs/22-status-projeto.md`
mantém o checklist de infra (DEV/STAGING/PROD/Backup/Monitoring) atualizado.
