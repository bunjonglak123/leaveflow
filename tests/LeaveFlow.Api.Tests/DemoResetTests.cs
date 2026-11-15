using System.Net.Http.Json;
using LeaveFlow.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveFlow.Api.Tests;

public class DemoResetTests
{
    [Fact]
    public async Task Reset_WipesVisitorChangesAndReseeds()
    {
        using var f = new ApiFactory();
        var hr = await f.LoginAs("hr@leaveflow.demo");
        await hr.PostAsJsonAsync("/api/hr/leave-types", new { nameTh = "ลาที่ผู้เยี่ยมชมเพิ่ม", yearlyQuota = 1 });

        f.Clock.Advance(TimeSpan.FromDays(1));
        using (var scope = f.Services.CreateScope())
            await DemoReset.RunAsync(scope.ServiceProvider);

        var fresh = await f.LoginAs("hr@leaveflow.demo");
        var types = (await fresh.GetFromJsonAsync<List<LeaveTypeBody>>("/api/hr/leave-types"))!;
        Assert.Equal(3, types.Count);
        Assert.DoesNotContain(types, t => t.NameTh == "ลาที่ผู้เยี่ยมชมเพิ่ม");
    }

    [Theory]
    [InlineData("2026-11-16T23:59:00+07:00", false)]
    [InlineData("2026-11-17T00:01:00+07:00", true)]
    public void IsDue_WhenLocalDateChanged(string now, bool due) =>
        Assert.Equal(due, DemoReset.IsDue(new DateOnly(2026, 11, 16), DateTimeOffset.Parse(now)));
}
