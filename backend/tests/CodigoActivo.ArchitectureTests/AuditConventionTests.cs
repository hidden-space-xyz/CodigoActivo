using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed class AuditConventionTests
{
    private static readonly string[] NeverRecordedIdentifiers =
    [
        "EventRatingId",
        "LoginChallengeId",
        "UserSessionId",
    ];

    private static readonly AuditAction[] DetectedEvents =
    [
        AuditAction.PasswordLocked,
        AuditAction.TwoFactorLocked,
        AuditAction.AccessDenied,
    ];

    private static readonly string[] CredentialProperties =
    [
        "Password",
        "CurrentPassword",
        "NewPassword",
        "Code",
        "Token",
    ];

    private static List<Type> Implementing(Type message)
    {
        return
        [
            .. ProductionAssemblies
                .Application.GetTypes()
                .Where(type =>
                    type is { IsClass: true, IsAbstract: false }
                    && type.GetInterfaces()
                        .Any(contract =>
                            contract.IsGenericType && contract.GetGenericTypeDefinition() == message
                        )
                ),
        ];
    }

    private static List<Type> Commands()
    {
        return Implementing(typeof(ICommand<>));
    }

    private static List<Type> Queries()
    {
        return Implementing(typeof(IQuery<>));
    }

    [Fact]
    public void EveryCommandHasItsAuditActionOrIsMarkedUnaudited()
    {
        var offenders = Commands()
            .Where(command =>
                (MessageAudit.ActionOf(command) is null)
                != typeof(IUnaudited).IsAssignableFrom(command)
            )
            .Select(command => command.Name)
            .ToList();

        Commands().Should().HaveCountGreaterThan(50);
        offenders
            .Should()
            .BeEmpty(
                "a new command needs an AuditAction member, with its translation in the frontend, or IUnaudited"
            );
    }

    [Fact]
    public void EveryAuditActionNamesACommandAnAuditedQueryOrADetectedEvent()
    {
        var actions = Commands()
            .Concat(Queries().Where(typeof(IAuditedQuery).IsAssignableFrom))
            .Select(MessageAudit.ActionOf)
            .OfType<AuditAction>()
            .ToHashSet();

        Enum.GetValues<AuditAction>()
            .Where(action => !actions.Contains(action))
            .Should()
            .BeEquivalentTo(DetectedEvents);
    }

    [Fact]
    public void OnlyTheReadsThatHandOverPersonalDataAreAudited()
    {
        var audited = Queries().Where(typeof(IAuditedQuery).IsAssignableFrom).ToList();

        audited
            .Select(query => query.Name)
            .Should()
            .BeEquivalentTo(
                "GetPersonalDataQuery",
                "GetEventTermsSignatureQuery",
                "GetEventRosterQuery",
                "GetEventBadgesQuery"
            );
        Queries()
            .Where(query => MessageAudit.ActionOf(query) is not null)
            .Should()
            .BeEquivalentTo(audited);
    }

    [Fact]
    public void NoCommandThatProvesACredentialIsRoutine()
    {
        Commands()
            .Where(typeof(IRoutineCommand).IsAssignableFrom)
            .Where(command =>
                command.GetProperties().Any(p => CredentialProperties.Contains(p.Name))
            )
            .Select(command => command.Name)
            .Should()
            .BeEmpty("a refused credential is what the audit log is for");
    }

    [Fact]
    public void AnAuditedCommandFollowsAtMostOneRecordingRule()
    {
        Type[] rules =
        [
            typeof(IUnaudited),
            typeof(IScheduledCommand),
            typeof(IRoutineCommand),
            typeof(IPreparatoryCommand),
            typeof(IAccountLookupCommand),
        ];

        Commands()
            .Where(command => rules.Count(rule => rule.IsAssignableFrom(command)) > 1)
            .Select(command => command.Name)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void OnlyThePasswordResetRequestDependsOnFindingItsAccount()
    {
        Commands()
            .Where(typeof(IAccountLookupCommand).IsAssignableFrom)
            .Select(command => command.Name)
            .Should()
            .BeEquivalentTo("ForgotPasswordCommand");
    }

    [Fact]
    public void OnlyTheStepsThatPrepareAChangeArePreparatory()
    {
        Commands()
            .Where(typeof(IPreparatoryCommand).IsAssignableFrom)
            .Select(command => command.Name)
            .Should()
            .BeEquivalentTo(
                "BeginAuthenticatorSetupCommand",
                "RequestAccountDeletionCodeCommand",
                "RequestBlockedDataConsultationCodeCommand"
            );
    }

    [Fact]
    public void OnlyTheRatingsAndWhatSaysNothingOfSecurityAreUnauditedAndPurgesAndNoticesScheduled()
    {
        Commands()
            .Where(typeof(IUnaudited).IsAssignableFrom)
            .Select(command => command.Name)
            .Should()
            .BeEquivalentTo(
                "SaveEventRatingCommand",
                "DeleteEventRatingCommand",
                "EndLoginChallengeCommand",
                "ResendTwoFactorCodeCommand",
                "ResendVerificationCommand",
                "SetEventFeaturedCommand",
                "SetNewsItemFeaturedCommand",
                "RemoveExpiredSessionsCommand",
                "DiscardExpiredAccountChangesCommand",
                "PurgeUnusedFilesCommand"
            );
        Commands()
            .Where(typeof(IScheduledCommand).IsAssignableFrom)
            .Select(command => command.Name)
            .Should()
            .BeEquivalentTo(
                "PurgeDataHistoryCommand",
                "PurgeUnverifiedAccountsCommand",
                "PurgeAdultDependentsCommand",
                "SendComingOfAgeNoticesCommand",
                "SendConsentWithdrawalNoticesCommand",
                "SendBlockedDataConsultationNoticesCommand"
            );
    }

    [Fact]
    public void OnlyTheAccountsOfTheBlockedDataAreUnauditedProperties()
    {
        ProductionAssemblies
            .Application.GetTypes()
            .SelectMany(type =>
                type.GetProperties()
                    .Where(property =>
                        property.IsDefined(typeof(UnauditedAttribute), inherit: false)
                    )
                    .Select(property => $"{type.Name}.{property.Name}")
            )
            .Should()
            .BeEquivalentTo(
                "ExportBlockedDataCommand.AccountId",
                "PlaceBlockedDataHoldCommand.AccountId"
            );
    }

    [Fact]
    public void AuditEntityTypesAreTheTypedIdentifiersThatMayBeRecorded()
    {
        var identifiers = ProductionAssemblies
            .Domain.GetTypes()
            .Where(type =>
                type.IsValueType
                && type.GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IEntityId<>)
                    )
            )
            .ToList();

        identifiers
            .Where(type => !NeverRecordedIdentifiers.Contains(type.Name))
            .Select(type => type.Name[..^"Id".Length])
            .Should()
            .BeEquivalentTo(Enum.GetNames<AuditEntityType>());
        identifiers
            .Where(type => NeverRecordedIdentifiers.Contains(type.Name))
            .Should()
            .HaveCount(NeverRecordedIdentifiers.Length)
            .And.OnlyContain(type => AuditEntity.TypeOf(type) == null);
    }
}
