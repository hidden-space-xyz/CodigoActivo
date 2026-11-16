using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Messaging;

public sealed class MessageAuditTests
{
    [Fact]
    public void ActionOfCommandNamedAfterAnActionReturnsIt()
    {
        MessageAudit.ActionOf(typeof(LoginCommand)).Should().Be(AuditAction.Login);
    }

    [Theory]
    [InlineData(typeof(UnlistedThingCommand))]
    [InlineData(typeof(Login))]
    public void ActionOfTypeNamedAfterNoActionReturnsNull(Type message)
    {
        MessageAudit.ActionOf(message).Should().BeNull();
    }

    [Fact]
    public void NameOfTypeWithoutSuffixKeepsItsName()
    {
        MessageAudit.NameOf(typeof(Login)).Should().Be(nameof(Login));
        MessageAudit.NameOf(typeof(UnlistedThingCommand)).Should().Be("UnlistedThing");
    }

    [Fact]
    public void EntitiesOfListWithGapsAndIndexedRecordsNamesOnlyTheIdentifiers()
    {
        var first = UserId.New();
        var second = UserId.New();
        var activity = ActivityId.New();

        var entities = MessageAudit.EntitiesOf(
            new UnlistedThingCommand(activity, [null, new Indexed(first), new Indexed(second)])
        );

        entities
            .Should()
            .Equal(
                new AuditEntity(AuditEntityType.Activity, activity.Value),
                new AuditEntity(AuditEntityType.User, first.Value),
                new AuditEntity(AuditEntityType.User, second.Value)
            );
    }

    [Fact]
    public void OfIdentifierNeverRecordedReturnsNull()
    {
        AuditEntity.Of(UserSessionId.New()).Should().BeNull();
    }

    private sealed record UnlistedThingCommand(ActivityId Activity, IReadOnlyList<Indexed?> Items);

    private sealed record Login(UserId User);

    private sealed record Indexed(UserId User)
    {
        public UserId this[int index] => UserId.New();
    }
}
