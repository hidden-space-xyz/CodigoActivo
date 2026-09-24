using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Queries;

public sealed class GetUsersEmailAudienceQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly GetUsersEmailAudienceQueryHandler sut;

    public GetUsersEmailAudienceQueryHandlerTests()
    {
        sut = new GetUsersEmailAudienceQueryHandler(users, new FakeQueryExecutor());
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
        var parent = NewUser("Ana", "ana@test.local", promotionalConsent: true);
        users.HasUsers(
            parent,
            NewUser("Berto", "berto@test.local"),
            NewUser("Carla", "carla@test.local", promotionalConsent: true),
            NewUser("Duplicado", "BERTO@test.local"),
            NewUser("Hijo", null, parent),
            NewUser("Blanco", "   ")
        );

        var audience = await AudienceAsync(new UserListQuery());

        audience.Should().Be(new EmailAudienceResponse(3, 1));
    }

    [Fact]
    public async Task HandleAsyncEveryoneConsentingReportsNoRecipientWithoutConsent()
    {
        users.HasUsers(
            NewUser("Ana", "ana@test.local", promotionalConsent: true),
            NewUser("Berto", "berto@test.local", promotionalConsent: true)
        );

        var audience = await AudienceAsync(new UserListQuery());

        audience.Should().Be(new EmailAudienceResponse(2, 0));
    }

    [Fact]
    public async Task HandleAsyncFiltersNarrowTheAudienceLikeTheSendEndpoint()
    {
        var target = NewUser("Ana", "ana@test.local");
        users.HasUsers(target, NewUser("Berto", "berto@test.local"));

        var byId = await AudienceAsync(new UserListQuery { Id = target.Id });
        var byConsent = await AudienceAsync(new UserListQuery { PromotionalConsent = true });

        byId.Should().Be(new EmailAudienceResponse(1, 1));
        byConsent.Should().Be(new EmailAudienceResponse(0, 0));
    }

    [Fact]
    public async Task HandleAsyncOnlyUsersWithoutAddressReportsAnEmptyAudience()
    {
        var parent = NewUser("Ana", null);
        users.HasUsers(parent, NewUser("Hijo", null, parent));

        var audience = await AudienceAsync(new UserListQuery());

        audience.Should().Be(new EmailAudienceResponse(0, 0));
    }
}
