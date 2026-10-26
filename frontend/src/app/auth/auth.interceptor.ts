import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // A 401 on a request that carried a token means the token expired or was revoked.
      // A failed login is also a 401, but it carries no token, so it is left alone.
      if (error.status === 401 && token) {
        auth.clearToken();
      }
      return throwError(() => error);
    }),
  );
};
