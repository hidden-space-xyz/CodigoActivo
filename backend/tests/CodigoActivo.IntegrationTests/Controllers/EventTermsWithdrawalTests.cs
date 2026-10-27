using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Application.TermsDecisions.Contracts;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EventTermsWithdrawalTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid eventId = Guid.NewGuid();
    private readonly Guid imageId = Guid.NewGuid();
    private readonly Guid conditionsId = Guid.NewGuid();

    private static TermsDocument Document(Guid id, string name, TermsDocumentKind kind)
    {
        return Persisted.As<TermsDocument>(
            new
            {
                Id = id,
                Name = $"{name} {id:N}",
                Description = "{}",
                RequiresSignature = false,
                Kind = kind,
            }
        );
    }

    private EventTermsAcceptance Accepted(Guid personId, Guid documentId)
    {
        return EventTermsAcceptance.Record(
            EventId.From(eventId),
            UserId.From(personId),
            UserId.From(TestSeedData.Users.MemberId),
            TermsDocumentId.From(documentId),
            1,
            true,
            null,
            UserId.From(TestSeedData.Users.MemberId),
            SeededAt
        );
    }

    private async Task SeedAsync()
    {
        var thumb = await SeedThumbnailAsync();
        await Factory.SeedAsync(db =>
        {
            db.TermsDocuments.AddRange(
                Document(imageId, "Imagen", TermsDocumentKind.Consent),
                Document(conditionsId, "Normas", TermsDocumentKind.Conditions)
            );
            var ev = Persisted.As<Event>(
                new
                {
                    Id = eventId,
                    Title = "Feria",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 7, 1),
                    EventEndsAt = new DateOnly(2026, 7, 31),
                    SignupStartsAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                    SignupEndsAt = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
                    ThumbnailId = thumb,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            Persisted.Add(
                ev.TermsDocuments,
                Persisted.As<EventTermsDocument>(
                    new
                    {
                        TermsDocumentId = conditionsId,
                        IsRequired = true,
                        DisplayOrder = 0,
                    }
                )
            );
            Persisted.Add(
                ev.TermsDocuments,
                Persisted.As<EventTermsDocument>(
                    new
                    {
                        TermsDocumentId = imageId,
                        IsRequired = false,
                        DisplayOrder = 1,
                    }
                )
            );
            db.Events.Add(ev);
            db.EventTermsAcceptances.AddRange(
                Accepted(TestSeedData.Users.MemberChildId, imageId),
                Accepted(TestSeedData.Users.MemberChildId, conditionsId)
            );
            return Task.CompletedTask;
        });
    }

    private Task<HttpResponseMessage> WithdrawAsync(HttpClient client, Guid userId, Guid documentId)
    {
        return client.SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/terms/decisions/{userId}/{documentId}/withdraw",
            null,
            Ct
        );
    }

    private Task<EventTermsAcceptance> DecisionAsync(Guid documentId)
    {
        return Factory.QueryAsync(db =>
            db.EventTermsAcceptances.Include(a => a.Changes)
                .SingleAsync(
                    a =>
                        a.EventId == EventId.From(eventId)
                        && a.UserId == UserId.From(TestSeedData.Users.MemberChildId)
                        && a.TermsDocumentId == TermsDocumentId.From(documentId),
                    Ct
                )
        );
    }

    [Fact]
    public async Task WithdrawAsGuardianEndsTheConsentKeepsTheHistoryAndTellsTheLegalContactOnTheNextRun()
    {
        await SeedAsync();
        var client = await LoginAsMemberAsync();

        using var response = await WithdrawAsync(client, TestSeedData.Users.MemberChildId, imageId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var decision = await DecisionAsync(imageId);
        decision.Accepted.Should().BeFalse();
        decision
            .Changes.Select(change => (change.Accepted, change.RecordedById.Value))
            .Should()
            .Equal((true, TestSeedData.Users.MemberId), (false, TestSeedData.Users.MemberId));
        decision.WithdrawalNoticePending.Should().BeTrue();
        Factory
            .EmailSender.Sent.Should()
            .BeEmpty("the legal contact is told by the scheduled notice");

        (await Factory.SendLegalNoticesAsync()).Withdrawals.Should().Be(1);

        var notice = Factory.EmailSender.Sent.Should().ContainSingle().Subject;
        notice.ToAddress.Should().Be("contacto@codigoactivo.test");
        notice.Subject.Should().Be("Se ha retirado un consentimiento");
        notice.TextBody.Should().Contain("de Mateo Miembro para el evento «Feria»");
        (await DecisionAsync(imageId)).WithdrawalNoticePending.Should().BeFalse();
        (await Factory.SendLegalNoticesAsync())
            .Withdrawals.Should()
            .Be(0, "a withdrawal is told once");
    }

    [Fact]
    public async Task WithdrawAsAdministratorRecordsItForTheGuardian()
    {
        await SeedAsync();
        var client = await LoginAsAdminAsync();

        using var response = await WithdrawAsync(client, TestSeedData.Users.MemberChildId, imageId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var withdrawal = (await DecisionAsync(imageId)).Changes.Last();
        withdrawal.DecidedById.Value.Should().Be(TestSeedData.Users.MemberId);
        withdrawal.RecordedById.Value.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task WithdrawForSomeoneOutsideTheHouseholdIsForbidden()
    {
        await SeedAsync();
        var client = await LoginAsMemberAsync();

        using var response = await WithdrawAsync(client, TestSeedData.Users.BlockedId, imageId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DecisionAsync(imageId)).Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task WithdrawConditionsReturnsBadRequest()
    {
        await SeedAsync();
        var client = await LoginAsMemberAsync();

        using var response = await WithdrawAsync(
            client,
            TestSeedData.Users.MemberChildId,
            conditionsId
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.EventTermsWithdrawalNotAllowed);
        (await DecisionAsync(conditionsId)).Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task WithdrawTwiceReturnsConflict()
    {
        await SeedAsync();
        var client = await LoginAsMemberAsync();
        using var first = await WithdrawAsync(client, TestSeedData.Users.MemberChildId, imageId);

        using var second = await WithdrawAsync(client, TestSeedData.Users.MemberChildId, imageId);

        await second.ShouldBeConflictAsync(ErrorCode.EventTermsConsentNotGiven);
        (await DecisionAsync(imageId)).Changes.Should().HaveCount(2);
    }

    [Fact]
    public async Task WithdrawnConsentShowsAsWithdrawnInMyConsentsAndToTheAdministrator()
    {
        await SeedAsync();
        var member = await LoginAsMemberAsync();
        using var withdrawn = await WithdrawAsync(
            member,
            TestSeedData.Users.MemberChildId,
            imageId
        );

        using var mine = await member.GetAsync(TestUri.Rel("/api/me/consents"), Ct);
        var admin = await LoginAsAdminAsync();
        using var decisions = await admin.GetAsync(
            TestUri.Rel($"/api/events/{eventId}/terms/decisions"),
            Ct
        );

        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        var consent = (await mine.ReadJsonAsync<List<MyConsentResponse>>(Ct))!
            .Should()
            .ContainSingle()
            .Subject;
        consent.TermsDocumentId.Should().Be(imageId);
        consent.PersonId.Should().Be(TestSeedData.Users.MemberChildId);
        consent.Accepted.Should().BeFalse();
        consent.Withdrawn.Should().BeTrue();
        decisions.StatusCode.Should().Be(HttpStatusCode.OK);
        (await decisions.ReadJsonAsync<PagedResult<EventTermsDecisionResponse>>(Ct))!
            .Items.Select(decision => (decision.TermsDocumentId, decision.Withdrawn))
            .Should()
            .BeEquivalentTo([(imageId, true), (conditionsId, false)]);
    }
}
