using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Logs;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class LogEntryReaderTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset Morning = new(2026, 7, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid MemberId = TestSeedData.Users.MemberId;
    private static readonly Guid StaffId = TestSeedData.Users.AdminId;

    private static AuditLogEntry Audit(
        DateTimeOffset occurredAt,
        string entityType,
        Guid entityId,
        Guid? actor = null,
        string? clientIp = null
    )
    {
        return new AuditLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            ActorType = actor is null ? "System" : "User",
            ActorId = actor,
            ClientIp = clientIp is null ? null : IPAddress.Parse(clientIp),
            Action = nameof(AuditAction.Login),
            EntityType = entityType,
            EntityId = entityId,
            Outcome = nameof(AuditOutcome.Succeeded),
        };
    }

    private static DiagnosticLogEntry Diagnostic(DateTimeOffset occurredAt, string message)
    {
        return new DiagnosticLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            Level = 3,
            Category = "CodigoActivo.Tests",
            Message = message,
        };
    }

    private async Task SeedAsync(params object[] entries)
    {
        await using (
            var connection = await Factory
                .Services.GetRequiredService<LogDatabase>()
                .OpenConnectionAsync(Ct)
        )
        {
            var maintainer = Factory.Services.GetRequiredService<LogPartitionMaintainer>();
            foreach (var table in LogTable.All)
            {
                await maintainer.EnsurePartitionsAsync(
                    connection,
                    table,
                    [new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 4)],
                    Ct
                );
            }
        }

        await Factory.SeedAsync(db =>
        {
            db.AddRange(entries);
            return Task.CompletedTask;
        });
    }

    private async Task<T> ReadAsync<T>(Func<ILogEntryReader, Task<T>> read)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<ILogEntryReader>());
    }

    [Fact]
    public async Task ListAuditAsyncNamesTheContentEachEntryActedOn()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var termsId = await SeedTermsDocumentAsync("Condiciones del campamento");
        var newsId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.News.Add(
                TestEntities.New<NewsItem>(
                    new
                    {
                        Id = newsId,
                        Title = "Nueva sede",
                        ThumbnailId = thumbnailId,
                        CreatedAt,
                    }
                )
            );
            db.Partners.Add(
                Persisted.As<Partner>(
                    new
                    {
                        Id = partnerId,
                        Name = "Imprenta Sur",
                        Tier = 1,
                        FromDate = new DateOnly(2024, 1, 1),
                        ThumbnailId = thumbnailId,
                        CreatedAt,
                        CreatedBy = StaffId,
                    }
                )
            );
            db.Resources.Add(
                Persisted.As<Resource>(
                    new
                    {
                        Id = resourceId,
                        Title = "Guía del monitor",
                        Subtitle = "Sub",
                        Description = "{}",
                        Url = "https://guia.example.org",
                        ResourceType = ResourceType.External,
                        ThumbnailId = thumbnailId,
                        CreatedAt,
                        CreatedBy = StaffId,
                    }
                )
            );
            return Task.CompletedTask;
        });
        await SeedAsync(
            Audit(Morning, nameof(AuditEntityType.NewsItem), newsId),
            Audit(Morning.AddMinutes(1), nameof(AuditEntityType.Partner), partnerId),
            Audit(Morning.AddMinutes(2), nameof(AuditEntityType.Resource), resourceId),
            Audit(Morning.AddMinutes(3), nameof(AuditEntityType.StoredFile), thumbnailId),
            Audit(Morning.AddMinutes(4), nameof(AuditEntityType.TermsDocument), termsId)
        );

        var page = await ReadAsync(reader =>
            reader.ListAuditAsync(
                new AuditLogEntryListQuery { Sort = "occurredAt" },
                new LogPeriod(null, null),
                Ct
            )
        );

        page.Items.Select(entry => entry.EntityName)
            .Should()
            .Equal(
                "Nueva sede",
                "Imprenta Sur",
                "Guía del monitor",
                thumbnailId.ToString(),
                "Condiciones del campamento"
            );
    }

    [Fact]
    public async Task ListAuditAsyncPeriodKeepsTheEntriesFromItsStart()
    {
        await SeedAsync(
            Audit(Morning.AddDays(-1), nameof(AuditEntityType.User), MemberId),
            Audit(Morning, nameof(AuditEntityType.User), MemberId)
        );

        var page = await ReadAsync(reader =>
            reader.ListAuditAsync(new AuditLogEntryListQuery(), new LogPeriod(Morning, null), Ct)
        );

        page.Items.Should().ContainSingle().Which.OccurredAt.Should().Be(Morning);
    }

    [Fact]
    public async Task ListDiagnosticsAsyncPeriodKeepsTheEntriesBeforeItsEnd()
    {
        await SeedAsync(Diagnostic(Morning.AddDays(-1), "Ayer"), Diagnostic(Morning, "Hoy"));

        var page = await ReadAsync(reader =>
            reader.ListDiagnosticsAsync(
                new DiagnosticLogEntryListQuery(),
                new LogPeriod(null, Morning),
                Ct
            )
        );

        page.Items.Should().ContainSingle().Which.Message.Should().Be("Ayer");
    }

    [Fact]
    public async Task ListAccountAuditAsyncHidesTheAddressOnlyOfWhatAnotherPersonDid()
    {
        await SeedAsync(
            Audit(Morning, nameof(AuditEntityType.User), MemberId, MemberId, "203.0.113.1"),
            Audit(
                Morning.AddMinutes(1),
                nameof(AuditEntityType.User),
                MemberId,
                StaffId,
                "203.0.113.2"
            ),
            Audit(
                Morning.AddMinutes(2),
                nameof(AuditEntityType.User),
                MemberId,
                null,
                "203.0.113.3"
            )
        );

        var entries = await ReadAsync(reader =>
            reader.ListAccountAuditAsync(MemberId, [MemberId], Ct)
        );

        entries
            .Select(entry => (entry.RanBySelf, entry.ClientIp))
            .Should()
            .Equal((true, "203.0.113.1"), (false, null), (false, "203.0.113.3"));
    }
}
