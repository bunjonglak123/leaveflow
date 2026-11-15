using LeaveFlow.Api.Data;
using LeaveFlow.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Tests;

public class SeedTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void Seed_CreatesDemoOrganisation()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);

        DemoSeed.Run(db, new DateOnly(2026, 11, 16));

        Assert.Equal(3, db.LeaveTypes.Count());
        Assert.Contains(db.LeaveTypes, t => t.NameTh == "ลาพักร้อน" && t.YearlyQuota == 6);
        Assert.Equal(1, db.Employees.Count(e => e.Role == Role.HrAdmin));
        Assert.True(db.Employees.Count(e => e.Role == Role.Manager) >= 2);
        Assert.True(db.Employees.Count(e => e.Role == Role.Employee) >= 6);
        Assert.All(db.Employees.Where(e => e.Role == Role.Employee), e => Assert.NotNull(e.ManagerId));
        Assert.True(db.Holidays.Count() >= 10);
        Assert.Contains(db.LeaveRequests, r => r.Status == LeaveStatus.Pending);
        Assert.Contains(db.LeaveRequests, r => r.Status == LeaveStatus.Approved);
        Assert.Equal(db.LeaveRequests.Count(), db.AuditEntries.Count(a => a.Action == AuditAction.Created));
    }

    [Fact]
    public void Seed_IsIdempotentWhenDataExists()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);
        DemoSeed.Run(db, new DateOnly(2026, 11, 16));
        var count = db.Employees.Count();

        DemoSeed.Run(db, new DateOnly(2026, 11, 16));

        Assert.Equal(count, db.Employees.Count());
    }

    [Fact]
    public void SeededRequests_RespectPolicy()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = NewDb(conn);
        DemoSeed.Run(db, new DateOnly(2026, 11, 16));

        foreach (var emp in db.Employees.ToList())
        {
            var mine = db.LeaveRequests.Where(r => r.EmployeeId == emp.Id).ToList();
            foreach (var type in db.LeaveTypes.ToList())
            {
                Assert.True(LeavePolicy.Balance(type.Id, type.YearlyQuota, 2026, mine).Remaining >= 0,
                    $"{emp.Name} over quota on {type.NameTh}");
            }
        }
    }
}
