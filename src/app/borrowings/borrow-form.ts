import {
  Component, EventEmitter, Output, ViewChild, ElementRef, inject, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormControl } from '@angular/forms';
import { MembersService } from '../api/members.service';
import { BookService } from '../api/book.service';
import { Observable, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, startWith, switchMap, map, catchError } from 'rxjs/operators';

type LookItem = { id: number; label: string; sub?: string | null };

@Component({
  standalone: true,
  selector: 'app-borrow-form',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './borrow-form.html',
  styleUrls: ['./borrow-form.css'],
})
export class BorrowFormComponent {
  private fb = inject(FormBuilder);
  private membersService = inject(MembersService);
  private bookService = inject(BookService);

  @ViewChild('dlg') dlg!: ElementRef<HTMLDialogElement>;

  @Output() save = new EventEmitter<{ id?: number; memberId: number; bookId: number; durationDays: number }>();
  @Output() cancel = new EventEmitter<void>();

  title = 'إضافة استعارة';

  // أخطاء الباك
  backendErrors = signal<string[]>([]);

  // إبراز الحقول
  fieldHasBackendErr = {
    memberId: signal(false),
    bookId: signal(false),
    durationDays: signal(false),
  };

  // قيم مختارة نهائية
  selectedMember?: LookItem;
  selectedBook?: LookItem;

  // إظهار/إخفاء القوائم
  memberMenuOpen = false;
  bookMenuOpen = false;

  // حقول البحث
  memberSearch = new FormControl<string>('', { nonNullable: true });
  bookSearch   = new FormControl<string>('', { nonNullable: true });

  // نموذج الإرسال
  form = this.fb.nonNullable.group({
    id: [null as number | null],
    memberId: [null as number | null, []],
    bookId: [null as number | null, []],
    durationDays: [14, [Validators.min(1), Validators.max(365)]],
  });

  // ---- Helpers ----
  private mapMembers(res: any): LookItem[] {
    const list = Array.isArray(res?.data) ? res.data : (Array.isArray(res) ? res : []);
    return list.map((u: any) => ({ id: u.id, label: u.name, sub: u.email }));
  }

  private mapBooks(res: any): LookItem[] {
    const list = (Array.isArray(res?.data) ? res.data : (Array.isArray(res?.items) ? res.items : (Array.isArray(res) ? res : []))) as any[];
    // المتاح فقط
    return list
      .filter(b => {
        const available = (b.availableCopies ?? ((b.copiesCount ?? 0) - (b.borrowCount ?? 0)));
        return (available ?? 0) > 0;
      })
      .map(b => ({ id: b.id, label: b.title, sub: b.isbn }));
  }

  // ---- Typeahead: نفس أسلوب صفحة الكتب (list + Q) ----

  memberOptions$: Observable<LookItem[]> = this.memberSearch.valueChanges.pipe(
    startWith(''),
    debounceTime(250),
    distinctUntilChanged(),
    switchMap(q => {
      const term = (q || '').trim();
      if (!term) return of([]);
      // نفس أسلوب Books: نرسل Q + ترقيم
      return this.membersService.list({ Q: term, Page: 1, PageSize: 20 }).pipe(
        map(res => this.mapMembers(res)),
        catchError(() => of([]))
      );
    })
  );

  bookOptions$: Observable<LookItem[]> = this.bookSearch.valueChanges.pipe(
    startWith(''),
    debounceTime(250),
    distinctUntilChanged(),
    switchMap(q => {
      const term = (q || '').trim();
      if (!term) return of([]);
      return this.bookService.list({ Q: term, Page: 1, PageSize: 20, IncludeBorrowCount: true }).pipe(
        map(res => this.mapBooks(res)),
        catchError(() => of([]))
      );
    })
  );

  /** استقبال أخطاء الباك من الأب */
  setBackendErrors(errs: string[] = []) {
    this.backendErrors.set(errs);
    this.fieldHasBackendErr.memberId.set(false);
    this.fieldHasBackendErr.bookId.set(false);
    this.fieldHasBackendErr.durationDays.set(false);

    const has = (s: string, r: RegExp) => r.test(s.toLowerCase());
    for (const e of errs) {
      if (has(e, /(member(id)?|العضو)/)) this.fieldHasBackendErr.memberId.set(true);
      if (has(e, /(book(id)?|الكتاب)/))   this.fieldHasBackendErr.bookId.set(true);
      if (has(e, /(duration|days|مدة|الأيام)/)) this.fieldHasBackendErr.durationDays.set(true);
    }
  }

  open(rec?: Partial<{ id:number; memberId:number; bookId:number; durationDays:number }>) {
    this.backendErrors.set([]);
    Object.values(this.fieldHasBackendErr).forEach(s => s.set(false));
    this.memberMenuOpen = false; this.bookMenuOpen = false;
    this.selectedMember = undefined; this.selectedBook = undefined;
    this.memberSearch.setValue(''); this.bookSearch.setValue('');

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
    if (node?.showModal) node.showModal(); else node?.setAttribute('open', 'open');
  }

  close() {
    const node = this.dlg?.nativeElement;
    if (node?.close) node.close(); else node?.removeAttribute('open');
    this.memberMenuOpen = false;
    this.bookMenuOpen = false;
  }

  // إغلاق مؤجّل بعد blur حتى يقدر المستخدم يضغط عنصر القائمة
  closeMenuLater(which: 'member'|'book') {
    setTimeout(() => {
      if (which === 'member') this.memberMenuOpen = false;
      else this.bookMenuOpen = false;
    }, 120);
  }

  // اختيار أول عنصر بسرعة عبر Enter
  pickFirst(which: 'member'|'book') {
    if (which === 'member') {
      const sub = this.memberOptions$.subscribe(list => { if (list?.length) this.pickMember(list[0]); sub.unsubscribe(); });
    } else {
      const sub = this.bookOptions$.subscribe(list => { if (list?.length) this.pickBook(list[0]); sub.unsubscribe(); });
    }
  }

  pickMember(it: LookItem) {
    this.selectedMember = it;
    this.form.patchValue({ memberId: it.id });
    this.memberSearch.setValue(it.label, { emitEvent: false });
    this.memberMenuOpen = false;
  }

  clearMember() {
    this.selectedMember = undefined;
    this.form.patchValue({ memberId: null });
    this.memberSearch.setValue('', { emitEvent: true });
    this.memberMenuOpen = true;
  }

  pickBook(it: LookItem) {
    this.selectedBook = it;
    this.form.patchValue({ bookId: it.id });
    this.bookSearch.setValue(it.label, { emitEvent: false });
    this.bookMenuOpen = false;
  }

  clearBook() {
    this.selectedBook = undefined;
    this.form.patchValue({ bookId: null });
    this.bookSearch.setValue('', { emitEvent: true });
    this.bookMenuOpen = true;
  }

  submit() {
    const v = this.form.getRawValue();
    if (!v.memberId) { this.backendErrors.set(['يجب اختيار عضو.']); this.fieldHasBackendErr.memberId.set(true); return; }
    if (!v.bookId)   { this.backendErrors.set(['يجب اختيار كتاب.']); this.fieldHasBackendErr.bookId.set(true);   return; }

    this.save.emit({
      id: v.id ?? undefined,
      memberId: Number(v.memberId),
      bookId: Number(v.bookId),
      durationDays: Number(v.durationDays) || 14,
    });
  }
}