// src/app/borrowings/borrowings.ts
import {
  Component,
  ViewChild,
  effect,
  inject,
  signal,
  HostListener,
  OnInit,
  OnDestroy,
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PLATFORM_ID } from '@angular/core';
import { BorrowService, BorrowRecordDto } from '../api/borrow.service';
import { ToastService } from '../ui/toasts/toast.service';
import { BorrowFormComponent } from './borrow-form';
import { MembersService } from '../api/members.service';
import { BookService } from '../api/book.service';
import { Subscription } from 'rxjs';

type SortField = 'borrowedDate' | 'dueDate' | 'returnedDate';

@Component({
  standalone: true,
  selector: 'app-borrowings',
  imports: [CommonModule, ReactiveFormsModule, BorrowFormComponent],
  templateUrl: './borrowings.html',
  styleUrls: ['./borrowings.css'],
})
export class BorrowingsComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private api = inject(BorrowService);
  private platformId = inject(PLATFORM_ID);
  private toast = inject(ToastService, { optional: true });
  private memberservice = inject(MembersService);
  private bookService = inject(BookService);

  @ViewChild('borrowForm') borrowForm!: BorrowFormComponent;

  // أعضاء وكتب
  members = signal<any[]>([]);
  books = signal<any[]>([]);

  // صلاحيات
  canRead = false;
  canCreate = false;
  canUpdate = false;
  canDelete = false;

  // فلاتر وترتيب وباجينيشن
  filters = this.fb.nonNullable.group({
    memberId: [null as number | null],
    bookId: [null as number | null],
    page: [1],
    pageSize: [12],
    sortBy: ['borrowedDate' as SortField],
    sortDir: ['Desc' as 'Asc' | 'Desc'],
    view: ['cards' as 'cards' | 'table'],
  });

  items = signal<BorrowRecordDto[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(12);
  loading = signal(false);
  totalPages = signal(1);

  // لوحات إحصاء
  statTotal = signal(0);
  statActive = signal(0);
  statOverdue = signal(0);
  statReturned = signal(0);

  // اشتراك واحد فقط للفلاتر
  private filtersSub?: Subscription;

  constructor() {
    // حساب عدد الصفحات عند تغيّر total / pageSize
    effect(() => {
      const pages = Math.max(1, Math.ceil(this.total() / this.pageSize()));
      this.totalPages.set(pages);
    });
  }


  @HostListener('window:keydown', ['$event'])
  onGlobalKeydown(e: KeyboardEvent) {
    if (!isPlatformBrowser(this.platformId)) return;

    // تجاهل لو المستخدم يكتب داخل input/textarea/عنصر قابل للتحرير
    const t = e.target as HTMLElement | null;
    const tag = (t?.tagName || '').toLowerCase();
    const typing = tag === 'input' || tag === 'textarea' || t?.isContentEditable;

    if (!typing && e.altKey && !e.ctrlKey && !e.metaKey && e.key.toLowerCase() === 'v') {
      e.preventDefault();
      const next = this.filters.value.view === 'cards' ? 'table' : 'cards';
      this.filters.patchValue({ view: next });
      this.toast?.success?.(next === 'cards' ? 'عرض الكروت' : 'عرض الجدول');
    }
  }

  ngOnInit(): void {
    // تحميل الأعضاء والكتب (باكراً)
    this.loadMembers();
    this.loadBooks();

    if (isPlatformBrowser(this.platformId)) {
      // صلاحيات من localStorage
      this.applyPermissionsFromLocalStorage();

      // تفضيل العرض من التخزين
      const savedView = (localStorage.getItem('borrow_view') as 'cards' | 'table' | null) ?? null;
      if (savedView) {
        this.filters.patchValue({ view: savedView }, { emitEvent: false });
      } else {
        // افتراضي: موبايل = كروت، سطح مكتب = جدول
        const prefer = window.matchMedia('(max-width: 768px)').matches ? 'cards' : 'table';
        this.filters.patchValue({ view: prefer }, { emitEvent: false });
      }

      // حفظ تفضيل العرض عند تغيّره
      this.filters.get('view')!.valueChanges.subscribe((v) => {
        localStorage.setItem('borrow_view', String(v));
      });

      // اشتراك وحيد على تغيّر الفلاتر
      this.filtersSub = this.filters.valueChanges.subscribe(() => {
        this.page.set(1);
        this.reload();
      });

      // أول تحميل إذا مسموح القراءة
      this.reload();
    }
  }

  ngOnDestroy(): void {
    this.filtersSub?.unsubscribe();
  }

  // ===== تحميل أعضاء/كتب =====
  loadMembers() {
    this.memberservice.list({ Page: 1, PageSize: 100 }).subscribe({
      next: (res) => this.members.set(res.data ?? res.data ?? res ?? []),
      error: () => this.members.set([]),
    });
  }

  loadBooks() {
    this.bookService.list({ Page: 1, PageSize: 100 }).subscribe({
      next: (res) => this.books.set(res.items ?? res.data ?? res ?? []),
      error: () => this.books.set([]),
    });
  }

  // ===== دوال عرض الأسماء =====
  getMemberName(memberId: number): string {
    const m = this.members().find((x) => x.id === memberId);
    return m ? (m.name || m.username || `عضو #${m.id}`) : 'غير معروف';
    // احتياطًا لو السيرفر لا يرسل name/username
  }

  getBookTitle(bookId: number): string {
    const b = this.books().find((x) => x.id === bookId);
    return b ? (b.title || `كتاب #${b.id}`) : 'غير معروف';
  }

  // ===== تواريخ/حالة =====
  get todayMs() {
    return Date.now();
  }

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
    const due = new Date(r.dueDate).getTime();
    const now = r.returnedDate ? new Date(r.returnedDate).getTime() : this.todayMs;
    const total = Math.max(1, due - start);
    const spent = Math.min(total, Math.max(0, now - start));
    return Math.round((spent / total) * 100);
  }

  statusKey(r: BorrowRecordDto): 'active' | 'late' | 'done' {
    if (this.isReturned(r)) return 'done';
    return this.isLate(r) ? 'late' : 'active';
  }

  statusLabel(r: BorrowRecordDto): string {
    return this.isReturned(r) ? 'مُرجع' : this.isLate(r) ? 'متأخر' : 'فعّال';
  }

  // ===== صلاحيات من localStorage =====
  private applyPermissionsFromLocalStorage() {
    try {
      const p = JSON.parse(localStorage.getItem('permissions') || '[]') as string[];
      const role = (localStorage.getItem('role') || '').toLowerCase();
      // حسب كودك: حتى الـ admin لا يُمنح كل شيء تلقائياً بل حسب الأذونات المخزنة
      const has = (need: string) => p.includes(need);

      this.canRead = has('borrow.read');
      this.canCreate = has('borrow.create');
      this.canUpdate = has('borrow.update');
      this.canDelete = has('borrow.delete');

      console.log('[permissions]', {
        role,
        p,
        canRead: this.canRead,
        canCreate: this.canCreate,
        canUpdate: this.canUpdate,
        canDelete: this.canDelete,
      });
    } catch (error) {
      console.error('Error parsing permissions or role:', error);
    }
  }

  // ===== تحميل البيانات =====
  private sortLocal(list: BorrowRecordDto[]) {
    const by = this.filters.value.sortBy!;
    const dir = this.filters.value.sortDir!;
    return [...list].sort((a, b) => {
      const av = (a as any)[by];
      const bv = (b as any)[by];
      const aa = av ? new Date(av).getTime() : 0;
      const bb = bv ? new Date(bv).getTime() : 0;
      const s = aa < bb ? -1 : aa > bb ? 1 : 0;
      return dir === 'Asc' ? s : -s;
    });
  }

  private recalcStats(list: BorrowRecordDto[]) {
    const now = new Date().getTime();
    let active = 0,
      overdue = 0,
      returned = 0;
    for (const r of list) {
      if (r.returnedDate) {
        returned++;
        continue;
      }
      const due = new Date(r.dueDate).getTime();
      if (now > due) overdue++;
      else active++;
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

    // حافظ على page/pageSize signals متزامنة مع الفلاتر إن أحببت
    if (this.page() !== v.page) this.page.set(v.page);
    if (this.pageSize() !== v.pageSize) this.pageSize.set(v.pageSize);

    this.api
      .list({
        memberId: v.memberId ?? undefined,
        bookId: v.bookId ?? undefined,
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (res) => {
          const sorted = this.sortLocal(res.items);
          this.items.set(sorted);
          this.total.set(res.total);
          this.page.set(res.page);
          this.pageSize.set(res.pageSize);
          this.recalcStats(sorted);
          this.loading.set(false);
        },
        error: (err) => {
          console.error(err);
          this.items.set([]);
          this.total.set(0);
          this.recalcStats([]);
          this.loading.set(false);
          this.notifyErr('تعذّر تحميل الاستعارات');
        },
      });
  }

  // ===== ترتيب سريع =====
  toggleSort(field: SortField) {
    const by = this.filters.value.sortBy!;
    const dir = this.filters.value.sortDir!;
    const next = by === field ? (dir === 'Asc' ? 'Desc' : 'Asc') : 'Asc';
    this.filters.patchValue({ sortBy: field, sortDir: next });
  }
  sortIcon(f: SortField) {
    const by = this.filters.value.sortBy!;
    const dir = this.filters.value.sortDir!;
    return by === f ? (dir === 'Asc' ? '▲' : '▼') : '';
  }

  // ===== باجينيشن =====
  prev() {
    if (this.page() > 1) {
      const n = this.page() - 1;
      this.page.set(n);
      this.filters.patchValue({ page: n }, { emitEvent: false });
      this.reload();
    }
  }
  next() {
    if (this.page() < this.totalPages()) {
      const n = this.page() + 1;
      this.page.set(n);
      this.filters.patchValue({ page: n }, { emitEvent: false });
      this.reload();
    }
  }
  setPageSize(v: number) {
    const n = Number(v) || 12;
    this.pageSize.set(n);
    this.filters.patchValue({ pageSize: n, page: 1 }, { emitEvent: false });
    this.page.set(1);
    this.reload();
  }

  // ===== إجراءات =====
  openCreate() {
    if (!this.canCreate) return;
    this.borrowForm.open();
  }
  openEdit(r: BorrowRecordDto) {
    if (!this.canUpdate) return;
    this.borrowForm.open(r);
  }

  save(rec: { id?: number; memberId: number; bookId: number; durationDays: number }) {
    const done = () => {
      this.borrowForm.close();
      this.reload();
    };
    if (rec.id) {
      this.api.update(rec.id, rec).subscribe({
        next: () => {
          this.notifyOk('تمّ تحديث الاستعارة');
          done();
        },
        error: (e) => this.borrowForm.setBackendErrors(this.collectBackend(e)),
      });
    } else {
      this.api.create(rec).subscribe({
        next: () => {
          this.notifyOk('تمّ إضافة الاستعارة');
          done();
        },
        error: (e) => this.borrowForm.setBackendErrors(this.collectBackend(e)),
      });
    }
  }

  returnNow(r: BorrowRecordDto) {
    if (!this.canUpdate || r.returnedDate) return;
    this.api.return(r.id).subscribe({
      next: () => {
        this.notifyOk('تم إرجاع الكتاب');
        this.reload();
      },
      error: (e) => this.notifyErr(e?.error?.message || 'فشل الإرجاع'),
    });
  }

  remove(r: BorrowRecordDto) {
    if (!this.canDelete) return;
    if (!confirm(`حذف سجل #${r.id}؟`)) return;
    this.api.delete(r.id).subscribe({
      next: () => {
        this.notifyOk('تم الحذف');
        this.reload();
      },
      error: (e) => this.notifyErr(e?.error?.message || 'فشل الحذف'),
    });
  }

  export() {
    const v = this.filters.getRawValue();
    this.api.export(v.memberId ?? undefined, v.bookId ?? undefined).subscribe({
      next: (resp) => {
        if (resp.status === 204) {
          this.notifyErr('لا يوجد بيانات للتصدير');
          return;
        }
        const url = URL.createObjectURL(resp.body!);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'borrow-records.xlsx';
        a.click();
        URL.revokeObjectURL(url);
        this.notifyOk('تم تصدير الملف');
      },
      error: () => this.notifyErr('فشل تصدير الملف'),
    });
  }

  // ===== Utils =====
  private notifyOk(msg: string) {
    this.toast?.success ? this.toast.success(msg) : console.log(msg);
  }
  private notifyErr(msg: string) {
    this.toast?.error ? this.toast.error(msg) : console.error(msg);
  }

  private collectBackend(e: any): string[] {
    if (e?.error?.errors && typeof e.error.errors === 'object') {
      const res: string[] = [];
      for (const k of Object.keys(e.error.errors)) {
        const arr = e.error.errors[k];
        if (Array.isArray(arr)) arr.forEach((m: string) => res.push(`${k}: ${m}`));
      }
      if (res.length) return res;
    }
    if (Array.isArray(e?.error?.messages)) return e.error.messages;
    const single =
      e?.error?.message || e?.error?.title || e?.error?.detail || e?.message;
    return single ? [single] : ['حدث خطأ غير معروف.'];
  }
}
