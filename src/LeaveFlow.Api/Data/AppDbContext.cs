using LeaveFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Employee>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Department).HasMaxLength(100);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LeaveType>(e =>
        {
            e.Property(x => x.NameTh).HasMaxLength(50).IsRequired();
            e.Property(x => x.YearlyQuota).HasPrecision(5, 1);
        });

        b.Entity<Holiday>(e =>
        {
            e.HasKey(x => x.Date);
            e.Property(x => x.NameTh).HasMaxLength(100).IsRequired();
        });

        b.Entity<LeaveRequest>(e =>
        {
            e.Property(x => x.Days).HasPrecision(5, 1);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.DecisionNote).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Half).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.EmployeeId, x.Start });
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => x.RequestId);
            e.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
