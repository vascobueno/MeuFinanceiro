# 09 — Conciliação de Conta Corrente

O extrato consolidado Santander de exemplo possui resumo da conta, data, descrição, documento, movimento e saldo; inclui compras de débito, saques, PIX, boleto, débito automático, aplicações e remuneração automática.

O extrato informa saldo de conta corrente de R$ 30.536,79 em 30/04 e R$ 33.293,39 em 31/05.

Há também saldos por período e saldo disponível; estes devem ser armazenados separadamente do saldo simples da conta.

Tipos:
DEBIT_CARD_PURCHASE, ATM_WITHDRAWAL, PIX_SENT, PIX_RECEIVED, TED_RECEIVED, BOLETO_PAYMENT, TAX_PAYMENT, DIRECT_DEBIT, INVESTMENT, INVESTMENT_INTEREST, TRANSFER, OTHER_INCOME, OTHER_EXPENSE.

Aplicação CDB/RDB é investimento, não despesa. O exemplo mostra R$ 10.000,00 aplicados em 20/05.

Validar saldo inicial + movimentos = saldo final.
