# GitHub Copilot Instructions — MeuFinanceiro

## 1. O que é o projeto

Sistema web de controle financeiro pessoal (substitui um controle antigo em Excel), com suporte
a múltiplos usuários por **grupo** (família/espaço compartilhado). Já está em produção de
uso ativo — não é um projeto novo nem um protótipo.

Stack:
- Frontend: Angular + TypeScript + Angular Material (biblioteca de componentes padrão de toda
  a aplicação — `mat-table`/`mat-sort`/`mat-paginator` para grids, `mat-dialog`, `mat-sidenav`
  + `mat-toolbar` para o shell, Reactive Forms para formulários).
- Backend: ASP.NET Core / C#, Clean Architecture (`Api` → `Application` → `Domain` ←
  `Infrastructure`).
- ORM: Entity Framework Core. Banco: PostgreSQL.
- Autenticação: JWT (access token de 1h + refresh token de 7 dias), emitido pelo próprio
  backend (`AuthService`/`JwtTokenService`) — não é um provedor de identidade externo. Os
  tokens vão em cookie httpOnly (nunca localStorage/JS), com CSRF via double-submit cookie
  (docs/12-seguranca.md).
- Deploy: sem dependência de Azure — reverse proxy + domínio único (frontend + `/api`), ver
  `docs/19-deploy.md`; hoje o ambiente real é só local (Docker Postgres + `dotnet run` +
  `ng serve`), ver `docs/23-configuracao-local.md`.
- CI: GitHub Actions (build + testes de backend e frontend) — ver `docs/18-ci-cd.md`.

### Regra absoluta

**Não recriar o projeto, não trocar Angular/PostgreSQL por outra coisa, não apagar
funcionalidade existente sem pedido explícito, não remover testes existentes.** Trabalhe
incrementalmente sobre o código existente.

## 2. Documentação é a fonte de verdade — leia antes de implementar

Regras de negócio, modelo de dados, API, segurança e status do projeto vivem em `docs/`, não
neste arquivo (evita duas fontes de verdade divergindo com o tempo). Antes de qualquer
funcionalidade nova, leia o que for relevante:

```text
docs/00-visao-geral.md          domínios e arquitetura geral
docs/01-requisitos-funcionais.md lista de RFs
docs/03-arquitetura.md          camadas backend + pastas frontend
docs/04-modelo-dados.md         entidades e índices
docs/05 a docs/11               regras de negócio por área (lançamentos, recorrência,
                                 parcelamento, conciliação, investimentos, projeção)
docs/12-seguranca.md            modelo de autorização (grupo, perfis, administrator)
docs/13-api.md                  endpoints por área
docs/14-angular.md              organização do frontend
docs/17-auditoria.md            auditoria de acessos e alterações
docs/20-testes.md               o que já é testado e como
docs/21-telas.md                todas as rotas/telas existentes
docs/22-status-projeto.md       o que está pronto e o que falta
```

Para uma tela específica, leia também `docs/screens/<tela>/mockup.html` — é a referência
visual/funcional daquela tela (mockup, não é HTML de produção; implementar como componentes
Angular Material reais, preservando hierarquia/campos/ações/estados).

Se a documentação estiver desatualizada em relação ao código, avise em vez de assumir que o
código está errado.

## 3. Modelo de dados/autorização atual (não é dono único por usuário)

Todo dado financeiro pertence a um **grupo** (`GrupoId`, não `UserId` direto — ver
`GrupoOwnedEntity`), compartilhado entre os membros desse grupo. Cada membro tem:
- `GrupoRole`: `Owner` (gerencia membros) ou `Member`.
- `GrupoAccessProfile`: `Completo`, `Editor` ou `Visualizador` — Owner é sempre `Completo`.
- Opcionalmente, a role de sistema `Administrator` (ASP.NET Identity role, ortogonal ao
  grupo — administra todos os grupos, não só o próprio).

`ICurrentUserService` deriva tudo isso do JWT (nunca confiar em `GrupoId`/`UserId` enviado
pelo cliente). Nunca duplicar essa checagem manualmente onde já existe um mecanismo central —
ver `docs/12-seguranca.md` para os guards de rota (`adminGuard`, `fullAccessGuard`,
`canWriteGuard`) e o filtro global de backend (`WriteAccessFilter`).

## 4. Regras que sempre valem, qualquer área

- Valores monetários: sempre `decimal`, nunca `float`/`double`.
- Cartão ≠ conta corrente; investimento nunca é somado automaticamente ao saldo de conta —
  patrimônio consolidado é sempre `contas + investimentos`, mostrado separadamente.
- Alteração de recorrência nunca reescreve histórico — cria nova versão a partir de uma data de
  vigência.
- Parcelamento deve fechar exatamente o valor principal (`decimal`, ajuste de arredondamento na
  última parcela).
- Autocomplete de descrição é por prefixo (`StartsWith`), server-side, nunca carregando todos
  os lançamentos pro browser para filtrar no cliente.
- Listagens de volume potencialmente grande (lançamentos, faturas) usam paginação/filtro/
  ordenação **server-side**; listas pequenas por natureza (contas, cartões, categorias) podem
  carregar tudo de uma vez e ordenar no cliente (`MatTableDataSource`).
- Toda alteração relevante é auditada — ver `docs/17-auditoria.md` (a maioria é automática via
  `AppDbContext.SaveChangesAsync`, não precisa de chamada manual por serviço).
- Nunca commitar segredo (JWT key, connection string, credencial de SMTP/CAPTCHA, senha,
  token) — usar User Secrets localmente, variável de ambiente em produção (docs/19-deploy.md).

## 5. Processo recomendado

Antes de codificar, identifique: o que muda, quais arquivos existentes serão reaproveitados
(não duplicar serviço/componente já existente), qual documentação é relevante, qual teste
cobre isso hoje. Depois de implementar: rodar `dotnet build`/`dotnet test` no backend e
`npm run build`/`npm test -- --browsers=ChromeHeadless` no frontend antes de considerar
pronto. Prefira mudanças pequenas e cirúrgicas — não reescrever arquivos inteiros sem
necessidade, não misturar UI + regra de negócio + schema de banco no mesmo commit quando dá
pra separar. Todo bug corrigido deve gerar um teste que impeça sua volta.

Se uma mudança estrutural grande parecer necessária (trocar biblioteca, arquitetura,
framework), explique o motivo e proponha antes de fazer.
