namespace LeaveFlow.Domain;

public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; private set; }
    public int LeaveTypeId { get; private set; }
    public DateOnly Start { get; private set; }
    public DateOnly End { get; private set; }
    public HalfDay Half { get; private set; }
    public decimal Days { get; private set; }
    public string Reason { get; private set; } = "";
    public LeaveStatus Status { get; private set; } = LeaveStatus.Pending;
    public int? DecidedById { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }

    private LeaveRequest() { } // EF Core

    public static LeaveRequest Create(int employeeId, int leaveTypeId, DateOnly start, DateOnly end,
        HalfDay half, decimal days, string reason, DateTime now) => new()
    {
        EmployeeId = employeeId,
        LeaveTypeId = leaveTypeId,
        Start = start,
        End = end,
        Half = half,
        Days = days,
        Reason = reason.Trim(),
        CreatedAt = now,
    };

    public bool IsActive => Status is LeaveStatus.Pending or LeaveStatus.Approved;

    public void Approve(int approverId, string? note, DateTime now)
    {
        EnsureDecidable(approverId);
        Decide(LeaveStatus.Approved, approverId, note, now);
    }

    public void Reject(int approverId, string? note, DateTime now)
    {
        EnsureDecidable(approverId);
        if (string.IsNullOrWhiteSpace(note)) throw new DomainException("กรุณาระบุเหตุผลที่ไม่อนุมัติ");
        Decide(LeaveStatus.Rejected, approverId, note, now);
    }

    public void Cancel(int byId, bool isHr, DateTime now)
    {
        var allowed = Status switch
        {
            LeaveStatus.Pending => byId == EmployeeId || isHr,
            LeaveStatus.Approved => isHr,
            _ => false,
        };
        if (!allowed)
        {
            throw new DomainException(Status == LeaveStatus.Approved
                ? "คำขอที่อนุมัติแล้ว ต้องให้ฝ่ายบุคคลเป็นผู้ยกเลิก"
                : "ยกเลิกคำขอนี้ไม่ได้");
        }
        Status = LeaveStatus.Cancelled;
        DecidedById = byId;
        DecidedAt = now;
    }

    /// <summary>True when the two requests claim the same working time (morning and afternoon of one day do not clash).</summary>
    public bool Overlaps(LeaveRequest other)
    {
        if (Start > other.End || other.Start > End) return false;
        var bothHalfSameDay = Half != HalfDay.None && other.Half != HalfDay.None && Start == other.Start;
        return !bothHalfSameDay || Half == other.Half;
    }

    private void EnsureDecidable(int approverId)
    {
        if (Status != LeaveStatus.Pending) throw new DomainException("คำขอนี้ถูกพิจารณาไปแล้ว");
        if (approverId == EmployeeId) throw new DomainException("อนุมัติคำขอของตัวเองไม่ได้");
    }

    private void Decide(LeaveStatus status, int approverId, string? note, DateTime now)
    {
        Status = status;
        DecidedById = approverId;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedAt = now;
    }
}
