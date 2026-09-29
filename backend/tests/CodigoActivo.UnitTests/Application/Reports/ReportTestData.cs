using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.UnitTests.Application.Reports;

internal static class ReportTestData
{
    public static readonly Guid QueriedEventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid AlphaRoleId = new("11111111-1111-1111-1111-111111111111");

    public static readonly Guid Confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
    public static readonly Guid Requested = SeedIds.AssignmentStatusTypes.Requested;
    public static readonly Guid Denied = SeedIds.AssignmentStatusTypes.Denied;

    public static readonly DateTimeOffset When = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    public static UserRow NewUserRow(
        string first,
        UserRow? parent = null,
        Guid? userTypeId = null,
        string? email = null,
        DateOnly? birthDate = null,
        string typeName = "Socio",
        Gender gender = Gender.Female,
        string? lastName = null,
        bool withContact = true
    )
    {
        var typeId = userTypeId ?? SeedIds.UserTypes.Member;
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = lastName ?? first + "-last",
            Email = withContact ? email ?? (first + "@test.local") : null,
            Phone = withContact ? "555-" + first : null,
            BirthDate = birthDate ?? new DateOnly(1990, 6, 15),
            Gender = gender,
            Parent = parent,
            ParentId = parent?.Id,
            UserTypeId = typeId,
            UserType = new UserTypeRow
            {
                Id = typeId,
                Description = "Descripción de prueba",
                Name = typeName,
                Color = "#EF4444",
            },
        };
    }
}
