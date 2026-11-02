import { Component, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService, LoginResponse } from '../auth/auth.service';
import { connectionErrorMessage, passwordControl, serverMessage, usernameControl } from '../shared/form-rules';

// One login screen for both roles. The route's data says which one: /admin/login or /user/login.
@Component({
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  readonly message = signal('');
  readonly submitting = signal(false);
  private http = inject(HttpClient);
  private router = inject(Router);
  private auth = inject(AuthService);
  readonly notice = this.router.currentNavigation()?.extras.state?.['notice'] as string || '';

  constructor() {
    this.message.set(this.router.currentNavigation()?.extras.state?.['errorMessage'] as string || '');
  }

  readonly role: 'admin' | 'user' = inject(ActivatedRoute).snapshot.data['role'] === 'user' ? 'user' : 'admin';
  readonly audience = this.role === 'admin' ? 'an admin' : 'a user';
  private readonly endpoint = this.role === 'admin' ? '/api/admins/login' : '/api/users/login';

  activeModal: 'username' | 'password' | null = null;

  showModal(which: string): void {
    this.activeModal = which as 'username' | 'password' | null;
  }

  hideModal(): void {
    this.activeModal = null;
  }

  readonly username = usernameControl();
  readonly password = passwordControl();

  acceptCredentials(): void {
    if (this.submitting()) { return; }
    this.message.set('');
    if (this.username.invalid) { this.activeModal = 'username'; this.message.set('Enter a username of 3 to 40 characters.'); return; }
    if (this.password.invalid) { this.activeModal = 'password'; this.message.set('Enter a password that meets the requirements shown.'); return; }

    const requestBody = { username: this.username.value, password: this.password.value };

    this.submitting.set(true);
    this.http.post<LoginResponse>(this.endpoint, requestBody).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: (response) => {
        this.auth.saveSession(response.token, this.role === 'admin' ? 'Admin' : 'User');
        this.router.navigate([this.role === 'admin' ? '/admin/home' : '/user/home']);
      },
      error: (error) => {
        const connectionMessage = connectionErrorMessage(error);
        if (connectionMessage) {
          this.message.set(connectionMessage);
          return;
        }
        if (error.status === 429) {
          this.message.set('Too many attempts. Please wait a minute and try again.');
        } else if (error.status === 423) {
          // Three wrong passwords lock the account for 15 minutes; the server says how long is left.
          this.message.set(serverMessage(error, 'This account is locked for 15 minutes after too many failed attempts.'));
        } else {
          this.message.set(error.status === 401 ? 'Invalid username or password.' : 'Login failed. Please try again.');
        }
      },
    });
  }
}
