using LeaveFlow.Api.Auth;
using LeaveFlow.Api.Data;
using LeaveFlow.Api.Services;
using LeaveFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Controllers;

public record PreviewResponse(decimal Days);
public record HolidayDto(DateOnly Date, string NameTh);

[ApiController]
[Route("api")]
[Authorize]
public class EmployeeController(AppDbContext db, LeaveService leave) : ControllerBase
{
    [HttpGet("balances")]
    public Task<List<BalanceDto>> Balances(int? year) =>
        leave.BalancesAsync(User.UserId(), year ?? leave.CurrentYear);

    [HttpGet("requests")]
    public Task<List<RequestDto>> MyRequests(int? year)
    {
        var me = User.UserId();
        var q = db.LeaveRequests.Where(r => r.EmployeeId == me);
        if (year is int y) q = q.Where(r => r.Start.Year == y);
        return leave.Query(q).ToListAsync();
    }

    [HttpPost("requests/preview")]
    public async Task<PreviewResponse> Preview(NewRequestDto req) => new(await leave.PreviewAsync(req));

    [HttpPost("requests")]
    public async Task<ActionResult<RequestDto>> Create(NewRequestDto req)
    {
        var created = await leave.CreateAsync(User.UserId(), req);
        return Created($"/api/requests/{created.Id}", await leave.DtoAsync(created.Id));
    }

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<ActionResult<RequestDto>> Cancel(int id)
    {
        var r = await db.LeaveRequests.FindAsync(id);
        if (r is null) return NotFound();
        var me = User.UserId();
        if (r.EmployeeId != me && !User.IsHr()) return Forbid();

        r.Cancel(me, User.IsHr(), leave.UtcNow);
        await db.SaveChangesAsync();
        await leave.AuditAsync(r.Id, me, AuditAction.Cancelled, null);
        return await leave.DtoAsync(r.Id);
    }

    [HttpGet("holidays")]
    public Task<List<HolidayDto>> Holidays(int? year)
    {
        var y = year ?? leave.CurrentYear;
        return db.Holidays.Where(h => h.Date.Year == y).OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Date, h.NameTh)).ToListAsync();
    }
}
