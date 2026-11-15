using System.Net;
using System.Net.Http.Json;

namespace LeaveFlow.Api.Tests;

public record CalendarBody(int RequestId, string EmployeeName, string LeaveTypeName, DateOnly Start, DateOnly End, string Half, string Status);
public record AuditBody(string Action, string ActorName, DateTime At, string? Note);

/// <summary>manager@ (ธนา) manages ปิติ, ชูใจ, มานะ, วีระ. hr@ can decide for anyone.</summary>
public class ManagerTests
{
    private const string Mgr = "manager@leaveflow.demo", Hr = "hr@leaveflow.demo", Piti = "employee@leaveflow.demo";

    private static async Task<List<RequestBody>> Queue(HttpClient c) =>
        (await c.GetFromJsonAsync<List<RequestBody>>("/api/team/requests?status=Pending"))!;

    [Fact]
    public async Task Employee_CannotSeeTeamQueue()
    {
        using var f = new ApiFactory();
        var c = await f.LoginAs(Piti);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/team/requests")).StatusCode);
    }

    [Fact]
    public async Task Manager_SeesOnlyDirectReportsPending()
    {
        using var f = new ApiFactory();
        var q = await Queue(await f.LoginAs(Mgr));
        Assert.Equal(["ชูใจ ทดลอง", "ปิติ สมมุติ"], q.Select(r => r.EmployeeName).Order().ToArray());
    }

    [Fact]
    public async Task Hr_SeesEveryonesPendingIncludingManagers()
    {
        using var f = new ApiFactory();
        var q = await Queue(await f.LoginAs(Hr));
        Assert.Contains(q, r => r.EmployeeName == "ธนา ทดสอบ");
        Assert.Contains(q, r => r.EmployeeName == "สุดา ทดสอบ");
    }

    [Fact]
    public async Task Approve_MovesPendingToUsed_AndIsAudited()
    {
        using var f = new ApiFactory();
        var mgr = await f.LoginAs(Mgr);
        var req = (await Queue(mgr)).Single(r => r.EmployeeName == "ปิติ สมมุติ");

        var res = await mgr.PostAsJsonAsync($"/api/team/requests/{req.Id}/approve", new { note = "โอเค" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Approved", (await res.Content.ReadFromJsonAsync<RequestBody>())!.Status);

        var piti = await f.LoginAs(Piti);
        var vac = (await piti.GetFromJsonAsync<List<BalanceBody>>("/api/balances"))!.Single(b => b.NameTh == "ลาพักร้อน");
        Assert.Equal((3m, 0m), (vac.Used, vac.Pending));

        var history = (await piti.GetFromJsonAsync<List<AuditBody>>($"/api/requests/{req.Id}/history"))!;
        Assert.Equal(["Created", "Approved"], history.Select(h => h.Action).ToArray());
        Assert.Equal("ธนา ทดสอบ", history[1].ActorName);
    }

    [Fact]
    public async Task Approve_NotMyReport_Is403()
    {
        using var f = new ApiFactory();
        var suda = (await Queue(await f.LoginAs(Hr))).Single(r => r.EmployeeName == "สุดา ทดสอบ");
        var mgr = await f.LoginAs(Mgr);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await mgr.PostAsJsonAsync($"/api/team/requests/{suda.Id}/approve", new { note = "" })).StatusCode);
    }

    [Fact]
    public async Task Hr_CanApproveManagersRequest()
    {
        using var f = new ApiFactory();
        var hr = await f.LoginAs(Hr);
        var thana = (await Queue(hr)).Single(r => r.EmployeeName == "ธนา ทดสอบ");
        Assert.Equal(HttpStatusCode.OK, (await hr.PostAsJsonAsync($"/api/team/requests/{thana.Id}/approve", new { })).StatusCode);
    }

    [Fact]
    public async Task Reject_NeedsNote()
    {
        using var f = new ApiFactory();
        var mgr = await f.LoginAs(Mgr);
        var req = (await Queue(mgr)).First();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await mgr.PostAsJsonAsync($"/api/team/requests/{req.Id}/reject", new { note = " " })).StatusCode);

        var res = await mgr.PostAsJsonAsync($"/api/team/requests/{req.Id}/reject", new { note = "ทีมไม่พอคน" });
        var body = await res.Content.ReadFromJsonAsync<RequestBody>();
        Assert.Equal(("Rejected", "ทีมไม่พอคน"), (body!.Status, body.DecisionNote));
    }

    [Fact]
    public async Task Approve_Twice_Is400()
    {
        using var f = new ApiFactory();
        var mgr = await f.LoginAs(Mgr);
        var req = (await Queue(mgr)).First();
        await mgr.PostAsJsonAsync($"/api/team/requests/{req.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await mgr.PostAsJsonAsync($"/api/team/requests/{req.Id}/approve", new { })).StatusCode);
    }

    [Fact]
    public async Task Calendar_ShowsActiveLeavesOfMyTeamInMonth()
    {
        using var f = new ApiFactory();
        var cal = (await (await f.LoginAs(Mgr)).GetFromJsonAsync<List<CalendarBody>>("/api/team/calendar?month=2026-11"))!;
        Assert.Contains(cal, x => x.EmployeeName == "มานะ เดโม" && x.Status == "Approved"); // 30 Nov – 2 Dec
        Assert.Contains(cal, x => x.EmployeeName == "ปิติ สมมุติ" && x.Status == "Pending");
        Assert.DoesNotContain(cal, x => x.Status == "Rejected");
        Assert.DoesNotContain(cal, x => x.EmployeeName == "สุดา ทดสอบ");
        Assert.All(cal, x => Assert.True(x.Start <= new DateOnly(2026, 11, 30) && x.End >= new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public async Task Calendar_BadMonth_Is400()
    {
        using var f = new ApiFactory();
        Assert.Equal(HttpStatusCode.BadRequest, (await (await f.LoginAs(Mgr)).GetAsync("/api/team/calendar?month=2026-13")).StatusCode);
    }

    [Fact]
    public async Task History_OfSomeoneElse_Is403ForEmployee()
    {
        using var f = new ApiFactory();
        var chujaiReq = (await Queue(await f.LoginAs(Mgr))).Single(r => r.EmployeeName == "ชูใจ ทดลอง");
        var piti = await f.LoginAs(Piti);
        Assert.Equal(HttpStatusCode.Forbidden, (await piti.GetAsync($"/api/requests/{chujaiReq.Id}/history")).StatusCode);
    }
}
