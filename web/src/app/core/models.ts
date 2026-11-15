export type Role = 'Employee' | 'Manager' | 'HrAdmin';
export type HalfDay = 'None' | 'Morning' | 'Afternoon';
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export interface User {
  id: number;
  name: string;
  email: string;
  role: Role;
  department: string;
}

export interface LoginResponse {
  token: string;
  user: User;
}

export interface Balance {
  leaveTypeId: number;
  nameTh: string;
  quota: number;
  used: number;
  pending: number;
  remaining: number;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  leaveTypeId: number;
  leaveTypeName: string;
  start: string; // yyyy-MM-dd
  end: string;
  half: HalfDay;
  days: number;
  reason: string;
  status: LeaveStatus;
  decisionNote: string | null;
  createdAt: string;
  decidedAt: string | null;
}

export interface NewLeaveRequest {
  leaveTypeId: number;
  start: string;
  end: string;
  half: HalfDay;
  reason: string;
}

export interface Holiday {
  date: string;
  nameTh: string;
}

export interface CalendarEntry {
  requestId: number;
  employeeName: string;
  leaveTypeName: string;
  start: string;
  end: string;
  half: HalfDay;
  status: LeaveStatus;
}

export interface AuditEntry {
  action: 'Created' | 'Approved' | 'Rejected' | 'Cancelled';
  actorName: string;
  at: string;
  note: string | null;
}

export interface Employee {
  id: number;
  name: string;
  email: string;
  department: string;
  role: Role;
  managerId: number | null;
  managerName: string | null;
  startDate: string;
  active: boolean;
}

export interface LeaveType {
  id: number;
  nameTh: string;
  yearlyQuota: number;
}

export interface MonthlyReport {
  month: string;
  leaveTypes: string[];
  rows: { employeeId: number; name: string; department: string; days: Record<string, number>; total: number }[];
}

export const ROLE_LABEL: Record<Role, string> = {
  Employee: 'พนักงาน',
  Manager: 'หัวหน้างาน',
  HrAdmin: 'ฝ่ายบุคคล',
};

export const STATUS_LABEL: Record<LeaveStatus, string> = {
  Pending: 'รออนุมัติ',
  Approved: 'อนุมัติแล้ว',
  Rejected: 'ไม่อนุมัติ',
  Cancelled: 'ยกเลิก',
};

export const HALF_LABEL: Record<HalfDay, string> = {
  None: 'เต็มวัน',
  Morning: 'ครึ่งเช้า',
  Afternoon: 'ครึ่งบ่าย',
};
