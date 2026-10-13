# 20 — Estratégia de Testes

## Unitários
Backend: `tests/backend/MeuFinanceiro.Domain.UnitTests` (hoje: `InstallmentCalculatorTests`,
`ImportRowValidatorTests`).
Frontend: arquivos `*.spec.ts` ao lado do componente, rodados via `ng test` (Karma/Jasmine) — não
há um projeto `tests/frontend` separado; hoje só `money-input` e `pagination` têm spec próprio.

Alvo de cobertura (ainda incompleto): parcelamento, recorrência, conciliação, projeção,
investimentos, transferências, validators e autocomplete.

## Integração
Backend: `tests/backend/MeuFinanceiro.Application.IntegrationTests` — a API inteira (HTTP ->
Controllers -> Application -> Infrastructure -> EF Core) contra **PostgreSQL 18 de verdade**,
num container que o próprio `dotnet test` sobe e remove (Testcontainers). Não há banco em
memória: o provider é o mesmo da produção (Npgsql), só a connection string muda.

Como funciona (`PostgresTestDatabase` e `CustomWebApplicationFactory`):

- **Um container por execução** da suíte (`postgres:18`, a imagem do `docker-compose.yml`), numa
  porta aleatória; o Ryuk do Testcontainers o remove no fim, até se o processo morrer. O banco de
  desenvolvimento (`meufinanceiro-postgres`) não é tocado.
- **Banco modelo:** as migrations rodam uma vez, em `mf_template`. Migration quebrada derruba a
  suíte logo na subida, com a mensagem do PostgreSQL.
- **Um banco por classe de teste** (`IClassFixture<CustomWebApplicationFactory>`), cópia do modelo
  por `CREATE DATABASE ... TEMPLATE` (milissegundos), apagado no fim da classe. As classes rodam
  em paralelo; dentro de uma classe, cada teste cria o próprio usuário, então não há limpeza
  entre testes.
- **Trava:** a factory recusa subir sem o banco da classe — sem ela, a API cairia na connection
  string do `appsettings.Development.json` local, o banco de desenvolvimento.
- **Sem Docker**, a suíte falha logo, com a mensagem "Docker não encontrado ou parado" (docs/23).

O que isso testa e o banco em memória de antes ignorava: índices únicos (o final de cartão
ativo, os fechamentos mensais, o hash de extrato e fatura), chaves estrangeiras `Restrict`, check
constraints (`CK_IssuedCards_Funcao`), transações, arredondamento de `numeric(18,2)`/`(18,6)` e a
tradução das consultas LINQ para SQL. `PostgresConstraintTests` prova os pontos que nenhum
teste de API alcança: a transação do cadastro, o índice do final, a check constraint e a disputa
por um fechamento mensal (forçada por um interceptor, para não depender de sorte).

Tempo: ~40 s para os 338 casos na máquina de desenvolvimento.

E-mail e anexos continuam com fakes em memória (`TestEmailSender`, `InMemoryFileStorage`) — o
que se quer provar ali é o fluxo, não o SMTP nem o MinIO.

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
