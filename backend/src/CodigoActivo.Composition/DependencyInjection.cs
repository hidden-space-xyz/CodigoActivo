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
using Npgsql;

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
        AddPasswordReset(services, configuration);
        AddEmail(services, configuration);
        AddApplicationServices(services);
        return services;
    }

    private static void AddApplicationOptions(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        var baseUrl = configuration["APP_BASE_URL"];
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
        return !bool.TryParse(configuration["ACCOUNT_VERIFICATION_REQUIRED"], out var required)
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

    private static void AddPasswordReset(IServiceCollection services, IConfiguration configuration)
    {
        var options = new PasswordResetOptions
        {
            CodeLifetime = ReadTimeSpan(
                configuration["PasswordReset:CodeLifetimeMinutes"],
                TimeSpan.FromMinutes,
                PasswordResetOptions.DefaultCodeLifetime
            ),
            ResendCooldown = ReadTimeSpan(
                configuration["PasswordReset:ResendCooldownSeconds"],
                TimeSpan.FromSeconds,
                PasswordResetOptions.DefaultResendCooldown
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
        {
            return fallback;
        }

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
            Host = configuration["SMTP_HOST"] ?? string.Empty,
            Port =
                int.TryParse(configuration["SMTP_PORT"], CultureInfo.InvariantCulture, out var port)
                && port > 0
                    ? port
                    : SmtpOptions.DefaultPort,
            Security = Enum.TryParse<SmtpSecurityMode>(
                configuration["SMTP_SECURITY"],
                ignoreCase: true,
                out var security
            )
                ? security
                : SmtpSecurityMode.StartTls,
            Username = configuration["SMTP_USERNAME"] ?? string.Empty,
            Password = configuration["SMTP_PASSWORD"] ?? string.Empty,
            FromAddress = configuration["SMTP_FROM_ADDRESS"] ?? string.Empty,
            FromName = configuration["SMTP_FROM_NAME"] ?? "Código Activo",
        };

        if (
            IsVerificationRequired(configuration)
            && (
                string.IsNullOrWhiteSpace(options.Host)
                || string.IsNullOrWhiteSpace(options.FromAddress)
            )
        )
        {
            throw new InvalidOperationException(
                "SMTP is not configured (SMTP_HOST and SMTP_FROM_ADDRESS are required) while "
                    + "ACCOUNT_VERIFICATION_REQUIRED is true. Configure SMTP or disable verification."
            );
        }

        services.AddSingleton(options);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
    }

    private static void AddClock(IServiceCollection services, IConfiguration configuration)
    {
        var timeZone = ResolveTimeZone(configuration["APP_TIMEZONE"]);
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
        {
            return viaWindows;
        }

        return
            TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var viaIana)
            ? viaIana
            : TimeZoneInfo.Local;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CodigoActivoDbContext>(options =>
            options
                .UseNpgsql(
                    BuildConnectionString(configuration),
                    npgsql =>
                        npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
                )
                .UseSnakeCaseNamingConvention()
        );

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CodigoActivoDbContext>());
        services.AddSingleton<IQueryExecutor, QueryExecutor>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DemoDataSeeder>();
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = configuration["POSTGRES_HOST"] ?? "localhost",
            Port = int.TryParse(
                configuration["POSTGRES_PORT"],
                CultureInfo.InvariantCulture,
                out var port
            )
                ? port
                : 5432,
            Database = configuration["POSTGRES_DB"] ?? "codigoactivo",
            Username = configuration["POSTGRES_USER"] ?? "codigoactivo",
            Password = configuration["POSTGRES_PASSWORD"] ?? string.Empty,
        }.ConnectionString;
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IResourceTypeRepository, ResourceTypeRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IUserTypeRepository, UserTypeRepository>();
        services.AddScoped<IUserStatusTypeRepository, UserStatusTypeRepository>();
        services.AddScoped<IActivityRoleTypeRepository, ActivityRoleTypeRepository>();
        services.AddScoped<IAssignmentStatusTypeRepository, AssignmentStatusTypeRepository>();
        services.AddScoped<IEventCategoryTypeRepository, EventCategoryTypeRepository>();
        services.AddScoped<IActivityModalityTypeRepository, ActivityModalityTypeRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
    }

    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var storageOptions = new FileStorageOptions
        {
            RootPath = configuration["FILE_STORAGE_ROOT"] ?? "files",
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
