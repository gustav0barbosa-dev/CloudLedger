import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { catchError, switchMap, throwError } from 'rxjs';

function addToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken;
  return next(addToken(req, token)).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !req.url.includes('/auth/')) {
        return auth.refresh().pipe(
          switchMap(res => {
            auth.setSession(res);
            return next(addToken(req, res.accessToken));
          }),
          catchError(refreshErr => {
            auth.logout();
            return throwError(() => refreshErr);
          })
        );
      }
      return throwError(() => err);
    })
  );
};