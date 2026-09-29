using AwesomeAssertions;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Application.Emails.Queries;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Queries;

public sealed class GetUsersEmailAudienceQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetUsersEmailAudienceQueryHandler sut;

    public GetUsersEmailAudienceQueryHandlerTests()
    {
        sut = new GetUsersEmailAudienceQueryHandler(store, new FakeQueryExecutor());
    }

    private async Task<EmailAudienceResponse> AudienceAsync(UserListQuery filters)
    {
        var result = await sut.HandleAsync(
            new GetUsersEmailAudienceQuery(filters),
            TestContext.Current.CancellationToken
        );
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task HandleAsyncMixedConsentCountsDistinctAddressesAndThoseWithoutConsent()
    {
        var parent = NewUserRow("Ana", "ana@test.local", promotionalConsent: true);
        store.Users.AddRange([
            parent,
            NewUserRow("Berto", "berto@test.local"),
            NewUserRow("Carla", "carla@test.local", promotionalConsent: true),
            NewUserRow("Duplicado", "BERTO@test.local"),
            NewUserRow("Hijo", null, parent),
            NewUserRow("Blanco", "   "),
        ]);

        var audience = await AudienceAsync(new UserListQuery());

        audience.Should().Be(new EmailAudienceResponse(3, 1));
    }

    [Fact]
    public async Task HandleAsyncEveryoneConsentingReportsNoRecipientWithoutConsent()
    {
        store.Users.AddRange([
            NewUserRow("Ana", "ana@test.local", promotionalConsent: true),
            NewUserRow("Berto", "berto@test.local", promotionalConsent: true),
        ]);

        var audience = await AudienceAsync(new UserListQuery());

        audience.Should().Be(new EmailAudienceResponse(2, 0));
    }

    [Fact]
    public async Task HandleAsyncFiltersNarrowTheAudienceLikeTheSendEndpoint()
    {
        var target = NewUserRow("Ana", "ana@test.local");
        store.Users.AddRange([target, NewUserRow("Berto", "berto@test.local")]);

        var byId = await AudienceAsync(new UserListQuery { Id = target.Id });
        var byConsent = await AudienceAsync(new UserListQuery { PromotionalConsent = true });

        byId.Should().Be(new EmailAudienceResponse(1, 1));
        byConsent.Should().Be(new EmailAudienceResponse(0, 0));
    }

    [Fact]
    public async Task HandleAsyncOnlyUsersWithoutAddressReportsAnEmptyAudience()
    {
        var parent = NewUserRow("Ana", null);
        store.Users.AddRange([parent, NewUserRow("Hijo", null, parent)]);

        var audience = await AudienceAsync(new UserListQuery());

        audience.Should().Be(new EmailAudienceResponse(0, 0));
    }
}
