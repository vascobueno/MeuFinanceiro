-- Remove a massa de performance de um grupo de teste.
--
-- A ordem importa: FinancialTransactions.InstallmentId e .RecurringTransactionId são FKs
-- Restrict (AppDbContext), então os lançamentos têm de sair antes das parcelas e recorrências.
--
-- Uso: psql -v grupo_id="'<uuid>'" -f teardown.sql

\set ON_ERROR_STOP on

BEGIN;

DELETE FROM "FinancialTransactions" WHERE "GrupoId" = :grupo_id::uuid;

DELETE FROM "Installments" i
WHERE EXISTS (SELECT 1 FROM "InstallmentPlans" p
              WHERE p."Id" = i."InstallmentPlanId" AND p."GrupoId" = :grupo_id::uuid);
DELETE FROM "InstallmentPlans" WHERE "GrupoId" = :grupo_id::uuid;

DELETE FROM "RecurringTransactionVersions" v
WHERE EXISTS (SELECT 1 FROM "RecurringTransactions" r
              WHERE r."Id" = v."RecurringTransactionId" AND r."GrupoId" = :grupo_id::uuid);
DELETE FROM "RecurringTransactions" WHERE "GrupoId" = :grupo_id::uuid;

DELETE FROM "AuditLogs" WHERE "GrupoId" = :grupo_id::uuid;

COMMIT;

SELECT 'restante em FinancialTransactions' AS conferencia, count(*)
FROM "FinancialTransactions" WHERE "GrupoId" = :grupo_id::uuid;
