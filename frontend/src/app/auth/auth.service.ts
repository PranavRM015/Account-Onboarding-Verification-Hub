import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, of, tap } from 'rxjs';

export interface LoginResponse {
  message: string;
  username: string;
  token: string;
  expiresAt: string;
}

const TOKEN_KEY = 'authToken';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  get token(): string | null {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }

  isLoggedIn(): boolean {
    return !!this.token;
  }

  saveToken(token: string): void {
    try {
      localStorage.setItem(TOKEN_KEY, token);
    } catch {
      // Storage is blocked; the session cookie still signs the admin in.
    }
  }

  clearToken(): void {
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch {
      // Nothing stored, nothing to clear.
    }
  }

  // Asks the server to revoke the token, then forgets it here even if that call fails.
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
