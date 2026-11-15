import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Api, toIsoDate } from '../core/api';
import { CalendarEntry, HALF_LABEL, STATUS_LABEL } from '../core/models';
import { buildMonthGrid, shiftMonth } from './calendar-grid';

@Component({
  selector: 'app-calendar',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="page">
      <h1>ปฏิทินทีม</h1>
      <p class="sub">ใครลาวันไหน (รวมคำขอที่รออนุมัติ)</p>

      <section class="card">
        <div class="head">
          <button mat-icon-button (click)="go(-1)" aria-label="เดือนก่อน"><mat-icon fontSet="material-symbols-outlined">chevron_left</mat-icon></button>
          <h2>{{ title() }}</h2>
          <button mat-icon-button (click)="go(1)" aria-label="เดือนถัดไป"><mat-icon fontSet="material-symbols-outlined">chevron_right</mat-icon></button>
          <span class="legend"><span class="chip Approved">อนุมัติแล้ว</span><span class="chip Pending">รออนุมัติ</span></span>
        </div>
        <div class="grid">
          @for (d of weekdays; track d) { <div class="wd">{{ d }}</div> }
          @for (week of weeks(); track $index) {
            @for (c of week; track c.iso) {
              <div class="cell" [class.out]="!c.inMonth" [class.off]="c.weekend || c.holiday" [class.today]="c.iso === today">
                <div class="num">{{ c.day }}</div>
                @if (c.holiday) { <div class="hol">{{ c.holiday }}</div> }
                @for (e of c.entries; track e.requestId) {
                  <div class="chip {{ e.status }}" [matTooltip]="tip(e)">{{ e.employeeName }}</div>
                }
              </div>
            }
          }
        </div>
      </section>
    </div>
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 4px; margin-bottom: 12px; flex-wrap: wrap; }
    .head h2 { margin: 0 8px; min-width: 140px; text-align: center; }
    .legend { margin-left: auto; display: flex; gap: 6px; }
    .grid { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); border-top: 1px solid var(--lf-border); border-left: 1px solid var(--lf-border); }
    .wd { font-size: 13px; color: var(--lf-text-2); padding: 6px; text-align: center; border-right: 1px solid var(--lf-border); border-bottom: 1px solid var(--lf-border); background: var(--lf-bg); }
    .cell { min-height: 96px; padding: 4px; border-right: 1px solid var(--lf-border); border-bottom: 1px solid var(--lf-border); overflow: hidden; }
    .cell.out { opacity: .45; }
    .cell.off { background: #f3f4f6; }
    .cell.today .num { background: var(--mat-sys-primary); color: #fff; border-radius: 99px; }
    .num { font-size: 13px; width: 24px; height: 24px; display: grid; place-items: center; }
    .hol { font-size: 11px; color: var(--lf-rejected); line-height: 1.2; }
    .chip { font-size: 12px; border-radius: 6px; padding: 1px 6px; margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .chip.Approved { background: var(--lf-approved-bg); color: var(--lf-approved); }
    .chip.Pending { background: var(--lf-pending-bg); color: var(--lf-pending); border: 1px dashed currentColor; }
    @media (max-width: 600px) { .cell { min-height: 64px; } .chip { font-size: 10px; padding: 0 3px; } }
  `,
})
export class CalendarPage implements OnInit {
  private readonly api = inject(Api);
  protected readonly weekdays = ['จ.', 'อ.', 'พ.', 'พฤ.', 'ศ.', 'ส.', 'อา.'];
  protected readonly today = toIsoDate(new Date());
  protected readonly month = signal(this.today.slice(0, 7));
  private readonly entries = signal<CalendarEntry[]>([]);
  private readonly holidays = signal(new Map<string, string>());

  protected readonly weeks = computed(() => buildMonthGrid(this.month(), this.entries(), this.holidays()));
  protected readonly title = computed(() => {
    const [y, m] = this.month().split('-').map(Number);
    return new Date(y, m - 1, 1).toLocaleDateString('th-TH', { month: 'long', year: 'numeric' });
  });

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected go(delta: number): Promise<void> {
    this.month.set(shiftMonth(this.month(), delta));
    return this.load();
  }

  protected tip(e: CalendarEntry): string {
    const half = e.half !== 'None' ? ` ${HALF_LABEL[e.half]}` : '';
    return `${e.employeeName} · ${e.leaveTypeName}${half} · ${STATUS_LABEL[e.status]}`;
  }

  private async load(): Promise<void> {
    const year = Number(this.month().slice(0, 4));
    const [entries, holidays] = await Promise.all([this.api.calendar(this.month()), this.api.holidays(year)]);
    this.entries.set(entries);
    this.holidays.set(new Map(holidays.map((h) => [h.date, h.nameTh])));
  }
}
