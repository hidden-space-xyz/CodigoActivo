using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Emails;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Communication.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Emails;

/// <summary>
/// Registers email delivery: the SMTP server, the outbox queue, the limits that protect it and the
/// emails administrators write.
/// </summary>
internal static class EmailsRegistration
{
    /// <summary>
    /// Adds the email services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEmails(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var settings = new Settings(configuration);
        AddSmtp(services, settings);
        AddQueue(services, settings);
        AddGuard(services, settings);
        services.AddValidatedOptions<ManualEmailOptions>(options =>
        {
            options.MaxRecipients = settings.PositiveInt(
                "ManualEmail:MaxRecipients",
                ManualEmailOptions.DefaultMaxRecipients
            );
            options.MaxAttachments = settings.PositiveInt(
                "ManualEmail:MaxAttachments",
                ManualEmailOptions.DefaultMaxAttachments
            );
            options.MaxAttachmentsBytes = settings.PositiveLong(
                "ManualEmail:MaxAttachmentsBytes",
                ManualEmailOptions.DefaultMaxAttachmentsBytes
            );
        });

        services.AddSingleton<IEmailTransport, SmtpEmailSender>();
        services.AddSingleton<EmailOutboxSignal>();
        services.AddSingleton<EmailOutboxProtector>();
        services.AddSingleton<IEmailOutboxStore, EmailOutboxStore>();
        services.AddSingleton<IEmailOutbox>(sp => sp.GetRequiredService<IEmailOutboxStore>());
        services.AddSingleton<EmailOutboxDeliverer>();
        services.AddHostedService<EmailOutboxProcessor>();
        services.AddSingleton<IEmailSender, ThrottledEmailSender>();
        services.AddSingleton<IManualEmailComposer, ManualEmailComposer>();
        services.AddScoped<ManualEmailDispatcher>();
        return services;
    }

    private static void AddSmtp(IServiceCollection services, Settings settings)
    {
        services.AddValidatedOptions<SmtpOptions>(
            options =>
            {
                options.Host = settings.Text("SMTP_HOST", string.Empty);
                options.Port = settings.PositiveInt("SMTP_PORT", SmtpOptions.DefaultPort);
                options.Security = settings.Choice("SMTP_SECURITY", SmtpSecurityMode.StartTls);
                options.Username = settings.Verbatim("SMTP_USERNAME");
                options.Password = settings.Verbatim("SMTP_PASSWORD");
                options.FromAddress = settings.Text("SMTP_FROM_ADDRESS", string.Empty);
                options.FromName = settings.Text("SMTP_FROM_NAME", "Código Activo");
            },
            options =>
                !string.IsNullOrWhiteSpace(options.Host)
                && !string.IsNullOrWhiteSpace(options.FromAddress),
            "SMTP is not configured (SMTP_HOST and SMTP_FROM_ADDRESS are required). "
                + "Login codes are delivered by email, so every deployment needs an SMTP server; "
                + "for local work point it at a mail catcher such as Mailpit."
        );
    }

    private static void AddQueue(IServiceCollection services, Settings settings)
    {
        services.AddValidatedOptions<EmailQueueOptions>(
            options =>
            {
                options.Capacity = settings.PositiveInt(
                    "EmailQueue:Capacity",
                    EmailQueueOptions.DefaultCapacity
                );
                options.Workers = settings.PositiveInt(
                    "EmailQueue:Workers",
                    EmailQueueOptions.DefaultWorkers
                );
                options.BatchSize = settings.PositiveInt(
                    "EmailQueue:BatchSize",
                    EmailQueueOptions.DefaultBatchSize
                );
                options.ShutdownDrain = settings.Duration(
                    "EmailQueue:ShutdownDrainSeconds",
                    TimeSpan.FromSeconds,
                    EmailQueueOptions.DefaultShutdownDrain
                );
                options.SendTimeout = settings.Duration(
                    "EmailQueue:SendTimeoutSeconds",
                    TimeSpan.FromSeconds,
                    EmailQueueOptions.DefaultSendTimeout
                );
                options.PollInterval = settings.Duration(
                    "EmailQueue:PollIntervalSeconds",
                    TimeSpan.FromSeconds,
                    EmailQueueOptions.DefaultPollInterval
                );
            },
            options =>
                options.Workers <= EmailQueueOptions.MaxWorkers
                && options.BatchSize <= EmailQueueOptions.MaxBatchSize
                && options.ShutdownDrain <= EmailQueueOptions.MaxShutdownDrain
                && options.SendTimeout <= EmailQueueOptions.MaxSendTimeout
                && options.PollInterval <= EmailQueueOptions.MaxPollInterval,
            $"EmailQueue settings exceed their limits: at most {EmailQueueOptions.MaxWorkers} workers, "
                + $"batches of {EmailQueueOptions.MaxBatchSize}, "
                + $"{EmailQueueOptions.MaxShutdownDrain.TotalSeconds} seconds of shutdown drain, "
                + $"{EmailQueueOptions.MaxSendTimeout.TotalSeconds} seconds of send timeout and "
                + $"{EmailQueueOptions.MaxPollInterval.TotalSeconds} seconds of poll interval."
        );
    }

    private static void AddGuard(IServiceCollection services, Settings settings)
    {
        services.AddValidatedOptions<EmailGuardOptions>(options =>
        {
            options.RecipientBurst = settings.PositiveInt(
                "EmailGuard:RecipientBurst",
                EmailGuardOptions.DefaultRecipientBurst
            );
            options.RecipientPerHour = settings.PositiveInt(
                "EmailGuard:RecipientPerHour",
                EmailGuardOptions.DefaultRecipientPerHour
            );
            options.RecipientPerDay = settings.PositiveInt(
                "EmailGuard:RecipientPerDay",
                EmailGuardOptions.DefaultRecipientPerDay
            );
            options.GlobalBurst = settings.PositiveInt(
                "EmailGuard:GlobalBurst",
                EmailGuardOptions.DefaultGlobalBurst
            );
            options.GlobalPerHour = settings.PositiveInt(
                "EmailGuard:GlobalPerHour",
                EmailGuardOptions.DefaultGlobalPerHour
            );
            options.GlobalCredentialReserve = settings.PositiveInt(
                "EmailGuard:GlobalCredentialReserve",
                EmailGuardOptions.DefaultGlobalCredentialReserve
            );
            options.MaxTrackedRecipients = settings.PositiveInt(
                "EmailGuard:MaxTrackedRecipients",
                EmailGuardOptions.DefaultMaxTrackedRecipients
            );
            options.SweepInterval = settings.Duration(
                "EmailGuard:SweepIntervalMinutes",
                TimeSpan.FromMinutes,
                EmailGuardOptions.DefaultSweepInterval
            );
            options.AlertInterval = settings.Duration(
                "EmailGuard:AlertIntervalMinutes",
                TimeSpan.FromMinutes,
                EmailGuardOptions.DefaultAlertInterval
            );
        });
    }
}
