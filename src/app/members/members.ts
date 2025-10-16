import { Component, ViewChild, ElementRef, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MembersService, MemberDto } from '../api/members.service';
import { ToastService } from '../ui/toasts/toast.service';
import { NgIf } from '@angular/common';

type SortField = 'Name' | 'Email' | 'RegisteredAt' | 'Id';
type SortDir = 'asc' | 'desc';
type ViewMode = 'cards' | 'table';

@Component({
  standalone: true,
  selector: 'app-members',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './members.html',
  styleUrls: ['./members.css']
})
export class MembersComponent {
  private fb   = inject(FormBuilder);
  private api  = inject(MembersService);
  private toast = inject(ToastService, { optional: true });

  // صلاحيات
  canRead = true; canCreate = false; canUpdate = false; canDelete = false;

  // إشعار
  private ok(m: string){ this.toast?.success ? this.toast.success(m) : console.log(m); }
  private err(m: string){ this.toast?.error ? this.toast.error(m) : console.error(m); }

  // حالة البيانات
  items = signal<MemberDto[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(12);
  totalPages = signal(1);
  loading = signal(false);
  loadError = signal<string|null>(null);

  // إحصاءات
  statTotal = signal(0);
  statToday = signal(0);

  // فلاتر/عرض
  filters = this.fb.nonNullable.group({
    Q: [''], Name: [''], Email: [''], Phone: [''],
    RegisteredFrom: [''], RegisteredTo: [''],
    Page: [1], PageSize: [12],
    SortBy: ['RegisteredAt' as SortField], SortDir: ['desc' as SortDir],
    View: ['cards' as ViewMode]
  });

  // إنشاء
  @ViewChild('createDlg') createDlg!: ElementRef<HTMLDialogElement>;
  createForm = this.fb.nonNullable.group({
    username:        ['', [Validators.required, Validators.minLength(3)]],
    name:            [''],
    email:           ['', [Validators.required, Validators.email]],
    phone:           [''],
    password:        ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(6)]]
  });
  creating = signal(false);
  createErrors: string[] = [];

  // تعديل
  @ViewChild('editDlg') editDlg!: ElementRef<HTMLDialogElement>;
  editingId: number | null = null;
  editForm = this.fb.nonNullable.group({
    name:  ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['']
  });
  saving = signal(false);
  editErrors: string[] = [];

