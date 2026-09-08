# 05 — Regras de Negócio

## Descrição
Autocomplete por `StartsWith(prefix)`. Priorizar frequência de uso e uso recente.

## Recorrência
Alteração a partir de uma data cria nova versão. Ocorrências históricas e conciliadas não mudam.

## Parcelamento
Modo A: principal + quantidade.
Modo B: valor da parcela + quantidade.
A soma deve fechar exatamente o principal.

## Transferência
Entre contas próprias não é receita nem despesa.

## Investimentos
Aplicação reduz conta e aumenta investimento.
Resgate aumenta conta e reduz investimento.
Rendimento é evento separado.

## Saldos
Saldo de conta não inclui investimentos.
Patrimônio consolidado pode somar os dois somente para apresentação.

## Projeção
Pode considerar lançamentos futuros, recorrências, parcelas, faturas, aplicações, resgates e rendimentos quando confiáveis. Nunca inventar rentabilidade.
