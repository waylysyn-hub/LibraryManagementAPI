import { Component, effect, inject, signal, PLATFORM_ID, ViewChild, OnInit, NgZone } from '@angular/core';
import { isPlatformBrowser, CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { HttpErrorResponse } from '@angular/common/http';
import { NgIf } from '@angular/common';
import { BookService } from '../api/book.service';
import { Book, BookQuery } from '../api/types';
import { BookFormComponent } from './book-form';
import { ToastService } from '../ui/toasts/toast.service';
import { HttpClient, HttpParams } from '@angular/common/http';

@Component({
  standalone: true,
  selector: 'app-books',
  imports: [CommonModule, ReactiveFormsModule, BookFormComponent],
  templateUrl: './books.html',
  styleUrls: ['./books.css']
})
export class BooksComponent implements OnInit {
  private fb = inject(FormBuilder);
  private api = inject(BookService);
  private platformId = inject(PLATFORM_ID);
  private toast = inject(ToastService);
  private zone = inject(NgZone);
 private requestId = 0;

  @ViewChild('bookForm') bookForm!: BookFormComponent;

  // صلاحيات (افتراضيًا false)
  canCreate = false;
  canUpdate = false;
  canDelete = false;

  // فلاتر/ترقيم
  filters = this.fb.nonNullable.group({
    Q: [''],
    Title: [''],
    Author: [''],
    Category: [''],
    Isbn: [''],
    IsbnStartsWith: [false],
    YearFrom: [null as number | null],
    YearTo: [null as number | null],
    MinCopies: [null as number | null],
    MaxCopies: [null as number | null],
    SortBy: ['--' as BookQuery['SortBy']],
    SortDir: ['--' as BookQuery['SortDir']],
    Page: [1],
    PageSize: [6],
    IncludeBorrowCount: [true]
  });

  items = signal<Book[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(6);
  loading = signal(false);
  totalPages = signal(1);

  // ---------- Permissions helpers ----------
  private normalizePerms(raw: string | null): string[] {
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed)) return parsed.map(x => String(x).trim().toLowerCase());
      if (typeof parsed === 'string') {
        return parsed.split(/[,;\s]+/).filter(Boolean).map(x => x.toLowerCase());
      }
    } catch {
      return raw.split(/[,;\s]+/).filter(Boolean).map(x => x.toLowerCase());
    }
    return [];
  }

  private has(need: string, perms: string[], role: string): boolean {
    const n = need.toLowerCase();
    const r = (role || '').toLowerCase();
    // هنا نقوم بالتحقق من الأذونات مباشرة دون النظر إلى الدور
    return perms.includes(n); // تحقق من الأذونات المخزنة مباشرة
  }

  private applyPermissionsFromStorage() {
    const role = (localStorage.getItem('role') || '').trim();  // جلب الدور
    const perms = this.normalizePerms(localStorage.getItem('permissions'));  // جلب الأذونات

    // تحديث الصلاحيات بناءً على الأذونات المخزنة
    this.canCreate = this.has('book.create', perms, role);
    this.canUpdate = this.has('book.update', perms, role);
    this.canDelete = this.has('book.delete', perms, role);

    console.log('[books/permissions]', { role, perms, canCreate: this.canCreate, canUpdate: this.canUpdate, canDelete: this.canDelete });
  }

  constructor() {
    // حساب عدد الصفحات
  effect(() => {
    this.totalPages.set(Math.max(1, Math.ceil(this.total() / this.pageSize())));
  });


    if (isPlatformBrowser(this.platformId)) {
      // حمّل الصلاحيات واسمَع لأي تغيير خارجي على localStorage
      this.applyPermissionsFromStorage();
      window.addEventListener('storage', () => this.zone.run(() => this.applyPermissionsFromStorage()));

      // Debounce للفلاتر
      this.filters.valueChanges
        .pipe(debounceTime(250), distinctUntilChanged())
        .subscribe(() => { this.page.set(1); this.reload(); });

      this.reload();
    }
  }

  ngOnInit(): void {
    // تأكيد قراءة الصلاحيات بعد التهيئة (يغطي تغيّر الستورج بعد Login)
    if (isPlatformBrowser(this.platformId)) {
      setTimeout(() => this.applyPermissionsFromStorage(), 0);
    }
  }

  private toQuery(): BookQuery {
    const v = this.filters.getRawValue();
    const clean = <T>(x: T | null | '' | undefined): T | undefined =>
      (x === null || x === '') ? undefined : x as T;

    return {
      Q: clean<string>(v.Q),
      Title: clean<string>(v.Title),
      Author: clean<string>(v.Author),
      Category: clean<string>(v.Category),
      Isbn: clean<string>(v.Isbn),
      IsbnStartsWith: v.IsbnStartsWith ? true : undefined,
      YearFrom: clean<number>(v.YearFrom),
      YearTo: clean<number>(v.YearTo),
      MinCopies: clean<number>(v.MinCopies),
      MaxCopies: clean<number>(v.MaxCopies),
      SortBy: v.SortBy === '--' ? undefined : v.SortBy,
      SortDir: v.SortDir === '--' ? undefined : v.SortDir,
    Page: this.page(), PageSize: this.pageSize(),
      IncludeBorrowCount: v.IncludeBorrowCount ? true : undefined
    };
  }

  // ✅ النسخة التي قلت إنها شغالة
  reload() {
    this.loading.set(true);

    const myId = ++this.requestId; // رقم هذا الطلب
    const query = this.toQuery();

    this.api.list(query).subscribe({
      next: (res) => {
        // تجاهل ردود قديمة
        if (myId !== this.requestId) return;

        this.items.set(res.data ?? []);
        this.total.set(res.meta?.total ?? res.data?.length ?? 0);

        // لا نغيّر pageSize من استجابة السيرفر إطلاقًا
        // this.pageSize.set(res.meta.pageSize); // ❌ لا تفعل هذا

        // الصفحة الحالية من السيرفر إن وُجدت
        if (typeof res.meta?.page === 'number') this.page.set(res.meta.page);

        // احسب إجمالي الصفحات وفق total و pageSize المحلي
        const pages = Math.max(1, Math.ceil(this.total() / this.pageSize()));
        this.totalPages.set(pages);

        // لو الصفحة الحالية أصبحت خارج المدى بعد التحديث، قلّمها وأعد طلبًا خفيفًا
        if (this.page() > this.totalPages()) {
          this.page.set(this.totalPages());
          const q2 = this.toQuery();
          this.api.list(q2).subscribe({
            next: (res2) => {
              if (myId !== this.requestId) return;
              this.items.set(res2.data ?? []);
              this.total.set(res2.meta?.total ?? res2.data?.length ?? 0);
              const pages2 = Math.max(1, Math.ceil(this.total() / this.pageSize()));
              this.totalPages.set(pages2);
              this.loading.set(false);
            },
            error: () => {
              if (myId !== this.requestId) return;
              this.items.set([]); this.total.set(0);
              this.totalPages.set(1);
              this.loading.set(false);
              this.toast.error('تعذر تحميل الكتب.');
            }
          });
          return;
        }
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        if (myId !== this.requestId) return;
        this.items.set([]);
        this.total.set(0);
        this.totalPages.set(1);
        this.loading.set(false);
        this.toast.error('تعذر تحميل الكتب.');
      }
    });
  }


  // الباقي كما هو (prev/next/sort/…)



  reset() {
    this.filters.reset({
      Q: '', Title: '', Author: '', Category: '', Isbn: '', IsbnStartsWith: false,
      YearFrom: null, YearTo: null, MinCopies: null, MaxCopies: null,
      SortBy: '--', SortDir: '--', Page: 1, PageSize: this.pageSize(), IncludeBorrowCount: false
    });
    this.page.set(1);
    this.reload();
  }

  sort(field: BookQuery['SortBy']) {
    const by  = this.filters.value.SortBy;
    const dir = this.filters.value.SortDir;
    const next: 'Asc'|'Desc' = by === field ? (dir === 'Asc' ? 'Desc' : 'Asc') : 'Asc';
    this.filters.patchValue({ SortBy: field, SortDir: next });
    this.page.set(1);
    this.reload();
  }

  sortIcon(field: BookQuery['SortBy']) {
    const by = this.filters.value.SortBy;
    const dir = this.filters.value.SortDir;
    if (by !== field) return '';
    return dir === 'Asc' ? '▲' : '▼';
  }

  prev(){ if (this.page()>1){ this.page.update(p=>p-1); this.reload(); } }
  next(){ if (this.page()<this.totalPages()){ this.page.update(p=>p+1); this.reload(); } }
  setPageSize(v:number){ const n=Number(v)||12; this.pageSize.set(n); this.page.set(1); this.reload(); }



  // ---------- CRUD ----------
  openCreate() {
    if (!this.canCreate) return;
    this.bookForm.open();
  }

  openEdit(b: Book) {
    if (!this.canUpdate) return;
    this.bookForm.open(b);
  }

  private collectBackendErrors(e: any): string[] {
    if (e?.error?.errors && typeof e.error.errors === 'object') {
      const out: string[] = [];
      for (const k of Object.keys(e.error.errors)) {
        const arr = e.error.errors[k];
        if (Array.isArray(arr)) for (const m of arr) out.push(`${k}: ${m}`);
      }
      if (out.length) return out;
    }
    if (Array.isArray(e?.error?.messages)) return e.error.messages;
    const single = e?.error?.message || e?.error?.title || e?.error?.detail || e?.message;
    return single ? [single] : ['حدث خطأ غير معروف.'];
  }

  save(payload: Omit<Book, 'id' | 'borrowCount'> & { id?: number }) {
    if (payload.id) {
      this.api.update(payload.id, payload).subscribe({
        next: () => {
          this.toast.success('تمّ تحديث الكتاب بنجاح');
          this.bookForm.close();
          this.reload();
        },
        error: (e: HttpErrorResponse) => {
          console.error(e);
          this.toast.error('فشل تحديث الكتاب');
          this.bookForm.setBackendErrors(this.collectBackendErrors(e));
        }
      });
    } else {
      this.api.create(payload).subscribe({
        next: () => {
          this.toast.success('تمّ إضافة الكتاب بنجاح');
          this.bookForm.close();
          this.reload();
        },
        error: (e: HttpErrorResponse) => {
          console.error(e);
          this.toast.error('فشل إضافة الكتاب');
          this.bookForm.setBackendErrors(this.collectBackendErrors(e));
        }
      });
    }
  }
