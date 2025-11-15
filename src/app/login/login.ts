import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { AuthService } from '../api/auth.service';
import { HttpErrorResponse } from '@angular/common/http';
import { MembersService } from '../api/members.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class Login {
  // 👇 أولاً: حقن الخدمات (قبل أي استخدام لها)
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private membersApi = inject(MembersService);

  // 🔹 فورم تسجيل الدخول
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    rememberMe: [true]
  });

  // 🔹 فورم إنشاء حساب (نفس فورم إضافة عضو)
  showRegister = false;

  registerForm = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    name: [''],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    password: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(6)]],
  });

  registerErrors: string[] = [];
  isRegistering = false;

  readonly year = new Date().getFullYear();

  apiErrors = signal<string[]>([]);
  apiError = computed(() => this.apiErrors()[0] ?? null);

  showPassword = signal(false);
  isSubmitting = signal(false);

  canSubmit = computed(() => this.form.valid && !this.isSubmitting());

  constructor() {
    // مجرد لوج للفورم
    this.form.statusChanges.subscribe(() => {
      console.log(
        'form.valid =', this.form.valid,
        'email.errors =', this.form.controls.email.errors,
        'password.errors =', this.form.controls.password.errors,
        'isSubmitting =', this.isSubmitting()
      );
    });
  }

  // فتح/إغلاق واجهة التسجيل
  openRegister() {
    this.showRegister = true;
    this.registerErrors = [];
  }

  cancelRegister() {
    this.showRegister = false;
    this.registerErrors = [];
  }

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  // تجميع رسائل خطأ الباك لتسجيل الدخول
  private collectBackend(e: HttpErrorResponse): string[] {
    if (e?.error?.errors && typeof e.error.errors === 'object') {
      const out: string[] = [];
      for (const k of Object.keys(e.error.errors)) {
        const arr = e.error.errors[k];
        if (Array.isArray(arr)) arr.forEach((m: string) => out.push(`${k}: ${m}`));
      }
      if (out.length) return out;
    }
    if (Array.isArray(e?.error?.messages)) return e.error.messages;
    const single = e?.error?.message || e?.error?.title || e?.error?.detail || e?.message;
    return single ? [single] : ['فشل تسجيل الدخول.'];
  }

  // 🔹 تسجيل الدخول العادي
  onLogin(): void {
    this.apiErrors.set([]);

    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const { email, password, rememberMe } = this.form.getRawValue();

    this.auth.login({ email, password }, rememberMe).subscribe({
      next: (res) => {
        console.log('[Login] success response:', res);
        this.isSubmitting.set(false);

        if (!res || res.success === false) {
          this.apiErrors.set([res?.message || 'فشل تسجيل الدخول.']);
          return;
        }

        sessionStorage.setItem('sessionLogin', '1');
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/books';
        this.router.navigateByUrl(returnUrl);
      },
      error: (err: HttpErrorResponse) => {
        console.error('[Login] error response:', err);
        this.apiErrors.set(this.collectBackend(err));
        this.isSubmitting.set(false);
      }
    });
  }

  // 🔹 إنشاء عضو جديد + تسجيل دخول تلقائي
  onRegister() {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    const v = this.registerForm.getRawValue();
    if (v.password !== v.confirmPassword) {
      this.registerForm.controls.confirmPassword.setErrors({ mismatch: true });
      return;
    }

    this.isRegistering = true;
    this.registerErrors = [];

    // 🔸 نفس الشي اللي يصير عند زر "إضافة عضو" (publicRegister)
    this.membersApi.publicRegister(v).subscribe({
      next: () => {
        // بعد إنشاء العضو، نسوي Login بنفس إيميل/باسورد التسجيل
        this.auth.login({ email: v.email!, password: v.password! }, true).subscribe({
          next: (res) => {
            this.isRegistering = false;
            if (!res || res.success === false) {
              this.registerErrors = [res?.message || 'تم إنشاء الحساب لكن فشل تسجيل الدخول. حاول الدخول يدويًا.'];
              return;
            }

            sessionStorage.setItem('sessionLogin', '1');
            this.showRegister = false;

            const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/books';
            this.router.navigateByUrl(returnUrl);
          },
          error: (e) => {
            this.isRegistering = false;
            this.registerErrors = ['تم إنشاء الحساب لكن فشل تسجيل الدخول. حاول الدخول يدويًا.'];
            console.error(e);
          }
        });
      },
      error: (e) => {
        this.isRegistering = false;
        const src = e?.error ?? e;
        if (src?.errors) {
          const msgs: string[] = [];
          for (const k of Object.keys(src.errors)) {
            const arr = src.errors[k];
            if (Array.isArray(arr)) msgs.push(...arr.map(String));
          }
          this.registerErrors = msgs.length ? msgs : ['تعذر إنشاء الحساب.'];
        } else if (src?.message) {
          this.registerErrors = [src.message];
        } else {
          this.registerErrors = ['تعذر إنشاء الحساب.'];
        }
      }
    });
  }
}
