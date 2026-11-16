import { Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api } from '../core/api';
import { errorMessage } from '../core/auth.interceptor';
import { passwordProblem } from './password-rules';

@Component({
  selector: 'app-password',
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <div class="page">
      <h1>เปลี่ยนรหัสผ่าน</h1>
      <p class="sub">รหัสผ่านใหม่ต้องมีอย่างน้อย 8 ตัวอักษร</p>
      <section class="card narrow">
        <form (ngSubmit)="submit(f)" #f="ngForm">
          <mat-form-field appearance="outline"><mat-label>รหัสผ่านปัจจุบัน</mat-label>
            <input matInput type="password" name="current" [(ngModel)]="current" required autocomplete="current-password" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>รหัสผ่านใหม่</mat-label>
            <input matInput type="password" name="next" [(ngModel)]="next" required autocomplete="new-password" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>ยืนยันรหัสผ่านใหม่</mat-label>
            <input matInput type="password" name="confirm" [(ngModel)]="confirm" required autocomplete="new-password" /></mat-form-field>
          @if (problem()) { <div class="err">{{ problem() }}</div> }
          <button mat-flat-button type="submit" [disabled]="busy()">บันทึกรหัสผ่านใหม่</button>
        </form>
      </section>
    </div>
  `,
  styles: `
    .narrow { max-width: 420px; }
    form { display: grid; }
    .err { background: var(--lf-rejected-bg); color: var(--lf-rejected); border-radius: 8px; padding: 10px 12px; font-size: 14px; margin-bottom: 12px; }
    form button { height: 44px; }
  `,
})
export class PasswordPage {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);
  protected readonly busy = signal(false);
  protected readonly problem = signal('');
  protected current = '';
  protected next = '';
  protected confirm = '';

  protected async submit(form: NgForm): Promise<void> {
    const problem = this.current ? passwordProblem(this.next, this.confirm) : 'กรอกรหัสผ่านปัจจุบัน';
    this.problem.set(problem);
    if (problem) return;
    this.busy.set(true);
    try {
      await this.api.changePassword(this.current, this.next);
      this.snack.open('เปลี่ยนรหัสผ่านแล้ว', 'ปิด', { duration: 4000 });
      form.resetForm();
    } catch (err) {
      this.problem.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
