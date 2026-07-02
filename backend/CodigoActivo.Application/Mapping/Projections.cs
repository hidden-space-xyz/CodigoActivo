using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Mapping;

/// <summary>
/// EF-translatable projections from entities to their read DTOs. Unlike the
/// <see cref="MappingExtensions"/> methods (which run on materialized entities for
/// write-return paths), these are expression trees composed into an <see cref="IQueryable{T}"/>
/// and translated to SQL. Every OData read endpoint uses these.
/// <para>
/// The outer DTO is built with a member initializer (not the positional constructor) so EF Core
/// can compose the OData query options ($filter/$orderby/$top/$skip, which include an auto key
/// ordering for stable paging) on top of the projection. EF cannot see through a constructor
/// projection for those composed operators, hence the object-initializer form.
/// </para>
/// </summary>
public static class Projections
{
    public static readonly Expression<Func<Event, EventResponse>> Event = @event =>
        new EventResponse
        {
            Id = @event.Id,
            Title = @event.Title,
            Subtitle = @event.Subtitle,
            Description = @event.Description,
            EventStartsAt = @event.EventStartsAt,
            EventEndsAt = @event.EventEndsAt,
            SignupStartsAt = @event.SignupStartsAt,
            SignupEndsAt = @event.SignupEndsAt,
            CreatedAt = @event.CreatedAt,
            UpdatedAt = @event.UpdatedAt,
            CreatedBy = @event.CreatedBy,
            UpdatedBy = @event.UpdatedBy,
            ThumbnailId = @event.ThumbnailId,
            Featured = @event.Featured,
        };

    public static readonly Expression<Func<Announcement, AnnouncementResponse>> Announcement =
        announcement =>
            new AnnouncementResponse
            {
                Id = announcement.Id,
                Title = announcement.Title,
                Subtitle = announcement.Subtitle,
                Description = announcement.Description,
                CreatedAt = announcement.CreatedAt,
                UpdatedAt = announcement.UpdatedAt,
                CreatedBy = announcement.CreatedBy,
                UpdatedBy = announcement.UpdatedBy,
                ThumbnailId = announcement.ThumbnailId,
                Featured = announcement.Featured,
            };

    public static readonly Expression<Func<Resource, ResourceResponse>> Resource = resource =>
        new ResourceResponse
        {
            Id = resource.Id,
            Title = resource.Title,
            Subtitle = resource.Subtitle,
            Description = resource.Description,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            CreatedBy = resource.CreatedBy,
            UpdatedBy = resource.UpdatedBy,
            ThumbnailId = resource.ThumbnailId,
        };

    public static readonly Expression<Func<Partner, PartnerResponse>> Partner = partner =>
        new PartnerResponse
        {
            Id = partner.Id,
            Name = partner.Name,
            FromDate = partner.FromDate,
            Tier = partner.Tier,
            Website = partner.Web,
            CreatedAt = partner.CreatedAt,
            UpdatedAt = partner.UpdatedAt,
            CreatedBy = partner.CreatedBy,
            UpdatedBy = partner.UpdatedBy,
            ThumbnailId = partner.ThumbnailId,
        };

    public static readonly Expression<Func<Activity, ActivityResponse>> Activity = activity =>
        new ActivityResponse
        {
            Id = activity.Id,
            Title = activity.Title,
            Description = activity.Description,
            ActivityStartsAt = activity.ActivityStartsAt,
            ActivityEndsAt = activity.ActivityEndsAt,
            EventId = activity.EventId,
            ThumbnailId = activity.ThumbnailId,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
            CreatedBy = activity.CreatedBy,
            UpdatedBy = activity.UpdatedBy,
            AllowedRoleTypes = activity
                .AllowedRoleTypes.Select(role => new ActivityAllowedRoleResponse(
                    role.ActivityRoleTypeId,
                    role.ActivityRoleType.Name,
                    role.DesiredSignups
                ))
                .ToList(),
        };

    public static readonly Expression<Func<User, UserResponse>> User = user =>
        new UserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone,
            BirthDate = user.BirthDate,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            ParentId = user.ParentId,
            Status = new UserStatusResponse(
                user.UserStatusTypeId,
                user.UserStatusType.Name,
                user.UserStatusType.Color
            ),
            Roles = user.TypeAssignments.Select(assignment => new UserRoleResponse(
                    assignment.UserTypeId,
                    assignment.UserType.Name,
                    assignment.UserType.Color
                ))
                .ToList(),
        };

    public static readonly Expression<
        Func<ActivityRoleType, ActivityRoleTypeResponse>
    > ActivityRoleType = roleType =>
        new ActivityRoleTypeResponse
        {
            Id = roleType.Id,
            Name = roleType.Name,
            Description = roleType.Description,
        };

    public static readonly Expression<
        Func<AssignmentStatusType, AssignmentStatusTypeResponse>
    > AssignmentStatusType = statusType =>
        new AssignmentStatusTypeResponse
        {
            Id = statusType.Id,
            Name = statusType.Name,
            Description = statusType.Description,
            Color = statusType.Color,
        };

    public static readonly Expression<
        Func<UserStatusType, UserStatusTypeResponse>
    > UserStatusType = statusType =>
        new UserStatusTypeResponse
        {
            Id = statusType.Id,
            Name = statusType.Name,
            Description = statusType.Description,
            Color = statusType.Color,
        };

    public static readonly Expression<Func<UserType, UserTypeResponse>> UserType = userType =>
        new UserTypeResponse
        {
            Id = userType.Id,
            Name = userType.Name,
            Description = userType.Description,
            Color = userType.Color,
        };

    public static readonly Expression<
        Func<UserType, RegistrationTypeResponse>
    > RegistrationType = userType =>
        new RegistrationTypeResponse
        {
            Id = userType.Id,
            Name = userType.Name,
            Description = userType.Description,
            Color = userType.Color,
            IsAllowedForMinors = userType.IsAllowedForMinors,
            IsAllowedForAdults = userType.IsAllowedForAdults,
        };

    public static readonly Expression<
        Func<ActivityUserRoleAssignment, AssignedActivityResponse>
    > AssignedActivity = assignment =>
        new AssignedActivityResponse
        {
            ActivityId = assignment.ActivityId,
            Title = assignment.Activity.Title,
            Description = assignment.Activity.Description,
            ActivityStartsAt = assignment.Activity.ActivityStartsAt,
            ActivityEndsAt = assignment.Activity.ActivityEndsAt,
            EventId = assignment.Activity.EventId,
            RoleType = new AssignedActivityRoleResponse(
                assignment.ActivityRoleTypeId,
                assignment.ActivityRoleType.Name
            ),
            Status = new AssignedActivityStatusResponse(
                assignment.AssignmentStatusId,
                assignment.AssignmentStatus.Name
            ),
        };
}
