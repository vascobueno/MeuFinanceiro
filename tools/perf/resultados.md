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

---

## Rodada 2 — item 1: saldo em uma consulta + corte `<= hoje`

`AccountBalanceQueries` + `AccountBalanceService`; `AccountService` e `ProjectionService` passam a
usar a mesma definição de saldo.

| Endpoint | p50 antes → depois | p95 antes → depois | queries |
|---|---|---|---|
| `GET /accounts` | 49 → **23 ms** (-53%) | 65 → **34 ms** | **12 → 3** |
| `GET /projection` +30d | 178 → **120 ms** (-33%) | 211 → **136 ms** | 4 → 5 |
| `GET /projection` +60d | 181 → **126 ms** (-30%) | 320 → **149 ms** (-53%) | 4 → 5 |
| **Dashboard** | 208 → **167 ms** (-20%) | 260 → **190 ms** | ~20 → ~11 |

**Correção verificada**: os 10 saldos devolvidos pela API batem centavo a centavo com o oráculo SQL
(`InitialBalance + SUM(...) WHERE Status <> 1 AND TransactionDate <= CURRENT_DATE`).

A projeção ganhou **uma** consulta (o saldo virou soma no banco em vez de agregação em memória) e
mesmo assim ficou 30% mais rápida, porque deixou de trazer o histórico inteiro para descartar quase
tudo em memória. Trocar uma consulta a mais por muito menos dado trafegado compensou.

Payloads seguem iguais — são o alvo dos itens 2 e 3.

---

## Rodada 3 — item 2: paginação de `/recurring-transactions`

O endpoint devolvia **todas** as recorrências de uma vez, cada uma com 3 próximas ocorrências.
Agora é paginado (`PagedRequest`/`PagedResult`), e as ocorrências são calculadas só para a página.

O relatório imprimível de gastos recorrentes **não** pode ser paginado — o total do rodapé precisa
fechar sobre o conjunto inteiro —, então ganhou endpoint próprio (`GET /recurring-transactions/report`)
com shape enxuto: sem origem e com apenas a primeira data, em vez das 3 ocorrências.

| Endpoint | p50 antes → depois | payload antes → depois |
|---|---|---|
| `GET /recurring-transactions` | 175 → **30 ms** (-83%) | **2237 KB → 9 KB** (-99,6%) |
| **Dashboard** | 167 → **145 ms** | 2343 KB (inalterado — alvo do item 3) |

### Nota de método: a primeira medição enganou

A rodada logo após reiniciar o backend acusou a projeção em 219/271 ms e o Dashboard em 235 ms —
*piora* em endpoints que este item nem tocou. Repetindo a medição, os valores voltaram para
105/104 ms e 145 ms. Era ruído de máquina, não regressão.

Fica o registro: **uma única rodada não decide nada**. Medir todos os endpoints a cada item (e
repetir quando algo destoa) é o que separa sinal de ruído — e teria evitado uma "otimização"
inventada para um problema inexistente.

---

## Rodada 4 — item 3: payload da projeção

O Dashboard baixava a linha do tempo inteira (1,5 MB na projeção de 60 dias) para usar **5 itens,
uma contagem e uma soma**. A resposta ganhou os agregados (`TimelineCount`,
`ProjectedExpensesTotal`), sempre calculados sobre a linha completa, e o parâmetro
`maxTimelineEntries` limita só o que trafega.

| Endpoint | p50 antes → depois | payload antes → depois |
|---|---|---|
| **Dashboard** | 208 (baseline) → **131 ms** | **2343 KB → 50 KB** (-98%) |
| `GET /projection` +60d (tela) | 181 → **103 ms** | 1523 KB (inalterado — a tela mostra a linha toda) |

### Nota de método: o harness media falhas como sucesso

Uma rodada devolveu ~15 ms e **0 KB** em todos os endpoints. Não era otimização mágica: o rate
limit da autenticação tinha disparado e o `bench.js` estava cronometrando respostas 401/429 como se
fossem válidas.

O harness passou a **abortar em qualquer resposta não-2xx**. Sem isso, uma medição inteira de
números falsos poderia ter virado "prova" de um ganho inexistente.

### Item 5 (chamada dupla da projeção): dispensado pela medição

O Dashboard ainda chama a projeção duas vezes (+30d e +60d), mas agora cada uma trafega poucos KB e
as duas correm em paralelo: **131 ms contra uma meta de 1 s**. Deduplicar exigiria um parâmetro
extra na API para pedir o saldo em duas datas, e economizaria poucas dezenas de ms.

