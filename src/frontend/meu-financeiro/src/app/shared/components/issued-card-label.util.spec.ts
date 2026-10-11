import { issuedCardCell } from './issued-card-label.util';

describe('issuedCardCell', () => {
  it('cartão cadastrado mostra o tipo e o final', () => {
    expect(issuedCardCell({ issuedCardLastFour: '4021', issuedCardKind: 1 }).text).toBe('Titular •••• 4021');
    expect(issuedCardCell({ issuedCardLastFour: '4022', issuedCardKind: 2 }).text).toBe('Adic. •••• 4022');
    expect(issuedCardCell({ issuedCardLastFour: '4023', issuedCardKind: 3 }).text).toBe('Virtual •••• 4023');
  });

  it('o cadastrado vale mais que o final da fatura', () => {
    const c = issuedCardCell({ issuedCardLastFour: '4021', issuedCardKind: 1, invoiceCardLastFour: '4031' });
    expect(c.source).toBe('cadastrado');
  });

  it('sem cadastro, o final da fatura com a dica', () => {
    const c = issuedCardCell({ invoiceCardLastFour: '4031' });
    expect(c.text).toBe('•••• 4031');
    expect(c.source).toBe('fatura');
    expect(c.hint).toContain('fatura');
  });

  it('sem nada, não informado', () => {
    expect(issuedCardCell({}).source).toBe('nenhum');
    expect(issuedCardCell({}).text).toBe('Não informado');
  });
});
