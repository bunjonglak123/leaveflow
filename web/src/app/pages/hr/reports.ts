import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Api, toIsoDate } from '../../core/api';
import { errorMessage } from '../../core/auth.interceptor';
import { MonthlyReport } from '../../core/models';
import { shiftMonth } from '../calendar-grid';

@Component({
  selector: 'app-reports',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="page">
      <h1>รายงานการลารายเดือน</h1>
      <p class="sub">จำนวนวันลาที่อนุมัติแล้วของพนักงานแต่ละคน แยกตามประเภท</p>

      <section class="card">
        <div class="head">
          <button mat-icon-button (click)="go(-1)" aria-label="เดือนก่อน"><mat-icon fontSet="material-symbols-outlined">chevron_left</mat-icon></button>
          <h2>{{ title() }}</h2>
          <button mat-icon-button (click)="go(1)" aria-label="เดือนถัดไป"><mat-icon fontSet="material-symbols-outlined">chevron_right</mat-icon></button>
          <button mat-stroked-button class="csv" (click)="downloadCsv()">
            <mat-icon fontSet="material-symbols-outlined">download</mat-icon> ดาวน์โหลด CSV (Excel)
          </button>
        </div>

        @if (report(); as r) {
          <div class="table-wrap">
            <table class="rep">
              <thead>
                <tr><th>ชื่อ</th><th>แผนก</th>@for (t of r.leaveTypes; track t) { <th class="n">{{ t }}</th> }<th class="n">รวม</th></tr>
              </thead>
              <tbody>
                @for (row of r.rows; track row.employeeId) {
                  <tr [class.zero]="row.total === 0">
                    <td>{{ row.name }}</td><td class="muted">{{ row.department }}</td>
                    @for (t of r.leaveTypes; track t) { <td class="n">{{ row.days[t] || '–' }}</td> }
                    <td class="n"><b>{{ row.total || '–' }}</b></td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr><td colspan="2">รวมทั้งหมด</td>@for (t of r.leaveTypes; track t) { <td class="n">{{ totals()[t] }}</td> }<td class="n"><b>{{ totals()['__all'] }}</b></td></tr>
              </tfoot>
            </table>
          </div>
        }
      </section>
    </div>
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 4px; margin-bottom: 12px; flex-wrap: wrap; }
    .head h2 { margin: 0 8px; min-width: 140px; text-align: center; }
    .csv { margin-left: auto; }
    table.rep { width: 100%; border-collapse: collapse; font-size: 14px; }
    .rep th, .rep td { padding: 10px 12px; border-bottom: 1px solid var(--lf-border); text-align: left; white-space: nowrap; }
    .rep th { font-weight: 600; color: var(--lf-text-2); background: var(--lf-bg); }
    .rep .n { text-align: right; }
    .rep tr.zero td { color: var(--lf-text-2); }
    .rep tfoot td { font-weight: 600; border-bottom: 0; }
  `,
})
export class ReportsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly snack = inject(MatSnackBar);
  protected readonly month = signal(toIsoDate(new Date()).slice(0, 7));
  protected readonly report = signal<MonthlyReport | null>(null);
  protected readonly title = computed(() => {
    const [y, m] = this.month().split('-').map(Number);
    return new Date(y, m - 1, 1).toLocaleDateString('th-TH', { month: 'long', year: 'numeric' });
  });
  protected readonly totals = computed(() => {
    const r = this.report();
    const out: Record<string, number> = { __all: 0 };
    if (!r) return out;
    for (const t of r.leaveTypes) out[t] = r.rows.reduce((s, row) => s + (row.days[t] ?? 0), 0);
    out['__all'] = r.rows.reduce((s, row) => s + row.total, 0);
    return out;
  });

  ngOnInit(): Promise<void> {
    return this.load();
  }

  protected go(delta: number): Promise<void> {
    this.month.set(shiftMonth(this.month(), delta));
    return this.load();
  }

  protected async downloadCsv(): Promise<void> {
    try {
      const blob = await this.api.monthlyReportCsv(this.month());
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `leave-report-${this.month()}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      this.snack.open(errorMessage(err), 'ปิด', { duration: 5000 });
    }
  }

  private async load(): Promise<void> {
    this.report.set(await this.api.monthlyReport(this.month()));
  }
}
