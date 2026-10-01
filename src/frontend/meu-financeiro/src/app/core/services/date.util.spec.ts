import { parseIsoDate, shiftMonthIso, startOfMonthIso, toIsoDate } from './date.util';

/**
 * Regressão da navegação entre ciclos de fatura.
 *
 * O bug: `new Date('2026-10-01')` é meia-noite UTC, que em fuso negativo vira 30/09 local. Num
 * cartão que fecha no dia 1, isso fazia o "avançar" não sair do lugar e o "voltar" pular dois
 * meses. Os testes abaixo falham se alguém voltar a usar `new Date(string)`.
 */
describe('date.util', () => {
  describe('parseIsoDate', () => {
    it('deve preservar o dia 1, que é onde o parse UTC quebrava', () => {
      const date = parseIsoDate('2026-10-01');

      expect(date.getFullYear()).toBe(2026);
      expect(date.getMonth()).toBe(9); // outubro, 0-based
      expect(date.getDate()).toBe(1);
    });

    it('deve preservar o dia em qualquer posição do mês', () => {
      for (const dia of [1, 2, 15, 28, 30, 31]) {
        const iso = `2026-10-${String(dia).padStart(2, '0')}`;
        expect(parseIsoDate(iso).getDate()).toBe(dia);
      }
    });

    it('deve aceitar ISO com hora, usando só a parte da data', () => {
      expect(parseIsoDate('2026-10-01T00:00:00').getDate()).toBe(1);
    });

    it('deve dar a volta completa por toIsoDate', () => {
      for (const iso of ['2026-01-01', '2026-02-28', '2026-12-31', '2028-02-29']) {
        expect(toIsoDate(parseIsoDate(iso))).toBe(iso);
      }
    });
  });

  describe('startOfMonthIso', () => {
    it('deve devolver o primeiro dia do mês', () => {
      expect(startOfMonthIso('2026-10-31')).toBe('2026-10-01');
      expect(startOfMonthIso('2026-10-01')).toBe('2026-10-01');
      expect(startOfMonthIso('2026-02-28')).toBe('2026-02-01');
    });
  });

  describe('shiftMonthIso', () => {
    it('deve avançar um mês a partir do dia 1 — o caso que não saía do lugar', () => {
      expect(shiftMonthIso('2026-10-01', 1)).toBe('2026-11-01');
    });

    it('deve voltar um mês a partir do dia 1 — o caso que pulava dois', () => {
      expect(shiftMonthIso('2026-10-01', -1)).toBe('2026-09-01');
    });

    it('deve andar nos dois sentidos em todos os meses do ano', () => {
      for (let mes = 1; mes <= 12; mes++) {
        const atual = `2026-${String(mes).padStart(2, '0')}-01`;
        const frente = shiftMonthIso(atual, 1);
        const tras = shiftMonthIso(atual, -1);

        expect(frente > atual).withContext(`avançar a partir de ${atual}`).toBe(true);
        expect(tras < atual).withContext(`voltar a partir de ${atual}`).toBe(true);
        expect(shiftMonthIso(frente, -1)).withContext(`ida e volta em ${atual}`).toBe(atual);
      }
    });

    it('deve cruzar a virada do ano nos dois sentidos', () => {
      expect(shiftMonthIso('2026-12-01', 1)).toBe('2027-01-01');
      expect(shiftMonthIso('2026-01-01', -1)).toBe('2025-12-01');
    });

    it('doze passos para frente e doze para trás voltam ao mês de origem', () => {
      let iso = '2026-01-01';
      for (let i = 0; i < 12; i++) iso = shiftMonthIso(iso, 1);
      expect(iso).toBe('2027-01-01');

      for (let i = 0; i < 12; i++) iso = shiftMonthIso(iso, -1);
      expect(iso).toBe('2026-01-01');
    });

    it('deve partir de qualquer dia do mês, não só do dia 1', () => {
      expect(shiftMonthIso('2026-10-31', 1)).toBe('2026-11-01');
      expect(shiftMonthIso('2026-03-15', -1)).toBe('2026-02-01');
    });
  });
});
