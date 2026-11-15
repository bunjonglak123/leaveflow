using System.Net;
using System.Net.Http.Json;

namespace LeaveFlow.Api.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public AuthTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Login_WithDemoPassword_ReturnsTokenAndUser()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "employee@leaveflow.demo", password = "demo1234" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiFactory.LoginBody>();
        Assert.False(string.IsNullOrEmpty(body!.Token));
        Assert.Equal("Employee", body.User.Role);
        Assert.Equal("ปิติ สมมุติ", body.User.Name);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "  Employee@LeaveFlow.demo ", password = "demo1234" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Theory]
    [InlineData("employee@leaveflow.demo", "wrong")]
    [InlineData("nobody@leaveflow.demo", "demo1234")]
    public async Task Login_BadCredentials_Is401WithThaiMessage(string email, string password)
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("อีเมลหรือรหัสผ่านไม่ถูกต้อง", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Me_WithoutToken_Is401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/me")).StatusCode);

    [Fact]
    public async Task Me_WithToken_ReturnsUser()
    {
        var client = await _f.LoginAs("manager@leaveflow.demo");
        var me = await client.GetFromJsonAsync<ApiFactory.UserBody>("/api/me");
        Assert.Equal("Manager", me!.Role);
    }

    [Theory]
    [InlineData("Employee", "employee@leaveflow.demo")]
    [InlineData("Manager", "manager@leaveflow.demo")]
    [InlineData("HrAdmin", "hr@leaveflow.demo")]
    public async Task DemoLogin_PicksTheRoleAccount(string role, string email)
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/demo", new { role });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiFactory.LoginBody>();
        Assert.Equal(email, body!.User.Email);
    }

    [Fact]
    public async Task DemoLogin_DisabledOutsideDemo_Is404()
    {
        using var prod = new ApiFactory { DemoEnabled = false };
        var res = await prod.CreateClient().PostAsJsonAsync("/api/auth/demo", new { role = "HrAdmin" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
