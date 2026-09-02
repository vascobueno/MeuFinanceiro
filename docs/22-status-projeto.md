# 22 — Status do Projeto

## Fundação
- [x] Repositório
- [x] Solution .NET
- [x] Angular
- [x] PostgreSQL
- [x] CI (GitHub Actions — build/test do backend e do frontend)

## Segurança
- [x] Identidade (JWT — registro/login/refresh/logout, login por nome de usuário)
- [x] Autorização (`[Authorize]` + escopo por grupo em todos os endpoints)
- [x] Perfis de acesso por membro (Completo/Editor/Visualizador — `WriteAccessFilter` +
      `fullAccessGuard`/`canWriteGuard`)
- [ ] Key Vault
- [ ] OIDC (GitHub -> Azure)
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
- [ ] DEV (Azure)
- [ ] STAGING
- [ ] PROD
- [ ] Backup
- [ ] Monitoring

Infra Azure (DEV/STAGING/PROD/Backup/Monitoring/Key Vault/OIDC) ainda não existe — o projeto
roda hoje só em ambiente local (PostgreSQL local via Docker, `dotnet run` + `ng serve`). Tudo em
Fundação/Segurança (exceto Key Vault/OIDC)/Grupo/Administração/Financeiro/Conciliação/
Investimentos/Relatórios/Interface está implementado e coberto pelas telas descritas em
`docs/21-telas.md`.
