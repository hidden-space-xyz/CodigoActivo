using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Application.TermsDecisions.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Messaging;

public sealed class AuditQueryDecoratorTests
{
    private static readonly IPAddress Address = IPAddress.Parse("203.0.113.7");

    private readonly RecordingAuditTrail trail = new();
    private readonly RecordingLogger<AuditWriter> logger = new();
    private readonly TestCurrentUser currentUser = new();

    private Task<TResult> HandleAsync<TQuery, TResult>(TQuery query, TResult answer)
        where TQuery : IQuery<TResult>
    {
        var origin = Substitute.For<IRequestOrigin>();
        origin.ClientAddress.Returns(Address);
        var sut = new AuditQueryDecorator<TQuery, TResult>(
            new AnsweringHandler<TQuery, TResult>(answer),
            new AuditWriter(trail, currentUser, origin, logger)
        );
        return sut.HandleAsync(query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAuditedReadThatHandedItsDataOverRecordsTheActorAndWhatItNamed()
    {
        var @event = EventId.New();
        var person = UserId.New();
        var document = TermsDocumentId.New();

        await HandleAsync<GetEventTermsSignatureQuery, Result<byte[]>>(
            new GetEventTermsSignatureQuery(@event, person, document),
            new byte[] { 1, 2, 3 }
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record
            .Should()
            .BeEquivalentTo(
                new AuditRecord(
                    AuditAction.GetEventTermsSignature,
                    AuditActorType.User,
                    currentUser.Id,
                    Address,
                    new AuditEntity(AuditEntityType.Event, @event.Value),
                    [
                        new AuditEntity(AuditEntityType.User, person.Value),
                        new AuditEntity(AuditEntityType.TermsDocument, document.Value),
                    ],
                    AuditOutcome.Succeeded,
                    ErrorCode: null
                )
            );
    }

    [Fact]
    public async Task HandleAsyncAuditedReadThatFoundNothingIsNotRecorded()
    {
        Result<EventRosterResponse> answer = Error.NotFound(ApplicationErrorCode.EventNotFound);

        var result = await HandleAsync<GetEventRosterQuery, Result<EventRosterResponse>>(
            new GetEventRosterQuery(EventId.New()),
            answer
        );

        result.Should().BeSameAs(answer);
        trail.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAnyOtherReadIsNotRecorded()
    {
        await HandleAsync<EverydayQuery, Result>(
            new EverydayQuery(EventId.New()),
            Result.Success()
        );

        trail.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncTrailFailureLeavesTheResultUntouched()
    {
        trail.Failure = new InvalidOperationException("queue gone");
        Result<byte[]> answer = new byte[] { 1 };

        var result = await HandleAsync<GetEventTermsSignatureQuery, Result<byte[]>>(
            new GetEventTermsSignatureQuery(EventId.New(), UserId.New(), TermsDocumentId.New()),
            answer
        );

        result.Should().BeSameAs(answer);
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("The audit entry of a GetEventTermsSignature could not be recorded");
    }

    private sealed record EverydayQuery(EventId EventId) : IQuery<Result>;

    private sealed class AnsweringHandler<TQuery, TResult>(TResult answer)
        : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
        {
            return Task.FromResult(answer);
        }
    }
}
