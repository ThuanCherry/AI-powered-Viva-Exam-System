using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.BusinessLogicLayer.Services;
using AIVES.DataAccessLayer.Data;
using AIVES.DataAccessLayer.Repositories;
using AIVES.DataAccessLayer.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIVES.BusinessLogicLayer;

/// <summary>
/// Nối tầng - ServiceCollectionExtensions:
/// Extension method đăng ký toàn bộ DI của Business Layer và Data Access Layer,
/// kết nối với Presentation Layer theo đúng sơ đồ kiến trúc 3-Layers.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAivesLayers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Data Access Layer - DbContext (Hỗ trợ MySQL 8.4 / SQL Server)
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AivesDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        // 2. Data Access Layer - File tài liệu (LocalMaterialFileStore - Storage/materials)
        var storagePath = configuration.GetValue<string>("MaterialStorage:BasePath");
        services.AddSingleton<IMaterialFileStore>(new LocalMaterialFileStore(storagePath));

        // 3. Data Access Layer - Repositories & Unit of Work (Query · Command)
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 4. Business Layer - Services (Auth, Course, Exam, Session)
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();

        return services;
    }
}
