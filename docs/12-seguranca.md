# 12 — Segurança

- HTTPS.
- OIDC/JWT conforme provedor escolhido.
- Ownership por **grupo**, não por usuário individual: todo dado financeiro pertence a um
  `GrupoId`, compartilhado entre os membros desse grupo. `GrupoId`/`GrupoRole`/
  `GrupoAccessProfile` são sempre derivados do JWT via `ICurrentUserService`, nunca
  confiados do cliente.
- `GrupoRole` (Owner/Member): só o Owner gerencia membros do grupo (adicionar/remover/
  mudar perfil de acesso) — checado em `GruposController.EnsureOwner()`.
- `GrupoAccessProfile` (Completo/Editor/Visualizador) controla, dentro do grupo:
  visibilidade das páginas de gestão (Configurações/Membros — só Completo/Owner vêem,
  `fullAccessGuard` no frontend + checagem em `GruposController.GetMine()`) e permissão de
  escrita (Visualizador é somente-leitura — bloqueado centralmente pelo `WriteAccessFilter`,
  um `IAsyncActionFilter` global no backend, e escondido no frontend via `AuthService.canWrite()`/
  `canWriteGuard`). Owner é sempre Completo — nunca pode ser rebaixado (evita autotravamento).
- Role de sistema `Administrator` (ASP.NET Identity role, `[Authorize(Roles="Administrator")]`)
  é ortogonal ao grupo pessoal do administrador — administra todos os grupos via
  `AdminController` (`adminGuard` no frontend), semeada só por configuração
  (`AdminSeed:Email`), sem auto-promoção.
- Recuperação de senha: token gerado pelo `DataProtectorTokenProvider` padrão do ASP.NET
  Identity (via `SecurityStamp`, sem tabela própria), validade de 1h, uso único. `POST
  /auth/forgot-password` sempre responde 204 e manda a mesma auditoria
  (`ForgotPasswordRequested`) exista ou não o e-mail — nunca revela se uma conta existe.
  Envio por SMTP configurável (`Smtp:*`); sem `Smtp:Host` configurado, o link só é logado
  (nunca exposto na resposta da API).
- Login é por nome de usuário (`UserName`), não e-mail — unicidade garantida pelo
  `UserValidator` padrão do Identity, com checagem prévia amigável em `AuthService.RegisterAsync`.
  O e-mail continua existindo na conta (recuperação de senha), só deixa de ser o identificador
  de entrada.
- Owner cria usuário direto (`POST grupos/me/members/new`), diferente do convite por e-mail
  (`POST grupos/me/members`, que só move quem já tem conta própria): o sistema cria a conta
  inteira com uma senha temporária gerada (`RandomNumberGenerator`, garantindo maiúscula/
  minúscula/dígito/símbolo) e envia por e-mail. `ApplicationUser.MustChangePassword` fica
  `true` até a pessoa trocar a senha em `POST auth/change-password` — o `authGuard` no
  frontend redireciona pra `/trocar-senha-inicial` antes de qualquer outra tela enquanto o
  flag estiver ativo. A troca de senha também confirma o e-mail (`EmailConfirmed = true`),
  usada como prova de que a pessoa recebeu a senha temporária.
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
