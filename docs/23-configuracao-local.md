# 23 — Configuração do Ambiente Local

## Pré-requisitos
- .NET 10 SDK
- Node.js 22.22.3+, 24.15.0+ ou 26+ (faixa oficial do Angular 22 — ver `src/frontend/meu-financeiro/package.json`). **Atenção:** Node 24.11.x (comum em instalações recentes) NÃO é suficiente para o Angular 22 — é preciso 24.15.0 ou mais recente.
- Docker Desktop (ou engine compatível)

## Passo a passo

1. **Subir dependências locais (PostgreSQL + emulador de Blob Storage):**
   ```bash
   docker compose up -d
   ```
   Isso cria os containers `meufinanceiro-postgres` (porta 5432, imagem `postgres:18`)
   e `meufinanceiro-azurite` (porta 10000, imagem `azurite:3.36.0`).
   Dados do Azurite persistem em `docker-data/azurite/` (ignorado pelo Git). Os dados do
   Postgres ficam num **named volume** do Docker (`postgres-data`), montado em
   `/var/lib/postgresql` (não `.../data` — convenção da imagem oficial 18+, ver
   [docker-library/postgres#1259](https://github.com/docker-library/postgres/pull/1259)). Para
   apagar os dados do Postgres local: `docker volume rm meufinanceiro_postgres-data` (ou
   `docker compose down -v`).

2. **Configurar o backend:**
   ```bash
   cp src/backend/appsettings.Development.json.example src/backend/MeuFinanceiro.Api/appsettings.Development.json
   ```
   Ajuste a `Jwt:Key` localmente. Prefira `dotnet user-secrets` a colocar segredos reais nesse arquivo, mesmo em dev:
   ```bash
   cd src/backend/MeuFinanceiro.Api
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Key" "sua-chave-local-aqui"
   ```

3. **Configurar o frontend:**
   ```bash
   cp src/frontend/environment.development.ts.example src/frontend/meu-financeiro/src/environments/environment.development.ts
   ```

4. **Rodar as migrations do EF Core** (após o projeto backend existir):
   ```bash
   dotnet ef database update --project src/backend/MeuFinanceiro.Infrastructure --startup-project src/backend/MeuFinanceiro.Api
   ```

5. **Rodar backend e frontend:**
   ```bash
   dotnet run --project src/backend/MeuFinanceiro.Api
   npm start --prefix src/frontend/meu-financeiro
   ```

## E-mail (recuperação de senha)

A seção `Smtp` do `appsettings.Development.json` é opcional. Sem `Smtp:Host` configurado, o
link de redefinição de senha (`POST /auth/forgot-password`) não é enviado por e-mail — só é
logado no console do `dotnet run`, o que já é suficiente pra testar o fluxo inteiro
localmente. Pra testar o envio de verdade, configure `Smtp:Host`/`Port`/`User`/`Password`
com um provedor real (Gmail, Outlook, SendGrid, etc.).

## Observações

- Em produção **não** se usa Docker local nem Azurite — ver `docs/19-deploy-azure.md` para Azure Database for PostgreSQL Flexible Server e Azure Blob Storage reais, com Key Vault e Managed Identity (ver `docs/12-seguranca.md`).
- Nunca commitar `appsettings.Development.json` ou `environment.development.ts` reais — ambos estão no `.gitignore`; apenas os `.example` são versionados.
