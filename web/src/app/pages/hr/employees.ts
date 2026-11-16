import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api, thaiDate, toIsoDate } from '../../core/api';
import { errorMessage } from '../../core/auth.interceptor';
import { Employee, ROLE_LABEL, Role } from '../../core/models';
import { passwordProblem } from '../password-rules';

interface Draft {
  id: number | null;
  name: string;
  email: string;
  department: string;
  role: Role;
  managerId: number | null;
  startDate: string;
  password: string;
  active: boolean;
}

const blank = (): Draft => ({
  id: null, name: '', email: '', department: '', role: 'Employee', managerId: null,
  startDate: toIsoDate(new Date()), password: '', active: true,
});

@Component({
  selector: 'app-employees',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule],
  template: `
    <div class="page">
      <h1>พนักงาน</h1>
      <p class="sub">เพิ่มพนักงาน กำหนดแผนก บทบาท และหัวหน้างานผู้อนุมัติ</p>

      <div class="cols">
        <section class="card">
          <div class="table-wrap">
            <table class="emp">
              <thead><tr><th>ชื่อ</th><th>แผนก</th><th>บทบาท</th><th>หัวหน้างาน</th><th>เริ่มงาน</th><th></th></tr></thead>
              <tbody>
                @for (e of employees(); track e.id) {
                  <tr [class.inactive]="!e.active">
                    <td><div>{{ e.name }}</div><div class="muted small">{{ e.email }}</div></td>
                    <td>{{ e.department }}</td>
                    <td>{{ roleLabel[e.role] }}</td>
                    <td>{{ e.managerName ?? '–' }}</td>
                    <td class="muted">{{ date(e.startDate) }}</td>
                    <td><button mat-button (click)="edit(e)">แก้ไข</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="card">
          <h2>{{ draft().id ? 'แก้ไขพนักงาน' : 'เพิ่มพนักงาน' }}</h2>
          <form (ngSubmit)="save()">
            <mat-form-field appearance="outline"><mat-label>ชื่อ-นามสกุล</mat-label>
              <input matInput name="name" [(ngModel)]="draft().name" required /></mat-form-field>
            @if (!draft().id) {
              <mat-form-field appearance="outline"><mat-label>อีเมล (ใช้เข้าสู่ระบบ)</mat-label>
                <input matInput type="email" name="email" [(ngModel)]="draft().email" required /></mat-form-field>
            }
            <mat-form-field appearance="outline"><mat-label>แผนก</mat-label>
              <input matInput name="dept" [(ngModel)]="draft().department" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>บทบาท</mat-label>
              <mat-select name="role" [(ngModel)]="draft().role">
                @for (r of roles; track r) { <mat-option [value]="r">{{ roleLabel[r] }}</mat-option> }
              </mat-select></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>หัวหน้างาน (ผู้อนุมัติ)</mat-label>
              <mat-select name="mgr" [(ngModel)]="draft().managerId">
                <mat-option [value]="null">– ไม่มี –</mat-option>
                @for (m of managers(); track m.id) { <mat-option [value]="m.id">{{ m.name }}</mat-option> }
              </mat-select></mat-form-field>
            @if (!draft().id) {
              <mat-form-field appearance="outline"><mat-label>วันเริ่มงาน</mat-label>
                <input matInput type="date" name="start" [(ngModel)]="draft().startDate" required /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>รหัสผ่านเริ่มต้น (อย่างน้อย 8 ตัว)</mat-label>
                <input matInput type="password" name="pw" [(ngModel)]="draft().password" minlength="8" required autocomplete="new-password" /></mat-form-field>
            } @else {
              <mat-checkbox name="active" [(ngModel)]="draft().active">ยังทำงานอยู่</mat-checkbox>
            }
            @if (draft().id) {
              <div class="reset">
                <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>รีเซ็ตรหัสผ่าน (รหัสใหม่)</mat-label>
                  <input matInput type="password" name="newpw" [(ngModel)]="newPassword" autocomplete="new-password" /></mat-form-field>
                <button mat-stroked-button type="button" (click)="resetPassword()" [disabled]="busy() || !newPassword">รีเซ็ต</button>
              </div>
            }
            <div class="btns">
              @if (draft().id) { <button mat-button type="button" (click)="draft.set(blank())">ยกเลิก</button> }
              <button mat-flat-button type="submit" [disabled]="busy()">บันทึก</button>
            </div>
          </form>
        </section>
      </div>
    </div>
  `,
  styles: `
    .cols { display: grid; grid-template-columns: 1fr 340px; gap: 20px; align-items: start; }
    table.emp { width: 100%; border-collapse: collapse; font-size: 14px; }
    .emp th, .emp td { padding: 8px 10px; border-bottom: 1px solid var(--lf-border); text-align: left; }
    .emp th { color: var(--lf-text-2); font-weight: 600; background: var(--lf-bg); white-space: nowrap; }
    .emp tr.inactive td { opacity: .5; }
    .small { font-size: 12px; }
    form { display: grid; }
    .reset { display: flex; gap: 8px; align-items: center; margin: 12px 0 4px; }
    .reset mat-form-field { flex: 1; }
    .btns { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
    @media (max-width: 1000px) { .cols { grid-template-columns: 1fr; } }
  `,
})
export class EmployeesPage implements OnInit {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);
  protected readonly employees = signal<Employee[]>([]);
  protected readonly draft = signal<Draft>(blank());
  protected readonly busy = signal(false);
  protected readonly blank = blank;
  protected readonly roles: Role[] = ['Employee', 'Manager', 'HrAdmin'];
  protected readonly roleLabel = ROLE_LABEL;
  protected readonly date = thaiDate;
  protected readonly managers = computed(() =>
    this.employees().filter((e) => e.active && e.role !== 'Employee' && e.id !== this.draft().id));

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected newPassword = '';

  protected edit(e: Employee): void {
    this.newPassword = '';
    this.draft.set({ ...blank(), id: e.id, name: e.name, email: e.email, department: e.department, role: e.role,
      managerId: e.managerId, startDate: e.startDate, active: e.active });
  }

  protected async save(): Promise<void> {
    const d = this.draft();
    this.busy.set(true);
    try {
      if (d.id) {
        await this.api.updateEmployee(d.id, { name: d.name, department: d.department, role: d.role, managerId: d.managerId, active: d.active });
        this.snack.open('บันทึกแล้ว', 'ปิด', { duration: 3000 });
      } else {
        await this.api.createEmployee({ name: d.name, email: d.email, department: d.department, role: d.role,
          managerId: d.managerId, startDate: d.startDate, password: d.password });
        this.snack.open(`เพิ่ม ${d.name} แล้ว`, 'ปิด', { duration: 3000 });
      }
      this.draft.set(blank());
      await this.load();
    } catch (err) {
      this.snack.open(errorMessage(err), 'ปิด', { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  protected async resetPassword(): Promise<void> {
    const d = this.draft();
    const problem = passwordProblem(this.newPassword);
    if (!d.id || problem) {
      if (problem) this.snack.open(problem, 'ปิด', { duration: 4000 });
      return;
    }
    this.busy.set(true);
    try {
      await this.api.resetPassword(d.id, this.newPassword);
      this.newPassword = '';
      this.snack.open(`รีเซ็ตรหัสผ่านของ ${d.name} แล้ว แจ้งรหัสใหม่ให้พนักงานและให้เปลี่ยนเองหลังเข้าสู่ระบบ`, 'ปิด', { duration: 6000 });
    } catch (err) {
      this.snack.open(errorMessage(err), 'ปิด', { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.employees.set(await this.api.employees());
  }
}
