namespace BookKiosk.Domain.Entities;

public class Employee : BaseEntity
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string Position { get; set; } = string.Empty;
    public DateTime? HireDate { get; set; }
    public decimal? CurrentBaseSalary { get; set; }
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
    public ICollection<SalaryPayment> SalaryPayments { get; set; } = new List<SalaryPayment>();
}
