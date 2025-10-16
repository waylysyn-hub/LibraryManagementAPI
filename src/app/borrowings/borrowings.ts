// src/app/borrowings/borrowings.ts
import { Component, ViewChild, effect, inject, signal } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PLATFORM_ID } from '@angular/core';
import { BorrowService, BorrowRecordDto } from '../api/borrow.service';
import { ToastService } from '../ui/toasts/toast.service';
import { BorrowFormComponent } from './borrow-form';
import { MembersService } from '../api/members.service';  // استيراد MembersService
import { BookService } from '../api/book.service';  // استيراد BookService
import { NgIf } from '@angular/common';

type SortField = 'borrowedDate'|'dueDate'|'returnedDate';

@Component({
  standalone: true,
  selector: 'app-borrowings',
  imports: [CommonModule, ReactiveFormsModule, BorrowFormComponent],
  templateUrl: './borrowings.html',
  styleUrls: ['./borrowings.css']
})
export class BorrowingsComponent {
  private fb = inject(FormBuilder);
  private api = inject(BorrowService);
  private platformId = inject(PLATFORM_ID);
  private toast = inject(ToastService, { optional: true });
  private memberservice = inject(MembersService);  // تأكد من أنك قد قمت بإنشاء هذه الخدمة بشكل صحيح
  private bookService = inject(BookService);  // تأكد من أنك قد قمت بإنشاء هذه الخدمة بشكل صحيح

  @ViewChild('borrowForm') borrowForm!: BorrowFormComponent;
    members = signal<any[]>([]);  // تخزين الأعضاء
  books = signal<any[]>([]);    // تخزين الكتب

  // تحميل الأعضاء
  loadMembers() {
    this.memberservice.list({ Page: 1, PageSize: 100 }).subscribe(res => {
      this.members.set(res.data);
    });
  }

  // تحميل الكتب
  loadBooks() {
    this.bookService.list({ Page: 1, PageSize: 100 }).subscribe(res => {
      this.books.set(res.items);
    });
  }



  // دوال عرض الأسماء
  getMemberName(memberId: number): string {
    const member = this.members().find(m => m.id === memberId);
    return member ? member.name : 'غير معروف';
  }

  getBookTitle(bookId: number): string {
    const book = this.books().find(b => b.id === bookId);
    return book ? book.title : 'غير معروف';
  }
// وقت "اليوم" للمقارنات (حدثه عند كل تحميل إن حبيت)
get todayMs(){ return Date.now(); }


isReturned(r: BorrowRecordDto): boolean {
  return !!r.returnedDate;
}

isLate(r: BorrowRecordDto): boolean {
  if (this.isReturned(r)) return false;
  const due = new Date(r.dueDate).getTime();
  return due < this.todayMs;
}

isActive(r: BorrowRecordDto): boolean {
  if (this.isReturned(r)) return false;
  const due = new Date(r.dueDate).getTime();
  return due >= this.todayMs;
}
abs(n: number): number { 
  return Math.abs(n ?? 0); 
}

// كم يوم متبقّي (سالب = متأخر)
daysLeft(r: BorrowRecordDto): number {
  if (r.returnedDate) return 0;
  const due = new Date(r.dueDate).getTime();
  return Math.ceil((due - this.todayMs) / 86_400_000);
}

// نسبة التقدّم من تاريخ الاستعارة حتى الآن/الإرجاع
progressPct(r: BorrowRecordDto): number {
  const start = new Date(r.borrowedDate).getTime();
  const due   = new Date(r.dueDate).getTime();
  const now   = r.returnedDate ? new Date(r.returnedDate).getTime() : this.todayMs;
  const total = Math.max(1, due - start);
  const spent = Math.min(total, Math.max(0, now - start));
  return Math.round((spent / total) * 100);
}

// لون الحالة كنص قصير (نستفيد منه بالـ CSS عبر [attr])
statusKey(r: BorrowRecordDto): 'active'|'late'|'done' {
  if (this.isReturned(r)) return 'done';
  return this.isLate(r) ? 'late' : 'active';
}

statusLabel(r: BorrowRecordDto): string {
  return this.isReturned(r) ? 'مُرجع' : (this.isLate(r) ? 'متأخر' : 'فعّال');
}

  // صلاحيات
  canRead = false;
  canCreate = false;
  canUpdate = false;   // إرجاع/تعديل
  canDelete = false;

  // فلاتر وترتيب وباجينيشن
  filters = this.fb.nonNullable.group({
    memberId: [null as number | null],
    bookId:   [null as number | null],
    page:     [1],
    pageSize: [12],
    sortBy:   ['borrowedDate' as SortField],
    sortDir:  ['Desc' as 'Asc' | 'Desc'],
    view:     ['cards' as 'cards'|'table']
  });

