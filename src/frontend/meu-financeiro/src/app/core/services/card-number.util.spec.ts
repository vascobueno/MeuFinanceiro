import { formatExpiry, maskedNumber, onlyDigits, parseExpiry, splitCardNumber } from './card-number.util';
import { sugerirBandeiraPeloNome } from '../data/bandeiras';

describe('splitCardNumber', () => {
  it('fica com os 6 primeiros e os 4 últimos e descarta o meio', () => {
    expect(splitCardNumber('4532 0151 1283 0366')).toEqual({ firstDigits: '453201', lastFour: '0366' });
    expect(splitCardNumber('4532-0151-1283-0366')).toEqual({ firstDigits: '453201', lastFour: '0366' });
  });

  it('não trata como número completo o que não tem de 13 a 19 dígitos', () => {
    expect(splitCardNumber('4532')).toBeNull();
    expect(splitCardNumber('453201')).toBeNull();
    expect(splitCardNumber('12345678901234567890')).toBeNull();
  });
});

describe('onlyDigits', () => {
  it('corta no tamanho do campo', () => {
    expect(onlyDigits('45-32 01 9', 6)).toBe('453201');
  });
});

describe('parseExpiry', () => {
  it('aceita MM/AA, MM/AAAA e MMAA', () => {
    expect(parseExpiry('12/30')).toEqual({ month: 12, year: 2030 });
    expect(parseExpiry('1/2031')).toEqual({ month: 1, year: 2031 });
    expect(parseExpiry('0429')).toEqual({ month: 4, year: 2029 });
  });

  it('vazio é sem validade; mês fora de 1 a 12 é inválido', () => {
    expect(parseExpiry('')).toBeNull();
    expect(parseExpiry('13/30')).toBe('invalid');
    expect(parseExpiry('ab')).toBe('invalid');
  });

  it('formata de volta como MM/AA', () => {
    expect(formatExpiry(4, 2029)).toBe('04/29');
    expect(formatExpiry(null, null)).toBe('');
  });
});

describe('maskedNumber', () => {
  it('mostra os primeiros dígitos quando há', () => {
    expect(maskedNumber('4532', '1234')).toBe('4532 •• 1234');
    expect(maskedNumber(null, '1234')).toBe('•••• 1234');
  });
});

describe('sugerirBandeiraPeloNome', () => {
  it('sugere quando o nome cita uma bandeira só', () => {
    expect(sugerirBandeiraPeloNome('Santander Elo Grafite')).toBe('Elo');
    expect(sugerirBandeiraPeloNome('Itaú Visa Infinite')).toBe('Visa');
    expect(sugerirBandeiraPeloNome('Black Mastercard')).toBe('Mastercard');
  });

  it('não sugere sem bandeira no nome, com duas, nem dentro de outra palavra', () => {
    expect(sugerirBandeiraPeloNome('Cartão da loja')).toBeNull();
    expect(sugerirBandeiraPeloNome('Visa ou Master')).toBeNull();
    expect(sugerirBandeiraPeloNome('Belo Horizonte')).toBeNull();
  });
});
