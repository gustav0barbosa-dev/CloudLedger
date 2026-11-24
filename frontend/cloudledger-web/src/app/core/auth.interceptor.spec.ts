import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('envia o access token nas requisições autenticadas', () => {
    auth.setSession({
      accessToken: 'token-1',
      refreshToken: 'refresh-1',
      user: { name: 'Demo', email: 'demo@cloudledger.dev' }
    });

    http.get('/api/v1/dashboard').subscribe();

    const request = httpMock.expectOne('/api/v1/dashboard');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush({});
  });

  it('renova o token após 401 e repete a requisição', () => {
    auth.setSession({
      accessToken: 'old',
      refreshToken: 'refresh-old',
      user: { name: 'Demo', email: 'demo@cloudledger.dev' }
    });

    http.get('/api/v1/dashboard').subscribe();

    httpMock.expectOne('/api/v1/dashboard').flush('unauthorized', {
      status: 401,
      statusText: 'Unauthorized'
    });

    httpMock.expectOne(`${environment.apiBaseUrl}/auth/refresh`).flush({
      accessToken: 'new',
      refreshToken: 'refresh-new',
      user: { name: 'Demo', email: 'demo@cloudledger.dev' }
    });

    const retry = httpMock.expectOne('/api/v1/dashboard');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush({});
  });
});
