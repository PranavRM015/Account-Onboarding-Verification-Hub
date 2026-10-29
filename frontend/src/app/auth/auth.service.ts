import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, of, tap } from 'rxjs';

export type Role = 'Admin' | 'User';

export interface LoginResponse {
  message: string;
  username: string;
  role: Role;
  token: string;
  expiresAt: string;
}

const TOKEN_KEY = 'authToken';
const ROLE_KEY = 'authRole';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  get token(): string | null {
    return this.read(TOKEN_KEY);
  }

  get role(): Role | null {
    const role = this.read(ROLE_KEY);
    return role === 'Admin' || role === 'User' ? role : null;
  }

  isLoggedIn(): boolean {
    return !!this.token;
  }

  // The server checks the role on every request; this one only decides which screens to show.
  hasRole(role: Role): boolean {
    return this.isLoggedIn() && this.role === role;
  }

  saveSession(token: string, role: Role): void {
    try {
      localStorage.setItem(TOKEN_KEY, token);
      localStorage.setItem(ROLE_KEY, role);
    } catch {
      // Storage is blocked; the session cookie still signs the person in.
    }
  }

  clearToken(): void {
    try {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(ROLE_KEY);
    } catch {
      // Nothing stored, nothing to clear.
    }
  }

  // Asks the server to revoke the token, then forgets it here even if that call fails. Admins and users share this endpoint.
  logout(): Observable<unknown> {
    return this.http.post('/api/admins/logout', null).pipe(
      catchError(() => of(null)),
      tap(() => {
        this.clearToken();
        this.router.navigate(['/']);
      }),
    );
  }
}
