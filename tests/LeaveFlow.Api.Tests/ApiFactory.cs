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

    /// <summary>
    /// Set LEAVEFLOW_TEST_SQLSERVER to a SQL Server connection string (no Database=) to run every
    /// API test against real SQL Server through EF migrations instead of SQLite. Used by CI.
    /// </summary>
    private static readonly string? SqlServerBase = Environment.GetEnvironmentVariable("LEAVEFLOW_TEST_SQLSERVER");
    private readonly string _sqlServerDb = "lf_test_" + Guid.NewGuid().ToString("N")[..12];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (SqlServerBase is { Length: > 0 })
        {
            builder.UseSetting("Database:Provider", "SqlServer");
            builder.UseSetting("ConnectionStrings:Default", $"{SqlServerBase.TrimEnd(';')};Database={_sqlServerDb}");
        }
        else
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:Default", "DataSource=" + _dbName);
        }
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

    private bool _dropped;

    /// <summary>Drops the per-factory SQL Server database. Must run before the host is disposed.</summary>
    private void DropSqlServerDb()
    {
        if (_dropped || SqlServerBase is not { Length: > 0 }) return;
        _dropped = true;
        using var conn = new Microsoft.Data.SqlClient.SqlConnection($"{SqlServerBase.TrimEnd(';')};Database=master");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_sqlServerDb}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{_sqlServerDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_sqlServerDb}]; END";
        cmd.ExecuteNonQuery();
    }

    public override async ValueTask DisposeAsync()
    {
        DropSqlServerDb();
        await base.DisposeAsync();
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DropSqlServerDb();
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }

    public record UserBody(int Id, string Name, string Email, string Role, string Department);
    public record LoginBody(string Token, UserBody User);
}
