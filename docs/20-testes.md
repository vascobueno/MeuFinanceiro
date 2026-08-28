# 20 — Estratégia de Testes

## Unitários
Backend: `tests/backend/MeuFinanceiro.Domain.UnitTests` (hoje: `InstallmentCalculatorTests`,
`ImportRowValidatorTests`).
Frontend: arquivos `*.spec.ts` ao lado do componente, rodados via `ng test` (Karma/Jasmine) — não
há um projeto `tests/frontend` separado; hoje só `money-input` e `pagination` têm spec próprio.

Alvo de cobertura (ainda incompleto): parcelamento, recorrência, conciliação, projeção,
investimentos, transferências, validators e autocomplete.

## Integração
Backend: `tests/backend/MeuFinanceiro.Application.IntegrationTests` (hoje:
`AccountsEndpointTests`, `InstallmentsRecurringInvoicesEndpointTests`,
`HouseholdsEndpointTests`, `HouseholdAccessProfileTests`, via `CustomWebApplicationFactory`
— EF Core InMemory, sem suporte a transação real).

Backend deve testar API -> Application -> Infrastructure -> EF Core -> PostgreSQL, preferencialmente com PostgreSQL em container.

CI (`.github/workflows/ci.yml`) roda os dois projetos de backend com filtro `Category=Unit`/
`Category=Integration`, e os specs de frontend via `ng test --browsers=ChromeHeadless`
(obrigatório em CI — ver docs/18-ci-cd.md).

## Regras críticas
- 1000/3 = 333,33 + 333,33 + 333,34.
- Alteração de recorrência não muda histórico.
- Conciliação não altera lançamento automaticamente.
- Transferência própria não vira receita/despesa.
- Aplicação reduz conta e aumenta investimento.
- Investimentos permanecem separados.
- `ama` encontra Amazon; `azon` não encontra no modo prefixo.

## Regressão
Todo bug corrigido deve gerar teste.
