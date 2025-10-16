import { Component, EventEmitter, Output, ViewChild, ElementRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MembersService } from '../api/members.service';  // خدمة الأعضاء
import { BookService } from '../api/book.service';      // خدمة الكتب

@Component({
  standalone: true,
  selector: 'app-borrow-form',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './borrow-form.html',
  styleUrls: ['./borrow-form.css']
})
export class BorrowFormComponent {
  private fb = inject(FormBuilder);
  private membersService = inject(MembersService);  // خدمة الأعضاء
  private bookService = inject(BookService);      // خدمة الكتب

  @ViewChild('dlg') dlg!: ElementRef<HTMLDialogElement>;

  @Output() save = new EventEmitter<{ id?: number; memberId: number; bookId: number; durationDays: number }>();
  @Output() cancel = new EventEmitter<void>();

  title = 'إضافة استعارة';

  // رسائل أخطاء قادمة من الباك
  backendErrors = signal<string[]>([]);

  // فلاغات إبراز للحقول في حال عرفنا أن الخطأ يخصها
  fieldHasBackendErr = {
    memberId: signal(false),
    bookId: signal(false),
    durationDays: signal(false),
  };

  // القوائم المنسدلة
  members = signal<any[]>([]);  // قائمة الأعضاء
  books = signal<any[]>([]);    // قائمة الكتب

  form = this.fb.nonNullable.group({
    id: [null as number | null],
    memberId: [null as number | null, []],    // بدون فاليديشن فرونت إذا بدك تعتمد كلياً على الباك
    bookId: [null as number | null, []],
    durationDays: [14, []],
  });

  constructor() {
    // جلب الأعضاء والكتب عند بدء التحميل
    this.loadMembers();
    this.loadBooks();
  }

  // دالة لتحميل الأعضاء من الخدمة
  loadMembers() {
    this.membersService.list({ Page: 1, PageSize: 100 }).subscribe(res => {
      this.members.set(res.data);  // تخزين الأعضاء في القيم المنسدلة
    });
  }

  // دالة لتحميل الكتب من الخدمة
  loadBooks() {
    this.bookService.list({ Page: 1, PageSize: 100 }).subscribe(res => {
      this.books.set(res.items);  // تخزين الكتب في القيم المنسدلة
    });
  }

  /** يستقبل أخطاء الباك من الأب ويعرضها */
  setBackendErrors(errs: string[] = []) {
    // خزّن الرسائل لعرضها أعلى الفورم
    this.backendErrors.set(errs);

    // صفّر إبراز الحقول
    this.fieldHasBackendErr.memberId.set(false);
    this.fieldHasBackendErr.bookId.set(false);
    this.fieldHasBackendErr.durationDays.set(false);

    // حاول خمن أي الحقول متأثرة بالرسالة (ببساطة بكلمات مفتاحية)
    const join = (s: string) => s.toLowerCase();
    for (const e of errs) {
      const msg = join(e);
      if (/(^|[^a-z])member(id)?|العضو/.test(msg)) this.fieldHasBackendErr.memberId.set(true);
      if (/(^|[^a-z])book(id)?|الكتاب/.test(msg))   this.fieldHasBackendErr.bookId.set(true);
      if (/duration(days)?|مدة|الأيام/.test(msg))   this.fieldHasBackendErr.durationDays.set(true);
    }
  }

  open(rec?: Partial<{ id:number; memberId:number; bookId:number; durationDays:number }>) {
    this.backendErrors.set([]);
    this.fieldHasBackendErr.memberId.set(false);
    this.fieldHasBackendErr.bookId.set(false);
    this.fieldHasBackendErr.durationDays.set(false);

    if (rec?.id) {
      this.title = `تعديل استعارة #${rec.id}`;
      this.form.reset({
        id: rec.id ?? null,
        memberId: rec.memberId ?? null,
        bookId: rec.bookId ?? null,
        durationDays: rec.durationDays ?? 14,
      });
    } else {
      this.title = 'إضافة استعارة';
      this.form.reset({ id: null, memberId: null, bookId: null, durationDays: 14 });
    }

    const node = this.dlg?.nativeElement;
    if (node?.showModal) node.showModal();
    else node?.setAttribute('open', 'open');
  }

  close() {
    const node = this.dlg?.nativeElement;
    if (node?.close) node.close();
    else node?.removeAttribute('open');
  }

  submit() {
    // ما نمنع الإرسال بفاليديشن فرونت — نخلي الباك يرجّع الأخطاء
    const v = this.form.getRawValue();
    this.save.emit({
      id: v.id ?? undefined,
      memberId: Number(v.memberId),
      bookId: Number(v.bookId),
      durationDays: Number(v.durationDays) || 14,
    });
  }
}
