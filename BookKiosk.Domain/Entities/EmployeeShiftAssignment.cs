namespace BookKiosk.Domain.Entities;

public class EmployeeShiftAssignment : BaseEntity
{
    public int AssignmentId { get; set; }
    public int WorkShiftId { get; set; }
    public int EmployeeId { get; set; }
    public int? AssignedKioskId { get; set; }
    public decimal? WorkedHours { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public WorkShift WorkShift { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public Kiosk? AssignedKiosk { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
