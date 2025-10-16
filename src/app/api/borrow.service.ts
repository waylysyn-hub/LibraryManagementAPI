// src/app/api/borrow.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable, map, catchError, of } from 'rxjs';

export interface BorrowRecordDto {
  id: number;
  memberId: number;
  bookId: number;
  borrowedDate: string;  // ISO
  dueDate: string;       // ISO
  returnedDate?: string | null;
}

export interface BorrowListResponse {
  items: BorrowRecordDto[];
  total: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class BorrowService {
  private http = inject(HttpClient);
  // عدّل إذا كان عندك environment
  private base = 'https://localhost:7091';

  private headers(): HttpHeaders {
    const token = localStorage.getItem('access_token') || '';
    let h = new HttpHeaders().set('Accept', 'application/json');
    if (token) h = h.set('Authorization', `Bearer ${token}`);
    return h;
  }

  list(options: { memberId?: number; bookId?: number; page?: number; pageSize?: number }): Observable<BorrowListResponse> {
    let params = new HttpParams();
    if (options.memberId != null) params = params.set('memberId', options.memberId);
    if (options.bookId != null)   params = params.set('bookId', options.bookId);
    params = params.set('page', String(options.page ?? 1));
    params = params.set('pageSize', String(options.pageSize ?? 50));

    return this.http.get<any>(`${this.base}/api/BorrowRecords`, { headers: this.headers(), params }).pipe(
      map(res => {
        // شكل الباك: { success:true, data:[...], meta:{page,pageSize,total,totalPages} }
        if (Array.isArray(res?.data)) {
          return {
            items: res.data as BorrowRecordDto[],
            total: res?.meta?.total ?? res.data.length,
            page: res?.meta?.page ?? (options.page ?? 1),
            pageSize: res?.meta?.pageSize ?? (options.pageSize ?? 50),
          };
        }
        // fallback لو رجّع مصفوفة مباشرة (غير متوقع بس للاحتياط)
        if (Array.isArray(res)) {
          return { items: res as BorrowRecordDto[], total: res.length, page: 1, pageSize: res.length };
        }
        return { items: [], total: 0, page: 1, pageSize: options.pageSize ?? 50 };
      }),
      catchError(err => {
        // 404 NotFound => لا توجد بيانات
        if (err?.status === 404) {
          return of({ items: [], total: 0, page: options.page ?? 1, pageSize: options.pageSize ?? 50 });
        }
        throw err;
      })
    );
  }

  get(id: number) {
    return this.http.get<any>(`${this.base}/api/BorrowRecords/${id}`, { headers: this.headers() });
  }

  create(body: { memberId: number; bookId: number; durationDays: number }) {
    return this.http.post<any>(`${this.base}/api/BorrowRecords`, body, { headers: this.headers() });
  }

  update(id: number, body: { memberId: number; bookId: number; durationDays: number }) {
    return this.http.put<any>(`${this.base}/api/BorrowRecords/${id}`, body, { headers: this.headers() });
  }

  delete(id: number) {
    return this.http.delete<any>(`${this.base}/api/BorrowRecords/${id}`, { headers: this.headers() });
  }

  return(id: number) {
    return this.http.post<any>(`${this.base}/api/BorrowRecords/${id}/return`, {}, { headers: this.headers() });
  }

  export(memberId?: number, bookId?: number) {
    let params = new HttpParams();
    if (memberId != null) params = params.set('memberId', memberId);
    if (bookId != null)   params = params.set('bookId', bookId);
    return this.http.get(`${this.base}/api/BorrowRecords/export`, {
      headers: this.headers(),
      params,
      responseType: 'blob',
      observe: 'response'
    });
  }
}
