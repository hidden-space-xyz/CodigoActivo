using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class KnownIds
{
    public static class Users
    {
        public static readonly Guid InitialAdministrator = CodigoActivo
            .Domain
            .Users
            .InitialAdministrator
            .Id
            .Value;
    }

    public static class UserStatusTypes
    {
        public static readonly Guid Pending = CatalogIds.UserStatuses.IdOf(UserStatus.Pending);
        public static readonly Guid Active = CatalogIds.UserStatuses.IdOf(UserStatus.Active);
        public static readonly Guid Blocked = CatalogIds.UserStatuses.IdOf(UserStatus.Blocked);
        public static readonly Guid Dependent = CatalogIds.UserStatuses.IdOf(UserStatus.Dependent);
    }

    public static class UserTypes
    {
        public static readonly Guid Member = CatalogIds.UserTypes.IdOf(UserType.Member);
        public static readonly Guid Sponsor = CatalogIds.UserTypes.IdOf(UserType.Sponsor);
        public static readonly Guid Participant = CatalogIds.UserTypes.IdOf(UserType.Participant);
    }

    public static class ActivityRoleTypes
    {
        public static readonly Guid Leader = CatalogIds.ActivityRoles.IdOf(ActivityRole.Leader);
        public static readonly Guid Volunteer = CatalogIds.ActivityRoles.IdOf(
            ActivityRole.Volunteer
        );
        public static readonly Guid Participant = CatalogIds.ActivityRoles.IdOf(
            ActivityRole.Participant
        );
    }

    public static class AssignmentStatusTypes
    {
        public static readonly Guid Requested = CatalogIds.AssignmentStatuses.IdOf(
            AssignmentStatus.Requested
        );
        public static readonly Guid Confirmed = CatalogIds.AssignmentStatuses.IdOf(
            AssignmentStatus.Confirmed
        );
        public static readonly Guid Denied = CatalogIds.AssignmentStatuses.IdOf(
            AssignmentStatus.Denied
        );
    }

    public static class ActivityModalityTypes
    {
        public static readonly Guid Presencial = CatalogIds.ActivityModalities.IdOf(
            ActivityModality.Presencial
        );
        public static readonly Guid Online = CatalogIds.ActivityModalities.IdOf(
            ActivityModality.Online
        );
    }

    public static class ResourceTypes
    {
        public static readonly Guid Internal = CatalogIds.ResourceTypes.IdOf(ResourceType.Internal);
        public static readonly Guid External = CatalogIds.ResourceTypes.IdOf(ResourceType.External);
    }
}
