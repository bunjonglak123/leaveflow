using LeaveFlow.Domain;

namespace LeaveFlow.Domain.Tests;

public class LeaveRequestTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private const int Emp = 10, Mgr = 20, Hr = 30, Vacation = 1, Sick = 2;

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static LeaveRequest Req(DateOnly s, DateOnly e, decimal days, int type = Vacation, HalfDay half = HalfDay.None, int emp = Emp) =>
        LeaveRequest.Create(emp, type, s, e, half, days, "ธุระส่วนตัว", Now);

    [Fact]
    public void Create_StartsPending()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        Assert.Equal(LeaveStatus.Pending, r.Status);
        Assert.Equal(Now, r.CreatedAt);
    }

    [Fact]
    public void Approve_SetsDecision()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        r.Approve(Mgr, "ok", Now.AddHours(1));
        Assert.Equal(LeaveStatus.Approved, r.Status);
        Assert.Equal(Mgr, r.DecidedById);
        Assert.Equal("ok", r.DecisionNote);
        Assert.Equal(Now.AddHours(1), r.DecidedAt);
    }

    [Fact]
    public void ApproveOwnRequest_Throws()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        Assert.Throws<DomainException>(() => r.Approve(Emp, null, Now));
    }

    [Fact]
    public void ApproveTwice_Throws()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        r.Approve(Mgr, null, Now);
        Assert.Throws<DomainException>(() => r.Approve(Mgr, null, Now));
    }

    [Fact]
    public void AutoApprove_ApprovesWithoutDecider()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        r.AutoApprove("ไม่มีผู้อนุมัติ", Now);
        Assert.Equal(LeaveStatus.Approved, r.Status);
        Assert.Null(r.DecidedById);
        Assert.Throws<DomainException>(() => r.AutoApprove("again", Now));
    }

    [Fact]
    public void Reject_RequiresNote()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        Assert.Throws<DomainException>(() => r.Reject(Mgr, "  ", Now));
        r.Reject(Mgr, "ช่วงนั้นคนไม่พอ", Now);
        Assert.Equal(LeaveStatus.Rejected, r.Status);
    }

    [Fact]
    public void OwnerCanCancelPending()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        r.Cancel(Emp, isHr: false, Now);
        Assert.Equal(LeaveStatus.Cancelled, r.Status);
    }

    [Fact]
    public void OtherEmployeeCannotCancel()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        Assert.Throws<DomainException>(() => r.Cancel(Mgr, isHr: false, Now));
    }

    [Fact]
    public void OnlyHrCanCancelApproved()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        r.Approve(Mgr, null, Now);
        Assert.Throws<DomainException>(() => r.Cancel(Emp, isHr: false, Now));
        r.Cancel(Hr, isHr: true, Now);
        Assert.Equal(LeaveStatus.Cancelled, r.Status);
    }

    [Fact]
    public void RejectedCannotBeCancelled()
    {
        var r = Req(D(11, 18), D(11, 18), 1);
        r.Reject(Mgr, "ไม่ได้", Now);
        Assert.Throws<DomainException>(() => r.Cancel(Hr, isHr: true, Now));
    }

    // ---- policy ----

    [Fact]
    public void Balance_CountsApprovedAndPendingSeparately()
    {
        var approved = Req(D(3, 2), D(3, 3), 2); approved.Approve(Mgr, null, Now);
        var pending = Req(D(11, 18), D(11, 18), 1);
        var rejected = Req(D(4, 1), D(4, 1), 1); rejected.Reject(Mgr, "no", Now);
        var sick = Req(D(5, 1), D(5, 1), 1, type: Sick); sick.Approve(Mgr, null, Now);

        var b = LeavePolicy.Balance(Vacation, 6, 2026, [approved, pending, rejected, sick]);
        Assert.Equal(2m, b.Used);
        Assert.Equal(1m, b.Pending);
        Assert.Equal(3m, b.Remaining);
    }

    [Fact]
    public void Balance_IgnoresOtherYears()
    {
        var old = LeaveRequest.Create(Emp, Vacation, new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 1), HalfDay.None, 1, "x", Now);
        old.Approve(Mgr, null, Now);
        Assert.Equal(0m, LeavePolicy.Balance(Vacation, 6, 2026, [old]).Used);
    }

    [Fact]
    public void EnsureCanRequest_RejectsZeroDays()
    {
        var ex = Assert.Throws<DomainException>(() => LeavePolicy.EnsureCanRequest(Req(D(11, 21), D(11, 22), 0), [], 6));
        Assert.Contains("ไม่มีวันทำงาน", ex.Message);
    }

    [Fact]
    public void EnsureCanRequest_RejectsOverlapWithActive()
    {
        var existing = Req(D(11, 16), D(11, 18), 3);
        Assert.Throws<DomainException>(() => LeavePolicy.EnsureCanRequest(Req(D(11, 18), D(11, 19), 2), [existing], 30));
    }

    [Fact]
    public void EnsureCanRequest_AllowsOverlapWithCancelledOrRejected()
    {
        var cancelled = Req(D(11, 16), D(11, 18), 3); cancelled.Cancel(Emp, false, Now);
        LeavePolicy.EnsureCanRequest(Req(D(11, 18), D(11, 19), 2), [cancelled], 30);
    }

    [Fact]
    public void EnsureCanRequest_AllowsMorningAndAfternoonSameDay()
    {
        var morning = Req(D(11, 18), D(11, 18), 0.5m, half: HalfDay.Morning);
        LeavePolicy.EnsureCanRequest(Req(D(11, 18), D(11, 18), 0.5m, half: HalfDay.Afternoon), [morning], 6);
        Assert.Throws<DomainException>(() =>
            LeavePolicy.EnsureCanRequest(Req(D(11, 18), D(11, 18), 0.5m, half: HalfDay.Morning), [morning], 6));
    }

    [Fact]
    public void EnsureCanRequest_PendingCountsAgainstBalance()
    {
        var pending = Req(D(11, 16), D(11, 20), 5);
        var ex = Assert.Throws<DomainException>(() => LeavePolicy.EnsureCanRequest(Req(D(11, 23), D(11, 24), 2), [pending], 6));
        Assert.Contains("เหลือ 1", ex.Message);
    }

    [Fact]
    public void EnsureCanRequest_OtherTypesDoNotUseBalance()
    {
        var sick = Req(D(11, 16), D(11, 20), 5, type: Sick);
        LeavePolicy.EnsureCanRequest(Req(D(11, 23), D(11, 24), 2), [sick], 6);
    }
}
