using System.Globalization;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Communication;
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
        AddApplicationOptions(services, configuration);
        AddAccountVerification(services, configuration);
        AddEmail(services, configuration);
        AddApplicationServices(services);
        return services;
    }

    private static void AddApplicationOptions(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        var baseUrl = configuration["App:BaseUrl"];
        services.AddSingleton(
            new ApplicationOptions
            {
                BaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                    ? ApplicationOptions.DefaultBaseUrl
                    : baseUrl,
            }
        );
    }

    private static bool IsVerificationRequired(IConfiguration configuration)
    {
        return !bool.TryParse(configuration["AccountVerification:Required"], out var required)
            || required;
    }

    private static void AddAccountVerification(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options = new AccountVerificationOptions
        {
            Required = IsVerificationRequired(configuration),
            OtpLifetime = ReadTimeSpan(
                configuration["AccountVerification:OtpLifetimeMinutes"],
                TimeSpan.FromMinutes,
                AccountVerificationOptions.DefaultOtpLifetime
            ),
            ResendCooldown = ReadTimeSpan(
                configuration["AccountVerification:ResendCooldownSeconds"],
                TimeSpan.FromSeconds,
                AccountVerificationOptions.DefaultResendCooldown
            ),
        };
        services.AddSingleton(options);
    }

    private static TimeSpan ReadTimeSpan(
        string? value,
        Func<double, TimeSpan> convert,
        TimeSpan fallback
    )
    {
        if (
            !double.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed <= 0
        )
            return fallback;

        // A finite but huge value overflows TimeSpan.From* — fall back rather than crash at startup.
        try
        {
            return convert(parsed);
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        var options = new SmtpOptions
        {
            Host = configuration["Smtp:Host"] ?? string.Empty,
            Port =
                int.TryParse(configuration["Smtp:Port"], CultureInfo.InvariantCulture, out var port)
                && port > 0
                    ? port
                    : SmtpOptions.DefaultPort,
            Security = Enum.TryParse<SmtpSecurityMode>(
                configuration["Smtp:Security"],
                ignoreCase: true,
                out var security
            )
                ? security
                : SmtpSecurityMode.StartTls,
            Username = configuration["Smtp:Username"] ?? string.Empty,
            Password = configuration["Smtp:Password"] ?? string.Empty,
            FromAddress = configuration["Smtp:FromAddress"] ?? string.Empty,
            FromName = configuration["Smtp:FromName"] ?? "Código Activo",
        };

        // Fail fast on a misconfigured deployment: if account verification is required but no SMTP
        // server is set, registrations would persist yet never be able to send a code, silently
        // stranding every new user. Development turns verification off, so this does not fire there.
        if (
            IsVerificationRequired(configuration)
            && (
                string.IsNullOrWhiteSpace(options.Host)
                || string.IsNullOrWhiteSpace(options.FromAddress)
            )
        )
            throw new InvalidOperationException(
                "SMTP is not configured (Smtp:Host and Smtp:FromAddress are required) while "
                    + "AccountVerification:Required is true. Configure SMTP or disable verification."
            );

        services.AddSingleton(options);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
    }

    private static void AddClock(IServiceCollection services, IConfiguration configuration)
    {
        var timeZone = ResolveTimeZone(configuration["App:TimeZone"]);
        services.AddSingleton<IClock>(new SystemClock(timeZone));
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TimeZoneInfo.Local;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var direct))
            return direct;

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
                )
                && maxSize > 0
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
