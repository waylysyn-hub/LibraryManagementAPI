import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { catchError, of } from 'rxjs'; // ⬅︎ جديد

export interface CodeRequestDto {
  email: string;
}
export interface CodeRequestRes {
  success: boolean;
  message: string;
  resetId: string;
  devCode?: string;
}
export interface CodeVerifyDto {
  resetId: string;
  code: string;
} // ⬅︎ جديد
export interface CodeConfirmDto {
  resetId: string;
  code: string;
  newPassword: string;
  confirmNewPassword: string;
}
export interface ApiRes {
  success: boolean;
  message: string;
}
export interface EmailCheckDto {
  email: string;
}
export interface EmailCheckRes {
  success: boolean;
  exists: boolean;
  message?: string;
}

@Injectable({ providedIn: 'root' })
export class ResetService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/Users`;

  requestCode(dto: CodeRequestDto) {
    return this.http.post<CodeRequestRes>(`${this.base}/password/code-request`, dto);
  }

  verifyCode(dto: CodeVerifyDto) {
    return this.http.post<ApiRes>(`${this.base}/password/code-verify`, dto);
  }
  checkEmail(dto: EmailCheckDto) {
    return this.http.post<EmailCheckRes>(`${this.base}/email/check`, dto);
  }

  confirmCode(dto: CodeConfirmDto) {
    return this.http.post<ApiRes>(`${this.base}/password/code-confirm`, dto);
  }
}
