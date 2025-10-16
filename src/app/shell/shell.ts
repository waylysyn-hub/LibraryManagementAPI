import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet, RouterModule, Router } from '@angular/router';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
 
@Component({
  standalone: true,
  selector: 'app-shell',
  imports: [CommonModule, RouterOutlet, RouterModule],
  template: `
  <div class="layout" dir="rtl">
    <aside class="sidebar">
      <div class="brand">📚 نظام المكتبة</div>
      <nav>
        <a *ngIf="canViewBooks" routerLink="/books" routerLinkActive="active">الكتب</a>
        <a *ngIf="canViewBorrowings" routerLink="/borrowings" routerLinkActive="active">الاستعارات</a>
        <a *ngIf="canViewMembers" routerLink="/members" routerLinkActive="active">الأعضاء</a>
      </nav>
      <button class="logout" (click)="logout()">تسجيل الخروج</button>
    </aside>
    <main class="content">
      <router-outlet></router-outlet>
    </main>
  </div>
  `,
  styles: [`
    .layout {
      display: grid;
      grid-template-columns: 260px 1fr;
      min-height: 100dvh;
      background: #f8fafc;
      box-shadow: inset 0 0 20px rgba(0, 0, 0, 0.1);
    }

    .sidebar {
      background: rgba(17, 24, 39, 0.8);  /* تدرج زجاجي */
      backdrop-filter: blur(8px);  /* تأثير ضبابي */
      color: #e5e7eb;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      border-radius: 16px;  /* حواف دائرية */
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
    }

    .brand {
      font-weight: 900;
      font-size: 22px;
      color: #fbbf24; /* لون مميز */
      margin-bottom: 12px;
    }

    nav {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    nav a {
      color: #cbd5e1;
      text-decoration: none;
      padding: 12px 14px;
      border-radius: 12px;
      transition: background 0.3s ease, transform 0.3s ease;  /* تأثير تحرك */
    }

    nav a.active,
    nav a:hover {
      background: rgba(31, 41, 55, 0.8);
      color: #fff;
      transform: scale(1.05);  /* تكبير العناصر عند التمرير */
    }

    .logout {
      margin-top: auto;
      border: none;
      background: linear-gradient(135deg, #2563eb, #3b82f6); /* تدرج لوني جميل */
      color: #fff;
      border-radius: 12px;
      height: 42px;
      cursor: pointer;
      transition: background 0.3s ease, transform 0.3s ease;
    }

    .logout:hover {
      background: linear-gradient(135deg, #3b82f6, #2563eb);
      transform: scale(1.1);
    }

    .content {
      padding: 22px;
      background: #ffffffc7; /* تأثير زجاجي على المحتوى */
      backdrop-filter: blur(5px);  /* تأثير ضبابي */
      border-radius: 16px;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.05);
    }
  `]
})
export class ShellComponent implements OnInit {
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);

  canViewBooks = false;
  canViewBorrowings = false;
  canViewMembers = false;

  logout() {
    if (!isPlatformBrowser(this.platformId)) return;

    localStorage.removeItem('authToken');
    sessionStorage.removeItem('authToken');
    sessionStorage.removeItem('sessionLogin');
    localStorage.removeItem('role');
    localStorage.removeItem('permissions');

    this.router.navigate(['/login']);
  }

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      try {
        const p = JSON.parse(localStorage.getItem('permissions') || '[]');
        const role = (localStorage.getItem('role') || '').toLowerCase();

        const has = (need: string) => {
          if (role === 'admin') {
            return true;
          }
          return p.includes(need);
        };

        this.canViewBooks = has('book.read');
        this.canViewBorrowings = has('borrow.read');
        this.canViewMembers = has('member.read');

        console.log('[permissions]', { role, p, canViewBooks: this.canViewBooks, canViewBorrowings: this.canViewBorrowings, canViewMembers: this.canViewMembers });
      } catch (error) {
        console.error('Error parsing permissions or role:', error);
      }
    }
  }
}
