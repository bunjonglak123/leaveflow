using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeaveFlow.Api.Data;

public static class DatabaseSetup
{
    /// <summary>Registers <see cref="AppDbContext"/> for <c>Database:Provider</c> = SqlServer (default) or Sqlite.</summary>
    public static IServiceCollection AddLeaveFlowDb(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "SqlServer";
        var cs = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not set");
        services.AddDbContext<AppDbContext>(o =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) o.UseSqlite(cs);
            else o.UseSqlServer(cs);
        });
        return services;
    }

    /// <summary>
    /// While SQL Server is still starting or recovering a database, EF's existence check gets
    /// "cannot open database", decides it doesn't exist and fails on CREATE DATABASE. Wait and retry.
    /// </summary>
    private static void MigrateWithRetry(AppDbContext db, ILogger log, int attempts = 20)
    {
        for (var i = 1; ; i++)
        {
            try
            {
                db.Database.Migrate();
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (i < attempts)
            {
                log.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", i, attempts, ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }
    }

    /// <summary>Creates/migrates the schema and seeds demo data when <c>Seed:Enabled</c> is true.</summary>
    public static void InitialiseDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsSqlServer()) MigrateWithRetry(db, app.Logger);
        else db.Database.EnsureCreated();

        if (app.Configuration.GetValue<bool>("Seed:Enabled"))
        {
            var today = DateOnly.FromDateTime(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);
            DemoSeed.Run(db, today);
        }
    }
}

/// <summary>Lets <c>dotnet ef</c> build SQL Server migrations without running the app.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=LeaveFlow;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
