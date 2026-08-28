# MeuFinanceiro

Sistema financeiro pessoal para lançamentos, recorrências, parcelamentos, conciliação bancária e de cartões, investimentos e projeção financeira.

## Stack
- Angular + Angular Material
- ASP.NET Core / C#
- Entity Framework Core
- PostgreSQL
- Azure App Service
- Azure Blob Storage
- Azure Key Vault + Managed Identity
- Application Insights
- GitHub + GitHub Copilot
- GitHub Actions + OIDC para CI/CD

## Regra central
O Excel é usado somente para a migração inicial. Depois da migração, o sistema passa a ser a fonte oficial dos lançamentos.

## Status
Implementação em andamento — ver `docs/22-status-projeto.md` para o checklist detalhado do
que já está pronto (fundação, autenticação, household multi-usuário com perfis de acesso,
administração do sistema, contas, cartões, categorias, lançamentos com parcelamento/
recorrência, conciliação bancária/cartão, investimentos, projeção, relatórios imprimíveis,
auditoria e skins de cores) e do que ainda falta (importação inicial do Excel, parsers reais
de extrato/fatura, deploy Azure).

## Como rodar localmente
Ver `docs/23-configuracao-local.md`. Resumo:

```bash
docker compose up -d
cp src/backend/appsettings.Development.json.example src/backend/MeuFinanceiro.Api/appsettings.Development.json
cd src/backend/MeuFinanceiro.Api && dotnet user-secrets init && dotnet user-secrets set "Jwt:Key" "sua-chave-local"
cd ../../..
dotnet ef migrations add InitialCreate --project src/backend/MeuFinanceiro.Infrastructure --startup-project src/backend/MeuFinanceiro.Api
dotnet ef database update --project src/backend/MeuFinanceiro.Infrastructure --startup-project src/backend/MeuFinanceiro.Api
dotnet run --project src/backend/MeuFinanceiro.Api

cp src/frontend/environment.development.ts.example src/frontend/meu-financeiro/src/environments/environment.development.ts
cd src/frontend/meu-financeiro && npm install && npm start
```
