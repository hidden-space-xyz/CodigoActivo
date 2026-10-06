using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Gender = CodigoActivo.Application.Users.Contracts.Gender;
using TwoFactorMethod = CodigoActivo.Application.Accounts.Contracts.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Users;

internal static class UserTestData
{
    public static readonly DateOnly Today = new(2026, 7, 4);
    public static readonly DateOnly MinorDob = Today.AddYears(-10);
    public static readonly DateOnly AdultDob = Today.AddYears(-40);
    public const string AdultNationalId = "12345678Z";

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
        string? passwordHash = null,
        string? nationalId = AdultNationalId
    )
    {
        return Persisted.As<User>(
            new
            {
                Id = id ?? Guid.NewGuid(),
                FirstName = first,
                LastName = last,
                Email = email,
                Phone = phone,
                PasswordHash = passwordHash,
                BirthDate = dob ?? (parentId is null ? null : MinorDob),
                NationalId = parentId is null ? nationalId : null,
                Gender = Gender.Male,
                ParentId = parentId,
                Status = CatalogIds.UserStatuses.ValueOf(
                    statusId ?? KnownIds.UserStatusTypes.Active
                ),
                IsAdmin = isAdmin,
                UserType = CatalogIds.UserTypes.ValueOf(typeId ?? KnownIds.UserTypes.Participant),
                CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
    }

    public static UserRow NewUserRow(
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
        string statusName = "Active",
        string? passwordHash = null,
        string? nationalId = AdultNationalId,
        bool promotionalConsent = false,
        UserRow? parent = null
    )
    {
        var guardianId = parentId ?? parent?.Id;
        var userStatusTypeId = statusId ?? Guid.NewGuid();
        var userTypeId = typeId ?? Guid.NewGuid();
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Email = email,
            Phone = phone,
            PasswordHash = passwordHash,
            BirthDate = dob ?? (guardianId is null ? null : MinorDob),
            NationalId = guardianId is null ? nationalId : null,
            PromotionalConsent = promotionalConsent,
            Gender = Gender.Male,
            ParentId = guardianId,
            Parent = parent,
            UserStatusTypeId = userStatusTypeId,
            UserStatusType = new UserStatusTypeRow
            {
                Id = userStatusTypeId,
                Name = statusName,
                Color = "#111",
                Description = "",
            },
            IsAdmin = isAdmin,
            UserTypeId = userTypeId,
            UserType = new UserTypeRow
            {
                Id = userTypeId,
                Name = typeName,
                Color = "#111",
                Description = "",
            },
            TwoFactorMethod = TwoFactorMethod.Email,
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
    }

    public static UserTypeRow NewUserTypeRow(string name)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Color = "#000",
        };
    }

    public static UserStatusTypeRow NewStatusTypeRow(string name)
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
            user.Status == UserStatus.Dependent
            && user.UserType == UserType.Participant
            && user.Gender is CodigoActivo.Domain.Users.Gender.Female;

        return isNamedKid
            && isDependentParticipant
            && user.ParentId == UserId.From(parentId)
            && user.CreatedAt == createdAt;
    }

    public static void FindReturns(this IUserRepository users, params User?[]? sequence)
    {
        if (sequence is null || sequence.Length is 0)
        {
            users.Finds(null);
            return;
        }

        users.Finds(sequence[0]);
        users
            .GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(sequence[0], [.. sequence.Skip(1)]);
    }
}
