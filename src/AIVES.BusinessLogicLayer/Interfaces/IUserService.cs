using AIVES.BusinessLogicLayer.Common;
namespace AIVES.BusinessLogicLayer.Interfaces;
public interface IUserService
{
    Task<ServiceResult<AuthUser>> RegisterAsync(RegisterRequest request);
    Task<ServiceResult<AuthUser>> LoginAsync(string email, string password);
    Task<AuthUser?> GetIdentityAsync(long userId);
}