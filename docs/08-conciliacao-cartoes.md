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

**Seções de cartão** — a fatura vem com um cabeçalho por cartão (`NOME - 5480 XXXX XXXX 1111`,
"@" no virtual, ou "final 1234" / "**** 1234"). Cada item guarda o final da seção em que veio
(`CardLastFour`) e, na importação, é ligado ao cartão cadastrado com esse final no grupo
(`IssuedCardId`; substituído também vale, porque a fatura ainda traz compras dele).

**Grupo pelos finais** — a leitura (`/card-invoices/parse`) devolve os cartões dos cabeçalhos e,
se todos os finais conhecidos forem do mesmo grupo, o reconhece (`detectedCreditCardId`): a tela
troca o grupo escolhido e avisa. Final que o grupo não tem pausa a importação e oferece
"Adicionar ao grupo" (o "@" sugere Virtual); ou se importa assim mesmo, com o final guardado.

**Tipo do item** (`Kind`) — Pagamento (negativo com PAGAMENTO/PAGTO), Crédito (outro negativo),
IOF (imposto), Tarifa (anuidade, tarifa, seguro, juros, multa — mesmo parcelada), Parcela (rótulo
NN/NN, com número e total guardados) ou Compra. O casamento do pagamento usa o tipo.

**Compra no exterior** — a segunda coluna de valor é a moeda estrangeira (`ForeignAmount`, USD).
A linha `COTAÇÃO DOLAR R$ 5,4970` dá a cotação das compras da seção (`ExchangeRate`, 6 casas);
sem ela, a cotação é calculada (reais ÷ estrangeiro) e marcada (`ExchangeRateEstimated`). Moeda e
valor de origem que a descrição mostre ("EUR 19,50") ficam em `OriginalCurrency`/`OriginalAmount`
— informação, porque o banco converte para dólar antes de cobrar em reais; o valor depois do
código da moeda nunca é lido como coluna. O IOF fica ligado à compra no exterior
(`RelatedItemId`). A tela mostra "US$ 21,22 × 5,4970 (calculada) · origem EUR 19,50".

**VALOR TOTAL** — quando a fatura traz um só, preenche o total se ele estiver em branco.

**Datas da fatura** — "Data de fechamento", "Vencimento" e "Próximo fechamento" são lidos
(`closingDateFromText`, `dueDateFromText`, `nextClosingDateFromText`). Depois da importação, se o
fechamento lido de um mês difere do que o calendário do cartão calcula, a tela oferece "Ajustar
esse ciclo" (um `CardCycleOverride` daquele mês) — para esta fatura e para a próxima. O ciclo é
procurado pelo mês do fechamento lido, não pela data: "fecha dia 3" com cadastro no dia 2 é o
ciclo do mesmo mês.

**Lançar** — item sem par (anuidade, seguro, IOF, a compra esquecida) vira lançamento no cartão já
conciliado (`POST /reconciliations/card/{id}/create-transaction`, categoria opcional): data e
valor do item, a parcela na descrição, o câmbio do item; estorno e crédito entram como despesa
com direção crédito (reduzem a fatura). Pagamento não se lança por aqui (`item-de-pagamento`): ele
se registra em "Pagar fatura". Item de **valor zero** (anuidade isenta) entra como **Ignorado** e
não gera pendência.

Faturas importadas antes destas regras foram classificadas pela migração com as mesmas regras de
tipo; final do cartão e câmbio ficam vazios nelas.

**Total** — o total da fatura é `Saldo anterior + soma dos itens`. A fatura traz o pagamento da
fatura passada como item negativo, então sem o saldo anterior a conta não fecha nunca. A tela tem
o campo "Saldo anterior" (`PreviousBalance`).

Não fecha, duplicidade e exclusão seguem as mesmas regras do extrato (doc 09), com
`fatura-nao-fecha`, `fatura-duplicada` e `fatura-com-conciliacao-aceita`; a exclusão fica na tela
Faturas, com a coluna Confere.

## Com o que cada item é comparado
Cada item da fatura procura, nesta ordem:

1. **Pagamento da fatura** — linha negativa com "PAGAMENTO"/"PAGTO". É comparada com os
   `CardPayment` do cartão (valor, data; +10 se o pagamento é do ciclo anterior ao da fatura). A
   ligação vai em `Reconciliation.CardPaymentId`, não em um lançamento: o lançamento do pagamento
   está na conta e é conciliado lá, com o extrato. Aceitar não muda o status de nada; fica só o
   `AcceptedAt`.
2. **Parcela** — item com rótulo `NN/TT`. A fatura imprime a parcela com a data da **compra**
   (`19/05 LOJA 04/06` na fatura de setembro), então a comparação é: parcela de número NN de um
   plano de TT parcelas, com a **data da 1ª parcela** perto da data do item. Antes a janela de 15
   dias em volta da data do item descartava a parcela, e o rótulo era comparado com `Contains` —
   "10/12" casava com a parcela 1.
3. **Lançamento à vista** — data do lançamento perto da do item, como antes. Item com rótulo de
   parcela nunca casa aqui com parcela de outro número.

Um lançamento ou pagamento serve a um item só na mesma importação. A coluna **Correspondência**
da tela mostra com o que o item foi ligado ("Parcela 4/6 de …", "Pagamento de …") ou por que não
foi.

**Aceite** — `Reconciliation.AcceptedAt` registra quando o usuário aceitou ou vinculou à mão;
status Matched sozinho também é o que a importação grava como sugestão. Fatura com item aceito não
pode ser excluída. Pagamento com conciliação aceita não pode ser excluído
(`pagamento-conciliado`); se só sugerido, a sugestão volta a "sem vínculo".
