import { Component, Input, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../auth/auth.service';

@Component({
  imports: [RouterLink],
  selector: 'app-admin-navigation',
  templateUrl: './admin-navigation.html',
  styleUrl: './admin-navigation.css',
})
export class AdminNavigation {
  @Input() active: 'tickets' | 'customer' = 'tickets';
  private auth = inject(AuthService);
  readonly loggingOut = signal(false);

  logout(): void {
    if (this.loggingOut()) { return; }
    this.loggingOut.set(true);
    this.auth.logout().pipe(finalize(() => this.loggingOut.set(false))).subscribe();
  }
}
