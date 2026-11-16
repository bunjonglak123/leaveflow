import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth';
import { errorMessage } from '../core/auth.interceptor';
import { Role } from '../core/models';

@Component({
  selector: 'app-login',
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  template: `
    <main class="wrap">
      <section class="panel">
        <div class="brand"><span class="dot"></span>LeaveFlow</div>
        <h1>ระบบลางานสำหรับทีมของคุณ</h1>
        <p class="sub">ยื่นลา อนุมัติ และดูวันลาคงเหลือได้ในที่เดียว</p>

        @if (demoEnabled()) {
        <div class="demo">
          <p class="label">ทดลองใช้งาน (ข้อมูลตัวอย่าง รีเซ็ตทุกวัน)</p>
          <div class="roles">
            @for (d of demos; track d.role) {
              <button mat-stroked-button type="button" (click)="demo(d.role)" [disabled]="busy()">
                <mat-icon fontSet="material-symbols-outlined">{{ d.icon }}</mat-icon>{{ d.label }}
              </button>
            }
          </div>
        </div>

        <div class="or"><span>หรือเข้าสู่ระบบด้วยอีเมล</span></div>
        }

        <form (ngSubmit)="login()">
          <mat-form-field appearance="outline">
            <mat-label>อีเมล</mat-label>
            <input matInput type="email" name="email" [(ngModel)]="email" autocomplete="username" required />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>รหัสผ่าน</mat-label>
            <input matInput type="password" name="password" [(ngModel)]="password" autocomplete="current-password" required />
          </mat-form-field>
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
          <button mat-flat-button type="submit" [disabled]="busy() || !email || !password">เข้าสู่ระบบ</button>
        </form>
      </section>
    </main>
  `,
  styles: `
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 24px 16px;
      background: radial-gradient(80% 60% at 20% 0%, #dde9ff, transparent 60%), var(--lf-bg); }
    .panel { width: 100%; max-width: 440px; background: #fff; border: 1px solid var(--lf-border); border-radius: 16px; padding: 32px 28px; }
    .brand { font-weight: 700; font-size: 18px; display: flex; align-items: center; gap: 8px; margin-bottom: 20px; }
    .dot { width: 12px; height: 12px; border-radius: 4px; background: var(--mat-sys-primary); }
    h1 { font-size: 22px; margin: 0 0 6px; }
    .sub { color: var(--lf-text-2); margin: 0 0 24px; }
    .label { font-size: 13px; color: var(--lf-text-2); margin: 0 0 8px; }
    .roles { display: grid; gap: 8px; }
    .roles button { justify-content: flex-start; height: 44px; }
    .roles mat-icon { margin-right: 8px; }
    .or { display: flex; align-items: center; gap: 12px; color: var(--lf-text-2); font-size: 13px; margin: 24px 0 16px; }
    .or::before, .or::after { content: ''; flex: 1; height: 1px; background: var(--lf-border); }
    form { display: grid; gap: 4px; }
    form button { height: 44px; }
    .error { color: var(--lf-rejected); margin: 0 0 8px; font-size: 14px; }
  `,
})
export class LoginPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected email = '';
  protected password = '';
  private readonly http = inject(HttpClient);
  /** Hidden until the server confirms demo mode, so real installs never flash the buttons. */
  protected readonly demoEnabled = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly demos: { role: Role; label: string; icon: string }[] = [
    { role: 'Employee', label: 'พนักงาน (ปิติ)', icon: 'person' },
    { role: 'Manager', label: 'หัวหน้างาน (ธนา)', icon: 'supervisor_account' },
    { role: 'HrAdmin', label: 'ฝ่ายบุคคล (มาลี)', icon: 'badge' },
  ];

  protected login(): Promise<void> {
    return this.run(() => this.auth.login(this.email, this.password));
  }

  async ngOnInit(): Promise<void> {
    try {
      const cfg = await firstValueFrom(this.http.get<{ demo: boolean }>('/api/config'));
      this.demoEnabled.set(cfg.demo);
    } catch {
      this.demoEnabled.set(false);
    }
  }

  protected demo(role: Role): Promise<void> {
    return this.run(() => this.auth.demo(role));
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
      await this.router.navigateByUrl(this.auth.homeUrl());
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
