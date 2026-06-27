using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Mapping;

public static class MappingExtensions
{
    public static UserResponse ToResponse(this User user)
    {
        var status = new UserStatusResponse(
            user.UserStatusTypeId,
            user.UserStatusType?.Name ?? string.Empty
        );
        var roles =
            user.TypeAssignments?.Select(assignment => new UserRoleResponse(
                    assignment.UserTypeId,
                    assignment.UserType?.Name ?? string.Empty
                ))
                .ToList() ?? [];

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Phone,
            user.BirthDate,
            user.LastLoginAt,
            user.CreatedAt,
            user.UpdatedAt,
            user.ParentId,
            status,
            roles
        );
    }

    public static EventResponse ToResponse(this Event @event)
    {
        return new(
            @event.Id,
            @event.Title,
            @event.Subtitle,
            @event.Description,
            @event.EventStartsAt,
            @event.EventEndsAt,
            @event.SignupStartsAt,
            @event.SignupEndsAt,
            @event.CreatedAt,
            @event.UpdatedAt,
            @event.CreatedBy,
            @event.UpdatedBy,
            @event.ThumbnailId,
            @event.Featured
        );
    }

    public static ActivityResponse ToResponse(this Activity activity)
    {
        var roles =
            activity
                .AllowedRoleTypes?.Select(role => new ActivityAllowedRoleResponse(
                    role.ActivityRoleTypeId,
                    role.ActivityRoleType?.Name,
                    role.DesiredSignups
                ))
                .ToList() ?? [];

        return new ActivityResponse(
            activity.Id,
            activity.Title,
            activity.Description,
            activity.ActivityStartsAt,
            activity.ActivityEndsAt,
            activity.EventId,
            activity.ThumbnailId,
            activity.CreatedAt,
            activity.UpdatedAt,
            activity.CreatedBy,
            activity.UpdatedBy,
            roles
        );
    }

    public static ResourceResponse ToResponse(this Resource resource)
    {
        return new(
            resource.Id,
            resource.Title,
            resource.Subtitle,
            resource.Description,
            resource.CreatedAt,
            resource.UpdatedAt,
            resource.CreatedBy,
            resource.UpdatedBy,
            resource.ThumbnailId
        );
    }

    public static AnnouncementResponse ToResponse(this Announcement announcement)
    {
        return new(
            announcement.Id,
            announcement.Title,
            announcement.Subtitle,
            announcement.Description,
            announcement.CreatedAt,
            announcement.UpdatedAt,
            announcement.CreatedBy,
            announcement.UpdatedBy,
            announcement.ThumbnailId,
            announcement.Featured
        );
    }

    public static PartnerResponse ToResponse(this Partner partner)
    {
        return new(
            partner.Id,
            partner.Name,
            partner.FromDate,
            partner.Tier,
            partner.Web,
            partner.CreatedAt,
            partner.UpdatedAt,
            partner.CreatedBy,
            partner.UpdatedBy,
            partner.ThumbnailId
        );
    }

    public static FileResponse ToResponse(this FileEntity file)
    {
        return new(file.Id, file.Name, file.Extension, file.UploadedAt, file.UploadedBy);
    }

    public static UserStatusTypeResponse ToResponse(this UserStatusType statusType)
    {
        return new(statusType.Id, statusType.Name, statusType.Description);
    }

    public static UserTypeResponse ToResponse(this UserType userType)
    {
        return new(userType.Id, userType.Name, userType.Description);
    }

    public static ActivityRoleTypeResponse ToResponse(this ActivityRoleType roleType)
    {
        return new(roleType.Id, roleType.Name, roleType.Description);
    }

    public static AssignmentStatusTypeResponse ToResponse(this AssignmentStatusType statusType)
    {
        return new(statusType.Id, statusType.Name, statusType.Description);
    }
}
