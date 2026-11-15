namespace LeaveFlow.Domain;

public enum HalfDay { None, Morning, Afternoon }

/// <summary>A business rule was broken. Message is Thai and safe to show to the user.</summary>
public class DomainException(string message) : Exception(message);

public static class LeaveDayCalculator
{
    /// <summary>
    /// Working days between <paramref name="start"/> and <paramref name="end"/> inclusive,
    /// skipping Saturdays, Sundays and <paramref name="holidays"/>. A half-day request counts 0.5
    /// and must be a single date. Ranges must stay within one calendar (leave) year.
    /// </summary>
    public static decimal WorkingDays(DateOnly start, DateOnly end, HalfDay half, IReadOnlySet<DateOnly> holidays)
    {
        if (end < start) throw new DomainException("วันสิ้นสุดต้องไม่ก่อนวันเริ่มลา");
        if (start.Year != end.Year) throw new DomainException("ช่วงลาต้องอยู่ในปีเดียวกัน กรุณาแยกเป็นสองคำขอ");
        if (half != HalfDay.None && start != end) throw new DomainException("ลาครึ่งวันได้ครั้งละหนึ่งวันเท่านั้น");

        var days = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (IsWorkingDay(d, holidays)) days++;
        }
        return half == HalfDay.None ? days : days * 0.5m;
    }

    public static bool IsWorkingDay(DateOnly d, IReadOnlySet<DateOnly> holidays) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(d);
}
