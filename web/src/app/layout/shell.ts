import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../core/auth';
import { ROLE_LABEL, Role } from '../core/models';

interface NavItem { path: string; label: string; icon: string; roles: Role[] }

const NAV: NavItem[] = [
  { path: '/me', label: 'การลาของฉัน', icon: 'event_available', roles: ['Employee', 'Manager', 'HrAdmin'] },
  { path: '/team', label: 'อนุมัติการลา', icon: 'fact_check', roles: ['Manager', 'HrAdmin'] },
  { path: '/calendar', label: 'ปฏิทินทีม', icon: 'calendar_month', roles: ['Manager', 'HrAdmin'] },
  { path: '/hr/reports', label: 'รายงานรายเดือน', icon: 'summarize', roles: ['HrAdmin'] },
  { path: '/hr/employees', label: 'พนักงาน', icon: 'group', roles: ['HrAdmin'] },
  { path: '/hr/settings', label: 'ประเภทการลาและวันหยุด', icon: 'tune', roles: ['HrAdmin'] },
  { path: '/password', label: 'เปลี่ยนรหัสผ่าน', icon: 'key', roles: ['Employee', 'Manager', 'HrAdmin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatSidenavModule, MatListModule, MatIconModule, MatButtonModule],
  template: `
    <mat-toolbar class="bar">
      @if (mobile()) {
        <button mat-icon-button (click)="drawer.toggle()" aria-label="เมนู"><mat-icon fontSet="material-symbols-outlined">menu</mat-icon></button>
      }
      <span class="brand"><span class="dot"></span>LeaveFlow</span>
      <span class="spacer"></span>
      <span class="who">
        <span class="name">{{ auth.user()?.name }}</span>
        <span class="role">{{ roleLabel() }}</span>
      </span>
      <button mat-icon-button (click)="auth.logout()" aria-label="ออกจากระบบ" title="ออกจากระบบ">
        <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
      </button>
    </mat-toolbar>

    <mat-sidenav-container class="container">
      <mat-sidenav #drawer [mode]="mobile() ? 'over' : 'side'" [opened]="!mobile()" class="nav">
        <mat-nav-list>
          @for (item of items(); track item.path) {
            <a mat-list-item [routerLink]="item.path" routerLinkActive="active" (click)="mobile() && drawer.close()">
              <mat-icon matListItemIcon fontSet="material-symbols-outlined">{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content><router-outlet /></mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host { display: flex; flex-direction: column; height: 100vh; }
    .bar { background: #fff; border-bottom: 1px solid var(--lf-border); position: relative; z-index: 2; }
    .brand { font-weight: 700; font-size: 18px; display: inline-flex; align-items: center; gap: 8px; }
    .dot { width: 12px; height: 12px; border-radius: 4px; background: var(--mat-sys-primary); }
    .spacer { flex: 1; }
    .who { display: flex; flex-direction: column; align-items: flex-end; line-height: 1.2; margin-right: 8px; }
    .who .name { font-size: 14px; font-weight: 500; }
    .who .role { font-size: 12px; color: var(--lf-text-2); }
    .container { flex: 1; background: var(--lf-bg); }
    .nav { width: 248px; border-right: 1px solid var(--lf-border); background: #fff; }
    a.active { background: color-mix(in srgb, var(--mat-sys-primary) 10%, transparent); }
    @media (max-width: 600px) { .who .name { display: none; } }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly mobile = toSignal(
    inject(BreakpointObserver).observe('(max-width: 900px)').pipe(map((s) => s.matches)),
    { initialValue: false },
  );
  protected readonly items = computed(() => {
    const role = this.auth.user()?.role;
    return NAV.filter((n) => role && n.roles.includes(role));
  });
  protected readonly roleLabel = computed(() => {
    const role = this.auth.user()?.role;
    return role ? ROLE_LABEL[role] : '';
  });
}
