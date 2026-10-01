# 06 — Lançamentos

Tela: `docs/screens/lancamentos/mockup.html` e `docs/screens/novo-lancamento/mockup.html`.

Campos:
- Data
- Descrição
- Tipo
- Conta/Cartão
- Categoria
- Valor
- Parcela
- Recorrência
- Observação

Autocomplete:
`GET /api/merchants/suggestions?prefix=ama`

Debounce ~300 ms; máximo 10–15 resultados.

Filtros:
período, descrição, categoria, conta, cartão, tipo, status, valor.

Ordenação por coluna e paginação server-side.

## Anexos
Um lançamento pode ter vários arquivos anexados — nota fiscal, boleto, comprovante. PDF e imagens
(JPG, PNG, WEBP, HEIC), até 10 MB cada e no máximo 10 por lançamento.

Ficam disponíveis na criação (segurados até o save, porque só então existe a que vinculá-los) e na
edição. Quando o lançamento é uma **parcela**, o anexo pertence ao plano, não à parcela — ver
docs/07. A lista mostra um clipe nas linhas que têm documento, contando também os anexos do plano
quando é parcela.

Regras de segurança do upload e do download em docs/12-seguranca.md; endpoints em docs/13-api.md.

## Duas telas: conta e cartão
Os lançamentos são vistos em duas telas separadas, porque elas respondem perguntas diferentes.

**`/lancamentos/conta`** — movimentações das contas correntes, com o **saldo** da conta escolhida no
topo (ou a soma de todas). É onde os pagamentos de fatura aparecem, como "Pagamento de cartão":
saem da conta, mas não contam como despesa (docs/05).

**`/lancamentos/cartao`** — a **fatura do ciclo** do cartão escolhido: total gasto, quanto já foi
pago e quanto falta, com navegação entre ciclos e a lista de pagamentos. Os pagamentos não entram
na tabela de gastos, porque são lançamentos da conta, não do cartão.

As duas compartilham `mf-transaction-filters` e `mf-transaction-table`. O filtro server-side
`Origin` (`Account` / `Card`) é o que permite "todos os lançamentos de conta" — os filtros por id
só respondem "desta conta".

No menu, as duas ficam sob os grupos **Contas** e **Cartões**, cada um com *Cadastro* (a tela de
cadastro) e *Lançamentos*. No modo recolhido os rótulos de grupo somem, então os tooltips
desambiguam ("Contas · Lançamentos"), senão restariam quatro ícones indistinguíveis.

> As rotas `lancamentos/conta` e `lancamentos/cartao` precisam ser declaradas **antes** de
> `lancamentos/:id` em `app.routes.ts`, senão o `:id` captura "conta" e abre a tela de edição.

## A origem vem da rota
O botão "+ Novo lançamento" de cada tela leva a uma rota própria — `lancamentos/conta/novo` ou
`lancamentos/cartao/novo` — com `data.origin` em `'account'` / `'card'`. O formulário lê esse dado
e **não mostra o seletor de origem**: quem navegou já disse de onde vem. O rótulo do campo seguinte
acompanha ("Conta" ou "Cartão"), e vindo do cartão o tipo *Transferência* sai da lista, porque
transferência é entre contas próprias (docs/05 — regra 9).

São rotas separadas, e não um query param, porque o breadcrumb lê `data.parent` estático: com
param, o lançamento aberto a partir do cartão citaria a tela da conta. Por isso o `parent` repete o
título da tela de origem ("Lançamentos da conta" / "Lançamentos do cartão").

`lancamentos/novo`, sem origem, continua existindo para o botão da lista de parcelamentos (docs/07)
e precisa vir **antes** de `lancamentos/:id` pelo mesmo motivo da nota acima.

O botão fica dentro do cabeçalho do painel da grade, à direita. Esse cabeçalho é renderizado
**sempre**, e não só quando há linhas: condicioná-lo esconderia o "+ Novo lançamento" justamente na
lista vazia, que é quando ele mais importa.

> Datas ISO vindas da API são lidas com `parseIsoDate` (`core/services/date.util.ts`), nunca com
> `new Date(string)` — este último é UTC e desloca o dia em fuso negativo. Foi o que travou a
> navegação entre ciclos em cartão que fecha no dia 1.