Pelo critério de parada do plano (D5 — toda otimização precisa citar a linha de medição que a
justifica), **não se justifica**. Fica registrado como decisão consciente, não como esquecimento.

---

## Rodada 5 — item 6: índice em `AuditLogs`

| | Antes | Depois |
|---|---|---|
| Plano | `Seq Scan` + `top-N heapsort` | **`Index Scan using IX_AuditLogs_GrupoId_Timestamp`** |
| Linhas varridas | 40.019 | 20 |
| Buffers | 1014 | **20** |
| Execution Time | 10,211 ms | **0,233 ms** (44×) |

O ganho absoluto em ms é pequeno no volume atual — mas o que muda é a **classe de custo**: antes
crescia linearmente com a tabela (que aumenta a cada `SaveChangesAsync`), agora não cresce.

---

## Rodada 6 — item 7: janela da conciliação no `WHERE`

A janela de busca (10 dias no extrato, 15 na fatura) já existia, mas era aplicada como `continue`
**dentro** do laço de scoring — depois de carregar todos os lançamentos não conciliados da
conta/cartão. Subiu para o `WHERE`.

Não aparece no `bench.js` (é caminho de escrita, não de leitura), então a medição é do mecanismo —
o conjunto de candidatos, que é o `m` do produto cartesiano:

| Cenário (uma conta da massa) | Candidatos carregados |
|---|---:|
| Antes — todos os não conciliados | **1198** |
| Depois — janela de um extrato de 30 dias | **55** (-95%) |

O scoring é O(n×m) com normalização e split de string por par. Um extrato de 200 itens sai de
~240 mil comparações para ~11 mil. E o número "antes" cresce com o histórico da conta para sempre,
enquanto o "depois" é limitado pelo período do extrato.

---

## Rodada 7 — itens 8 e 9: importação em lote e fechamento mensal

`GET /audit` caiu de 40 para **16 ms** (efeito do índice da rodada 5 medido pelo endpoint, não só
pelo `EXPLAIN`). `GET /accounts` chegou a **17 ms** (baseline: 49 ms).

### Prova de correção do fechamento

O fechamento é um cache — o risco dele é dar saldo errado em silêncio. Os testes:

| Caso | Resultado |
|---|---|
| Cache gerado do zero | 13 fechamentos na conta A, saldo −40.647,51 · `verify` OK |
| **Escrita via EF** em mês de 400 dias atrás (criar + confirmar pela API) | fechamentos **13 → 0**: a invalidação disparou e apagou as linhas afetadas |
| Saldo após essa escrita | **−41.882,07** = −40.647,51 − 1.234,56 (exato) · `verify` OK nas 10 contas |
| Apagar o cache inteiro à mão | regenera 314 linhas e `verify` volta OK — **auto-curativo** |
| **Ponto cego**: `UPDATE` direto no SQL alterando um valor antigo | `verify` acusa **divergência de 1000** |

O último caso é o mais importante: confirma que o ponto cego documentado (operação em lote ou
script SQL não passa pelo ChangeTracker) **existe de fato** e que a rede de segurança
(`GET /accounts/balances/verify`) **o detecta**. Quem implementar o arquivamento do `docs/24`
precisa apagar os fechamentos afetados explicitamente.

### Dois testes que passaram por acidente (e foram refeitos)

Vale registrar, porque quase viraram falso positivo:

1. Mudei a **data** de um lançamento antigo entre dois meses **ambos anteriores à âncora**. O
   acumulado na âncora não muda, então o teste passaria mesmo com a invalidação quebrada.
2. Tentei exercitar o caminho do EF excluindo um lançamento **Reconciled** — a regra 12 bloqueia,
   o `DELETE` voltou 405 e nada foi exercitado. O "OK" era ausência de mudança, não correção.

Teste que passa sem exercitar o que deveria é pior que teste ausente: dá confiança falsa.

---

# Rodada 8 (final) — Parte D

## Resultado consolidado

| Endpoint | Baseline | Final | Ganho |
|---|---:|---:|---|
| `GET /accounts` | 49 ms · **12 queries** · 2 KB | **15 ms · 3 queries** | -69% |
| `GET /transactions` | 18 ms · 7 KB | 16 ms · 7 KB | — |
| `GET /transactions` (busca) | 46 ms | 46 ms | — |
| `GET /projection` +30d | 178 ms · 771 KB | **98 ms** · 771 KB | -45% |
| `GET /projection` +60d | 181 ms (p95 **320**) · 1523 KB | **112 ms** (p95 185) | -38% |
| `GET /audit` | 35 ms | **15 ms** | -57% |
| `GET /installment-plans` | 15 ms | 17 ms | — |
| `GET /recurring-transactions` | 175 ms · **2237 KB** | **17 ms · 9 KB** | **-90% / -99,6%** |
| **Dashboard** | 208 ms · **2343 KB** · ~20 queries | **117 ms · 50 KB · ~11** | -44% / **-98%** |

