using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed partial class LoggingConventionTests
{
    private static readonly string[] AllowedPlaceholders =
    [
        "ElapsedMilliseconds",
        "UseCase",
        "Attempts",
        "Capacity",
        "Count",
        "Effect",
        "Kind",
        "Limit",
        "MaxFailedAttempts",
        "Method",
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

    private static readonly string[] SecurityEvents =
    [
        "PasswordLockoutTriggered",
        "SecurityNotificationRateLimited",
        "TwoFactorLockoutTriggered",
    ];

    private static readonly LogLevel[] AllowedLevels =
    [
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    ];

    [GeneratedRegex(@"\{(@|\$)?(?<name>[A-Za-z0-9_]+)(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private static IReadOnlyList<(MethodInfo Method, LoggerMessageAttribute Attribute)> Events()
    {
        return
        [
            .. ProductionAssemblies
                .AllTypes.SelectMany(type =>
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
    public void LogEventsNeverUseALevelBelowWarning()
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
