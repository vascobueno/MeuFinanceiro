# 13 — API

Todos os endpoints ficam sob `api/v1`, exigem autenticação (cookie httpOnly `access_token`,
lido via `JwtBearerEvents.OnMessageReceived` — nunca um header `Authorization` manual) salvo
indicação em contrário, e são sempre escopados ao usuário autenticado (`ICurrentUserService`).
Todo POST/PUT/DELETE fora do `## Auth` abaixo exige também o header `X-XSRF-TOKEN` (CSRF —
ver docs/12-seguranca.md), obtido via `GET /auth/csrf`.

## Auth
POST `/api/v1/auth/register` (Name, Username, Email, Password, CaptchaToken opcional) — não
loga mais automaticamente, devolve só `{ message }`; manda e-mail de confirmação.
POST `/api/v1/auth/login` (Username, Password — login é por nome de usuário, não e-mail) —
devolve `UserResponse` no corpo; os tokens vão em cookie httpOnly (`Set-Cookie`), não no JSON.
POST `/api/v1/auth/refresh` (sem corpo — lê o cookie `refresh_token`, restrito ao path
`/api/v1/auth`) — devolve `UserResponse`, renova os dois cookies.
GET `/api/v1/auth/me` (autenticado) — "quem sou eu", usado pelo frontend pra rehidratar a
sessão a partir do cookie após um F5.
GET `/api/v1/auth/csrf` (sem autenticação) — grava o cookie `XSRF-TOKEN` (legível por JS).
POST `/api/v1/auth/logout` (autenticado, sem corpo — lê o cookie `refresh_token`) — revoga o
refresh token e limpa os dois cookies de sessão.
POST `/api/v1/auth/forgot-password` (Email, CaptchaToken opcional — sem autenticação; sempre
204, exista ou não o e-mail — evita enumeração de contas)
POST `/api/v1/auth/reset-password` (sem autenticação; token do Identity, 1h de validade,
revoga todos os refresh tokens do usuário ao concluir)
POST `/api/v1/auth/change-password` (CurrentPassword, NewPassword — usada tanto pra troca
voluntária quanto pra troca obrigatória de senha temporária; limpa `MustChangePassword` e
confirma o e-mail ao concluir)
POST `/api/v1/auth/confirm-email` (Email, Token — sem autenticação; mesma mensagem genérica
pra "e-mail não existe" e "token inválido/expirado")
POST `/api/v1/auth/resend-confirmation` (Username — sem autenticação; sempre 204)

Todos os endpoints de `## Auth` (`[AllowAnonymous]` ou não) ficam de fora do `CsrfFilter` —
login/registro precisam funcionar antes de existir qualquer cookie CSRF.

## Transactions (Lançamentos)
GET `/api/v1/transactions` (paginação, filtros, ordenação)
GET `/api/v1/transactions/{id}`
POST `/api/v1/transactions` (simples, parcelamento ou recorrência conforme o corpo)
PUT `/api/v1/transactions/{id}`
DELETE `/api/v1/transactions/{id}`

## Installment plans (Parcelamento)
POST `/api/v1/installment-plans` (resposta traz o `id` do plano, usado para anexar arquivos)

## Attachments (Anexos de lançamento e parcelamento)
GET `/api/v1/attachments?transactionId=` ou `?installmentPlanId=` (exatamente um dos dois)
POST `/api/v1/attachments?transactionId=` ou `?installmentPlanId=` (multipart, campo `file`)
GET `/api/v1/attachments/{id}/content` (download)
DELETE `/api/v1/attachments/{id}`

PDF e imagens (JPG, PNG, WEBP, HEIC), até 10 MB, no máximo 10 por item. O tipo é conferido pela
assinatura do arquivo, não pela extensão nem pelo Content-Type enviado (docs/12-seguranca.md).
O download sai sempre como `Content-Disposition: attachment` com `X-Content-Type-Options: nosniff`,
e anexo de outro grupo devolve **404** (não 403), para não confirmar que o id existe.
Perfil Visualizador lê e baixa, mas não anexa nem remove.

