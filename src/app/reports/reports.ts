import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { ReportsService, Option } from '../api/reports.service';
import {
  DashboardStats, MostBorrowedRow, OverdueRow, ActiveMemberRow, BorrowRecordsQuery,
  BorrowTrendPoint, CategoryStat, Cohorts, TrendBucket
} from '../api/types';
import { ToastService } from '../ui/toasts/toast.service';
import { NgIf } from '@angular/common';
function toISODate(d: any): string | undefined {
  if (!d) return undefined;
  try { const dt = new Date(d); if (isNaN(+dt)) return undefined; return dt.toISOString().slice(0,10); }
  catch { return undefined; }
}

@Component({
  standalone: true,
  selector: 'app-reports',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './reports.html',
  styleUrls: ['./reports.css']
})
export class ReportsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private api = inject(ReportsService);
  private toast = inject(ToastService);

  // Loading flags
  loadingStats = signal(false);
  loadingMost = signal(false);
  loadingOverdue = signal(false);
  loadingMembers = signal(false);
  loadingBorrow = signal(false);

  // جديد
  loadingTrend = signal(false);
  loadingCats  = signal(false);
  loadingCoh   = signal(false);
  loadingMemberOpts = signal(false);
  loadingBookOpts   = signal(false);

  // Data
  stats = signal<DashboardStats | null>(null);
  mostBorrowed = signal<MostBorrowedRow[]>([]);
  overdue = signal<OverdueRow[]>([]);
  activeMembers = signal<ActiveMemberRow[]>([]);
  borrowRows = signal<any[]>([]);

  // جديد
  trend = signal<BorrowTrendPoint[]>([]);
  categories = signal<CategoryStat[]>([]);
  cohorts = signal<Cohorts | null>(null);

  // خيارات القوائم
  memberOptions = signal<Option[]>([]);
  bookOptions   = signal<Option[]>([]);

  // نطاق افتراضي للترند (آخر 30 يوم)
  todayISO = new Date().toISOString().slice(0,10);
  from30ISO = new Date(Date.now() - 29*24*3600*1000).toISOString().slice(0,10);

  // Filters (IDs فقط)
  filters = this.fb.nonNullable.group({
    memberId: [null as number | null],
    bookId: [null as number | null],
    fromDate: [''],
    toDate: ['']
  });

  ngOnInit(): void {
    // تحميل القوائم
    this.loadMemberOptions();
    this.loadBookOptions();

    // كروت وإحصائيات
    this.loadDashboard();
    this.loadMostBorrowed();
    this.loadOverdue();
    this.loadActiveMembers();

    // التحليلات
    this.loadTrend(this.from30ISO, this.todayISO, 'day');
    this.loadCategories(8);
    this.loadCohorts(90);

    // جدول السجلات
    this.searchBorrowRecords();
  }

  private serverMsg(e: any): string {
    if (e?.error?.message) return e.error.message;
    if (e?.error?.title) return e.error.title;
    if (e?.message) return e.message;
    return 'حدث خطأ غير متوقع.';
  }

  // تحميل قوائم الأسماء
  loadMemberOptions() {
    this.loadingMemberOpts.set(true);
    this.api.listMemberOptions(200).subscribe({
      next: opts => { this.memberOptions.set(opts); this.loadingMemberOpts.set(false); },
      error: _ => { this.memberOptions.set([]); this.loadingMemberOpts.set(false); }
    });
  }
  loadBookOptions() {
    this.loadingBookOpts.set(true);
    this.api.listBookOptions(200).subscribe({
      next: opts => { this.bookOptions.set(opts); this.loadingBookOpts.set(false); },
      error: _ => { this.bookOptions.set([]); this.loadingBookOpts.set(false); }
    });
  }

  // نسبة للترند
  percent(value: number | undefined | null, max: number): number {
    const v = Number(value ?? 0), m = Number(max ?? 0);
    if (!m || m <= 0 || !isFinite(m)) return 0;
    const pct = (v / m) * 100;
    return v > 0 && pct < 1 ? 1 : Math.min(100, Math.max(0, pct));
  }

  loadDashboard() {
    this.loadingStats.set(true);
    this.api.dashboardStats().subscribe({
      next: s => { this.stats.set(s); this.loadingStats.set(false); },
      error: e => { this.loadingStats.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }

  loadMostBorrowed(limit = 10) {
    this.loadingMost.set(true);
    this.api.mostBorrowed(limit).subscribe({
      next: rows => { this.mostBorrowed.set(rows); this.loadingMost.set(false); },
      error: e => { this.loadingMost.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }

  loadOverdue() {
    this.loadingOverdue.set(true);
    this.api.overdueBooks().subscribe({
      next: rows => { this.overdue.set(rows); this.loadingOverdue.set(false); },
      error: e => { this.loadingOverdue.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }

  loadActiveMembers(limit = 10) {
    this.loadingMembers.set(true);
    this.api.activeMembers(limit).subscribe({
      next: rows => { this.activeMembers.set(rows); this.loadingMembers.set(false); },
      error: e => { this.loadingMembers.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }

  // Borrow records browse
  searchBorrowRecords() {
    const v = this.filters.getRawValue();
    const q: BorrowRecordsQuery = {
      memberId: v.memberId ?? undefined,
      bookId: v.bookId ?? undefined,
      fromDate: toISODate(v.fromDate),
      toDate: toISODate(v.toDate)
    };
    this.loadingBorrow.set(true);
    this.api.borrowRecords(q).subscribe({
      next: rows => { this.borrowRows.set(rows); this.loadingBorrow.set(false); },
      error: e => { this.borrowRows.set([]); this.loadingBorrow.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }

  onResetFilters() {
    this.filters.reset({ memberId: null, bookId: null, fromDate: '', toDate: '' }, { emitEvent: false });
    this.searchBorrowRecords();
  }

  exportPdf() {
    const v = this.filters.getRawValue();
    const q: BorrowRecordsQuery = {
      memberId: v.memberId ?? undefined,
      bookId: v.bookId ?? undefined,
      fromDate: toISODate(v.fromDate),
      toDate: toISODate(v.toDate)
    };
    this.api.exportBorrowPdf(q).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = 'borrow-records.pdf'; a.click();
        URL.revokeObjectURL(url);
        this.toast.success('تمّ توليد PDF بنجاح');
      },
      error: e => this.toast.error(this.serverMsg(e))
    });
  }

  exportXlsx() {
    const v = this.filters.getRawValue();
    const q: BorrowRecordsQuery = {
      memberId: v.memberId ?? undefined,
      bookId: v.bookId ?? undefined,
      fromDate: toISODate(v.fromDate),
      toDate: toISODate(v.toDate)
    };
    this.api.exportBorrowXlsx(q).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = 'borrow-records.xlsx'; a.click();
        URL.revokeObjectURL(url);
        this.toast.success('تمّ تصدير Excel بنجاح');
      },
      error: e => this.toast.error(this.serverMsg(e))
    });
  }

  // Trend / Categories / Cohorts
  loadTrend(fromISO: string, toISO: string, bucket: TrendBucket) {
    this.loadingTrend.set(true);
    this.api.borrowingTrend(fromISO, toISO, bucket).subscribe({
      next: rows => { this.trend.set(rows); this.loadingTrend.set(false); },
      error: e => { this.loadingTrend.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }
  loadCategories(limit = 8) {
    this.loadingCats.set(true);
    this.api.categoriesTop(limit).subscribe({
      next: rows => { this.categories.set(rows); this.loadingCats.set(false); },
      error: e => { this.loadingCats.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }
  loadCohorts(days = 90) {
    this.loadingCoh.set(true);
    this.api.memberCohorts(days).subscribe({
      next: data => { this.cohorts.set(data); this.loadingCoh.set(false); },
      error: e => { this.loadingCoh.set(false); this.toast.error(this.serverMsg(e)); }
    });
  }
  maxBorrowInTrend(): number { const arr = this.trend(); return arr.length ? Math.max(...arr.map(x => x.borrow || 0)) : 0; }
  maxReturnInTrend(): number { const arr = this.trend(); return arr.length ? Math.max(...arr.map(x => x.return || 0)) : 0; }
}
