using System.Linq.Expressions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Users;

internal static class UserTestData
{
    public static readonly DateOnly Today = new(2026, 7, 4);
    public static readonly DateOnly MinorDob = Today.AddYears(-10);
    public static readonly DateOnly AdultDob = Today.AddYears(-40);

    public static User NewUser(
        string first = "Ana",
        string last = "Lopez",
        Guid? id = null,
        Guid? parentId = null,
        DateOnly? dob = null,
        string? email = "ana@test.com",
        string? phone = "555-0100",
        bool isAdmin = false,
        Guid? typeId = null,
        Guid? statusId = null,
        string typeName = "Socio",
        string statusName = "Active"
    )
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Email = email,
            Phone = phone,
            BirthDate = dob ?? AdultDob,
            Gender = Gender.Male,
            ParentId = parentId,
            UserStatusTypeId = statusId ?? Guid.NewGuid(),
            UserStatusType = new UserStatusType
            {
                Name = statusName,
                Color = "#111",
                Description = "",
            },
            IsAdmin = isAdmin,
            UserTypeId = typeId ?? Guid.NewGuid(),
            UserType = new UserType
            {
                Name = typeName,
                Color = "#111",
                Description = "",
            },
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
    }

    public static UserType NewUserType(string name)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Color = "#000",
        };
    }

    public static UserStatusType NewStatusType(string name)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Color = "#000",
        };
    }

    public static bool IsAddedChild(User? user, Guid parentId, DateTimeOffset createdAt)
    {
        if (user is null)
        {
            return false;
        }

        var isNamedKid =
            string.Equals(user.FirstName, "Kid", StringComparison.Ordinal)
            && string.Equals(user.LastName, "Doe", StringComparison.Ordinal);
        var isDependentParticipant =
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
            && user.UserTypeId == SeedIds.UserTypes.Participant
            && user.Gender is Gender.Female;

        return isNamedKid
            && isDependentParticipant
            && user.ParentId == parentId
            && user.CreatedAt == createdAt;
    }

    public static void HasUsers(this IUserRepository users, params User[] items)
    {
        users.Query().Returns(items.AsQueryable());
    }

    public static void HasUserTypes(this IUserTypeRepository userTypes, params UserType[] items)
    {
        userTypes.Query().Returns(items.AsQueryable());
    }

    public static void HasStatusTypes(
        this IUserStatusTypeRepository userStatusTypes,
        params UserStatusType[] items
    )
    {
        userStatusTypes.Query().Returns(items.AsQueryable());
    }

    public static void FindReturns(this IUserRepository users, params User?[]? sequence)
    {
        if (sequence is null || sequence.Length is 0)
        {
            users.Finds(null);
            return;
        }

        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(sequence[0], [.. sequence.Skip(1)]);
    }
}
