import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

const TOKEN_KEY = 'authToken';
const REMEMBER_FLAG = 'rememberMe'; // '1' إذا محفوظ في localStorage

interface AuthData {
  token: string;
  role?: string;
  permissions?: string[];
}
interface AuthResponse {
  success: boolean;
  message: string;
  data: AuthData;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private http: HttpClient) {}

  login(
    credentials: { email: string; password: string },
    rememberMe: boolean
  ): Observable<AuthResponse> {
    const url = '/api/Auth/login';

    return this.http.post<AuthResponse>(url, credentials).pipe(
      tap((res: any) => {
        console.log('[AuthService] login response:', res);

        const token =
          res?.data?.token ??
          res?.token ??
          res?.jwt ??
          res?.accessToken ??
          res?.data?.jwt ??
          res?.data?.accessToken ??
          null;

        this.clearAll();

        if (token) {
          if (rememberMe) {
            localStorage.setItem(TOKEN_KEY, token);
            localStorage.setItem(REMEMBER_FLAG, '1');
            console.log('[AuthService] token stored in localStorage');
          } else {
            sessionStorage.setItem(TOKEN_KEY, token);
            console.log('[AuthService] token stored in sessionStorage');
          }
        } else {
          console.warn('[AuthService] no token found in response');
        }

        // اختيارياً: دور وصلاحيات للواجهة
        const role = res?.data?.role ?? res?.role;
        const permissions = res?.data?.permissions ?? res?.permissions;
        if (role) localStorage.setItem('role', role);
        if (permissions) localStorage.setItem('permissions', JSON.stringify(permissions));
      })
    );
  }

  getToken(): string | null {
    // تفضيل جلسة المتصفح (تزول عند الإغلاق) ثم المحلية
    return sessionStorage.getItem(TOKEN_KEY) ?? localStorage.getItem(TOKEN_KEY);
  }

  clearAll(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REMEMBER_FLAG);
    sessionStorage.removeItem('sessionLogin');
    localStorage.removeItem('role');
    localStorage.removeItem('permissions');
  }

  logout(): void {
    this.clearAll();
  }
}
