import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService, LoginResponse } from '../auth/auth.service';
import { passwordControl, serverMessage, usernameControl } from '../shared/form-rules';

// One login screen for both roles. The route's data says which one: /admin/login or /user/login.
@Component({
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  private http = inject(HttpClient);
  private router = inject(Router);
  private auth = inject(AuthService);

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
    if (this.username.invalid) { this.activeModal = 'username'; return; }
    if (this.password.invalid) { this.activeModal = 'password'; return; }

    const requestBody = { username: this.username.value, password: this.password.value };

    this.http.post<LoginResponse>(this.endpoint, requestBody).subscribe({
      next: (response) => {
        this.auth.saveSession(response.token, this.role === 'admin' ? 'Admin' : 'User', response.firstName);
        this.router.navigate([this.role === 'admin' ? '/admin/home' : '/user/home']);
      },
      error: (error) => {
        if (error.status === 429) {
          alert('Too many attempts. Please wait a minute and try again.');
        } else if (error.status === 423) {
          // Three wrong passwords lock the account for 15 minutes; the server says how long is left.
          alert(serverMessage(error, 'This account is locked for 15 minutes after too many failed attempts.'));
        } else {
          alert(error.status === 401 ? 'Invalid username or password.' : 'Login failed. Please try again.');
        }
      },
    });
  }
}
