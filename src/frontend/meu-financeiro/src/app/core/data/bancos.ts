/**
 * Bancos mais comuns, pelo código COMPE de 3 dígitos — o mesmo que sai no extrato, no boleto e no
 * BANKID do OFX. A lista é curta de propósito: banco que não esteja aqui entra por "Outro", com o
 * código e o nome digitados. Nada no backend depende desta lista; ela só preenche o formulário.
 */
export interface Banco {
  code: string;
  name: string;
}

export const BANCOS: readonly Banco[] = [
  { code: '001', name: 'Banco do Brasil' },
  { code: '003', name: 'Banco da Amazônia' },
  { code: '004', name: 'Banco do Nordeste' },
  { code: '021', name: 'Banestes' },
  { code: '033', name: 'Santander' },
  { code: '037', name: 'Banpará' },
  { code: '041', name: 'Banrisul' },
  { code: '047', name: 'Banese' },
  { code: '070', name: 'BRB' },
  { code: '077', name: 'Banco Inter' },
  { code: '102', name: 'XP Investimentos' },
  { code: '104', name: 'Caixa Econômica Federal' },
  { code: '136', name: 'Unicred' },
  { code: '197', name: 'Stone' },
  { code: '208', name: 'BTG Pactual' },
  { code: '212', name: 'Banco Original' },
  { code: '237', name: 'Bradesco' },
  { code: '260', name: 'Nubank' },
  { code: '290', name: 'PagBank' },
  { code: '323', name: 'Mercado Pago' },
  { code: '336', name: 'C6 Bank' },
  { code: '341', name: 'Itaú Unibanco' },
  { code: '380', name: 'PicPay' },
  { code: '389', name: 'Banco Mercantil do Brasil' },
  { code: '403', name: 'Cora' },
  { code: '422', name: 'Banco Safra' },
  { code: '623', name: 'Banco Pan' },
  { code: '655', name: 'Banco BV' },
  { code: '707', name: 'Banco Daycoval' },
  { code: '748', name: 'Sicredi' },
  { code: '756', name: 'Sicoob' },
];

/** Valor do item "Outro" no seletor — não é um código válido, então não colide com nenhum banco. */
export const OUTRO_BANCO = 'outro';

export function bancoPorCodigo(code: string | null | undefined): Banco | undefined {
  return code ? BANCOS.find((b) => b.code === code) : undefined;
}
