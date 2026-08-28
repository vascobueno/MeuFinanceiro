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
