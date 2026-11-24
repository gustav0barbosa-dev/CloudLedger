import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';

interface UserProfile {
  id: string;
  name: string;
  email: string;
  createdAt: string;
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="page-head"><h1>Meu perfil</h1></div>

    <div class="card">
      <div class="card-head"><strong>Dados pessoais</strong></div>
      <div class="form-row">
        <input [(ngModel)]="name" placeholder="Nome" />
        <input [(ngModel)]="email" type="email" placeholder="E-mail" />
        <button class="btn" (click)="saveProfile()">Salvar dados</button>
      </div>
      @if (profile(); as user) {
        <div class="muted">Usuário desde {{ user.createdAt | date: 'dd/MM/yyyy' }}</div>
      }
    </div>

    <div class="card" style="margin-top:12px">
      <div class="card-head"><strong>Alterar senha</strong></div>
      <div class="form-row">
        <input [(ngModel)]="currentPassword" type="password" placeholder="Senha atual" />
        <input [(ngModel)]="newPassword" type="password" placeholder="Nova senha (mínimo 8 caracteres)" />
        <button class="btn secondary" (click)="changePassword()">Alterar senha</button>
      </div>
    </div>
  `,
  styles: [`.muted { color:var(--muted); font-size:12px; }`]
})
export class ProfileComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);

  readonly profile = signal<UserProfile | null>(null);

  name = '';
  email = '';
  currentPassword = '';
  newPassword = '';

  ngOnInit(): void {
    this.api.get<UserProfile>('/users/me').subscribe({
      next: user => {
        this.profile.set(user);
        this.name = user.name;
        this.email = user.email;
      }
    });
  }

  saveProfile(): void {
    this.api.put<UserProfile>('/users/me', { name: this.name, email: this.email }).subscribe({
      next: user => {
        this.profile.set(user);
        this.auth.updateUser({ name: user.name, email: user.email });
        this.toasts.success('Perfil atualizado.');
      }
    });
  }

  changePassword(): void {
    this.api.post('/users/me/password', {
      currentPassword: this.currentPassword,
      newPassword: this.newPassword
    }).subscribe({
      next: () => {
        this.currentPassword = '';
        this.newPassword = '';
        this.toasts.success('Senha alterada com sucesso.');
      }
    });
  }
}
