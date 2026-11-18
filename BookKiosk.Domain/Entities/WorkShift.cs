using BookKiosk.Domain.Enums;

namespace BookKiosk.Domain.Entities;

public class WorkShift : BaseEntity
{
    public int WorkShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public WorkShiftStatus Status { get; set; } = WorkShiftStatus.Open;
    public DateTime? FinalizedAt { get; set; }
    public int? FinalizedByUserId { get; set; }
    public User? FinalizedByUser { get; set; }
    public ICollection<EmployeeShiftAssignment> Assignments { get; set; } = new List<EmployeeShiftAssignment>();
}
