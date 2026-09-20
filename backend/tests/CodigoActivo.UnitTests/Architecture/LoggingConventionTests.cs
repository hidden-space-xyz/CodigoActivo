using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed partial class LoggingConventionTests
{
    private static readonly string[] AllowedPlaceholders =
    [
        "Attempts",
        "Capacity",
        "ConfiguredMode",
        "Count",
        "Drain",
        "IsAdmin",
        "Kind",
        "Limit",
        "MaxFailedAttempts",
        "Method",
        "Remaining",
        "RouteTemplate",
        "SecurityChange",
    ];

    private static readonly string[] ForbiddenPlaceholders =
    [
        "ActingUserId",
        "ActivityId",
        "Address",
        "Body",
        "Code",
        "Cookie",
        "Email",
        "FileId",
        "FileIds",
        "Identifier",
        "Ip",
        "Name",
        "Otp",
        "Path",
        "Phone",
        "Query",
        "Recipient",
        "Recipients",
        "RemoteIpAddress",
        "SessionId",
        "Token",
        "TwoFactorMethod",
        "UserAgent",
        "UserId",
        "UserStatusTypeId",
        "UserTypeId",
    ];

    private static readonly string[] LifecycleEvents =
    [
        "DatabaseMigrationsApplied",
        "DatabaseSeedApplied",
        "DeploymentModeLockSkipped",
    ];

    private static readonly string[] SecurityEvents =
    [
        "AdministratorFlagChanged",
        "PasswordLockoutTriggered",
        "SecurityNotificationRateLimited",
        "TwoFactorLockoutTriggered",
    ];

    private static readonly LogLevel[] AllowedLevels =
    [
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    ];

    [GeneratedRegex(@"\{(@|\$)?(?<name>[A-Za-z0-9_]+)(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private static IReadOnlyList<Assembly> ProductionAssemblies()
    {
        return
        [
            typeof(Result).Assembly,
            typeof(IQuery<>).Assembly,
            typeof(SmtpEmailSender).Assembly,
            typeof(DependencyInjection).Assembly,
            typeof(ApiErrorResponseExtensions).Assembly,
        ];
    }

    private static IReadOnlyList<(MethodInfo Method, LoggerMessageAttribute Attribute)> Events()
    {
        return
        [
            .. ProductionAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .SelectMany(type =>
                    type.GetMethods(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Instance
                            | BindingFlags.DeclaredOnly
                    )
                )
                .Select(method =>
                    (Method: method, Attribute: method.GetCustomAttribute<LoggerMessageAttribute>())
                )
                .Where(candidate => candidate.Attribute is not null)
                .Select(candidate => (candidate.Method, Attribute: candidate.Attribute!)),
        ];
    }

    private static IReadOnlyList<string> Placeholders(string? message)
    {
        return
        [
            .. PlaceholderPattern()
                .Matches(message ?? string.Empty)
                .Select(match => match.Groups["name"].Value),
        ];
    }

    [Fact]
    public void ProductionAssembliesDeclareEveryLogEventWithLoggerMessage()
    {
        Events().Should().HaveCountGreaterThan(20);
    }

    [Fact]
    public void LogEventPlaceholdersOnlyUseTheAllowedNames()
    {
        var offenders = Events()
            .SelectMany(entry =>
                Placeholders(entry.Attribute.Message)
                    .Where(placeholder =>
                        !AllowedPlaceholders.Contains(placeholder, StringComparer.Ordinal)
                    )
                    .Select(placeholder => $"{entry.Method.Name}: {placeholder}")
            )
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void LogEventPlaceholdersNeverNamePersonalData()
    {
        var offenders = Events()
            .SelectMany(entry =>
                Placeholders(entry.Attribute.Message)
                    .Where(placeholder =>
                        ForbiddenPlaceholders.Contains(
                            placeholder,
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    .Select(placeholder => $"{entry.Method.Name}: {placeholder}")
            )
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void LogEventsAtInformationAreOnlyTheLifecycleOnes()
    {
        var offenders = Events()
            .Where(entry => entry.Attribute.Level == LogLevel.Information)
            .Select(entry => entry.Method.Name)
            .Where(name => !LifecycleEvents.Contains(name, StringComparer.Ordinal))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void LogEventsNeverUseALevelTheDefaultFilterDiscards()
    {
        var offenders = Events()
            .Where(entry => !AllowedLevels.Contains(entry.Attribute.Level))
            .Select(entry => entry.Method.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void SecurityLogDeclaresExactlyTheKeptSecurityEvents()
    {
        var declared = typeof(SecurityLog)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        declared.Should().Equal(SecurityEvents);
    }
}
