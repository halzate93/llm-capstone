using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    // POST: /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existingByName = await _userManager.FindByNameAsync(request.Username);
        if (existingByName is not null)
        {
            return Conflict(new { message = $"Username '{request.Username}' is already taken." });
        }

        var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingByEmail is not null)
        {
            return Conflict(new { message = $"Email '{request.Email}' is already registered." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            FulfillmentCenter = request.FulfillmentCenter
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "User registration failed.",
                errors = createResult.Errors.Select(e => e.Description)
            });
        }

        var targetRole = string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role.Trim();
        if (!await _roleManager.RoleExistsAsync(targetRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(targetRole));
        }

        await _userManager.AddToRoleAsync(user, targetRole);

        return Ok(new
        {
            message = "User registered successfully.",
            username = user.UserName,
            email = user.Email,
            role = targetRole
        });
    }

    // POST: /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByNameAsync(request.Username)
                   ?? await _userManager.FindByEmailAsync(request.Username);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new { message = "Account is temporarily locked due to failed login attempts." });
        }

        if (!signInResult.Succeeded)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles, out var expiration);

        return Ok(new AuthResponse
        {
            Token = token,
            Expiration = expiration,
            Username = user.UserName ?? string.Empty,
            Roles = roles
        });
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles, out DateTime expiration)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "LogiTrack_SuperSecret_SigningKey_2026_AtLeast32Bytes!";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "LogiTrackApi";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "LogiTrackClients";
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var mins) ? mins : 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        expiration = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
