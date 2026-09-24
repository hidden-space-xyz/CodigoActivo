using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Mapping;

/// <summary>
/// Provides reusable extension methods for mapping.
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="user">The user value.</param>
    /// <returns>The resulting user value.</returns>
    public static UserResponse ToResponse(this User user)
    {
        var status = new UserStatusResponse(
            user.UserStatusTypeId,
            user.UserStatusType?.Name ?? string.Empty,
            user.UserStatusType?.Color ?? string.Empty
        );

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Phone,
            user.BirthDate,
            user.NationalId,
            user.PromotionalConsent,
            user.Gender,
            user.LastLoginAt,
            user.CreatedAt,
            user.UpdatedAt,
            user.ParentId,
            null,
            null,
            status,
            user.IsAdmin,
            null,
            user.TwoFactorMethod
        );
    }

    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="resource">The resource value.</param>
    /// <returns>The resulting resource value.</returns>
    public static ResourceResponse ToResponse(this Resource resource)
    {
        var type = new ResourceTypeResponse(
            resource.ResourceTypeId,
            resource.ResourceType?.Name ?? string.Empty,
            resource.ResourceType?.Description ?? string.Empty,
            resource.ResourceType?.Color ?? string.Empty,
            resource.ResourceType?.IsExternal ?? false
        );

        return new ResourceResponse(
            resource.Id,
            resource.Title,
            resource.Subtitle,
            resource.Description,
            resource.Url,
            type,
            resource.CreatedAt,
            resource.UpdatedAt,
            resource.CreatedBy,
            resource.UpdatedBy,
            resource.ThumbnailId
        );
    }

    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="announcement">The announcement value.</param>
    /// <returns>The resulting announcement value.</returns>
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

    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="partner">The partner value.</param>
    /// <returns>The resulting partner value.</returns>
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

    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="file">The file value.</param>
    /// <returns>The resulting file value.</returns>
    public static FileResponse ToResponse(this FileEntity file)
    {
        return new FileResponse(
            file.Id,
            file.Name,
            file.Extension,
            file.UploadedAt,
            file.UploadedBy
        );
    }

    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="categoryType">The category type value.</param>
    /// <returns>The resulting event category type value.</returns>
    public static EventCategoryTypeResponse ToResponse(this EventCategoryType categoryType)
    {
        return new EventCategoryTypeResponse(
            categoryType.Id,
            categoryType.Name,
            categoryType.Color
        );
    }

    /// <summary>
    /// Maps the domain value to its API response model.
    /// </summary>
    /// <param name="termsDocument">The terms document value.</param>
    /// <returns>The resulting terms document value.</returns>
    public static TermsDocumentResponse ToResponse(this TermsDocument termsDocument)
    {
        return new TermsDocumentResponse(
            termsDocument.Id,
            termsDocument.Name,
            termsDocument.Description
        );
    }
}
