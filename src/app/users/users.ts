import { Component, ElementRef, ViewChild, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { UsersService, UserRow } from '../api/users.service';
import { ToastService } from '../ui/toasts/toast.service';
import { PermissionsService, PermissionDto } from '../api/permissions.service';
import { FormsModule } from '@angular/forms';
import { NgIf } from '@angular/common';

type SortField = 'Username' | 'Email' | 'RoleId' | 'CreatedAt';
type SortDir = 'asc' | 'desc';

@Component({
  standalone: true,
  selector: 'app-users',
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './users.html',
  styleUrls: ['./users.css'],
})
export class UsersComponent {
  private fb = inject(FormBuilder);
  private api = inject(UsersService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);
  private permsApi = inject(PermissionsService);
  // BEGIN: added (تخزين القيم لاستخدامها في التأكيد)
pendingCreateData: {
  username: string;
  email: string;
  phone: string | null;
  roleId: number;
} | null = null;

pendingEditData: {
  id: number;
  username: string;
  email: string;
  phone: string | null;
} | null = null;
// END: added - pending edit state

// END: added

  // صلاحيات (بدّلها لاحقاً من localStorage)
  canRead = true;
  canCreate = true;
  canUpdate = true;
  canDelete = true;
  canPerm = false;
  // حالة البيانات
  items = signal<UserRow[]>([]);
  filtered = signal<UserRow[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);
  activeCard: number | null = null;

  // بحث/فرز/ترقيم
  q = signal(''); // بحث عام (اسم/إيميل/دور)
  qName = signal(''); // فلتر عمود الاسم
  qEmail = signal(''); // فلتر عمود الإيميل
  qPhone = signal(''); // فلتر عمود الهاتف (مع توحيد الصيغة)
  sortBy = signal<SortField>('CreatedAt');
  sortDir = signal<SortDir>('desc');
  page = signal(1);
  pageSize = signal(12);
  totalPages = signal(1);
  total = signal(0);
  @ViewChild('codeDlg') codeDlg!: ElementRef<HTMLDialogElement>;
codeLoading = signal(false);
codeErrors: string[] = [];
pendingRequestId: string | null = null;
pendingEmail: string | null = null;

// نموذج رمز التحقق فقط
codeForm = this.fb.nonNullable.group({
  code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
});

  @ViewChild('permDlg') permDlg!: ElementRef<HTMLDialogElement>;
  permLoading = signal(false);
  permErrors: string[] = [];
  permUserId: number | null = null;
  allPerms = signal<PermissionDto[]>([]);
  userPermIds = signal<Set<number>>(new Set());
  selectedAddId = signal<number | null>(null);
  selectedRemoveId = signal<number | null>(null);
  // إنشاء
  @ViewChild('createDlg') createDlg!: ElementRef<HTMLDialogElement>;
  creating = signal(false);
  createErrors: string[] = [];
  createForm = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.pattern(/^(?:09\d{9}|\+963\d{9})$/)]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(6)]],
    role: ['Employee' as 'Admin' | 'Employee'],
  });

  // تعديل
  @ViewChild('editDlg') editDlg!: ElementRef<HTMLDialogElement>;
  editingId: number | null = null;
  saving = signal(false);
  editErrors: string[] = [];
  editForm = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.pattern(/^(?:09\d{9}|\+963\d{9})$/)]],
  });

  // تغيير كلمة المرور (مع تأكيد)
  @ViewChild('pwdDlg') pwdDlg!: ElementRef<HTMLDialogElement>;
  pwdUserId: number | null = null;
  changingPwd = signal(false);
  pwdErrors: string[] = [];
  pwdForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.minLength(6)]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmNewPassword: ['', [Validators.required, Validators.minLength(6)]],
  });

  constructor() {
    // افتح مودال الإنشاء لو وصلنا بـ ?open=create
    this.route.queryParamMap.subscribe((q) => {
      if (q.get('open') === 'create') this.openCreate();
    });

    // 🔧 توحيد صيغة الهاتف للبحث
    const normalizePhone = (s?: string | null) => {
      if (!s) return '';
      let x = String(s).replace(/\D+/g, '');
      // حوّل البادئات المختلفة إلى 963 ثم رقم بدون صفر بادئ
      if (x.startsWith('00963')) x = x.replace(/^00963/, '963');
      if (x.startsWith('9630')) x = x.replace(/^9630/, '963');
      if (x.startsWith('0')) x = x.replace(/^0/, '');
      if (x.startsWith('963')) return x; // 963xxxxxxxxx
      if (x.length === 10 && x.startsWith('9')) return '963' + x; // 9xxxxxxxxx -> 9639xxxxxxxx
      return x;
    };

    // فلترة/فرز/ترقيم بالفرونت
    effect(() => {
      const list = this.items();

      // مدخلات الفلترة
      const text = this.q().toLowerCase().trim(); // بحث عام
      const name = this.qName().toLowerCase().trim(); // فلتر اسم
      const email = this.qEmail().toLowerCase().trim(); // فلتر إيميل
      const phoneFilter = normalizePhone(this.qPhone().trim());

      const sb = this.sortBy();
      const sd = this.sortDir();

      // 1) استبعاد الأعضاء (Member = roleId 3)
      let x = list.filter((u) => u.roleId !== 3);

      // 2) فلاتر الأعمدة (AND)
      if (name) x = x.filter((u) => (u.username || '').toLowerCase().includes(name));
      if (email) x = x.filter((u) => (u.email || '').toLowerCase().includes(email));
      if (phoneFilter) {
        x = x.filter((u) => {
          const up = normalizePhone(u.phone ?? '');
          return up.includes(phoneFilter);
        });
      }

      // 3) البحث العام (OR على اسم/إيميل/الدور)
      if (text) {
        const _text = text;
        x = x.filter((u) => {
          const r = this.roleName(u.roleId).toLowerCase();
          return (
            (u.username || '').toLowerCase().includes(_text) ||
            (u.email || '').toLowerCase().includes(_text) ||
            r.includes(_text)
          );
        });
      }

      // 4) الفرز
      x = x.slice().sort((a, b) => {
        let av: any, bv: any;
        switch (sb) {
          case 'Username':
            av = a.username || '';
            bv = b.username || '';
            break;
          case 'Email':
            av = a.email || '';
            bv = b.email || '';
            break;
          case 'RoleId':
            av = this.roleName(a.roleId);
            bv = this.roleName(b.roleId);
            break;
          case 'CreatedAt':
          default:
            av = a.createdAt || '';
            bv = b.createdAt || '';
            break;
        }
        if (av < bv) return sd === 'asc' ? -1 : 1;
        if (av > bv) return sd === 'asc' ? 1 : -1;
        return 0;
      });

      // 5) الترقيم
      this.total.set(x.length);
      const ps = this.pageSize();
      const pages = Math.max(1, Math.ceil(x.length / ps));
      this.totalPages.set(pages);
      const p = Math.min(this.page(), pages);
      const start = (p - 1) * ps;
      this.filtered.set(x.slice(start, start + ps));
    });

    // تنظيف أخطاء backend عند تغيير حقل الهاتف (إنشاء)
    this.createForm.controls.phone.valueChanges.subscribe(() => {
      const c = this.createForm.controls.phone;
      const errs = c.errors as any;
      if (errs && (errs['backend'] || errs['backendMsg'])) {
        const copy: any = { ...errs };
        delete copy['backend'];
        delete copy['backendMsg'];
        c.setErrors(Object.keys(copy).length ? copy : null);
      }
    });

    // تنظيف أخطاء backend عند تغيير حقل الهاتف (تعديل)
    this.editForm.controls.phone.valueChanges.subscribe(() => {
      const c = this.editForm.controls.phone;
      const errs = c.errors as any;
      if (errs && (errs['backend'] || errs['backendMsg'])) {
        const copy: any = { ...errs };
        delete copy['backend'];
        delete copy['backendMsg'];
        c.setErrors(Object.keys(copy).length ? copy : null);
      }
    });
    try {
      const permissions = JSON.parse(localStorage.getItem('permissions') || '[]') as string[];
      this.canPerm = permissions.includes('user.crud');
    } catch {
      this.canPerm = false;
    }
    this.reload();
  }
  // ✅ دالة موحدة للإضافة/الإزالة حسب حالة الـ checkbox
  togglePermission(pid: number, checked: boolean) {
    const uid = this.permUserId;
    if (!uid) return;

    this.permLoading.set(true);

    const obs = checked
      ? this.permsApi.addToUser(uid, pid)
      : this.permsApi.removeFromUser(uid, pid);

    obs.subscribe({
      next: (r) => {
        const set = new Set(this.userPermIds());
        if (checked) set.add(pid);
        else set.delete(pid);
        this.userPermIds.set(set);
        this.permLoading.set(false);
        this.toast.success(r.message || (checked ? 'تمت الإضافة' : 'تمت الإزالة'));
      },
      error: (e) => {
        this.permLoading.set(false);
        this.toast.error(e?.error?.message || e?.message || 'حدث خطأ في تعديل الصلاحية');
      },
    });
  }

  // فتح مودال الصلاحيات لمستخدم معيّن
  openPerm(u: UserRow) {
    if (!this.canPerm) return;
    this.permErrors = [];
    this.permUserId = u.id;
    this.permLoading.set(true);

    // اجلب كل الصلاحيات + صلاحيات المستخدم
    Promise.all([this.permsApi.getAll().toPromise(), this.permsApi.getUser(u.id).toPromise()])
      .then(([allRes, userRes]) => {
        const all = allRes?.data ?? [];
        const owned = new Set((userRes?.data ?? []).map((x) => x.id));
        this.allPerms.set(all);
        this.userPermIds.set(owned);

        // قيَم افتراضية للـ selects
        const canAdd = all.find((p) => !owned.has(p.id));
        const canRem = all.find((p) => owned.has(p.id));
        this.selectedAddId.set(canAdd?.id ?? null);
        this.selectedRemoveId.set(canRem?.id ?? null);

        this.permLoading.set(false);
        this.permDlg?.nativeElement?.showModal();
      })
      .catch((e) => {
        this.permLoading.set(false);
        this.permErrors = [e?.error?.message || e?.message || 'تعذّر تحميل الصلاحيات'];
      });
  }

  closePerm() {
    this.permDlg?.nativeElement?.close();
    this.permUserId = null;
  }
  
  // تنفيذ إضافة
  addPerm() {
    const uid = this.permUserId,
      pid = this.selectedAddId();
    if (!this.canPerm || !uid || !pid) return;
    this.permLoading.set(true);
    this.permsApi.addToUser(uid, pid).subscribe({
      next: (r) => {
        // حدّث المجموعة محلياً
        const set = new Set(this.userPermIds());
        set.add(pid);
        this.userPermIds.set(set);

        // حدّث خيارات السليكت
        const all = this.allPerms();
        const canAdd = all.find((p) => !set.has(p.id));
        const canRem = all.find((p) => set.has(p.id));
        this.selectedAddId.set(canAdd?.id ?? null);
        this.selectedRemoveId.set(canRem?.id ?? null);

        this.permLoading.set(false);
        this.toast.success(r.message || 'تمت إضافة الصلاحية');
      },
      error: (e) => {
        this.permLoading.set(false);
        this.toast.error(e?.error?.message || e?.message || 'فشل إضافة الصلاحية');
      },
    });
  }

  // تنفيذ إزالة
  removePerm() {
    const uid = this.permUserId,
      pid = this.selectedRemoveId();
    if (!this.canPerm || !uid || !pid) return;
    this.permLoading.set(true);
    this.permsApi.removeFromUser(uid, pid).subscribe({
      next: (r) => {
        const set = new Set(this.userPermIds());
        set.delete(pid);
        this.userPermIds.set(set);

        const all = this.allPerms();
        const canAdd = all.find((p) => !set.has(p.id));
        const canRem = all.find((p) => set.has(p.id));
        this.selectedAddId.set(canAdd?.id ?? null);
        this.selectedRemoveId.set(canRem?.id ?? null);

        this.permLoading.set(false);
        this.toast.success(r.message || 'تمت إزالة/رفض الصلاحية');
      },
      error: (e) => {
        this.permLoading.set(false);
        this.toast.error(e?.error?.message || e?.message || 'فشل إزالة الصلاحية');
      },
    });
  }
  clearFilters() {
    this.q.set('');
    this.qName.set('');
    this.qEmail.set('');
    this.qPhone.set('');
    this.page.set(1);
  }

  // ===== Helpers (UI) =====
  trackById = (_: number, r: UserRow) => r.id;
  sortIcon(f: SortField) {
    const by = this.sortBy(),
      dir = this.sortDir();
    return by === f ? (dir === 'asc' ? '▲' : '▼') : '';
  }
  prev() {
    if (this.page() > 1) this.page.update((p) => p - 1);
  }
  next() {
    if (this.page() < this.totalPages()) this.page.update((p) => p + 1);
  }
  setPageSize(n: number | string) {
    const v = Number(n) || 12;
    this.pageSize.set(v);
    this.page.set(1);
  }
  toggleSort(field: SortField) {
    const by = this.sortBy();
    const dir = this.sortDir();
    this.sortBy.set(field);
    this.sortDir.set(by === field ? (dir === 'asc' ? 'desc' : 'asc') : 'asc');
  }

  // تحويل رقم الدور إلى اسم
  roleName(roleId: number): 'Admin' | 'Employee' | 'Member' | 'Unknown' {
    if (roleId === 1) return 'Admin';
    if (roleId === 2) return 'Employee';
    if (roleId === 3) return 'Member';
    return 'Unknown';
  }

  // ===== Errors mapping (Backend) =====
  private mapFieldName(raw?: string): string | undefined {
    if (!raw) return;
    const k = String(raw).toLowerCase();
    if (['username', 'user', 'name'].includes(k)) return 'username';
    if (['email', 'mail'].includes(k)) return 'email';
    if (['phone', 'mobile', 'phonenumber'].includes(k)) return 'phone';
    if (['password', 'currentpassword'].includes(k)) return 'password';
    if (['newpassword'].includes(k)) return 'newPassword';
    if (['confirmnewpassword', 'confirmpassword'].includes(k)) return 'confirmNewPassword';
    return raw;
  }

  private collectBackendErrors(form: FormGroup, e: any): string[] {
    const msgs: string[] = [];
    const src = e?.error ?? e;

    if (src?.errors && !Array.isArray(src.errors) && typeof src.errors === 'object') {
      for (const [field, arr] of Object.entries(src.errors as Record<string, any>)) {
        const msg = Array.isArray(arr) && arr.length ? String(arr[0]) : String(arr);
        msgs.push(msg);
        this.assignFieldBackendError(form, this.mapFieldName(field), msg);
      }
    }
    if (Array.isArray(src?.errors)) for (const m of src.errors) msgs.push(String(m));
    if (typeof src?.message === 'string') msgs.push(src.message);
    if (typeof src?.title === 'string') msgs.push(src.title);
    if (typeof src?.detail === 'string') msgs.push(src.detail);
    if (typeof src?.field === 'string' && msgs.length) {
      this.assignFieldBackendError(form, this.mapFieldName(src.field), msgs[0]);
    }
    if (Array.isArray(src?.messages)) msgs.push(...src.messages.map(String));
    if (!msgs.length && typeof src === 'string') msgs.push(src);
    if (!msgs.length && typeof e?.message === 'string') msgs.push(e.message);

    return Array.from(new Set(msgs.filter(Boolean)));
  }

  private assignFieldBackendError(form: FormGroup, field: string | undefined, message: string) {
    if (!field) return;
    const ctrl = (form as any).controls?.[field];
    if (ctrl) ctrl.setErrors({ ...(ctrl.errors || {}), backend: true, backendMsg: message });
  }

  isInv(form: FormGroup, c: string) {
    const ctrl = (form as any).controls[c];
    return !!ctrl && ctrl.invalid && (ctrl.dirty || ctrl.touched);
  }
  hasErr(form: FormGroup, c: string, key: string) {
    const ctrl = (form as any).controls[c];
    return !!ctrl?.errors?.[key];
  }
  getFieldMsg(form: FormGroup, c: string) {
    const ctrl = (form as any).controls[c] as any;
    return ctrl?.errors?.backendMsg || '';
  }

  // ===== API =====
  reload() {
    if (!this.canRead) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.list().subscribe({
      next: (res) => {
        const arr: UserRow[] = (res?.data ?? []).map((u) => ({
          id: u.id,
          username: u.username,
          email: u.email,
          roleId: u.roleId,
          createdAt: u.createdAt,
          phone: (u as any).phone ?? null,
        }));
        this.items.set(arr);
        this.page.set(1);
        this.loading.set(false);
      },
      error: (e) => {
        this.loading.set(false);
        const msg =
          e?.error?.message ||
          e?.error?.title ||
          e?.error?.detail ||
          e?.message ||
          'تعذّر تحميل المستخدمين';
        this.error.set(msg);
        this.toast.error(msg);
      },
    });
  }

  openCreate() {
    if (!this.canCreate) return;
    this.createErrors = [];
    this.createForm.reset({
      username: '',
      email: '',
      phone: '',
      password: '',
      confirmPassword: '',
      role: 'Employee',
    });
    this.creating.set(false);
    this.createDlg?.nativeElement?.showModal();
  }
  closeCreate() {
    this.createDlg?.nativeElement?.close();
  }

