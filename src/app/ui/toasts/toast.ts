import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from './toast.service';

@Component({
  standalone: true,
  selector: 'app-toast',
  imports: [CommonModule],
  template: `
  <div class="toast-wrap" aria-live="polite" aria-atomic="true" dir="rtl">
    <div class="toast" *ngFor="let m of toast.messages()"
         [class.ok]="m.kind==='success'"
         [class.err]="m.kind==='error'"
         [class.inf]="m.kind==='info'"
         (click)="toast.dismiss(m.id)">
      {{ m.text }}
    </div>
  </div>`,
  styles: [`
    .toast-wrap{ position:fixed; inset-inline-end:16px; inset-block-start:16px; display:flex; flex-direction:column; gap:8px; z-index:9999 }
    .toast{ background:#111827; color:#fff; padding:10px 14px; border-radius:10px; box-shadow:0 4px 14px rgba(0,0,0,.15); cursor:pointer }
    .toast.ok{ background:#065f46 }
    .toast.err{ background:#7f1d1d }
    .toast.inf{ background:#1f2937 }
    .toast-wrap {
      position: fixed;
      top: 16px;       /* أعلى الصفحة */
      right: 16px;     /* أعلى يمين */
      display: flex;
      flex-direction: column;
      gap: 8px;
      z-index: 99999;  /* خليها أعلى من أي بطاقة أو modal */
    }


  `]
})
export class ToastComponent {
  toast = inject(ToastService);
}
