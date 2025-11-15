import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ResetService } from '../api/reset.service';
import { finalize } from 'rxjs/operators'; // ⬅️ أضِف الاستيراد

@Component({
  standalone: true,
  selector: 'app-reset-code',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
  <div class="stage" dir="rtl">
    <section class="card" role="form" aria-labelledby="rcode-title">
      <header class="head">
        <div class="brand">
          <div class="logo" aria-hidden="true">🔑</div>
          <div>
            <h2 id="rcode-title">أدخل رمز التحقق</h2>
            <p class="sub">أرسلنا رمزًا مكوّنًا من ٦ خانات إلى بريدك.</p>
          </div>
        </div>
        <!-- مؤشّر التقدم (2/3) -->
        <div class="steps" aria-hidden="true">
          <span class="dot"></span>
          <span class="dot active"></span>
          <span class="dot"></span>
        </div>
      </header>

      <form class="form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field" [class.invalid]="invalid()">
          <label for="code">الرمز (٦ خانات)</label>
          <div class="input-wrap">
            <input
              id="code"
              class="input"
              inputmode="numeric"
              maxlength="6"
              formControlName="code"
              placeholder="123456"
              [attr.aria-invalid]="invalid()"
            />
            <span class="icon">#</span>
          </div>
          <small class="hint" *ngIf="invalid()">الرمز يجب أن يكون ٦ أرقام.</small>
        </div>

        <div class="actions">
          <a routerLink="/forgot-password" class="link">تغيير البريد</a>
          <button class="btn primary" type="submit" [disabled]="form.invalid || loading()">
            <ng-container *ngIf="!loading(); else loadingTpl">متابعة</ng-container>
          </button>
        </div>

        <ng-template #loadingTpl>
          <span class="spinner" aria-hidden="true"></span>
          جاري التحقق...
        </ng-template>

        <div class="msg" [class.ok]="ok()" [class.err]="!ok()" *ngIf="msg()">{{ msg() }}</div>
      </form>

      <div class="links-bottom">
        <a routerLink="/login" class="link">العودة لتسجيل الدخول</a>
      </div>
    </section>
  </div>
  `,
  styles: [`
  /* ===== موبايل أولاً: خلفية + بطاقة زجاجية ===== */
  .stage{
    min-height:100dvh; display:flex; align-items:center; justify-content:center;
    padding:16px;
    background:
      radial-gradient(900px 420px at 100% -10%, rgba(124,58,237,.18), transparent 55%),
      radial-gradient(700px 320px at -10% 0%, rgba(109,124,255,.14), transparent 60%),
      linear-gradient(180deg, #0b1020 0%, #0b1226 100%);
  }
  .card{
    width: clamp(92vw, 92vw, 640px);
    color:#e7ecf7;
    border-radius:18px;
    background: linear-gradient(180deg, rgba(255,255,255,.09), rgba(255,255,255,.05));
    border:1px solid rgba(255,255,255,.14);
    box-shadow:0 18px 54px rgba(0,0,0,.35), inset 0 1px 0 rgba(255,255,255,.08);
    backdrop-filter: blur(8px);
    padding: clamp(14px, 4vw, 22px);
    overflow:hidden;
  }

  /* رأس + مؤشّر الخطوات */
  .head{ display:flex; align-items:flex-start; justify-content:space-between; gap:10px; margin-bottom:8px; }
  .brand{ display:flex; align-items:center; gap:10px; }
  .logo{ font-size:clamp(20px, 4vw, 26px); filter: drop-shadow(0 0 10px rgba(124,58,237,.5)); }
  h2{ margin:0; font-size:clamp(1.05rem, 2.8vw, 1.25rem); font-weight:800; }
  .sub{ margin:.25rem 0 0; color:#9aa4bd; font-size:clamp(.9rem, 2.4vw, .95rem); }
  .steps{ display:flex; gap:8px; margin-top:6px; }
  .dot{ width:8px; height:8px; border-radius:50%; background:#334155; box-shadow:0 0 0 1px rgba(255,255,255,.08) inset; }
  .dot.active{ background:#4f46e5; box-shadow:0 0 0 6px rgba(79,70,229,.18); }

  /* الفورم */
  .form{ display:grid; gap:14px; margin-top:6px; padding-inline:6px; }
  .field label{ display:block; margin:6px 0 8px; color:#e9edf8; font-weight:700; font-size:clamp(.95rem,2.6vw,1rem); }
  .input-wrap{ position:relative; }

  /* الهوية: تدرّج رمادي + حد أحمر + فوكس بنفسجي */
  .input{
    width:100%; box-sizing:border-box;
    height: clamp(44px, 6.2vh, 52px);
    padding: 12px 44px 12px 12px;
    border-radius:12px;
    border:1px solid #dc2626;
    background: linear-gradient(180deg, #aab3c7, #e2e2e3);
    color:#0f172a; outline:none;
    font-size: clamp(.95rem, 2.8vw, 1rem);
  }
  .input::placeholder{ color:#334155; opacity:.85; }
  .input:focus{ border-color:#4338ca; box-shadow:0 0 0 4px rgba(79,70,229,.18); }
  .field.invalid .input{ border-color:#ef4444; }

  .icon{
    position:absolute; right:12px; top:50%; translate:0 -50%;
    color:#0f172a; opacity:.7; pointer-events:none;
    font-size:clamp(.95rem,2.8vw,1rem);
  }

  .hint{ color:#fca5a5; font-size:.85rem; margin-top:6px; }

  /* أزرار وروابط */
  .actions{ display:flex; flex-direction:column; gap:12px; margin-top:4px; }
  .link{ color:#c7d2fe; text-decoration:underline; text-align:center; }
  .link:hover{ color:#ffffff; }

  .btn{
    width:100%;
    height: clamp(44px, 6vh, 50px);
    padding:0 16px;
    border-radius:14px;
    border:1px solid #dc2626;
    background: linear-gradient(180deg, #aab3c7, #e2e2e3);
    color:#111827; cursor:pointer;
    transition: transform .12s ease, box-shadow .12s ease, opacity .12s;
    font-size: clamp(.95rem, 2.6vw, 1rem);
  }
  .btn.primary{ border-color:#4338ca; }
  .btn:hover{ transform: translateY(-1px); box-shadow:0 8px 22px rgba(0,0,0,.22); }
  .btn:active{ transform: translateY(0); }
  .btn:disabled{ opacity:.6; cursor:not-allowed; box-shadow:none; }

  .msg{ margin-top:6px; padding:10px; border-radius:12px; }
  .msg.ok{ background:#ecfdf5; border:1px solid #10b981; color:#064e3b; }
  .msg.err{ background:#fef2f2; border:1px solid #ef4444; color:#7f1d1d; }

  .links-bottom{ margin-top:10px; text-align:center; }

  /* سبينر صغير */
  .spinner{
    display:inline-block; width:16px; height:16px; margin-inline-end:8px;
    border-radius:50%; border:2px solid rgba(0,0,0,.28); border-top-color:#4338ca;
    animation: spin .8s linear infinite;
  }
  @keyframes spin{ to{ transform: rotate(360deg); } }

  /* ≥ 480px: صف واحد */
  @media (min-width: 480px){
    .card{ width: clamp(86vw, 72vw, 640px); }
    .actions{ flex-direction:row; align-items:center; }
    .btn{ width:auto; min-width:160px; }
    .link{ text-align:start; }
  }
  /* ≥ 768px: مسافات أكبر */
  @media (min-width: 768px){
    .card{ padding: 22px 26px; }
    .form{ gap:16px; padding-inline:8px; }
  }
  /* تقليل الحركة */
  @media (prefers-reduced-motion: reduce){
    .btn, .spinner{ transition:none; animation:none; }
  }
  `]
})

export class ResetCodeComponent {
  private fb = inject(FormBuilder);
  private api = inject(ResetService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  resetId = signal<string>('');        // من query param
  loading = signal(false);
  msg = signal<string|null>(null);
  ok  = signal(true);

  form = this.fb.group({ code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]] });
  invalid = () => this.form.controls.code.invalid && this.form.controls.code.touched;

  constructor() {
    const id = this.route.snapshot.queryParamMap.get('id');
    if (id) this.resetId.set(id);
  }

submit(){
  if (this.form.invalid){ this.form.markAllAsTouched(); return; }
  this.loading.set(true); this.msg.set(null);

  this.api.verifyCode({
    resetId: this.resetId(),
    code: this.form.value.code!
  })
  .pipe(finalize(() => this.loading.set(false)))  // ⬅️ المهم
  .subscribe({
    next: res => {
      this.ok.set(true);
      this.msg.set(res.message || 'تم التحقق من الرمز.');
      this.router.navigate(['/reset/change'], {
        queryParams: { id: this.resetId(), code: this.form.value.code! }
      });
    },
    error: err => {
      this.ok.set(false);
      this.msg.set(err?.error?.message || 'الرمز غير صالح أو منتهي.');
    }
    // ⚠️ لا تضع complete هنا، مو لازم.
  });

  }
}
