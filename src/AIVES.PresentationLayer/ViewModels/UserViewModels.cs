using System.ComponentModel.DataAnnotations;
namespace AIVES.PresentationLayer.ViewModels;
public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}
public class RegisterViewModel
{
    [Required, StringLength(255, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; } = "";
    [StringLength(50)] public string? StudentCode { get; set; }
    [Required, StringLength(128, MinimumLength = 8), RegularExpression(@"^(?=.*\p{L})(?=.*\d).{8,128}$", ErrorMessage = "Mật khẩu cần chữ, số và ít nhất 8 ký tự."), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Required, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}