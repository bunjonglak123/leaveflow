import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LoginResponse, Role, User } from './models';

const STORAGE_KEY = 'leaveflow.session';

function readSession(): LoginResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as LoginResponse) : null;
  } catch {
    return null;
  }
}

function writeSession(s: LoginResponse | null): void {
  try {
    if (s) localStorage.setItem(STORAGE_KEY, JSON.stringify(s));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* storage blocked: session lives in memory only */
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<LoginResponse | null>(readSession());

  readonly user = computed<User | null>(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);

  hasRole(...roles: Role[]): boolean {
    const u = this.user();
    return !!u && roles.includes(u.role);
  }

  async login(email: string, password: string): Promise<void> {
    this.set(await firstValueFrom(this.http.post<LoginResponse>('/api/auth/login', { email, password })));
  }

  async demo(role: Role): Promise<void> {
    this.set(await firstValueFrom(this.http.post<LoginResponse>('/api/auth/demo', { role })));
  }

  logout(): void {
    this.set(null);
    void this.router.navigateByUrl('/login');
  }

  /** Landing page for the signed-in role. */
  homeUrl(): string {
    const role = this.user()?.role;
    if (role === 'HrAdmin') return '/hr/reports';
    if (role === 'Manager') return '/team';
    return '/me';
  }

  private set(s: LoginResponse | null): void {
    writeSession(s);
    this.session.set(s);
  }
}
