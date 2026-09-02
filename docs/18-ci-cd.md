# 18 — CI/CD

Plataforma principal: GitHub + GitHub Actions.

Fluxo alvo (planejado):
feature -> Pull Request -> CI -> develop/staging -> aprovação -> main/production.

## Estado atual (`.github/workflows/ci.yml`)

Dois jobs, ambos em `ubuntu-latest`, disparados em push/PR para `main`/`develop`:

- **backend**: `dotnet restore` -> `dotnet build --configuration Release` -> testes unitários
  (`tests/backend/MeuFinanceiro.Domain.UnitTests`, filtro `Category=Unit`) -> testes de
  integração (`tests/backend/MeuFinanceiro.Application.IntegrationTests`, filtro
  `Category=Integration`) -> `dotnet list package --vulnerable --include-transitive` (reporta
  no log; não falha o build sozinho — ver nota abaixo).
- **frontend**: `npm ci` -> `npm audit --audit-level=high` (falha o build se achar
  vulnerabilidade alta/crítica) -> `npm test -- --watch=false --browsers=ChromeHeadless` ->
  `npm run build`. O flag `--browsers=ChromeHeadless` é obrigatório — o runner do GitHub
  Actions não tem servidor X, então o Chrome padrão (não-headless) do Karma falha com
  "Missing X server or $DISPLAY".

`.github/dependabot.yml` abre PR automático (semanal) quando sai atualização de segurança pra
um pacote NuGet ou npm — cobertura contínua além do que o CI já verifica a cada push/PR.

`dotnet list package --vulnerable` só reporta no log (não falha o build automaticamente) —
diferente do `npm audit`, que já é bloqueante por padrão nesse nível (`--audit-level=high`).
Se quiser que o passo do .NET também bloqueie o merge, precisa de um passo extra fazendo
grep na saída.

Ainda não implementado: lint, e todo o CD (deploy automático em DEV/STAGING/PROD, aprovação).
O projeto roda hoje só localmente.

Quando o CD existir: migrations de produção devem ser controladas e não destrutivas
automaticamente.
