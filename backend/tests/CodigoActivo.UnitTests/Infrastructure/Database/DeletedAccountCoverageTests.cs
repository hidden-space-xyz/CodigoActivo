using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class DeletedAccountCoverageTests : IDisposable
{
    private const string Decide =
        "every new column or table about a user must be classified here and, when it is copied, "
        + "added to the projections of DeletedAccountSnapshot and to DeletedAccountRepositoryTests; "
        + "otherwise leave it out on purpose or hand it over to the initial administrator in "
        + "DeletedAccountRepository and DeletedAccountRepositoryTests";

    private static readonly string[] CopiedReferences =
    [
        "ActivityUserRoleAssignment.UserId",
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
        "FileEntity.UploadedBy",
        "NewsItem.CreatedBy",
        "NewsItem.UpdatedBy",
        "Partner.CreatedBy",
        "Partner.UpdatedBy",
        "Resource.CreatedBy",
        "Resource.UpdatedBy",
    ];

    private static readonly string[] CopiedAssignmentProperties =
    [
        nameof(ActivityUserRoleAssignment.UserId),
        nameof(ActivityUserRoleAssignment.ActivityId),
        nameof(ActivityUserRoleAssignment.ActivityRoleTypeId),
        nameof(ActivityUserRoleAssignment.AssignmentStatusId),
        nameof(ActivityUserRoleAssignment.CreatedAt),
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

    private static List<string> CopiedUserProperties()
    {
        return
        [
            .. typeof(DeletedAccountSnapshot.PersonCopy)
                .GetProperties()
                .Select(property => property.Name)
                .Except(DeletedAccountSnapshot.CatalogNames),
        ];
    }

    private static User NewUser(Guid? parentId = null)
    {
        return new User
        {
            FirstName = "Ana",
            LastName = "Gil",
            ParentId = parentId,
        };
    }

    [Fact]
    public void EveryUserPropertyIsEitherCopiedOrDeliberatelyLeftOut()
    {
        PropertiesOf<User>()
            .Should()
            .BeEquivalentTo(
                [.. CopiedUserProperties(), .. DeletedAccountSnapshot.ExcludedUserProperties],
                Decide
            );
        CopiedUserProperties()
            .Intersect(DeletedAccountSnapshot.ExcludedUserProperties)
            .Should()
            .BeEmpty();
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
        PropertiesOf<ActivityUserRoleAssignment>()
            .Should()
            .BeEquivalentTo(CopiedAssignmentProperties, Decide);
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
        context.DeletedAccounts.Add(new DeletedAccount { Id = Guid.NewGuid() });

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*EraseAsync*");
    }

    [Fact]
    public void SaveChangesDeletingTheInitialAdministratorIsRefusedEvenWithItsCopy()
    {
        var administrator = NewUser();
        administrator.Id = SeedIds.Users.InitialAdministrator;
        context.Users.Attach(administrator);
        context.Users.Remove(administrator);
        context.DeletedAccounts.Add(new DeletedAccount { Id = administrator.Id });

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*initial administrator*");
    }

    [Fact]
    public void SaveChangesChangingAStoredCopyIsRefused()
    {
        var copy = new DeletedAccount { Id = Guid.NewGuid() };
        context.DeletedAccounts.Attach(copy);
        copy.Data = "{\"schemaVersion\":1}";

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*retention purge*");
    }

    [Fact]
    public void SaveChangesDeletingAStoredCopyIsRefused()
    {
        var copy = new DeletedAccount { Id = Guid.NewGuid() };
        context.DeletedAccounts.Attach(copy);
        context.DeletedAccounts.Remove(copy);

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*retention purge*");
    }

    [Fact]
    public async Task UserRepositoryRemoveAsyncAlwaysThrows()
    {
        var users = new UserRepository(context);

        var remove = () => users.RemoveAsync(_ => true, TestContext.Current.CancellationToken);

        await remove.Should().ThrowAsync<NotSupportedException>().WithMessage("*EraseAsync*");
    }
}
