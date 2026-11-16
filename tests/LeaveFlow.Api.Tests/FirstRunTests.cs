using System.Net;
using System.Net.Http.Json;
using LeaveFlow.Api.Data;
using LeaveFlow.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Tests;

/// <summary>Real installs: no demo data, first HR admin comes from Setup:* configuration.</summary>
public class FirstRunTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    private static AppDbContext NewDb(SqliteConnection conn)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void FirstRun_CreatesAdminLeaveTypesAndHolidays()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);

        var created = FirstRunSetup.Run(db, "  Boss@Company.co.th ", "secret123", "คุณสมศรี", Today);

        Assert.True(created);
        var admin = Assert.Single(db.Employees);
        Assert.Equal("boss@company.co.th", admin.Email);
        Assert.Equal("คุณสมศรี", admin.Name);
        Assert.Equal(Role.HrAdmin, admin.Role);
        Assert.Null(admin.ManagerId);
        Assert.Equal(new[] { "ลาพักร้อน", "ลาป่วย", "ลากิจ" }, db.LeaveTypes.OrderBy(t => t.Id).Select(t => t.NameTh));
        Assert.Contains(db.Holidays, h => h.Date == new DateOnly(2026, 12, 5));
        Assert.Contains(db.Holidays, h => h.Date == new DateOnly(2027, 1, 1)); // next year ready too
        Assert.Empty(db.LeaveRequests);
    }

    [Fact]
    public void FirstRun_DoesNothingWhenEmployeesExist()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);
        FirstRunSetup.Run(db, "a@b.co", "secret123", null, Today);

        Assert.False(FirstRunSetup.Run(db, "other@b.co", "secret123", null, Today));
        Assert.Single(db.Employees);
        Assert.Equal(3, db.LeaveTypes.Count());
    }

    [Fact]
    public void FirstRun_WithoutConfig_DoesNothing()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);

        Assert.False(FirstRunSetup.Run(db, null, null, null, Today));
        Assert.Empty(db.Employees);
    }

    [Theory]
    [InlineData("not-an-email", "secret123")]
    [InlineData("a@b.co", "short")]
    public void FirstRun_BadConfig_FailsLoudly(string email, string password)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);

        var ex = Assert.Throws<InvalidOperationException>(() => FirstRunSetup.Run(db, email, password, null, Today));
        Assert.Contains("Setup:Admin", ex.Message);
        Assert.Empty(db.Employees);
    }

    [Fact]
    public async Task App_WithSetupConfig_AdminCanLogInAndThereIsNoDemoData()
    {
        await using var f = new ApiFactory
        {
            SeedEnabled = false,
            DemoEnabled = false,
            Settings = new() { ["Setup:AdminEmail"] = "hr@company.co.th", ["Setup:AdminPassword"] = "secret123" },
        };
        var hr = await f.LoginAs("hr@company.co.th", "secret123");

        var employees = await hr.GetFromJsonAsync<List<ApiFactory.UserBody>>("/api/hr/employees");
        var only = Assert.Single(employees!);
        Assert.Equal("ผู้ดูแลระบบ", only.Name);
        var types = await hr.GetFromJsonAsync<List<object>>("/api/hr/leave-types");
        Assert.Equal(3, types!.Count);

        var demo = await f.CreateClient().PostAsJsonAsync("/api/auth/demo", new { role = "HrAdmin" });
        Assert.Equal(HttpStatusCode.NotFound, demo.StatusCode);
        var config = await f.CreateClient().GetFromJsonAsync<Dictionary<string, bool>>("/api/config");
        Assert.False(config!["demo"]);
    }
}
