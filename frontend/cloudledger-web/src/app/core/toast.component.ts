import { Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    <div class="toast-stack" role="status" aria-live="polite">
      @for (toast of toasts.messages(); track toast.id) {
        <div class="toast" [class]="'toast ' + toast.kind">
          <span>{{ toast.message }}</span>
          <button type="button" aria-label="Fechar" (click)="toasts.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-stack { position:fixed; right:24px; bottom:24px; display:flex; flex-direction:column; gap:8px; z-index:1000; max-width:min(380px,calc(100vw - 32px)); }
    .toast { background:#25252c; border:1px solid #484852; border-left:3px solid var(--cyan); border-radius:9px; padding:12px 14px; color:#fff; box-shadow:0 10px 30px #0008; display:flex; gap:14px; align-items:flex-start; font-size:13px; }
    .toast.success { border-left-color:var(--green); }
    .toast.error { border-left-color:var(--red); }
    .toast button { background:none; border:0; color:#aaa; font-size:16px; line-height:1; padding:0; }
  `]
})
export class ToastComponent {
  readonly toasts = inject(ToastService);
}
