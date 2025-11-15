import { Component, HostListener, inject, OnInit } from '@angular/core';
import { RouterOutlet, RouterModule, Router } from '@angular/router';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { NgIf } from '@angular/common';
@Component({
  standalone: true,
  selector: 'app-shell',
  imports: [CommonModule, RouterOutlet, RouterModule],
  template: `
  <div class="layout" dir="ltr" [class.sidebar-closed]="isSidebarClosed" [class.is-small]="!isLarge">
  <!-- الشريط الجانبي -->
  <aside class="sidebar" [class.closed]="isSidebarClosed" aria-label="الشريط الجانبي">
    <div class="brand">📚 نظام المكتبة</div>

<nav>
  <a *ngIf="canViewBooks"
     routerLink="/books"
     routerLinkActive="active"
     (click)="onNav()">
    <i class="fas fa-book"></i> الكتب
  </a>

  <a *ngIf="canViewBorrowings"
     routerLink="/borrowings"
     routerLinkActive="active"
     (click)="onNav()">
    <i class="fas fa-exchange-alt"></i> الاستعارات
  </a>

  <a *ngIf="canViewMembers"
     routerLink="/members"
     routerLinkActive="active"
     (click)="onNav()">
    <i class="fas fa-users"></i> الأعضاء
  </a>

  <a *ngIf="canViewReports"
     routerLink="/reports"
     routerLinkActive="active"
     (click)="onNav()">
    <i class="fas fa-chart-line"></i> التقارير
  </a>

  <a *ngIf="canCreateUser"
     routerLink="/users"
     [queryParams]="{ open: 'create' }"
     routerLinkActive="active"
     (click)="onNav()">
    <i class="fas fa-user"></i> المستخدمين
  </a>

  <!-- 👇 الرابط الجديد لحسابي -->
  <a routerLink="/profile"
     routerLinkActive="active"
     (click)="onNav()">
    <i class="fas fa-id-card"></i> حسابي
  </a>
</nav>

    <div class="sidebar-footer">
      <button class="logout" type="button" (click)="logout()"><i class="fas fa-sign-out-alt"></i><span>تسجيل الخروج</span></button>
      <button class="toggle-sidebar" type="button" (click)="toggleSidebar()"><i class="fas fa-chevron-right"></i><span>إغلاق القائمة</span></button>
    </div>
  </aside>

  <!-- خلفية تعتيم في الموبايل -->
  <div class="scrim" *ngIf="!isLarge && !isSidebarClosed" (click)="toggleSidebar()" aria-hidden="true"></div>

  <!-- زر عائم يظهر فقط عند إغلاق الشريط -->
  <button *ngIf="isSidebarClosed" class="sidebar-fab" type="button" (click)="toggleSidebar()" aria-label="فتح القائمة">☰</button>

  <main class="content">
    <router-outlet></router-outlet>
  </main>
</div>

  `,
  styles: [
    `
      :host {
        display: block;
      }

      /* إعدادات عامة */
      .layout {
        direction: ltr; /* نثبت RTL */
        --sidebar-w: 280px; /* غيّرها إذا بدك عرض مختلف */
        --slide-ms: 280ms;
        min-height: 100vh;
        background: #f8fafc;
        position: relative;
      }

      /* الشريط الجانبي مثبت على اليمين (RTL) */
      .sidebar {
        position: fixed;
        inset-block: 0;
        inset-inline-end: 0;
        width: var(--sidebar-w);
        background: rgba(17, 24, 39, 0.95);
        backdrop-filter: blur(8px);
        color: #e5e7eb;

        /* ✅ نقسم الشريط: رأس / قائمة تتمرّر / ذيل مثبت */
        display: grid;
        grid-template-rows: auto 1fr auto;

        padding: 8px 10px;
        box-shadow: -10px 0 24px rgba(2, 6, 23, 0.22);
        border-radius: 12px 0 0 12px;
        transform: translateX(0);
        transition: transform var(--slide-ms) ease;
        z-index: 60;
      }
      .sidebar.closed {
        transform: translateX(100%);
      } /* اختفاء تام */

      /* رأس الشريط */
      .brand {
        font-weight: 900;
        font-size: 22px;
        color: #fbbf24;
        padding: 10px 8px;
        margin: 0 0 8px 0;
        position: sticky;
        top: 0;
        z-index: 1;
        background: linear-gradient(180deg, rgba(17, 24, 39, 0.98), rgba(17, 24, 39, 0.9));
        border-radius: 10px;
      }

      /* قائمة الروابط — تتمرّر داخلياً فقط */
      nav {
        display: flex;
        flex-direction: column;
        gap: 10px;
        overflow-y: auto;
        padding: 6px 4px;
        scrollbar-width: thin;
        scrollbar-color: #4f46e5 #0f172a;
      }
      nav::-webkit-scrollbar {
        width: 8px;
      }
      nav::-webkit-scrollbar-track {
        background: #0f172a;
        border-radius: 10px;
      }
      nav::-webkit-scrollbar-thumb {
        background: #4f46e5;
        border-radius: 10px;
      }

      nav a {
        color: #cbd5e1;
        text-decoration: none;
        padding: 12px 14px;
        border-radius: 12px;
        display: flex;
        align-items: center;
        gap: 10px;
        transition: background 0.2s ease, transform 0.2s ease;
      }
      nav a.active,
      nav a:hover {
        background: rgba(31, 41, 55, 0.8);
        color: #fff;
        transform: translateX(-4px);
      }

      /* أسفل الشريط (مثبّت) */
      .sidebar-footer {
        display: flex;
        flex-direction: column;
        gap: 10px;
        padding: 8px 6px 6px;
        background: linear-gradient(0deg, rgba(17, 24, 39, 0.9), rgba(17, 24, 39, 0));
        border-radius: 10px;
        margin-top: 8px;
      }
      .sidebar-footer button {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 8px;
        height: 44px;
        border-radius: 12px;
        font-weight: 800;
        border: 0;
        cursor: pointer;
        transition: transform 0.15s ease, filter 0.15s ease;
      }
      .sidebar-footer .logout {
        background: linear-gradient(135deg, #2563eb, #3b82f6);
        color: #fff;
        box-shadow: 0 10px 24px rgba(37, 99, 235, 0.25);
      }
      .sidebar-footer .logout:hover {
        filter: brightness(1.05);
        transform: translateY(-1px);
      }
      .sidebar-footer .toggle-sidebar {
        background: #111827;
        color: #fff;
        border: 1px solid #1f2937;
      }
      .sidebar-footer .toggle-sidebar:hover {
        background: #1e293b;
        transform: translateY(-1px);
      }

      /* المحتوى يفسح مجالاً للشريط */
      .content {
        min-height: 100vh;
        padding: 22px;
        background: #ffffffc7;
        backdrop-filter: blur(5px);
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.05) inset;
        transition: margin var(--slide-ms) ease;
        margin-inline-end: var(--sidebar-w); /* نفس عرض الشريط */
        position: relative;
        z-index: 1;
      }
      /* عند إغلاق الشريط: فل وِدث */
      .layout.sidebar-closed .content {
        margin-inline-end: 0;
      }

      /* زر عائم يعيد فتح الشريط */
      .sidebar-fab {
        position: fixed;
        inset-inline-end: 16px;
        inset-block-end: 20px;
        width: 52px;
        height: 52px;
        border-radius: 14px;
        border: 0;
        background: linear-gradient(180deg, #5b56f7, #4f46e5);
        color: #fff;
        font-size: 22px;
        font-weight: 900;
        box-shadow: 0 14px 32px rgba(79, 70, 229, 0.35);
        cursor: pointer;
        z-index: 70;
        transition: transform 0.15s ease, filter 0.15s ease;
      }
      .sidebar-fab:hover {
        transform: translateY(-2px);
        filter: brightness(1.05);
      }

      /* موبايل: دايمًا فل ودث، والقائمة تطفو فوق */
      @media (max-width: 900px) {
        .content {
          margin-inline-end: 0;
        }
        .sidebar {
          width: min(86vw, 320px);
        }
      }
      /* قاعدة: الجهاز الكبير يفسح مساحة للشريط */
.layout {
  --sidebar-w: 280px;
}
.content {
  margin-inline-end: var(--sidebar-w);
}

/* scrim لموبايل */
.scrim{
  position: fixed;
  inset: 0;
  background: rgba(2,6,23,.55);
  backdrop-filter: blur(2px);
  z-index: 50;
}

/* موبايل/تابلت: الشريط يطفو فوق المحتوى */
@media (max-width: 900px){
  .layout.is-small .content{
    margin-inline-end: 0;            /* لا تترك مساحة للشريط */
    padding: 18px;
  }
  .layout.is-small .sidebar{
    width: min(86vw, 320px);
    inset-inline-end: 0;
    inset-block: env(safe-area-inset-top) env(safe-area-inset-bottom);
    border-radius: 12px 0 0 12px;
    box-shadow: -12px 0 32px rgba(2,6,23,.28);
  }
  .layout.is-small .sidebar.closed{
    transform: translateX(100%);
  }
  .sidebar-fab{
    inset-inline-end: 16px;
    inset-block-end: calc(16px + env(safe-area-inset-bottom));
  }
  /* اجعل قائمة الروابط أكثر تماسكًا في الموبايل */
  .sidebar nav a{ padding: 12px; }
  .sidebar .brand{ font-size: 20px; padding: 10px }
}

/* شاشات أصغر جدًا */
@media (max-width: 480px){
  .layout.is-small .sidebar{ width: min(92vw, 300px); }
  .content{ padding: 16px; }
}

/* احترام تقلّيل الحركة */
@media (prefers-reduced-motion: reduce){
  .sidebar, .content, .sidebar-fab { transition: none !important; }
}

    `,
  ],
})
export class ShellComponent implements OnInit {
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);
  isLarge = true;
  canViewBooks = false;
  canViewBorrowings = false;
  canViewMembers = false;
  canViewReports = false;
  canCreateUser = false;
  isSidebarClosed = false;

  logout() {
    if (!isPlatformBrowser(this.platformId)) return;
    localStorage.removeItem('authToken');
    sessionStorage.removeItem('authToken');
    sessionStorage.removeItem('sessionLogin');
    localStorage.removeItem('role');
    localStorage.removeItem('permissions');
    this.router.navigate(['/login']);
  }

  toggleSidebar() {
    this.isSidebarClosed = !this.isSidebarClosed;
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem('sidebarCollapsed', this.isSidebarClosed ? '1' : '0');
    }
  }
    @HostListener('window:resize')
  onResize() {
    const wasLarge = this.isLarge;
    this.isLarge = window.innerWidth >= 900;
    // انتقال بين الوضعين: افتح الشريط على الكبير، أغلقه على الصغير
    if (this.isLarge && !wasLarge) this.isSidebarClosed = false;
    if (!this.isLarge && wasLarge) this.isSidebarClosed = true;
  }

  onNav() {
    // في الموبايل أغلق الشريط بعد التنقل
    if (!this.isLarge) this.isSidebarClosed = true;
  }

 ngOnInit() {
  if (!isPlatformBrowser(this.platformId)) return;
     if (!this.isLarge) this.isSidebarClosed = true;
  const savedState = localStorage.getItem('sidebarCollapsed');
  if (savedState !== null) this.isSidebarClosed = savedState === '1';

  try {
    // استرجاع الصلاحيات من الـ localStorage
    const p = JSON.parse(localStorage.getItem('permissions') || '[]');
    const role = (localStorage.getItem('role') || '').toLowerCase();

    // دالة للتحقق من الصلاحيات
    const has = (need: string) => p.includes(need);  // التحقق من وجود الصلاحية

    // تعيين الصلاحيات بناءً على الدور والصلاحيات المخزنة
    this.canViewBooks = has('book.read');
    this.canViewBorrowings = has('borrow.read');
    this.canViewMembers = has('member.read');
    this.canViewReports = role === 'admin' || role === 'employee' && has('ViewReports') ;
    this.canCreateUser = has('user.crud') && role==='admin';
  } catch (e) {
    console.error('Error parsing permissions or role:', e);
  }
}

}
