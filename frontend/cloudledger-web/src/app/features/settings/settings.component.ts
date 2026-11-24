import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page-head"><h1>Configurações</h1></div>

    <div class="card">
      <div class="card-head"><strong>Conta</strong></div>
      <div class="links">
        <a routerLink="/profile">Dados pessoais e senha ›</a>
        <a routerLink="/notifications">Notificações ›</a>
        <a routerLink="/audit">Auditoria ›</a>
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <div class="card-head"><strong>Sessão</strong></div>
      <p class="muted">A autenticação usa access token de curta duração e refresh token com rotação.</p>
      <button class="btn secondary" (click)="logout()">Encerrar sessão</button>
    </div>

    <div class="card" style="margin-top:12px">
      <div class="card-head"><strong>Sobre</strong></div>
      <p class="muted">CloudLedger — demonstração de gestão financeira pessoal. Nenhuma operação realiza pagamentos ou transferências bancárias.</p>
    </div>
  `,
  styles: [`
    .links { display:flex; flex-direction:column; gap:8px; }
    .links a { color:#c9c7d0; font-size:13px; }
    .links a:hover { color:#fff; }
    .muted { color:var(--muted); font-size:12px; }
  `]
})
export class SettingsComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
