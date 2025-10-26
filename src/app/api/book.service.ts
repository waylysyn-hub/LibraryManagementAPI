import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, catchError, throwError } from 'rxjs';
import { Book, BookQuery, PagedResult } from './types';

@Injectable({ providedIn: 'root' })
export class BookService {
  private http = inject(HttpClient);
  private base = 'https://localhost:7091/api/Books';

  list(q: BookQuery): Observable<PagedResult<Book>> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v === undefined || v === null || v === '') return;
      params = params.set(k, String(v));
    });

    return this.http.get<any>(this.base, { params }).pipe(
      map(res => this.unwrapPaged<Book>(res, q)),
      catchError(this.handleError)
    );
  }

  get(id: number): Observable<Book> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(
      map(res => this.unwrapOne<Book>(res)),
      catchError(this.handleError)
    );
  }

  create(body: Omit<Book, 'id' | 'borrowCount'>): Observable<Book> {
    return this.http.post<any>(this.base, body).pipe(
      map(res => this.unwrapOne<Book>(res)),
      catchError(this.handleError)
    );
  }

  update(id: number, body: Partial<Omit<Book, 'id'>>): Observable<Book> {
    return this.http.put<any>(`${this.base}/${id}`, body).pipe(
      map(res => this.unwrapOne<Book>(res)),
      catchError(this.handleError)
    );
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(
      catchError(this.handleError)
    );
  }

  export(q: BookQuery): Observable<Blob> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v === undefined || v === null || v === '') return;
      params = params.set(k, String(v));
    });
    return this.http.get(`${this.base}/export`, { params, responseType: 'blob' }).pipe(
      catchError(this.handleError)
    );
  }

  // ---------- Helpers ----------
  private unwrapOne<T>(res: any): T {
    return (res?.data ?? res) as T;
  }

  private unwrapPaged<T>(res: any, q: BookQuery): PagedResult<T> {
    // 1) الشكل القياسي: { data: T[], meta: {...} }
    if (res?.meta && Array.isArray(res?.data)) {
      const items = res.data as T[];
      const meta  = res.meta || {};
      const page      = Number(meta.page ?? q.Page ?? 1);
      const pageSize  = Number(meta.pageSize ?? q.PageSize ?? items.length);
      const total     = Number(meta.total ?? items.length);
      const totalPages= Number(meta.totalPages ?? Math.ceil(total / Math.max(1, pageSize)));

      return {
        success: Boolean(res?.success ?? true),
        message: res?.message ?? 'تم الجلب بنجاح',
        items,
        total,
        data: items,
        meta: {
          page,
          pageSize,
          total,
          totalPages,
          sortBy: meta.sortBy ?? 'Id',
          sortDir: meta.sortDir ?? 'asc'
        }
      };
    }

    // 2) بيانات كمصفوفة فقط (بدون meta)
    const data = res?.data ?? res;
    if (Array.isArray(data)) {
      const items = data as T[];
      const page     = q.Page ?? 1;
      const pageSize = q.PageSize ?? items.length;
      const total    = items.length;

      return {
        success: true,
        message: 'تم الجلب بنجاح',
        items,
        total,
        data: items,
        meta: {
          page,
          pageSize,
          total,
          totalPages: Math.ceil(total / Math.max(1, pageSize)),
          sortBy: 'Id',
          sortDir: 'asc'
        }
      };
    }

    // 3) fallback لأشكال مخصّصة { items/Rows, total/Total, page/Page, pageSize/PageSize }
    const items = (data?.items ?? data?.Rows ?? []) as T[];
    const total = Number(data?.total ?? data?.Total ?? items.length);
    const page  = Number(data?.page ?? data?.Page ?? q.Page ?? 1);
    const pageSize = Number(data?.pageSize ?? data?.PageSize ?? q.PageSize ?? items.length);

    return {
      success: true,
      message: 'تم الجلب بنجاح',
      items,
      total,
      data: items,
      meta: {
        page,
        pageSize,
        total,
        totalPages: Math.ceil(total / Math.max(1, pageSize)),
        sortBy: 'Id',
        sortDir: 'asc'
      }
    };
  }

  // مرّر الخطأ كما هو ليستطيع الـ component قراءة ModelState/ProblemDetails
  private handleError(error: any) {
    return throwError(() => error);
  }
}
