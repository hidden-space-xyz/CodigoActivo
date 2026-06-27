using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddCodigoActivo(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        AddPersistence(services, configuration);
        AddRepositories(services);
        AddFileStorage(services, configuration);
        AddCaching(services);
        AddApplicationServices(services);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CodigoActivoDbContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("Default"),
                    npgsql =>
                        npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
                )
                .UseSnakeCaseNamingConvention()
        );

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CodigoActivoDbContext>());
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<DatabaseSeeder>();
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IUserTypeRepository, UserTypeRepository>();
        services.AddScoped<IUserStatusTypeRepository, UserStatusTypeRepository>();
        services.AddScoped<IActivityRoleTypeRepository, ActivityRoleTypeRepository>();
        services.AddScoped<IAssignmentStatusTypeRepository, AssignmentStatusTypeRepository>();
    }

    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var storageOptions = new FileStorageOptions
        {
            RootPath = configuration["FileStorage:RootPath"] ?? "files",
            MaxSizeBytes =
                long.TryParse(configuration["FileStorage:MaxSizeBytes"], out var maxSize)
                && maxSize > 0
                    ? maxSize
                    : FileStorageOptions.DefaultMaxSizeBytes,
        };
        services.AddSingleton(storageOptions);
        services.AddSingleton<ILocalFileSystemRepository, LocalFileSystemRepository>();
    }

    private static void AddCaching(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IResponseCacheService, ResponseCacheService>();
    }

    private static void AddApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IPartnerService, PartnerService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IReportService, ReportService>();
    }
}
