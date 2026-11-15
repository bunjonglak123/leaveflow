using System.Globalization;
using System.Text;
using LeaveFlow.Api.Data;
using LeaveFlow.Api.Services;
using LeaveFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Controllers;

public record EmployeeDto(int Id, string Name, string Email, string Department, Role Role, int? ManagerId,
    string? ManagerName, DateOnly StartDate, bool Active);
public record NewEmployeeDto(string Name, string Email, string Department, Role Role, int? ManagerId, DateOnly StartDate, string Password);
public record UpdateEmployeeDto(string Name, string Department, Role Role, int? ManagerId, bool Active);
public record LeaveTypeDto(int Id, string NameTh, decimal YearlyQuota);
public record SaveLeaveTypeDto(string NameTh, decimal YearlyQuota);
public record ReportRowDto(int EmployeeId, string Name, string Department, Dictionary<string, decimal> Days, decimal Total);
public record MonthlyReportDto(string Month, List<string> LeaveTypes, List<ReportRowDto> Rows);

[ApiController]
[Route("api/hr")]
[Authorize(Policy = "Hr")]
public class HrController(AppDbContext db, LeaveService leave) : ControllerBase
{
    private static readonly PasswordHasher<Employee> Hasher = new();

    // ---------- employees ----------

    [HttpGet("employees")]
    public Task<List<EmployeeDto>> Employees() =>
        (from e in db.Employees
         join m in db.Employees on e.ManagerId equals m.Id into mg
         from m in mg.DefaultIfEmpty()
         orderby e.Department, e.Name
         select new EmployeeDto(e.Id, e.Name, e.Email, e.Department, e.Role, e.ManagerId, m == null ? null : m.Name,
             e.StartDate, e.Active)).ToListAsync();

    [HttpPost("employees")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee(NewEmployeeDto req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(req.Name) || !email.Contains('@')) throw new DomainException("กรุณากรอกชื่อและอีเมลให้ถูกต้อง");
        if ((req.Password ?? "").Length < 8) throw new DomainException("รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร");
        if (await db.Employees.AnyAsync(e => e.Email == email)) throw new DomainException("อีเมลนี้มีอยู่แล้ว");
        await EnsureManagerExists(req.ManagerId);

        var e = new Employee
        {
            Name = req.Name.Trim(), Email = email, Department = (req.Department ?? "").Trim(), Role = req.Role,
            ManagerId = req.ManagerId, StartDate = req.StartDate,
        };
        e.PasswordHash = Hasher.HashPassword(e, req.Password!);
        db.Employees.Add(e);
        await db.SaveChangesAsync();
        return Created($"/api/hr/employees/{e.Id}", (await Employees()).Single(x => x.Id == e.Id));
    }

