using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed class DataHistoryModelTests : IDisposable
{
    private const string Decide =
        "every entity declares HasHistory() or HasNoHistory() in its configuration; a historized "
        + "entity holds personal data or what its liabilities need, and its rows leave a data history "
        + "when they are changed or deleted";

    private static readonly Type[] Historized =
    [
        typeof(Activity),
        typeof(Assignment),
        typeof(Event),
        typeof(EventRating),
        typeof(EventTermsAcceptance),
        typeof(NewsConsentChange),
        typeof(NewsItem),
        typeof(Partner),
        typeof(Resource),
        typeof(StoredFile),
        typeof(TermsDecisionChange),
        typeof(TermsDocument),
        typeof(TermsDocumentVersion),
        typeof(User),
    ];

    private static readonly string[] ExcludedUserProperties =
    [
        UserConfiguration.RowVersion,
        nameof(User.Authenticator),
        nameof(User.EmailChange),
        nameof(User.LastLoginAt),
        nameof(User.LoginChallenge),
        nameof(User.LoginCode),
        nameof(User.PasswordHash),
        nameof(User.PasswordLockout),
        nameof(User.PasswordResetCode),
        nameof(User.TwoFactorLockout),
        nameof(User.VerificationCode),
    ];

    private readonly CodigoActivoDbContext context = new(
        new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options
    );

    public void Dispose()
    {
        context.Dispose();
    }

    [Fact]
    public void ModelEveryEntityTypeDeclaresItsHistory()
    {
        var undeclared = context
            .Model.GetEntityTypes()
            .Where(type => type.DeclaredHistory() is null)
            .Select(type => type.ClrType.Name);

        undeclared.Should().BeEmpty(Decide);
    }

    [Fact]
    public void ModelHistorizedEntitiesAreTheDecidedOnes()
    {
        var historized = context
            .Model.GetEntityTypes()
            .Where(type => type.IsHistorized())
            .Select(type => type.ClrType);

        historized.Should().BeEquivalentTo(Historized, Decide);
    }

    [Fact]
    public void ModelHistorizedRowsAreNeverDeletedByADatabaseCascade()
    {
        var cascades = context
            .Model.GetEntityTypes()
            .Where(type => type.IsHistorized())
            .SelectMany(type => type.GetForeignKeys())
            .Where(foreignKey => foreignKey.DeleteBehavior is DeleteBehavior.Cascade)
            .Select(foreignKey =>
                $"{foreignKey.DeclaringEntityType.ClrType.Name}.{string.Join(",", foreignKey.Properties.Select(p => p.Name))}"
            );

        cascades
            .Should()
            .BeEmpty(
                "the data history only sees the change tracker, so a historized dependent uses "
                    + "DeleteBehavior.ClientCascade and is loaded before its principal is deleted"
            );
    }

    [Fact]
    public void UserHistoryLeavesOutCredentialsAndSecurityState()
    {
        var user = context.Model.FindEntityType(typeof(User))!;
        var excluded = user.GetProperties()
            .Cast<IReadOnlyPropertyBase>()
            .Concat(user.GetComplexProperties())
            .Where(property => property.IsExcludedFromHistory())
            .Select(property => property.Name);

        excluded
            .Should()
            .BeEquivalentTo(
                ExcludedUserProperties,
                "passwords, codes, authenticator secrets, unconfirmed email changes, lockouts and "
                    + "logins never reach the data history; every other user property does"
            );
    }
}
