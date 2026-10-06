using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class DeletedAccountCoverageTests : IDisposable
{
    private const string Decide =
        "every new column or table about a user must be classified here and, when it is copied, "
        + "added to LegalCopyPerson, to the projections of DeletedAccountSnapshot and to "
        + "AccountErasureTests; otherwise leave it out on purpose in LegalCopy.ExcludedUserProperties "
        + "or hand it over to the initial administrator in AccountErasureStore and AccountErasureTests";

    private static readonly string[] CopiedReferences =
    [
        "Assignment.UserId",
        "EventTermsAcceptance.UserId",
        "User.ParentId",
    ];

    private static readonly string[] TechnicalReferences = ["UserSession.UserId"];

    private static readonly string[] HandedOverReferences =
    [
        "Activity.CreatedBy",
        "Activity.UpdatedBy",
        "Event.CreatedBy",
        "Event.UpdatedBy",
        "StoredFile.UploadedBy",
        "NewsItem.CreatedBy",
        "NewsItem.UpdatedBy",
        "Partner.CreatedBy",
        "Partner.UpdatedBy",
        "Resource.CreatedBy",
        "Resource.UpdatedBy",
    ];

    private static readonly string[] CopiedAssignmentProperties =
    [
        nameof(Assignment.UserId),
        nameof(Assignment.ActivityId),
        nameof(Assignment.Role),
        nameof(Assignment.Status),
        nameof(Assignment.CreatedAt),
    ];

    private static readonly string[] CopiedDecisionProperties =
    [
        nameof(EventTermsAcceptance.EventId),
        nameof(EventTermsAcceptance.UserId),
        nameof(EventTermsAcceptance.TermsDocumentId),
        nameof(EventTermsAcceptance.Accepted),
        nameof(EventTermsAcceptance.DecidedAt),
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

    private IEntityType Entity<T>()
    {
        return context.Model.FindEntityType(typeof(T))!;
    }

    private IEnumerable<string> PropertiesOf<T>()
    {
        return Entity<T>().GetProperties().Select(property => property.Name);
    }

    private static readonly Dictionary<string, string> UserPropertyOfCatalogId = new(
        StringComparer.Ordinal
    )
    {
        [nameof(LegalCopyPerson.UserTypeId)] = nameof(User.UserType),
        [nameof(LegalCopyPerson.UserStatusTypeId)] = nameof(User.Status),
    };

    private static List<string> CopiedUserProperties()
    {
        return
        [
            .. typeof(LegalCopyPerson)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Except(LegalCopyPerson.CatalogNameProperties)
                .Select(name => UserPropertyOfCatalogId.GetValueOrDefault(name, name)),
        ];
    }

    private static User NewUser(Guid? id = null, Guid? parentId = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = id ?? Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "Gil",
                ParentId = parentId,
            }
        );
    }

    [Fact]
    public void EveryUserPropertyIsEitherCopiedOrDeliberatelyLeftOut()
    {
        PropertiesOf<User>()
            .Should()
            .BeEquivalentTo(
                [.. CopiedUserProperties(), .. LegalCopy.ExcludedUserProperties],
                Decide
            );
        CopiedUserProperties().Intersect(LegalCopy.ExcludedUserProperties).Should().BeEmpty();
    }

    [Fact]
    public void CredentialsAndOneTimeCodesAreNeverCopied()
    {
        CopiedUserProperties()
            .Should()
            .NotContain(name =>
                name.EndsWith("Hash", StringComparison.Ordinal)
                || name.EndsWith("Key", StringComparison.Ordinal)
                || name.Contains("Challenge", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void EveryForeignKeyToUsersIsCopiedHandedOverOrTechnical()
    {
        var references = Entity<User>()
            .GetReferencingForeignKeys()
            .SelectMany(foreignKey =>
                foreignKey.Properties.Select(property =>
                    $"{foreignKey.DeclaringEntityType.ClrType.Name}.{property.Name}"
                )
            );

        references
            .Should()
            .BeEquivalentTo(
                [.. CopiedReferences, .. TechnicalReferences, .. HandedOverReferences],
                Decide
            );
    }

    [Fact]
    public void EveryAssignmentAndTermsDecisionPropertyIsCopied()
    {
        PropertiesOf<Assignment>().Should().BeEquivalentTo(CopiedAssignmentProperties, Decide);
        PropertiesOf<EventTermsAcceptance>()
            .Should()
            .BeEquivalentTo(CopiedDecisionProperties, Decide);
    }

    [Fact]
    public void SaveChangesRemovingAUserWithoutItsCopyIsRefusedBeforeTouchingTheDatabase()
    {
        var user = NewUser();
        context.Users.Attach(user);
        context.Users.Remove(user);

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*EraseAsync*");
    }

    [Fact]
    public async Task SaveChangesAsyncRemovingAUserWithoutItsCopyIsRefused()
    {
        var user = NewUser();
        context.Users.Attach(user);
        context.Users.Remove(user);

        var save = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EraseAsync*");
    }

    [Fact]
    public void SaveChangesRemovingADependentWithoutAnyCopyIsRefused()
    {
        var dependent = NewUser(parentId: Guid.NewGuid());
        context.Users.Attach(dependent);
        context.Users.Remove(dependent);
        context.DeletedAccounts.Add(Persisted.As<DeletedAccount>(new { Id = Guid.NewGuid() }));

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*EraseAsync*");
    }

    [Fact]
    public void SaveChangesDeletingTheInitialAdministratorIsRefusedEvenWithItsCopy()
    {
        var administrator = NewUser(id: KnownIds.Users.InitialAdministrator);
        context.Users.Attach(administrator);
        context.Users.Remove(administrator);
        context.DeletedAccounts.Add(Persisted.As<DeletedAccount>(new { Id = administrator.Id }));

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*initial administrator*");
    }

    [Fact]
    public void SaveChangesChangingAStoredCopyIsRefused()
    {
        var copy = Persisted.As<DeletedAccount>(new { Id = Guid.NewGuid() });
        context.DeletedAccounts.Attach(copy);
        Persisted.Overwrite(copy, new { Data = "{\"schemaVersion\":1}" });

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*retention purge*");
    }

    [Fact]
    public void SaveChangesDeletingAStoredCopyIsRefused()
    {
        var copy = Persisted.As<DeletedAccount>(new { Id = Guid.NewGuid() });
        context.DeletedAccounts.Attach(copy);
        context.DeletedAccounts.Remove(copy);

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*retention purge*");
    }

    [Fact]
    public async Task UserRepositoryRemoveWithoutTheCopyIsRefusedOnSave()
    {
        var user = NewUser();
        context.Users.Attach(user);
        new UserRepository(context).Remove(user);

        var save = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EraseAsync*");
    }
}
