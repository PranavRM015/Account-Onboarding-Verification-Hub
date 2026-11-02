import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { countries } from '../shared/countries';
import { serverMessage } from '../shared/form-rules';

interface PendingDetails {
  customerId: number;
  firstName: string;
  middleName: string | null;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  taxIdType: string;
  taxIdLastFourDigits: string;
  residentialAddress: string;
  countryOfResidence: string;
  countryOfCitizenship: string;
  occupation: string;
  employer: string;
}

interface Row {
  key: string;
  label: string;
  value: string;
}

// The form for a customer who signed themselves up. It shows exactly what they entered, read-only, and leaves blank the one
// thing they never chose: the account type. The admin fills that in and opens the ticket.
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-pending-onboarding',
  styleUrl: './pending-onboarding.css',
  templateUrl: './pending-onboarding.html',
})
export class PendingOnboarding {
  private http = inject(HttpClient);
  private router = inject(Router);
  private customerId = Number(inject(ActivatedRoute).snapshot.paramMap.get('customerId'));

  readonly rows = signal<Row[]>([]);
  readonly loadError = signal('');
  readonly submitError = signal('');
  readonly submitting = signal(false);

  // Only the admin's choices are editable. Both start empty.
  readonly typeOfAccount = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly jointCustomerId = new FormControl<number | null>({ value: null, disabled: true }, {
    validators: [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)],
  });
  readonly form = new FormGroup({ typeOfAccount: this.typeOfAccount, jointCustomerId: this.jointCustomerId });

  constructor() {
    this.typeOfAccount.valueChanges.subscribe((type) => {
      if (type === 'Joint') {
        this.jointCustomerId.enable();
      } else {
        this.jointCustomerId.reset(null);
        this.jointCustomerId.disable();
      }
    });
    this.http.get<PendingDetails>(`/api/admins/pending-customers/${this.customerId}`).subscribe({
      next: (customer) => this.rows.set(this.toRows(customer)),
      error: (error: HttpErrorResponse) => this.handleLoadError(error),
    });
  }

  private countryName(code: string): string {
    return countries.find((country) => country.code === code)?.name ?? code;
  }

  // Shown the way the customer entered it. The full tax ID was never stored, so only its last four digits can be shown.
  private toRows(c: PendingDetails): Row[] {
    return [
      { key: 'firstName', label: 'First Name', value: c.firstName },
      { key: 'middleName', label: 'Middle Name', value: c.middleName ?? '' },
      { key: 'lastName', label: 'Last Name', value: c.lastName },
      { key: 'dateOfBirth', label: 'Date of Birth', value: c.dateOfBirth },
      { key: 'gender', label: 'Gender', value: c.gender },
      { key: 'taxIdType', label: 'Tax ID Type', value: c.taxIdType },
      { key: 'taxId', label: 'Tax ID Number', value: `•••••${c.taxIdLastFourDigits}` },
      { key: 'residentialAddress', label: 'Residential Address', value: c.residentialAddress },
      { key: 'countryOfResidence', label: 'Country of Residence', value: this.countryName(c.countryOfResidence) },
      { key: 'countryOfCitizenship', label: 'Country of Citizenship', value: this.countryName(c.countryOfCitizenship) },
      { key: 'occupation', label: 'Occupation', value: c.occupation },
      { key: 'employer', label: 'Employer', value: c.employer },
    ];
  }

  onSubmit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    const typeOfAccount = this.typeOfAccount.value;
    const body = { typeOfAccount, jointCustomerId: typeOfAccount === 'Joint' ? this.jointCustomerId.value : null };
    this.submitError.set('');
    this.submitting.set(true);
    this.http.post<{ ticketId: number }>(`/api/admins/pending-customers/${this.customerId}/submit`, body).subscribe({
      next: (response) => this.router.navigate(['/review', response.ticketId]),
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.handleSubmitError(error);
      },
    });
  }

  private handleLoadError(error: HttpErrorResponse): void {
    if (error.status === 401) {
      alert('Your session has expired. Please log in again.');
      this.router.navigate(['/admin/login']);
    } else if (error.status === 404 || error.status === 409) {
      this.loadError.set(serverMessage(error, 'That customer is no longer waiting.'));
    } else {
      this.loadError.set('Could not load that customer. Please try again.');
    }
  }

  private handleSubmitError(error: HttpErrorResponse): void {
    if (error.status === 401) {
      alert('Your session has expired. Please log in again.');
      this.router.navigate(['/admin/login']);
    } else if (error.status === 400 || error.status === 404 || error.status === 409) {
      this.submitError.set(serverMessage(error, 'The ticket could not be opened.'));
    } else {
      this.submitError.set('Submission failed. Please try again.');
    }
  }
}
