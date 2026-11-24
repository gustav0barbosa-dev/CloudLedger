import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ToastService } from './toast.service';

type Query = Record<string, string | number | boolean | null | undefined>;

/** Cliente HTTP com URL base e mensagens de erro consistentes para o usuário. */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  get<T>(path: string, query: Query = {}): Observable<T> {
    return this.http
      .get<T>(this.url(path), { params: this.params(query) })
      .pipe(catchError(error => this.fail(error)));
  }

  post<T>(path: string, body: unknown = {}, query: Query = {}): Observable<T> {
    return this.http
      .post<T>(this.url(path), body, { params: this.params(query) })
      .pipe(catchError(error => this.fail(error)));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(this.url(path), body).pipe(catchError(error => this.fail(error)));
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(this.url(path)).pipe(catchError(error => this.fail(error)));
  }

  private url(path: string): string {
    return `${environment.apiBaseUrl}${path.startsWith('/') ? path : '/' + path}`;
  }

  private params(query: Query): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return params;
  }

  private fail(error: HttpErrorResponse): Observable<never> {
    const problem = error.error as { detail?: string; title?: string; errors?: Record<string, string[]> } | null;
    const firstFieldError = problem?.errors
      ? Object.values(problem.errors).flat()[0]
      : undefined;

    let message = problem?.detail ?? firstFieldError ?? problem?.title;
    if (!message) {
      message = error.status === 0
        ? 'Não foi possível comunicar com a API. Verifique se o servidor está ativo.'
        : 'Não foi possível concluir a operação. Tente novamente.';
    }

    this.toasts.error(message);
    return throwError(() => error);
  }
}
