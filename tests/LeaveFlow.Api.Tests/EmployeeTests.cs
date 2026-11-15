using System.Net;
using System.Net.Http.Json;

namespace LeaveFlow.Api.Tests;

public record BalanceBody(int LeaveTypeId, string NameTh, decimal Quota, decimal Used, decimal Pending, decimal Remaining);
public record RequestBody(int Id, int EmployeeId, string EmployeeName, int LeaveTypeId, string LeaveTypeName,
    DateOnly Start, DateOnly End, string Half, decimal Days, string Reason, string Status, string? DecisionNote);
public record PreviewBody(decimal Days);
public record ProblemBody(string Title);

/// <summary>Demo org, today = Mon 16 Nov 2026. ปิติ (employee@) has vacation used 2, pending 1 (Mon 23 Nov).</summary>
public class EmployeeTests
{
    private const string Piti = "employee@leaveflow.demo", Chujai = "chujai@leaveflow.demo";

    private static async Task<int> VacationId(HttpClient c) =>
        (await c.GetFromJsonAsync<List<BalanceBody>>("/api/balances"))!.Single(b => b.NameTh == "ลาพักร้อน").LeaveTypeId;

    private static object NewRequest(int type, string start, string end, string half = "None") =>
        new { leaveTypeId = type, start, end, half, reason = "ธุระ" };

    [Fact]
    public async Task Balances_ShowUsedPendingRemaining()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var b = (await c.GetFromJsonAsync<List<BalanceBody>>("/api/balances"))!;
        var vac = b.Single(x => x.NameTh == "ลาพักร้อน");
        Assert.Equal((6m, 2m, 1m, 3m), (vac.Quota, vac.Used, vac.Pending, vac.Remaining));
        Assert.Equal(1m, b.Single(x => x.NameTh == "ลาป่วย").Used);
        Assert.Equal(3, b.Count);
    }

    [Fact]
    public async Task Create_Valid_IsPendingAndCountsAsPending()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var res = await c.PostAsJsonAsync("/api/requests", NewRequest(await VacationId(c), "2026-12-01", "2026-12-02"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var r = await res.Content.ReadFromJsonAsync<RequestBody>();
        Assert.Equal(("Pending", 2m), (r!.Status, r.Days));
        var vac = (await c.GetFromJsonAsync<List<BalanceBody>>("/api/balances"))!.Single(x => x.NameTh == "ลาพักร้อน");
        Assert.Equal(3m, vac.Pending);
    }

    [Fact]
    public async Task Create_OverBalance_Is400()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var res = await c.PostAsJsonAsync("/api/requests", NewRequest(await VacationId(c), "2026-12-01", "2026-12-04"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("วันลาคงเหลือไม่พอ", (await res.Content.ReadFromJsonAsync<ProblemBody>())!.Title);
    }

    [Fact]
    public async Task Create_OverlappingPending_Is400()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var res = await c.PostAsJsonAsync("/api/requests", NewRequest(await VacationId(c), "2026-11-23", "2026-11-23"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("ทับ", (await res.Content.ReadFromJsonAsync<ProblemBody>())!.Title);
    }

    [Fact]
    public async Task Create_WeekendOnly_Is400()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var res = await c.PostAsJsonAsync("/api/requests", NewRequest(await VacationId(c), "2026-12-05", "2026-12-06"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownLeaveType_Is400()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var res = await c.PostAsJsonAsync("/api/requests", NewRequest(999, "2026-12-01", "2026-12-01"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Preview_SkipsHoliday()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var res = await c.PostAsJsonAsync("/api/requests/preview", NewRequest(await VacationId(c), "2026-12-07", "2026-12-11"));
        Assert.Equal(4m, (await res.Content.ReadFromJsonAsync<PreviewBody>())!.Days); // 10 Dec = วันรัฐธรรมนูญ
    }

    [Fact]
    public async Task List_ReturnsOnlyMyRequests()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var mine = (await c.GetFromJsonAsync<List<RequestBody>>("/api/requests"))!;
        Assert.Equal(3, mine.Count);
        Assert.All(mine, r => Assert.Equal("ปิติ สมมุติ", r.EmployeeName));
    }

    [Fact]
    public async Task Cancel_OwnPending_Works()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var pending = (await c.GetFromJsonAsync<List<RequestBody>>("/api/requests"))!.Single(r => r.Status == "Pending");
        var res = await c.PostAsync($"/api/requests/{pending.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Cancelled", (await res.Content.ReadFromJsonAsync<RequestBody>())!.Status);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesRequest_Is403()
    {
        using var f = new ApiFactory();
        var chujai = await f.LoginAs(Chujai);
        var theirs = (await chujai.GetFromJsonAsync<List<RequestBody>>("/api/requests"))!.First();
        var piti = await f.LoginAs(Piti);
        Assert.Equal(HttpStatusCode.Forbidden, (await piti.PostAsync($"/api/requests/{theirs.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Cancel_Unknown_Is404()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/requests/99999/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Holidays_ForYear()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        var list = await c.GetFromJsonAsync<List<HolidayBody>>("/api/holidays?year=2026");
        Assert.Contains(list!, h => h.Date == new DateOnly(2026, 12, 10));
    }

    [Fact]
    public async Task Requests_RequireLogin() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await new ApiFactory().CreateClient().GetAsync("/api/requests")).StatusCode);
}

public record HolidayBody(DateOnly Date, string NameTh);
