using System.Net;
using System.Net.Http.Json;

namespace LeaveFlow.Api.Tests;

public record EmployeeBody(int Id, string Name, string Email, string Department, string Role, int? ManagerId,
    string? ManagerName, DateOnly StartDate, bool Active);
public record LeaveTypeBody(int Id, string NameTh, decimal YearlyQuota);
public record ReportBody(string Month, List<string> LeaveTypes, List<ReportRowBody> Rows);
public record ReportRowBody(int EmployeeId, string Name, string Department, Dictionary<string, decimal> Days, decimal Total);

public class HrTests
{
    private const string Hr = "hr@leaveflow.demo", Mgr = "manager@leaveflow.demo";

    [Fact]
    public async Task NonHr_Is403()
    {
        using var f = new ApiFactory();
        var mgr = await f.LoginAs(Mgr);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.GetAsync("/api/hr/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.GetAsync("/api/hr/reports/monthly?month=2026-11")).StatusCode);
    }

    [Fact]
    public async Task ListEmployees_IncludesManagerNames()
    {
        using var f = new ApiFactory();
        var list = (await (await f.LoginAs(Hr)).GetFromJsonAsync<List<EmployeeBody>>("/api/hr/employees"))!;
        Assert.Equal(10, list.Count);
        Assert.Equal("ธนา ทดสอบ", list.Single(e => e.Email == "employee@leaveflow.demo").ManagerName);
    }

    [Fact]
    public async Task CreateEmployee_CanThenLogIn()
    {
        using var f = new ApiFactory();
        var hr = await f.LoginAs(Hr);
        var mgrId = (await hr.GetFromJsonAsync<List<EmployeeBody>>("/api/hr/employees"))!.Single(e => e.Email == Mgr).Id;
        var res = await hr.PostAsJsonAsync("/api/hr/employees", new
        {
            name = "พนักงาน ใหม่", email = "New@LeaveFlow.demo", department = "พัฒนาระบบ", role = "Employee",
            managerId = mgrId, startDate = "2026-11-16", password = "newpass123",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var login = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "new@leaveflow.demo", password = "newpass123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData("employee@leaveflow.demo", "longenough1", "อีเมลนี้มีอยู่แล้ว")]
    [InlineData("fresh@leaveflow.demo", "short", "รหัสผ่าน")]
    public async Task CreateEmployee_Invalid_Is400(string email, string password, string message)
    {
        using var f = new ApiFactory();
        var res = await (await f.LoginAs(Hr)).PostAsJsonAsync("/api/hr/employees", new
        {
            name = "x", email, department = "x", role = "Employee", managerId = (int?)null, startDate = "2026-11-16", password,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(message, (await res.Content.ReadFromJsonAsync<ProblemBody>())!.Title);
    }

    [Fact]
    public async Task UpdateEmployee_ManagerCannotBeSelf()
    {
        using var f = new ApiFactory();
        var hr = await f.LoginAs(Hr);
        var piti = (await hr.GetFromJsonAsync<List<EmployeeBody>>("/api/hr/employees"))!.Single(e => e.Email == "employee@leaveflow.demo");
        var res = await hr.PutAsJsonAsync($"/api/hr/employees/{piti.Id}", new
        {
            name = piti.Name, department = piti.Department, role = piti.Role, managerId = piti.Id, active = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task LeaveTypes_CreateAndUpdate()
    {
        using var f = new ApiFactory();
        var hr = await f.LoginAs(Hr);
        var created = await (await hr.PostAsJsonAsync("/api/hr/leave-types", new { nameTh = "ลาบวช", yearlyQuota = 15 }))
            .Content.ReadFromJsonAsync<LeaveTypeBody>();
        var upd = await hr.PutAsJsonAsync($"/api/hr/leave-types/{created!.Id}", new { nameTh = "ลาอุปสมบท", yearlyQuota = 120 });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
        var all = (await hr.GetFromJsonAsync<List<LeaveTypeBody>>("/api/hr/leave-types"))!;
        Assert.Contains(all, t => t.NameTh == "ลาอุปสมบท" && t.YearlyQuota == 120);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await hr.PostAsJsonAsync("/api/hr/leave-types", new { nameTh = "ผิด", yearlyQuota = -1 })).StatusCode);
    }

    [Fact]
    public async Task Holidays_AddDuplicateDelete()
    {
        using var f = new ApiFactory();
        var hr = await f.LoginAs(Hr);
        var add = await hr.PostAsJsonAsync("/api/hr/holidays", new { date = "2026-12-07", nameTh = "ชดเชยวันพ่อ" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await hr.PostAsJsonAsync("/api/hr/holidays", new { date = "2026-12-07", nameTh = "ซ้ำ" })).StatusCode);

        var types = (await hr.GetFromJsonAsync<List<LeaveTypeBody>>("/api/hr/leave-types"))!;
        var preview = await hr.PostAsJsonAsync("/api/requests/preview",
            new { leaveTypeId = types[0].Id, start = "2026-12-07", end = "2026-12-11", half = "None", reason = "" });
        Assert.Equal(3m, (await preview.Content.ReadFromJsonAsync<PreviewBody>())!.Days);

        Assert.Equal(HttpStatusCode.NoContent, (await hr.DeleteAsync("/api/hr/holidays/2026-12-07")).StatusCode);
    }

    [Fact]
    public async Task MonthlyReport_CountsApprovedDaysInsideMonth()
    {
        using var f = new ApiFactory();
        var report = (await (await f.LoginAs(Hr)).GetFromJsonAsync<ReportBody>("/api/hr/reports/monthly?month=2026-11"))!;
        Assert.Equal(["ลาพักร้อน", "ลาป่วย", "ลากิจ"], report.LeaveTypes);
        var row = (string name) => report.Rows.Single(r => r.Name == name);
        Assert.Equal(1m, row("ปิติ สมมุติ").Days["ลาป่วย"]);
        Assert.Equal(0m, row("ปิติ สมมุติ").Days["ลาพักร้อน"]); // vacation was in October; Nov 23 is still pending
        Assert.Equal(1m, row("มานะ เดโม").Days["ลาพักร้อน"]);  // 30 Nov – 2 Dec: only 30 Nov is in November
        Assert.Equal(2m, row("กานต์ สมมุติ").Total);
        Assert.Equal(10, report.Rows.Count);
    }

    [Fact]
    public async Task MonthlyReport_Csv_HasBomAndThaiHeader()
    {
        using var f = new ApiFactory();
        var res = await (await f.LoginAs(Hr)).GetAsync("/api/hr/reports/monthly?month=2026-11&format=csv");
        Assert.Equal("text/csv", res.Content.Headers.ContentType!.MediaType);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).TrimEnd().Split("\r\n");
        Assert.Equal("ชื่อ,แผนก,ลาพักร้อน,ลาป่วย,ลากิจ,รวม", lines[0]);
        Assert.Equal(11, lines.Length);
    }
}
