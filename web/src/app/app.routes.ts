import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then((m) => m.LoginPage), title: 'เข้าสู่ระบบ · LeaveFlow' },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: 'me', loadComponent: () => import('./pages/me').then((m) => m.MePage), title: 'การลาของฉัน · LeaveFlow' },
      { path: 'password', loadComponent: () => import('./pages/password').then((m) => m.PasswordPage), title: 'เปลี่ยนรหัสผ่าน · LeaveFlow' },
      {
        path: 'team',
        canActivate: [roleGuard('Manager', 'HrAdmin')],
        loadComponent: () => import('./pages/team').then((m) => m.TeamPage),
        title: 'อนุมัติการลา · LeaveFlow',
      },
      {
        path: 'calendar',
        canActivate: [roleGuard('Manager', 'HrAdmin')],
        loadComponent: () => import('./pages/calendar').then((m) => m.CalendarPage),
        title: 'ปฏิทินทีม · LeaveFlow',
      },
      {
        path: 'hr',
        canActivate: [roleGuard('HrAdmin')],
        children: [
          { path: 'reports', loadComponent: () => import('./pages/hr/reports').then((m) => m.ReportsPage), title: 'รายงาน · LeaveFlow' },
          { path: 'employees', loadComponent: () => import('./pages/hr/employees').then((m) => m.EmployeesPage), title: 'พนักงาน · LeaveFlow' },
          { path: 'settings', loadComponent: () => import('./pages/hr/settings').then((m) => m.SettingsPage), title: 'ตั้งค่าการลา · LeaveFlow' },
        ],
      },
      { path: '', pathMatch: 'full', loadComponent: () => import('./pages/home-redirect').then((m) => m.HomeRedirect) },
    ],
  },
  { path: '**', redirectTo: '' },
];