## Recurring transactions (Recorrências)
GET `/api/v1/recurring-transactions?type=` (lista + prévia das próximas ocorrências; `type`
opcional filtra Income/Expense — usado pelo relatório de gastos recorrentes)
GET `/api/v1/recurring-transactions/{id}`
POST `/api/v1/recurring-transactions/{id}/versions` (regra 13: nova versão por data de vigência)

## Merchants (autocomplete de descrição)
GET `/api/v1/merchants/suggestions?prefix=ama`

## Accounts
GET `/api/v1/accounts`
GET `/api/v1/accounts/{id}`
POST `/api/v1/accounts`
PUT `/api/v1/accounts/{id}`
DELETE `/api/v1/accounts/{id}`

## Cards
GET `/api/v1/cards`
GET `/api/v1/cards/{id}`
POST `/api/v1/cards`
PUT `/api/v1/cards/{id}`
DELETE `/api/v1/cards/{id}`

## Card payments (Pagamento de fatura)
GET `/api/v1/cards/{id}/invoice?reference=yyyy-MM-01` — fatura do ciclo, calculada a partir dos
lançamentos. **`reference` é o MÊS (dia 1)**, não a data de fechamento; omitida, devolve o ciclo
atual. Devolve total, pago, em aberto, período, vencimento, os pagamentos e se o fechamento foi
ajustado.
PUT `/api/v1/cards/{id}/invoice/{reference}/closing` — ajusta o fechamento desta fatura
DELETE `/api/v1/cards/{id}/invoice/{reference}/closing` — volta ao dia do cadastro
PUT `/api/v1/cards/{id}/invoice/{reference}/due` — ajusta o vencimento desta fatura
DELETE `/api/v1/cards/{id}/invoice/{reference}/due` — volta ao vencimento derivado
POST `/api/v1/card-payments`
PUT `/api/v1/card-payments/{id}` (permite trocar a conta, inclusive para "outra forma")
DELETE `/api/v1/card-payments/{id}`

Erros 400: `referencia-invalida` (a referência não é o dia 1 — deixaria pagamentos num ciclo
inexistente); `fechamento-fora-do-mes` (faria dois ciclos se sobreporem);
`vencimento-antes-do-fechamento`; `vencimento-distante` (mais de 90 dias após o fechamento);
`fechamento-depois-do-vencimento` (o fechamento passaria um vencimento já ajustado — ajuste o
vencimento primeiro). Ver docs/05 para a regra de que o pagamento é
movimentação, não despesa.

## Categories
GET `/api/v1/categories`
POST `/api/v1/categories`
PUT `/api/v1/categories/{id}`
DELETE `/api/v1/categories/{id}`
POST `/api/v1/categories/{id}/subcategories`

## Issued cards (cartões de um grupo)
GET `/api/v1/cards/{cardId}/issued-cards`
POST `/api/v1/cards/{cardId}/issued-cards` (`kind`, `lastFour`, `firstDigits?`, `expiryMonth?`, `expiryYear?`, `holderName?`; 400 `final-duplicado` / `numero-completo-nao-guardado` / `validade-invalida`…)
PUT `/api/v1/issued-cards/{id}`
POST `/api/v1/issued-cards/{id}/replace` (mesmo corpo; devolve o novo; o antigo fica Substituído)
POST `/api/v1/issued-cards/{id}/cancel`
DELETE `/api/v1/issued-cards/{id}`

`brand` de `/api/v1/cards` é o nome da bandeira (`Mastercard`, `AmericanExpress`…); na entrada
valem também "Master" e "Amex". 400 `bandeira-invalida` / `bandeira-de-debito`.

## Card invoices (Faturas)
GET `/api/v1/card-invoices` (paginação, filtros, ordenação; `balanceValidated` por fatura)
DELETE `/api/v1/card-invoices/{id}` (204; 400 `fatura-com-conciliacao-aceita`)

## Bank statements (Extratos)
GET `/api/v1/bank-statements` (paginação, filtros, ordenação) — lista os extratos importados.
DELETE `/api/v1/bank-statements/{id}` (204; 400 `extrato-com-conciliacao-aceita`)
A importação fica em Conciliação bancária, abaixo.

