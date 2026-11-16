using System.Net;
using System.Net.Http.Json;

namespace LeaveFlow.Api.Tests;

/// <summary>Demo login disabled, so password login is the only way in (like a real install).</summary>
public class PasswordFactory : ApiFactory
{
    public PasswordFactory() => DemoEnabled = false;
}

public class PasswordTests : IClassFixture<PasswordFactory>
{
    private readonly PasswordFactory _f;
    public PasswordTests(PasswordFactory f) => _f = f;

    private async Task<HttpStatusCode> LoginStatus(string email, string password) =>
        (await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode;

    [Fact]
    public async Task ChangeOwnPassword_NewWorksOldDoesNot()
    {
        var c = await _f.LoginAs("chujai@leaveflow.demo");
        var res = await c.PostAsJsonAsync("/api/me/password", new { currentPassword = "demo1234", newPassword = "brandnew99" });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginStatus("chujai@leaveflow.demo", "brandnew99"));
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus("chujai@leaveflow.demo", "demo1234"));
    }

    [Fact]
    public async Task ChangeOwnPassword_WrongCurrent_Is400()
    {
        var c = await _f.LoginAs("mana@leaveflow.demo");
        var res = await c.PostAsJsonAsync("/api/me/password", new { currentPassword = "nope", newPassword = "brandnew99" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("รหัสผ่านเดิมไม่ถูกต้อง", await res.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, await LoginStatus("mana@leaveflow.demo", "demo1234"));
    }

    [Fact]
    public async Task ChangeOwnPassword_TooShort_Is400()
    {
        var c = await _f.LoginAs("weera@leaveflow.demo");
        var res = await c.PostAsJsonAsync("/api/me/password", new { currentPassword = "demo1234", newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("อย่างน้อย 8", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangeOwnPassword_WithoutToken_Is401()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/me/password", new { currentPassword = "demo1234", newPassword = "brandnew99" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task HrResetsPassword()
    {
        var hr = await _f.LoginAs("hr@leaveflow.demo");
        var employees = await hr.GetFromJsonAsync<List<ApiFactory.UserBody>>("/api/hr/employees");
        var suda = employees!.Single(e => e.Email == "suda@leaveflow.demo");

        var res = await hr.PostAsJsonAsync($"/api/hr/employees/{suda.Id}/password", new { newPassword = "fromhr1234" });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginStatus("suda@leaveflow.demo", "fromhr1234"));
    }

    [Fact]
    public async Task HrReset_TooShort_Is400_UnknownId_Is404()
    {
        var hr = await _f.LoginAs("hr@leaveflow.demo");
        var shortRes = await hr.PostAsJsonAsync("/api/hr/employees/1/password", new { newPassword = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, shortRes.StatusCode);
        var missing = await hr.PostAsJsonAsync("/api/hr/employees/99999/password", new { newPassword = "fromhr1234" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ManagerCannotResetPasswords()
    {
        var m = await _f.LoginAs("manager@leaveflow.demo");
        var res = await m.PostAsJsonAsync("/api/hr/employees/1/password", new { newPassword = "fromhr1234" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}

/// <summary>On the public demo nobody may change passwords, or the next visitor gets locked out.</summary>
public class DemoPasswordTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public DemoPasswordTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task DemoMode_BlocksPasswordChanges()
    {
        var hr = await _f.LoginAs("hr@leaveflow.demo");
        var own = await hr.PostAsJsonAsync("/api/me/password", new { currentPassword = "demo1234", newPassword = "brandnew99" });
        var reset = await hr.PostAsJsonAsync("/api/hr/employees/2/password", new { newPassword = "brandnew99" });

        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);
        Assert.Contains("เดโม", await own.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }
}
