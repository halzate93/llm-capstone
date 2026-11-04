using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

public class RegisterRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(100)]
    public string? FulfillmentCenter { get; set; }

    /// <summary>
    /// Optional role to assign on registration (e.g., "Manager" or "User"). Defaults to "User".
    /// </summary>
    public string? Role { get; set; }
}

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public string Username { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
}
