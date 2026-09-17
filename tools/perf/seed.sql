-- Massa de dados para testes de performance (Parte C do plano de desempenho).
--
-- Gera, para um grupo de teste JÁ EXISTENTE (criado via API pelo bootstrap.js):
--   10.000 lançamentos avulsos
--    5.000 recorrências (+ versão + 1ª ocorrência materializada)
--    5.000 parcelas em ~417 planos de 12x
--   40.000 registros de auditoria
--   => ~20.000 FinancialTransactions no total
--
-- Por que SQL cru e não a API: 20 mil chamadas HTTP levariam horas, e o seed precisa ser
-- determinístico e repetível entre as medições. É massa de teste, não código de produção.
--
-- Uso:
--   psql -v grupo_id="'<uuid>'" -v user_id="'<uuid>'" -f seed.sql
--
-- Pré-requisito: bootstrap.js já criou o usuário, o grupo, ~10 contas e ~5 cartões.

\set ON_ERROR_STOP on

BEGIN;

-- ---------------------------------------------------------------------------
-- Referências: contas, cartões e categorias já criadas pelo bootstrap
-- ---------------------------------------------------------------------------
CREATE TEMP TABLE _accounts AS
SELECT "Id", row_number() OVER (ORDER BY "Name") - 1 AS idx
FROM "Accounts" WHERE "GrupoId" = :grupo_id::uuid;

CREATE TEMP TABLE _cards AS
SELECT "Id", row_number() OVER (ORDER BY "Name") - 1 AS idx
FROM "CreditCards" WHERE "GrupoId" = :grupo_id::uuid;

CREATE TEMP TABLE _categories AS
SELECT "Id", row_number() OVER (ORDER BY "Name") - 1 AS idx
FROM "Categories" WHERE "GrupoId" = :grupo_id::uuid;

DO $$
BEGIN
  IF (SELECT count(*) FROM _accounts) = 0 THEN
    RAISE EXCEPTION 'Nenhuma conta no grupo. Rode o bootstrap.js antes do seed.';
  END IF;
END $$;

-- Datas espalhadas por ~3 anos para trás: o custo da projeção cresce com a IDADE da
-- recorrência, não só com o horizonte. Concentrar em datas recentes esconderia o problema.
\set dias_historico 1095

-- ---------------------------------------------------------------------------
-- 1) 10.000 lançamentos avulsos
-- ---------------------------------------------------------------------------
-- Mistura de status proposital: Pending NÃO entra no saldo, então uma massa só de
-- Confirmed mediria um caso que não existe na prática.
INSERT INTO "FinancialTransactions"
  ("Id","TransactionDate","Description","AccountId","CreditCardId","CategoryId",
   "Type","Direction","Amount","Status","CreatedAt","GrupoId")
SELECT
  gen_random_uuid(),
  (CURRENT_DATE - (i % :dias_historico))::date,
  'Perf avulso ' || i,
  CASE WHEN i % 4 = 0 THEN NULL
       ELSE (SELECT "Id" FROM _accounts WHERE idx = i % (SELECT count(*) FROM _accounts)) END,
  CASE WHEN i % 4 = 0
       THEN (SELECT "Id" FROM _cards WHERE idx = i % GREATEST((SELECT count(*) FROM _cards),1))
       ELSE NULL END,
  (SELECT "Id" FROM _categories WHERE idx = i % GREATEST((SELECT count(*) FROM _categories),1)),
  CASE WHEN i % 5 = 0 THEN 1 ELSE 2 END,               -- Income : Expense
  CASE WHEN i % 5 = 0 THEN 2 ELSE 1 END,               -- Credit : Debit
  round((random() * 900 + 10)::numeric, 2),
  CASE WHEN i % 10 < 2 THEN 1                          -- 20% Pending
       WHEN i % 10 < 5 THEN 2                          -- 30% Confirmed
       ELSE 3 END,                                     -- 50% Reconciled
  now(),
  :grupo_id::uuid
FROM generate_series(1, 10000) AS i;

-- ---------------------------------------------------------------------------
-- 2) 5.000 recorrências (+ versão vigente + 1ª ocorrência materializada)
-- ---------------------------------------------------------------------------
CREATE TEMP TABLE _recurring AS
SELECT
  gen_random_uuid() AS id,
  i,
  (CURRENT_DATE - (i % :dias_historico))::date AS start_date,
  (SELECT "Id" FROM _accounts WHERE idx = i % (SELECT count(*) FROM _accounts)) AS account_id,
  round((random() * 400 + 20)::numeric, 2) AS amount
FROM generate_series(1, 5000) AS i;

INSERT INTO "RecurringTransactions"
  ("Id","Description","AccountId","CategoryId","Type","Frequency","StartDate","IsActive","CreatedAt","GrupoId")
