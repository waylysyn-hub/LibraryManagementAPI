import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface ProfileDto {
  name: string | null;
  email: string;
  phone: string | null;
}

@Injectable({ providedIn: 'root' })
export class AccountService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/Account`;

  getMe() {
    return this.http.get<ProfileDto>(`${this.base}/me`);
  }

  updateMe(dto: ProfileDto) {
    return this.http.put<void>(`${this.base}/me`, dto, { observe: 'response' });
  }
}
