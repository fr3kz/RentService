using System.ComponentModel.DataAnnotations;

namespace RentService.Models.Forms;

public class RegisterModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;
}