submitCreate() {
  this.createErrors = [];
  
  // نضّف أي رسائل backend قديمة من الحقول
  const clearBackendErr = (name: keyof typeof this.createForm.controls) => {
    const c = this.createForm.controls[name];
    const e: any = c.errors || {};
    if (e?.backend || e?.backendMsg) {
      delete e.backend; delete e.backendMsg;
      c.setErrors(Object.keys(e).length ? e : null);
    }
  };
  (['username','email','phone','password','confirmPassword'] as const).forEach(clearBackendErr);

  // 1) تحقّق required/format محلي برسائل مطابقة للباك
  const f = this.createForm.controls;
  let hasError = false;

  // اسم المستخدم: مطلوب + >=3 — "اسم المستخدم قصير."
  if (!f.username.value || f.username.value.trim().length < 3) {
    f.username.setErrors({ ...(f.username.errors||{}), backend: true, backendMsg: 'اسم المستخدم قصير.' });
    hasError = true;
  }

  // الإيميل: مطلوب + صيغة — "صيغة البريد الإلكتروني غير صحيحة."
  const emailVal = (f.email.value || '').trim();
  const emailOk = /^\S+@\S+\.\S+$/.test(emailVal);
  if (!emailVal || !emailOk) {
    f.email.setErrors({ ...(f.email.errors||{}), backend: true, backendMsg: 'صيغة البريد الإلكتروني غير صحيحة.' });
    hasError = true;
  }

  // الهاتف (اختياري): نمط سوري — "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام."
  const phoneVal = (f.phone.value || '').trim();
  if (phoneVal && !/^(?:09\d{9}|\+963\d{9})$/.test(phoneVal)) {
    f.phone.setErrors({ ...(f.phone.errors||{}), pattern: true, backend: true, backendMsg: 'رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.' });
    hasError = true;
  }

  // كلمة المرور: مطلوب + >=6 — "كلمة المرور يجب ألا تقل عن 6 محارف."
  const pwd = f.password.value || '';
  if (!pwd || pwd.length < 6) {
    f.password.setErrors({ ...(f.password.errors||{}), backend: true, backendMsg: 'كلمة المرور يجب ألا تقل عن 6 محارف.' });
    hasError = true;
  }

  // تأكيد كلمة المرور: مطابق — "كلمتا المرور غير متطابقتان."
  const cpw = f.confirmPassword.value || '';
  if (!cpw || cpw !== pwd) {
    f.confirmPassword.setErrors({ ...(f.confirmPassword.errors||{}), mismatch: true, backend: true, backendMsg: 'كلمتا المرور غير متطابقتان.' });
    hasError = true;
  }

  // الدور
  const roleCtrl = (this.createForm.controls as any).role;
  if (!roleCtrl?.value) {
    hasError = true; // غالباً select له قيمة افتراضية
  }

  // 1.أ — فحوصات "موجود مسبقًا" محليًا قبل الاتصال بالسيرفر
  const norm = (s: string) => s.trim().toLowerCase();
  const last9 = (p?: string|null) => {
    if (!p) return '';
    const d = String(p).replace(/\D+/g, '');
    return d.length >= 9 ? d.slice(-9) : d;
  };

  const uname = (f.username.value || '').trim();
  const phone9 = last9(phoneVal);

  // اسم المستخدم مستخدم بالفعل؟
  const usernameTaken = this.items().some(u => norm(u.username || '') === norm(uname));
  if (usernameTaken) {
    f.username.setErrors({ ...(f.username.errors||{}), backend: true, backendMsg: 'اسم المستخدم مستخدم بالفعل.' });
    hasError = true;
  }

  // البريد مستخدم بالفعل؟ (تحسين UX؛ السيرفر سيتحقق أيضًا)
  if (emailVal) {
    const emailTaken = this.items().some(u => norm(u.email || '') === norm(emailVal));
    if (emailTaken) {
      f.email.setErrors({ ...(f.email.errors||{}), backend: true, backendMsg: 'البريد الإلكتروني مستخدم بالفعل. يرجى اختيار بريد آخر.' });
      hasError = true;
    }
  }

  // رقم الهاتف مستخدم مسبقاً؟
  if (phone9) {
    const phoneTaken = this.items().some(u => last9(u.phone || '') === phone9);
    if (phoneTaken) {
      f.phone.setErrors({ ...(f.phone.errors||{}), backend: true, backendMsg: 'رقم الهاتف مستخدم مسبقاً.' });
      hasError = true;
    }
  }

  if (hasError) {
    this.createForm.markAllAsTouched();
    return; // ❌ لا ننتقل لمودال الكود
  }

  // 2) خزّن البيانات لخطوة التأكيد
  const roleId = roleCtrl.value === 'Admin' ? 1 : 2;
  this.pendingCreateData = {
    username: uname,
    email: emailVal,
    phone: phoneVal || null,
    roleId,
  };

  // 3) استدعاء register-start (قد يرجّع "البريد مستخدم" → نعرضه على الحقل)
  this.creating.set(true);
  this.api.registerStart({ email: emailVal }).subscribe({
    next: (res) => {
      this.creating.set(false);
      // نجاح: افتح مودال الكود
      this.pendingRequestId = res.requestId;
      this.pendingEmail = emailVal;
      this.codeErrors = [];
      this.codeForm.reset({ code: '' });
      this.codeDlg?.nativeElement?.showModal();
      if (res.devCode) this.toast.info(`DEV CODE: ${res.devCode}`);
    },
    error: (e) => {
      this.creating.set(false);
      // اربط رسالة الباك بحقل الإيميل إن متوفّر
      const src = e?.error ?? e;
      const msg = src?.message || src?.title || src?.detail || 'تعذّر إرسال رمز التحقق';
      const field = (src?.field || '').toString().toLowerCase();

      // لو رجع الباك "هذا البريد مستخدم بالفعل."
      if (field === 'email' || /بريد.*مستخدم|email.*exists/i.test(msg)) {
        const c = this.createForm.controls.email;
        c.setErrors({ ...(c.errors||{}), backend: true, backendMsg: msg });
      }
      this.createErrors = [msg];
      this.toast.error(msg);
      // ❌ لا نفتح مودال الكود
    },
  });
}


