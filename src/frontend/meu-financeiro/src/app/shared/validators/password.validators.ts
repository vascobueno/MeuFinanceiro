import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

/**
 * Mesma política de senha do backend (Identity): mínimo 8 caracteres, com maiúscula,
 * minúscula, número e símbolo — validar aqui evita ir e voltar pra ver um erro em inglês
 * vindo direto do Identity (não localizado nesta versão). Compartilhado entre o cadastro
 * (register.component.ts) e a redefinição de senha (reset-password.component.ts).
 */
export const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$/;

export const passwordValidators: ValidatorFn[] = [Validators.required, Validators.pattern(PASSWORD_PATTERN)];

export function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password && confirmPassword && password !== confirmPassword ? { passwordMismatch: true } : null;
}
