import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../auth/auth.service';

// Where a signed-in user lands. Admins' tools are on the admin side and are closed to users.
@Component({
  selector: 'app-user-home',
  templateUrl: './user-home.html',
})
export class UserHome {
  private auth = inject(AuthService);

  readonly username = signal('');

  constructor() {
    inject(HttpClient).get<{ username: string }>('/api/users/me').subscribe({
      next: (me) => this.username.set(me.username),
      error: () => this.username.set(''),
    });
  }

  logout(): void {
    this.auth.logout().subscribe();
  }
}
