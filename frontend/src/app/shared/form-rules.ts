import { FormControl, ValidatorFn, Validators } from '@angular/forms';

// The questions offered at signup. The server only accepts one of these exact strings.
export const recoveryQuestions = [
  'What is your favorite food?',
  'What was the name of the elementary school you went to?',
  'What is the name of your first pet?',
  'Where was your last vacation?',
];

export const passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$/;

// number street name, City, State/Province, ZIP code
export const addressPattern = /^\d+[A-Za-z]?\s+[^,]+,\s*[^,]+,\s*[^,]+,\s*[A-Za-z0-9][A-Za-z0-9 -]{2,9}$/;

// Latest allowed birth date (yyyy-MM-dd): exactly 18 years ago today. Built from local date parts, not toISOString(), to avoid a UTC day shift.
export const maxBirthDate = (): string => {
  const d = new Date();
  d.setFullYear(d.getFullYear() - 18);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
};

// yyyy-MM-dd strings compare correctly with > and <
export const adultOnly: ValidatorFn = (control) =>
  control.value && control.value > maxBirthDate() ? { underage: true } : null;

// 9 digits, dashes allowed (123-45-6789 or 12-3456789)
export const nineDigits: ValidatorFn = (control) =>
  control.value && !/^\d{9}$/.test(String(control.value).replace(/-/g, '')) ? { taxIdFormat: true } : null;

export const requiredText = (max: number) =>
  new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(max)] });

export const usernameControl = () =>
  new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(3), Validators.maxLength(40)],
  });

export const passwordControl = () =>
  new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(8), Validators.pattern(passwordPattern)],
  });

// The text of a failed request's body, when the server sent one (it sends plain sentences for 4xx errors).
export const serverMessage = (error: { error?: unknown }, fallback: string): string =>
  typeof error.error === 'string' && error.error.length > 0 && error.error.length < 300 ? error.error : fallback;
