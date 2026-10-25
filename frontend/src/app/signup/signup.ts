import { Component, inject  } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';


@Component({
  imports: [FormsModule, ReactiveFormsModule],
  selector: 'app-signup',
  styleUrl: './signup.css',
  templateUrl: './signup.html',
})
export class Signup {
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

  readonly signupCode = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });

  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  acceptAdminCredentials(): void {
    console.log('Form submitted!');
    if (this.username.invalid) { this.activeModal = 'username'; return; }
    if (this.password.invalid) { this.activeModal = 'password'; return; }
    if (this.signupCode.invalid) { return; }

    const username = this.username.value;
    const password = this.password.value;
    const signupCode = this.signupCode.value;
    const requestBody = { username, password, signupCode };
    console.log('Submitting signup request:', {
      username: requestBody.username,
      passwordPresent: !!requestBody.password
    });

    this.http.post('/api/admins/signup', requestBody, { responseType: 'text' }).subscribe({
      next: (response) => {
        console.log('Admin created successfully:', response);
        alert('Successfully signed up! Please log in with your new credentials.');
        this.router.navigate(['/']);
      },
      error: (error) => {
        console.error('Signup failed:', error);
        if (error.status === 401) {
          alert('Only a logged-in admin can create a new admin. Please log in first.');
          this.router.navigate(['/login']);
          return;
        }
        if (error.status === 429) {
          alert('Too many attempts. Please wait a minute and try again.');
          return;
        }
        if (error.status === 403) {
          alert('That signup code is not valid.');
          return;
        }
        alert(error.status === 409 ? 'That username is already taken.' : 'Signup failed. Please try again.');
      }
    });
  }
}
