using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.BlockedData.Queries;
using CodigoActivo.Composition.BlockedData;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Activities;
using CodigoActivo.Infrastructure.BlockedData;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.Infrastructure.EventCategories;
using CodigoActivo.Infrastructure.Events;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.Infrastructure.Legal;
using CodigoActivo.Infrastructure.Reports;
using CodigoActivo.Infrastructure.TermsDocuments;
using CodigoActivo.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class DataHistoryConventionTests
{
    private static readonly string[] BulkWrites =
    [
        "ExecuteUpdate",
        "ExecuteUpdateAsync",
        "ExecuteDelete",
        "ExecuteDeleteAsync",
    ];

    private static readonly string[] RawSqlPrefixes = ["ExecuteSql", "SqlQuery", "FromSql"];

    private static readonly Type[] ReviewedRawSqlUsers =
    [
        typeof(AccountErasureStore),
        typeof(BlockedDataConsultationRepository),
        typeof(DashboardCountsReader),
        typeof(EmailOutboxStore),
        typeof(EventCategoryTypeRepository),
        typeof(EventRepository),
        typeof(LegalPolicyRepository),
        typeof(ParticipationErasureStore),
        typeof(StoredFileRepository),
        typeof(TermsDocumentRepository),
        typeof(UserRepository),
    ];

    private static HashSet<Type> HistorizedTypes()
    {
        using var context = new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql("Host=localhost;Database=unused")
                .UseSnakeCaseNamingConvention()
                .Options
        );
        return context
            .Model.GetEntityTypes()
            .Where(type => type.IsHistorized())
            .Select(type => type.ClrType)
            .ToHashSet();
    }

    private static IEnumerable<(Type Owner, MethodInfo Called)> EntityFrameworkCalls()
    {
        return ProductionAssemblies.AllTypes.SelectMany(type =>
            IlReferences
                .MethodsOf(type)
                .SelectMany(IlReferences.MembersInBody)
                .OfType<MethodInfo>()
                .Where(method =>
                    method.DeclaringType?.Namespace?.StartsWith(
                        "Microsoft.EntityFrameworkCore",
                        StringComparison.Ordinal
                    )
                        is true
                )
                .Select(method => (IlReferences.OwnerOf(type), method))
        );
    }

    [Fact]
    public void BulkWritesNeverTouchAHistorizedEntity()
    {
        var historized = HistorizedTypes();

        var bulkWrites = EntityFrameworkCalls()
            .Where(call => BulkWrites.Contains(call.Called.Name) && call.Called.IsGenericMethod)
            .Select(call => (call.Owner, Target: call.Called.GetGenericArguments()[0]))
            .Where(call => call.Target.IsGenericParameter || historized.Contains(call.Target))
            .Distinct()
            .ToList();

        bulkWrites
            .Should()
            .Equal(
                [(typeof(UserRepository), typeof(User))],
                "the data history only sees the change tracker; the password state that "
                    + "UserRepository writes in bulk is excluded from it"
            );
    }

    [Fact]
    public void DataHistoryIsOnlyReadByTheConsultationsOfTheBlockedData()
    {
        var readers = ProductionAssemblies
            .AllTypes.Where(type =>
                IlReferences
                    .TypesUsedBy(type)
                    .SelectMany(IlReferences.Expand)
                    .Contains(typeof(IDataHistoryReader))
            )
            .Select(IlReferences.OwnerOf)
            .Where(type =>
                type != typeof(DataHistoryReader) && type != typeof(BlockedDataRegistration)
            )
            .Distinct();

        readers
            .Should()
            .BeEquivalentTo(
                [
                    typeof(SearchBlockedDataCommandHandler),
                    typeof(ExportBlockedDataCommandHandler),
                    typeof(GetBlockedDataAccessQueryHandler),
                ],
                "the data history is blocked (article 32 LOPDGDD): only a consultation reads it"
            );
    }

    [Fact]
    public void RawSqlIsOnlyUsedByTheReviewedTypes()
    {
        var users = EntityFrameworkCalls()
            .Where(call =>
                RawSqlPrefixes.Any(prefix =>
                    call.Called.Name.StartsWith(prefix, StringComparison.Ordinal)
                )
            )
            .Select(call => call.Owner)
            .Distinct();

        users
            .Should()
            .BeEquivalentTo(
                ReviewedRawSqlUsers,
                "raw SQL bypasses the data history, so a new user of it must only lock or read "
                    + "rows, or write tables that keep no history, and be added here once reviewed"
            );
    }
}
