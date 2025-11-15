// src/app/members/member-form.ts
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

@Component({
  standalone: true,
  selector: 'app-member-form',
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <!-- بانر أخطاء الباك -->
    <div *ngIf="backendErrors().length" class="error-messages" role="alert" aria-live="assertive" style="margin-bottom:10px">
      <ul><li *ngFor="let e of backendErrors()">{{ e }}</li></ul>
    </div>

    <form (ngSubmit)="submit()" novalidate>
      <label [class.err]="form.controls.name.touched && form.controls.name.invalid">
        الاسم
        <input type="text" [formControl]="form.controls.name" placeholder="الاسم الكامل" />
      </label>

      <label [class.err]="form.controls.email.touched && form.controls.email.invalid">
        الإيميل
        <input type="email" [formControl]="form.controls.email" placeholder="name@example.com" />
      </label>

      <label>
        الهاتف
        <input type="text" [formControl]="form.controls.phone" placeholder="09xxxxxxxx" />
      </label>

      <div class="actions" style="margin-top:10px; display:flex; gap:8px">
        <button type="button" class="ghost" (click)="cancel.emit()">إلغاء</button>
        <button type="submit" class="primary">حفظ</button>
      </div>
    </form>
  `
})

export class MemberFormComponent {
  private fb = inject(FormBuilder);

  // رسائل الباك (تعرض كبانر)
  backendErrors = signal<string[]>([]);

  // نموذج: name/email non-nullable، phone يسمح بـ null
  form = this.fb.group({
    name:  this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]),
    email: this.fb.nonNullable.control('', [Validators.required, Validators.email]),
    phone: this.fb.control<string | null>(null)
  });

  // تحميل قيمة ابتدائية
  @Input() set value(v: { name?: string; email?: string; phone?: string | null } | null) {
    if (v) {
      this.form.reset({
        name:  v.name  ?? '',
        email: v.email ?? '',
        phone: v.phone ?? null
      });
    }
  }
  
  // تمرير أخطاء الباك من الأب (اختياري)
  @Input() set errors(errs: string[] | null) {
    this.setBackendErrors(errs ?? []);
  }

  @Output() save = new EventEmitter<{ name: string; email: string; phone: string | null }>();
  @Output() cancel = new EventEmitter<void>();

  // استقبال أخطاء الباك وتوزيعها (تلوين الكنترولات إن أمكن)
  setBackendErrors(errs: string[] = []) {
    this.backendErrors.set(errs);

    // إعادة إبراز الفاليديشن حتى تظهر الرسائل الحمراء المحلية إن وُجدت
    this.form.markAllAsTouched();

    // محاولة بسيطة لمعرفة الحقل المقصود من النص
    const hit = (k: string) => errs.some(e => new RegExp(`\\b${k}\\b`, 'i').test(e) || e.includes(''+k) );
    if (hit('name') || hit('الاسم')) {
      const c = this.form.controls.name;
      c.setErrors({ ...(c.errors||{}), backend: true });
    }
    if (hit('email') || hit('mail') || hit('الإيميل')) {
      const c = this.form.controls.email;
      c.setErrors({ ...(c.errors||{}), backend: true });
    }
    if (hit('phone') || hit('mobile') || hit('الهاتف')) {
      const c = this.form.controls.phone!;
      c.setErrors({ ...(c.errors||{}), backend: true });
    }
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const val = this.form.getRawValue(); // { name: string, email: string, phone: string|null }
    this.save.emit(val);
  }
}
