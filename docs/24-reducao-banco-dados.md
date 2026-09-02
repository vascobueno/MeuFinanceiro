# 24 — Redução do banco de dados (arquivamento de dados antigos)

Pesquisa apenas — não implementado nesta rodada (decisão explícita, ver plano de
Grupo/Relatórios). Cobre como reduzir o tamanho do banco fazendo backup de dados
anteriores a uma data de corte e depois eliminando-os.

## Por que um DELETE simples não encolhe o banco

Postgres usa MVCC — linhas apagadas viram "tuplas mortas"; o espaço fica marcado como
reutilizável, mas o arquivo em disco não encolhe sozinho. Depois do DELETE, uma das opções:

- **`VACUUM`** (sem `FULL`): recupera o espaço para reuso futuro pelo próprio Postgres, mas
  não devolve o espaço ao sistema operacional — não reduz o tamanho do arquivo. Não trava a
  tabela.
- **`VACUUM FULL`**: reescreve a tabela inteira e devolve o espaço ao SO de verdade — mas toma
  um lock `ACCESS EXCLUSIVE` (bloqueia leitura e escrita na tabela inteira) até terminar. Em
  tabelas grandes de produção isso pode significar minutos de indisponibilidade daquela tabela.
- **`pg_repack`**: faz o mesmo que `VACUUM FULL`, mas "online" (sem lock exclusivo
  prolongado); exige a extensão instalada no servidor. O projeto roda Postgres 18 localmente
  (`docker-compose.yml`); em produção (docs/19-deploy.md, sem dependência de Azure), a
  disponibilidade de `pg_repack` depende do provedor de PostgreSQL escolhido — conferir se o
  provedor permite habilitar extensões antes de contar com isso em produção.

## Ordem segura de exclusão

Derivada do grafo de FK real em `AppDbContext.OnModelCreating` (folhas primeiro):

```
Reconciliation
  → InvestmentPosition / InvestmentTransaction
  → BankStatementItem / CardInvoiceItem / MerchantAlias / RecurringTransactionVersion
  → Installment
  → FinancialTransaction
  → Investment / InstallmentPlan / RecurringTransaction / BankStatement / CardInvoice
  → Account / CreditCard / Merchant / Subcategory / InvestmentAccount
  → Category
```

`ImportFile` e `AuditLog` não têm FK para as outras 11 e podem ser arquivados
independentemente, a qualquer momento.

## Onde guardar o backup

`IFileStorageService`/`AzureBlobStorageService` já existe no projeto (hoje usado para
importação de fatura em PDF) e é genérico o bastante
(`SaveAsync(containerName, fileName, stream, contentType)`) para guardar um arquivo de backup
sem nenhuma mudança nele — só chamar com um container novo (ex.: `"backups"`).

## Desenho proposto (para quando for construir)

1. Admin escolhe uma data de corte.
2. Backend exporta as linhas anteriores a essa data (na ordem acima) para um arquivo (JSON/CSV
   por tabela, compactado num `.zip`) e sobe via `AzureBlobStorageService.SaveAsync`.
3. Só depois do upload confirmado, apaga as linhas na ordem folhas-primeiro acima.
4. Roda `VACUUM (VERBOSE, ANALYZE)` como mínimo seguro; só usar `VACUUM FULL`/`pg_repack` se o
   objetivo for realmente liberar espaço em disco visível para o SO (não só performance de
   query).

## Ressalva a decidir antes de construir

Apagar lançamentos antigos muda o que as próprias telas do app mostram (histórico,
relatórios, projeção) — não é só uma operação de DBA invisível para o usuário. Antes de
implementar de fato, decidir: o backup deve ficar baixável pelo usuário? Existe um caminho de
"restaurar"? Nenhuma dessas duas perguntas foi respondida ainda — só sinalizando para não ser
surpresa quando a feature for construída de verdade.
