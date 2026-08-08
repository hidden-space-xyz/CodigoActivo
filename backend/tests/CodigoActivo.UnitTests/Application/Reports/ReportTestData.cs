using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Reports;

internal static class ReportTestData
{
    public static readonly Guid QueriedEventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid AlphaRoleId = new("11111111-1111-1111-1111-111111111111");

    public static readonly Guid Confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
    public static readonly Guid Requested = SeedIds.AssignmentStatusTypes.Requested;
    public static readonly Guid Denied = SeedIds.AssignmentStatusTypes.Denied;

    public static readonly DateTimeOffset When = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    public static User NewUser(
        string first,
        User? parent = null,
        Guid? userTypeId = null,
        string? email = null,
        DateOnly? birthDate = null,
        string typeName = "Socio",
        Gender gender = Gender.Female
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = first + "-last",
            Email = email ?? (first + "@test.local"),
            Phone = "555-" + first,
            BirthDate = birthDate ?? new DateOnly(1990, 6, 15),
            Gender = gender,
            Parent = parent,
            ParentId = parent?.Id,
            UserTypeId = userTypeId ?? SeedIds.UserTypes.Member,
            UserType = new UserType
            {
                Description = "Descripción de prueba",
                Name = typeName,
                Color = "#EF4444",
            },
        };
    }

    public static void HasEvents(this IEventRepository events, params Event[] list)
    {
        events.Query().Returns(list.AsQueryable());
    }

    public static void HasAssignments(
        this IActivityRepository activities,
        params ActivityUserRoleAssignment[] assignments
    )
    {
        activities.QueryAssignments().Returns(assignments.AsQueryable());
    }

    public static void HasUsers(this IUserRepository users, params User[] list)
    {
        users.Query().Returns(list.AsQueryable());
    }
}
