// src/app/api/users.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

export type UserRoleForCreate = 'Admin' | 'Employee';

export interface UserRow {
  id: number;
  username: string;
  email: string;
  roleId: number;
  createdAt: string;
  phone?: string | null;
}

export interface ApiListUsers {
  success: boolean;
  count: number;
  data: Array<{
    id: number; username: string; email: string; roleId: number; createdAt: string; phone?: string | null;
  }>;
}

export interface ApiUserOne {
  success: boolean;
  data: {
    id: number; username: string; email: string; roleId: number; createdAt: string;
    permissions?: Array<{ id: number; name: string }>;
    phone?: string | null;
  };
}

export interface ApiMsg {
  success: boolean;
  message: string;
}
export interface RegisterStartReq { email: string; }
export interface RegisterStartRes {
  success: boolean;
  message: string;
  requestId: string;
  devCode?: string;
}

export interface RegisterConfirmReq {
  requestId: string;
  code: string;
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  roleId: number;       // 1=Admin, 2=Employee, 3=Member
  phone?: string | null;
}
export interface RegisterConfirmRes {
  success: boolean;
  userId?: number;
  message: string;
}
export interface AdminRegisterDto {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  phone?: string | null;
  role: UserRoleForCreate; // Admin | Employee
}

export interface UserUpdateDto {
  username: string;
  email: string;
  phone?: string | null;
}

export interface UpdatePasswordDto {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  // ✅ مسار نسبي — apiBaseInterceptor سيضيف الدومين
  private base = '/api/Users';
    registerStart(dto: RegisterStartReq) {
    return this.http.post<RegisterStartRes>(`${this.base}/register-start`, dto);
  }
  registerConfirm(dto: RegisterConfirmReq) {
    return this.http.post<RegisterConfirmRes>(`${this.base}/register-confirm`, dto);
  }
  constructor(private http: HttpClient) {}
adminUpdateStart(id: number, dto: { username: string; email: string; phone: string | null }) {
  return this.http.post<any>(`${this.base}/${id}/admin-update-start`, dto);
}

adminUpdateConfirm(
  id: number,
  dto: { requestId: string; code: string; username: string; email: string; phone: string | null }
) {
  return this.http.post<any>(`${this.base}/${id}/admin-update-confirm`, dto);
}
  list(): Observable<ApiListUsers> {
    return this.http.get<ApiListUsers>(this.base);
  }

  getById(id: number): Observable<ApiUserOne> {
    return this.http.get<ApiUserOne>(`${this.base}/${id}`);
  }

  // إنشاء (Admin/Employee) بصيغة x-www-form-urlencoded كما يطلب الكنترولر
  adminRegister(dto: AdminRegisterDto): Observable<HttpResponse<any>> {
    const body =
      `Username=${encodeURIComponent(dto.username)}&` +
      `Email=${encodeURIComponent(dto.email)}&` +
      `Password=${encodeURIComponent(dto.password)}&` +
      `ConfirmPassword=${encodeURIComponent(dto.confirmPassword)}&` +
      (dto.phone ? `Phone=${encodeURIComponent(dto.phone)}&` : '') +
      `Role=${encodeURIComponent(dto.role)}`;

    const headers = new HttpHeaders({ 'Content-Type': 'application/x-www-form-urlencoded' });

    return this.http.post<any>(`${this.base}/admin-register`, body, {
      headers,
      observe: 'response'
    });
  }

  update(id: number, dto: UserUpdateDto): Observable<ApiMsg> {
    return this.http.put<ApiMsg>(`${this.base}/${id}`, dto);
  }

  updatePassword(id: number, dto: UpdatePasswordDto): Observable<ApiMsg> {
    return this.http.put<ApiMsg>(`${this.base}/${id}/password`, dto);
  }

  delete(id: number): Observable<ApiMsg> {
    return this.http.delete<ApiMsg>(`${this.base}/${id}`);
  }
}
