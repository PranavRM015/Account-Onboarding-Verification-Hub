import { Component, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';

// ISO 3166-1 alpha-2 codes (the database stores char(2)); the display names come from Intl.DisplayNames
const isoCountryCodes = [
  'AD', 'AE', 'AF', 'AG', 'AI', 'AL', 'AM', 'AO', 'AQ', 'AR', 'AS', 'AT', 'AU', 'AW', 'AX', 'AZ',
  'BA', 'BB', 'BD', 'BE', 'BF', 'BG', 'BH', 'BI', 'BJ', 'BL', 'BM', 'BN', 'BO', 'BQ', 'BR', 'BS', 'BT', 'BV', 'BW', 'BY', 'BZ',
  'CA', 'CC', 'CD', 'CF', 'CG', 'CH', 'CI', 'CK', 'CL', 'CM', 'CN', 'CO', 'CR', 'CU', 'CV', 'CW', 'CX', 'CY', 'CZ',
  'DE', 'DJ', 'DK', 'DM', 'DO', 'DZ',
  'EC', 'EE', 'EG', 'EH', 'ER', 'ES', 'ET',
  'FI', 'FJ', 'FK', 'FM', 'FO', 'FR',
  'GA', 'GB', 'GD', 'GE', 'GF', 'GG', 'GH', 'GI', 'GL', 'GM', 'GN', 'GP', 'GQ', 'GR', 'GS', 'GT', 'GU', 'GW', 'GY',
  'HK', 'HM', 'HN', 'HR', 'HT', 'HU',
  'ID', 'IE', 'IL', 'IM', 'IN', 'IO', 'IQ', 'IR', 'IS', 'IT',
  'JE', 'JM', 'JO', 'JP',
  'KE', 'KG', 'KH', 'KI', 'KM', 'KN', 'KP', 'KR', 'KW', 'KY', 'KZ',
  'LA', 'LB', 'LC', 'LI', 'LK', 'LR', 'LS', 'LT', 'LU', 'LV', 'LY',
  'MA', 'MC', 'MD', 'ME', 'MF', 'MG', 'MH', 'MK', 'ML', 'MM', 'MN', 'MO', 'MP', 'MQ', 'MR', 'MS', 'MT', 'MU', 'MV', 'MW', 'MX', 'MY', 'MZ',
  'NA', 'NC', 'NE', 'NF', 'NG', 'NI', 'NL', 'NO', 'NP', 'NR', 'NU', 'NZ',
  'OM',
  'PA', 'PE', 'PF', 'PG', 'PH', 'PK', 'PL', 'PM', 'PN', 'PR', 'PS', 'PT', 'PW', 'PY',
  'QA',
  'RE', 'RO', 'RS', 'RU', 'RW',
  'SA', 'SB', 'SC', 'SD', 'SE', 'SG', 'SH', 'SI', 'SJ', 'SK', 'SL', 'SM', 'SN', 'SO', 'SR', 'SS', 'ST', 'SV', 'SX', 'SY', 'SZ',
  'TC', 'TD', 'TF', 'TG', 'TH', 'TJ', 'TK', 'TL', 'TM', 'TN', 'TO', 'TR', 'TT', 'TV', 'TW', 'TZ',
  'UA', 'UG', 'UM', 'US', 'UY', 'UZ',
  'VA', 'VC', 'VE', 'VG', 'VI', 'VN', 'VU',
  'WF', 'WS',
  'YE', 'YT',
  'ZA', 'ZM', 'ZW',
];

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
  // 9 digits, dashes allowed (123-45-6789 or 12-3456789)
  private readonly nineDigits: ValidatorFn = (control) =>
    control.value && !/^\d{9}$/.test(String(control.value).replace(/-/g, '')) ? { taxIdFormat: true } : null;

  readonly taxIdNumber = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, this.nineDigits],
  });
  // number street name, City, State/Province, ZIP code
  readonly residentialAddress = new FormControl('', {
    nonNullable: true,
    validators: [
      Validators.required,
      Validators.maxLength(200),
      Validators.pattern(/^\d+[A-Za-z]?\s+[^,]+,\s*[^,]+,\s*[^,]+,\s*[A-Za-z0-9][A-Za-z0-9 -]{2,9}$/),
    ],
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
  activeModal: 'jointCustomer' | 'address' | null = null;
  showTaxId = false;

  showModal(which: string): void {
    this.activeModal = which as 'jointCustomer' | 'address' | null;
  }

  toggleTaxId(): void {
    this.showTaxId = !this.showTaxId;
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

  readonly countries = (() => {
    const names = new Intl.DisplayNames(['en'], { type: 'region' });
    return isoCountryCodes
      .map((code) => ({ code, name: names.of(code) ?? code }))
      .sort((a, b) => a.name.localeCompare(b.name));
  })();

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
    this.http.post<{ customerId: number; ticketId: number }>('/api/admins/inputForm', requestBody).subscribe({
      next: (response) => {
        console.log('Customer saved successfully:', response);
        this.userForm.reset();
        this.router.navigate(['/review', response.ticketId]);
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