## Bank reconciliation (Conciliação bancária)
POST `/api/v1/bank-statements/parse` (texto → itens, erros por linha, saldos lidos, soma)
POST `/api/v1/bank-statements/parse-ofx` (multipart `file`; conta reconhecida, período, saldos, itens com `externalId`)
POST `/api/v1/bank-statements/import` (`confirmUnbalanced`; 400 `extrato-nao-fecha` / `extrato-duplicado` / `extrato-sem-movimentos-novos`; `skippedDuplicates` na resposta)
GET `/api/v1/reconciliations/bank`
POST `/api/v1/reconciliations/bank/{id}/accept`
POST `/api/v1/reconciliations/bank/manual`
POST `/api/v1/reconciliations/bank/{id}/undo`

## Card reconciliation (Conciliação de cartão)
POST `/api/v1/card-invoices/parse`
POST `/api/v1/card-invoices/import` (`previousBalance`, `confirmUnbalanced`; 400 `fatura-nao-fecha` / `fatura-duplicada`)
GET `/api/v1/reconciliations/card`
POST `/api/v1/reconciliations/card/{id}/accept`
POST `/api/v1/reconciliations/card/manual` (`financialTransactionId` **ou** `cardPaymentId`)
POST `/api/v1/reconciliations/card/{id}/undo`

## Projection
GET `/api/v1/projection?date=2026-12-31` (cada item da timeline tem `Type` Income/Expense —
usado pelo relatório de projeção de receitas, que filtra `Type == Income` no frontend)

## Grupos (Grupo — RF016)
GET `/api/v1/grupos/me` (grupo atual + membros; 403 se o perfil não for Completo/Owner)
POST `/api/v1/grupos/me/members` (Owner-only — convida por e-mail, só se o grupo do
convidado ainda estiver vazio)
POST `/api/v1/grupos/me/members/new` (Owner-only — cria a conta inteira de alguém que ainda
não tem conta, com senha temporária enviada por e-mail; `MustChangePassword` força a troca no
primeiro login)
DELETE `/api/v1/grupos/me/members/{userId}` (Owner-only — bloqueia remover o último Owner)
PUT `/api/v1/grupos/me/members/{userId}/access-profile` (Owner-only — Completo/Editor/
Visualizador; rejeita se o alvo for Owner)

## Admin (Administrator — RF017)
Tudo sob `[Authorize(Roles="Administrator")]`, opera em qualquer grupo (não só o próprio):
GET `/api/v1/admin/grupos` (todos os grupos do sistema)
GET `/api/v1/admin/members` (todos os membros de todos os grupos, achatado — usado pela
tela "Perfis de acesso")
GET/POST/DELETE `/api/v1/admin/grupos/{grupoId}/members[/{userId}]` (mesmas regras do
`/grupos/me/members`, sem exigir ser Owner)
POST `/api/v1/admin/grupos/{grupoId}/members/new` (mesma regra de `/grupos/me/members/new`,
sem exigir ser Owner)
PUT `/api/v1/admin/grupos/{grupoId}/members/{userId}/access-profile`

## Audit (Auditoria — RF014, ver docs/17-auditoria.md)
GET `/api/v1/audit` (filtro por período e Action; Owner/Member vê só o próprio grupo,
Administrator vê de todos, com filtro adicional por grupo)

## Investments
GET `/api/v1/investments/summary`
GET `/api/v1/investments`
POST `/api/v1/investments`
POST `/api/v1/investments/accounts`
POST `/api/v1/investments/transactions`

## Imports (migração inicial)
GET `/api/v1/imports`
POST `/api/v1/imports/excel`
POST `/api/v1/imports/excel/{importFileId}/confirm`

Todas as listas devem suportar paginação, filtros e ordenação onde fizer sentido para o volume
de dados (Transactions, Card invoices); listas pequenas por natureza (Accounts, Cards,
Categories, Recurring transactions) retornam tudo de uma vez.
