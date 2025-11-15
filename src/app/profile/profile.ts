import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AccountService, ProfileDto } from '../api/account.service';

@Component({
  standalone: true,
  selector: 'app-profile',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './profile.html',
  styleUrls: ['./profile.css']
})
export class ProfileComponent {
  private fb = inject(FormBuilder);
  private api = inject(AccountService);

  loading = signal(true);
  saving  = signal(false);
  errors: string[] = [];
  successMsg: string | null = null;

  form = this.fb.nonNullable.group({
    name:  [''],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
  });

  constructor() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.errors = [];
    this.api.getMe().subscribe({
      next: (p) => {
        this.form.reset({
          name:  p.name ?? '',
          email: p.email,
          phone: p.phone ?? '',
        });
        this.loading.set(false);
      },
      error: (e) => {
        this.loading.set(false);
        this.errors = [e?.error?.message || e?.message || 'تعذر تحميل البيانات.'];
      }
    });
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const dto: ProfileDto = {
      name: v.name?.trim() || null,
      email: v.email.trim(),
      phone: (v.phone?.trim() || null) as string | null,
    };

    this.saving.set(true);
    this.errors = [];
    this.successMsg = null;

    this.api.updateMe(dto).subscribe({
      next: () => {
        this.saving.set(false);
        this.successMsg = 'تم حفظ التعديلات بنجاح.';
      },
      error: (e) => {
        this.saving.set(false);

        const src = e?.error ?? e;
        // معالجة ValidationProblemDetails
        if (src?.errors) {
          const msgs: string[] = [];
          for (const key of Object.keys(src.errors)) {
            const arr = src.errors[key];
            if (Array.isArray(arr)) msgs.push(...arr.map(String));
          }
          this.errors = msgs.length ? msgs : ['تعذر حفظ التعديلات.'];
        } else if (src?.message) {
          this.errors = [src.message];
        } else {
          this.errors = ['تعذر حفظ التعديلات.'];
        }

        // خطأ Email من الباك
        if (src?.errors?.Email) {
          this.form.controls.email.setErrors({
            ...(this.form.controls.email.errors || {}),
            backend: src.errors.Email[0],
          });
        }
      }
    });
  }
}
