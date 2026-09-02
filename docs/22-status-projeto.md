# 22 — Status do Projeto

## Fundação
- [x] Repositório
- [x] Solution .NET
- [x] Angular
- [x] PostgreSQL
- [x] CI (GitHub Actions — build/test do backend e do frontend)

## Segurança
- [x] Identidade (JWT — registro/login/refresh/logout, login por nome de usuário, sessão via
      cookie httpOnly + CSRF double-submit)
- [x] Autorização (`[Authorize]` + escopo por grupo em todos os endpoints)
- [x] Perfis de acesso por membro (Completo/Editor/Visualizador — `WriteAccessFilter` +
      `fullAccessGuard`/`canWriteGuard`)
- [x] Bloqueio de conta por tentativas de senha errada (lockout)
- [x] Rate limiting dedicado pros endpoints de auth
- [x] Validação obrigatória de chave JWT no startup (nunca aceita fallback)
- [x] Cabeçalhos de segurança + HSTS
- [x] Confirmação de e-mail obrigatória antes do login
- [x] CAPTCHA (Cloudflare Turnstile) no cadastro e recuperação de senha
- [x] Scan de dependências vulneráveis no CI + Dependabot
- [ ] Segredos via variável de ambiente em produção (mecanismo já pronto — `Jwt__Key` etc.,
      docs/19-deploy.md — falta só o deploy de verdade acontecer)
- [x] Auditoria (`AuditService`/`AuditLog`, automática via `SaveChangesAsync` + manual nas
      conciliações + acessos de login)

## Grupo / Multi-usuário
- [x] Grupo compartilhado (Owner/Member, dados em comum)
- [x] Convite por e-mail (só se o grupo do convidado estiver vazio)
- [x] Criação direta de usuário pelo Owner, com senha temporária por e-mail e troca
      obrigatória no primeiro login (`/trocar-senha-inicial`)
- [x] Remoção de membro (nunca fica sem grupo — ganha um novo, vazio)
- [x] Tela própria de gestão de membros (`/membros`)
- [x] Perfis de acesso por membro (Completo/Editor/Visualizador)

## Administração
- [x] Role de sistema Administrator (seed por configuração, sem auto-promoção)
- [x] Tela de grupos (`/admin/grupos`)
- [x] Tela cruzada de perfis de acesso (`/admin/perfis-acesso`)

## Relatórios
- [x] Projeção de receitas (`/relatorios/projecao-receitas`, imprimível/PDF via navegador)
- [x] Gastos recorrentes (`/relatorios/gastos-recorrentes`, imprimível/PDF via navegador)
- [x] Auditoria (`/relatorios/auditoria`, filtrável por período e tipo)

## Interface
- [x] 7 skins de cores (seleção em Configurações, persistida no navegador)
- [x] Ordenação por coluna nas grids (server-side em Lançamentos/Faturas/Contas/Cartões,
      client-side via `MatTableDataSource` nas listas menores e nas telas de Admin)

## Financeiro
- [x] Contas
- [x] Cartões
- [x] Categorias
- [x] Lançamentos
- [x] Autocomplete
- [x] Recorrências (lançamento + tela de listagem com prévia das próximas ocorrências)
- [x] Parcelamentos

## Conciliação
- [x] Parser extrato
- [x] Conciliação bancária
- [x] Parser fatura
- [x] Conciliação cartão

## Investimentos
- [x] Importação
- [x] Saldo separado
- [x] Projeção

## Infra
- [ ] DEV
- [ ] STAGING
- [ ] PROD
- [ ] Backup
- [ ] Monitoring

Infra de produção (DEV/STAGING/PROD/Backup/Monitoring, ver docs/19-deploy.md) ainda não
existe — o projeto roda hoje só em ambiente local (PostgreSQL local via Docker, `dotnet run`
+ `ng serve`). Tudo em Fundação/Segurança/Grupo/Administração/Financeiro/Conciliação/
Investimentos/Relatórios/Interface está implementado e coberto pelas telas descritas em
`docs/21-telas.md`.
