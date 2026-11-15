namespace LeaveFlow.Domain;

public enum Role { Employee, Manager, HrAdmin }

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Department { get; set; } = "";
    public Role Role { get; set; } = Role.Employee;
    public int? ManagerId { get; set; }
    public string PasswordHash { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public bool Active { get; set; } = true;
}

public class LeaveType
{
    public int Id { get; set; }
    public string NameTh { get; set; } = "";
    public decimal YearlyQuota { get; set; }
}

public class Holiday
{
    public DateOnly Date { get; set; }
    public string NameTh { get; set; } = "";
}

public enum AuditAction { Created, Approved, Rejected, Cancelled }

public class AuditEntry
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int ActorId { get; set; }
    public AuditAction Action { get; set; }
    public DateTime At { get; set; }
    public string? Note { get; set; }
}
