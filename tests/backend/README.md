# Testes Backend

## Unit
`MeuFinanceiro.Domain.UnitTests`

Testam regras isoladas sem infraestrutura externa.

## Integration
`MeuFinanceiro.Application.IntegrationTests`

Testam API + Application + Infrastructure + PostgreSQL.

## Regra
Teste de integração não deve usar o banco de produção nem dados financeiros reais.
