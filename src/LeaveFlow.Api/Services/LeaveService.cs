using LeaveFlow.Api.Data;
using LeaveFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Services;

public record BalanceDto(int LeaveTypeId, string NameTh, decimal Quota, decimal Used, decimal Pending, decimal Remaining);
public record RequestDto(int Id, int EmployeeId, string EmployeeName, int LeaveTypeId, string LeaveTypeName,
    DateOnly Start, DateOnly End, HalfDay Half, decimal Days, string Reason, LeaveStatus Status,
    string? DecisionNote, DateTime CreatedAt, DateTime? DecidedAt);
public record NewRequestDto(int LeaveTypeId, DateOnly Start, DateOnly End, HalfDay Half, string? Reason);

/// <summary>Application logic shared by the employee, manager and HR endpoints.</summary>
public class LeaveService(AppDbContext db, TimeProvider clock)
{
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;
    public int CurrentYear => clock.GetLocalNow().Year;

    public async Task<HashSet<DateOnly>> HolidaysAsync(int year)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        return (await db.Holidays.Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync()).ToHashSet();
    }

    public async Task<decimal> PreviewAsync(NewRequestDto req) =>
        LeaveDayCalculator.WorkingDays(req.Start, req.End, req.Half, await HolidaysAsync(req.Start.Year));

    public async Task<List<BalanceDto>> BalancesAsync(int employeeId, int year)
    {
        var types = await db.LeaveTypes.OrderBy(t => t.Id).ToListAsync();
        var mine = await db.LeaveRequests.Where(r => r.EmployeeId == employeeId).ToListAsync();
        return types.Select(t =>
        {
            var b = LeavePolicy.Balance(t.Id, t.YearlyQuota, year, mine);
            return new BalanceDto(t.Id, t.NameTh, b.Quota, b.Used, b.Pending, b.Remaining);
        }).ToList();
    }

    public async Task<LeaveRequest> CreateAsync(int employeeId, NewRequestDto req)
    {
        var type = await db.LeaveTypes.FindAsync(req.LeaveTypeId) ?? throw new DomainException("ไม่พบประเภทการลา");
        var reason = (req.Reason ?? "").Trim();
        if (reason.Length > 500) throw new DomainException("เหตุผลยาวเกิน 500 ตัวอักษร");

        var days = await PreviewAsync(req);
        var candidate = LeaveRequest.Create(employeeId, type.Id, req.Start, req.End, req.Half, days, reason, UtcNow);
        var mine = await db.LeaveRequests.Where(r => r.EmployeeId == employeeId).ToListAsync();
        LeavePolicy.EnsureCanRequest(candidate, mine, type.YearlyQuota);

        db.LeaveRequests.Add(candidate);
        await db.SaveChangesAsync();
        await AuditAsync(candidate.Id, employeeId, AuditAction.Created, null);

        // Nobody can approve: no manager and no other HR admin (e.g. a sole HR admin). Don't leave it stuck.
        var me = await db.Employees.FindAsync(employeeId);
        var hasApprover = me?.ManagerId is not null
            || await db.Employees.AnyAsync(e => e.Role == Role.HrAdmin && e.Active && e.Id != employeeId);
        if (!hasApprover)
        {
            const string note = "อนุมัติอัตโนมัติ: ไม่มีผู้อนุมัติในระบบ";
            candidate.AutoApprove(note, UtcNow);
            await db.SaveChangesAsync();
            await AuditAsync(candidate.Id, employeeId, AuditAction.Approved, note);
        }
        return candidate;
    }

    public async Task AuditAsync(int requestId, int actorId, AuditAction action, string? note)
    {
        db.AuditEntries.Add(new AuditEntry { RequestId = requestId, ActorId = actorId, Action = action, At = UtcNow, Note = note });
        await db.SaveChangesAsync();
    }

    /// <summary>Requests joined with employee and leave-type names, newest start date first.</summary>
    public IQueryable<RequestDto> Query(IQueryable<LeaveRequest> requests) =>
        from r in requests
        join e in db.Employees on r.EmployeeId equals e.Id
        join t in db.LeaveTypes on r.LeaveTypeId equals t.Id
        orderby r.Start descending, r.Id descending
        select new RequestDto(r.Id, e.Id, e.Name, t.Id, t.NameTh, r.Start, r.End, r.Half, r.Days, r.Reason,
            r.Status, r.DecisionNote, r.CreatedAt, r.DecidedAt);

    public async Task<RequestDto> DtoAsync(int requestId) =>
        await Query(db.LeaveRequests.Where(r => r.Id == requestId)).SingleAsync();
}
