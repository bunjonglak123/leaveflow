using LeaveFlow.Domain;
using Microsoft.AspNetCore.Identity;

namespace LeaveFlow.Api.Data;

/// <summary>
/// Fake demo organisation. All names are made up. Dates are relative to <c>today</c> so the demo
/// always looks current. Every seeded account uses password <see cref="DemoPassword"/>.
/// </summary>
public static class DemoSeed
{
    public const string DemoPassword = "demo1234";

    public static void Run(AppDbContext db, DateOnly today)
    {
        if (db.Employees.Any()) return;

        var types = FirstRunSetup.DefaultLeaveTypes().ToArray();
        db.LeaveTypes.AddRange(types);
        db.Holidays.AddRange(FixedThaiHolidays(today.Year));

        var hasher = new PasswordHasher<Employee>();
        Employee Person(string name, string email, string dept, Role role, Employee? manager)
        {
            var e = new Employee
            {
                Name = name, Email = email, Department = dept, Role = role,
                StartDate = today.AddYears(-2), ManagerId = manager?.Id,
            };
            e.PasswordHash = hasher.HashPassword(e, DemoPassword);
            db.Employees.Add(e);
            db.SaveChanges(); // need Id for reports
            return e;
        }

        var hr = Person("มาลี ใจงาม", "hr@leaveflow.demo", "ฝ่ายบุคคล", Role.HrAdmin, null);
        var itMgr = Person("ธนา ทดสอบ", "manager@leaveflow.demo", "พัฒนาระบบ", Role.Manager, hr);
        var salesMgr = Person("วิภา ตัวอย่าง", "sales.manager@leaveflow.demo", "ฝ่ายขาย", Role.Manager, hr);
        var piti = Person("ปิติ สมมุติ", "employee@leaveflow.demo", "พัฒนาระบบ", Role.Employee, itMgr);
        var chujai = Person("ชูใจ ทดลอง", "chujai@leaveflow.demo", "พัฒนาระบบ", Role.Employee, itMgr);
        var mana = Person("มานะ เดโม", "mana@leaveflow.demo", "พัฒนาระบบ", Role.Employee, itMgr);
        Person("วีระ ตัวอย่าง", "weera@leaveflow.demo", "พัฒนาระบบ", Role.Employee, itMgr);
        var suda = Person("สุดา ทดสอบ", "suda@leaveflow.demo", "ฝ่ายขาย", Role.Employee, salesMgr);
        var karn = Person("กานต์ สมมุติ", "karn@leaveflow.demo", "ฝ่ายขาย", Role.Employee, salesMgr);
        Person("นภา เดโม", "napa@leaveflow.demo", "ฝ่ายขาย", Role.Employee, salesMgr);

        var holidays = db.Holidays.Local.Select(h => h.Date).ToHashSet();
        var created = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        var (vacation, sick, personal) = (types[0].Id, types[1].Id, types[2].Id);

        void Add(Employee who, int type, int startOffset, int workdays, HalfDay half, string reason,
            LeaveStatus status, Employee? decider = null, string? note = null)
        {
            var start = Workday(today, startOffset, holidays);
            var end = start;
            for (var i = 1; i < workdays; i++) end = Workday(end, 1, holidays);
            if (end.Year != start.Year) end = new DateOnly(start.Year, 12, 31);
            var days = LeaveDayCalculator.WorkingDays(start, end, half, holidays);
            var r = LeaveRequest.Create(who.Id, type, start, end, half, days, reason, created);
            if (status == LeaveStatus.Approved) r.Approve(decider!.Id, note, created.AddHours(2));
            if (status == LeaveStatus.Rejected) r.Reject(decider!.Id, note, created.AddHours(2));
            db.LeaveRequests.Add(r);
            db.SaveChanges();
            db.AuditEntries.Add(new AuditEntry { RequestId = r.Id, ActorId = who.Id, Action = AuditAction.Created, At = created });
            if (status is LeaveStatus.Approved or LeaveStatus.Rejected)
            {
                db.AuditEntries.Add(new AuditEntry
                {
                    RequestId = r.Id, ActorId = decider!.Id, At = created.AddHours(2), Note = note,
                    Action = status == LeaveStatus.Approved ? AuditAction.Approved : AuditAction.Rejected,
                });
            }
        }

        Add(piti, vacation, -25, 2, HalfDay.None, "ไปเที่ยวต่างจังหวัดกับครอบครัว", LeaveStatus.Approved, itMgr);
        Add(piti, sick, -8, 1, HalfDay.None, "ไข้หวัด", LeaveStatus.Approved, itMgr);
        Add(piti, vacation, 5, 1, HalfDay.None, "ธุระที่ต่างจังหวัด", LeaveStatus.Pending);
        Add(chujai, personal, 2, 1, HalfDay.Morning, "ไปติดต่อราชการ", LeaveStatus.Pending);
        Add(mana, vacation, -15, 1, HalfDay.None, "พักผ่อน", LeaveStatus.Rejected, itMgr, "ช่วงนั้นมีงานส่งลูกค้า ขอเลื่อนได้ไหม");
        Add(mana, vacation, 10, 3, HalfDay.None, "ไปงานแต่งเพื่อน", LeaveStatus.Approved, itMgr);
        Add(suda, vacation, 4, 2, HalfDay.None, "พาพ่อแม่ไปหาหมอ", LeaveStatus.Pending);
        Add(karn, sick, -5, 2, HalfDay.None, "ปวดหลัง หมอให้พัก", LeaveStatus.Approved, salesMgr);
        Add(itMgr, vacation, 7, 1, HalfDay.None, "ธุระส่วนตัว", LeaveStatus.Pending);
        db.SaveChanges();
    }

