import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TicketDetails, flagLabel, riskCodeLabel, riskCodeList } from '../ticket/ticketDetails';
import { AuthService } from '../auth/auth.service';

@Component({
  imports: [DatePipe, RouterLink],
  selector: 'app-result',
  styleUrl: './result.css',
  templateUrl: './result.html',
})
export class Result implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private auth = inject(AuthService);

  readonly ticket = signal<TicketDetails | null>(null);
  readonly errorMessage = signal('');
  readonly loading = signal(false);

  readonly flagLabel = flagLabel;
  readonly riskCodeLabel = riskCodeLabel;
  readonly riskCodeList = riskCodeList;

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.load(Number(params.get('ticketId')));
    });
  }

  customerName(ticket: TicketDetails): string {
    const customer = ticket.customer;
    return [customer.firstName, customer.middleName, customer.lastName].filter((part) => !!part).join(' ');
  }

  rejectedBySystem(): boolean {
    return this.ticket()?.decision?.decidedBy === 'system';
  }

  unclearedCount(): number {
    return this.ticket()?.flags.filter((flag) => !flag.cleared).length ?? 0;
  }

  logout(): void {
    this.auth.logout().subscribe();
  }

  private load(ticketId: number): void {
    if (!Number.isInteger(ticketId) || ticketId < 1) {
      this.errorMessage.set('That ticket id is not valid.');
      return;
    }
    this.errorMessage.set('');
    this.loading.set(true);
    this.http.get<TicketDetails>(`/api/admins/tickets/${ticketId}`).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (ticket) => this.ticket.set(ticket),
      error: (error) => this.handleError(error),
    });
  }

  private handleError(error: HttpErrorResponse): void {
    console.error('Result request failed:', error);
    if (error.status === 401) {
      this.router.navigate(['/admin/login'], { state: { errorMessage: 'Your session has expired. Please log in again.' } });
      return;
    }
    const detail = typeof error.error === 'string' ? error.error : error.error?.title;
    this.errorMessage.set(detail || 'Something went wrong. Please try again.');
  }
}
