import { AdminNavigation } from '../shared/admin-navigation';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

interface ReviewTicket {
  ticketId: number;
  customerName: string;
  riskRating: string | null;
  verificationDeadline: string;
}

@Component({
  imports: [AdminNavigation, DatePipe, RouterLink, ReactiveFormsModule],
  selector: 'app-admin-home',
  templateUrl: './admin-home.html',
  styleUrl: './admin-home.css',
})
export class AdminHome implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);

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

  clearFilters(): void {
    this.search.reset('');
    this.risk.reset('All');
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading()) { return; }
    this.loading.set(true);
    this.errorMessage.set('');
    this.http.get<ReviewTicket[]>('/api/admins/tickets').pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (tickets) => this.tickets.set(tickets),
      error: (error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.router.navigate(['/admin/login'], { state: { errorMessage: 'Your session has expired. Please log in again.' } });
        } else if (error.status === 403) {
          this.errorMessage.set('Only admins can view onboarding tickets.');
        } else {
          this.errorMessage.set('Unable to load tickets. Check that the server is running and try again.');
        }
      },
    });
  }

}