SELECT
  r.id, 'Perf recorrente ' || r.i, r.account_id,
  (SELECT "Id" FROM _categories WHERE idx = r.i % GREATEST((SELECT count(*) FROM _categories),1)),
  2,                                                   -- Expense
  CASE WHEN r.i % 10 = 0 THEN 1 WHEN r.i % 10 = 1 THEN 3 ELSE 2 END,  -- Weekly/Yearly/Monthly
  r.start_date,
  true,
  now(),
  :grupo_id::uuid
FROM _recurring r;

INSERT INTO "RecurringTransactionVersions"
  ("Id","RecurringTransactionId","EffectiveFrom","Amount","CreatedAt")
SELECT gen_random_uuid(), r.id, r.start_date, r.amount, now() FROM _recurring r;

-- Só a 1ª ocorrência vira lançamento real (docs/07).
INSERT INTO "FinancialTransactions"
  ("Id","TransactionDate","Description","AccountId","Type","Direction","Amount","Status",
   "RecurringTransactionId","CreatedAt","GrupoId")
SELECT
  gen_random_uuid(), r.start_date, 'Perf recorrente ' || r.i, r.account_id,
  2, 1, r.amount,
  CASE WHEN r.i % 10 < 3 THEN 1 ELSE 3 END,
  r.id, now(), :grupo_id::uuid
FROM _recurring r;

-- ---------------------------------------------------------------------------
-- 3) 5.000 parcelas em ~417 planos de 12x
-- ---------------------------------------------------------------------------
CREATE TEMP TABLE _plans AS
SELECT
  gen_random_uuid() AS id,
  p,
  (CURRENT_DATE - (p % :dias_historico))::date AS first_due,
  (SELECT "Id" FROM _accounts WHERE idx = p % (SELECT count(*) FROM _accounts)) AS account_id,
  round((random() * 200 + 50)::numeric, 2) AS parcela
FROM generate_series(1, 417) AS p;

INSERT INTO "InstallmentPlans"
  ("Id","Description","Mode","TotalAmount","InstallmentCount","CreatedAt","GrupoId")
SELECT p.id, 'Perf parcelado ' || p.p, 1, p.parcela * 12, 12, now(), :grupo_id::uuid
FROM _plans p;

CREATE TEMP TABLE _installments AS
SELECT
  gen_random_uuid() AS id,
  gen_random_uuid() AS tx_id,
  p.id AS plan_id, p.p, p.account_id, p.parcela,
  n AS numero,
  (p.first_due + (n - 1) * INTERVAL '1 month')::date AS due_date
FROM _plans p, generate_series(1, 12) AS n;

INSERT INTO "Installments"
  ("Id","InstallmentPlanId","Number","Amount","DueDate","FinancialTransactionId","CreatedAt")
SELECT i.id, i.plan_id, i.numero, i.parcela, i.due_date, i.tx_id, now() FROM _installments i;

INSERT INTO "FinancialTransactions"
  ("Id","TransactionDate","Description","AccountId","Type","Direction","Amount","Status",
   "InstallmentId","CreatedAt","GrupoId")
SELECT
  i.tx_id, i.due_date,
  'Perf parcelado ' || i.p || ' (' || i.numero || '/12)',
  i.account_id, 2, 1, i.parcela,
  CASE WHEN i.due_date > CURRENT_DATE THEN 1 ELSE 3 END,   -- futuro fica Pending
  i.id, now(), :grupo_id::uuid
FROM _installments i;

-- ---------------------------------------------------------------------------
-- 4) 40.000 registros de auditoria (exercita o índice do item A1)
-- ---------------------------------------------------------------------------
INSERT INTO "AuditLogs"
  ("Id","UserId","Timestamp","Action","EntityName","EntityId","CorrelationId","GrupoId")
SELECT
  gen_random_uuid(), :user_id::uuid,
  now() - (i % :dias_historico) * INTERVAL '1 day',
  CASE WHEN i % 3 = 0 THEN 'Added' WHEN i % 3 = 1 THEN 'Modified' ELSE 'Deleted' END,
  'FinancialTransaction', gen_random_uuid()::text, gen_random_uuid()::text,
  :grupo_id::uuid
FROM generate_series(1, 40000) AS i;

COMMIT;

-- ---------------------------------------------------------------------------
-- Conferência
-- ---------------------------------------------------------------------------
SELECT 'FinancialTransactions' AS tabela, count(*) FROM "FinancialTransactions" WHERE "GrupoId" = :grupo_id::uuid
UNION ALL SELECT 'RecurringTransactions', count(*) FROM "RecurringTransactions" WHERE "GrupoId" = :grupo_id::uuid
UNION ALL SELECT 'InstallmentPlans', count(*) FROM "InstallmentPlans" WHERE "GrupoId" = :grupo_id::uuid
UNION ALL SELECT 'Installments', count(*) FROM "Installments" i
          WHERE EXISTS (SELECT 1 FROM "InstallmentPlans" p WHERE p."Id" = i."InstallmentPlanId" AND p."GrupoId" = :grupo_id::uuid)
UNION ALL SELECT 'AuditLogs', count(*) FROM "AuditLogs" WHERE "GrupoId" = :grupo_id::uuid;
