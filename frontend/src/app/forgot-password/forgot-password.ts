import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { passwordControl, serverMessage, usernameControl } from '../shared/form-rules';

// Step 1: ask for the username and show the recovery question. Step 2: the answer plus a new password.
@Component({
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
  selector: 'app-forgot-password',
  styleUrl: './forgot-password.css',
  templateUrl: './forgot-password.html',
})
export class ForgotPassword {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly role: 'admin' | 'user' = inject(ActivatedRoute).snapshot.paramMap.get('role') === 'user' ? 'user' : 'admin';
  private readonly api = this.role === 'admin' ? '/api/admins' : '/api/users';

  readonly step = signal<'username' | 'answer'>('username');
  readonly question = signal('');
  readonly message = signal('');
  activeModal: 'password' | null = null;

  readonly username = usernameControl();
  readonly answer = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/)] });
  readonly newPassword = passwordControl();
  readonly confirmPassword = new FormControl('', { nonNullable: true, validators: [Validators.required] });

  showModal(which: string): void {
    this.activeModal = which as 'password' | null;
  }
  hideModal(): void {
    this.activeModal = null;
  }

  get passwordsMatch(): boolean {
    return this.newPassword.value === this.confirmPassword.value;
  }

  findQuestion(): void {
    this.message.set('');
    if (this.username.invalid) {
      this.username.markAsTouched();
      this.message.set('Enter your username (3 to 40 characters).');
      return;
    }
    this.http.get<{ question: string }>(`${this.api}/recovery-question`, { params: { username: this.username.value } }).subscribe({
      next: (response) => {
        this.question.set(response.question);
        this.step.set('answer');
      },
      error: (error) => this.fail(error, 'Could not look up your question. Please try again.'),
    });
  }

  resetPassword(): void {
    this.message.set('');
    if (this.answer.invalid) {
      this.message.set('Enter the answer to your recovery question.');
      return;
    }
    if (this.newPassword.invalid) {
      this.activeModal = 'password';
      return;
    }
    if (!this.passwordsMatch) {
      this.message.set('The two passwords do not match.');
      return;
    }
    const requestBody = { username: this.username.value, answer: this.answer.value, newPassword: this.newPassword.value };
    this.http.post(`${this.api}/reset-password`, requestBody).subscribe({
      next: () => {
        alert('Your password has been changed. Please log in with your new password.');
        this.router.navigate([`/${this.role}/login`]);
      },
      error: (error) => this.fail(error, 'Could not reset your password. Please try again.'),
    });
  }

  backToUsername(): void {
    this.step.set('username');
    this.answer.reset();
    this.newPassword.reset();
    this.confirmPassword.reset();
    this.message.set('');
  }

  private fail(error: { status: number; error?: unknown }, fallback: string): void {
    if (error.status === 429) {
      this.message.set('Too many attempts. Please wait a minute and try again.');
    } else if (error.status === 423) {
      this.message.set(serverMessage(error, 'This account is locked for 15 minutes after too many failed attempts.'));
    } else if (error.status === 401) {
      this.message.set('That answer is not right. Please try again.');
    } else {
      this.message.set(serverMessage(error, fallback));
    }
  }
}
