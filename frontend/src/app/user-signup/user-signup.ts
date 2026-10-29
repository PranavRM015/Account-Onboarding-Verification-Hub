import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { countries } from '../shared/countries';
import {
  addressPattern, adultOnly, maxBirthDate, nineDigits, passwordControl, recoveryQuestions, requiredText, serverMessage,
  usernameControl,
} from '../shared/form-rules';

// A customer signing themselves up. (An admin can also sign a customer up from the input form.)
@Component({
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
  selector: 'app-user-signup',
  styleUrl: './user-signup.css',
  templateUrl: './user-signup.html',
})
export class UserSignup {
  activeModal: 'username' | 'password' | 'address' | null = null;

  showModal(which: string): void {
    this.activeModal = which as 'username' | 'password' | 'address' | null;
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
  readonly taxIdType = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly taxIdNumber = new FormControl('', { nonNullable: true, validators: [Validators.required, nineDigits] });
  readonly residentialAddress = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200), Validators.pattern(addressPattern)],
  });
  readonly countryOfResidence = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly countryOfCitizenship = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly occupation = requiredText(100);
  readonly employer = requiredText(100);
  readonly passwordRecoveryQuestion = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly passwordRecoveryAnswer = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)],
  });

  readonly countries = countries;
  readonly questions = recoveryQuestions;

  private readonly details = [
    this.firstName, this.middleName, this.lastName, this.dateOfBirth, this.gender, this.taxIdType, this.taxIdNumber,
    this.residentialAddress, this.countryOfResidence, this.countryOfCitizenship, this.occupation, this.employer,
    this.passwordRecoveryQuestion, this.passwordRecoveryAnswer,
  ];

  private http = inject(HttpClient);
  private router = inject(Router);

  signUp(): void {
    if (this.username.invalid) { this.activeModal = 'username'; return; }
    if (this.password.invalid) { this.activeModal = 'password'; return; }
    if (this.details.some((control) => control.invalid)) {
      this.details.forEach((control) => control.markAsTouched());
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
      taxIdType: this.taxIdType.value,
      taxIdNumber: this.taxIdNumber.value,
      residentialAddress: this.residentialAddress.value,
      countryOfResidence: this.countryOfResidence.value,
      countryOfCitizenship: this.countryOfCitizenship.value,
      occupation: this.occupation.value,
      employer: this.employer.value,
      passwordRecoveryQuestion: this.passwordRecoveryQuestion.value,
      passwordRecoveryAnswer: this.passwordRecoveryAnswer.value,
    };

    this.http.post('/api/users/signup', requestBody, { responseType: 'text' }).subscribe({
      next: () => {
        alert('Successfully signed up! Please log in with your new credentials.');
        this.router.navigate(['/user/login']);
      },
      error: (error) => {
        if (error.status === 429) {
          alert('Too many attempts. Please wait a minute and try again.');
        } else if (error.status === 409) {
          alert(serverMessage(error, 'That username is already taken.'));
        } else {
          alert(serverMessage(error, 'Signup failed. Please try again.'));
        }
      },
    });
  }
}
