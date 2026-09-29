using System.Text.Json;
using System.Text.Json.Serialization;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

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
        Guid userId,
        AccountErasure erasure,
        CancellationToken ct
    )
    {
        var household = await (
            from u in context.Users.AsNoTracking()
            join type in context.UserTypes.AsNoTracking() on u.UserTypeId equals type.Id
            join status in context.UserStatusTypes.AsNoTracking()
                on u.UserStatusTypeId equals status.Id
            where u.Id == userId || u.ParentId == userId
            select new LegalCopyPerson(
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
                u.UserTypeId,
                type.Name,
                u.UserStatusTypeId,
                status.Name,
                u.IsAdmin,
                u.TwoFactorMethod,
                u.ParentId,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt
            )
        ).ToListAsync(ct);
        var householdIds = household.Select(person => person.Id).ToList();
        var guardianId = household.Single(person => person.Id == userId).ParentId;

        var guardian = guardianId is null
            ? null
            : await context
                .Users.AsNoTracking()
                .Where(u => u.Id == guardianId)
                .Select(u => new LegalCopyGuardian(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.NationalId,
                    u.Email,
                    u.Phone
                ))
                .SingleAsync(ct);

        var signups = await (
            from activity in context.Activities.AsNoTracking()
            from assignment in activity.Assignments
            join modality in context.ActivityModalityTypes.AsNoTracking()
                on activity.ActivityModalityTypeId equals modality.Id
            join role in context.ActivityRoleTypes.AsNoTracking()
                on assignment.ActivityRoleTypeId equals role.Id
            join status in context.AssignmentStatusTypes.AsNoTracking()
                on assignment.AssignmentStatusId equals status.Id
            where householdIds.Contains(assignment.UserId)
            select new EventSignup(
                activity.EventId,
                new LegalCopyActivity(
                    assignment.ActivityId,
                    activity.Title,
                    activity.Location,
                    modality.Name,
                    activity.ActivityStartsAt,
                    activity.ActivityEndsAt,
                    assignment.UserId,
                    role.Name,
                    status.Name,
                    assignment.CreatedAt
                )
            )
        ).ToListAsync(ct);

        var decisions = await (
            from acceptance in context.EventTermsAcceptances.AsNoTracking()
            join document in context.TermsDocuments.AsNoTracking()
                on acceptance.TermsDocumentId equals document.Id
            where householdIds.Contains(acceptance.UserId) || acceptance.UserId == guardianId
            select new EventDecision(
                acceptance.EventId,
                new LegalCopyDecision(
                    acceptance.TermsDocumentId,
                    document.Name,
                    acceptance.Accepted,
                    acceptance.DecidedAt,
                    acceptance.UserId
                )
            )
        ).ToListAsync(ct);

        var documentIds = decisions.Select(d => d.Decision.DocumentId).Distinct().ToList();
        var documents = await context
            .TermsDocuments.AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => new LegalCopyTermsDocument(d.Id, d.Name, d.Description))
            .ToListAsync(ct);

        var eventIds = signups
            .Select(s => s.EventId)
            .Union(decisions.Select(d => d.EventId))
            .ToList();
        var events = await context
            .Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new LegalCopyEvent(
                e.Id,
                e.Title,
                e.EventStartsAt,
                e.EventEndsAt,
                new List<LegalCopyDecision>(),
                new List<LegalCopyActivity>()
            ))
            .ToListAsync(ct);

        var copy = LegalCopy.Compose(
            userId,
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
