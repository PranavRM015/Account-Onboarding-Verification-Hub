export interface Credential { username: string; password: string; }

export const blankCredentials: Credential = { username: '', password: '' };
export const blankUsernamePwdViolation1: Credential = { username: '', password: 'Abcd12!' };
export const blankUsernamePwdViolation2: Credential = { username: '', password: 'abcdn12!' };
export const blankUsernamePwdViolation3: Credential = { username: '', password: 'A123456!' };
export const blankUsernamePwdViolation4: Credential = { username: '', password: 'Abcdepw!' };
export const blankUsernamePwdViolation5: Credential = { username: '', password: 'ABcbde74' };

export const shortUsernamePwdViolation1: Credential = { username: 'abc', password: 'Abcd12!' };
export const shortUsernamePwdViolation2: Credential = { username: 'abc', password: 'abcdn12!' };
export const shortUsernamePwdViolation3: Credential = { username: 'abc', password: 'A123456!' };
export const shortUsernamePwdViolation4: Credential = { username: 'abc', password: 'Abcdepw!' };
export const shortUsernamePwdViolation5: Credential = { username: 'abc', password: 'ABcbde74' };

export const validUsernamePwdViolation1: Credential = { username: 'Andy', password: 'Abcd12!' };
export const validUsernamePwdViolation2: Credential = { username: 'Ben', password: 'abcdn12!' };
export const validUsernamePwdViolation3: Credential = { username: 'Carrie', password: 'A123456!' };
export const validUsernamePwdViolation4: Credential = { username: 'Ephiram', password: 'Abcdepw!' };
export const validUsernamePwdViolation5: Credential = { username: 'Sanjay', password: 'ABcbde74' };

export const longUsernamePwdViolation1: Credential = { username: 'abcdefghijklmnopqrstuvwxyz1234567890!@#$%^', password: 'Abcd12!' };
export const longUsernamePwdViolation2: Credential = { username: 'abcdefghijklmnopqrstuvwxyz1234567890!@#$%^', password: 'abcdn12!' };
export const longUsernamePwdViolation3: Credential = { username: 'abcdefghijklmnopqrstuvwxyz1234567890!@#$%^', password: 'A1234567!' };
export const longUsernamePwdViolation4: Credential = { username: 'abcdefghijklmnopqrstuvwxyz1234567890!@#$%^', password: 'Abcdepw!' };
export const longUsernamePwdViolation5: Credential = { username: 'abcdefghijklmnopqrstuvwxyz1234567890!@#$%^', password: 'ABcbde74' };

export const validUsernamePwd: Credential = { username: 'Andy', password: 'Abcdef12!' };