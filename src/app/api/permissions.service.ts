// src/app/api/permissions.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface PermissionDto { id: number; name: string; }
export interface ApiList<T> { success: boolean; data: T[]; message?: string; }
export interface ApiMsg { success: boolean; message: string; }

@Injectable({ providedIn: 'root' })
export class PermissionsService {
  // ✅ مسار نسبي — الإنترسبتور سيحوّله إلى <environment.apiBase>/api/Permissions
  private base = '/api/Permissions';

  constructor(private http: HttpClient) {}

  // كل الصلاحيات
  getAll(): Observable<ApiList<PermissionDto>> {
    return this.http.get<ApiList<PermissionDto>>(this.base);
  }

  // صلاحيات مستخدم معيّن
  getUser(userId: number): Observable<ApiList<PermissionDto>> {
    return this.http.get<ApiList<PermissionDto>>(`${this.base}/user/${userId}`);
  }

  // إضافة صلاحية للمستخدم
  addToUser(userId: number, permissionId: number): Observable<ApiMsg> {
    return this.http.post<ApiMsg>(`${this.base}/user/${userId}/add/${permissionId}`, {});
  }

  // إزالة/رفض صلاحية من المستخدم
  removeFromUser(userId: number, permissionId: number): Observable<ApiMsg> {
    return this.http.delete<ApiMsg>(`${this.base}/user/${userId}/remove/${permissionId}`);
  }
}
