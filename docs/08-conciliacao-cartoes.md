# 08 — Conciliação de Cartões

Entrada inicial: PDF da fatura.

Normalizar em `CardInvoice` + `CardInvoiceItem`.

Comparar valor, data, descrição/estabelecimento, parcela e cartão.

Score inicial sugerido:
- valor 35
- data 25
- descrição 25
- parcela/documento 10
- tipo 5

90–100 MATCHED; 70–89 POSSIBLE_MATCH; abaixo de 70 UNMATCHED.

Ações: aceitar, manual, criar lançamento, ignorar, duplicidade, desfazer com auditoria.

## Importação da fatura
O texto colado é lido no backend (`POST /api/v1/card-invoices/parse`, `InvoiceTextReader`):
- Linha com duas colunas lado a lado (como sai do PDF) é separada em duas.
- Dígito de modalidade no começo (1–3) é descartado.
- Até dois valores no fim: o primeiro é o valor em reais; o segundo (dólar) é ignorado.
- Parcela `04/06`, `Parcela 04/06` ou `04-06` vira o rótulo `04/06`.
- Linha sem data é ignorada, exceto IOF, que herda a data da anterior.
- Pagamento aparece negativo (`-1.000,00`).

**Total** — o total da fatura é `Saldo anterior + soma dos itens`. A fatura traz o pagamento da
fatura passada como item negativo, então sem o saldo anterior a conta não fecha nunca. A tela tem
o campo "Saldo anterior" (`PreviousBalance`).

Não fecha, duplicidade e exclusão seguem as mesmas regras do extrato (doc 09), com
`fatura-nao-fecha`, `fatura-duplicada` e `fatura-com-conciliacao-aceita`; a exclusão fica na tela
Faturas, com a coluna Confere.
