using LeaveFlow.Domain;
using Microsoft.AspNetCore.Identity;

namespace LeaveFlow.Api.Data;

/// <summary>
/// Real installs (no demo seed): on an empty database, create the first HR admin from
/// <c>Setup:AdminEmail</c> / <c>Setup:AdminPassword</c> / <c>Setup:AdminName</c>, plus the
/// Thai labour-law leave types and fixed public holidays for this year and next.
/// </summary>
public static class FirstRunSetup
{
    public const string DefaultAdminName = "ผู้ดูแลระบบ";

    /// <returns>true when the admin was created; false when the database already has people or nothing is configured.</returns>
    /// <exception cref="InvalidOperationException">Configured values are invalid, so startup fails with a clear message.</exception>
    public static bool Run(AppDbContext db, string? adminEmail, string? adminPassword, string? adminName, DateOnly today)
    {
        if (db.Employees.Any()) return false;
        if (string.IsNullOrWhiteSpace(adminEmail) && string.IsNullOrEmpty(adminPassword)) return false;

        var email = (adminEmail ?? "").Trim().ToLowerInvariant();
        if (!email.Contains('@')) throw new InvalidOperationException("Setup:AdminEmail must be a valid email address.");
        if ((adminPassword ?? "").Length < 8) throw new InvalidOperationException("Setup:AdminPassword must be at least 8 characters.");

        if (!db.LeaveTypes.Any()) db.LeaveTypes.AddRange(DefaultLeaveTypes());
        var existing = db.Holidays.Select(h => h.Date).ToHashSet();
        db.Holidays.AddRange(DemoSeed.FixedThaiHolidays(today.Year)
            .Concat(DemoSeed.FixedThaiHolidays(today.Year + 1))
            .Where(h => !existing.Contains(h.Date)));

        var admin = new Employee
        {
            Name = string.IsNullOrWhiteSpace(adminName) ? DefaultAdminName : adminName.Trim(),
            Email = email, Department = "ฝ่ายบุคคล", Role = Role.HrAdmin, StartDate = today,
        };
        admin.PasswordHash = new PasswordHasher<Employee>().HashPassword(admin, adminPassword!);
        db.Employees.Add(admin);
        db.SaveChanges();
        return true;
    }

    /// <summary>Thai Labour Protection Act minimums: vacation 6, sick 30, personal business 3 days/year.</summary>
    public static IEnumerable<LeaveType> DefaultLeaveTypes() =>
    [
        new() { NameTh = "ลาพักร้อน", YearlyQuota = 6 },
        new() { NameTh = "ลาป่วย", YearlyQuota = 30 },
        new() { NameTh = "ลากิจ", YearlyQuota = 3 },
    ];
}
