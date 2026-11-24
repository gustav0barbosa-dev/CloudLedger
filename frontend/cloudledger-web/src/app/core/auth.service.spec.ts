import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

const response = {
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  user: { name: 'Demo', email: 'demo@cloudledger.dev' }
};

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('envia credenciais e retorna tokens no login', () => {
    let result: typeof response | undefined;
    service.login('demo@cloudledger.dev', 'Demo@12345').subscribe(value => (result = value));

    const request = http.expectOne(`${environment.apiBaseUrl}/auth/login`);
    expect(request.request.body).toEqual({ email: 'demo@cloudledger.dev', password: 'Demo@12345' });
    request.flush(response);

    expect(result?.accessToken).toBe('access-1');
    expect(result?.refreshToken).toBe('refresh-1');
  });

  it('compartilha uma única renovação entre chamadas simultâneas', () => {
    localStorage.setItem('cl_refresh_token', 'refresh-old');
    let first: typeof response | undefined;
    let second: typeof response | undefined;

    service.refresh().subscribe(value => (first = value));
    service.refresh().subscribe(value => (second = value));

    http.expectOne(`${environment.apiBaseUrl}/auth/refresh`).flush({
      ...response,
      accessToken: 'access-2',
      refreshToken: 'refresh-2'
    });

    expect(first?.accessToken).toBe('access-2');
    expect(second?.accessToken).toBe('access-2');
    expect(service.accessToken).toBe('access-2');
    http.expectNone(`${environment.apiBaseUrl}/auth/refresh`);
  });

  it('limpa a sessão no logout', () => {
    service.setSession(response);
    service.logout();
    http.expectOne(`${environment.apiBaseUrl}/auth/logout`).flush({});

    expect(service.accessToken).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });
});
