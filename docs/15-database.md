# 15 — Banco de Dados

Tecnologia: PostgreSQL.

Desenvolvimento: PostgreSQL 18 via Docker (`docker-compose.yml`, ver `docs/23-configuracao-local.md`).
Produção: PostgreSQL gerenciado (qualquer provedor) ou self-hosted num container — sem
exigência de um provedor específico (docs/19-deploy.md).

ORM: EF Core (provider `Npgsql.EntityFrameworkCore.PostgreSQL`).
Migrations versionadas.
Valores: decimal(18,2).
Considerar a coluna de sistema `xmin` (ou um `[ConcurrencyCheck]` próprio) para concorrência
otimista.
Índices baseados em consultas reais.
