import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService, LoginResponse } from '../auth/auth.service';

@Component({
  imports: [FormsModule, ReactiveFormsModule],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  activeModal: 'username' | 'password' | null = null;

  showModal(which: string): void {
    this.activeModal = which as 'username' | 'password' | null;
  }

  hideModal(): void {
    this.activeModal = null;
  }

  readonly username = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(3), Validators.maxLength(40)],
  });
  readonly password = new FormControl('', {
    nonNullable: true,
    validators: [
      Validators.required,
      Validators.minLength(8),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$/),
    ],
  });
  
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private auth = inject(AuthService);

  acceptAdminCredentials(): void {
    console.log('Form submitted!');
    if (this.username.invalid) { this.activeModal = 'username'; return; }
    if (this.password.invalid) { this.activeModal = 'password'; return; }

    const username = this.username.value;
    const password = this.password.value;
    const requestBody = { username, password };
    console.log('Submitting login request:', {
      username: requestBody.username,
      passwordPresent: !!requestBody.password
    });

    this.http.post<LoginResponse>('/api/admins/login', requestBody).subscribe({
      next: (response) => {
        console.log('Admin logged in successfully:', response.username);
        this.auth.saveToken(response.token);
        this.router.navigate(['/inputForm']);
      },
      error: (error) =>{
        console.error('Login failed:', error);
        if (error.status === 429) {
          alert('Too many attempts. Please wait a minute and try again.');
          return;
        }
        alert(error.status === 401 ? 'Invalid username or password.' : 'Login failed. Please try again.');
      }
    });
  }
}
