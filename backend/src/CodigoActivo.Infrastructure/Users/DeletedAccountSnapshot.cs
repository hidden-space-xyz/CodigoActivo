using System.Text.Json;
using System.Text.Json.Serialization;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Reads what the erasure of an account removes, lets <see cref="LegalCopy.Compose"/> decide what
/// the copy keeps, and serializes it as the JSON stored in <see cref="DeletedAccount.Data"/>, with
/// the version of its layout.
/// </summary>
internal static class DeletedAccountSnapshot
{
    /// <summary>
    /// Version of the JSON layout, stored in every copy.
    /// </summary>
    internal const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        MaxDepth = 128,
        Converters = { new JsonStringEnumConverter(), new TermsDocumentConverter() },
    };

    /// <summary>
    /// Reads everything the erasure of the user removes and serializes the copy. Call it inside the
    /// erasing transaction, after locking the household, so nothing changes in between.
    /// </summary>
    /// <param name="context">Database context of the erasing transaction.</param>
    /// <param name="userId">Identifier of the user being erased.</param>
    /// <param name="erasure">Who asked for the deletion.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the serialized copy.</returns>
    internal static async Task<string> BuildAsync(
        CodigoActivoDbContext context,
        UserId userId,
        AccountErasure erasure,
        CancellationToken ct
    )
    {
        var people = await (
            from u in context.Users.AsNoTracking()
            join type in context.UserTypes.AsNoTracking() on u.UserType equals type.Id
            join status in context.UserStatusTypes.AsNoTracking() on u.Status equals status.Id
            where u.Id == userId || u.ParentId == userId
            select new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.SecondaryPhone,
                u.NationalId,
                u.BirthDate,
                u.Gender,
                u.PromotionalConsent,
                u.UserType,
                TypeName = type.Name,
                u.Status,
                StatusName = status.Name,
                u.IsAdmin,
                u.TwoFactorMethod,
                u.ParentId,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt,
            }
        ).ToListAsync(ct);
        var household = people.ConvertAll(row => new LegalCopyPerson(
            row.Id.Value,
            row.FirstName,
            row.LastName,
            row.Email?.Value,
            row.Phone?.Value,
            row.SecondaryPhone?.Value,
            row.NationalId?.Value,
            row.BirthDate,
            row.Gender,
            row.PromotionalConsent,
            CatalogIds.UserTypes.IdOf(row.UserType),
            row.TypeName,
            CatalogIds.UserStatuses.IdOf(row.Status),
            row.StatusName,
            row.IsAdmin,
            row.TwoFactorMethod,
            row.ParentId?.Value,
            row.CreatedAt,
            row.UpdatedAt,
            row.LastLoginAt
        ));
        var householdIds = people.ConvertAll(row => row.Id);
        var guardianId = people.Single(row => row.Id == userId).ParentId;

        var guardianRow = guardianId is null
            ? null
            : await context
                .Users.AsNoTracking()
                .Where(u => u.Id == guardianId)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.NationalId,
                    u.Email,
                    u.Phone,
                })
                .SingleAsync(ct);
        var guardian = guardianRow is null
            ? null
            : new LegalCopyGuardian(
                guardianRow.Id.Value,
                guardianRow.FirstName,
                guardianRow.LastName,
                guardianRow.NationalId?.Value,
                guardianRow.Email?.Value,
                guardianRow.Phone?.Value
            );

        var signupRows = await (
            from activity in context.Activities.AsNoTracking()
            from assignment in activity.Assignments
            join modality in context.ActivityModalityTypes.AsNoTracking()
                on activity.Modality equals modality.Id
            join role in context.ActivityRoleTypes.AsNoTracking() on assignment.Role equals role.Id
            join status in context.AssignmentStatusTypes.AsNoTracking()
                on assignment.Status equals status.Id
            where householdIds.Contains(assignment.UserId)
            select new
            {
                activity.EventId,
                assignment.ActivityId,
                activity.Title,
                activity.Location,
                ModalityName = modality.Name,
                StartsAt = EF.Property<DateTimeOffset>(activity, ScheduleColumns.ActivityStartsAt),
                EndsAt = EF.Property<DateTimeOffset>(activity, ScheduleColumns.ActivityEndsAt),
                assignment.UserId,
                RoleName = role.Name,
                StatusName = status.Name,
                assignment.CreatedAt,
            }
        ).ToListAsync(ct);
        var signups = signupRows.ConvertAll(row => new EventSignup(
            row.EventId.Value,
            new LegalCopyActivity(
                row.ActivityId.Value,
                row.Title,
                row.Location,
                row.ModalityName,
                row.StartsAt,
                row.EndsAt,
                row.UserId.Value,
                row.RoleName,
                row.StatusName,
                row.CreatedAt
            )
        ));

        var decisionRows = await (
            from acceptance in context.EventTermsAcceptances.AsNoTracking()
            join document in context.TermsDocuments.AsNoTracking()
                on acceptance.TermsDocumentId equals document.Id
            where householdIds.Contains(acceptance.UserId) || acceptance.UserId == guardianId
            select new
            {
                acceptance.EventId,
                acceptance.TermsDocumentId,
                DocumentName = document.Name,
                acceptance.Accepted,
                acceptance.DecidedAt,
                acceptance.UserId,
            }
        ).ToListAsync(ct);
        var decisions = decisionRows.ConvertAll(row => new EventDecision(
            row.EventId.Value,
            new LegalCopyDecision(
                row.TermsDocumentId.Value,
                row.DocumentName,
                row.Accepted,
                row.DecidedAt,
                row.UserId.Value
            )
        ));

        var documentIds = decisionRows.Select(row => row.TermsDocumentId).Distinct().ToList();
        var documents = (
            await context
                .TermsDocuments.AsNoTracking()
                .Where(d => documentIds.Contains(d.Id))
                .ToListAsync(ct)
        ).ConvertAll(d => new LegalCopyTermsDocument(d.Id.Value, d.Name, d.Description.Json));

        var eventIds = signupRows
            .Select(row => row.EventId)
            .Union(decisionRows.Select(row => row.EventId))
            .ToList();
        var eventRows = await context
            .Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.Title,
                StartsAt = EF.Property<DateOnly>(e, ScheduleColumns.EventStartsAt),
                EndsAt = EF.Property<DateOnly>(e, ScheduleColumns.EventEndsAt),
            })
            .ToListAsync(ct);
        var events = eventRows.ConvertAll(row => new LegalCopyEvent(
            row.Id.Value,
            row.Title,
            row.StartsAt,
            row.EndsAt,
            new List<LegalCopyDecision>(),
            new List<LegalCopyActivity>()
        ));

        var copy = LegalCopy.Compose(
            userId.Value,
            erasure,
            household,
            guardian,
            [.. signups.Select(s => (s.EventId, s.Activity))],
            [.. decisions.Select(d => (d.EventId, d.Decision))],
            documents,
            events
        );

        return JsonSerializer.Serialize(
            new Snapshot(
                SchemaVersion,
                copy.Deletion,
                copy.Account,
                copy.Guardian,
                copy.Dependents,
                copy.TermsDocuments,
                copy.Events
            ),
            SerializerOptions
        );
    }

    private sealed record EventSignup(Guid EventId, LegalCopyActivity Activity);

    private sealed record EventDecision(Guid EventId, LegalCopyDecision Decision);

    private sealed record Snapshot(
        int SchemaVersion,
        LegalCopyDeletion Deletion,
        LegalCopyPerson Account,
        LegalCopyGuardian? Guardian,
        IReadOnlyList<LegalCopyPerson> Dependents,
        IReadOnlyList<LegalCopyTermsDocument> TermsDocuments,
        IReadOnlyList<LegalCopyEvent> Events
    );

    private sealed class TermsDocumentConverter : JsonConverter<LegalCopyTermsDocument>
    {
        public override LegalCopyTermsDocument Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            throw new NotSupportedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            LegalCopyTermsDocument value,
            JsonSerializerOptions options
        )
        {
            writer.WriteStartObject();
            writer.WriteString("id", value.Id);
            writer.WriteString("name", value.Name);
            writer.WritePropertyName("textAtDeletion");
            using var document = JsonDocument.Parse(value.TextAtDeletion);
            document.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }
    }
}
