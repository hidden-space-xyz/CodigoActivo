using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Users;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class UserProjections
{
    /// <summary>
    /// Stores the shared user value.
    /// </summary>
    public static readonly Expression<Func<UserRow, UserResponse>> User = user => new UserResponse
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Phone = user.Phone,
        SecondaryPhone = user.SecondaryPhone,
        BirthDate = user.BirthDate,
        NationalId = user.NationalId,
        PromotionalConsent = user.PromotionalConsent,
        Gender = user.Gender,
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        ParentId = user.ParentId,
        Status = new UserStatusResponse(
            user.UserStatusTypeId,
            user.UserStatusType.Name,
            user.UserStatusType.Color
        ),
        IsAdmin = user.IsAdmin,
        IsInitialAdmin = user.Id == SeedIds.Users.InitialAdministrator,
        TwoFactorMethod = user.TwoFactorMethod,
        EarlySignupEligible = EarlySignup.EntitledUserTypeIds.Contains(
            user.Parent != null ? user.Parent.UserTypeId : user.UserTypeId
        ),
    };

    /// <summary>
    /// Stores the shared user with type value.
    /// </summary>
    public static readonly Expression<Func<UserRow, UserResponse>> UserWithType =
        user => new UserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone,
            SecondaryPhone = user.SecondaryPhone,
            BirthDate = user.BirthDate,
            NationalId = user.NationalId,
            PromotionalConsent = user.PromotionalConsent,
            Gender = user.Gender,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            ParentId = user.ParentId,
            ParentName =
                user.Parent == null ? null : user.Parent.FirstName + " " + user.Parent.LastName,
            DependentCount = user.Children.Count,
            Status = new UserStatusResponse(
                user.UserStatusTypeId,
                user.UserStatusType.Name,
                user.UserStatusType.Color
            ),
            IsAdmin = user.IsAdmin,
            IsInitialAdmin = user.Id == SeedIds.Users.InitialAdministrator,
            Type = new UserTypeSummaryResponse(
                user.UserTypeId,
                user.UserType.Name,
                user.UserType.Color
            ),
            TwoFactorMethod = user.TwoFactorMethod,
            EarlySignupEligible = EarlySignup.EntitledUserTypeIds.Contains(
                user.Parent != null ? user.Parent.UserTypeId : user.UserTypeId
            ),
        };

    /// <summary>
    /// Stores the shared user status type value.
    /// </summary>
    public static readonly Expression<
        Func<UserStatusTypeRow, UserStatusTypeResponse>
    > UserStatusType = statusType => new UserStatusTypeResponse
    {
        Id = statusType.Id,
        Name = statusType.Name,
        Description = statusType.Description,
        Color = statusType.Color,
    };

    /// <summary>
    /// Stores the shared user type value.
    /// </summary>
    public static readonly Expression<Func<UserTypeRow, UserTypeResponse>> UserType =
        userType => new UserTypeResponse
        {
            Id = userType.Id,
            Name = userType.Name,
            Description = userType.Description,
            Color = userType.Color,
        };
}
