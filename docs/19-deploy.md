# 19 — Deploy

Sem dependência de Azure — qualquer VPS, PaaS (Railway, Render, Fly.io) ou container
funciona. A única exigência real é a topologia abaixo, por causa dos cookies de sessão.

## Topologia recomendada

```text
                    domínio único (ex.: app.meufinanceiro.com)
                              |
                     reverse proxy (Nginx/Caddy/Traefik)
                     /                              \
        build estático do Angular              /api/* -> backend .NET
```

Frontend e API **atrás de um único domínio**, com o proxy roteando `/api/*` pro backend e
tudo mais pro build estático do Angular — é o que faz os cookies `httpOnly`/`SameSite=Strict`
(docs/12-seguranca.md) funcionarem sem precisar lidar com CORS entre origens diferentes.
Alternativa que também funciona: subdomínios do mesmo domínio-base (`app.meufinanceiro.com` +
`api.meufinanceiro.com`) — `SameSite=Strict` considera subdomínios do mesmo domínio-base como
"mesmo site". Duas origens completamente diferentes (domínios ou até portas diferentes sem
proxy) **não** funcionam pra isso.

## Variáveis de ambiente obrigatórias

Convenção do .NET pra configuração aninhada via variável de ambiente: dois underscores no
lugar de `:` (ex.: `Jwt:Key` vira `Jwt__Key`).

| Variável | O que é |
|---|---|
| `Jwt__Key` | Chave de assinatura JWT, mínimo 32 caracteres (ex.: `openssl rand -base64 48`) — o backend recusa subir sem isso (docs/12-seguranca.md). |
| `ConnectionStrings__DefaultConnection` | Connection string do PostgreSQL de produção. |
| `Cors__AllowedOrigins__0` | Domínio real do frontend (só relevante se front/API não estiverem atrás do mesmo proxy). |
| `App__FrontendBaseUrl` | Base usada nos links de e-mail (confirmação, reset de senha). |
| `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password`, `Smtp__FromEmail` | Provedor de e-mail real — sem isso, ninguém recebe o link de confirmação de cadastro nem consegue se registrar. |
| `Turnstile__SecretKey` | Opcional — sem isso, o CAPTCHA não bloqueia nada. |
| `Storage__ServiceUrl`, `Storage__AccessKey`, `Storage__SecretKey` | Endpoint + credenciais de um serviço S3-compatible real (MinIO self-hosted, Cloudflare R2, Backblaze B2, AWS S3 etc. — `IFileStorageService`/`S3FileStorageService`, docs/03-arquitetura.md) — sem isso, a importação de arquivo Excel falha ao tentar salvar o arquivo original. |
| `AllowedHosts` | Domínio real (ex.: `app.meufinanceiro.com`) — `appsettings.json` comitado tem `"*"`, aceitável só em dev. |

## Banco de dados

PostgreSQL gerenciado (qualquer provedor) ou self-hosted num container — sem exigência de
um provedor específico. Rodar `dotnet ef database update` nas migrations pendentes antes de
subir uma versão nova do backend (nunca aplicar migration automaticamente num pipeline sem
revisão — mesma prática já usada localmente neste projeto).

## Armazenamento de arquivo

Qualquer serviço S3-compatible real (não usar Docker local nem o MinIO de dev) — MinIO
self-hosted no mesmo VPS/container que hospeda o backend, ou um serviço gerenciado (Cloudflare
R2, Backblaze B2, AWS S3 etc.). `S3FileStorageService` (docs/03-arquitetura.md) cria o bucket
automaticamente no primeiro upload se ele ainda não existir.

## Isolamento de rede — MinIO e PostgreSQL

Nem o MinIO nem o PostgreSQL devem ter suas portas (9000/9001 e 5432) acessíveis pela
internet pública, independente do provedor. Autorização por grupo (`GrupoId`) é feita 100%
pela aplicação (`_currentUser.GrupoId`, EF Core, `ForbiddenException` em `ExcelImportService`)
— quem tiver acesso de rede + credencial direto num desses serviços contorna essa checagem
inteira, porque nem o MinIO nem o Postgres sabem o que é "grupo", só a API .NET sabe.

- **Rede**: os dois só precisam ser alcançáveis pelo próprio backend — mesma VPS (bind em
  `127.0.0.1`, nunca `0.0.0.0`) ou rede privada/VPC do provedor, nunca uma porta aberta pro
  público. `docker-compose.yml` expõe `5432`/`9000`/`9001` em todas as interfaces, mas isso é
  só para desenvolvimento local — não replicar esse binding em produção.
- **Credenciais**: nunca reaproveitar as de dev (`minioadmin`/`minioadmin123`,
  `DevPassword!2026`) — gerar credenciais fortes e únicas por ambiente.
- **Menor privilégio**:
  - MinIO: criar uma access key de aplicação com policy restrita ao bucket `imports`, em vez
    de usar as credenciais root (`MINIO_ROOT_USER`/`PASSWORD`) na `Storage:AccessKey`/`SecretKey`
    do backend — um vazamento dessa key fica limitado a um bucket, não à instância inteira.
  - PostgreSQL: a aplicação deve conectar com uma role dedicada (`GRANT` só nas tabelas
    necessárias), não com o superusuário `postgres` usado em dev — um comprometimento da
    aplicação não deveria herdar privilégio de superusuário do banco.
- **TLS em trânsito**: se backend, MinIO e PostgreSQL não estiverem 100% na mesma rede privada
  confiável, usar TLS entre eles (`SSL Mode=Require` na connection string do Npgsql; certificado
  próprio ou proxy TLS na frente do MinIO) — a maioria dos provedores gerenciados (RDS,
  Supabase, Railway Postgres) já força isso por padrão.
- **Criptografia em repouso**: provedores gerenciados costumam oferecer isso já ligado;
  self-hosted precisa de configuração própria (disco criptografado / LUKS, ou SSE do MinIO).
- **Row-Level Security (opcional, defesa em profundidade)**: hoje não existe — o isolamento
  por grupo depende inteiramente do código nunca esquecer o filtro `GrupoId` numa query nova.
  RLS no Postgres reforçaria isso no próprio banco, mas é uma mudança de escopo grande, não
  uma pendência crítica com o modelo atual.

## Pendências antes de produção

RPO/RTO e estratégia de backup do banco ainda precisam ser definidos. `docs/22-status-projeto.md`
mantém o checklist de infra (DEV/STAGING/PROD/Backup/Monitoring) atualizado.
