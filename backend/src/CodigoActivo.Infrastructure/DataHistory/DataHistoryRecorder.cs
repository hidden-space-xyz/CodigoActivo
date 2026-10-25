using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Keeps, encrypted, the previous state of every row of a historized entity that a save changes or
/// deletes, in that same save and so in the same transaction: a change never commits without its
/// history. Additions record nothing, since the row itself is the record, and neither does a
/// change of properties excluded from history alone. It also refuses any change to a stored entry;
/// the retention purge is the only thing that removes them. It only sees the change tracker, so
/// historized rows are never deleted by a database cascade nor written in bulk.
/// </summary>
/// <param name="protector">Protector that encrypts the records.</param>
/// <param name="currentUser">Signed-in user, if any.</param>
/// <param name="commandScope">Running command, if any.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DataHistoryRecorder(
    DataHistoryProtector protector,
    ICurrentUser currentUser,
    ICommandScope commandScope,
    IClock clock
) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Record(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Record(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        context.ChangeTracker.CascadeChanges();
        var now = clock.UtcNow;
        var entries = new List<DataHistoryEntry>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (
                entry.Entity is DataHistoryEntry
                && entry.State is EntityState.Modified or EntityState.Deleted
            )
            {
                throw new InvalidOperationException(
                    "A data history entry cannot be changed or deleted; only the retention purge removes it."
                );
            }

            if (OperationOf(entry) is { } operation)
            {
                entries.Add(Capture(entry, operation, now));
            }
        }

        context.AddRange(entries);
    }

    private static DataHistoryOperation? OperationOf(EntityEntry entry)
    {
        if (!entry.Metadata.IsHistorized())
        {
            return null;
        }

        return entry.State switch
        {
            EntityState.Deleted => DataHistoryOperation.Deleted,
            EntityState.Modified
                when RecordedMembers(entry).Any(member => member.Entry.IsModified) =>
                DataHistoryOperation.Modified,
            _ => null,
        };
    }

    private DataHistoryEntry Capture(
        EntityEntry entry,
        DataHistoryOperation operation,
        DateTimeOffset now
    )
    {
        var id = Guid.CreateVersion7(now);
        var record = new DataHistoryRecord(
            operation,
            currentUser.Id?.Value,
            commandScope.Command,
            RecordedMembers(entry)
                .ToDictionary(
                    member => member.Name,
                    member => StoredValue(member.Entry.Metadata, member.Entry.OriginalValue),
                    StringComparer.Ordinal
                )
        );
        return new DataHistoryEntry
        {
            Id = id,
            Entity = entry.Metadata.ClrType.Name,
            OccurredAt = now,
            SubjectIds = [.. SubjectsOf(entry).OfType<Guid>().Distinct()],
            Payload = protector.Protect(id, record),
        };
    }

    private static IEnumerable<(string Name, PropertyEntry Entry)> RecordedMembers(
        EntityEntry entry
    )
    {
        return entry
            .Properties.Where(property => !property.Metadata.IsExcludedFromHistory())
            .Select(property => (property.Metadata.Name, property))
            .Concat(
                entry
                    .ComplexProperties.Where(complex => !complex.Metadata.IsExcludedFromHistory())
                    .SelectMany(complex => MembersOf(complex, complex.Metadata.Name))
            );
    }

    private static IEnumerable<(string Name, PropertyEntry Entry)> MembersOf(
        ComplexPropertyEntry complex,
        string prefix
    )
    {
        return complex
            .Properties.Where(property => !property.Metadata.IsExcludedFromHistory())
            .Select(property => ($"{prefix}.{property.Metadata.Name}", property))
            .Concat(
                complex
                    .ComplexProperties.Where(nested => !nested.Metadata.IsExcludedFromHistory())
                    .SelectMany(nested => MembersOf(nested, $"{prefix}.{nested.Metadata.Name}"))
            );
    }

    private static IEnumerable<Guid?> SubjectsOf(EntityEntry entry)
    {
        return entry
            .Metadata.GetProperties()
            .Where(property =>
                property.ClrType == typeof(UserId) || property.ClrType == typeof(UserId?)
            )
            .Select(property =>
                StoredValue(property, entry.Property(property.Name).OriginalValue) as Guid?
            );
    }

    private static object? StoredValue(IProperty property, object? value)
    {
        return value is not null && property.GetTypeMapping().Converter is { } converter
            ? converter.ConvertToProvider(value)
            : value;
    }
}
