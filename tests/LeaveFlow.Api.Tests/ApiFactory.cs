using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LeaveFlow.Api.Tests;

/// <summary>Runs the real API against a private in-memory SQLite DB seeded with the demo org on 16 Nov 2026 (Mon).</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Today = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(7));
    private readonly string _dbName = "file:" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;

    public bool DemoEnabled { get; init; } = true;
    public FakeTimeProvider Clock { get; } = new(Today);

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection("DataSource=" + _dbName);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", "DataSource=" + _dbName);
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Demo:Enabled", DemoEnabled ? "true" : "false");
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-long-enough-for-hs256!!");
        builder.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Clock));
    }

    public async Task<HttpClient> LoginAs(string email)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "demo1234" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }

    public record UserBody(int Id, string Name, string Email, string Role, string Department);
    public record LoginBody(string Token, UserBody User);
}
