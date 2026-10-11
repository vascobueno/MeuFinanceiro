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

## Abas, e não combobox
A conta (e o cartão) é escolhida numa faixa de **abas** — `p-tabs`/`p-tablist`/`p-tab`, com
`[scrollable]="true"` no `p-tabs`, não no `p-tablist`. Só a faixa é usada: a grade fica fora das
abas, senão a tabela seria duplicada por conta.

Não existe aba "Todas". O agregado virou uma **caixa de saldo total**, fora das abas (centrada
com elas) e imune à troca de aba.

### Cada número no cabeçalho do painel a que pertence
O nome da conta ativa e o saldo dela ficam no cabeçalho do painel **Lançamentos** — é o cabeçalho
do que a grade está mostrando, e por isso mudam ao trocar de aba.

No cartão, a faixa de três cartões KPI não existe mais:

| Número | Cabeçalho |
|---|---|
| Total da fatura, Pago | **Pagamentos desta fatura** |
| Em aberto (+ "vence em", vermelho quando vencida) | **Gastos do ciclo** |

> O "Em aberto" precisa do seu próprio `@if (cycle(); as c)` **dentro** do cabeçalho: o painel de
> gastos é renderizado mesmo sem fatura (cartão recém-cadastrado), ao contrário do de pagamentos,
> que inteiro vive dentro do `@if`.

Cada número vai numa **pill** (`.mf-panel-metric`), com o rótulo em caixa normal. No cartão são
quatro: Total da fatura, Pago, Em aberto e Vence em — esta última separada, e vermelha quando a
fatura está vencida. Na conta é só o Saldo: o nome da conta não se repete ali, porque a aba ativa
logo acima já o diz.

A ação de criar virou **ícone** (`pi-plus`) nos dois cabeçalhos, com `aria-label` — sem texto, um
ícone não tem nome acessível, e na lista vazia ele é o único elemento acionável da grade.

Sem conta (ou cartão) cadastrado não há aba nenhuma. Em vez de uma tela vazia, aparece o convite
ao cadastro e o "+ Novo lançamento" **não é renderizado** — não haveria onde lançar.

### A escolha mora na URL
`?conta=<id>` e `?cartao=<id>` guardam a aba ativa. É daí que o formulário recebe a origem, e é o
que faz a aba sobreviver a um F5 e ao pós-salvar. A troca de aba reescreve a URL com
`replaceUrl: true`: trocar de aba não é passo de histórico, senão o Voltar do navegador percorreria
conta por conta antes de sair da tela.

> A lista de lançamentos só é carregada **depois** que as contas chegam, porque antes disso não se
> sabe qual filtrar. Por isso `load()` fica dentro do `subscribe`, com `loading` ligado desde o
> começo para a tela não piscar "nenhum lançamento".

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

### E QUAL conta, também
A rota diz o tipo de origem (`data.origin`); o query param diz qual conta ou cartão. Com os dois, o
formulário **não mostra campo de origem nenhum**: a conta aparece como etiqueta ao lado do título,
e o "Voltar" e o pós-salvar devolvem para a mesma aba.

Transferência segue a mesma lógica: a conta da aba é a origem, só o destino é escolhido — e a
lista de destino exclui a própria conta de origem.

> O id vem da URL, que é editável à mão, sobrevive à exclusão da conta e pode ser de outro grupo.
> `validId` (`core/services/selection.util.ts`) devolve `null` quando o id não existe e **nunca
> chuta outro**: aí o campo de conta reaparece. Se ele chutasse, o cabeçalho anunciaria uma conta e
> o lançamento iria para outra — erro silencioso. As telas, que precisam mostrar alguma coisa,
> completam com `?? defaultId(...)`; o formulário, não.

A edição (`/lancamentos/:id`) **mantém** o campo: mover um lançamento de conta é operação legítima,
e ali não existe aba de origem de onde herdar.

> Datas ISO vindas da API são lidas com `parseIsoDate` (`core/services/date.util.ts`), nunca com
> `new Date(string)` — este último é UTC e desloca o dia em fuso negativo. Foi o que travou a
> navegação entre ciclos em cartão que fecha no dia 1.

## Compra no exterior
O lançamento simples pode ter dados de compra no exterior: valor e moeda da cobrança
(`ForeignAmount`/`ForeignCurrency`, ex. US$ 21,22), a cotação (`ExchangeRate`, 6 casas) e, se houver,
a moeda e o valor de origem (`OriginalCurrency`/`OriginalAmount`, ex. EUR 19,50, cobrado em dólar).
O valor em reais continua em `Amount` — é ele que entra em saldo, fatura e relatórios. No
formulário, "Compra no exterior" abre os campos; valor em reais vazio é preenchido com
estrangeiro × cotação. A grade mostra "US$ 21,22 × 5,4970 · origem EUR 19,50" sob a descrição.

Parcelamento e recorrência não levam (o valor estrangeiro é da compra, não de cada parcela). Na
edição, mandar sem `foreign` tira os dados. Ao conciliar com o item da fatura (aceitar, vincular
ou "Lançar"), o lançamento que não tinha câmbio fica com o da fatura — o que o banco usou.

## Cartão usado
Opcional: o cartão do lançamento (`IssuedCardId`) — titular, adicional ou virtual no cartão de
crédito; o cartão com débito na conta no lançamento da conta. Tem de ser do grupo de usuários
(403) e combinar com a origem: `cartao-de-outra-fatura` / `cartao-sem-debito-na-conta`. As
parcelas herdam; transferência e recorrência não levam. No formulário, "Cartão usado" só aparece
quando a origem tem cartões cadastrados (cancelados ficam de fora; substituídos aparecem, porque a
compra pode ser de antes da troca). Num lançamento novo do cartão, se o grupo tem um único cartão
ativo, o campo já vem com ele. Na edição, mandar sem `issuedCardId` tira a indicação.

Na grade, "Gastos do ciclo" (tela do cartão) mostra a coluna "Cartão" **sempre**: "Titular ••••
4021", "Adic. •••• 4022", "Virtual •••• 4023"; sem cartão gravado, o final da seção da fatura com
que o lançamento foi conciliado (`invoiceCardLastFour`, em itálico, só conciliação aceita) ou
"Não informado". Nos lançamentos da conta ela só aparece quando algum lançamento diz qual foi. A
caixa Filtros da tela do cartão ganha "Cartão" (`issuedCardId`), com os cartões do grupo.

Preenchido também pela conciliação, só quando o lançamento não tinha: aceitar ou "Lançar" o item
da fatura leva o cartão da seção; aceitar a compra no débito do extrato leva o cartão de débito da
conta **se** a descrição trouxer a bandeira ("DEB MC" = Mastercard) e houver exatamente um cartão
ativo dela com débito na conta (`CardBrands.PickDebitCard`). Com dois, não chuta.

Cartão cadastrado depois da fatura (ou com o final corrigido, ou na substituição): os itens do
grupo com aquele final que estavam sem cartão passam a apontar para ele, e os lançamentos
conciliados (aceitos) com esses itens também — só os que não tinham cartão. Sugestão não aceita e
itens de outro grupo não são tocados.
