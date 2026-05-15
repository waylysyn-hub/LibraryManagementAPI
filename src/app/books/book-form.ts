import { Component, EventEmitter, Input, Output, ViewChild, ElementRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Book } from '../api/types';

@Component({
  standalone: true,
  selector: 'app-book-form',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './book-form.html',
  styleUrls: ['./book-form.css']
})
export class BookFormComponent {
  private fb = inject(FormBuilder);

  @ViewChild('dlg') dlg!: ElementRef<HTMLDialogElement>;

  @Input() title = 'إضافة كتاب';
  @Input() initial: Partial<Book> | null = null;

  @Output() cancel = new EventEmitter<void>();
  @Output() save = new EventEmitter<Omit<Book, 'id' | 'borrowCount'> & { id?: number }>();

  backendErrors = signal<string[]>([]);
  readonly currentYear = new Date().getFullYear();

  form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.required, Validators.maxLength(200)]],
    category: [''],
    year: [null as number | null, [Validators.required, Validators.min(1500), Validators.max(this.currentYear)]],
    copiesCount: [null as number | null, [Validators.required, Validators.min(1), Validators.max(1000)]],
    // Regex مرن لـ ISBN-10/13 مع أو بدون فواصل
    isbn: ['', [Validators.pattern(/^(97(8|9))?\d{9}(\d|X)$|^(97(8|9))?[-\s]?\d{1,5}[-\s]?\d{1,7}[-\s]?\d{1,7}[-\s]?(\d|X)$/i)]]
  });

  open(book?: Book) {
    this.title = book ? 'تعديل كتاب' : 'إضافة كتاب';
    this.backendErrors.set([]);

    if (book) {
      this.form.patchValue({
        title: book.title,
        author: book.author,
        category: book.category ?? '',
        year: book.year ?? null,
        copiesCount: book.copiesCount ?? null,
        isbn: book.isbn ?? ''
      });
      this.initial = book;
    } else {
      this.form.reset({
        title: '',
        author: '',
        category: '',
        year: null,
        copiesCount: null,
        isbn: ''
      });
      this.initial = null;
    }

    this.dlg.nativeElement.showModal();
  }

  close() {
    this.dlg.nativeElement.close();
  }

  submit() {
    this.backendErrors.set([]);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const payload = {
      ...(this.initial?.id ? { id: this.initial.id } : {}),
      title: v.title.trim(),
      author: v.author.trim(),
      category: v.category?.trim() || undefined,
      year: v.year ?? undefined,
      copiesCount: v.copiesCount ?? undefined,
      isbn: v.isbn?.trim() || undefined
    };

    this.save.emit(payload);
  }

  setBackendErrors(errors: string[]) {
    this.backendErrors.set(errors);
  }
}
