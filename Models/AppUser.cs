using Microsoft.AspNetCore.Identity;

namespace backend.Models;

public class AppUser : IdentityUser
{
    public int? BusinessId { get; set; }
    public Business? Business { get; set; }
    public bool IsAdmin { get; set; } = false;
    public bool IsSuperAdmin { get; set; } = false;
}
