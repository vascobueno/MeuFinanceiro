import { statementItemTypeLabel } from './statement-item-type.util';

describe('statementItemTypeLabel', () => {
  it('traduz os tipos conhecidos', () => {
    expect(statementItemTypeLabel('PIX_SENT')).toBe('Pix enviado');
    expect(statementItemTypeLabel('BILL_PAYMENT')).toBe('Fatura de cartão');
  });

  it('mostra como veio um tipo que a tela ainda não conhece', () => {
    expect(statementItemTypeLabel('NOVO_TIPO')).toBe('NOVO_TIPO');
  });

  it('vazio fica vazio', () => {
    expect(statementItemTypeLabel(undefined)).toBe('');
  });
});
