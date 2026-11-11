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
        $"{nameof(User.EmailChange)}.{nameof(EmailChange.CodeHash)}",
        $"{nameof(User.EmailChange)}.{nameof(EmailChange.ExpiresAt)}",
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
        var excluded = ExcludedMembers(context.Model.FindEntityType(typeof(User))!, prefix: "");

        excluded
            .Should()
            .BeEquivalentTo(
                ExcludedUserProperties,
                "passwords, codes, authenticator secrets, lockouts and logins never reach the data "
                    + "history; every other user property does, the address an email change asks "
                    + "for included"
            );
    }

    [Fact]
    public void ModelAccountsAHistoryEntryIsNotAboutAreTheRecordersOfDecisions()
    {
        var notSubjects = context
            .Model.GetEntityTypes()
            .Where(type => type.IsHistorized())
            .SelectMany(type => type.GetProperties())
            .Where(property =>
                (property.ClrType == typeof(UserId) || property.ClrType == typeof(UserId?))
                && !property.IsHistorySubject()
            )
            .Select(property => $"{property.DeclaringType.ClrType.Name}.{property.Name}");

        notSubjects
            .Should()
            .BeEquivalentTo(
                [$"{nameof(TermsDecisionChange)}.{nameof(TermsDecisionChange.RecordedById)}"],
                "every account a historized row holds is what its entries are about, so extracts and "
                    + "holds find them, except the account that only recorded a decision about "
                    + "someone else"
            );
    }

    private static IEnumerable<string> ExcludedMembers(IReadOnlyTypeBase type, string prefix)
    {
        var complex = type.GetComplexProperties().ToList();
        return type.GetProperties()
            .Cast<IReadOnlyPropertyBase>()
            .Concat(complex)
            .Where(property => property.IsExcludedFromHistory())
            .Select(property => prefix + property.Name)
            .Concat(
                complex
                    .Where(property => !property.IsExcludedFromHistory())
                    .SelectMany(property =>
                        ExcludedMembers(property.ComplexType, $"{prefix}{property.Name}.")
                    )
            );
    }
}
