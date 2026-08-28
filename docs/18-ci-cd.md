# 18 — CI/CD

Plataforma principal: GitHub + GitHub Actions.

Fluxo alvo (planejado):
feature -> Pull Request -> CI -> develop/staging -> aprovação -> main/production.

## Estado atual (`.github/workflows/ci.yml`)

Dois jobs, ambos em `ubuntu-latest`, disparados em push/PR para `main`/`develop`:

- **backend**: `dotnet restore` -> `dotnet build --configuration Release` -> testes unitários
  (`tests/backend/MeuFinanceiro.Domain.UnitTests`, filtro `Category=Unit`) -> testes de
  integração (`tests/backend/MeuFinanceiro.Application.IntegrationTests`, filtro
  `Category=Integration`).
- **frontend**: `npm ci` -> `npm test -- --watch=false --browsers=ChromeHeadless` -> `npm run
  build`. O flag `--browsers=ChromeHeadless` é obrigatório — o runner do GitHub Actions não tem
  servidor X, então o Chrome padrão (não-headless) do Karma falha com "Missing X server or
  $DISPLAY".

Ainda não implementado: lint, scan de dependências/segurança, e todo o CD (deploy automático em
DEV/STAGING/PROD, aprovação, OIDC GitHub -> Azure). O projeto roda hoje só localmente.

Quando o CD existir: migrations de produção devem ser controladas e não destrutivas
automaticamente.
