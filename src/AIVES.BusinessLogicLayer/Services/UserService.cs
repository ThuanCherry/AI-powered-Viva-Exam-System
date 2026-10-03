using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.Extensions.Logging;

namespace AIVES.BusinessLogicLayer.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<User>> GetByIdAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ServiceResult<User>.FailureResult("User not found.");

        return ServiceResult<User>.SuccessResult(user);
    }

    public async Task<ServiceResult<IEnumerable<User>>> GetAllAsync()
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        return ServiceResult<IEnumerable<User>>.SuccessResult(users);
    }

    public async Task<ServiceResult<User>> CreateAsync(User user, string plainPassword)
    {
        var exists = await _unitOfWork.Users.ExistsAsync(u => u.Email == user.Email);
        if (exists)
            return ServiceResult<User>.FailureResult("Email already registered.");

        user.PasswordHash = HashPassword(plainPassword);
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User created: {Email}, Role: {Role}", user.Email, user.Role);
        return ServiceResult<User>.SuccessResult(user, "User created successfully.");
    }

    public async Task<ServiceResult<LoginResult>> LoginAsync(string email, string password)
    {
        var users = await _unitOfWork.Users.FindAsync(u => u.Email == email);
        var user = users.FirstOrDefault();

        if (user == null || !VerifyPassword(password, user.PasswordHash))
            return ServiceResult<LoginResult>.FailureResult("Invalid email or password.");

        if (!user.IsActive)
            return ServiceResult<LoginResult>.FailureResult("Account is deactivated.");

        var response = new LoginResult
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            Token = GenerateToken(user)
        };

        _logger.LogInformation("User logged in: {Email}", user.Email);
        return ServiceResult<LoginResult>.SuccessResult(response, "Login successful.");
    }

    public async Task<ServiceResult<bool>> DeactivateAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ServiceResult<bool>.FailureResult("User not found.");

        user.IsActive = false;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User deactivated: {UserId}", userId);
        return ServiceResult<bool>.SuccessResult(true, "User deactivated.");
    }

    // ===== Private Helper Methods =====
    private static string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    private static string GenerateToken(User user)
    {
        return $"token_{user.UserId}_{DateTime.UtcNow.Ticks}";
    }
}
