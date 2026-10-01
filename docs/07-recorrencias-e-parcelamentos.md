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

## Onde se cria um parcelamento
Só existe uma porta: a aba **Parcelamento** do formulário de lançamento
(`TransactionFormComponent`). Havia também uma tela dedicada `/parcelamento/novo`, com a mesma
criação escrita duas vezes; ela foi retirada e o desenho dela — os dois cartões "Como quer
informar o parcelamento?" — é o que o formulário usa hoje.

A origem vem da navegação: entrando por Contas › Lançamentos a origem é conta, por Cartões ›
Lançamentos é cartão, e o formulário nem mostra o seletor. O botão "+ Novo parcelamento" da lista
de parcelamentos abre `/lancamentos/novo?modo=parcelamento`, que não está sob nenhuma das duas
áreas — ali, e só ali, o seletor de origem aparece.

## Anexos do parcelamento
O anexo pertence ao **plano inteiro**, não a cada parcela: a nota fiscal da compra é um documento
só e vale para todas as parcelas dela.

Como o `InstallmentPlan` não guarda conta nem cartão — a origem vive no `FinancialTransaction` de
cada parcela —, parcelamento de conta e de cartão são o mesmo caso: uma implementação cobre os
dois.

Pode ser anexado na criação (aba Parcelamento do formulário de lançamento) e depois pelo detalhe
expandido da lista de parcelamentos. Na tela de Lançamentos, cada parcela mostra o clipe herdado do plano, para as 12
linhas não parecerem sem documento.

Recorrências não têm anexo: ali o documento pertenceria à recorrência, não ao lançamento gerado.

## Parcelas e a fatura do cartão
Cada parcela é um lançamento com data própria, então cai sozinha no ciclo de fatura correspondente
(docs/05 — Pagamento de cartão). Não há nada a fazer: um parcelamento de 12x num cartão aparece
distribuído por 12 faturas, uma parcela em cada.
