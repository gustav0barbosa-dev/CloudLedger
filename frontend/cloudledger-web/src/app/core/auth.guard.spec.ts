import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideRouter([])]
    });
  });

  function runGuard(): unknown {
    return TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
  }

  it('nega acesso e redireciona sem autenticação', () => {
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate');

    expect(runGuard() as boolean).toBeFalse();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('permite acesso com token armazenado', () => {
    localStorage.setItem('cl_access_token', 'token');
    TestBed.inject(AuthService);

    expect(runGuard() as boolean).toBeTrue();
  });
});
