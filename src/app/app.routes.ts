import { Routes } from '@angular/router';
import { Login } from './login/login';
import { authzCanMatch } from './auth.guard';
import { ShellComponent } from './shell/shell';
import { BooksComponent } from './books/books';
import { BorrowingsComponent } from './borrowings/borrowings';
import { MembersComponent } from './members/members';
import { ReportsComponent } from './reports/reports';
import { UsersComponent } from './users/users';
import { ForgotPasswordComponent } from './forgot-password/forgot-password';
import { ResetCodeComponent } from './reset-code/reset-code';
import { ResetChangeComponent } from './reset-change/reset-change';
import { ProfileComponent } from './profile/profile';

export const routes: Routes = [
  { path: 'login', component: Login },
    { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset/code', component: ResetCodeComponent },
  { path: 'reset/change', component: ResetChangeComponent },
   { path: 'register', component: MembersComponent },

  {
    path: '',
    component: ShellComponent,
    canMatch: [authzCanMatch],
    children: [
      {
        path: 'books',
        component: BooksComponent,
        data: { roles: ['Admin', 'Employee', 'Member'] },
      },
      {
        path: 'borrowings',
        component: BorrowingsComponent,
        data: { roles: ['Admin', 'Employee'] },
      },
      { path: 'members', component: MembersComponent, data: { roles: ['Admin'] } },
      { path: 'reports', component: ReportsComponent, data: { roles: ['Admin', 'Employee'] } },
      { path: 'users', component: UsersComponent, data: { roles: ['Admin'] } },
      { path: 'profile', component: ProfileComponent },
      { path: '', pathMatch: 'full', redirectTo: 'books' },
    ],
  },

  { path: '**', redirectTo: 'login' },
];