constructor() {
  effect(() => {
    this.totalPages.set(Math.max(1, Math.ceil(this.total() / this.pageSize())));
  });

  // الصلاحيات من التخزين
  try {
    const permissions = JSON.parse(localStorage.getItem('permissions') || '[]') as string[]; // جلب الأذونات
    const role = (localStorage.getItem('role') || '').toLowerCase(); // جلب الدور من localStorage

    // تسجيل القيم للتحقق منها
    console.log('Permissions:', permissions);
    console.log('Role:', role);

    // دالة للتحقق من الصلاحية
    const has = (perm: string) => {
      // هنا نتأكد من وجود الصلاحية في الأذونات للمستخدم
      return permissions.includes(perm); // تحقق من أن الصلاحية موجودة
    };

    // تحديث الصلاحيات للمستخدم بناءً على الأذونات
    this.canRead   = has('member.read');
    this.canCreate = has('member.create');
    this.canUpdate = has('member.update');
    this.canDelete = has('member.delete');
    
    // تسجيل القيم بعد التحديث
    console.log('canRead:', this.canRead);
    console.log('canCreate:', this.canCreate);
    console.log('canUpdate:', this.canUpdate);
    console.log('canDelete:', this.canDelete);
  } catch (error) {
    console.error('Error while parsing permissions or role:', error);
  }

  this.filters.valueChanges.subscribe(() => { this.page.set(1); this.reload(); });
  this.reload();
}


  // ─────────────── تطبيع الهاتف + أخطاء الباك ───────────────
  private normalizeMember = (m: any): MemberDto => ({
    id: m.id,
    userId: m.userId,
    name: m.name,
    email: m.email,
    phone: m.phone ?? m.phoneNumber ?? m.userPhone ?? m.user?.phone ?? null,
    registeredAt: m.registeredAt
  });

  private mapFieldName(raw?: string): string | undefined {
    if (!raw) return;
    const k = String(raw).toLowerCase();
    if (['name','fullname'].includes(k))  return 'name';
    if (['email','mail'].includes(k))     return 'email';
    if (['phone','mobile','phonenumber'].includes(k))   return 'phone';
    return raw;
  }

  private assignFieldBackendError(form: any, field: string | undefined, message: string){
    if (!field) return;
    const ctrl = form.controls?.[field];
    if (ctrl) ctrl.setErrors({ backend: message, ...(ctrl.errors||{}) });
  }

  private serverMessage(e: any): string {
    const src = e?.error ?? e;
    if (src && typeof src === 'object') {
      if (typeof src.message === 'string') return src.message;
      if (typeof src.title   === 'string') return src.title;
      if (typeof src.detail  === 'string') return src.detail;
      if (src.errors && typeof src.errors === 'object') {
        const firstKey = Object.keys(src.errors)[0];
        const arr = (src.errors as any)[firstKey];
        if (Array.isArray(arr) && arr.length) return String(arr[0]);
      }
    }
    if (typeof src === 'string') return src;
    if (typeof e?.message === 'string') return e.message;
    return 'حدث خطأ غير متوقع';
  }

  private collectBackendErrors(form: any, e: any): string[] {
    const msgs: string[] = [];
    const src = e?.error ?? e;

    // ValidationProblemDetails
    if (src?.errors && typeof src.errors === 'object') {
      for (const [field, arr] of Object.entries(src.errors as Record<string, any>)) {
        const msg = Array.isArray(arr) && arr.length ? String(arr[0]) : String(arr);
        msgs.push(msg);
        this.assignFieldBackendError(form, this.mapFieldName(field), msg);
      }
    }

    // { message/title/detail, field? }
    if (typeof src?.message === 'string') msgs.push(src.message);
    if (typeof src?.title   === 'string') msgs.push(src.title);
    if (typeof src?.detail  === 'string') msgs.push(src.detail);
    if (typeof src?.field   === 'string' && msgs.length) {
      this.assignFieldBackendError(form, this.mapFieldName(src.field), msgs[0]);
    }

    // messages[]
    if (Array.isArray(src?.messages)) msgs.push(...src.messages.map(String));

    if (!msgs.length && typeof src === 'string') msgs.push(src);
    if (!msgs.length && typeof e?.message === 'string') msgs.push(e.message);

    return Array.from(new Set(msgs.filter(Boolean)));
  }
  // ──────────────────────────────────────────────────────────

  private recalcStats(list: MemberDto[]){
    this.statTotal.set(this.total());
    const start = new Date(); start.setHours(0,0,0,0);
    const end   = new Date(); end.setHours(23,59,59,999);
    this.statToday.set(
      list.filter(m => {
        const t = new Date(m.registeredAt).getTime();
        return t >= start.getTime() && t <= end.getTime();
      }).length
    );
  }

  private buildQuery(){
    const v = this.filters.getRawValue();
    return {
      Q: v.Q?.trim(), Name: v.Name?.trim(), Email: v.Email?.trim(), Phone: v.Phone?.trim(),
      RegisteredFrom: v.RegisteredFrom || undefined,
      RegisteredTo:   v.RegisteredTo   || undefined,
      Page: this.page(), PageSize: this.pageSize(),
      SortBy: v.SortBy, SortDir: v.SortDir
    };
  }

  reload(){
    if (!this.canRead) return;
    this.loading.set(true);
    this.loadError.set(null);
    this.api.list(this.buildQuery()).subscribe({
      next: (res) => {
        const raw  = res?.data ?? [];
        const data = raw.map(this.normalizeMember); // 👈 إظهار الهاتف حتى لو جاء باسم مختلف
        this.items.set(data);
        this.total.set(res?.meta?.total ?? data.length);
        this.page.set(res?.meta?.page ?? 1);
        this.pageSize.set(res?.meta?.pageSize ?? this.pageSize());
        this.recalcStats(data);
        this.loading.set(false);
      },
      error: (e) => {
        this.items.set([]); this.total.set(0); this.recalcStats([]);
        this.loading.set(false);
        this.loadError.set(this.serverMessage(e));
        this.err('تعذّر تحميل الأعضاء');
      }
    });
  }

  // ترتيب
  toggleSort(field: SortField){
    const by  = this.filters.value.SortBy!;
    const dir = this.filters.value.SortDir!;
    const next = by === field ? (dir === 'asc' ? 'desc' : 'asc') : 'asc';
    this.filters.patchValue({ SortBy: field, SortDir: next });
  }
  sortIcon(f: SortField){ const by=this.filters.value.SortBy!, dir=this.filters.value.SortDir!; return by===f ? (dir==='asc'?'▲':'▼') : ''; }

  // باجينيشن
  prev(){ if (this.page()>1){ this.page.update(p=>p-1); this.reload(); } }
  next(){ if (this.page()<this.totalPages()){ this.page.update(p=>p+1); this.reload(); } }
  setPageSize(v:number){ const n=Number(v)||12; this.pageSize.set(n); this.page.set(1); this.reload(); }

  // تصدير
  export(){
    this.api.export(this.buildQuery()).subscribe({
      next: (resp) => {
        if (resp.status === 204){ this.err('لا يوجد بيانات للتصدير'); return; }
        const fname = MembersService.getFilename(resp, 'members.xlsx');
        const url = URL.createObjectURL(resp.body!);
        const a = document.createElement('a'); a.href=url; a.download=fname; a.click();
        URL.revokeObjectURL(url);
        this.ok('تم تصدير الملف');
      },
      error: () => this.err('فشل تصدير الملف')
    });
  }

  // إنشاء
  openCreate(){
    if (!this.canCreate) return;
    this.createErrors = [];
    this.createForm.reset({ username:'', name:'', email:'', phone:'', password:'', confirmPassword:'' });
    this.creating.set(false);
    this.createDlg?.nativeElement?.showModal();
  }
  closeCreate(){ this.createDlg?.nativeElement?.close(); }

  submitCreate(){
    if (this.createForm.invalid){ this.createForm.markAllAsTouched(); return; }
    const { password, confirmPassword } = this.createForm.getRawValue();
    if (password !== confirmPassword){
      this.createForm.controls.confirmPassword.setErrors({ mismatch: true });
      return;
    }

    this.creating.set(true);
    const dto = this.createForm.getRawValue();
    this.api.publicRegister(dto).subscribe({
      next: (resp) => {
        this.creating.set(false);
        if (resp.status === 201) this.ok('تم إنشاء العضو بنجاح');
        this.closeCreate();
        this.reload();
      },
      error: (e) => {
        this.creating.set(false);
        this.createErrors = this.collectBackendErrors(this.createForm, e);
      }
    });
  }

  // تعديل
  openEdit(m: MemberDto){
    if (!this.canUpdate) return;
    this.editErrors = [];
    this.editingId = m.id;
    this.saving.set(false);
    this.editForm.reset({ name: m.name ?? '', email: m.email ?? '', phone: m.phone ?? '' });
    this.editDlg?.nativeElement?.showModal();
  }
  closeEdit(){ this.editDlg?.nativeElement?.close(); this.editingId = null; }

  submitEdit(){
    if (!this.editingId) return;
    // نلوّن فقط إن كان في أخطاء فرونت
    if (this.editForm.invalid){ this.editForm.markAllAsTouched(); }

    this.saving.set(true);
    const raw = this.editForm.getRawValue();
    const dto = { name: raw.name, email: raw.email, phone: (raw.phone?.trim() || null) as string|null };

    // 👇 استخدم end-point الإداري
    this.api.adminUpdate(this.editingId, dto).subscribe({
      next: () => { this.saving.set(false); this.ok('تمّ تحديث العضو'); this.closeEdit(); this.reload(); },
      error: (e) => {
        this.saving.set(false);
        this.editErrors = this.collectBackendErrors(this.editForm, e);
      }
    });
  }

  // حذف
remove(m: MemberDto) {
  if (!this.canDelete) return;
  if (!confirm(`حذف العضو #${m.id}؟`)) return;

  this.api.delete(m.id).subscribe({
    next: () => {
      this.ok('تم الحذف');
      this.reload();
    },
    error: (e) => {
      if (e.status === 400) {
        // عرض رسالة الخطأ على الواجهة
        this.showErrorMessage(e.error.message || 'حدث خطأ أثناء محاولة الحذف');
      } else {
        this.err(this.serverMessage(e));
      }
    }
  });
}

// دالة لعرض رسالة الخطأ
showErrorMessage(message: string) {
  // استخدم Toast أو Alert لعرض الرسالة
  alert(message);  // يمكنك استبدالها بنافذة منبثقة مخصصة مثل Toast أو Snackbar
  // أو إذا كنت تستخدم مكتبة مثل ngToast أو ngSnackBar يمكنك استخدامها هنا.
}


}
