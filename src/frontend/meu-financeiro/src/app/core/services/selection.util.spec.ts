import { defaultId, validId } from './selection.util';

/**
 * A aba ativa vem da URL, que não é confiável: id de conta excluída, id de outro grupo, id
 * digitado à mão. Se `validId` "chutasse" um item quando o pedido não existe, o formulário
 * gravaria o lançamento numa conta diferente da que o cabeçalho mostra — erro silencioso, que é
 * o pior tipo. Estes testes falham se alguém trocar o `null` por um palpite.
 */
describe('selection.util', () => {
  const contas = [
    { id: 'a', isActive: false },
    { id: 'b', isActive: true },
    { id: 'c', isActive: true },
  ];

  describe('validId', () => {
    it('deve devolver o id pedido quando ele existe na lista', () => {
      expect(validId(contas, 'c')).toBe('c');
    });

    it('deve devolver null para id que não está na lista, sem chutar outro', () => {
      expect(validId(contas, 'inexistente')).toBeNull();
    });

    it('deve devolver null sem id pedido', () => {
      expect(validId(contas, null)).toBeNull();
      expect(validId(contas, undefined)).toBeNull();
      expect(validId(contas, '')).toBeNull();
    });

    it('deve devolver null quando a lista está vazia', () => {
      expect(validId([], 'a')).toBeNull();
    });
  });

  describe('defaultId', () => {
    it('deve pular os inativos e pegar o primeiro ativo', () => {
      expect(defaultId(contas)).toBe('b');
    });

    it('deve cair no primeiro quando nenhum está ativo', () => {
      expect(defaultId([{ id: 'x', isActive: false }, { id: 'y', isActive: false }])).toBe('x');
    });

    it('deve devolver null com lista vazia', () => {
      expect(defaultId([])).toBeNull();
    });
  });
});