    /// <summary>Move <paramref name="offset"/> working days from <paramref name="from"/>, staying inside its year.</summary>
    private static DateOnly Workday(DateOnly from, int offset, IReadOnlySet<DateOnly> holidays)
    {
        var step = offset >= 0 ? 1 : -1;
        var d = from;
        var moved = 0;
        while (moved < Math.Abs(offset) || !LeaveDayCalculator.IsWorkingDay(d, holidays))
        {
            var next = d.AddDays(step);
            if (next.Year != from.Year) { step = -step; next = d.AddDays(step); }
            d = next;
            if (LeaveDayCalculator.IsWorkingDay(d, holidays)) moved++;
        }
        return d;
    }

    /// <summary>
    /// Fixed-date Thai public holidays. Lunar holidays (มาฆบูชา, วิสาขบูชา, อาสาฬหบูชา) and
    /// substitution days change every year, so HR adds them from the cabinet announcement.
    /// </summary>
    public static IEnumerable<Holiday> FixedThaiHolidays(int year) =>
    [
        new() { Date = new(year, 1, 1), NameTh = "วันขึ้นปีใหม่" },
        new() { Date = new(year, 4, 6), NameTh = "วันจักรี" },
        new() { Date = new(year, 4, 13), NameTh = "วันสงกรานต์" },
        new() { Date = new(year, 4, 14), NameTh = "วันสงกรานต์" },
        new() { Date = new(year, 4, 15), NameTh = "วันสงกรานต์" },
        new() { Date = new(year, 5, 1), NameTh = "วันแรงงานแห่งชาติ" },
        new() { Date = new(year, 5, 4), NameTh = "วันฉัตรมงคล" },
        new() { Date = new(year, 6, 3), NameTh = "วันเฉลิมพระชนมพรรษาสมเด็จพระราชินี" },
        new() { Date = new(year, 7, 28), NameTh = "วันเฉลิมพระชนมพรรษา ร.10" },
        new() { Date = new(year, 8, 12), NameTh = "วันแม่แห่งชาติ" },
        new() { Date = new(year, 10, 13), NameTh = "วันนวมินทรมหาราช" },
        new() { Date = new(year, 10, 23), NameTh = "วันปิยมหาราช" },
        new() { Date = new(year, 12, 5), NameTh = "วันพ่อแห่งชาติ" },
        new() { Date = new(year, 12, 10), NameTh = "วันรัฐธรรมนูญ" },
        new() { Date = new(year, 12, 31), NameTh = "วันสิ้นปี" },
    ];
}
