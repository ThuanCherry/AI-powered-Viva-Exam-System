using AIVES.Api.Features.Auth;
using AIVES.Api.Features.Catalog;
using AIVES.Api.Features.Common;
using AIVES.Api.Features.Exams;
using AIVES.Api.Features.Lecturer;
using AIVES.Api.Features.Workflow;

namespace AIVES.Api.Features;

public static class ApplicationServices
{
    // Shared by production and isolated HTTP tests; does not register AI or workers.
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserManagementService>();
        services.AddScoped<CourseCatalogService>();
        services.AddScoped<ClassQueryService>();
        services.AddScoped<QuestionQueryService>();
        services.AddScoped<ExamQueryService>();
        services.AddScoped<WorkflowService>();
        services.AddScoped<ScheduleNotifications>();
        services.AddScoped<LecturerService>();
        services.AddScoped<DocumentStore>();
        services.AddScoped<VivaEngine>();
        services.AddScoped<DatabaseHealthService>();
        return services;
    }
}
