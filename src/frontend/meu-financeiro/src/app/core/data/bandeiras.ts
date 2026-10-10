/**
 * Bandeiras de cartão — o valor é o nome do enum CardBrand do backend (gravado pelo nome).
 * As de débito (Maestro, Visa Electron, Elo Débito) não valem para grupo de cartões de crédito.
 */
export interface Bandeira {
  value: string;
  label: string;
}

export const BANDEIRAS_CREDITO: readonly Bandeira[] = [
  { value: 'Visa', label: 'Visa' },
  { value: 'Mastercard', label: 'Mastercard' },
  { value: 'Elo', label: 'Elo' },
  { value: 'AmericanExpress', label: 'American Express' },
  { value: 'Hipercard', label: 'Hipercard' },
  { value: 'DinersClub', label: 'Diners Club' },
  { value: 'Jcb', label: 'JCB' },
  { value: 'Discover', label: 'Discover' },
  { value: 'UnionPay', label: 'UnionPay' },
  { value: 'Cabal', label: 'Cabal' },
  { value: 'Outra', label: 'Outra' },
];

export const BANDEIRAS_DEBITO: readonly Bandeira[] = [
  { value: 'Maestro', label: 'Maestro' },
  { value: 'VisaElectron', label: 'Visa Electron' },
  { value: 'EloDebito', label: 'Elo Débito' },
];

export function bandeiraLabel(value: string | null | undefined): string {
  if (!value) return '';
  return [...BANDEIRAS_CREDITO, ...BANDEIRAS_DEBITO].find((b) => b.value === value)?.label ?? value;
}

// Palavra inteira: "elo" não pode casar dentro de "Belo Horizonte".
const PELO_NOME: readonly [RegExp, string][] = [
  [/\bvisa\b/i, 'Visa'],
  [/\bmaster(card)?\b/i, 'Mastercard'],
  [/\belo\b/i, 'Elo'],
  [/\b(amex|american express)\b/i, 'AmericanExpress'],
  [/\bhiper(card)?\b/i, 'Hipercard'],
  [/\bdiners\b/i, 'DinersClub'],
  [/\bjcb\b/i, 'Jcb'],
  [/\bdiscover\b/i, 'Discover'],
  [/\bunion ?pay\b/i, 'UnionPay'],
  [/\bcabal\b/i, 'Cabal'],
];

/**
 * Bandeira pelo nome que o usuário deu ao cartão ("Santander Elo", "Itaú Visa Infinite") — só
 * quando o nome cita exatamente uma. A sugestão pelos primeiros dígitos é do backend.
 */
export function sugerirBandeiraPeloNome(nome: string | null | undefined): string | null {
  if (!nome) return null;
  const achadas = PELO_NOME.filter(([re]) => re.test(nome)).map(([, v]) => v);
  return achadas.length === 1 ? achadas[0] : null;
}