Metas do C4: p95 < 300 ms em todas as listagens ✅ · Dashboard < 1 s ✅ (117 ms) ·
`/accounts` em 2-3 queries ✅ · auditoria em Index Scan ✅.

## Veredito sobre stored procedure — agora com medição

**Segue não se justificando para performance.** O experimento decisivo foi decompor o endpoint mais
lento que sobrou, a projeção de 60 dias (~241 ms):

| Camada | Custo |
|---|---:|
| SQL (carregar 5.000 recorrências + versões) | **3,9 ms** |
| Serializar 1,5 MB de linha do tempo | **~0 ms** (241 ms com timeline completa vs 256 ms sem — dentro do ruído) |
| **C# — expansão das recorrências** | **~237 ms** |

O banco responde em 4 ms. Os outros 98% do tempo são aritmética de calendário em C#. Uma procedure
teria de reimplementar em PL/pgSQL a lógica de frequência **e** a seleção de versão vigente da
regra 13, para economizar 4 ms — num projeto sem testes. É a ferramenta errada para este gargalo.

Os ganhos reais vieram de: corrigir um laço com `await` dentro (item 1), paginar (item 2), parar de
trafegar o que ninguém usa (item 3) e **um índice** (item 6). Nenhum exigiu SQL cru.

A única procedure que chegou a se justificar nesta análise não era por velocidade, e sim por
**integridade**: um trigger de invalidação do fechamento, que operação em lote e script SQL não
conseguiriam burlar. Optou-se pelo hook em C# — ver o ponto cego documentado na rodada 7.

## O que sobrou, e por que não foi mexido

### 1. Expansão de recorrências: O(recorrências × idade) — **achado importante**

`RecurrenceOccurrenceCalculator.EnumerateUntil` começa o cursor na `StartDate` e avança **um período
por vez**, descartando tudo anterior a hoje. Para uma recorrência semanal de 3 anos atrás são ~156
iterações inúteis antes da primeira útil; com 5.000 recorrências, centenas de milhares. São os
237 ms acima, e o custo **cresce indefinidamente com a idade** da recorrência.

A correção óbvia — pular direto para a primeira ocorrência após hoje — **muda o resultado**:

```
recorrência que começa em 2026-01-31
  avançando mês a mês : 2026-04-28   (o clamp de fevereiro é composto e nunca volta ao dia 31)
  pulando 3 meses     : 2026-04-30   (preserva o dia de origem)
```

Ou seja, hoje uma conta que vence todo dia 31 **degrada permanentemente para o dia 28** depois da
primeira passagem por fevereiro. Isso é discutivelmente um bug de negócio — mas corrigi-lo muda
datas de vencimento projetadas, o que é decisão de produto, não de performance. **Fica em aberto,
com a pergunta anexada.**

### 2. Busca por descrição — dentro da meta

`lower("Description") LIKE '%...%'` faz `Seq Scan` (11,6 ms no banco, 46 ms no endpoint). Um índice
GIN `pg_trgm` resolveria, mas p95 de 60 ms contra meta de 300 ms **não justifica** adicionar uma
extensão do Postgres. Registrado para quando o volume crescer.

### 3. Tela de Projeção ainda recebe 1,5 MB

O Dashboard foi resolvido, mas a tela de Projeção exibe a linha do tempo inteira num `p-table` —
com esta massa, ~10 mil linhas. É problema de usabilidade antes de ser de performance (ninguém lê
10 mil linhas); a solução é paginar ou agregar por dia, e isso é decisão de UX.

### Critério de parada aplicado

Itens **não** implementados por estarem dentro da meta: chamada dupla da projeção no Dashboard
(item 5), índice trigram na busca, paginação da tela de Projeção. Cada um tem a linha de medição
que justifica a decisão acima.

---

### Ajuste de rumo

O baseline reordena prioridades em relação ao plano. Entram na Parte D, por evidência:

- **Paginar `/recurring-transactions`** (hoje sem paginação, 2,2 MB) — provável maior ganho isolado.
- **Enxugar o payload da projeção**: a timeline devolve item a item; avaliar agregação por dia ou
  paginação do horizonte.

Ambos superam, em impacto medido, vários itens previstos nas Partes A/B.
