using System.Globalization;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Storage;
using CodigoActivo.Infrastructure.Time;
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
        AddClock(services, configuration);
        AddApplicationServices(services);
        return services;
    }

    private static void AddClock(IServiceCollection services, IConfiguration configuration)
    {
        // "App:TimeZone" is an IANA (e.g. "Europe/Madrid") or Windows (e.g. "Romance Standard Time")
        // id; unset falls back to the server's local timezone. Determines the date used for the
        // upcoming/past boundary reads.
        var timeZone = ResolveTimeZone(configuration["App:TimeZone"]);
        services.AddSingleton<IClock>(new SystemClock(timeZone));
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var direct)) return direct;

        // The configured id may use the "other" convention for this host (an IANA id on a Windows
        // box without ICU, or a Windows id on Linux); convert and retry before giving up.
        if (
            TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out var viaWindows)
        )
            return viaWindows;

        if (
            TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var viaIana)
        )
            return viaIana;

        return TimeZoneInfo.Local;
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
        services.AddSingleton<IQueryExecutor, QueryExecutor>();
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
        services.AddScoped<IEventCategoryTypeRepository, EventCategoryTypeRepository>();
        services.AddScoped<IActivityModalityTypeRepository, ActivityModalityTypeRepository>();
    }

    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var storageOptions = new FileStorageOptions
        {
            RootPath = configuration["FileStorage:RootPath"] ?? "files",
            MaxSizeBytes =
                long.TryParse(
                    configuration["FileStorage:MaxSizeBytes"],
                    CultureInfo.InvariantCulture,
                    out var maxSize
                ) && maxSize > 0
                    ? maxSize
                    : FileStorageOptions.DefaultMaxSizeBytes,
        };
        services.AddSingleton(storageOptions);
        services.AddSingleton<ILocalFileSystemRepository, LocalFileSystemRepository>();
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