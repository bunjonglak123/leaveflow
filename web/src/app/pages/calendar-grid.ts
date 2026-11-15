import { toIsoDate } from '../core/api';
import { CalendarEntry } from '../core/models';

export interface DayCell {
  iso: string;
  day: number;
  inMonth: boolean;
  weekend: boolean;
  holiday: string | null;
  entries: CalendarEntry[];
}

/**
 * Monday-first weeks covering <month> (yyyy-MM). Leave entries are placed on every working day
 * they cover (weekends and holidays are skipped, matching how days are counted).
 */
export function buildMonthGrid(month: string, entries: CalendarEntry[], holidays: Map<string, string>): DayCell[][] {
  const [y, m] = month.split('-').map(Number);
  const first = new Date(y, m - 1, 1);
  const offset = (first.getDay() + 6) % 7; // Monday = 0
  const cursor = new Date(y, m - 1, 1 - offset);
  const weeks: DayCell[][] = [];

  do {
    const week: DayCell[] = [];
    for (let i = 0; i < 7; i++) {
      const iso = toIsoDate(cursor);
      const weekend = cursor.getDay() === 0 || cursor.getDay() === 6;
      const holiday = holidays.get(iso) ?? null;
      week.push({
        iso,
        day: cursor.getDate(),
        inMonth: cursor.getMonth() === m - 1,
        weekend,
        holiday,
        entries: weekend || holiday ? [] : entries.filter((e) => e.start <= iso && iso <= e.end),
      });
      cursor.setDate(cursor.getDate() + 1);
    }
    weeks.push(week);
  } while (cursor.getMonth() === m - 1);

  return weeks;
}

/** yyyy-MM shifted by <delta> months. */
export function shiftMonth(month: string, delta: number): string {
  const [y, m] = month.split('-').map(Number);
  const d = new Date(y, m - 1 + delta, 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}
