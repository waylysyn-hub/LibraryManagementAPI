import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError, map, catchError } from 'rxjs';
import {
  DashboardStats,
  MostBorrowedRow,
  OverdueRow,
  ActiveMemberRow,
  BorrowRecordsQuery,
  BorrowRecordExportRow,
  BorrowTrendPoint,
  TrendBucket,
  CategoryStat,
  Cohorts,
} from './types';
export interface Option {
  id: number;
  label: string;
}
@Injectable({ providedIn: 'root' })
export class ReportsService {
  [x: string]: any;
  private http = inject(HttpClient);
  private base = 'https://localhost:7091/api/Reports';
  private membersBase = 'https://localhost:7091/api/Members';
  private booksBase = 'https://localhost:7091/api/Books';
  private handleError(error: any) {
    return throwError(() => error);
  }

  private unwrapArray<T>(res: any): T[] {
    if (Array.isArray(res)) return res as T[];
    if (Array.isArray(res?.data)) return res.data as T[];
    return [];
  }

  dashboardStats(): Observable<DashboardStats> {
    return this.http
      .get<DashboardStats>(`${this.base}/dashboard-stats`)
      .pipe(catchError(this.handleError));
  }

  mostBorrowed(limit = 10): Observable<MostBorrowedRow[]> {
    const params = new HttpParams().set('limit', String(limit));
    return this.http.get<any>(`${this.base}/most-borrowed-books`, { params }).pipe(
      map((res) => this.unwrapArray<MostBorrowedRow>(res)),
      catchError(this.handleError)
    );
  }

  overdueBooks(): Observable<OverdueRow[]> {
    return this.http.get<any>(`${this.base}/overdue-books`).pipe(
      map((res) => this.unwrapArray<OverdueRow>(res)),
      catchError(this.handleError)
    );
  }

  activeMembers(limit = 10): Observable<ActiveMemberRow[]> {
    const params = new HttpParams().set('limit', String(limit));
    return this.http.get<any>(`${this.base}/active-members`, { params }).pipe(
      map((res) => this.unwrapArray<ActiveMemberRow>(res)),
      catchError(this.handleError)
    );
  }

  borrowRecords(q: BorrowRecordsQuery): Observable<BorrowRecordExportRow[]> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    });
    return this.http.get<any>(`${this.base}/borrow-records`, { params }).pipe(
      map((res) => this.unwrapArray<BorrowRecordExportRow>(res)),
      catchError(this.handleError)
    );
  }
  searchBooks(term: string): Observable<Option[]> {
    let params = new HttpParams().set('Q', term).set('Page', '1').set('PageSize', '8');
    return this.http.get<any>(this.booksBase, { params }).pipe(
      map((res) => {
        const rows = this.unwrapArray<any>(res);
        // توقع الحقول: id, title
        return rows.map((b: any) => ({ id: b.id, label: b.title } as Option));
      }),
      catchError(() => [])
    );
  }
  // === خيارات جاهزة للأعضاء والكتب ===
  listMemberOptions(pageSize = 200): Observable<Option[]> {
    let params = new HttpParams().set('Page', '1').set('PageSize', String(pageSize));
    // نتوقع أن /api/Members يرجّع {data:[], meta...} أو مصفوفة مباشرة
    return this.http.get<any>(this.membersBase, { params }).pipe(
      map((res) => {
        const rows = this.unwrapArray<any>(res);
        return rows.map((m: any) => ({ id: m.id, label: m.name } as Option));
      }),
      catchError(() => [])
    );
  }

  listBookOptions(pageSize = 200): Observable<Option[]> {
    let params = new HttpParams().set('Page', '1').set('PageSize', String(pageSize));
    return this.http.get<any>(this.booksBase, { params }).pipe(
      map((res) => {
        const rows = this.unwrapArray<any>(res);
        return rows.map((b: any) => ({ id: b.id, label: b.title } as Option));
      }),
      catchError(() => [])
    );
  }

  searchMembers(term: string): Observable<Option[]> {
    let params = new HttpParams().set('Q', term).set('Page', '1').set('PageSize', '8');
    return this.http.get<any>(this.membersBase, { params }).pipe(
      map((res) => {
        const rows = this.unwrapArray<any>(res);
        // توقع الحقول: id, name
        return rows.map((m: any) => ({ id: m.id, label: m.name } as Option));
      }),
      catchError(() => []) // ارجع مصفوفة فاضية بصمت
    );
  }

  exportBorrowPdf(q: BorrowRecordsQuery): Observable<Blob> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    });
    return this.http
      .get(`${this.base}/borrow-records/export-pdf`, { params, responseType: 'blob' })
      .pipe(catchError(this.handleError));
  }

  exportBorrowXlsx(q: BorrowRecordsQuery): Observable<Blob> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    });
    return this.http
      .get(`${this.base}/borrow-records/export-xlsx`, { params, responseType: 'blob' })
      .pipe(catchError(this.handleError));
  }

  // ===== جديد =====
  borrowingTrend(
    fromISO: string,
    toISO: string,
    bucket: TrendBucket = 'day'
  ): Observable<BorrowTrendPoint[]> {
    const params = new HttpParams().set('from', fromISO).set('to', toISO).set('bucket', bucket);
    return this.http.get<any>(`${this.base}/borrowing-trend`, { params }).pipe(
      map((res) => this.unwrapArray<BorrowTrendPoint>(res)),
      catchError(this.handleError)
    );
  }

  categoriesTop(limit = 10): Observable<CategoryStat[]> {
    const params = new HttpParams().set('limit', String(limit));
    return this.http.get<any>(`${this.base}/categories-top`, { params }).pipe(
      map((res) => this.unwrapArray<CategoryStat>(res)),
      catchError(this.handleError)
    );
  }

  memberCohorts(days = 90): Observable<Cohorts> {
    const params = new HttpParams().set('days', String(days));
    return this.http
      .get<Cohorts>(`${this.base}/member-cohorts`, { params })
      .pipe(catchError(this.handleError));
  }
}
