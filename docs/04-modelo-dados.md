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
Attachment
CardPayment
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

## Attachment
Arquivo anexado a um lançamento ou a um parcelamento — nota fiscal, boleto, comprovante.
Guarda apenas metadados (`FileName`, `ContentType`, `SizeBytes`, `FileHash` SHA-256, `BlobPath`);
o conteúdo vive no storage S3-compatible.

Dono: **exatamente um** entre `FinancialTransactionId` e `InstallmentPlanId` — mesmo idioma que
`FinancialTransaction` usa para a origem (conta OU cartão). Uma tabela só, em vez de uma por tipo
de dono, para que um service/controller/componente cubra os dois casos e um terceiro dono futuro
custe uma coluna.

O anexo do parcelamento fica no **plano inteiro**, não na parcela: a nota da compra é um documento
só e vale para todas as parcelas. Como o plano não guarda conta nem cartão (a origem vive nos
lançamentos das parcelas), "parcelamento de conta" e "de cartão" são o mesmo caso aqui.

`ContentType` é o deduzido da assinatura do arquivo no servidor, nunca o informado pelo cliente
(ver docs/12-seguranca.md). `FileName` é só para exibir e para o `Content-Disposition` — nunca
entra na chave do storage, que é `{grupoId}/{attachmentId}{extensão}`.

FK `Cascade` para os dois donos: o anexo não tem vida própria. O cascade apaga as linhas, mas
**não** os objetos no storage — por isso a Application remove os blobs antes de excluir o dono.

## CardPayment
Pagamento de uma fatura de cartão. Campos: `CreditCardId`, `CycleClosingDate` (qual fatura ele
quita), `PaymentDate`, `Amount`, `AccountId?` e `PaymentTransactionId?`.

**O ciclo é calculado, não persistido.** Não existe tabela de faturas derivadas: `CardCycleCalculator`
traduz o `ClosingDay`/`DueDay` do cartão em períodos concretos, e a chave do ciclo é a própria data
de fechamento. Persistir ciclos exigiria gerá-los e mantê-los em dia para sempre, sem ganho —
diferente do fechamento de saldo, que existe porque somar o histórico inteiro a cada leitura custa.

`AccountId` nulo significa **pago por outra forma**: não sai de conta nenhuma e não gera lançamento.
Quando há conta, o pagamento gera um `FinancialTransaction` nela (`PaymentTransactionId`) — é esse
lançamento que derruba o saldo, pela consulta de saldo que já existe.

Entidade própria, e não um par de lançamentos como a transferência, porque "outra forma" não tem
perna em conta nenhuma e porque o pagamento é o que o usuário edita depois (trocar a conta).

FKs: `Restrict` para cartão e conta — excluir um deles com pagamento vinculado é barrado na
Application com mensagem clara; `SetNull` para o lançamento, porque o pagamento sobrevive a ele.

Não confundir com `CardInvoice`, que é o PDF importado do banco para conciliação (docs/08).

## Reconciliation
FinancialTransactionId?, BankStatementItemId?, CardInvoiceItemId?, Status, Score, AmountDifference, DateDifferenceDays, DescriptionScore, Reason, auditoria.

## AuditLog
UserId, GrupoId (do dado alterado, ou de quem logou; null quando não se aplica),
Timestamp, Action, EntityName, EntityId, OldValues/NewValues (JSON), IpAddress, UserAgent,
CorrelationId — ver `docs/17-auditoria.md` para como é preenchido.

## Índices
GrupoId + TransactionDate; GrupoId + Description; MerchantId; data dos itens de
extrato/fatura; status de conciliação; UserId único em GrupoMember.
