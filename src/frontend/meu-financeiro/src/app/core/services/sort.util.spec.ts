import { nextSort } from './sort.util';

/**
 * Regressão do clique que não invertia: a tabela é recriada a cada carga, então o `order` que o
 * p-table emite é sempre 1 e a direção precisa sair daqui. Se alguém voltar a confiar no evento,
 * o caso "mesmo campo, ascendente" falha.
 */
describe('sort.util', () => {
  it('deve inverter para desc ao clicar de novo na coluna já ascendente', () => {
    expect(nextSort({ sortBy: 'valor', sortDir: 'asc' }, 'valor', 'data')).toEqual({ sortBy: 'valor', sortDir: 'desc' });
  });

  it('deve voltar para asc ao clicar na coluna já descendente', () => {
    expect(nextSort({ sortBy: 'valor', sortDir: 'desc' }, 'valor', 'data')).toEqual({ sortBy: 'valor', sortDir: 'asc' });
  });

  it('deve começar ascendente ao trocar de coluna', () => {
    expect(nextSort({ sortBy: 'valor', sortDir: 'asc' }, 'descricao', 'data')).toEqual({ sortBy: 'descricao', sortDir: 'asc' });
  });

  it('deve usar o campo padrão quando o evento não traz campo', () => {
    expect(nextSort({ sortBy: 'valor', sortDir: 'asc' }, undefined, 'data')).toEqual({ sortBy: 'data', sortDir: 'asc' });
    expect(nextSort({ sortBy: 'data', sortDir: 'asc' }, null, 'data')).toEqual({ sortBy: 'data', sortDir: 'desc' });
  });

  it('deve funcionar a partir de um estado vazio', () => {
    expect(nextSort({}, 'conta', 'periodo')).toEqual({ sortBy: 'conta', sortDir: 'asc' });
  });
});
