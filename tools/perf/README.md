# Testes de performance

Ferramental para medir o custo real das consultas com volume, em vez de otimizar por intuição.
Referência: plano de desempenho e arquitetura de saldo (Partes C e D).

Massa gerada: **~20.000 lançamentos** (10.000 avulsos + 5.000 de recorrência + 5.000 parcelas),
5.000 recorrências, ~417 planos de parcelamento e 40.000 registros de auditoria.

## Como rodar

Use sempre um **backend isolado numa porta separada** (ex.: 5011), nunca o de desenvolvimento
do dia a dia — a massa é grande e o teardown apaga tudo do grupo de teste.

```bash
# 1. Criar usuário, contas e cartões (via API)
node tools/perf/bootstrap.js --api=https://localhost:5011

# 2. Confirmar o e-mail do usuário (o registro exige confirmação)
#    UPDATE "AspNetUsers" SET "EmailConfirmed"=true WHERE "Email"='perf.bench@meufinanceiro.local';

# 3. Rodar o bootstrap de novo para criar as referências e obter os ids
node tools/perf/bootstrap.js --api=https://localhost:5011

# 4. Popular a massa (usar os ids impressos pelo bootstrap)
psql -U postgres -d MeuFinanceiro \
  -v grupo_id="'<grupoId>'" -v user_id="'<userId>'" \
  -f tools/perf/seed.sql

# 5. Medir
node tools/perf/bench.js --api=https://localhost:5011 --label="baseline"

# 6. Limpar quando terminar
psql -U postgres -d MeuFinanceiro -v grupo_id="'<grupoId>'" -f tools/perf/teardown.sql
```

## Round-trips ao banco

O tempo de relógio oscila em máquina de desenvolvimento; a **contagem de queries por requisição**
é a métrica estável, e é o que prova que um N+1 foi corrigido. Para obtê-la, suba o backend com o
log de comandos do EF em `Information`:

```bash
Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information dotnet run ...
```

Depois conte as linhas `Executed DbCommand` entre o início e o fim de cada requisição.

## Consultas-chave para `EXPLAIN ANALYZE`

"Ficou mais rápido" não basta — confirme que o plano de execução mudou:

```sql
-- Saldo das contas (deve virar um único GROUP BY indexado)
EXPLAIN (ANALYZE, BUFFERS)
SELECT "AccountId", SUM(CASE WHEN "Direction" = 2 THEN "Amount" ELSE -"Amount" END)
FROM "FinancialTransactions"
WHERE "GrupoId" = '<grupoId>' AND "AccountId" IS NOT NULL
  AND "Status" <> 1 AND "TransactionDate" <= CURRENT_DATE
GROUP BY "AccountId";

-- Auditoria (deve trocar Seq Scan + Sort por Index Scan)
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM "AuditLogs"
WHERE "GrupoId" = '<grupoId>'
ORDER BY "Timestamp" DESC
LIMIT 20;
```

## Resultados

Cada rodada é registrada em [`resultados.md`](resultados.md), com a data e o que mudou desde a
anterior. Nenhum item do plano é dado como concluído sem número antes e depois.
