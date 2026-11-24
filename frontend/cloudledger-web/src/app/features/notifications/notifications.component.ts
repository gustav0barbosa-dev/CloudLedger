import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Notification {
  id: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAt: string;
}

interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="page-head">
      <h1>Notificações</h1>
      <button class="btn secondary" (click)="markAll()">Marcar todas como lidas</button>
    </div>

    <div class="card">
      <label class="filter">
        <input type="checkbox" [(ngModel)]="unreadOnly" (ngModelChange)="load()" />
        Somente não lidas
      </label>

      @for (item of items(); track item.id) {
        <div class="notification" [class.unread]="!item.isRead">
          <div class="head">
            <strong>{{ item.title }}</strong>
            <span>{{ item.createdAt | date: 'dd/MM/yyyy HH:mm' }}</span>
          </div>
          <p>{{ item.message }}</p>
          @if (!item.isRead) {
            <button class="link" (click)="markRead(item)">Marcar como lida</button>
          }
        </div>
      } @empty {
        <div class="empty">Nenhuma notificação encontrada.</div>
      }
    </div>
  `,
  styles: [`
    .filter { display:flex; align-items:center; gap:8px; font-size:12px; color:var(--muted); margin-bottom:12px; }
    .notification { padding:12px 0; border-bottom:1px solid var(--line); }
    .notification:last-child { border-bottom:0; }
    .notification.unread { border-left:3px solid var(--purple); padding-left:10px; }
    .head { display:flex; justify-content:space-between; gap:12px; font-size:13px; }
    .head span { color:var(--muted); font-size:11px; }
    .notification p { margin:6px 0; color:#c8c6cf; font-size:12px; }
  `]
})
export class NotificationsComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly items = signal<Notification[]>([]);
  unreadOnly = false;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<Paged<Notification>>('/notifications', { unreadOnly: this.unreadOnly, pageSize: 100 }).subscribe({
      next: page => this.items.set(page.items)
    });
  }

  markRead(item: Notification): void {
    this.api.post(`/notifications/${item.id}/read`).subscribe({
      next: () => {
        item.isRead = true;
        this.toasts.success('Notificação marcada como lida.');
        if (this.unreadOnly) {
          this.load();
        }
      }
    });
  }

  markAll(): void {
    this.api.post<number>('/notifications/read-all').subscribe({
      next: count => {
        this.toasts.success(`${count} notificação(ões) marcada(s) como lida(s).`);
        this.load();
      }
    });
  }
}
