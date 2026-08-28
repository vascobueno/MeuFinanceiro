# 19 — Deploy Azure

Arquitetura:
```text
GitHub -> GitHub Actions -> OIDC/Entra ID -> Azure

Azure:
App Service
Azure Database for PostgreSQL Flexible Server
Blob Storage
Key Vault
Application Insights
```

Ambientes: DEV, STAGING, PROD.

Blob privado para:
- bank-statements
- card-invoices
- migration
- investment-documents

RPO/RTO devem ser definidos antes da produção.
