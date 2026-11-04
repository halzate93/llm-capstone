using Microsoft.AspNetCore.Identity;

namespace LogiTrack.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? FulfillmentCenter { get; set; }
}
