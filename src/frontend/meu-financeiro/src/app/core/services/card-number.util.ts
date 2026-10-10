/**
 * O número completo do cartão nunca é guardado nem enviado (docs/04). Quem colar o número inteiro
 * num dos campos tem ele dividido aqui: ficam os 6 primeiros (que dizem a bandeira) e os 4 últimos
 * (que a fatura imprime); o meio é descartado antes de chegar a qualquer estado do formulário.
 */
export interface CardNumberParts {
  firstDigits: string;
  lastFour: string;
}

/** Um número de cartão tem de 13 a 19 dígitos; espaços, pontos e traços são ignorados. */
export function splitCardNumber(raw: string | null | undefined): CardNumberParts | null {
  const digits = (raw ?? '').replace(/\D/g, '');
  if (digits.length < 13 || digits.length > 19) return null;
  return { firstDigits: digits.slice(0, 6), lastFour: digits.slice(-4) };
}

/** Só dígitos, cortado no tamanho do campo. */
export function onlyDigits(raw: string | null | undefined, max: number): string {
  return (raw ?? '').replace(/\D/g, '').slice(0, max);
}

/** "12/30", "12/2030", "1230" → { month: 12, year: 2030 }; vazio → null; inválido → 'invalid'. */
export function parseExpiry(raw: string | null | undefined): { month: number; year: number } | null | 'invalid' {
  const s = (raw ?? '').trim();
  if (!s) return null;
  const m = /^(\d{1,2})\s*\/?\s*(\d{2}|\d{4})$/.exec(s);
  if (!m) return 'invalid';
  const month = Number(m[1]);
  let year = Number(m[2]);
  if (year < 100) year += 2000;
  if (month < 1 || month > 12) return 'invalid';
  return { month, year };
}

export function formatExpiry(month: number | null | undefined, year: number | null | undefined): string {
  if (!month || !year) return '';
  return `${String(month).padStart(2, '0')}/${String(year % 100).padStart(2, '0')}`;
}

/** "4532 •• 1234" ou "•••• 1234" — como o cartão aparece na tela. */
export function maskedNumber(firstDigits: string | null | undefined, lastFour: string): string {
  return firstDigits ? `${firstDigits} •• ${lastFour}` : `•••• ${lastFour}`;
}
