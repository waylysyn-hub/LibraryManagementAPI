import { ApplicationConfig } from '@angular/core';
import { provideRouter, withEnabledBlockingInitialNavigation } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    // يضمن انتظار نتيجة الحراس قبل عرض أي صفحة (لا وميض /books)
    provideRouter(routes, withEnabledBlockingInitialNavigation()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor]))
  ]
};
