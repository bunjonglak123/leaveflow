import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api, thaiRange, toIsoDate } from '../core/api';
import { errorMessage } from '../core/auth.interceptor';
import { Balance, HALF_LABEL, HalfDay, LeaveRequest, NewLeaveRequest, STATUS_LABEL } from '../core/models';

@Component({
  selector: 'app-me',
  imports: [FormsModule, MatFormFieldModule, MatSelectModule, MatDatepickerModule, MatInputModule, MatRadioModule,
    MatButtonModule, MatProgressBarModule],
  template: `
    <div class="page">
      <h1>การลาของฉัน</h1>
      <p class="sub">วันลาคงเหลือปีนี้ และคำขอลาทั้งหมดของคุณ</p>

      <section class="balances">
        @for (b of balances(); track b.leaveTypeId) {
          <div class="card bal">
            <div class="bal-head"><span>{{ b.nameTh }}</span><span class="muted">{{ b.quota }} วัน/ปี</span></div>
            <div class="bal-num">{{ b.remaining }} <small>วันคงเหลือ</small></div>
            <mat-progress-bar mode="determinate" [value]="b.quota ? ((b.used + b.pending) / b.quota) * 100 : 0" />
            <div class="bal-foot muted">ใช้ไป {{ b.used }} · รออนุมัติ {{ b.pending }}</div>
          </div>
        }
      </section>

      <div class="cols">
        <section class="card">
          <h2>ยื่นคำขอลา</h2>
          <form (ngSubmit)="submit()" #f="ngForm">
            <mat-form-field appearance="outline">
              <mat-label>ประเภทการลา</mat-label>
              <mat-select name="type" [(ngModel)]="typeId" (ngModelChange)="refreshPreview()" required>
                @for (b of balances(); track b.leaveTypeId) {
                  <mat-option [value]="b.leaveTypeId">{{ b.nameTh }} (เหลือ {{ b.remaining }} วัน)</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>ช่วงวันที่ลา</mat-label>
              <mat-date-range-input [rangePicker]="picker">
                <input matStartDate name="start" [(ngModel)]="start" (ngModelChange)="refreshPreview()" placeholder="วันเริ่ม" required />
                <input matEndDate name="end" [(ngModel)]="end" (ngModelChange)="refreshPreview()" placeholder="วันสิ้นสุด" required />
              </mat-date-range-input>
              <mat-datepicker-toggle matIconSuffix [for]="picker" />
              <mat-date-range-picker #picker />
            </mat-form-field>

            @if (singleDay()) {
              <mat-radio-group name="half" [(ngModel)]="half" (ngModelChange)="refreshPreview()" class="half">
                @for (h of halves; track h) { <mat-radio-button [value]="h">{{ halfLabel[h] }}</mat-radio-button> }
              </mat-radio-group>
            }

            <mat-form-field appearance="outline">
              <mat-label>เหตุผล</mat-label>
              <textarea matInput name="reason" [(ngModel)]="reason" rows="2" maxlength="500"></textarea>
            </mat-form-field>

            <div class="preview" [class.bad]="previewError()">
              @if (previewError()) { {{ previewError() }} }
              @else if (previewDays() !== null) { รวม <b>{{ previewDays() }}</b> วันทำงาน (ไม่นับเสาร์-อาทิตย์และวันหยุด) }
              @else { เลือกประเภทและวันที่เพื่อดูจำนวนวัน }
            </div>
            <button mat-flat-button type="submit" [disabled]="busy() || !canSubmit()">ส่งคำขอ</button>
          </form>
        </section>

        <section class="card">
          <h2>คำขอของฉัน</h2>
          @if (requests().length === 0) { <p class="empty">ยังไม่มีคำขอลา</p> }
          <ul class="reqs">
            @for (r of requests(); track r.id) {
              <li>
                <div class="r-main">
                  <div><b>{{ r.leaveTypeName }}</b> · {{ range(r) }} <span class="muted">({{ r.days }} วัน{{ r.half !== 'None' ? ' ' + halfLabel[r.half] : '' }})</span></div>
                  @if (r.reason) { <div class="muted small">{{ r.reason }}</div> }
                  @if (r.decisionNote) { <div class="small note">หมายเหตุ: {{ r.decisionNote }}</div> }
                </div>
                <div class="r-side">
                  <span class="status {{ r.status }}">{{ statusLabel[r.status] }}</span>
                  @if (r.status === 'Pending') {
                    @if (confirmCancel() === r.id) {
                      <button mat-button color="warn" (click)="cancel(r)" [disabled]="busy()">ยืนยันยกเลิก</button>
                    } @else {
                      <button mat-button (click)="confirmCancel.set(r.id)">ยกเลิก</button>
                    }
                  }
                </div>
              </li>
            }
          </ul>
        </section>
      </div>
    </div>
  `,
  styles: `
    .balances { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .bal-head { display: flex; justify-content: space-between; font-weight: 500; }
    .bal-num { font-size: 32px; font-weight: 700; margin: 8px 0 12px; }
    .bal-num small { font-size: 14px; font-weight: 400; color: var(--lf-text-2); }
    .bal-foot { font-size: 13px; margin-top: 8px; }
    .cols { display: grid; grid-template-columns: minmax(300px, 400px) 1fr; gap: 20px; align-items: start; }
    form { display: grid; }
    .half { display: flex; gap: 16px; margin: -4px 0 16px; }
    .preview { background: var(--lf-bg); border-radius: 8px; padding: 10px 12px; font-size: 14px; margin-bottom: 12px; }
    .preview.bad { background: var(--lf-rejected-bg); color: var(--lf-rejected); }
    form button { height: 44px; }
    .reqs { list-style: none; margin: 0; padding: 0; }
    .reqs li { display: flex; justify-content: space-between; gap: 12px; padding: 12px 0; border-top: 1px solid var(--lf-border); }
    .reqs li:first-child { border-top: 0; }
    .r-side { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; }
    .small { font-size: 13px; margin-top: 2px; }
    .note { color: var(--lf-rejected); }
    @media (max-width: 900px) { .cols { grid-template-columns: 1fr; } }
  `,
})
export class MePage implements OnInit {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);

  protected readonly balances = signal<Balance[]>([]);
  protected readonly requests = signal<LeaveRequest[]>([]);
  protected readonly busy = signal(false);
  protected readonly previewDays = signal<number | null>(null);
  protected readonly previewError = signal('');
  protected readonly confirmCancel = signal<number | null>(null);
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly halfLabel = HALF_LABEL;
  protected readonly halves: HalfDay[] = ['None', 'Morning', 'Afternoon'];
  protected readonly range = (r: LeaveRequest) => thaiRange(r.start, r.end);

  protected typeId: number | null = null;
  protected start: Date | null = null;
  protected end: Date | null = null;
  protected half: HalfDay = 'None';
  protected reason = '';
  private readonly dates = signal<[Date | null, Date | null]>([null, null]);
  protected readonly singleDay = computed(() => {
    const [s, e] = this.dates();
    return !!s && !!e && toIsoDate(s) === toIsoDate(e);
  });
  private previewSeq = 0;

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  protected canSubmit(): boolean {
    return !!this.typeId && !!this.start && !!this.end && (this.previewDays() ?? 0) > 0 && !this.previewError();
  }

  protected async refreshPreview(): Promise<void> {
    this.dates.set([this.start, this.end]);
    if (!this.singleDay()) this.half = 'None';
    const req = this.buildRequest();
    if (!req) { this.previewDays.set(null); this.previewError.set(''); return; }
    const seq = ++this.previewSeq;
    try {
      const { days } = await this.api.preview(req);
      if (seq !== this.previewSeq) return;
      this.previewError.set(days > 0 ? '' : 'ช่วงที่เลือกไม่มีวันทำงาน');
      this.previewDays.set(days);
    } catch (err) {
      if (seq !== this.previewSeq) return;
      this.previewDays.set(null);
      this.previewError.set(errorMessage(err));
    }
  }

  protected async submit(): Promise<void> {
    const req = this.buildRequest();
    if (!req) return;
    await this.run(async () => {
      await this.api.createRequest(req);
      this.snack.open('ส่งคำขอลาแล้ว รอหัวหน้าอนุมัติ', 'ปิด', { duration: 4000 });
      this.start = this.end = null;
      this.reason = '';
      this.half = 'None';
      this.previewDays.set(null);
      this.dates.set([null, null]);
    });
  }

  protected async cancel(r: LeaveRequest): Promise<void> {
    await this.run(async () => {
      await this.api.cancel(r.id);
      this.snack.open('ยกเลิกคำขอแล้ว', 'ปิด', { duration: 3000 });
    });
    this.confirmCancel.set(null);
  }

  private buildRequest(): NewLeaveRequest | null {
    if (!this.typeId || !this.start || !this.end) return null;
    return { leaveTypeId: this.typeId, start: toIsoDate(this.start), end: toIsoDate(this.end), half: this.half, reason: this.reason };
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      await this.reload();
    } catch (err) {
      this.snack.open(errorMessage(err), 'ปิด', { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  private async reload(): Promise<void> {
    const [b, r] = await Promise.all([this.api.balances(), this.api.myRequests()]);
    this.balances.set(b);
    this.requests.set(r);
  }
}
