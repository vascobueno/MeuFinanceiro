# 12 — Segurança

- HTTPS.
- OIDC/JWT conforme provedor escolhido.
- Ownership por **household**, não por usuário individual: todo dado financeiro pertence a um
  `HouseholdId`, compartilhado entre os membros desse household. `HouseholdId`/`HouseholdRole`/
  `HouseholdAccessProfile` são sempre derivados do JWT via `ICurrentUserService`, nunca
  confiados do cliente.
- `HouseholdRole` (Owner/Member): só o Owner gerencia membros do household (adicionar/remover/
  mudar perfil de acesso) — checado em `HouseholdsController.EnsureOwner()`.
- `HouseholdAccessProfile` (Completo/Editor/Visualizador) controla, dentro do household:
  visibilidade das páginas de gestão (Configurações/Membros — só Completo/Owner vêem,
  `fullAccessGuard` no frontend + checagem em `HouseholdsController.GetMine()`) e permissão de
  escrita (Visualizador é somente-leitura — bloqueado centralmente pelo `WriteAccessFilter`,
  um `IAsyncActionFilter` global no backend, e escondido no frontend via `AuthService.canWrite()`/
  `canWriteGuard`). Owner é sempre Completo — nunca pode ser rebaixado (evita autotravamento).
- Role de sistema `Administrator` (ASP.NET Identity role, `[Authorize(Roles="Administrator")]`)
  é ortogonal ao household pessoal do administrador — administra todos os households via
  `AdminController` (`adminGuard` no frontend), semeada só por configuração
  (`AdminSeed:Email`), sem auto-promoção.
- Key Vault.
- Managed Identity.
- GitHub Actions com OIDC.
- Blob Storage privado.
- Uploads validados.
- Auditoria.
- Rate limiting quando aplicável.
- EF Core/queries parametrizadas.
- Não registrar senha, tokens, connection strings ou conteúdo integral de documentos.
- Menor privilégio.
