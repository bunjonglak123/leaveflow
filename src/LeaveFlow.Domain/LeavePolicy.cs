namespace LeaveFlow.Domain;

public record LeaveBalance(int LeaveTypeId, decimal Quota, decimal Used, decimal Pending)
{
    public decimal Remaining => Quota - Used - Pending;
}

public static class LeavePolicy
{
    /// <summary>Balance of one leave type for one employee in <paramref name="year"/>.</summary>
    public static LeaveBalance Balance(int leaveTypeId, decimal quota, int year, IEnumerable<LeaveRequest> employeeRequests)
    {
        var ofType = employeeRequests.Where(r => r.LeaveTypeId == leaveTypeId && r.Start.Year == year).ToList();
        return new LeaveBalance(
            leaveTypeId,
            quota,
            ofType.Where(r => r.Status == LeaveStatus.Approved).Sum(r => r.Days),
            ofType.Where(r => r.Status == LeaveStatus.Pending).Sum(r => r.Days));
    }

    /// <summary>Throws <see cref="DomainException"/> if the employee may not submit <paramref name="candidate"/>.</summary>
    public static void EnsureCanRequest(LeaveRequest candidate, IEnumerable<LeaveRequest> employeeRequests, decimal quota)
    {
        if (candidate.Days <= 0) throw new DomainException("ช่วงที่เลือกไม่มีวันทำงาน (ตรงกับวันหยุดทั้งหมด)");

        var existing = employeeRequests.Where(r => r.IsActive && !ReferenceEquals(r, candidate)).ToList();
        if (existing.Any(candidate.Overlaps)) throw new DomainException("ช่วงวันลาทับกับคำขอลาที่มีอยู่แล้ว");

        var balance = Balance(candidate.LeaveTypeId, quota, candidate.Start.Year, existing);
        if (candidate.Days > balance.Remaining)
        {
            throw new DomainException($"วันลาคงเหลือไม่พอ (เหลือ {balance.Remaining:0.#} วัน รวมคำขอที่รออนุมัติ)");
        }
    }
}
