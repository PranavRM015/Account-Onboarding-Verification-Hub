import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';

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
  protected username = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(3), Validators.maxLength(40)],
  });
  protected password = new FormControl('', {
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

    this.http.post('/api/admins/login', requestBody, { responseType: 'text' }).subscribe({
      next: (response) => {
        console.log('Admin created successfully:', response);
        alert('Successfully signed up! Please log in with your new credentials.');
        this.router.navigate(['/']);
      },
      error: (error) =>{
        console.error('Login failed:', error);
        alert(error.status === 409 ? 'That username is already taken.' : 'Signup failed. Please try again.'); 
      }
    });
  }
}
