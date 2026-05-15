// src/app/auth.interceptor.ts
import { HttpInterceptorFn, HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { catchError } from 'rxjs/operators';
import { of, throwError } from 'rxjs';
import { AuthService } from './api/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const auth = inject(AuthService);
  const platformId = inject(PLATFORM_ID);
  const isBrowser = isPlatformBrowser(platformId);

  const url = (req.url || '').toLowerCase();
  const isLogin = /\/api\/auth\/login\b/i.test(url);
  const isDevAsset = url.includes('/@fs/') || url.includes('/vite/') || url.includes('.hot-update.');

  const token = isBrowser ? auth.getToken() : null;

  const wrapped = (!token || isLogin || isDevAsset)
    ? req
    : req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  // ✅ خاص بطلب اللوجين: حوّل أي 4xx/5xx إلى رد 200 بجسم success:false
  if (isLogin) {
    return next(wrapped).pipe(
      catchError((err: HttpErrorResponse) => {
        const msg =
          err?.error?.message ||
          err?.error?.title ||
          err?.error?.detail ||
          (err?.status === 401 ? 'Invalid email or password' : 'An unexpected error occurred.');
        const fake = new HttpResponse({
          status: 200,
          body: { success: false, message: msg, data: {} }
        });
        return of(fake);
      })
    );
  }

  // باقي الطلبات: سياسة 401/403 المعتادة
  return next(wrapped).pipe(
    catchError(err => {
      if (isBrowser && (err?.status === 401 || err?.status === 403)) {
        const alreadyLogin = location.pathname.startsWith('/login');
        const currentUrl = location.pathname + location.search;
        auth.clearAll();
        if (!alreadyLogin) router.navigate(['/login'], { queryParams: { returnUrl: currentUrl } });
      }
      return throwError(() => err);
    })
  );
};
