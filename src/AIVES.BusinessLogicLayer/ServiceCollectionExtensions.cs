using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.BusinessLogicLayer.Services;
using AIVES.DataAccessLayer.Data;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using AIVES.DataAccessLayer.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace AIVES.BusinessLogicLayer;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAivesLayers(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection was not found.");
        services.AddDbContext<AivesDbContext>(options => options.UseMySQL(connection));
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<IQuestionAssignmentService, QuestionAssignmentService>();
        services.AddSingleton<IQuestionSelectionStrategy, RandomQuestionSelectionStrategy>();
        services.AddSingleton<IQuestionSelectionStrategy, AdaptiveQuestionSelectionStrategy>();
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddSingleton<IMaterialFileStore>(new LocalMaterialFileStore(configuration.GetValue<string>("MaterialStorage:BasePath")));
        return services;
    }
}