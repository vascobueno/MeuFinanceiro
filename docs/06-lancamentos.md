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
