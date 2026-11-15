import { CalendarEntry } from '../core/models';
import { buildMonthGrid, shiftMonth } from './calendar-grid';

const entry = (start: string, end: string, name = 'มานะ'): CalendarEntry => ({
  requestId: 1, employeeName: name, leaveTypeName: 'ลาพักร้อน', start, end, half: 'None', status: 'Approved',
});

describe('buildMonthGrid', () => {
  it('starts on Monday and covers November 2026 in 6 weeks', () => {
    const weeks = buildMonthGrid('2026-11', [], new Map());
    expect(weeks[0][0].iso).toBe('2026-10-26'); // Nov 1 2026 is a Sunday
    expect(weeks[0][6].iso).toBe('2026-11-01');
    expect(weeks.length).toBe(6);
    expect(weeks.at(-1)!.some((d) => d.iso === '2026-11-30')).toBe(true);
  });

  it('places a leave on working days only', () => {
    const weeks = buildMonthGrid('2026-11', [entry('2026-11-20', '2026-11-23')], new Map());
    const days = weeks.flat().filter((d) => d.entries.length).map((d) => d.iso);
    expect(days).toEqual(['2026-11-20', '2026-11-23']);
  });

  it('marks holidays and leaves them empty', () => {
    const weeks = buildMonthGrid('2026-12', [entry('2026-12-09', '2026-12-11')], new Map([['2026-12-10', 'วันรัฐธรรมนูญ']]));
    const dec10 = weeks.flat().find((d) => d.iso === '2026-12-10')!;
    expect(dec10.holiday).toBe('วันรัฐธรรมนูญ');
    expect(dec10.entries).toEqual([]);
  });
});

describe('shiftMonth', () => {
  it('crosses year boundaries', () => {
    expect(shiftMonth('2026-12', 1)).toBe('2027-01');
    expect(shiftMonth('2026-01', -1)).toBe('2025-12');
  });
});
