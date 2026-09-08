# 23 — Configuração do Ambiente Local

## Pré-requisitos
- .NET 10 SDK
- Node.js 22.22.3+, 24.15.0+ ou 26+ (faixa oficial do Angular 22 — ver `src/frontend/meu-financeiro/package.json`). **Atenção:** Node 24.11.x (comum em instalações recentes) NÃO é suficiente para o Angular 22 — é preciso 24.15.0 ou mais recente.
- Docker Desktop (ou engine compatível)

## Passo a passo

1. **Subir dependências locais (PostgreSQL + storage S3-compatible):**
   ```bash
   docker compose up -d
   ```
   Isso cria os containers `meufinanceiro-postgres` (porta 5432, imagem `postgres:18`)
   e `meufinanceiro-minio` (porta 9000 API S3 / 9001 console web, imagem
   `minio/minio:RELEASE.2025-09-07T16-13-09Z`) — mesmo protocolo usado em produção
   (`IFileStorageService`/`S3FileStorageService`, docs/03-arquitetura.md), só troca o
   `Storage:ServiceUrl`. Acesse o console em `http://localhost:9001` com as credenciais
   `minioadmin`/`minioadmin123` (ou as que você configurar via `MINIO_ROOT_USER`/
   `MINIO_ROOT_PASSWORD`) pra ver os buckets/arquivos gravados.
   Dados do MinIO persistem em `docker-data/minio/` (ignorado pelo Git). Os dados do
   Postgres ficam num **named volume** do Docker (`postgres-data`), montado em
   `/var/lib/postgresql` (não `.../data` — convenção da imagem oficial 18+, ver
   [docker-library/postgres#1259](https://github.com/docker-library/postgres/pull/1259)). Para
   apagar os dados do Postgres local: `docker volume rm meufinanceiro_postgres-data` (ou
   `docker compose down -v`).

2. **Configurar o backend:**
   ```bash
   cp src/backend/appsettings.Development.json.example src/backend/MeuFinanceiro.Api/appsettings.Development.json
   ```
   Ajuste a `Jwt:Key` localmente — **mínimo 32 caracteres**, o backend recusa subir com uma
   chave ausente ou mais curta (docs/12-seguranca.md). Prefira `dotnet user-secrets` a
   colocar segredos reais nesse arquivo, mesmo em dev:
   ```bash
   cd src/backend/MeuFinanceiro.Api
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Key" "sua-chave-local-de-pelo-menos-32-caracteres"
   ```
   `Jwt:AccessTokenExpirationMinutes` (padrão 60) e `Turnstile:SecretKey` (opcional, ver seção
   de CAPTCHA abaixo) também vivem nesse arquivo. `Storage:ServiceUrl`/`AccessKey`/`SecretKey`
   já vêm preenchidos com os valores padrão do MinIO local do passo 1 — só precisa mudar se
   você customizar `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` no `docker-compose.yml`.

3. **Configurar o frontend:**
   ```bash
   cp src/frontend/environment.development.ts.example src/frontend/meu-financeiro/src/environments/environment.development.ts
   ```
   `apiUrl` fica relativo (`/api/v1`, não `https://localhost:5001/api/v1`) — o `ng serve`
   usa `proxy.config.json` (já no repositório) pra encaminhar `/api` pro backend por baixo
   dos panos, servindo tudo pela mesma origem (`localhost:4200`). Isso é necessário pros
   cookies de sessão (`SameSite=Strict`) funcionarem em dev — sem o proxy, front e backend
   seriam origens diferentes (portas diferentes) e o navegador não enviaria o cookie de volta
   (docs/12-seguranca.md).

4. **Rodar as migrations do EF Core** (após o projeto backend existir):
   ```bash
   dotnet ef database update --project src/backend/MeuFinanceiro.Infrastructure --startup-project src/backend/MeuFinanceiro.Api
   ```

5. **Rodar backend e frontend:**
   ```bash
   dotnet run --project src/backend/MeuFinanceiro.Api
   npm start --prefix src/frontend/meu-financeiro
   ```

## E-mail (recuperação de senha e confirmação de cadastro)

A seção `Smtp` do `appsettings.Development.json` é opcional pra **testar** o fluxo — sem
`Smtp:Host` configurado, o link de redefinição de senha e o link de confirmação de e-mail
(`POST /auth/register`, obrigatório desde docs/12-seguranca.md) não são enviados por e-mail
de verdade — só são logados no console do `dotnet run`, o que já é suficiente pra copiar o
link manualmente e testar o fluxo inteiro localmente. Pra qualquer ambiente onde pessoas
reais vão se cadastrar (inclusive um "staging" compartilhado), configure `Smtp:Host`/`Port`/
`User`/`Password` com um provedor real (Gmail com senha de app, SendGrid, Mailgun, Amazon
SES, etc.) — sem isso, ninguém recebe o link e ninguém consegue completar o cadastro.

## CAPTCHA (Cloudflare Turnstile)

Opcional em dev — sem `Turnstile:SecretKey` configurado, o widget de CAPTCHA simplesmente não
aparece nas telas de cadastro/recuperação de senha, e a verificação no backend não bloqueia
nada (mesmo padrão de degradação graciosa do SMTP). Pra testar de verdade:
1. Crie uma conta gratuita em [dash.cloudflare.com](https://dash.cloudflare.com) (não precisa
   ter domínio nenhum apontando pra Cloudflare).
2. Vá em "Turnstile" e crie um "site" apontando pro(s) domínio(s) onde o frontend roda (pode
   incluir `localhost`).
3. Configure a **site key** (pública) em `environment.development.ts` (`turnstileSiteKey`) e a
   **secret key** (privada) em `Turnstile:SecretKey` no backend (via `dotnet user-secrets`,
   nunca commitada).

## Observações

- Em produção **não** se usa Docker local nem o MinIO local — ver `docs/19-deploy.md` pra
  topologia de deploy recomendada (reverse proxy + domínio único), a lista de variáveis de
  ambiente obrigatórias e as opções de serviço S3-compatible pra produção.
- Nunca commitar `appsettings.Development.json` ou `environment.development.ts` reais — ambos estão no `.gitignore`; apenas os `.example` são versionados.