// ✅ دالة لعرض رسائل النجاح
ok(msg: string) {
  this.toast.success(msg); // أو this.toast.showSuccess(msg)
}

// ✅ دالة لعرض رسائل الخطأ
err(msg: string) {
  this.toast.error(msg); // أو this.toast.showError(msg)
}

// ✅ دالة ذكية لاستخراج رسالة السيرفر من الخطأ
serverMessage(e: any): string {
  if (!e) return 'خطأ غير معروف';

  // في حال السيرفر أرسل رسالة مباشرة
  if (e.error?.message) return e.error.message;

  // في حال كان فيه كائن أخطاء
  if (e.error?.errors) {
    const first = Object.values(e.error.errors)[0];
    if (Array.isArray(first)) return first[0];
    return first as string;
  }

  // حالات أخرى عامة
  if (typeof e.message === 'string') return e.message;

  return 'حدث خطأ أثناء الاتصال بالسيرفر.';
}
remove(b: Book) {
  if (!this.canDelete) return;
  if (!confirm(`حذف "${b.title}"؟`)) return;

  this.api.delete(b.id).subscribe({
    next: () => {
      this.toast.success('تمّ حذف الكتاب');
      this.reload();
    },
    error: (e: any) => {
      // عرض رسالة الخطأ الفعلية من السيرفر
      const msg = e?.error?.message || 'حدث خطأ غير معروف';
      this.toast.error(msg); // 👈 هنا يطلع الخطأ على الشاشة
      console.error('[Book Delete Error]', e);
    }
  });
}


  toggleDir() {
    const dir = this.filters.value.SortDir;
    this.filters.patchValue({ SortDir: dir === 'Asc' ? 'Desc' : 'Asc' });
    this.page.set(1);
    this.reload();
  }

  coverOf(b: Book): string {
    return (b as any).coverUrl || '/book-placeholder.jpg';
  }

  onCoverError(ev: Event) {
    const img = ev.target as HTMLImageElement;
    if (img && img.src !== location.origin + '/book-placeholder.jpg') {
      img.src = '/book-placeholder.jpg';
    }
  }

  export() {
    const q = this.toQuery();
    this.api.export(q).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'books.xlsx';
        a.click();
        URL.revokeObjectURL(url);
        this.toast.success('تمّ تصدير الملف');
      },
      error: err => {
        console.error(err);
        this.toast.error('فشل تصدير الملف');
      }
    });
  }
}
