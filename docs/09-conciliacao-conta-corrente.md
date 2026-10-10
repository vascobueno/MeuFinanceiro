# 09 — Conciliação de Conta Corrente

O extrato consolidado Santander de exemplo possui resumo da conta, data, descrição, documento, movimento e saldo; inclui compras de débito, saques, PIX, boleto, débito automático, aplicações e remuneração automática.

O extrato informa saldo de conta corrente de R$ 30.536,79 em 30/04 e R$ 33.293,39 em 31/05.

Há também saldos por período e saldo disponível; estes devem ser armazenados separadamente do saldo simples da conta.

Tipos:
DEBIT_CARD_PURCHASE, ATM_WITHDRAWAL, PIX_SENT, PIX_RECEIVED, TED_SENT, TED_RECEIVED, BOLETO_PAYMENT, BILL_PAYMENT (fatura de cartão paga pela conta), TAX_PAYMENT, DIRECT_DEBIT, BANK_FEE, INTEREST_CHARGE, INVESTMENT, INVESTMENT_REDEMPTION, INVESTMENT_INTEREST, TRANSFER, OTHER_INCOME, OTHER_EXPENSE.

**Classificação** — o tipo sai da descrição e do sinal (`StatementItemClassifier`), com regras
em ordem e palavra inteira ("TED" não casa em "CREDITED"). O sinal separa os pares: Pix e TED
enviados ou recebidos, aplicação ou resgate. Na colagem estruturada vale o tipo informado se for
um dos conhecidos; vazio ou desconhecido é classificado — também na importação, para clientes que
mandem sem tipo. A tela de conciliação mostra o tipo em português.

Extratos importados antes da classificação mantêm OTHER_EXPENSE/OTHER_INCOME.

**Data da compra no débito** — o extrato imprime a compra na data do débito e, na linha do
estabelecimento, a data da compra (`08/01 COMPRA CARTAO DEB MC … 18,00-` / `02/01 LOJA`). Quando
ela é anterior (até 10 dias), vira `EffectiveDate` e sai da descrição; a conciliação compara com
ela, porque é a data em que a compra é registrada. A tela mostra "compra em dd/MM" sob a data.
O prefixo do banco ("COMPRA CARTAO DEB MC") deixa a descrição só parecida, então a compra
costuma chegar a "Possível", não a "Conciliado".

Aplicação CDB/RDB é investimento, não despesa. O exemplo mostra R$ 10.000,00 aplicados em 20/05.

Validar saldo inicial + movimentos = saldo final.

## Importação do extrato
O texto colado é lido **no backend** (`POST /api/v1/bank-statements/parse`, `StatementTextReader`),
não mais na tela. A tela só mostra o resultado e envia os itens para a importação.

Dois modos de leitura:
- **Estruturado**: `data;descrição;valor[;documento][;tipo]` (ou separado por tabulação).
- **Como o banco imprime**: linhas no formato do extrato colado do PDF.
  - Valor com sinal no fim (`1.288,46-`), no começo (`-1.288,46`) ou com `R$`.
  - Data `dd/MM` (o ano vem do fim do período; uma data depois do fim + 7 dias é do ano anterior),
    `dd/MM/aaaa` ou `aaaa-MM-dd`. Linha sem data herda a data da anterior.
  - Até dois valores no fim da linha: o movimento e o saldo do dia (o saldo é ignorado).
  - Linha sem valor continua a descrição da anterior (estabelecimento, nome do pagador).
  - `SALDO EM` / `SALDO ANTERIOR` preenchem o saldo inicial e o final.
  - Só valores com exatamente duas casas contam como dinheiro — é o que separa o valor do número
    de documento e de uma cotação de quatro casas.

Cada linha que não dá para ler volta com o número da linha e o motivo; nada é importado até
corrigir.

**Não fecha** — se saldo inicial + movimentos ≠ saldo final, a importação é recusada com a regra
`extrato-nao-fecha` e a diferença em reais. A tela oferece "Importar mesmo assim", que reenvia com
`ConfirmUnbalanced: true`. O resultado fica gravado em `BalanceValidated` e aparece na coluna
**Confere** da tela Extratos.

