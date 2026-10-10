import { brlFromForeign, formatForeign } from './foreign.util';

describe('formatForeign', () => {
  it('mostra valor, cotação e moeda de origem', () => {
    expect(formatForeign({ amount: 21.22, currency: 'USD', exchangeRate: 5.497, originalAmount: 19.5, originalCurrency: 'EUR' }))
      .toBe('US$ 21,22 × 5,4970 · origem EUR 19,50');
  });

  it('marca a cotação calculada e aceita moeda sem símbolo', () => {
    expect(formatForeign({ amount: 1000, currency: 'ARS', exchangeRate: 0.0061 }, true)).toBe('ARS 1.000,00 × 0,0061 (calculada)');
  });

  it('sem dados, nada', () => {
    expect(formatForeign(null)).toBe('');
  });
});

describe('brlFromForeign', () => {
  it('converte e arredonda nos centavos', () => {
    expect(brlFromForeign(21.22, 5.497)).toBe(116.65);
    expect(brlFromForeign(null, 5)).toBeNull();
  });
});
