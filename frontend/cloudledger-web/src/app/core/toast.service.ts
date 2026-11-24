import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info';

export interface ToastMessage {
  id: number;
  message: string;
  kind: ToastKind;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly timer = new Map<number, ReturnType<typeof setTimeout>>();

  readonly messages = signal<ToastMessage[]>([]);

  show(message: string, kind: ToastKind = 'info'): void {
    const id = this.nextId++;
    this.messages.update(items => [...items, { id, message, kind }]);
    this.timer.set(id, setTimeout(() => this.dismiss(id), 4000));
  }

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  dismiss(id: number): void {
    const timer = this.timer.get(id);
    if (timer) {
      clearTimeout(timer);
      this.timer.delete(id);
    }
    this.messages.update(items => items.filter(item => item.id !== id));
  }
}