**Duplicidade** — cada extrato grava um `ContentHash` (SHA-256 de conta, período, saldos e itens
com a descrição normalizada). Importar o mesmo conteúdo de novo é recusado com
`extrato-duplicado`; índice único filtrado `(GrupoId, ContentHash)` garante isso no banco também.
Extratos importados antes desta regra ficam com hash nulo e não são comparados.

## Importação de OFX
"Arquivo OFX" na tela de conciliação envia o arquivo para `POST /api/v1/bank-statements/parse-ofx`
(`OfxStatementReader`), que lê OFX 1.x (SGML) e 2.x (XML) e não grava nada:

- **Conta** — reconhecida por BANKID/BRANCHID/ACCTID contra banco, agência e conta do cadastro
  (`OfxAccountMatcher`), só com dígitos e sem zeros à esquerda: ACCTID como número, número+dígito
  ou agência+número+dígito; BANKID com 3 ou 4 dígitos; BRANCHID opcional. Só vale se **uma** conta
  do grupo casar; senão a tela pede para escolher e diz o que o arquivo traz.
- **Período e saldos** — DTSTART/DTEND; saldo final do LEDGERBAL. OFX não tem saldo inicial: ele
  é o final menos os movimentos, então a conferência de saldo fecha por construção.
- **Movimentos** — DTPOSTED, TRNAMT (ponto ou vírgula), MEMO e/ou NAME, CHECKNUM/REFNUM, tipo
  pelo classificador e, na compra no débito, a data da compra do MEMO.
- **FITID** — gravado em `BankStatementItem.ExternalId`. Na importação, movimentos com FITID já
  importado **na mesma conta** ficam de fora (`skippedDuplicates`): dá para baixar períodos que
  se sobrepõem. Se nada for novo, `extrato-sem-movimentos-novos`.

Recusados com explicação: OFX de **cartão de crédito** (`CREDITCARDMSGSRSV1`/`CCSTMTRS`) — lido
como extrato, lançaria as compras na conta; vai em Cartões › Conciliação — e o **pedido** de
extrato (`STMTRQ`, o que o programa manda ao banco, sem movimentos). Conferido contra exemplos
públicos de OFX 1.x (SGML) e 2.x (XML), inclusive agência com dígito ("0009-8") e valor sem
centavos ("-100").

Depois de lido, o formulário fica preenchido e editável; "Importar" usa o mesmo
`/bank-statements/import` do extrato colado. Arquivo até 2 MB, decodificado como UTF-8 ou, se
não for, Latin-1 (que coincide com o Windows-1252 dos bancos nas letras do português).

## Tela Extratos
`/extratos` (menu: Contas › Extratos) lista o que a importação acima gravou: conta, período,
saldo inicial e final, movimento do período (final − inicial), quantidade de itens e o arquivo de
origem. É a irmã da tela Faturas do lado da conta — mesma estrutura de filtros, ordenação e
paginação server-side, sem o Status, que não existe para extrato.

Importar extrato continua em Contas › Conciliação. Aqui só se lista e se **exclui**: excluir
apaga o extrato, os itens e as sugestões de conciliação, e libera reimportar o mesmo conteúdo — é o
caminho quando uma importação entrou errada. É recusado (`extrato-com-conciliacao-aceita`) se algum
item já tem conciliação aceita: desfaça a conciliação antes.

> A exclusão carrega e remove os itens explicitamente. Deixar para o cascade do banco fazia o EF
> apagar o extrato antes das conciliações e o Postgres recusava pela FK — o banco em memória dos
> testes não tem FK e não pega isso.

> `GET /api/v1/bank-statements` filtra `GrupoId` à mão, como todas as consultas do projeto — não
> há `HasQueryFilter` global. O teste `Nao_deve_mostrar_extrato_de_outro_grupo` existe por causa
> disso: sem o filtro, o vazamento não quebraria nada visível.
