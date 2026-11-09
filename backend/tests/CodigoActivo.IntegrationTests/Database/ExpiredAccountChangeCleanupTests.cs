using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class ExpiredAccountChangeCleanupTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string ExpiredAddress = "caducada@codigoactivo.test";
    private const string WaitingAddress = "pendiente@codigoactivo.test";

    private Task<int> CleanUpAsync()
    {
        return Factory.RunScheduledAsync<DiscardExpiredAccountChangesCommand>();
    }

    private Task SeedChangesAsync()
    {
        var now = Factory.Clock.UtcNow;
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.SingleAsync(
                u => u.Id == UserId.From(TestSeedData.Users.MemberId),
                Ct
            );
            Persisted.Overwrite(
                member,
                new
                {
                    EmailChange = new
                    {
                        NewEmail = ExpiredAddress,
                        CodeHash = "fake:email-change",
                        ExpiresAt = now.AddSeconds(-1),
                    },
                    Authenticator = new
                    {
                        PendingKey = "protected:pending-secret",
                        PendingExpiresAt = now,
                    },
                }
            );
            var admin = await db.Users.SingleAsync(
                u => u.Id == UserId.From(TestSeedData.Users.AdminId),
                Ct
            );
            Persisted.Overwrite(
                admin,
                new
                {
                    EmailChange = new
                    {
                        NewEmail = WaitingAddress,
                        CodeHash = "fake:email-change",
                        ExpiresAt = now,
                    },
                }
            );
        });
    }

    private Task<User?> UserAsync(Guid id)
    {
        return Factory.QueryAsync(db =>
            db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == UserId.From(id), Ct)
        );
    }

    [Fact]
    public async Task CleanUpAsyncDropsTheExpiredChangesAndBlocksOnlyTheAddress()
    {
        await SeedChangesAsync();
        await Factory.QueryAsync(db => db.DataHistory.ExecuteDeleteAsync(Ct));

        (await CleanUpAsync()).Should().Be(1);

        var member = await UserAsync(TestSeedData.Users.MemberId);
        member!.EmailChange.Should().Be(default(EmailChange));
        member.Authenticator.PendingKey.Should().BeNull();
        member.Authenticator.PendingExpiresAt.Should().BeNull();
        (await UserAsync(TestSeedData.Users.AdminId))!
            .EmailChange.NewEmail?.Value.Should()
            .Be(WaitingAddress, "its code can still confirm it");

        var history = await DataHistoryAsync();
        var record = history.Should().ContainSingle().Which.Record;
        record.Operation.Should().Be(DataHistoryOperation.Modified);
        record.Command.Should().Be("DiscardExpiredAccountChanges");
        ((JsonElement)record.Values[$"{nameof(User.EmailChange)}.{nameof(EmailChange.NewEmail)}"]!)
            .GetString()
            .Should()
            .Be(ExpiredAddress);
        record
            .Values.Keys.Should()
            .NotContain(key =>
                key.StartsWith(nameof(User.Authenticator), StringComparison.Ordinal)
                || key.EndsWith(nameof(EmailChange.CodeHash), StringComparison.Ordinal)
            );

        (await CleanUpAsync()).Should().Be(0);
    }
}
