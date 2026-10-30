import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export interface PendingCustomer {
  customerId: number;
  firstName: string;
  middleName: string | null;
  lastName: string;
  createdAt: string; // UTC; the date pipe shows it in the viewer's own time zone
}

// Where a signed-in admin lands: the customers who signed themselves up and are still waiting for a ticket, oldest first.
@Component({
  imports: [DatePipe, RouterLink],
  selector: 'app-admin-home',
  styleUrl: './admin-home.css',
  templateUrl: './admin-home.html',
})
export class AdminHome {
  private http = inject(HttpClient);
  private router = inject(Router);
  private auth = inject(AuthService);

  readonly adminName = this.auth.name;
  readonly customers = signal<PendingCustomer[]>([]);
  readonly status = signal<'loading' | 'ready' | 'failed'>('loading');

  constructor() {
    this.http.get<PendingCustomer[]>('/api/admins/pending-customers').subscribe({
      next: (customers) => {
        this.customers.set(customers);
        this.status.set('ready');
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 401) {
          alert('Your session has expired. Please log in again.');
          this.router.navigate(['/admin/login']);
        } else {
          this.status.set('failed');
        }
      },
    });
  }

  fullName(customer: PendingCustomer): string {
    return [customer.firstName, customer.middleName, customer.lastName].filter((part) => !!part).join(' ');
  }

  logout(): void {
    this.auth.logout().subscribe();
  }
}
