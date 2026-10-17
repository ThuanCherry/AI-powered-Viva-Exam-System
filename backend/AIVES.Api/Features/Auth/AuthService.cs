using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AIVES.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AIVES.Api.Features.Auth;

public class AuthService
{
    private readonly AivesDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AivesDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new LoginResult(false, "Invalid email or password.");

        if (user.Status != "Active")
            return new LoginResult(false, "Account is not active.");

        var token = GenerateJwt(user);

        return new LoginResult(true, null, new UserDto(
            user.UserId,
            user.FullName,
            user.Email,
            user.Role?.RoleName ?? "Unknown"
        ), token);
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return new RegisterResult(false, "Passwords do not match.");

        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
            return new RegisterResult(false, "Email is already registered.");

        // Default role: Student (role_id = 3 based on typical AIVES seed)
        var studentRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Student");
        if (studentRole is null)
            return new RegisterResult(false, "Default role not found. Please seed the database.");

        var user = new Models.User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RoleId = studentRole.RoleId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new RegisterResult(true);
    }

    private string GenerateJwt(Models.User user)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Student")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// DTOs
public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record RegisterRequest(string FullName, string Email, string Password, string ConfirmPassword);
public record UserDto(int UserId, string FullName, string Email, string Role);
public record LoginResult(bool Success, string? Message = null, UserDto? User = null, string? Token = null);
public record RegisterResult(bool Success, string? Message = null);