// BEGIN: closeCode cleanup
closeCode() {
  this.codeDlg?.nativeElement?.close();
  this.codeErrors = [];
  this.codeForm.reset({ code: '' });
}
// END: closeCode cleanup


// BEGIN: submitCreateCode (unified for create/edit)
submitCreateCode() {
  this.codeErrors = [];
  if (this.codeForm.invalid) {
    this.codeForm.markAllAsTouched();
    return;
  }
  if (!this.pendingRequestId) {
    this.codeErrors = ['طلب غير معروف. أعد الإرسال من جديد.'];
    return;
  }

  // حالة 1: تأكيد إنشاء
  if (this.pendingCreateData) {
    const v = this.createForm.getRawValue();
    if (!v.password || !v.confirmPassword) {
      this.codeErrors = ['يرجى إدخال كلمة المرور وتأكيدها.'];
      return;
    }
    if (v.password !== v.confirmPassword) {
      this.createForm.controls.confirmPassword.setErrors({ mismatch: true });
      this.codeErrors = ['كلمتا المرور غير متطابقتين.'];
      return;
    }

    const dto = {
      requestId: this.pendingRequestId,
      code: this.codeForm.value.code!,
      username: this.pendingCreateData.username,
      email: this.pendingCreateData.email,
      password: v.password!,
      confirmPassword: v.confirmPassword!,
      roleId: this.pendingCreateData.roleId,
      phone: this.pendingCreateData.phone ?? undefined,
    };

    this.codeLoading.set(true);
    this.api.registerConfirm(dto).subscribe({
      next: (res) => {
        this.codeLoading.set(false);
        this.toast.success(res.message || 'تم إنشاء المستخدم');
        this.closeCode();
        this.closeCreate();
        this.pendingRequestId = null;
        this.pendingEmail = null;
        this.pendingCreateData = null;
        this.reload();
      },
      error: (e) => {
        this.codeLoading.set(false);
        const errs = this.collectBackendErrors(this.createForm, e);
        this.codeErrors = errs.length ? errs : ['فشل تأكيد الرمز/إنشاء الحساب'];
        this.toast.error(this.codeErrors[0]);
      },
    });
    return;
  }

  // حالة 2: تأكيد تعديل
  if (this.pendingEditData) {
    const dto = {
      requestId: this.pendingRequestId,
      code: this.codeForm.value.code!,
      username: this.pendingEditData.username,
      email: this.pendingEditData.email,
      phone: this.pendingEditData.phone,
    };
    this.codeLoading.set(true);
    this.api.adminUpdateConfirm(this.pendingEditData.id, dto).subscribe({
      next: (res) => {
        this.codeLoading.set(false);
        this.toast.success(res?.message || 'تم التحديث بعد تأكيد البريد.');
        this.closeCode();
        this.closeEdit();
        this.pendingRequestId = null;
        this.pendingEmail = null;
        this.pendingEditData = null;
        this.reload();
      },
      error: (e) => {
        this.codeLoading.set(false);
        // نربط أخطاء التعديل على editForm لأن الحقول تتبع التعديل
        const errs = this.collectBackendErrors(this.editForm, e);
        this.codeErrors = errs.length ? errs : ['فشل تأكيد الرمز/إتمام التعديل'];
        this.toast.error(this.codeErrors[0]);
      },
    });
    return;
  }

  // لا إنشاء ولا تعديل؟ حالة غير متوقعة
  this.codeErrors = ['لا توجد عملية بانتظار التأكيد.'];
}
// END: submitCreateCode (unified for create/edit)



