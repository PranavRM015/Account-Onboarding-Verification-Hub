import { Component, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  imports: [FormsModule, ReactiveFormsModule],
  selector: 'app-form',
  standalone: true,
  styleUrls: ['./inputForm.css'],
  templateUrl: './inputForm.html',
})
export class UserFormComponent {
  // Instantiate the FormGroup matching the name in the HTML
  readonly firstName = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/[a-z]/i), Validators.minLength(1), Validators.maxLength(50)],
  });
  readonly middleName = new FormControl('', {
    nonNullable: true,
    validators: [Validators.minLength(0), Validators.pattern(/[a-z]/i), Validators.maxLength(50)],
  });
  readonly lastName = new FormControl('', {
    nonNullable: true,
    validators: [
      Validators.required,
      Validators.pattern(/[a-z]/i),
      Validators.minLength(1),
      Validators.maxLength(50)
    ],
  });

  // Latest allowed birth date (yyyy-MM-dd): exactly 18 years ago today. Built from local date parts, not toISOString(), to avoid a UTC day shift.
  readonly maxBirthDate = (() => {
    const d = new Date();
    d.setFullYear(d.getFullYear() - 18);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  })();

  // yyyy-MM-dd strings compare correctly with > and <
  private readonly adultOnly: ValidatorFn = (control) =>
    control.value && control.value > this.maxBirthDate ? { underage: true } : null;

  readonly dateOfBirth = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, this.adultOnly],
  });
  readonly gender = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });
  readonly taxIdType = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });
  readonly taxIdNumber = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[A-Za-z0-9-]{5,20}$/)],
  });
  readonly residentialAddress = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200)],
  });
  readonly countryOfResidence = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100)],
  });
  readonly countryOfCitizenship = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100)],
  });
  readonly occupation = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100)],
  });
  readonly employer = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100)],
  });

  readonly typeOfAccount = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });
  // Only used (and only validated) when typeOfAccount is Joint; disabled otherwise.
  readonly jointCustomerId = new FormControl<number | null>({ value: null, disabled: true }, {
    validators: [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)],
  });

  // Keys must match the formControlName values used in inputForm.html
  readonly userForm = new FormGroup({
    FirstName: this.firstName,
    MiddleName: this.middleName,
    LastName: this.lastName,
    DateOfBirth: this.dateOfBirth,
    Gender: this.gender,
    taxIdType: this.taxIdType,
    taxIdNumber: this.taxIdNumber,
    residentialAddress: this.residentialAddress,
    countryOfResidence: this.countryOfResidence,
    countryOfCitizenship: this.countryOfCitizenship,
    occupation: this.occupation,
    Employer: this.employer,
    typeOfAccount: this.typeOfAccount,
    jointCustomerId: this.jointCustomerId,
  });

  // Same pattern as the password/username hints in login: the template shows the modal when this matches.
  activeModal: 'jointCustomer' | null = null;

  showModal(which: string): void {
    this.activeModal = which as 'jointCustomer' | null;
  }

  hideModal(): void {
    this.activeModal = null;
  }

  constructor() {
    this.typeOfAccount.valueChanges.subscribe((type) => {
      if (type === 'Joint') {
        this.jointCustomerId.enable();
      } else {
        this.jointCustomerId.reset(null);
        this.jointCustomerId.disable();
        this.hideModal();
      }
    });
  }

  // ISO 3166-1 alpha-2 codes (the database stores char(2)); extend as needed
  readonly countries = [
    { code: 'US', name: 'United States' },
    { code: 'CA', name: 'Canada' },
    { code: 'MX', name: 'Mexico' },
    { code: 'GB', name: 'United Kingdom' },
    { code: 'IE', name: 'Ireland' },
    { code: 'FR', name: 'France' },
    { code: 'DE', name: 'Germany' },
    { code: 'ES', name: 'Spain' },
    { code: 'IT', name: 'Italy' },
    { code: 'NL', name: 'Netherlands' },
    { code: 'CH', name: 'Switzerland' },
    { code: 'SE', name: 'Sweden' },
    { code: 'IN', name: 'India' },
    { code: 'CN', name: 'China' },
    { code: 'JP', name: 'Japan' },
    { code: 'KR', name: 'South Korea' },
    { code: 'SG', name: 'Singapore' },
    { code: 'PH', name: 'Philippines' },
    { code: 'PK', name: 'Pakistan' },
    { code: 'BD', name: 'Bangladesh' },
    { code: 'AU', name: 'Australia' },
    { code: 'NZ', name: 'New Zealand' },
    { code: 'BR', name: 'Brazil' },
    { code: 'AR', name: 'Argentina' },
    { code: 'CO', name: 'Colombia' },
    { code: 'NG', name: 'Nigeria' },
    { code: 'ZA', name: 'South Africa' },
    { code: 'EG', name: 'Egypt' },
    { code: 'AE', name: 'United Arab Emirates' },
    { code: 'SA', name: 'Saudi Arabia' },
  ];

  private http = inject(HttpClient);
  private router = inject(Router);
  onSubmit(): void {
    if (this.userForm.invalid) { this.userForm.markAllAsTouched(); return; }
    const FirstName = this.firstName.value;
    const MiddleName = this.middleName.value;
    const LastName = this.lastName.value;
    const DateOfBirth = this.dateOfBirth.value;
    const Gender = this.gender.value;
    const taxIdType = this.taxIdType.value;
    const taxIdNumber = this.taxIdNumber.value;
    const residentialAddress = this.residentialAddress.value;
    const countryOfResidence = this.countryOfResidence.value;
    const countryOfCitizenship = this.countryOfCitizenship.value;
    const occupation = this.occupation.value;
    const Employer = this.employer.value;
    const typeOfAccount = this.typeOfAccount.value;
    const jointCustomerId = typeOfAccount === 'Joint' ? this.jointCustomerId.value : null;
    const requestBody = {
      FirstName,
      MiddleName,
      LastName,
      DateOfBirth,
      Gender,
      taxIdType,
      taxIdNumber,
      residentialAddress,
      countryOfResidence,
      countryOfCitizenship,
      occupation,
      Employer,
      typeOfAccount,
      jointCustomerId,
    };
    console.log('Submitting input form request:', {
      firstName: requestBody.FirstName,
      middleName: requestBody.MiddleName,
      lastName: requestBody.LastName,
    });

    if (typeOfAccount !== 'Joint') {
      this.saveCustomer(requestBody);
      return;
    }
    // Joint account: make sure the second holder exists before saving anything.
    this.http.get<{ exists: boolean }>(`/api/admins/inputForm/customers/${jointCustomerId}/exists`).subscribe({
      next: (result) => {
        if (result.exists) {
          this.saveCustomer(requestBody);
        } else {
          // The form is left untouched, so everything the user typed stays in place.
          this.showModal('jointCustomer');
        }
      },
      error: (error) => this.handleError(error),
    });
  }

  private saveCustomer(requestBody: object): void {
    this.http.post<{ customerId: number }>('/api/admins/inputForm', requestBody).subscribe({
      next: (response) => {
        console.log('Customer saved successfully:', response);
        alert(`Submitted successfully. This customer's ID is ${response.customerId}. ` +
          'Use it as the Joint Customer ID if they are a joint account holder.');
        this.userForm.reset();
      },
      error: (error) => this.handleError(error),
    });
  }

  private handleError(error: HttpErrorResponse): void {
    console.error('Submission failed:', error);
    if (error.status === 401) {
      alert('Your session has expired. Please log in again.');
      this.router.navigate(['/login']);
    } else if (error.status === 409) {
      alert(`A customer with that tax ID already exists (Customer ID ${error.error?.customerId}).`);
    } else {
      alert('Submission failed. Please try again.');
    }
  }
}