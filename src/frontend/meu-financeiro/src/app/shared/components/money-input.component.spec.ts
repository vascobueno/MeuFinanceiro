import { ChangeDetectorRef } from '@angular/core';
import { MoneyInputComponent } from './money-input.component';

describe('MoneyInputComponent', () => {
  let component: MoneyInputComponent;
  let markForCheckSpy: jasmine.Spy;

  beforeEach(() => {
    // Stub mínimo de ChangeDetectorRef — este teste instancia a classe diretamente (sem
    // TestBed), então só precisamos garantir que markForCheck() não quebre e possa ser
    // espionado (writeValue() agora chama cdr.markForCheck() por conta do OnPush real do
    // Angular 22 — ver comentário na implementação).
    const cdrStub = { markForCheck: () => undefined } as unknown as ChangeDetectorRef;
    markForCheckSpy = spyOn(cdrStub, 'markForCheck');
    component = new MoneyInputComponent(cdrStub);
  });

  it('deve formatar um valor numérico com vírgula decimal ao escrever', () => {
    component.writeValue(1234.5);
    expect(component.displayValue).toBe('1234,50');
  });

  it('deve marcar a view para verificação ao escrever um valor programaticamente', () => {
    component.writeValue(10);
    expect(markForCheckSpy).toHaveBeenCalled();
  });

  it('deve converter texto com vírgula para number ao perder o foco', () => {
    let emitted: number | null = null;
    component.registerOnChange((value) => (emitted = value));

    component.displayValue = '1.234,56';
    component.commit();

    expect(emitted as number | null).toBe(1234.56);
  });

  it('deve emitir null quando o campo é limpo', () => {
    let emitted: number | null = 0;
    component.registerOnChange((value) => (emitted = value));

    component.displayValue = '';
    component.commit();

    expect(emitted).toBeNull();
  });
});