  items   = signal<BorrowRecordDto[]>([]);
  total   = signal(0);
  page    = signal(1);
  pageSize= signal(12);
  loading = signal(false);
  totalPages = signal(1);

  // لوحات إحصاء
  statTotal    = signal(0);
  statActive   = signal(0);
  statOverdue  = signal(0);
  statReturned = signal(0);

constructor() {
  // تحميل الأعضاء والكتب
  this.loadMembers();
  this.loadBooks();

  // حساب عدد الصفحات بناءً على البيانات
  effect(() => {
    const pages = Math.max(1, Math.ceil(this.total() / this.pageSize()));
    this.totalPages.set(pages);
  });

  // التحقق من التصفح على المتصفح فقط
  if (isPlatformBrowser(this.platformId)) {

    // التبديل بين عرض الكروت والجدول عبر ضغط مفتاح V
    window.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v') {
        const next = this.filters.value.view === 'cards' ? 'table' : 'cards';
        this.filters.patchValue({ view: next });
        this.toast?.success?.(next === 'cards' ? 'عرض الكروت' : 'عرض الجدول');
      }
    });

    // استنتاج صلاحيات المستخدم بناءً على الأذونات في localStorage
    try {
      const p = JSON.parse(localStorage.getItem('permissions') || '[]') as string[];  // جلب الأذونات
      const role = (localStorage.getItem('role') || '').toLowerCase();  // جلب الدور من localStorage

      // دالة للتحقق من الصلاحيات بناءً على الدور
      const has = (need: string) => {
        // إذا كان الدور admin، نتحقق من الأذونات المخزنة ونسمح بالصلاحية فقط إذا كانت موجودة
        if (role === 'admin') {
          return p.includes(need);  // نتحقق من الأذونات حتى لو كان الدور Admin
        }
        // إذا لم يكن الدور admin، نبحث في الأذونات المحددة للمستخدم
        return p.includes(need);
      };

      // تعيين الصلاحيات بناءً على الدور والأذونات
      this.canRead   = has('borrow.read');
      this.canCreate = has('borrow.create');
      this.canUpdate = has('borrow.update');
      this.canDelete = has('borrow.delete');
      
      // تسجيل الصلاحيات
      console.log('[permissions]', { role, p, canRead: this.canRead, canCreate: this.canCreate, canUpdate: this.canUpdate, canDelete: this.canDelete });
    } catch (error) {
      console.error('Error parsing permissions or role:', error);
    }

    // الاشتراك في التغيرات على الفلاتر
    this.filters.valueChanges.subscribe(() => { this.page.set(1); this.reload(); });

    // استدعاء الميثود الخاصة بإعادة تحميل البيانات
    this.reload();
  }

  // إذا كان هناك تفضيل محفوظ للعرض (كروت أو جدول)
  if (isPlatformBrowser(this.platformId)) {
    const savedView = localStorage.getItem('borrow_view') as 'cards'|'table'|null;
    if (savedView) this.filters.patchValue({ view: savedView }, { emitEvent:false });

    this.filters.get('view')!.valueChanges.subscribe(v => {
      localStorage.setItem('borrow_view', v as string);
    });

    // التفضيل الافتراضي للموبايل هو عرض الكروت وللأجهزة المكتبية هو عرض الجدول
    if (!savedView) {
      const prefer = window.matchMedia('(max-width: 768px)').matches ? 'cards' : 'table';
      this.filters.patchValue({ view: prefer }, { emitEvent:false });
    }

    // الاشتراك في التغيرات على الفلاتر
    this.filters.valueChanges.subscribe(() => { this.page.set(1); this.reload(); });

    // استدعاء الميثود الخاصة بإعادة تحميل البيانات
    this.reload();
  }
}




  private notifyOk(msg: string)  { this.toast?.success ? this.toast.success(msg) : console.log(msg); }
  private notifyErr(msg: string) { this.toast?.error   ? this.toast.error(msg)   : console.error(msg); }

  private sortLocal(list: BorrowRecordDto[]) {
    const by  = this.filters.value.sortBy!;
    const dir = this.filters.value.sortDir!;
    return [...list].sort((a,b)=>{
      const av = (a as any)[by]; const bv = (b as any)[by];
      const aa = av ? new Date(av).getTime() : 0;
      const bb = bv ? new Date(bv).getTime() : 0;
      const s = aa < bb ? -1 : aa > bb ? 1 : 0;
      return dir === 'Asc' ? s : -s;
    });
  }

  private recalcStats(list: BorrowRecordDto[]) {
    const now = new Date().getTime();
    let active=0, overdue=0, returned=0;
    for (const r of list) {
      if (r.returnedDate) { returned++; continue; }
      const due = new Date(r.dueDate).getTime();
      if (now > due) overdue++; else active++;
    }
    this.statTotal.set(list.length);
    this.statActive.set(active);
    this.statOverdue.set(overdue);
    this.statReturned.set(returned);
  }

  reload() {
    if (!this.canRead) return;
    this.loading.set(true);
    const v = this.filters.getRawValue();

    this.api.list({
      memberId: v.memberId ?? undefined,
      bookId:   v.bookId ?? undefined,
      page:     this.page(),
      pageSize: this.pageSize()
    }).subscribe({
      next: res => {
        const sorted = this.sortLocal(res.items);
        this.items.set(sorted);
        this.total.set(res.total);
        this.page.set(res.page);
        this.pageSize.set(res.pageSize);
        this.recalcStats(sorted);
        this.loading.set(false);
      },
      error: err => {
        console.error(err);
        this.items.set([]); this.total.set(0);
        this.recalcStats([]);
        this.loading.set(false);
        this.notifyErr('تعذّر تحميل الاستعارات');
      }
    });
  }

  // ترتيب سريع
  toggleSort(field: SortField) {
    const by = this.filters.value.sortBy!;
    const dir = this.filters.value.sortDir!;
    const next = by === field ? (dir === 'Asc' ? 'Desc':'Asc') : 'Asc';
    this.filters.patchValue({ sortBy: field, sortDir: next });
    // reload سيعمل عبر valueChanges
  }
  sortIcon(f: SortField){ const by=this.filters.value.sortBy!; const dir=this.filters.value.sortDir!; return by===f ? (dir==='Asc'?'▲':'▼') : ''; }

  // باجينيشن
  prev(){ if (this.page()>1){ this.page.update(p=>p-1); this.reload(); } }
  next(){ if (this.page()<this.totalPages()){ this.page.update(p=>p+1); this.reload(); } }
  setPageSize(v:number){ const n=Number(v)||12; this.pageSize.set(n); this.page.set(1); this.reload(); }

  // إجراءات
  openCreate(){ if (!this.canCreate) return; this.borrowForm.open(); }
  openEdit(r: BorrowRecordDto){ if (!this.canUpdate) return; this.borrowForm.open(r); }

  save(rec: { id?: number; memberId:number; bookId:number; durationDays:number }) {
    const done = () => { this.borrowForm.close(); this.reload(); };
    if (rec.id){
      this.api.update(rec.id, rec).subscribe({
        next: _ => { this.notifyOk('تمّ تحديث الاستعارة'); done(); },
        error: e => this.borrowForm.setBackendErrors(this.collectBackend(e))
      });
    } else {
      this.api.create(rec).subscribe({
        next: _ => { this.notifyOk('تمّ إضافة الاستعارة'); done(); },
        error: e => this.borrowForm.setBackendErrors(this.collectBackend(e))
      });
    }
  }

  returnNow(r: BorrowRecordDto){
    if (!this.canUpdate || r.returnedDate) return;
    this.api.return(r.id).subscribe({
      next: _ => { this.notifyOk('تم إرجاع الكتاب'); this.reload(); },
      error: e => this.notifyErr(e?.error?.message || 'فشل الإرجاع')
    });
  }

  remove(r: BorrowRecordDto){
    if (!this.canDelete) return;
    if (!confirm(`حذف سجل #${r.id}؟`)) return;
    this.api.delete(r.id).subscribe({
      next: _ => { this.notifyOk('تم الحذف'); this.reload(); },
      error: e => this.notifyErr(e?.error?.message || 'فشل الحذف')
    });
  }

  export(){
    const v = this.filters.getRawValue();
    this.api.export(v.memberId ?? undefined, v.bookId ?? undefined).subscribe({
      next: resp => {
        if (resp.status === 204){ this.notifyErr('لا يوجد بيانات للتصدير'); return; }
        const url = URL.createObjectURL(resp.body!);
        const a = document.createElement('a'); a.href=url; a.download='borrow-records.xlsx'; a.click();
        URL.revokeObjectURL(url); this.notifyOk('تم تصدير الملف');
      },
      error: _ => this.notifyErr('فشل تصدير الملف')
    });
  }

  private collectBackend(e:any):string[]{
    if (e?.error?.errors && typeof e.error.errors==='object'){
      const res:string[]=[]; for (const k of Object.keys(e.error.errors)){
        const arr=e.error.errors[k]; if (Array.isArray(arr)) arr.forEach((m:string)=>res.push(`${k}: ${m}`));
      } if (res.length) return res;
    }
    if (Array.isArray(e?.error?.messages)) return e.error.messages;
    const single = e?.error?.message || e?.error?.title || e?.error?.detail || e?.message;
    return single ? [single] : ['حدث خطأ غير معروف.'];
  }
}
