# Testes Backend

## Unit
`MeuFinanceiro.Domain.UnitTests`

Testam regras isoladas sem infraestrutura externa.

## Integration
`MeuFinanceiro.Application.IntegrationTests`

Testam API + Application + Infrastructure + PostgreSQL. O PostgreSQL é de verdade, num
container que o próprio `dotnet test` sobe e remove (Testcontainers) — precisa do Docker
aberto. Um banco por classe de teste, cópia de um modelo com as migrations; detalhes em
`docs/20-testes.md`.

## Regra
Teste de integração não deve usar o banco de produção nem dados financeiros reais.
