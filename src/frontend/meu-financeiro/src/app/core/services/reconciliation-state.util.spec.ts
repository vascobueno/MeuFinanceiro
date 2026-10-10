import { isSuggestion, reconciliationCounts, reconciliationLabel, reconciliationState } from './reconciliation-state.util';

describe('reconciliationState', () => {
  it('sugestão da importação não é conciliado até aceitar', () => {
    expect(reconciliationState({ status: 'Matched', accepted: false })).toBe('sugerido');
    expect(reconciliationLabel({ status: 'Matched', accepted: false })).toBe('Sugerido');
    expect(reconciliationState({ status: 'Matched', accepted: true })).toBe('aceito');
    expect(reconciliationLabel({ status: 'Matched', accepted: true })).toBe('Conciliado');
  });

  it('vínculo manual é sempre aceito', () => {
    expect(reconciliationState({ status: 'ManuallyMatched' })).toBe('aceito');
  });

  it('possível e sugerido são sugestões; ignorado e sem par, não', () => {
    expect(isSuggestion({ status: 'PossibleMatch' })).toBeTrue();
    expect(isSuggestion({ status: 'Matched', accepted: false })).toBeTrue();
    expect(isSuggestion({ status: 'Ignored' })).toBeFalse();
    expect(isSuggestion({ status: 'Unmatched' })).toBeFalse();
  });
});

describe('reconciliationCounts', () => {
  it('conta como conciliado só o aceito e deixa o ignorado fora do percentual', () => {
    const c = reconciliationCounts([
      { status: 'Matched', accepted: true },
      { status: 'Matched', accepted: false },
      { status: 'PossibleMatch' },
      { status: 'Unmatched' },
      { status: 'Ignored' },
    ]);
    expect(c).toEqual({ accepted: 1, toReview: 2, unmatched: 1, percent: 25 });
  });
});
