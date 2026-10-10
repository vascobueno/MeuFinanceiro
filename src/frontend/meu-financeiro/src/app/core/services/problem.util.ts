import { HttpErrorResponse } from '@angular/common/http';

/**
 * Regra de negócio recusada pelo backend: ProblemDetails com `type` terminando no código da regra
 * (ex.: `.../erros/extrato-duplicado`) e a mensagem em português no `title`.
 */
export interface BusinessRuleError {
  rule: string;
  message: string;
}

export function businessRuleOf(error: unknown): BusinessRuleError | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) return null;
  const body = error.error as { type?: string; title?: string } | null;
  if (!body?.type || !body.title) return null;
  const rule = body.type.split('/').pop() ?? '';
  return { rule, message: body.title };
}
