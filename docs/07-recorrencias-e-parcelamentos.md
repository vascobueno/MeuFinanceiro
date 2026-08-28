# 07 — Recorrências e Parcelamentos

## Recorrência
Possui versões por `EffectiveFrom`.

Exemplo:
```text
01/01/2026 -> R$ 150
01/09/2026 -> R$ 175
```

Só a primeira ocorrência (a `StartDate`) vira um `FinancialTransaction` real, no momento em que
a recorrência é criada. As ocorrências seguintes não são materializadas automaticamente — elas
são calculadas sob demanda por `RecurrenceOccurrenceCalculator` (compartilhado entre a tela
Projeção e a prévia de próximas ocorrências da tela Recorrências) e só viram lançamentos de
verdade quando de fato acontecerem (hoje, isso ainda depende de um novo lançamento manual ou de
um job de materialização a ser implementado — não existe job automático ainda).

A tela `/recorrencia` lista todas as recorrências do usuário com a versão vigente e as 3
próximas datas previstas; `/recorrencia/:id` altera o valor a partir de uma data de vigência.

## Parcelamento
```text
TOTAL_AMOUNT:
1000 / 3 = 333,33 + 333,33 + 333,34

INSTALLMENT_AMOUNT:
250 x 12 = 3000
```

Cada parcela é independente para conciliação.
