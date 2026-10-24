using AwesomeAssertions;
using CodigoActivo.API.Legal.Contracts;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Legal.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Legal;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using LegalPolicyKindContract = CodigoActivo.Application.Abstractions.Querying.ReadModel.LegalPolicyKind;

namespace CodigoActivo.UnitTests.Application.Legal.Commands;

public sealed class PublishLegalPolicyCommandHandlerTests
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Friday = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly ILegalPolicyRepository legalPolicies =
        Substitute.For<ILegalPolicyRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>().RunsTransactions();
    private readonly TestClock clock = new(Friday);
    private readonly AuditSubject auditSubject = new();
    private readonly PublishLegalPolicyCommandHandler sut;

    public PublishLegalPolicyCommandHandlerTests()
    {
        sut = new PublishLegalPolicyCommandHandler(legalPolicies, uow, clock, auditSubject);
    }

    private static string Text(string text)
    {
        return $$"""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"{{text}}"}]}]}""";
    }

    private Task<Result> PublishAsync(LegalPolicyKindContract kind, string content)
    {
        return sut.HandleAsync(
            new PublishLegalPolicyRequest(content).ToCommand(kind),
            TestContext.Current.CancellationToken
        );
    }

    private void Stored(LegalPolicy? policy)
    {
        legalPolicies
            .LockAsync(Arg.Any<LegalPolicyKind>(), Arg.Any<CancellationToken>())
            .Returns(policy);
    }

    [Fact]
    public async Task HandleAsyncFirstPublicationAddsThePolicyAndNamesItInTheAudit()
    {
        Stored(null);
        LegalPolicy? added = null;
        await legalPolicies.AddAsync(
            Arg.Do<LegalPolicy>(policy => added = policy),
            Arg.Any<CancellationToken>()
        );

        var result = await PublishAsync(LegalPolicyKindContract.Cookies, Text("Cookies"));

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.Kind.Should().Be(LegalPolicyKind.Cookies);
        added.Version.Should().Be(LegalPolicy.FirstVersion);
        added.Content.Json.Should().Be(Text("Cookies"));
        added.PublishedAt.Should().Be(Friday);
        auditSubject
            .Entity.Should()
            .Be(new AuditEntity(AuditEntityType.LegalPolicy, added.Id.Value));
        await legalPolicies
            .Received(1)
            .LockAsync(LegalPolicyKind.Cookies, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncPublishedPolicyGetsANewVersion()
    {
        var policy = LegalPolicy
            .Publish(LegalPolicyKind.Privacy, RichText.From(Text("Primera")), Monday)
            .Value;
        Stored(policy);

        var result = await PublishAsync(LegalPolicyKindContract.Privacy, Text("Segunda"));

        result.IsSuccess.Should().BeTrue();
        policy.Version.Should().Be(2);
        policy.Content.Json.Should().Be(Text("Segunda"));
        policy.PublishedAt.Should().Be(Friday);
        policy.PreviousVersions.Should().ContainSingle();
        auditSubject
            .Entity.Should()
            .Be(new AuditEntity(AuditEntityType.LegalPolicy, policy.Id.Value));
        await legalPolicies
            .DidNotReceive()
            .AddAsync(Arg.Any<LegalPolicy>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncFirstPublicationWithoutTextIsRefused()
    {
        Stored(null);

        var result = await PublishAsync(LegalPolicyKindContract.Privacy, "{}");

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.LegalPolicyContentRequired);
        await legalPolicies
            .DidNotReceive()
            .AddAsync(Arg.Any<LegalPolicy>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRevisionWithoutTextIsRefused()
    {
        var policy = LegalPolicy
            .Publish(LegalPolicyKind.Privacy, RichText.From(Text("Vigente")), Monday)
            .Value;
        Stored(policy);

        var result = await PublishAsync(LegalPolicyKindContract.Privacy, "{}");

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.LegalPolicyContentRequired);
        policy.Version.Should().Be(LegalPolicy.FirstVersion);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncConcurrentFirstPublicationReturnsConflict()
    {
        Stored(null);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(
                new UniqueConstraintViolationException(
                    typeof(LegalPolicy),
                    new InvalidOperationException("duplicate kind")
                )
            );

        var result = await PublishAsync(LegalPolicyKindContract.Cookies, Text("Cookies"));

        result.ShouldFail(
            ErrorKind.Conflict,
            ApplicationErrorCode.LegalPolicyPublishedConcurrently
        );
    }

    [Fact]
    public async Task HandleAsyncOtherUniqueViolationIsNotAnswered()
    {
        Stored(null);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(
                new UniqueConstraintViolationException(
                    typeof(LegalPolicyVersion),
                    new InvalidOperationException("duplicate version")
                )
            );

        var publish = () => PublishAsync(LegalPolicyKindContract.Cookies, Text("Cookies"));

        await publish.Should().ThrowAsync<UniqueConstraintViolationException>();
    }
}
