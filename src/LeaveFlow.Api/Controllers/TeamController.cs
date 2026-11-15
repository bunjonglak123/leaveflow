using System.Globalization;
using LeaveFlow.Api.Auth;
using LeaveFlow.Api.Data;
using LeaveFlow.Api.Services;
using LeaveFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Controllers;

public record DecisionDto(string? Note);
public record CalendarEntryDto(int RequestId, string EmployeeName, string LeaveTypeName, DateOnly Start, DateOnly End,
    HalfDay Half, LeaveStatus Status);
public record AuditDto(AuditAction Action, string ActorName, DateTime At, string? Note);

[ApiController]
[Route("api")]
[Authorize]
public class TeamController(AppDbContext db, LeaveService leave) : ControllerBase
{
    /// <summary>Requests the caller may decide on: direct reports, or everyone except themself for HR.</summary>
    private IQueryable<LeaveRequest> TeamRequests()
    {
        var me = User.UserId();
        if (User.IsHr()) return db.LeaveRequests.Where(r => r.EmployeeId != me);
        var reports = db.Employees.Where(e => e.ManagerId == me).Select(e => e.Id);
        return db.LeaveRequests.Where(r => reports.Contains(r.EmployeeId));
    }

    [HttpGet("team/requests")]
    [Authorize(Policy = "Manager")]
    public Task<List<RequestDto>> Queue(LeaveStatus? status)
    {
        var q = TeamRequests();
        if (status is { } s) q = q.Where(r => r.Status == s);
        return leave.Query(q).ToListAsync();
    }

    [HttpPost("team/requests/{id:int}/approve")]
    [Authorize(Policy = "Manager")]
    public Task<ActionResult<RequestDto>> Approve(int id, DecisionDto body) =>
        Decide(id, body, (r, me, now) => r.Approve(me, body.Note, now), AuditAction.Approved);

    [HttpPost("team/requests/{id:int}/reject")]
    [Authorize(Policy = "Manager")]
    public Task<ActionResult<RequestDto>> Reject(int id, DecisionDto body) =>
        Decide(id, body, (r, me, now) => r.Reject(me, body.Note, now), AuditAction.Rejected);

    private async Task<ActionResult<RequestDto>> Decide(int id, DecisionDto body,
        Action<LeaveRequest, int, DateTime> decide, AuditAction action)
    {
        if (!await db.LeaveRequests.AnyAsync(r => r.Id == id)) return NotFound();
        var r = await TeamRequests().SingleOrDefaultAsync(x => x.Id == id);
        if (r is null) return Forbid();

        var me = User.UserId();
        decide(r, me, leave.UtcNow);
        await db.SaveChangesAsync();
        await leave.AuditAsync(r.Id, me, action, r.DecisionNote);
        return await leave.DtoAsync(r.Id);
    }

    [HttpGet("team/calendar")]
    [Authorize(Policy = "Manager")]
    public async Task<ActionResult<List<CalendarEntryDto>>> Calendar(string month)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return Problem(title: "รูปแบบเดือนไม่ถูกต้อง (ใช้ YYYY-MM)", statusCode: 400);
        var last = first.AddMonths(1).AddDays(-1);

        var q = TeamRequests().Where(r =>
            (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved) && r.Start <= last && r.End >= first);
        return await (from r in q
                      join e in db.Employees on r.EmployeeId equals e.Id
                      join t in db.LeaveTypes on r.LeaveTypeId equals t.Id
                      orderby r.Start, e.Name
                      select new CalendarEntryDto(r.Id, e.Name, t.NameTh, r.Start, r.End, r.Half, r.Status)).ToListAsync();
    }

    /// <summary>Status timeline. Visible to the requester, their manager, and HR.</summary>
    [HttpGet("requests/{id:int}/history")]
    public async Task<ActionResult<List<AuditDto>>> History(int id)
    {
        var r = await db.LeaveRequests.FindAsync(id);
        if (r is null) return NotFound();
        var me = User.UserId();
        var isManager = await db.Employees.AnyAsync(e => e.Id == r.EmployeeId && e.ManagerId == me);
        if (r.EmployeeId != me && !isManager && !User.IsHr()) return Forbid();

        return await (from a in db.AuditEntries.Where(a => a.RequestId == id)
                      join e in db.Employees on a.ActorId equals e.Id
                      orderby a.At, a.Id
                      select new AuditDto(a.Action, e.Name, a.At, a.Note)).ToListAsync();
    }
}
