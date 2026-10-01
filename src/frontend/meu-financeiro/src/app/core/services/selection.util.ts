/**
 * Qual conta ou cartão fica ativo numa faixa de abas (docs/06).
 *
 * A escolha vive na URL (`?conta=`, `?cartao=`), porque é dali que o formulário de novo
 * lançamento recebe a origem e é o que faz a aba sobreviver a um F5. Só que a URL é editável à
 * mão, sobrevive à exclusão da conta e pode ter vindo de outro grupo.
 *
 * Daí a separação entre as duas funções: `validId` responde "este id existe?" e **nunca chuta
 * outro**, enquanto `defaultId` é o palpite explícito de quem pode dar um. As telas usam
 * `validId(...) ?? defaultId(...)`, porque precisam mostrar alguma coisa; o formulário usa
 * `validId` sozinho, e o `null` faz o campo de conta reaparecer — é o que impede um lançamento
 * de ser gravado numa conta diferente da que o cabeçalho anuncia.
 */

/** O id pedido, se existir na lista; senão `null`. */
export function validId<T extends { id: string }>(
  items: readonly T[],
  requested: string | null | undefined,
): string | null {
  if (!requested) return null;
  return items.some((item) => item.id === requested) ? requested : null;
}

/** Primeiro item ativo — ou o primeiro de todos, se nenhum estiver ativo. */
export function defaultId<T extends { id: string; isActive: boolean }>(items: readonly T[]): string | null {
  return items.find((item) => item.isActive)?.id ?? items[0]?.id ?? null;
}
