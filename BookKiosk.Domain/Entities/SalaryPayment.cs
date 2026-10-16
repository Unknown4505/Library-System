using BookKiosk.Domain.Enums;

namespace BookKiosk.Domain.Entities;

public class SalaryPayment : BaseEntity
{
    public int SalaryPaymentId { get; set; }
    public int EmployeeId { get; set; }
    public int PayMonth { get; set; }
    public int PayYear { get; set; }
    public decimal BaseSalarySnapshot { get; set; }
    public decimal AllowanceAmount { get; set; }
    public decimal DeductionAmount { get; set; }
    public decimal NetSalary { get; set; }
    public SalaryPaymentStatus Status { get; set; } = SalaryPaymentStatus.Draft;
    public int? PaidByUserId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }

    public Employee? Employee { get; set; }
    public User? PaidByUser { get; set; }
}
