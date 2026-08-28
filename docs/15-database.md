# 15 — Banco de Dados

Tecnologia: PostgreSQL.

Desenvolvimento: PostgreSQL via Docker (`docker-compose.yml`, ver `docs/23-configuracao-local.md`).
Produção: Azure Database for PostgreSQL Flexible Server.

ORM: EF Core (provider `Npgsql.EntityFrameworkCore.PostgreSQL`).
Migrations versionadas.
Valores: decimal(18,2).
Considerar a coluna de sistema `xmin` (ou um `[ConcurrencyCheck]` próprio) para concorrência
otimista.
Índices baseados em consultas reais.
