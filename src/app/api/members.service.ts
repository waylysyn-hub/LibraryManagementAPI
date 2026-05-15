// src/app/api/members.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MemberDto {
  id: number;
  userId: number;
  name: string;
  email: string;
  phone?: string | null;
  registeredAt: string;
}

export type MemberSortBy = 'Name'|'Email'|'RegisteredAt'|'Id';
export type SortDir = 'asc'|'desc';

export interface MemberQuery {
  Q?: string;
  Name?: string;
  Email?: string;
  Phone?: string;
  RegisteredFrom?: string;
  RegisteredTo?: string;
  Page?: number;
  PageSize?: number;
  SortBy?: MemberSortBy;
  SortDir?: SortDir;
}

export interface ApiPaged<T> {
  success: boolean;
  message?: string;
  data: T[];
  meta: {
    page: number;
    pageSize: number;
    total: number;
    totalPages: number;
    sortBy?: string;
    sortDir?: string;
  };
}

export interface ApiOne<T> {
  success: boolean;
  message?: string;
  data: T;
}

export interface ApiMsg {
  success: boolean;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class MembersService {
  // ✅ مسارات نسبية — apiBaseInterceptor سيضيف الدومين
  private base = '/api/Members';
  private userBase = '/api/Users';

  constructor(private http: HttpClient){}

  // قائمة الأعضاء
  list(q: MemberQuery): Observable<ApiPaged<MemberDto>> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') {
        params = params.set(k, String(v));
      }
    });
    return this.http.get<ApiPaged<MemberDto>>(this.base, { params });
  }
  lookupMembers(q: string, limit = 20) {
    const params = new HttpParams().set('q', q).set('limit', limit);
    return this.http.get<{ id:number; label:string; sub?:string }[]>(
      '/api/Lookup/members', { params }
    );
  }

  // تفاصيل
  getById(id: number): Observable<ApiOne<MemberDto>> {
    return this.http.get<ApiOne<MemberDto>>(`${this.base}/${id}`);
  }

  // تحديث ذاتي (/me) – إن لزم
  updateMe(dto: { name: string; email: string; phone?: string | null }): Observable<ApiMsg> {
    return this.http.put<ApiMsg>(`${this.base}/me`, dto);
  }

  // تحديث إداري
  adminUpdate(id: number, dto: { name: string; email: string; phone?: string | null }): Observable<ApiMsg> {
    return this.http.put<ApiMsg>(`${this.base}/${id}`, dto);
  }

  // Alias للتوافق
  update(id: number, dto: { name: string; email: string; phone?: string | null }): Observable<ApiMsg> {
    return this.adminUpdate(id, dto);
  }

  // حذف
  delete(id: number): Observable<ApiMsg> {
    return this.http.delete<ApiMsg>(`${this.base}/${id}`);
  }

  // تصدير
  export(q: MemberQuery): Observable<HttpResponse<Blob>> {
    let params = new HttpParams();
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') {
        params = params.set(k, String(v));
      }
    });
    // ملاحظة: cast قياسي مع Blob
    return this.http.get<Blob>(`${this.base}/export`, {
      params,
      observe: 'response',
      responseType: 'blob' as 'json'
    });
  }

  // تسجيل عام
  publicRegister(dto: {
    username: string;
    email: string;
    password: string;
    confirmPassword: string;
    name?: string;
    phone?: string | null;
  }): Observable<HttpResponse<any>> {
    return this.http.post<any>(`${this.userBase}/public-register`, dto, { observe: 'response' });
  }

  // استخراج اسم الملف
  static getFilename(resp: HttpResponse<Blob>, fallback = 'members.xlsx'): string {
    const cd = resp.headers.get('Content-Disposition') || resp.headers.get('content-disposition');
    if (!cd) return fallback;
    const m = /filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i.exec(cd);
    try { return m ? decodeURIComponent(m[1]) : fallback; } catch { return fallback; }
  }
}
