import { Component, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { countries } from '../shared/countries';
import {
  adultOnly, maxBirthDate, nineDigits, fieldError, connectionErrorMessage, passwordControl, recoveryQuestions, requiredText, serverMessage, usernameControl,
} from '../shared/form-rules';

// Admin signup: login details, the admin's own profile, and a password recovery question.
@Component({
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
  selector: 'app-signup',
  styleUrl: './signup.css',
  templateUrl: './signup.html',
})
export class Signup {
  readonly fieldError = fieldError;
  readonly message = signal('');
  readonly submitting = signal(false);
  activeModal: 'username' | 'password' | null = null;

  showModal(which: string): void {
    this.activeModal = which as 'username' | 'password' | null;
  }
  hideModal(): void {
    this.activeModal = null;
  }

  readonly username = usernameControl();
  readonly password = passwordControl();

  readonly firstName = requiredText(50);
  readonly middleName = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] });
  readonly lastName = requiredText(50);
  readonly maxBirthDate = maxBirthDate();
  readonly dateOfBirth = new FormControl('', { nonNullable: true, validators: [Validators.required, adultOnly] });
  readonly gender = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly ssn = new FormControl('', { nonNullable: true, validators: [Validators.required, nineDigits] });
  readonly isCitizen = new FormControl(false, { nonNullable: true });
  readonly countryOfCitizenship = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly passwordRecoveryQuestion = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly passwordRecoveryAnswer = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)],
  });

  readonly countries = countries;
  readonly questions = recoveryQuestions;

  private readonly details = [
    this.firstName, this.middleName, this.lastName, this.dateOfBirth, this.gender, this.ssn,
    this.countryOfCitizenship, this.passwordRecoveryQuestion, this.passwordRecoveryAnswer,
  ];

  private http = inject(HttpClient);
  private router = inject(Router);

  acceptAdminCredentials(): void {
    if (this.submitting()) { return; }
    this.message.set('');
    [this.username, this.password, ...this.details].forEach((control) => control.markAsTouched());
    if (this.username.invalid) { this.activeModal = 'username'; this.message.set('Enter a username of 3 to 40 characters.'); return; }
    if (this.password.invalid) { this.activeModal = 'password'; this.message.set('Enter a password that meets the requirements shown.'); return; }
    if (this.details.some((control) => control.invalid)) {
      this.details.forEach((control) => control.markAsTouched());
      this.message.set('Complete all required fields and correct any invalid details.');
      return;
    }

    const requestBody = {
      username: this.username.value,
      password: this.password.value,
      firstName: this.firstName.value,
      middleName: this.middleName.value,
      lastName: this.lastName.value,
      dateOfBirth: this.dateOfBirth.value,
      gender: this.gender.value,
      ssn: this.ssn.value,
      isCitizen: this.isCitizen.value,
      countryOfCitizenship: this.countryOfCitizenship.value,
      passwordRecoveryQuestion: this.passwordRecoveryQuestion.value,
      passwordRecoveryAnswer: this.passwordRecoveryAnswer.value,
    };

    this.submitting.set(true);
    this.http.post('/api/admins/signup', requestBody, { responseType: 'text' }).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => {
        this.router.navigate(['/admin/login'], { state: { notice: 'Account created. Please log in with your new credentials.' } });
      },
      error: (error) => {
        const connectionMessage = connectionErrorMessage(error);
        if (connectionMessage) {
          this.message.set(connectionMessage);
          return;
        }
        if (error.status === 401) {
          this.message.set('Only a logged-in admin can create a new admin. Please log in first.');
          this.router.navigate(['/admin/login'], { state: { errorMessage: this.message() } });
        } else if (error.status === 429) {
          this.message.set('Too many attempts. Please wait a minute and try again.');
        } else if (error.status === 409) {
          this.message.set(serverMessage(error, 'That username is already taken.'));
        } else {
          this.message.set(serverMessage(error, 'Signup failed. Please try again.'));
        }
      },
    });
  }
}
