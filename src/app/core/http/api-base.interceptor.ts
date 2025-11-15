// src/app/core/http/api-base.interceptor.ts
import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export const apiBaseInterceptor: HttpInterceptorFn = (req, next) => {
  // اترك الروابط المطلقة كما هي
  const isAbsolute = /^https?:\/\//i.test(req.url);

  // لو الرابط نسبي أضف الدومين من environment.apiBase
  if (!isAbsolute) {
    const clean = req.url.startsWith('/') ? req.url : '/' + req.url;
    req = req.clone({ url: environment.apiBase + clean });
  }

  return next(req);
};
