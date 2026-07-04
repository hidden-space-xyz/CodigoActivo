using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Mapping;

public static class MappingExtensions
{
    public static UserResponse ToResponse(this User user)
    {
        var status = new UserStatusResponse(
            user.UserStatusTypeId,
            user.UserStatusType?.Name ?? string.Empty,
            user.UserStatusType?.Color ?? string.Empty
        );
        var type = new UserTypeSummaryResponse(
            user.UserTypeId,
            user.UserType?.Name ?? string.Empty,
            user.UserType?.Color ?? string.Empty
        );

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
            user.IsAdmin,
            type
        );
    }

    public static ResourceResponse ToResponse(this Resource resource)
    {
        return new ResourceResponse(
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
        return new AnnouncementResponse(
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
        return new PartnerResponse(
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
        return new FileResponse(file.Id, file.Name, file.Extension, file.UploadedAt, file.UploadedBy);
    }

    public static ActivityRoleTypeResponse ToResponse(this ActivityRoleType roleType)
    {
        return new ActivityRoleTypeResponse(roleType.Id, roleType.Name, roleType.Description);
    }

    public static EventCategoryTypeResponse ToResponse(this EventCategoryType categoryType)
    {
        return new EventCategoryTypeResponse(categoryType.Id, categoryType.Name, categoryType.Color);
    }
}