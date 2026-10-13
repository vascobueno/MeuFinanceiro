# 33 — Tecnologias, Infraestrutura e Versões

Inventário de tudo que o projeto realmente usa hoje, com a versão instalada/confirmada no
ambiente de desenvolvimento (`ng version`, `npm list`, `dotnet --version`, `docker compose
config`). Gerado a partir dos manifestos reais do repositório (`package.json`, `*.csproj`,
`docker-compose.yml`, `.github/workflows/ci.yml`) — se este arquivo divergir do código, o
código é a fonte de verdade; avise em vez de presumir que o código está errado.

Alternativas a esta pilha, e por que não foram adotadas: `docs/25-alternativas-tecnologicas.md`.

## Frontend (`src/frontend/meu-financeiro`)

```text
Angular CLI            22.1.6
Angular (core/router/  22.1.4
  forms/common/etc.)
@angular/cdk           22.1.5   (só BreakpointObserver no shell — ver Regra abaixo)
@angular/animations    22.1.4   (necessário pro provideAnimationsAsync() do PrimeNG)
TypeScript             6.0.3
RxJS                   7.8.2
Node.js                24.19.0  (faixa oficial do Angular 22: 22.22.3+, 24.15.0+ ou 26+)
npm                    11.17.0
```

Biblioteca de componentes e estilo (PrimeNG + Tailwind, ver `docs/21-telas.md`):

```text
primeng                 22.1.0
@primeuix/themes        3.0.0
primeicons              8.0.0
tailwindcss             4.3.3
@tailwindcss/postcss    4.3.3
tailwindcss-primeui     0.6.1
postcss                 8.5.26
```

Testes (Karma/Jasmine, via `ng test` — ver `docs/20-testes.md`):

```text
karma                        6.4.4
karma-chrome-launcher        3.2.0
karma-coverage               2.2.1
karma-jasmine                5.1.0
karma-jasmine-html-reporter  2.3.0
jasmine-core                 7.0.2
@types/jasmine               6.0.0
```

## Backend (`src/backend/`)

```text
.NET SDK / TargetFramework   net10.0 (SDK instalado: 10.0.400 — sem global.json, flutua
                              dentro da major 10)
```

Pacotes NuGet por projeto:

| Pacote | Versão | Projeto | Uso |
|---|---|---|---|
| `Microsoft.EntityFrameworkCore` | 10.0.9 | Application | ORM |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.9 | Api, Infrastructure | ferramentas de migration |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Infrastructure | provider PostgreSQL do EF Core |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.9 | Infrastructure | usuários, roles, lockout |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.9 | Api | validação do JWT emitido pelo próprio backend |
| `System.IdentityModel.Tokens.Jwt` | 8.1.2 | Infrastructure | emissão do JWT |
| `AWSSDK.S3` | 4.0.102.4 | Infrastructure | `IFileStorageService`/`S3FileStorageService` — fala protocolo S3, funciona com MinIO (dev/produção) ou qualquer outro serviço S3-compatible trocando só `Storage:ServiceUrl` (Cloudflare R2, Backblaze B2, AWS S3 de verdade) |
| `ClosedXML` | 0.104.2 | Infrastructure | leitura de planilha na importação inicial (`ExcelImportService`) e exportação |
| `Swashbuckle.AspNetCore` | 6.7.3 | Api | Swagger/OpenAPI |
| `Microsoft.ApplicationInsights.AspNetCore` | 2.22.0 | Api | telemetria, opcional (só ativa com `ApplicationInsights:ConnectionString` configurado) |

## Testes de backend (`tests/backend/`)

| Pacote | Versão | Uso |
|---|---|---|
| `xunit` | 2.9.2 | framework de teste |
| `xunit.runner.visualstudio` | 2.8.2 | runner |
| `FluentAssertions` | 6.12.1 | assertions |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | SDK de teste do .NET |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.9 | `CustomWebApplicationFactory` (testes de integração via `WebApplicationFactory`) |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.9 | banco em memória nos testes de integração (sem suporte a transação real) |

## Banco de dados

```text
PostgreSQL   18 (imagem Docker "postgres:18", tag flutuante dentro da major 18)
```

Desenvolvimento: container local via `docker-compose.yml`. Produção: PostgreSQL gerenciado
(qualquer provedor) ou self-hosted num container — sem exigência de um provedor específico
(`docs/19-deploy.md`, `docs/15-database.md`).

## Infraestrutura e serviços externos

| Item | Uso | Ambiente |
|---|---|---|
| Docker / Docker Compose | Postgres + MinIO locais (`docker-compose.yml`) | só dev — produção não usa Docker local nem o MinIO local |
| MinIO `RELEASE.2025-09-07T16-13-09Z` (`minio/minio`) | storage S3-compatible — mesmo protocolo/SDK usado em produção (só troca o endpoint) | dev; também viável como opção self-hosted em produção |
| Serviço S3-compatible (MinIO self-hosted, Cloudflare R2, Backblaze B2, AWS S3 etc.) | armazenamento de arquivo real (arquivo original de cada importação Excel) | produção |
| Cloudflare Turnstile | CAPTCHA no cadastro/recuperação de senha, opcional (`Turnstile:SecretKey`) | dev opcional, recomendado em produção |
| SMTP (provedor a escolher — Gmail com senha de app, SendGrid, Mailgun, Amazon SES etc.) | envio de e-mail (confirmação de cadastro, reset de senha, senha temporária) | opcional em dev (loga o link), obrigatório em qualquer ambiente com usuários reais |
| Application Insights | telemetria/logs, opcional | qualquer ambiente, se configurado |
| Hospedagem (VPS, PaaS tipo Railway/Render/Fly.io, ou qualquer container) | compute do backend + build estático do frontend atrás de reverse proxy | produção — **sem dependência de Azure especificamente** (`docs/19-deploy.md`) |

## CI/CD

```text
GitHub Actions        (.github/workflows/ci.yml — build + testes, sem deploy)
actions/checkout      v4
actions/setup-dotnet  v4  (dotnet-version: '10.x')
actions/setup-node    v4  (node-version: '22')
Dependabot            .github/dependabot.yml — nuget (backend) + npm (frontend, semanal)
```

## Regra de atualização

Não alterar essas versões como consequência automática de outras mudanças.

`@angular/cdk` (só `BreakpointObserver` no shell) deve acompanhar a major do Angular.
PrimeNG/PrimeIcons/Tailwind e os pacotes NuGet do backend têm cada um seu próprio ciclo de
versão, independente do Angular — atualizar um não implica atualizar os outros.

Antes de qualquer update:

```bash
ng version
npm list @angular/core @angular/cdk primeng
dotnet list src/backend/MeuFinanceiro.Api package
```

Se houver incompatibilidade, parar e analisar antes de atualizar.

## Nota sobre Node

O ambiente atual está usando Node 24.19.0. Não alterar automaticamente. Caso o build ou uma
dependência apresente incompatibilidade, registrar o erro e avaliar a matriz oficial de
compatibilidade antes de mudar Node.
