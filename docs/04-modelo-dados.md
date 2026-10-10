# 04 — Modelo de Dados

## Entidades
```text
ApplicationUser
RefreshToken
Grupo
GrupoMember
Account
CreditCard
IssuedCard
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
CardCycleOverride
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

## Account
Name, Bank (nome), Type, InitialBalance, IsActive, mais:

- **Dados bancários** — `BankCode` (COMPE, 3 dígitos), `Agency` (4), `AgencyDigit?`,
  `AccountNumber` (até 13 dígitos), `AccountDigit` (dígito ou X). Opcionais, mas todos juntos
  (só o dígito da agência pode faltar): `dados-bancarios-incompletos`. Gravados só com dígitos —
  "12.345-6" vira número "12345" e dígito "6"; código e agência ganham zeros à esquerda. A mesma
  agência e conta não se repetem no grupo (`conta-duplicada`, e índice único filtrado
  `GrupoId + BankCode + Agency + AccountNumber + AccountDigit` onde há número). Contas
  cadastradas antes ficam só com o nome do banco; na edição ele aparece como "Outro".
- **`OverdraftLimit`** — limite do cheque especial (≥ 0, `limite-invalido`). Não entra no saldo
  nem no "Saldo total"; a resposta traz `AvailableBalance` = saldo + limite, que a tela de
  lançamentos da conta mostra como "Disponível" quando há limite.

A lista de bancos do formulário (`core/data/bancos.ts`) vem da relação oficial do Banco Central
(Participantes do STR, 460 instituições com código, baixada em 10/10/2026); os 31 mais comuns têm
nome curto e aparecem primeiro. É só conveniência da tela: o backend aceita qualquer código de 3
dígitos, e banco que surgir depois entra por "Outro".

## CreditCard / IssuedCard
`CreditCard` é o **grupo de cartões** que caem na mesma fatura: nome, fechamento, vencimento,
limite e a **bandeira** (`CardBrand`, gravada pelo nome: Visa, Mastercard, Elo, AmericanExpress,
Hipercard, DinersClub, Jcb, Discover, UnionPay, Cabal, Outra). A bandeira é do grupo porque a
fatura é uma só. Maestro, VisaElectron e EloDebito existem no enum só para o cartão de débito;
o grupo de crédito os recusa (`bandeira-de-debito`). A API aceita os apelidos antigos ("Master",
"Amex"); a migração converteu o texto livre que havia (desconhecido → Outra).

`IssuedCard` é cada cartão do grupo:

- `Kind` — Titular, Adicional ou Online (virtual). **Não há regra de um titular só**: o mesmo grupo
  pode ter dois cartões de titular ativos.
- `FirstDigits` (4 a 6, opcional) e `LastFour` (obrigatório). **O número completo nunca é
  guardado**, nem o CVV — não há campo para ele no contrato; mais de 6 primeiros ou 4 últimos é
  recusado (`numero-completo-nao-guardado`), e a tela divide o número colado e descarta o meio
  antes de enviar.
- `ExpiryMonth`/`ExpiryYear` (os dois ou nenhum), `HolderName`.
- `Status` — Ativo, Substituído (com `ReplacedById` apontando o novo) ou Cancelado. "Vencido" é
  calculado pela validade (`IsExpired`), não é status.
- Final único **entre os ativos do grupo de usuários** (`final-duplicado`, índice único filtrado
  `GrupoId + LastFour` onde `Status = 1`): é o final que a fatura imprime em cada compra.
  Substituídos e cancelados não contam, porque a renovação costuma manter o número.

Substituir grava em dois passos (o antigo sai de Ativo antes de o novo entrar), para o índice
único não ver dois ativos com o mesmo final; tudo o que pode ser recusado é validado antes.

**Bandeira pelos primeiros dígitos** (`CardBrands.SuggestFromDigits`) — só quando não há dúvida:
"4011" pode ser Visa ou Elo (401178), então não sugere; "401178" é Elo. Dígitos de outra bandeira
que a do grupo viram aviso (`BrandMismatch`), não bloqueio. Na tela, o nome do grupo também sugere
("Santander Elo" → Elo) quando a bandeira está em branco.

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
Pagamento de uma fatura de cartão. Campos: `CreditCardId`, `CycleReference` (qual fatura ele
quita), `PaymentDate`, `Amount`, `AccountId?` e `PaymentTransactionId?`.

**A fatura é identificada pelo MÊS (`CycleReference`, dia 1), não pela data de fechamento.** A data
pode ser ajustada por fatura (ver `CardCycleOverride`); se ela fosse a chave, cada ajuste deixaria os
pagamentos já registrados órfãos e eles sumiriam da tela sem aviso. O mês não muda.

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

## CardCycleOverride
Exceções ao calendário do cartão para **um** mês: `CreditCardId`, `ReferenceMonth` (dia 1),
`ClosingDate?` e `DueDate?`. A operadora às vezes antecipa ou adia o fechamento (fim de semana,
feriado) e pode remarcar o vencimento; os dias do cadastro valem para todos os meses.

Uma linha por `(cartão, mês)` com os dois campos anuláveis, e não duas tabelas: mesma chave, uma
consulta só, e limpar tudo é um `DELETE`. **Linha com os dois nulos não existe** — ao limpar um
ajuste com o outro já nulo, a linha é removida.

**Invariante do fechamento**: `ClosingDate` tem de cair **dentro** de `ReferenceMonth`. Sem isso,
dois ciclos poderiam se sobrepor — o mesmo lançamento entraria em duas faturas — ou se inverter,
deixando uma janela vazia. Os dois erram em silêncio.

O vencimento não tem invariante rígido porque **não define a janela do ciclo**: ele só aparece na
tela e no cálculo de atraso. A única regra é não vencer antes de fechar (ver docs/05).

Único em `(CreditCardId, ReferenceMonth)`: dois ajustes para o mesmo mês seriam ambíguos, e o
índice é a barreira contra isso mesmo em corrida entre requisições.

## Reconciliation
FinancialTransactionId?, BankStatementItemId?, CardInvoiceItemId?, CardPaymentId?, Status, Score, AmountDifference, DateDifferenceDays, DescriptionScore, Reason, AcceptedAt?, auditoria.

## AuditLog
UserId, GrupoId (do dado alterado, ou de quem logou; null quando não se aplica),
Timestamp, Action, EntityName, EntityId, OldValues/NewValues (JSON), IpAddress, UserAgent,
CorrelationId — ver `docs/17-auditoria.md` para como é preenchido.

## Índices
GrupoId + TransactionDate; GrupoId + Description; MerchantId; data dos itens de
extrato/fatura; status de conciliação; UserId único em GrupoMember.