// BEGIN: resendCode (context-aware)
resendCode() {
  // إعادة إرسال لسيناريو الإنشاء
  if (this.pendingCreateData && this.pendingEmail) {
    this.creating.set(true);
    this.api.registerStart({ email: this.pendingEmail }).subscribe({
      next: (res) => {
        this.creating.set(false);
        this.pendingRequestId = res.requestId;
        if (res.devCode) this.toast.info(`DEV CODE: ${res.devCode}`);
      },
      error: (e) => {
        this.creating.set(false);
        const errs = this.collectBackendErrors(this.createForm, e);
        this.toast.error(errs[0] || 'فشل إعادة الإرسال');
      },
    });
    return;
  }

  // إعادة إرسال لسيناريو التعديل
  if (this.pendingEditData) {
    const d = this.pendingEditData;
    this.saving.set(true);
    this.api.adminUpdateStart(d.id, {
      username: d.username,
      email: d.email,
      phone: d.phone,
    }).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.pendingRequestId = res.requestId;
        if (res.devCode) this.toast.info(`DEV CODE: ${res.devCode}`);
      },
      error: (e) => {
        this.saving.set(false);
        const errs = this.collectBackendErrors(this.editForm, e);
        this.toast.error(errs[0] || 'فشل إعادة الإرسال');
      },
    });
  }
}
// END: resendCode (context-aware)


  openEdit(u: UserRow) {
    if (!this.canUpdate) return;
    this.editErrors = [];
    this.editingId = u.id;
    this.saving.set(false);
    this.api.getById(u.id).subscribe({
      next: (res) => {
        const d = res.data;
        this.editForm.reset({
          username: d.username || u.username,
          email: d.email || u.email,
          phone: (d as any).phone ?? '',
        });
        this.editDlg?.nativeElement?.showModal();
      },
      error: (_) => {
        this.editForm.reset({
          username: u.username,
          email: u.email,
          phone: (u as any).phone ?? '',
        });
        this.editDlg?.nativeElement?.showModal();
      },
    });
  }
  closeEdit() {
    this.editDlg?.nativeElement?.close();
    this.editingId = null;
  }

