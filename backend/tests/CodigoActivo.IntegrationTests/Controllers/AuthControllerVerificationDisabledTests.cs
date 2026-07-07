using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Security;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class AuthControllerVerificationDisabledTests : IntegrationTestBase
{
    private readonly WebApplicationFactory<Program> disabledFactory;

    public AuthControllerVerificationDisabledTests(CodigoActivoWebAppFactory factory)
        : base(factory)
    {
        disabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AccountVerificationOptions>();
                services.AddSingleton(new AccountVerificationOptions { Required = false });
            })
        );
    }

    public override async ValueTask DisposeAsync()
    {
        await disabledFactory.DisposeAsync();
    }

    private static RegisterRequest NewAdultRequest()
    {
        return new RegisterRequest(
            "Nadia",
            "Nueva",
            "new.adult@codigoactivo.test",
            "+34600000099",
            "Str0ngPass!",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30),
            SeedIds.UserTypes.Member,
            Minors: null
        );
    }

    [Fact]
    public async Task Register_creates_an_active_account_without_sending_email()
    {
        var client = disabledFactory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register", NewAdultRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>();
        body!.RequiresVerification.Should().BeFalse();
        body.Adult.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);
        Factory.EmailSender.Sent.Should().BeEmpty();

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(body.Adult.Id).AsTask());
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        stored.OtpCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task Register_then_login_works_immediately()
    {
        var client = disabledFactory.CreateClient();
        using var register = await client.PostJsonAsync("/api/auth/register", NewAdultRequest());
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest("new.adult@codigoactivo.test", "Str0ngPass!")
        );

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_activates_an_existing_pending_user()
    {
        var client = disabledFactory.CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.PendingId).AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
    }

    [Fact]
    public async Task Resend_verification_is_rejected_when_verification_is_disabled()
    {
        var client = disabledFactory.CreateClient();

        var response = await client.PostJsonAsync(
            $"/api/auth/{TestSeedData.Users.PendingId}/resend-verification",
            body: null
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
    }
}
