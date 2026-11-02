import { AdminNavigation } from '../shared/admin-navigation';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TicketDetails, flagLabel, riskCodeLabel, riskCodeList } from '../ticket/ticketDetails';

type ReviewAction = 'approve' | 'reject' | 'clear';

@Component({
  imports: [AdminNavigation, DatePipe, ReactiveFormsModule, RouterLink],
  selector: 'app-review',
  styleUrl: './review.css',
  templateUrl: './review.html',
})
export class Review implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  private ticketId = 0;

  readonly ticket = signal<TicketDetails | null>(null);
  readonly unresolvedCount = computed(() => this.ticket()?.flags.filter((flag) => !flag.cleared).length ?? 0);
  readonly errorMessage = signal('');
  readonly loading = signal(false);
  readonly notice = signal('');
  readonly action = signal<ReviewAction | null>(null);
  readonly clearFlagId = signal<number | null>(null);
  readonly submitting = signal(false);

  readonly flagLabel = flagLabel;
  readonly riskCodeLabel = riskCodeLabel;
  readonly riskCodeList = riskCodeList;

  readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(500)],
  });

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.ticketId = Number(params.get('ticketId'));
      this.load();
    });
  }

  hasUnclearedFlags(): boolean {
    return this.unresolvedCount() > 0;
  }

  canApprove(): boolean {
    return this.ticket()?.ticketStatus === 'Review' && !this.hasUnclearedFlags();
  }

  modalTitle(): string {
    switch (this.action()) {
      case 'approve': return 'Approve this ticket';
      case 'reject': return 'Reject this ticket';
      case 'clear': return 'Clear this flag';
      default: return '';
    }
  }

  openAction(action: ReviewAction, flagId: number | null = null): void {
    if (this.loading() || this.submitting()) { return; }
    this.reason.reset('');
    this.notice.set('');
    this.errorMessage.set('');
    this.clearFlagId.set(flagId);
    this.action.set(action);
  }

  closeModal(): void {
    if (this.submitting()) { return; }
    this.action.set(null);
    this.clearFlagId.set(null);
  }

  confirm(): void {
    const action = this.action();
    if (!action || this.reason.invalid || this.submitting() || this.loading()) { return; }
    this.errorMessage.set('');
    const requestBody = { reason: this.reason.value.trim() };
    const baseUrl = `/api/admins/tickets/${this.ticketId}`;
    const url = action === 'clear' ? `${baseUrl}/flags/${this.clearFlagId()}/clear` : `${baseUrl}/${action}`;

    this.submitting.set(true);
    this.http.post<{ message: string }>(url, requestBody).subscribe({
      next: (response) => {
        this.submitting.set(false);
        this.closeModal();
        this.refresh(response.message);
      },
      error: (error) => {
        this.submitting.set(false);
        this.handleError(error);
      },
    });
  }

  private load(): void {
    if (!Number.isInteger(this.ticketId) || this.ticketId < 1) {
      this.errorMessage.set('That ticket id is not valid.');
      return;
    }
    this.errorMessage.set('');
    this.loading.set(true);
    this.http.get<TicketDetails>(`/api/admins/tickets/${this.ticketId}`).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (ticket) => this.ticket.set(ticket),
      error: (error) => this.handleError(error),
    });
  }

  private refresh(message: string): void {
    this.loading.set(true);
    this.http.get<TicketDetails>(`/api/admins/tickets/${this.ticketId}`).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (ticket) => {
        if (ticket.ticketStatus !== 'Review') {
          this.router.navigate(['/result', ticket.ticketId]);
          return;
        }
        this.ticket.set(ticket);
        this.notice.set(message);
      },
      error: (error) => this.handleError(error),
    });
  }

  private handleError(error: HttpErrorResponse): void {
    console.error('Review request failed:', error);
    if (error.status === 401) {
      this.router.navigate(['/admin/login'], { state: { errorMessage: 'Your session has expired. Please log in again.' } });
      return;
    }
    const detail = typeof error.error === 'string' ? error.error : error.error?.title;
    this.errorMessage.set(detail || 'Something went wrong. Please try again.');
  }
}
