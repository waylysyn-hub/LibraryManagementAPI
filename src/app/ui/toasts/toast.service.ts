import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info';
export interface ToastMsg { kind: ToastKind; text: string; id: number; }

@Injectable({ providedIn: 'root' })
export class ToastService {
  messages = signal<ToastMsg[]>([]);
  #id = 1;

  push(kind: ToastKind, text: string, timeoutMs = 3500) {
    const msg: ToastMsg = { kind, text, id: this.#id++ };
    this.messages.update(list => [...list, msg]);
    if (timeoutMs > 0) {
      setTimeout(() => this.dismiss(msg.id), timeoutMs);
    }
  }

  dismiss(id: number) {
    this.messages.update(list => list.filter(m => m.id !== id));
  }

  success(t: string){ this.push('success', t); }
  error(t: string){ this.push('error', t); }
  info(t: string){ this.push('info', t); }
}
