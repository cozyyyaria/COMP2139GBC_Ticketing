using Microsoft.AspNetCore.Identity;

namespace GBC_Ticketing.Web.Models;

public class ApplicationUser : IdentityUser 
{
    public string? FullName { get; set; }
    public string? ProfileImagePath { get; set; }
}
