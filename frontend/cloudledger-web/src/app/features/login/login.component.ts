import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="login-page">
      <form (ngSubmit)="submit()" class="login-card">
        <h1>CloudLedger</h1>
        <p>Entre na sua conta</p>
        <label>E-mail</label>
        <input type="email" [(ngModel)]="email" name="email" required />
        <label>Senha</label>
        <input type="password" [(ngModel)]="password" name="password" required />
        <button type="submit" [disabled]="loading()">Entrar</button>
        @if (error()) { <span class="error">{{ error() }}</span> }
      </form>
    </div>
  `,
  styles: [`
    .login-page { display:flex; align-items:center; justify-content:center; min-height:100vh; background:#0f172a; color:#e2e8f0; }
    .login-card { background:#1e293b; padding:2rem; border-radius:12px; width:320px; display:flex; flex-direction:column; gap:.75rem; }
    .login-card h1 { margin:0; font-size:1.5rem; }
    .login-card p { margin:0 0 1rem; color:#94a3b8; }
    label { font-size:.85rem; color:#cbd5e1; }
    input { padding:.6rem .75rem; border-radius:8px; border:1px solid #334155; background:#0f172a; color:#e2e8f0; }
    button { margin-top:.5rem; padding:.7rem; border:none; border-radius:8px; background:#3b82f6; color:#fff; cursor:pointer; }
    button:disabled { opacity:.6; }
    .error { color:#f87171; font-size:.85rem; }
  `]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = 'demo@cloudledger.dev';
  password = 'Demo@12345';
  loading = signal(false);
  error = signal<string | null>(null);

  submit() {
    this.loading.set(true);
    this.error.set(null);
    this.auth.login(this.email, this.password).subscribe({
      next: (res) => {
        this.auth.setSession(res);
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.error.set('Credenciais inválidas.');
        this.loading.set(false);
      }
    });
  }
}