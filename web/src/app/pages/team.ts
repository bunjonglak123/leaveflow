import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api, thaiRange } from '../core/api';
import { errorMessage } from '../core/auth.interceptor';
import { HALF_LABEL, LeaveRequest, LeaveStatus, STATUS_LABEL } from '../core/models';

@Component({
  selector: 'app-team',
  imports: [FormsModule, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule],
  template: `
    <div class="page">
      <h1>อนุมัติการลา</h1>
      <p class="sub">คำขอลาของลูกทีมที่รอการพิจารณา</p>

      <mat-button-toggle-group [value]="filter()" (change)="setFilter($event.value)" class="filter">
        <mat-button-toggle value="Pending">รออนุมัติ</mat-button-toggle>
        <mat-button-toggle value="All">ทั้งหมด</mat-button-toggle>
      </mat-button-toggle-group>

      <section class="card">
        @if (requests().length === 0) {
          <p class="empty">{{ filter() === 'Pending' ? 'ไม่มีคำขอที่รออนุมัติ' : 'ยังไม่มีคำขอลา' }}</p>
        }
        <ul class="list">
          @for (r of requests(); track r.id) {
            <li>
              <div class="info">
                <div class="who"><b>{{ r.employeeName }}</b> <span class="status {{ r.status }}">{{ statusLabel[r.status] }}</span></div>
                <div>{{ r.leaveTypeName }} · {{ range(r) }} · <b>{{ r.days }} วัน</b>{{ r.half !== 'None' ? ' (' + halfLabel[r.half] + ')' : '' }}</div>
                @if (r.reason) { <div class="muted">เหตุผล: {{ r.reason }}</div> }
                @if (r.decisionNote) { <div class="muted">หมายเหตุ: {{ r.decisionNote }}</div> }
              </div>
              @if (r.status === 'Pending') {
                <div class="actions">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic" class="note">
                    <mat-label>หมายเหตุ (จำเป็นถ้าไม่อนุมัติ)</mat-label>
                    <input matInput [(ngModel)]="notes[r.id]" [name]="'note' + r.id" maxlength="500" />
                  </mat-form-field>
                  <div class="btns">
                    <button mat-stroked-button (click)="decide(r, 'reject')" [disabled]="busy() === r.id">ไม่อนุมัติ</button>
                    <button mat-flat-button (click)="decide(r, 'approve')" [disabled]="busy() === r.id">อนุมัติ</button>
                  </div>
                </div>
              }
            </li>
          }
        </ul>
      </section>
    </div>
  `,
  styles: `
    .filter { margin-bottom: 16px; }
    .list { list-style: none; margin: 0; padding: 0; }
    .list li { display: grid; grid-template-columns: 1fr minmax(260px, 340px); gap: 16px; padding: 16px 0; border-top: 1px solid var(--lf-border); }
    .list li:first-child { border-top: 0; padding-top: 0; }
    .who { display: flex; gap: 8px; align-items: center; margin-bottom: 4px; }
    .info > div { margin-top: 2px; }
    .actions { display: grid; gap: 8px; }
    .note { width: 100%; }
    .btns { display: flex; gap: 8px; justify-content: flex-end; }
    @media (max-width: 700px) { .list li { grid-template-columns: 1fr; } }
  `,
})
export class TeamPage implements OnInit {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);
  protected readonly requests = signal<LeaveRequest[]>([]);
  protected readonly filter = signal<'Pending' | 'All'>('Pending');
  protected readonly busy = signal<number | null>(null);
  protected readonly notes: Record<number, string> = {};
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly halfLabel = HALF_LABEL;
  protected readonly range = (r: LeaveRequest) => thaiRange(r.start, r.end);

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected setFilter(v: 'Pending' | 'All'): Promise<void> {
    this.filter.set(v);
    return this.load();
  }

  protected async decide(r: LeaveRequest, action: 'approve' | 'reject'): Promise<void> {
    const note = (this.notes[r.id] ?? '').trim();
    if (action === 'reject' && !note) {
      this.snack.open('กรุณาระบุเหตุผลที่ไม่อนุมัติในช่องหมายเหตุ', 'ปิด', { duration: 4000 });
      return;
    }
    this.busy.set(r.id);
    try {
      await (action === 'approve' ? this.api.approve(r.id, note) : this.api.reject(r.id, note));
      this.snack.open(action === 'approve' ? `อนุมัติคำขอของ ${r.employeeName} แล้ว` : `ไม่อนุมัติคำขอของ ${r.employeeName}`, 'ปิด', { duration: 3000 });
      await this.load();
    } catch (err) {
      this.snack.open(errorMessage(err), 'ปิด', { duration: 5000 });
    } finally {
      this.busy.set(null);
    }
  }

  private async load(): Promise<void> {
    const status: LeaveStatus | undefined = this.filter() === 'Pending' ? 'Pending' : undefined;
    this.requests.set(await this.api.teamRequests(status));
  }
}
