using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Emails;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Common;
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
        services.AddFixedOptions<EmailQueueOptions>();
        services.AddFixedOptions<EmailGuardOptions>();
        services.AddFixedOptions<ManualEmailOptions>();

        services.AddSingleton<IEmailTransport, SmtpEmailSender>();
        services.AddSingleton<EmailOutboxSignal>();
        services.AddSingleton<EmailOutboxProtector>();
        services.AddSingleton<IEmailOutboxStore, EmailOutboxStore>();
        services.AddSingleton<IEmailOutbox>(sp => sp.GetRequiredService<IEmailOutboxStore>());
        services.AddSingleton<EmailOutboxDeliverer>();
        services.AddHostedService<EmailOutboxDeliveryWorker>();
        services.AddSingleton<IEmailSender, ThrottledEmailSender>();
        services.AddSingleton<IUnsubscribeTokens, DataProtectionUnsubscribeTokens>();
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
                options.Security = settings.Choice(
                    "SMTP_SECURITY",
                    SmtpOptions.DefaultSecurity(options.Port)
                );
                options.Username = settings.Verbatim("SMTP_USERNAME");
                options.Password = settings.Verbatim("SMTP_PASSWORD");
                options.FromAddress = settings.Text("SMTP_FROM_ADDRESS", string.Empty);
                options.FromName = settings.Text("SMTP_FROM_NAME", Organization.Name);
            },
            options =>
                !string.IsNullOrWhiteSpace(options.Host)
                && !string.IsNullOrWhiteSpace(options.FromAddress),
            "SMTP is not configured (SMTP_HOST and SMTP_FROM_ADDRESS are required). "
                + "Login codes are delivered by email, so every deployment needs an SMTP server; "
                + "for local work point it at a mail catcher such as Mailpit."
        );
        services
            .AddOptions<SmtpOptions>()
            .Validate(
                options => options.Port <= IPEndPoint.MaxPort,
                $"SMTP_PORT must be between 1 and {IPEndPoint.MaxPort}."
            );
    }
}
