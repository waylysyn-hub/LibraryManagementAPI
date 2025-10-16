import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { AuthService } from '../auth.service';
import { HttpErrorResponse } from '@angular/common/http';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class Login {
  readonly year = new Date().getFullYear();
  apiError = computed(() => this.apiErrors()[0] ?? null);
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    rememberMe: [true]
  });

  showPassword = signal(false);
  isSubmitting = signal(false);
  apiErrors = signal<string[]>([]);

  canSubmit = computed(() => this.form.valid && !this.isSubmitting());

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

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
    // السيرفر رجع فشل منطقي، خزّنه برسالة واجهة المستخدم
    this.apiErrors.set([res?.message || 'فشل تسجيل الدخول.']);
    return;
  }

  // إذا نجح فعلاً (success=true + token موجود)
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

  constructor() {
    this.form.statusChanges.subscribe(() => {
      console.log(
        'form.valid =', this.form.valid,
        'email.errors =', this.form.controls.email.errors,
        'password.errors =', this.form.controls.password.errors,
        'isSubmitting =', this.isSubmitting()
      );
    });
  }
}
