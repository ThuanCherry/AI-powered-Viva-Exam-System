using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;

namespace AIVES.BusinessLogicLayer.Interfaces;

public class LoginResult
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public interface IUserService
{
    Task<ServiceResult<User>> GetByIdAsync(int userId);
    Task<ServiceResult<IEnumerable<User>>> GetAllAsync();
    Task<ServiceResult<User>> CreateAsync(User user, string plainPassword);
    Task<ServiceResult<LoginResult>> LoginAsync(string email, string password);
    Task<ServiceResult<bool>> DeactivateAsync(int userId);
}
