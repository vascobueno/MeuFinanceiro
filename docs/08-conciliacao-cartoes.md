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
