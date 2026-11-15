import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api, thaiDate, toIsoDate } from '../../core/api';
import { errorMessage } from '../../core/auth.interceptor';
import { Holiday, LeaveType } from '../../core/models';

@Component({
  selector: 'app-settings',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatIconModule],
  template: `
    <div class="page">
      <h1>ประเภทการลาและวันหยุด</h1>
      <p class="sub">กำหนดสิทธิ์วันลาต่อปี และวันหยุดบริษัทที่ไม่นับเป็นวันลา</p>

      <div class="cols">
        <section class="card">
          <h2>ประเภทการลา</h2>
          @for (t of types(); track t.id) {
            <form class="row" (ngSubmit)="saveType(t)">
              <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>ชื่อ</mat-label>
                <input matInput [name]="'n' + t.id" [(ngModel)]="t.nameTh" /></mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="num"><mat-label>วัน/ปี</mat-label>
                <input matInput type="number" min="0" max="365" step="0.5" [name]="'q' + t.id" [(ngModel)]="t.yearlyQuota" /></mat-form-field>
              <button mat-stroked-button type="submit">บันทึก</button>
            </form>
          }
          <form class="row add" (ngSubmit)="addType()">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>ประเภทใหม่</mat-label>
              <input matInput name="newName" [(ngModel)]="newType.nameTh" placeholder="เช่น ลาบวช" /></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="num"><mat-label>วัน/ปี</mat-label>
              <input matInput type="number" min="0" max="365" name="newQuota" [(ngModel)]="newType.yearlyQuota" /></mat-form-field>
            <button mat-flat-button type="submit" [disabled]="!newType.nameTh">เพิ่ม</button>
          </form>
        </section>

        <section class="card">
          <div class="hhead">
            <h2>วันหยุดปี {{ year() + 543 }}</h2>
            <span class="spacer"></span>
            <button mat-icon-button (click)="setYear(year() - 1)" aria-label="ปีก่อน"><mat-icon fontSet="material-symbols-outlined">chevron_left</mat-icon></button>
            <button mat-icon-button (click)="setYear(year() + 1)" aria-label="ปีถัดไป"><mat-icon fontSet="material-symbols-outlined">chevron_right</mat-icon></button>
          </div>
          <p class="muted hint">วันหยุดทางจันทรคติ (มาฆบูชา วิสาขบูชา อาสาฬหบูชา) และวันหยุดชดเชยเปลี่ยนทุกปี เพิ่มเองตามประกาศ ครม.</p>
          <form class="row" (ngSubmit)="addHoliday()">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>วันที่</mat-label>
              <input matInput type="date" name="hd" [(ngModel)]="newHoliday.date" required /></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>ชื่อวันหยุด</mat-label>
              <input matInput name="hn" [(ngModel)]="newHoliday.nameTh" required /></mat-form-field>
            <button mat-flat-button type="submit" [disabled]="!newHoliday.date || !newHoliday.nameTh">เพิ่ม</button>
          </form>
          <ul class="hol">
            @for (h of holidays(); track h.date) {
              <li>
                <span class="d">{{ date(h.date) }}</span><span>{{ h.nameTh }}</span>
                <button mat-icon-button (click)="removeHoliday(h)" [attr.aria-label]="'ลบ ' + h.nameTh"><mat-icon fontSet="material-symbols-outlined">delete</mat-icon></button>
              </li>
            }
          </ul>
        </section>
      </div>
    </div>
  `,
  styles: `
    .cols { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; align-items: start; }
    .row { display: flex; gap: 8px; align-items: center; margin-bottom: 12px; }
    .row mat-form-field { flex: 1; }
    .row .num { flex: 0 0 110px; }
    .row.add { padding-top: 12px; border-top: 1px dashed var(--lf-border); }
    .hhead { display: flex; align-items: center; }
    .hhead h2 { margin: 0; }
    .spacer { flex: 1; }
    .hint { font-size: 13px; margin: 4px 0 12px; }
    .hol { list-style: none; margin: 0; padding: 0; }
    .hol li { display: grid; grid-template-columns: 130px 1fr auto; align-items: center; border-top: 1px solid var(--lf-border); padding: 2px 0; }
    .hol .d { color: var(--lf-text-2); font-size: 14px; }
    @media (max-width: 1000px) { .cols { grid-template-columns: 1fr; } }
  `,
})
export class SettingsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);
  protected readonly types = signal<LeaveType[]>([]);
  protected readonly holidays = signal<Holiday[]>([]);
  protected readonly year = signal(new Date().getFullYear());
  protected readonly date = thaiDate;
  protected newType = { nameTh: '', yearlyQuota: 0 };
  protected newHoliday: Holiday = { date: toIsoDate(new Date()), nameTh: '' };

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadTypes(), this.loadHolidays()]);
  }

  protected saveType(t: LeaveType): Promise<void> {
    return this.run(() => this.api.updateLeaveType(t.id, { nameTh: t.nameTh, yearlyQuota: Number(t.yearlyQuota) }), 'บันทึกแล้ว', () => this.loadTypes());
  }

  protected addType(): Promise<void> {
    return this.run(async () => {
      await this.api.createLeaveType({ nameTh: this.newType.nameTh, yearlyQuota: Number(this.newType.yearlyQuota) });
      this.newType = { nameTh: '', yearlyQuota: 0 };
    }, 'เพิ่มประเภทการลาแล้ว', () => this.loadTypes());
  }

  protected addHoliday(): Promise<void> {
    return this.run(async () => {
      await this.api.addHoliday(this.newHoliday);
      this.year.set(Number(this.newHoliday.date.slice(0, 4)));
      this.newHoliday = { ...this.newHoliday, nameTh: '' };
    }, 'เพิ่มวันหยุดแล้ว', () => this.loadHolidays());
  }

  protected removeHoliday(h: Holiday): Promise<void> {
    return this.run(() => this.api.deleteHoliday(h.date), `ลบ ${h.nameTh} แล้ว`, () => this.loadHolidays());
  }

  protected setYear(y: number): Promise<void> {
    this.year.set(y);
    return this.loadHolidays();
  }

  private async run(action: () => Promise<unknown>, done: string, reload: () => Promise<void>): Promise<void> {
    try {
      await action();
      this.snack.open(done, 'ปิด', { duration: 3000 });
    } catch (err) {
      this.snack.open(errorMessage(err), 'ปิด', { duration: 5000 });
    }
    await reload();
  }

  private async loadTypes(): Promise<void> {
    this.types.set(await this.api.leaveTypes());
  }

  private async loadHolidays(): Promise<void> {
    this.holidays.set(await this.api.holidays(this.year()));
  }
}
