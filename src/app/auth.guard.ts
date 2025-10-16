import { inject } from '@angular/core';
import { CanMatchFn, Router, Route, UrlSegment } from '@angular/router';
import { PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

// مفاتيح التخزين كما عندك
const TOKEN_KEY = 'authToken';

// دالة بسيطة لاستخراج الأدوار من الـJWT (claim: "role" أو "roles")
function getRolesFromToken(token: string | null): string[] {
  if (!token) return [];
  try {
    const payload = token.split('.')[1];
    if (!payload) return [];
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    // دعم صيغ متعددة: role=string | roles=string[] | "http://schemas..." إلخ
    const direct = json['role'] ?? json['roles'];
    if (Array.isArray(direct)) return direct.map(String);
    if (typeof direct === 'string') return [direct];

    // محاولة claims معيارية لـ ASP.NET
    const claimRole =
      json['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
      json['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role'];
    if (Array.isArray(claimRole)) return claimRole.map(String);
    if (typeof claimRole === 'string') return [claimRole];

    return [];
  } catch {
    return [];
  }
}

// حارس واحد يتعامل مع (التأكد من التوكن) + (التحقق من الأدوار إن وُجدت في data.roles)
export const authzCanMatch: CanMatchFn = (route: Route, _segments: UrlSegment[]) => {
  const router = inject(Router);
  const platformId = inject(PLATFORM_ID);

  // أثناء SSR لا نمنع، القرار في المتصفح
  if (!isPlatformBrowser(platformId)) return true;

  const token = localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY);
  const currentPath =
    typeof location !== 'undefined' ? location.pathname + location.search : '/';

  // 1) التحقق من وجود التوكن
  if (!token) {
    router.navigate(['/login'], { queryParams: { returnUrl: currentPath } });
    return false;
  }

  // 2) لو المسار يطلب أدوار محددة
  const allowedRoles = (route?.data?.['roles'] as string[] | undefined) ?? undefined;
  if (!allowedRoles || allowedRoles.length === 0) {
    return true; // لا يوجد شرط أدوار، يكفي وجود توكن
  }

  const userRoles = getRolesFromToken(token).map(r => r.toLowerCase());
  const allowed = allowedRoles.some(r => userRoles.includes(String(r).toLowerCase()));
  if (allowed) return true;

  // لا يملك الدور المطلوب → رجّعه لصفحة افتراضية (مثل /books) أو صفحة "غير مخوّل"
  router.navigate(['/books']);
  return false;
};