    [HttpPut("employees/{id:int}")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(int id, UpdateEmployeeDto req)
    {
        var e = await db.Employees.FindAsync(id);
        if (e is null) return NotFound();
        if (req.ManagerId == id) throw new DomainException("หัวหน้างานต้องไม่ใช่ตัวเอง");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new DomainException("กรุณากรอกชื่อ");
        await EnsureManagerExists(req.ManagerId);

        e.Name = req.Name.Trim();
        e.Department = (req.Department ?? "").Trim();
        e.Role = req.Role;
        e.ManagerId = req.ManagerId;
        e.Active = req.Active;
        await db.SaveChangesAsync();
        return (await Employees()).Single(x => x.Id == id);
    }

    private async Task EnsureManagerExists(int? managerId)
    {
        if (managerId is int m && !await db.Employees.AnyAsync(e => e.Id == m)) throw new DomainException("ไม่พบหัวหน้างานที่เลือก");
    }

    // ---------- leave types ----------

    [HttpGet("leave-types")]
    public Task<List<LeaveTypeDto>> LeaveTypes() =>
        db.LeaveTypes.OrderBy(t => t.Id).Select(t => new LeaveTypeDto(t.Id, t.NameTh, t.YearlyQuota)).ToListAsync();

    [HttpPost("leave-types")]
    public async Task<ActionResult<LeaveTypeDto>> CreateLeaveType(SaveLeaveTypeDto req)
    {
        Validate(req);
        var t = new LeaveType { NameTh = req.NameTh.Trim(), YearlyQuota = req.YearlyQuota };
        db.LeaveTypes.Add(t);
        await db.SaveChangesAsync();
        return Created($"/api/hr/leave-types/{t.Id}", new LeaveTypeDto(t.Id, t.NameTh, t.YearlyQuota));
    }

    [HttpPut("leave-types/{id:int}")]
    public async Task<ActionResult<LeaveTypeDto>> UpdateLeaveType(int id, SaveLeaveTypeDto req)
    {
        Validate(req);
        var t = await db.LeaveTypes.FindAsync(id);
        if (t is null) return NotFound();
        t.NameTh = req.NameTh.Trim();
        t.YearlyQuota = req.YearlyQuota;
        await db.SaveChangesAsync();
        return new LeaveTypeDto(t.Id, t.NameTh, t.YearlyQuota);
    }

    private static void Validate(SaveLeaveTypeDto req)
    {
        if (string.IsNullOrWhiteSpace(req.NameTh)) throw new DomainException("กรุณากรอกชื่อประเภทการลา");
        if (req.YearlyQuota is < 0 or > 365) throw new DomainException("จำนวนวันต่อปีต้องอยู่ระหว่าง 0 ถึง 365");
    }

    // ---------- holidays ----------

    [HttpPost("holidays")]
    public async Task<ActionResult<HolidayDto>> AddHoliday(HolidayDto req)
    {
        if (string.IsNullOrWhiteSpace(req.NameTh)) throw new DomainException("กรุณากรอกชื่อวันหยุด");
        if (await db.Holidays.AnyAsync(h => h.Date == req.Date)) throw new DomainException("มีวันหยุดในวันที่นี้อยู่แล้ว");
        db.Holidays.Add(new Holiday { Date = req.Date, NameTh = req.NameTh.Trim() });
        await db.SaveChangesAsync();
        return Created($"/api/hr/holidays/{req.Date:yyyy-MM-dd}", req with { NameTh = req.NameTh.Trim() });
    }

    [HttpDelete("holidays/{date}")]
    public async Task<IActionResult> DeleteHoliday(DateOnly date)
    {
        var h = await db.Holidays.FindAsync(date);
        if (h is null) return NotFound();
        db.Holidays.Remove(h);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- reports ----------

    /// <summary>Approved leave days per employee and type that fall inside <paramref name="month"/> (YYYY-MM).</summary>
    [HttpGet("reports/monthly")]
    public async Task<IActionResult> Monthly(string month, string? format)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return Problem(title: "รูปแบบเดือนไม่ถูกต้อง (ใช้ YYYY-MM)", statusCode: 400);
        var last = first.AddMonths(1).AddDays(-1);

        var types = await db.LeaveTypes.OrderBy(t => t.Id).ToListAsync();
        var employees = await db.Employees.Where(e => e.Active).OrderBy(e => e.Department).ThenBy(e => e.Name).ToListAsync();
        var approved = await db.LeaveRequests
            .Where(r => r.Status == LeaveStatus.Approved && r.Start <= last && r.End >= first).ToListAsync();
        var holidays = await leave.HolidaysAsync(first.Year);

        var rows = employees.Select(e =>
        {
            var days = types.ToDictionary(t => t.NameTh, t => approved
                .Where(r => r.EmployeeId == e.Id && r.LeaveTypeId == t.Id)
                .Sum(r => LeaveDayCalculator.WorkingDays(Max(r.Start, first), Min(r.End, last), r.Half, holidays)));
            return new ReportRowDto(e.Id, e.Name, e.Department, days, days.Values.Sum());
        }).ToList();
        var report = new MonthlyReportDto(month, types.Select(t => t.NameTh).ToList(), rows);

        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)) return Ok(report);

        var csv = new StringBuilder();
        csv.Append(string.Join(",", new[] { "ชื่อ", "แผนก" }.Concat(report.LeaveTypes).Append("รวม").Select(Csv))).Append("\r\n");
        foreach (var r in rows)
        {
            var cells = new[] { r.Name, r.Department }
                .Concat(report.LeaveTypes.Select(t => r.Days[t].ToString("0.#", CultureInfo.InvariantCulture)))
                .Append(r.Total.ToString("0.#", CultureInfo.InvariantCulture));
            csv.Append(string.Join(",", cells.Select(Csv))).Append("\r\n");
        }
        // BOM so Excel opens Thai text correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"leave-report-{month}.csv");
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static string Csv(string s)
    {
        // Quote when needed; neutralise spreadsheet formula injection.
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
