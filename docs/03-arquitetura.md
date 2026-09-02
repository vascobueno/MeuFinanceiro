# 03 — Arquitetura

```text
Frontend Angular
    |
    | HTTPS/JSON
    v
ASP.NET Core API
    |
    +-- Domain
    +-- Application
    +-- Infrastructure
    |
    v
EF Core -> PostgreSQL

API -> Azure Blob Storage
API -> Key Vault via Managed Identity
API -> Application Insights
```

## Backend
```text
src/backend/
  MeuFinanceiro.Api
  MeuFinanceiro.Application
  MeuFinanceiro.Domain
  MeuFinanceiro.Infrastructure
```

## Frontend
```text
src/frontend/meu-financeiro/
  core/
  shared/
  layout/
  features/
    auth/
    dashboard/
    transactions/
    accounts/
    cards/
    categories/
    installments/
    invoices/
    recurring/
    bank-reconciliation/
    card-reconciliation/
    investments/
    projection/
    settings/
    grupo/
    admin/
    reports/
    help/
```
