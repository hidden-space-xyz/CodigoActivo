using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Mapping;

public static class Projections
{
    public static readonly Expression<Func<Event, EventResponse>> Event =
        @event => new EventResponse
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
            Categories = @event
                .Categories.Select(category => new EventCategoryResponse(
                    category.EventCategoryTypeId,
                    category.EventCategoryType.Name,
                    category.EventCategoryType.Color
                ))
                .ToList(),
        };

    public static readonly Expression<Func<Event, EventListItemResponse>> EventListItem =
        @event => new EventListItemResponse
        {
            Id = @event.Id,
            Title = @event.Title,
            Subtitle = @event.Subtitle,
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
            Categories = @event
                .Categories.Select(category => new EventCategoryResponse(
                    category.EventCategoryTypeId,
                    category.EventCategoryType.Name,
                    category.EventCategoryType.Color
                ))
                .ToList(),
        };

    public static readonly Expression<Func<Announcement, AnnouncementResponse>> Announcement =
        announcement => new AnnouncementResponse
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

    public static readonly Expression<
        Func<Announcement, AnnouncementListItemResponse>
    > AnnouncementListItem = announcement => new AnnouncementListItemResponse
    {
        Id = announcement.Id,
        Title = announcement.Title,
        Subtitle = announcement.Subtitle,
        CreatedAt = announcement.CreatedAt,
        UpdatedAt = announcement.UpdatedAt,
        CreatedBy = announcement.CreatedBy,
        UpdatedBy = announcement.UpdatedBy,
        ThumbnailId = announcement.ThumbnailId,
        Featured = announcement.Featured,
    };

    public static readonly Expression<Func<Resource, ResourceResponse>> Resource =
        resource => new ResourceResponse
        {
            Id = resource.Id,
            Title = resource.Title,
            Subtitle = resource.Subtitle,
            Description = resource.Description,
            Url = resource.Url,
            Type = new ResourceTypeResponse
            {
                Id = resource.ResourceType.Id,
                Name = resource.ResourceType.Name,
                Description = resource.ResourceType.Description,
                Color = resource.ResourceType.Color,
                IsExternal = resource.ResourceType.IsExternal,
            },
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            CreatedBy = resource.CreatedBy,
            UpdatedBy = resource.UpdatedBy,
            ThumbnailId = resource.ThumbnailId,
        };

    public static readonly Expression<Func<Resource, ResourceListItemResponse>> ResourceListItem =
        resource => new ResourceListItemResponse
        {
            Id = resource.Id,
            Title = resource.Title,
            Subtitle = resource.Subtitle,
            Url = resource.Url,
            Type = new ResourceTypeResponse
            {
                Id = resource.ResourceType.Id,
                Name = resource.ResourceType.Name,
                Description = resource.ResourceType.Description,
                Color = resource.ResourceType.Color,
                IsExternal = resource.ResourceType.IsExternal,
            },
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            CreatedBy = resource.CreatedBy,
            UpdatedBy = resource.UpdatedBy,
            ThumbnailId = resource.ThumbnailId,
        };

    public static readonly Expression<Func<ResourceType, ResourceTypeResponse>> ResourceType =
        resourceType => new ResourceTypeResponse
        {
            Id = resourceType.Id,
            Name = resourceType.Name,
            Description = resourceType.Description,
            Color = resourceType.Color,
            IsExternal = resourceType.IsExternal,
        };

    public static readonly Expression<Func<Partner, PartnerResponse>> Partner =
        partner => new PartnerResponse
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

    public static readonly Expression<Func<Activity, ActivityResponse>> Activity =
        activity => new ActivityResponse
        {
            Id = activity.Id,
            Title = activity.Title,
            Description = activity.Description,
            Location = activity.Location,
            ActivityStartsAt = activity.ActivityStartsAt,
            ActivityEndsAt = activity.ActivityEndsAt,
            EventId = activity.EventId,
            ModalityId = activity.ActivityModalityTypeId,
            ModalityName = activity.ActivityModalityType.Name,
            ThumbnailId = activity.ThumbnailId,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
            CreatedBy = activity.CreatedBy,
            UpdatedBy = activity.UpdatedBy,
            AllowedRoleTypes = activity
                .AllowedRoleTypes.Select(role => new ActivityAllowedRoleResponse(
                    role.ActivityRoleTypeId,
                    role.ActivityRoleType.Name
                ))
                .ToList(),
        };

    public static readonly Expression<Func<User, UserResponse>> User = user => new UserResponse
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
        IsAdmin = user.IsAdmin,
    };

    public static readonly Expression<Func<User, UserResponse>> UserWithType =
        user => new UserResponse
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
            IsAdmin = user.IsAdmin,
            Type = new UserTypeSummaryResponse(
                user.UserTypeId,
                user.UserType.Name,
                user.UserType.Color
            ),
        };

    public static readonly Expression<
        Func<ActivityRoleType, ActivityRoleTypeResponse>
    > ActivityRoleType = roleType => new ActivityRoleTypeResponse
    {
        Id = roleType.Id,
        Name = roleType.Name,
        Description = roleType.Description,
    };

    public static readonly Expression<
        Func<AssignmentStatusType, AssignmentStatusTypeResponse>
    > AssignmentStatusType = statusType => new AssignmentStatusTypeResponse
    {
        Id = statusType.Id,
        Name = statusType.Name,
        Description = statusType.Description,
        Color = statusType.Color,
    };

    public static readonly Expression<
        Func<EventCategoryType, EventCategoryTypeResponse>
    > EventCategoryType = categoryType => new EventCategoryTypeResponse
    {
        Id = categoryType.Id,
        Name = categoryType.Name,
        Color = categoryType.Color,
    };

    public static readonly Expression<
        Func<ActivityModalityType, ActivityModalityTypeResponse>
    > ActivityModalityType = modalityType => new ActivityModalityTypeResponse
    {
        Id = modalityType.Id,
        Name = modalityType.Name,
    };

    public static readonly Expression<Func<UserStatusType, UserStatusTypeResponse>> UserStatusType =
        statusType => new UserStatusTypeResponse
        {
            Id = statusType.Id,
            Name = statusType.Name,
            Description = statusType.Description,
            Color = statusType.Color,
        };

    public static readonly Expression<Func<UserType, UserTypeResponse>> UserType =
        userType => new UserTypeResponse
        {
            Id = userType.Id,
            Name = userType.Name,
            Description = userType.Description,
            Color = userType.Color,
        };

    public static readonly Expression<
        Func<ActivityUserRoleAssignment, AssignedActivityResponse>
    > AssignedActivity = assignment => new AssignedActivityResponse
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
