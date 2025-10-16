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
  const data = res?.data ?? res; // التعامل مع البيانات القادمة من الـ API
  const page = q.Page ?? 1;
  const pageSize = q.PageSize ?? data.length;

  // التعامل مع حالة الـ Array
  if (Array.isArray(data)) {
    return {
      success: true,
      message: "تم الجلب بنجاح",  // رسالة نجاح، يمكنك تعديلها حسب الحاجة
      items: data as T[],  // البيانات نفسها
      total: data.length, // إجمالي العناصر
      data: data as T[],  // البيانات نفسها
      meta: {  // تضمين الـ pagination
        page,
        pageSize,
        total: data.length,
        totalPages: Math.ceil(data.length / pageSize),  // حساب إجمالي الصفحات
        sortBy: 'title',  // ترتيب الافتراضي
        sortDir: 'asc'   // ترتيب التصفية الافتراضي
      }
    };
  }

  // التعامل مع الحالة التي تكون فيها البيانات غير مصفوفة
  const items = (data?.items ?? data?.Rows ?? []) as T[];
  const total = Number(data?.total ?? data?.Total ?? items.length);
  const pageNumber = Number(data?.page ?? data?.Page ?? q.Page ?? 1);  // تجنب إعادة تعريف المتغير
  const pageSizeNumber = Number(data?.pageSize ?? data?.PageSize ?? q.PageSize ?? items.length);  // تجنب إعادة تعريف المتغير

  return {
    success: true,  // رسالة النجاح
    message: "تم الجلب بنجاح",  // رسالة النجاح
    items,  // البيانات المسترجعة
    total,  // إجمالي العناصر
    data: items,  // البيانات نفسها
    meta: {  // تضمين الـ pagination
      page: pageNumber,
      pageSize: pageSizeNumber,
      total,
      totalPages: Math.ceil(total / pageSizeNumber),  // حساب إجمالي الصفحات
      sortBy: 'title',  // ترتيب الافتراضي
      sortDir: 'asc'   // ترتيب التصفية الافتراضي
    }
  };
}




  // مرّر الخطأ كما هو (لا تحوّله لـ Error) ليقدر الـ component يقرأ ModelState/ProblemDetails
  private handleError(error: any) {
    return throwError(() => error);
  }
}
