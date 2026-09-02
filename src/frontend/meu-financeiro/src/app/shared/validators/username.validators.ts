import { ValidatorFn, Validators } from '@angular/forms';

/** Mesma política do backend (AuthService.UsernamePattern): 3 a 30 caracteres, letras, números, ., _ ou -. */
export const USERNAME_PATTERN = /^[a-zA-Z0-9_.-]{3,30}$/;

export const usernameValidators: ValidatorFn[] = [Validators.required, Validators.pattern(USERNAME_PATTERN)];
