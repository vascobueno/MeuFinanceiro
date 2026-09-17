# Resultados de performance

Cada rodada usa a massa do [`seed.sql`](seed.sql), num backend isolado (porta 5011) contra o
Postgres local. Números de máquina de desenvolvimento: o que vale é a **comparação entre rodadas**
e a contagem de round-trips, não o valor absoluto.

**Massa:** 20.004 `FinancialTransactions` (10.000 avulsos + 5.000 de recorrência + 5.004 parcelas),
5.000 `RecurringTransactions`, 417 `InstallmentPlans`, 40.017 `AuditLogs`, 10 contas, 5 cartões.

---

## Rodada 1 — baseline (antes de qualquer alteração)

Commit `e08a8b8`. 10 execuções por endpoint, 3 de aquecimento descartadas.

| Endpoint | p50 (ms) | p95 (ms) | payload | queries |
|---|---:|---:|---:|---:|
| `GET /accounts` | 49 | 65 | 2 KB | **12** |
| `GET /transactions` | 18 | 65 | 7 KB | 2 |
| `GET /transactions` (busca) | 46 | 79 | 8 KB | — |
| `GET /projection` +30d | 178 | 211 | **771 KB** | 4 |
| `GET /projection` +60d | 181 | 320 | **1523 KB** | 4 |
| `GET /audit` | 35 | 53 | 6 KB | 3 |
| `GET /installment-plans` | 15 | 20 | 3 KB | 3 |
| `GET /recurring-transactions` | 174 | 271 | **2237 KB** | 3 |
| `GET /investments/summary` | 16 | 16 | 0 KB | 1 |
| **Dashboard** (5 em paralelo) | **208** | **260** | **2343 KB** | ~20 |

### Planos de execução

**Auditoria** — `Seq Scan` + `top-N heapsort` sobre as 40 mil linhas, como previsto:
```
Limit (actual time=10.168..10.173 rows=20)
  -> Sort  Sort Key: "Timestamp" DESC   Sort Method: top-N heapsort
    -> Seq Scan on "AuditLogs"  (actual rows=40019)  Buffers: shared hit=1014
Execution Time: 10.211 ms
```

**Saldo por conta** — `Bitmap Heap Scan` usando o índice de FK `AccountId`, 294 linhas por conta,
~1 ms. Barato **por conta** — o problema não é esta consulta, é executá-la 10 vezes por requisição.

### Leitura do baseline

1. **N+1 confirmado com número**: `GET /accounts` faz **12 queries** para 10 contas (2 + N). É o
   alvo direto do item 2 (B1).
2. **O gargalo real neste volume é o tamanho da resposta, não o tempo de banco.** Isto não estava
   previsto no plano: `/recurring-transactions` devolve **2,2 MB** e a projeção de 60 dias **1,5 MB**.
   O Dashboard trafega **2,3 MB** por abertura. Com 5.000 recorrências × 3 próximas ocorrências, a
   resposta explode — e `/recurring-transactions` **não tem paginação nenhuma**.
3. **A auditoria sem índice ainda é rápida** (35 ms / 10 ms no banco) porque 40 mil linhas cabem
   em memória. O índice do item 1 segue valendo pelo crescimento — a tabela cresce a cada
   `SaveChangesAsync` —, mas a urgência é menor do que o plano supunha.
4. **A projeção não é dominada pela expansão de recorrências** como se temia: 4 queries e ~180 ms,
   com o peso concentrado na serialização do payload.

### Ajuste de rumo

O baseline reordena prioridades em relação ao plano. Entram na Parte D, por evidência:

- **Paginar `/recurring-transactions`** (hoje sem paginação, 2,2 MB) — provável maior ganho isolado.
- **Enxugar o payload da projeção**: a timeline devolve item a item; avaliar agregação por dia ou
  paginação do horizonte.

Ambos superam, em impacto medido, vários itens previstos nas Partes A/B.
