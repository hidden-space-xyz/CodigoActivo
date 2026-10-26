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

    private static List<Type> Commands()
    {
        return
        [
            .. ProductionAssemblies
                .Application.GetTypes()
                .Where(type =>
                    type is { IsClass: true, IsAbstract: false }
                    && type.GetInterfaces()
                        .Any(contract =>
                            contract.IsGenericType
                            && contract.GetGenericTypeDefinition() == typeof(ICommand<>)
                        )
                ),
        ];
    }

    [Fact]
    public void EveryCommandHasItsAuditActionOrIsMarkedUnaudited()
    {
        var offenders = Commands()
            .Where(command =>
                (CommandAudit.ActionOf(command) is null)
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
    public void EveryAuditActionNamesAnExistingCommand()
    {
        var actions = Commands().Select(CommandAudit.ActionOf).OfType<AuditAction>().ToHashSet();

        Enum.GetValues<AuditAction>().Should().OnlyContain(action => actions.Contains(action));
    }

    [Fact]
    public void OnlyTheRatingCommandsAreUnauditedAndOnlyThePurgesAreScheduled()
    {
        Commands()
            .Where(typeof(IUnaudited).IsAssignableFrom)
            .Select(command => command.Name)
            .Should()
            .BeEquivalentTo("SaveEventRatingCommand", "DeleteEventRatingCommand");
        Commands()
            .Where(typeof(IScheduledCommand).IsAssignableFrom)
            .Select(command => command.Name)
            .Should()
            .BeEquivalentTo(
                "RemoveExpiredSessionsCommand",
                "PurgeDataHistoryCommand",
                "PurgeUnverifiedAccountsCommand",
                "PurgeUnusedFilesCommand",
                "PurgeAdultDependentsCommand",
                "SendComingOfAgeNoticesCommand"
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