// BEGIN: submitEdit (admin-update-start flow)
submitEdit() {
  if (!this.editingId) return;
  if (this.editForm.invalid) {
    this.editForm.markAllAsTouched();
    return;
  }
  const raw = this.editForm.getRawValue();
  const dto = {
    username: raw.username!,
    email: raw.email!,
    phone: (raw.phone?.trim() || null) as string | null,
  };

  this.saving.set(true);
  // 1) نبدأ خطوة التحقق/الإرسال
  this.api.adminUpdateStart(this.editingId, dto).subscribe({
    next: (res) => {
      this.saving.set(false);

      // إذا الإيميل لم يتغيّر: تحديث تمّ مباشرة
      if (!res?.requiresCode) {
        this.toast.success(res?.message || 'تم التحديث.');
        this.closeEdit();
        this.reload();
        return;
      }

      // يحتاج كود: خزّن القيم وافتح مودال الكود
      this.pendingEditData = {
        id: this.editingId!,
        username: dto.username,
        email: dto.email,
        phone: dto.phone,
      };
      this.pendingRequestId = res.requestId;
      this.pendingEmail = dto.email;
      this.codeErrors = [];
      this.codeForm.reset({ code: '' });
      this.codeDlg?.nativeElement?.showModal();
      if (res.devCode) this.toast.info(`DEV CODE: ${res.devCode}`);
    },
    error: (e) => {
      this.saving.set(false);
      this.editErrors = this.collectBackendErrors(this.editForm, e);
      // لو في رسالة خاصة بالهاتف وما انعكست على الحقل
      if (!this.hasErr(this.editForm, 'phone', 'backend')) {
        const m = this.editErrors.find((x) => /هاتف|phone/i.test(x));
        if (m) this.assignFieldBackendError(this.editForm, 'phone', m);
      }
      if (!this.editErrors.length) this.editErrors = ['فشل التعديل'];
      this.toast.error(this.editErrors[0]);
    },
  });
}
// END: submitEdit (admin-update-start flow)


  openPwd(u: UserRow) {
    if (!this.canUpdate) return;
    this.pwdErrors = [];
    this.pwdUserId = u.id;
    this.changingPwd.set(false);
    this.pwdForm.reset({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
    this.pwdDlg?.nativeElement?.showModal();
  }
  closePwd() {
    this.pwdDlg?.nativeElement?.close();
    this.pwdUserId = null;
  }

  submitPwd() {
    if (!this.pwdUserId) return;
    if (this.pwdForm.invalid) {
      this.pwdForm.markAllAsTouched();
      return;
    }
    const v = this.pwdForm.getRawValue();

    if (v.newPassword !== v.confirmNewPassword) {
      this.pwdForm.controls.confirmNewPassword.setErrors({ mismatch: true });
      this.pwdForm.controls.confirmNewPassword.markAsTouched();
      return;
    }

    this.changingPwd.set(true);
    this.api
      .updatePassword(this.pwdUserId, {
        currentPassword: v.currentPassword!,
        newPassword: v.newPassword!,
        confirmNewPassword: v.confirmNewPassword!,
      })
      .subscribe({
        next: () => {
          this.changingPwd.set(false);
          this.toast.success('تم تحديث كلمة المرور');
          this.closePwd();
        },
        error: (e) => {
          this.changingPwd.set(false);
          this.pwdErrors = this.collectBackendErrors(this.pwdForm, e);
          if (!this.pwdErrors.length) this.pwdErrors = ['فشل تحديث كلمة المرور'];
          this.toast.error(this.pwdErrors[0]);
        },
      });
  }

  remove(u: UserRow) {
    if (!this.canDelete) return;
    if (!confirm(`حذف المستخدم #${u.id} (${u.username})؟`)) return;
    this.api.delete(u.id).subscribe({
      next: () => {
        this.toast.success('تم حذف المستخدم');
        this.reload();
      },
      error: (e) => {
        const errs = this.collectBackendErrors(this.editForm, e);
        const msg = errs[0] || e?.error?.message || e?.message || 'فشل الحذف';
        this.toast.error(msg);
      },
    });
  }
}
