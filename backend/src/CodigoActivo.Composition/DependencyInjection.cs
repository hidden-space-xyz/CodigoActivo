using System.Globalization;
using CodigoActivo.Application.Announcements.Commands;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.Auth.Queries;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Application.Files.Queries;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Participation.Queries;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Application.Seo.Queries;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
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
    private const long LocalCacheSizeLimitBytes = 64 * 1024 * 1024;
    private const long MaximumCachedPayloadBytes = 1024 * 1024;

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
        AddCaching(services);
        AddApplicationServices(services);
        AddApplicationHandlers(services);
        return services;
    }

    private static void AddCaching(IServiceCollection services)
    {
        services.AddMemoryCache(options => options.SizeLimit = LocalCacheSizeLimitBytes);
        services.AddHybridCache(options => options.MaximumPayloadBytes = MaximumCachedPayloadBytes);
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
        services.AddSingleton<IEmailTransport, SmtpEmailSender>();
        services.AddSingleton(BuildEmailQueueOptions(configuration));
        services.AddSingleton<ChannelEmailDispatcher>();
        services.AddSingleton<IEmailDispatcher>(sp =>
            sp.GetRequiredService<ChannelEmailDispatcher>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<ChannelEmailDispatcher>());
        services.AddSingleton<IEmailSender, ThrottledEmailSender>();
        services.AddSingleton(BuildEmailGuardOptions(configuration));
        services.AddSingleton(
            new ManualEmailOptions
            {
                MaxRecipients = ReadPositiveInt(
                    configuration["ManualEmail:MaxRecipients"],
                    ManualEmailOptions.DefaultMaxRecipients
                ),
                MaxAttachments = ReadPositiveInt(
                    configuration["ManualEmail:MaxAttachments"],
                    ManualEmailOptions.DefaultMaxAttachments
                ),
                MaxAttachmentsBytes = ReadPositiveLong(
                    configuration["ManualEmail:MaxAttachmentsBytes"],
                    ManualEmailOptions.DefaultMaxAttachmentsBytes
                ),
            }
        );
    }

    private static EmailGuardOptions BuildEmailGuardOptions(IConfiguration configuration)
    {
        return new EmailGuardOptions
        {
            RecipientBurst = ReadPositiveInt(
                configuration["EmailGuard:RecipientBurst"],
                EmailGuardOptions.DefaultRecipientBurst
            ),
            RecipientPerHour = ReadPositiveInt(
                configuration["EmailGuard:RecipientPerHour"],
                EmailGuardOptions.DefaultRecipientPerHour
            ),
            RecipientPerDay = ReadPositiveInt(
                configuration["EmailGuard:RecipientPerDay"],
                EmailGuardOptions.DefaultRecipientPerDay
            ),
            GlobalBurst = ReadPositiveInt(
                configuration["EmailGuard:GlobalBurst"],
                EmailGuardOptions.DefaultGlobalBurst
            ),
            GlobalPerHour = ReadPositiveInt(
                configuration["EmailGuard:GlobalPerHour"],
                EmailGuardOptions.DefaultGlobalPerHour
            ),
            GlobalCredentialReserve = ReadPositiveInt(
                configuration["EmailGuard:GlobalCredentialReserve"],
                EmailGuardOptions.DefaultGlobalCredentialReserve
            ),
            MaxTrackedRecipients = ReadPositiveInt(
                configuration["EmailGuard:MaxTrackedRecipients"],
                EmailGuardOptions.DefaultMaxTrackedRecipients
            ),
            SweepInterval = ReadTimeSpan(
                configuration["EmailGuard:SweepIntervalMinutes"],
                TimeSpan.FromMinutes,
                EmailGuardOptions.DefaultSweepInterval
            ),
            AlertInterval = ReadTimeSpan(
                configuration["EmailGuard:AlertIntervalMinutes"],
                TimeSpan.FromMinutes,
                EmailGuardOptions.DefaultAlertInterval
            ),
        };
    }

    private static EmailQueueOptions BuildEmailQueueOptions(IConfiguration configuration)
    {
        return new EmailQueueOptions
        {
            Capacity = ReadPositiveInt(
                configuration["EmailQueue:Capacity"],
                EmailQueueOptions.DefaultCapacity
            ),
            Workers = Math.Min(
                ReadPositiveInt(
                    configuration["EmailQueue:Workers"],
                    EmailQueueOptions.DefaultWorkers
                ),
                EmailQueueOptions.MaxWorkers
            ),
            ShutdownDrain = ReadBoundedTimeSpan(
                configuration["EmailQueue:ShutdownDrainSeconds"],
                EmailQueueOptions.DefaultShutdownDrain,
                EmailQueueOptions.MaxShutdownDrain
            ),
            SendTimeout = ReadBoundedTimeSpan(
                configuration["EmailQueue:SendTimeoutSeconds"],
                EmailQueueOptions.DefaultSendTimeout,
                EmailQueueOptions.MaxSendTimeout
            ),
        };
    }

    private static TimeSpan ReadBoundedTimeSpan(string? value, TimeSpan fallback, TimeSpan max)
    {
        var parsed = ReadTimeSpan(value, TimeSpan.FromSeconds, fallback);
        return parsed > max ? max : parsed;
    }

    private static int ReadPositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static long ReadPositiveLong(string? value, long fallback)
    {
        return long.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static void AddClock(IServiceCollection services, IConfiguration configuration)
    {
        var timeZone = ResolveTimeZone(configuration["APP_TIMEZONE"]);
        services.AddSingleton<IClock>(new SystemClock(timeZone));
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var direct))
        {
            return direct;
        }

        if (
            TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out var viaWindows)
        )
        {
            return viaWindows;
        }

        if (
            TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var viaIana)
        )
        {
            return viaIana;
        }

        return TimeZoneInfo.Local;
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
        services.AddScoped<IEventRatingRepository, EventRatingRepository>();
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
        var uploadOptions = new FileUploadOptions
        {
            MaxSizeBytes =
                long.TryParse(
                    configuration["FileStorage:MaxSizeBytes"],
                    CultureInfo.InvariantCulture,
                    out var maxSize
                )
                && maxSize > 0
                    ? maxSize
                    : FileUploadOptions.DefaultMaxSizeBytes,
        };
        services.AddSingleton(uploadOptions);

        var storageOptions = new FileStorageOptions
        {
            RootPath = configuration["FILE_STORAGE_ROOT"] ?? "files",
        };
        services.AddSingleton(storageOptions);
        services.AddSingleton<ILocalFileSystemRepository, LocalFileSystemRepository>();
    }

    private static void AddApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IOrphanFileCleaner, OrphanFileCleaner>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IEmailService, EmailService>();
    }

    private static void AddApplicationHandlers(IServiceCollection services)
    {
        AddPartnerHandlers(services);
        AddSeoHandlers(services);
        AddAnnouncementHandlers(services);
        AddResourceHandlers(services);
        AddFileHandlers(services);
        AddEventHandlers(services);
        AddParticipationHandlers(services);
        AddUserHandlers(services);
        AddAuthHandlers(services);
    }

    private static void AddPartnerHandlers(IServiceCollection services)
    {
        services.AddScoped<ListPartnersQueryHandler>();
        services.AddScoped<GetPartnerByIdQueryHandler>();
        services.AddScoped<CreatePartnerCommandHandler>();
        services.AddScoped<UpdatePartnerCommandHandler>();
        services.AddScoped<DeletePartnerCommandHandler>();
    }

    private static void AddSeoHandlers(IServiceCollection services)
    {
        services.AddScoped<GetSitemapXmlQueryHandler>();
        services.AddScoped<GetRobotsTxtQueryHandler>();
    }

    private static void AddAnnouncementHandlers(IServiceCollection services)
    {
        services.AddScoped<ListAnnouncementsQueryHandler>();
        services.AddScoped<GetAnnouncementByIdQueryHandler>();
        services.AddScoped<GetAnnouncementYearsQueryHandler>();
        services.AddScoped<CreateAnnouncementCommandHandler>();
        services.AddScoped<UpdateAnnouncementCommandHandler>();
        services.AddScoped<DeleteAnnouncementCommandHandler>();
        services.AddScoped<SetAnnouncementFeaturedCommandHandler>();
    }

    private static void AddResourceHandlers(IServiceCollection services)
    {
        services.AddScoped<ListResourcesQueryHandler>();
        services.AddScoped<ListResourceTypesQueryHandler>();
        services.AddScoped<GetResourceByIdQueryHandler>();
        services.AddScoped<CreateResourceCommandHandler>();
        services.AddScoped<UpdateResourceCommandHandler>();
        services.AddScoped<DeleteResourceCommandHandler>();
    }

    private static void AddFileHandlers(IServiceCollection services)
    {
        services.AddScoped<GetFileByIdQueryHandler>();
        services.AddScoped<GetFileContentQueryHandler>();
        services.AddScoped<CreateFileCommandHandler>();
        services.AddScoped<UpdateFileCommandHandler>();
        services.AddScoped<DeleteFileCommandHandler>();
        services.AddScoped<FileUploadValidator>();
    }

    private static void AddEventHandlers(IServiceCollection services)
    {
        services.AddScoped<ListEventsQueryHandler>();
        services.AddScoped<GetEventByIdQueryHandler>();
        services.AddScoped<GetPastEventYearsQueryHandler>();
        services.AddScoped<ListEventCategoryTypesQueryHandler>();
        services.AddScoped<CreateEventCommandHandler>();
        services.AddScoped<UpdateEventCommandHandler>();
        services.AddScoped<DeleteEventCommandHandler>();
        services.AddScoped<SetEventFeaturedCommandHandler>();
        services.AddScoped<CreateEventCategoryTypeCommandHandler>();
        services.AddScoped<UpdateEventCategoryTypeCommandHandler>();
        services.AddScoped<DeleteEventCategoryTypeCommandHandler>();
        services.AddScoped<EventCategoryChecker>();
    }

    private static void AddParticipationHandlers(IServiceCollection services)
    {
        services.AddScoped<GetEventHistoryQueryHandler>();
        services.AddScoped<GetEventCertificatesQueryHandler>();
        services.AddScoped<ListEventRatingsQueryHandler>();
        services.AddScoped<SaveEventRatingCommandHandler>();
    }

    private static void AddUserHandlers(IServiceCollection services)
    {
        services.AddScoped<ListUsersQueryHandler>();
        services.AddScoped<GetUserByIdQueryHandler>();
        services.AddScoped<ListUserStatusTypesQueryHandler>();
        services.AddScoped<ListUserTypesQueryHandler>();
        services.AddScoped<UpdateUserCommandHandler>();
        services.AddScoped<DeleteUserCommandHandler>();
        services.AddScoped<SetAdminCommandHandler>();
        services.AddScoped<ChangeUserTypeCommandHandler>();
        services.AddScoped<AddChildCommandHandler>();
        services.AddScoped<ChangePasswordCommandHandler>();
    }

    private static void AddAuthHandlers(IServiceCollection services)
    {
        services.AddScoped<GetCurrentUserQueryHandler>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<RegisterCommandHandler>();
        services.AddScoped<VerifyUserCommandHandler>();
        services.AddScoped<ResendVerificationCommandHandler>();
        services.AddScoped<ForgotPasswordCommandHandler>();
        services.AddScoped<ResetPasswordCommandHandler>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<OtpValidator>();
    }
}
