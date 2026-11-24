import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { finalize, Observable, shareReplay, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: { name: string; email: string };
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'cl_access_token';
  private readonly refreshKey = 'cl_refresh_token';
  private refreshInFlight: Observable<AuthResponse> | null = null;

  readonly user = signal<AuthResponse['user'] | null>(null);
  readonly isAuthenticated = signal<boolean>(this.hasToken());

  constructor(private http: HttpClient, private router: Router) {
    const token = this.accessToken;
    if (token) {
      this.isAuthenticated.set(true);
    }
  }

  get accessToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  private hasToken(): boolean {
    return !!localStorage.getItem(this.tokenKey);
  }

  login(email: string, password: string) {
    return this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, { email, password });
  }

  /**
   * Renovação compartilhada: várias requisições 401 aguardam o mesmo POST,
   * evitando disputa pela rotação do refresh token.
   */
  refresh(): Observable<AuthResponse> {
    if (!this.refreshInFlight) {
      const refresh = localStorage.getItem(this.refreshKey);

      this.refreshInFlight = this.http
        .post<AuthResponse>(`${environment.apiBaseUrl}/auth/refresh`, { refreshToken: refresh })
        .pipe(
          tap(response => this.setSession(response)),
          finalize(() => {
            this.refreshInFlight = null;
          }),
          shareReplay({ bufferSize: 1, refCount: false })
        );
    }

    return this.refreshInFlight;
  }

  setSession(response: AuthResponse) {
    localStorage.setItem(this.tokenKey, response.accessToken);
    localStorage.setItem(this.refreshKey, response.refreshToken);
    this.user.set(response.user);
    this.isAuthenticated.set(true);
  }

  updateUser(user: AuthResponse['user']): void {
    this.user.set(user);
  }

  logout() {
    const refresh = localStorage.getItem(this.refreshKey);
    if (refresh) {
      this.http.post(`${environment.apiBaseUrl}/auth/logout`, { refreshToken: refresh }).subscribe({ next: () => {}, error: () => {} });
    }
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshKey);
    this.user.set(null);
    this.isAuthenticated.set(false);
    this.router.navigate(['/login']);
  }
}
