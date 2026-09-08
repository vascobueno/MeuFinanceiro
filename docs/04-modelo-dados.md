# 04 — Modelo de Dados

## Entidades
```text
ApplicationUser
RefreshToken
Grupo
GrupoMember
Account
CreditCard
Merchant
MerchantAlias
Category
Subcategory
FinancialTransaction
RecurringTransaction
RecurringTransactionVersion
InstallmentPlan
Installment
BankStatement
BankStatementItem
CardInvoice
CardInvoiceItem
Reconciliation
InvestmentAccount
Investment
InvestmentPosition
InvestmentTransaction
ImportFile
AuditLog
```

## ApplicationUser
Além dos campos padrão do Identity (`UserName`, `Email`, senha com hash), `Name` e
`MustChangePassword` (bool) — força a troca de senha no primeiro login de contas criadas
diretamente pelo Owner com senha temporária (ver `docs/12-seguranca.md`).

## Grupo / GrupoMember
`Grupo`: Id, Name. `GrupoMember`: GrupoId, UserId, Role (`GrupoRole`:
Owner/Member), AccessProfile (`GrupoAccessProfile`: Completo/Editor/Visualizador — Owner é
sempre Completo). Todo `ApplicationUser` pertence a exatamente um grupo (índice único em
`GrupoMember.UserId`).

## FinancialTransaction
Id, **GrupoId** (dono real do dado, compartilhado entre os membros do grupo — não é
mais `UserId` direto), TransactionDate, Description, MerchantId, CategoryId, AccountId?,
CreditCardId?, Type, Amount decimal(18,2), Direction, Status, InstallmentId?,
RecurringTransactionId?, CreatedAt/By, UpdatedAt/By (`CreatedBy`/`UpdatedBy` continuam
identificando a pessoa, não o grupo). Mesmo padrão (`GrupoOwnedEntity`) vale para as
outras 11 entidades financeiras (Account, CreditCard, Category, Merchant,
RecurringTransaction, InstallmentPlan, BankStatement, CardInvoice, Reconciliation,
InvestmentAccount, ImportFile).

## Reconciliation
FinancialTransactionId?, BankStatementItemId?, CardInvoiceItemId?, Status, Score, AmountDifference, DateDifferenceDays, DescriptionScore, Reason, auditoria.

## AuditLog
UserId, GrupoId (do dado alterado, ou de quem logou; null quando não se aplica),
Timestamp, Action, EntityName, EntityId, OldValues/NewValues (JSON), IpAddress, UserAgent,
CorrelationId — ver `docs/17-auditoria.md` para como é preenchido.

## Índices
GrupoId + TransactionDate; GrupoId + Description; MerchantId; data dos itens de
extrato/fatura; status de conciliação; UserId único em GrupoMember.
