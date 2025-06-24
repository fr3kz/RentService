using Microsoft.AspNetCore.Identity;

namespace RentService.Models;

public class User : IdentityUser
{
    public string Imie { get; set; } = string.Empty;
    public string Nazwisko { get; set; } = string.Empty;    
    public bool IsAdmin  { get; set; } = false;
    
}