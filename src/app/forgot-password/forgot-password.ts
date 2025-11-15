import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ResetService } from '../api/reset.service';
import { finalize } from 'rxjs/operators';

@Component({
  standalone: true,
  selector: 'app-forgot-password',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="stage" dir="rtl">
      <section class="card" role="form" aria-labelledby="fp-title">
        <header class="head">
          <div class="brand">
            <div class="logo" aria-hidden="true">🔐</div>
            <div>
              <h2 id="fp-title">استعادة الوصول</h2>
              <p class="sub">أدخل بريدك لإرسال رمز التحقق</p>
            </div>
          </div>

          <!-- مؤشّر التقدم (1/3) -->
          <div class="steps" aria-hidden="true">
            <span class="dot active"></span>
            <span class="dot"></span>
            <span class="dot"></span>
          </div>
        </header>

        <form class="form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field" [class.invalid]="invalid()">
            <label for="email">البريد الإلكتروني</label>
            <div class="input-wrap">
              <input
                id="email"
                class="input"
                type="email"
                formControlName="email"
                placeholder="name@example.com"
                autocomplete="email"
                inputmode="email"
                [attr.aria-invalid]="invalid()"
                [attr.aria-describedby]="invalid() ? 'email-hint' : null"
              />
              <span class="icon">@</span>
            </div>
            <small id="email-hint" class="hint" *ngIf="invalid()">الرجاء إدخال بريد إلكتروني صالح.</small>
          </div>

          <div class="actions">
            <a routerLink="/login" class="link">العودة لتسجيل الدخول</a>

            <button class="btn primary" type="submit" [disabled]="form.invalid || loading()">
              <ng-container *ngIf="!loading(); else loadingTpl">إرسال الرمز</ng-container>
            </button>
          </div>

          <ng-template #loadingTpl>
            <span class="spinner" aria-hidden="true"></span>
            جاري الإرسال...
          </ng-template>

          <div class="msg" [class.ok]="ok()" [class.err]="!ok()" *ngIf="msg()">{{ msg() }}</div>
          <div class="msg ok dev" *ngIf="devCode()">رمز التطوير: <b>{{ devCode() }}</b></div>
        </form>
      </section>
    </div>
  `,
  styles: [`
    /* خلفية مشهدية مع توهج بنفسجي/أزرق خفيف */
    .stage{
      min-height:100dvh;
      display:flex; align-items:center; justify-content:center;
      padding:24px 12px;
      background:
        radial-gradient(1200px 600px at 100% -10%, rgba(124,58,237,.20), transparent 55%),
        radial-gradient(900px 400px at -10% 0%, rgba(109,124,255,.18), transparent 60%),
        linear-gradient(180deg, #0b1020 0%, #0b1226 100%);
    }

    /* البطاقة الزجاجية */
    .card{
      width:100%;
      max-width:560px;
      color:#e7ecf7;
      border-radius:20px;
      background: linear-gradient(180deg, rgba(255,255,255,.09), rgba(255,255,255,.05));
      border:1px solid rgba(255,255,255,.14);
      box-shadow: 0 30px 80px rgba(0,0,0,.35), inset 0 1px 0 rgba(255,255,255,.08);
      backdrop-filter: blur(8px);
      padding:22px;
    }

    /* الرأس */
    .head{ display:flex; align-items:flex-start; justify-content:space-between; gap:12px; margin-bottom:10px; }
    .brand{ display:flex; align-items:center; gap:12px; }
    .logo{ font-size:28px; filter: drop-shadow(0 0 10px rgba(124,58,237,.55)); }
    h2{ margin:0; font-size:1.35rem; font-weight:800; letter-spacing:.2px; }
    .sub{ margin:2px 0 0; color:#9aa4bd; font-size:.95rem; }

    /* مؤشّر الخطوات */
    .steps{ display:flex; gap:8px; margin-top:6px; }
    .dot{ width:9px; height:9px; border-radius:50%; background:#334155; box-shadow:0 0 0 1px rgba(255,255,255,.08) inset; }
    .dot.active{ background:#4f46e5; box-shadow:0 0 0 6px rgba(79,70,229,.18); }

    /* النموذج */
    .form{ display:grid; gap:16px; margin-top:8px; }

    .field label{ display:block; margin:6px 0 8px; color:#e9edf8; font-weight:700; }
    .input-wrap{ position:relative; }

    /* ★ هوية الأزرار والحقول حسب تفضيلاتك */
    .input{
      width:91%; padding:12px 44px 12px 12px;
      border-radius:12px;
      border:1px solid #dc2626;                 /* حد أحمر */
      background: linear-gradient(180deg, #aab3c7, #e2e2e3); /* التدرج الرمادي */
      color:#0f172a; outline:none;
    }
    .input::placeholder{ color:#334155; opacity:.85; }
    .input:focus{
      border-color:#4338ca;                      /* primary-600 */
      box-shadow:0 0 0 6px rgba(79,70,229,.18);  /* ring */
    }
    .field.invalid .input{ border-color:#ef4444; } /* خطأ */

    .icon{
      position:absolute; right:12px; top:50%; translate:0 -50%;
      color:#0f172a; opacity:.7; pointer-events:none;
    }

    .hint{ color:#fca5a5; font-size:.85rem; margin-top:6px; }

    .actions{ display:flex; align-items:center; justify-content:space-between; gap:12px; margin-top:2px; }
    .link{ color:#c7d2fe; text-decoration:underline; }
    .link:hover{ color:#ffffff; }

    .btn{
      padding:12px 16px; border-radius:14px;
      border:1px solid #dc2626;
      background: linear-gradient(180deg, #aab3c7, #e2e2e3);
      color:#111827; cursor:pointer;
      transition: transform .12s ease, box-shadow .12s ease, opacity .12s;
    }
    .btn.primary{ border-color:#4338ca; }
    .btn:hover{ transform: translateY(-1px); box-shadow:0 8px 22px rgba(0,0,0,.22); }
    .btn:active{ transform: translateY(0); }
    .btn:disabled{ opacity:.6; cursor:not-allowed; box-shadow:none; }

    /* الرسائل */
    .msg{ margin-top:6px; padding:10px; border-radius:12px; }
    .msg.ok{ background:#ecfdf5; border:1px solid #10b981; color:#064e3b; }
    .msg.err{ background:#fef2f2; border:1px solid #ef4444; color:#7f1d1d; }
    .dev{ color:#0f5132; }

    /* سبينر صغير داخل الزر */
    .spinner{
      display:inline-block; width:16px; height:16px; margin-inline-end:8px;
      border-radius:50%; border:2px solid rgba(0,0,0,.28); border-top-color:#4338ca;
      animation: spin .8s linear infinite;
    }
    @keyframes spin{ to{ transform: rotate(360deg); } }
    @media (min-width: 480px){
  .actions{ flex-direction:row; }       /* الأزرار وروابط بنفس السطر */
  .btn{ width:auto; min-width:160px; }  /* زر ثابت العرض */
  .link{ text-align:start; align-self:center; }
}
/* ===== موبايل أولاً ===== */
.stage{
  min-height:100dvh; display:flex; align-items:center; justify-content:center;
  padding: max(12px, env(safe-area-inset-top)) max(12px, env(safe-area-inset-right))
           max(12px, env(safe-area-inset-bottom)) max(12px, env(safe-area-inset-left));
  background:
    radial-gradient(900px 420px at 100% -10%, rgba(124,58,237,.18), transparent 55%),
    radial-gradient(700px 320px at -10% 0%, rgba(109,124,255,.14), transparent 60%),
    linear-gradient(180deg, #0b1020 0%, #0b1226 100%);
}

/* كارت مرن: العرض بين 92vw و 640px */
.card{
  width: clamp(92vw, 92vw, 640px);
  color:#e7ecf7;
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(255,255,255,.09), rgba(255,255,255,.05));
  border: 1px solid rgba(255,255,255,.14);
  box-shadow: 0 18px 54px rgba(0,0,0,.35), inset 0 1px 0 rgba(255,255,255,.08);
  backdrop-filter: blur(8px);
  padding: clamp(14px, 4vw, 22px);
  overflow: hidden;
}

/* رأس الكارت */
.head{ display:flex; align-items:flex-start; justify-content:space-between; gap:10px; margin-bottom:8px; }
.brand{ display:flex; align-items:center; gap:10px; }
.logo{ font-size:clamp(20px, 4vw, 26px); filter: drop-shadow(0 0 10px rgba(124,58,237,.5)); }
h2{ margin:0; font-size:clamp(1.05rem, 2.8vw, 1.25rem); font-weight:800; }
.sub{ margin:.25rem 0 0; color:#9aa4bd; font-size:clamp(.9rem, 2.4vw, .95rem); }

/* المؤشر */
.steps{ display:flex; gap:8px; margin-top:6px; }
.dot{ width:8px; height:8px; border-radius:50%; background:#334155; box-shadow:0 0 0 1px rgba(255,255,255,.08) inset; }
.dot.active{ background:#4f46e5; box-shadow:0 0 0 6px rgba(79,70,229,.18); }

/* فورم */
.form{ display:grid; gap:14px; margin-top:6px; padding-inline:6px; }
.field label{ display:block; margin:6px 0 8px; color:#e9edf8; font-weight:700; font-size:clamp(.95rem,2.6vw,1rem); }
.input-wrap{ position:relative; }

/* حقل مريح للمس */
.input{
  width:100%; box-sizing:border-box;
  height: clamp(44px, 6.2vh, 52px);
  padding: 12px 44px 12px 12px; /* مكان للأيقونة */
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

/* أزرار وروابط: عمود على الموبايل */
.actions{
  display:flex; flex-direction:column; gap:12px; margin-top:4px;
}
.link{ color:#c7d2fe; text-decoration:underline; text-align:center; }
.link:hover{ color:#ffffff; }

.btn{
  width:100%;
  height: clamp(44px, 6vh, 50px);
  padding: 0 16px;
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

/* رسائل */
.msg{ margin-top:6px; padding:10px; border-radius:12px; }
.msg.ok{ background:#ecfdf5; border:1px solid #10b981; color:#064e3b; }
.msg.err{ background:#fef2f2; border:1px solid #ef4444; color:#7f1d1d; }

/* سبينر */
.spinner{ display:inline-block; width:16px; height:16px; margin-inline-end:8px;
  border-radius:50%; border:2px solid rgba(0,0,0,.28); border-top-color:#4338ca; animation:spin .8s linear infinite;
}
@keyframes spin{ to{ transform:rotate(360deg); } }

/* ===== ≥ 480px: الزر واللينك بسطر واحد ===== */
@media (min-width: 480px){
  .card{ width: clamp(86vw, 72vw, 640px); }
  .actions{ flex-direction:row; align-items:center; }
  .btn{ width:auto; min-width:160px; }
  .link{ text-align:start; }
}

/* ===== ≥ 768px: مسافات ونِسَب أكبر قليلاً ===== */
@media (min-width: 768px){
  .card{ padding: 22px 26px; }
  .form{ gap:16px; padding-inline:8px; }
}

/* تقليل الحركة */
@media (prefers-reduced-motion: reduce){
  .btn, .spinner{ transition:none; animation:none; }
}

@media (min-width: 768px){
  .stage{ padding:24px; }
  .card{ padding:24px 28px; }
  .form{ gap:16px; padding-inline:8px; }
}

/* تقليل الحركة */
@media (prefers-reduced-motion: reduce){
  .btn, .spinner{ transition:none; animation:none; }
}
  `],
})

export class ForgotPasswordComponent {
  private fb = inject(FormBuilder);
  private api = inject(ResetService);
  private router = inject(Router);

  loading = signal(false);
  msg = signal<string | null>(null);
  ok = signal(true);
  devCode = signal<string | null>(null);

  form = this.fb.group({ email: ['', [Validators.required, Validators.email]] });
  invalid = () => this.form.controls.email.invalid && this.form.controls.email.touched;

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.msg.set(null);

    const email = this.form.value.email!;

    this.api
      .checkEmail({ email })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (chk) => {
          if (!chk?.exists) {
            this.ok.set(false);
            this.msg.set('البريد غير موجود لدينا. تأكد من كتابته بشكل صحيح.');
            return;
          }
          // موجود → الآن أرسل طلب الكود
          this.loading.set(true);
          this.api
            .requestCode({ email })
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
              next: (res) => {
                this.ok.set(true);
                this.msg.set(res.message || 'تم إرسال الرمز.');
                // انقل لواجهة إدخال الكود
                this.router.navigate(['/reset/code'], { queryParams: { id: res.resetId } });
              },
              error: (err) => {
                this.ok.set(false);
                this.msg.set(err?.error?.message || 'تعذر إرسال الرمز.');
              },
            });
        },
        error: (err) => {
          this.ok.set(false);
          this.msg.set(err?.error?.message || 'تعذر التحقق من البريد حالياً.');
        },
      });
  }
}
