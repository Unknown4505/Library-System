using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace BookKiosk.Application.Validation;

/// <summary>Shared Create/Update contact policy. Normalizes without changing subscriber digits.</summary>
public static class SupplierContactPolicy
{
    public static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Số điện thoại không được để trống.");
        if (value.Length > 20)
            throw new ArgumentException("Số điện thoại tối đa 20 ký tự.");
        var phone = value.Trim();
        if (phone.StartsWith("+84", StringComparison.Ordinal)) phone = "0" + phone[3..];
        // Domestic mobile: 10 digits; geographic landline: 11 digits beginning 02.
        if (!Regex.IsMatch(phone, @"\A(?:0[35789][0-9]{8}|02[0-9]{9})\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Số điện thoại phải là số di động Việt Nam 10 chữ số hoặc số cố định 11 chữ số; có thể thay số 0 đầu bằng +84.");
        return phone;
    }

    public static string? NormalizeEmail(string? value)
    {
        if (value is { Length: > 100 })
            throw new ArgumentException("Email tối đa 100 ký tự.");
        if (string.IsNullOrWhiteSpace(value)) return null;
        var email = value.Trim();
        if (email.Any(char.IsControl) || !new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Email không đúng định dạng.");
        return email;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SupplierPhoneAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        try { SupplierContactPolicy.NormalizePhone(value as string); return ValidationResult.Success; }
        catch (ArgumentException ex) { return new ValidationResult(ex.Message); }
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SupplierEmailAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        try { SupplierContactPolicy.NormalizeEmail(value as string); return ValidationResult.Success; }
        catch (ArgumentException ex) { return new ValidationResult(ex.Message); }
    }
}
