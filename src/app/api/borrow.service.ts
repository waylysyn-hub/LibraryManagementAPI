// src/app/api/borrow.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
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
  // ✅ مسار نسبي — apiBaseInterceptor سيضيف الدومين
  private base = '/api/BorrowRecords';

  list(options: { memberId?: number; bookId?: number; page?: number; pageSize?: number }): Observable<BorrowListResponse> {
    let params = new HttpParams()
      .set('page', String(options.page ?? 1))
      .set('pageSize', String(options.pageSize ?? 50));

    if (options.memberId != null) params = params.set('memberId', options.memberId);
    if (options.bookId != null)   params = params.set('bookId', options.bookId);

    return this.http.get<any>(this.base, { params }).pipe(
      map(res => {
        if (Array.isArray(res?.data)) {
          return {
            items: res.data as BorrowRecordDto[],
            total: res?.meta?.total ?? res.data.length,
            page: res?.meta?.page ?? (options.page ?? 1),
            pageSize: res?.meta?.pageSize ?? (options.pageSize ?? 50),
          };
        }
        if (Array.isArray(res)) {
          return { items: res as BorrowRecordDto[], total: res.length, page: 1, pageSize: res.length };
        }
        return { items: [], total: 0, page: options.page ?? 1, pageSize: options.pageSize ?? 50 };
      }),
      catchError(err => {
        if (err?.status === 404) {
          return of({ items: [], total: 0, page: options.page ?? 1, pageSize: options.pageSize ?? 50 });
        }
        throw err;
      })
    );
  }

  get(id: number) {
    return this.http.get<any>(`${this.base}/${id}`);
  }

  create(body: { memberId: number; bookId: number; durationDays: number }) {
    return this.http.post<any>(this.base, body);
  }

  update(id: number, body: { memberId: number; bookId: number; durationDays: number }) {
    return this.http.put<any>(`${this.base}/${id}`, body);
  }

  delete(id: number) {
    return this.http.delete<any>(`${this.base}/${id}`);
  }

  return(id: number) {
    return this.http.post<any>(`${this.base}/${id}/return`, {});
  }

  export(memberId?: number, bookId?: number): Observable<HttpResponse<Blob>> {
    let params = new HttpParams();
    if (memberId != null) params = params.set('memberId', memberId);
    if (bookId != null)   params = params.set('bookId', bookId);

    // 👇 cast قياسي مع blob
    return this.http.get<Blob>(`${this.base}/export`, {
      params,
      responseType: 'blob' as 'json',
      observe: 'response'
    });
  }
}
