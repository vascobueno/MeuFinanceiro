# 17 — Auditoria

AuditLog:
- UserId
- HouseholdId (household do dado alterado, ou do usuário que logou; null quando não se aplica — ex.: login falhou com e-mail inexistente)
- Timestamp
- Action
- EntityName
- EntityId
- OldValues
- NewValues
- IpAddress
- UserAgent
- CorrelationId

Alterações (Create/Update/Delete) são capturadas automaticamente por um override de
`AppDbContext.SaveChangesAsync` — percorre o `ChangeTracker` a cada save e gera uma linha por
entidade adicionada/alterada/removida, sem precisar de chamada manual em cada serviço.
Cobre as 12 entidades de household (lançamentos, recorrências, parcelamentos, contas/cartões,
investimentos, importações, etc.) e qualquer entidade nova por padrão. Exceção:
`CardReconciliationService`/`BankReconciliationService` continuam com chamadas manuais a
`IAuditService.LogAsync` pra ações com semântica própria (Accept/Undo) que o override genérico
não capturaria — as duas formas convivem.

Acessos (login) são auditados à parte via `IAuditService.LogAccessAsync`, chamado em
`AuthService.LoginAsync` — grava Action `"Login"` ou `"LoginFailed"` com IP e User-Agent.

Leitura: `GET /api/v1/audit` (filtro por período e Action) — Owner/Member vê só o audit log
do próprio household; Administrator vê de todos (com filtro adicional por household).

Nunca guardar tokens ou senhas na auditoria.
