import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../auth/auth.service';

export interface PendingCustomer {
  customerId: number;
  firstName: string;
  middleName: string | null;
  lastName: string;
  createdAt: string; // UTC; the date pipe shows it in the viewer's own time zone
}

interface ReviewTicket {
  ticketId: number;
  customerName: string;
  riskRating: string | null;
  verificationDeadline: string;
}

// Where a signed-in admin lands: the customers who signed themselves up and are still waiting for a ticket, oldest first.
@Component({
  imports: [DatePipe, RouterLink, ReactiveFormsModule],
  selector: 'app-admin-home',
  styleUrl: './admin-home.css',
  templateUrl: './admin-home.html',
})
export class AdminHome implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private auth = inject(AuthService);

  readonly adminName = this.auth.name;
  readonly customers = signal<PendingCustomer[]>([]);
  readonly status = signal<'loading' | 'ready' | 'failed'>('loading');

  readonly tickets = signal<ReviewTicket[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly search = new FormControl('', { nonNullable: true });
  readonly risk = new FormControl('All', { nonNullable: true });
  private readonly searchValue = toSignal(this.search.valueChanges, { initialValue: '' });
  private readonly riskValue = toSignal(this.risk.valueChanges, { initialValue: 'All' });

  readonly filteredTickets = computed(() => {
    const query = this.searchValue().trim().toLowerCase();
    const ticketQuery = query.replace(/^#/, '');

    return this.tickets().filter((ticket) =>
      (this.riskValue() === 'All' || ticket.riskRating === this.riskValue())
      && (!query || ticket.customerName.toLowerCase().includes(query) || String(ticket.ticketId).includes(ticketQuery)),
    );
  });

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

  ngOnInit(): void {
    this.load();
  }

  fullName(customer: PendingCustomer): string {
    return [customer.firstName, customer.middleName, customer.lastName].filter((part) => !!part).join(' ');
  }

  logout(): void {
    this.auth.logout().subscribe();
  }

  clearFilters(): void {
    this.search.reset('');
    this.risk.reset('All');
  }

  load(): void {
    if (this.loading()) { return; }

    this.loading.set(true);
    this.errorMessage.set('');

    this.http.get<ReviewTicket[]>('/api/admins/tickets').pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (tickets) => this.tickets.set(tickets),
      error: (error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.router.navigate(['/admin/login'], {
            state: { errorMessage: 'Your session has expired. Please log in again.' },
          });
        } else if (error.status === 403) {
          this.errorMessage.set('Only admins can view onboarding tickets.');
        } else {
          this.errorMessage.set('Unable to load tickets. Check that the server is running and try again.');
        }
      },
    });
  }
}