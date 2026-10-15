using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.API.TermsDocuments.Contracts;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EventTermsSignatureTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task<(Guid EventId, Guid ActivityId, Guid TermsDocumentId)> SeedSignedTermsAsync()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.TermsDocuments.Add(
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = termsDocumentId,
                        Name = $"Autorización {termsDocumentId:N}",
                        Description = "{}",
                        RequiresSignature = true,
                    }
                )
            );
            var ev = Persisted.As<Event>(
                new
                {
                    Id = eventId,
                    Title = "Evento",
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
                        TermsDocumentId = termsDocumentId,
                        IsRequired = true,
                        DisplayOrder = 0,
                    }
                )
            );
            db.Events.Add(ev);
            db.Activities.Add(
                Persisted.As<Activity>(
                    new
                    {
                        Id = activityId,
                        Title = "Actividad",
                        Description = "Descripcion",
                        Location = "Sala",
                        Modality = ActivityModality.Presencial,
                        ActivityStartsAt = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                        ActivityEndsAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
                        EventId = eventId,
                        ThumbnailId = thumb,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            return Task.CompletedTask;
        });
        return (eventId, activityId, termsDocumentId);
    }

    private static Task<HttpResponseMessage> SignUpAsync(
        HttpClient client,
        Guid activityId,
        TermsDecisionRequest decision
    )
    {
        return client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/assign",
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant, [decision]),
            Ct
        );
    }

    private Task<EventTermsAcceptance?> FindDecisionAsync(Guid eventId, Guid termsDocumentId)
    {
        return Factory.QueryAsync(db =>
            db.EventTermsAcceptances.SingleOrDefaultAsync(
                a =>
                    a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(TestSeedData.Users.MemberId)
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId),
                Ct
            )
        );
    }

    private static string SignaturePath(Guid eventId, Guid userId, Guid termsDocumentId)
    {
        return $"/api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/signature";
    }

    [Fact]
    public async Task SignupAcceptingDocumentThatAsksForSignatureWithoutOneReturnsBadRequest()
    {
        var (eventId, activityId, termsDocumentId) = await SeedSignedTermsAsync();
        var client = await LoginAsMemberAsync();

        using var response = await SignUpAsync(
            client,
            activityId,
            new TermsDecisionRequest(termsDocumentId, true)
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.EventTermsSignatureRequired);
        (await FindDecisionAsync(eventId, termsDocumentId)).Should().BeNull();
    }

    [Fact]
    public async Task SignupWithSignatureThatIsNotPngReturnsBadRequest()
    {
        var (eventId, activityId, termsDocumentId) = await SeedSignedTermsAsync();
        var client = await LoginAsMemberAsync();

        using var response = await SignUpAsync(
            client,
            activityId,
            new TermsDecisionRequest(termsDocumentId, true, [1, 2, 3])
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        (await FindDecisionAsync(eventId, termsDocumentId)).Should().BeNull();
    }

    [Fact]
    public async Task SignupWithSignatureThatIsNotBase64ReturnsBadRequest()
    {
        var (_, activityId, termsDocumentId) = await SeedSignedTermsAsync();
        var client = await LoginAsMemberAsync();

        using var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/assign",
            new
            {
                activityRoleTypeId = KnownIds.ActivityRoleTypes.Participant,
                termsDecisions = new[]
                {
                    new
                    {
                        termsDocumentId,
                        accepted = true,
                        signature = "no es base64",
                    },
                },
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task SignupWithSignatureStoresItAndAdministratorsCanReadIt()
    {
        var (eventId, activityId, termsDocumentId) = await SeedSignedTermsAsync();
        var member = await LoginAsMemberAsync();
        var png = TestSeedData.ValidPng();

        using var signup = await SignUpAsync(
            member,
            activityId,
            new TermsDecisionRequest(termsDocumentId, true, png)
        );

        signup.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindDecisionAsync(eventId, termsDocumentId);
        stored!.Accepted.Should().BeTrue();
        stored.Signature!.ToArray().Should().Equal(png);

        var admin = await LoginAsAdminAsync();
        using var list = await admin.GetAsync(
            TestUri.Rel($"/api/events/{eventId}/terms/decisions"),
            Ct
        );
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await list.ReadJsonAsync<PagedResult<EventTermsDecisionResponse>>(Ct);
        var decision = page!.Items.Should().ContainSingle().Subject;
        decision.UserId.Should().Be(TestSeedData.Users.MemberId);
        decision.TermsDocumentId.Should().Be(termsDocumentId);
        decision.Accepted.Should().BeTrue();
        decision.HasSignature.Should().BeTrue();

        using var image = await admin.GetAsync(
            TestUri.Rel(SignaturePath(eventId, TestSeedData.Users.MemberId, termsDocumentId)),
            Ct
        );
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        image.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        image.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await image.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(png);
    }

    [Fact]
    public async Task SignatureOfDecisionWithoutOneReturnsNotFound()
    {
        var (eventId, _, termsDocumentId) = await SeedSignedTermsAsync();
        var admin = await LoginAsAdminAsync();

        using var response = await admin.GetAsync(
            TestUri.Rel(SignaturePath(eventId, TestSeedData.Users.MemberId, termsDocumentId)),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.EventTermsSignatureNotFound);
    }

    [Fact]
    public async Task DecisionsAndSignaturesAsMemberReturnForbidden()
    {
        var (eventId, _, termsDocumentId) = await SeedSignedTermsAsync();
        var member = await LoginAsMemberAsync();

        using var list = await member.GetAsync(
            TestUri.Rel($"/api/events/{eventId}/terms/decisions"),
            Ct
        );
        using var image = await member.GetAsync(
            TestUri.Rel(SignaturePath(eventId, TestSeedData.Users.MemberId, termsDocumentId)),
            Ct
        );

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        image.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DecisionsOfUnknownEventReturnNotFound()
    {
        var admin = await LoginAsAdminAsync();

        using var response = await admin.GetAsync(
            TestUri.Rel($"/api/events/{Guid.NewGuid()}/terms/decisions"),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task TermsStateTellsWhichDocumentsAskForSignature()
    {
        var (eventId, _, termsDocumentId) = await SeedSignedTermsAsync();
        var member = await LoginAsMemberAsync();

        using var response = await member.GetAsync(TestUri.Rel($"/api/events/{eventId}/terms"), Ct);

        var state = await response.ReadJsonAsync<EventTermsStateResponse>(Ct);
        var document = state!.Documents.Should().ContainSingle().Subject;
        document.TermsDocumentId.Should().Be(termsDocumentId);
        document.RequiresSignature.Should().BeTrue();
    }

    [Fact]
    public async Task TermsDocumentCreatedAskingForSignatureKeepsIt()
    {
        var admin = await LoginAsAdminAsync();

        using var created = await admin.PostJsonAsync(
            "/api/events/termsDocument",
            new CreateTermsDocumentRequest(
                $"Firma {Guid.NewGuid():N}",
                "{\"type\":\"doc\",\"content\":[]}",
                RequiresSignature: true
            ),
            Ct
        );

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await created.ReadJsonAsync<TermsDocumentResponse>(Ct);
        document!.RequiresSignature.Should().BeTrue();
        (await FindAsync<TermsDocument>(document.Id))!.RequiresSignature.Should().BeTrue();
    }
}
