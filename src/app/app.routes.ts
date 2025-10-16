import { Routes } from '@angular/router';
import { Login } from './login/login';
import { authzCanMatch } from './auth.guard';
import { ShellComponent } from './shell/shell';
import { BooksComponent } from './books/books';
import { BorrowingsComponent } from './borrowings/borrowings';
import { MembersComponent } from './members/members';

export const routes: Routes = [
  // ابدأ دائمًا من صفحة الدخول
  { path: 'login', component: Login },

  // أي شيء داخل الشل يحتاج مصادقة
  {
    path: '',
    component: ShellComponent,
    canMatch: [authzCanMatch],
    children: [
      // أمثلة على تقييد الأدوار:
      // الكتب: الكل (أدمن + موظف + عضو)
      { path: 'books', component: BooksComponent, data: { roles: ['Admin', 'Employee', 'Member'] } },

      // الاستعارات: أدمن + موظف فقط
      { path: 'borrowings', component: BorrowingsComponent, data: { roles: ['Admin', 'Employee'] } },

      // الأعضاء: أدمن فقط
      { path: 'members', component: MembersComponent, data: { roles: ['Admin'] } },

      { path: '', pathMatch: 'full', redirectTo: 'books' }
    ]
  },

  // مسارات غير معروفة → إلى /login (سيُعاد توجيه المصادقين تلقائيًا بعد الدخول)
  { path: '**', redirectTo: 'login' }
];
