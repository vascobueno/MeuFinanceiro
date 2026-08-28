# 04 — Modelo de Dados

## Entidades
```text
ApplicationUser
RefreshToken
Household
HouseholdMember
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

## Household / HouseholdMember
`Household`: Id, Name. `HouseholdMember`: HouseholdId, UserId, Role (`HouseholdRole`:
Owner/Member), AccessProfile (`HouseholdAccessProfile`: Completo/Editor/Visualizador — Owner é
sempre Completo). Todo `ApplicationUser` pertence a exatamente um household (índice único em
`HouseholdMember.UserId`).

## FinancialTransaction
Id, **HouseholdId** (dono real do dado, compartilhado entre os membros do household — não é
mais `UserId` direto), TransactionDate, Description, MerchantId, CategoryId, AccountId?,
CreditCardId?, Type, Amount decimal(18,2), Direction, Status, InstallmentId?,
RecurringTransactionId?, CreatedAt/By, UpdatedAt/By (`CreatedBy`/`UpdatedBy` continuam
identificando a pessoa, não o household). Mesmo padrão (`HouseholdOwnedEntity`) vale para as
outras 11 entidades financeiras (Account, CreditCard, Category, Merchant,
RecurringTransaction, InstallmentPlan, BankStatement, CardInvoice, Reconciliation,
InvestmentAccount, ImportFile).

## Reconciliation
FinancialTransactionId?, BankStatementItemId?, CardInvoiceItemId?, Status, Score, AmountDifference, DateDifferenceDays, DescriptionScore, Reason, auditoria.

## AuditLog
UserId, HouseholdId (do dado alterado, ou de quem logou; null quando não se aplica),
Timestamp, Action, EntityName, EntityId, OldValues/NewValues (JSON), IpAddress, UserAgent,
CorrelationId — ver `docs/17-auditoria.md` para como é preenchido.

## Índices
HouseholdId + TransactionDate; HouseholdId + Description; MerchantId; data dos itens de
extrato/fatura; status de conciliação; UserId único em HouseholdMember.
