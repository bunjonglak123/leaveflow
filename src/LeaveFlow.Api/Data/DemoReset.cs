using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Data;

/// <summary>
/// Public demo only (<c>Demo:Enabled</c>): wipes everything visitors changed and reseeds the fake
/// organisation once per local day, so dates stay current and nobody can break the demo for others.
/// </summary>
public class DemoReset(IServiceProvider services, TimeProvider clock, ILogger<DemoReset> log) : BackgroundService
{
    public static bool IsDue(DateOnly lastReset, DateTimeOffset localNow) => DateOnly.FromDateTime(localNow.DateTime) > lastReset;

    public static async Task RunAsync(IServiceProvider scoped)
    {
        var db = scoped.GetRequiredService<AppDbContext>();
        var clock = scoped.GetRequiredService<TimeProvider>();
        // Keep the schema; clear rows child-first (works for SQL Server and SQLite alike).
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.AuditEntries.ExecuteDeleteAsync();
            await db.LeaveRequests.ExecuteDeleteAsync();
            await db.Employees.ExecuteUpdateAsync(s => s.SetProperty(e => e.ManagerId, (int?)null));
            await db.Employees.ExecuteDeleteAsync();
            await db.LeaveTypes.ExecuteDeleteAsync();
            await db.Holidays.ExecuteDeleteAsync();
            await tx.CommitAsync();
        }
        db.ChangeTracker.Clear();
        DemoSeed.Run(db, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var last = DateOnly.FromDateTime(clock.GetLocalNow().DateTime); // seeded at startup
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);
        while (await timer.WaitForNextTickAsync(stop))
        {
            if (!IsDue(last, clock.GetLocalNow())) continue;
            try
            {
                using var scope = services.CreateScope();
                await RunAsync(scope.ServiceProvider);
                last = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
                log.LogInformation("Demo data reset for {Date}", last);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Demo reset failed; will retry");
            }
        }
    }
}
