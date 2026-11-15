import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AuditEntry, Balance, CalendarEntry, Employee, Holiday, LeaveRequest, LeaveStatus, LeaveType, MonthlyReport,
  NewLeaveRequest, Role,
} from './models';

/** Typed wrapper around the LeaveFlow REST API. All methods return promises. */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private get = <T>(url: string, params?: Record<string, string>) =>
    firstValueFrom(this.http.get<T>(url, { params: new HttpParams({ fromObject: params ?? {} }) }));
  private post = <T>(url: string, body: unknown = {}) => firstValueFrom(this.http.post<T>(url, body));
  private put = <T>(url: string, body: unknown) => firstValueFrom(this.http.put<T>(url, body));

  // employee
  balances = () => this.get<Balance[]>('/api/balances');
  myRequests = () => this.get<LeaveRequest[]>('/api/requests');
  preview = (r: NewLeaveRequest) => this.post<{ days: number }>('/api/requests/preview', r);
  createRequest = (r: NewLeaveRequest) => this.post<LeaveRequest>('/api/requests', r);
  cancel = (id: number) => this.post<LeaveRequest>(`/api/requests/${id}/cancel`);
  history = (id: number) => this.get<AuditEntry[]>(`/api/requests/${id}/history`);
  holidays = (year: number) => this.get<Holiday[]>('/api/holidays', { year: String(year) });

  // manager
  teamRequests = (status?: LeaveStatus) =>
    this.get<LeaveRequest[]>('/api/team/requests', status ? { status } : undefined);
  approve = (id: number, note: string) => this.post<LeaveRequest>(`/api/team/requests/${id}/approve`, { note });
  reject = (id: number, note: string) => this.post<LeaveRequest>(`/api/team/requests/${id}/reject`, { note });
  calendar = (month: string) => this.get<CalendarEntry[]>('/api/team/calendar', { month });

  // hr
  employees = () => this.get<Employee[]>('/api/hr/employees');
  createEmployee = (e: {
    name: string; email: string; department: string; role: Role; managerId: number | null; startDate: string; password: string;
  }) => this.post<Employee>('/api/hr/employees', e);
  updateEmployee = (id: number, e: { name: string; department: string; role: Role; managerId: number | null; active: boolean }) =>
    this.put<Employee>(`/api/hr/employees/${id}`, e);
  leaveTypes = () => this.get<LeaveType[]>('/api/hr/leave-types');
  createLeaveType = (t: { nameTh: string; yearlyQuota: number }) => this.post<LeaveType>('/api/hr/leave-types', t);
  updateLeaveType = (id: number, t: { nameTh: string; yearlyQuota: number }) => this.put<LeaveType>(`/api/hr/leave-types/${id}`, t);
  addHoliday = (h: Holiday) => this.post<Holiday>('/api/hr/holidays', h);
  deleteHoliday = (date: string) => firstValueFrom(this.http.delete<void>(`/api/hr/holidays/${date}`));
  monthlyReport = (month: string) => this.get<MonthlyReport>('/api/hr/reports/monthly', { month });
  monthlyReportCsv = (month: string) =>
    firstValueFrom(this.http.get('/api/hr/reports/monthly', { params: { month, format: 'csv' }, responseType: 'blob' }));
}

/** Local date → yyyy-MM-dd (no timezone shift). */
export function toIsoDate(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** yyyy-MM-dd → Thai short date, e.g. "16 พ.ย. 2569". */
export function thaiDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('th-TH', { day: 'numeric', month: 'short', year: 'numeric' });
}

/** Range label: one date, or "start – end". */
export function thaiRange(start: string, end: string): string {
  return start === end ? thaiDate(start) : `${thaiDate(start)} – ${thaiDate(end)}`;
}
