using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Mapping;

/// <summary>
/// Unit tests for the EF-translatable <see cref="Projections"/> expression trees. Each expression is
/// compiled and invoked over a fully-populated sample entity so that every mapped field, including the
/// nested collection projections, is exercised.
/// </summary>
public sealed class ProjectionsTests
{
    private static readonly DateTimeOffset Created = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2025, 6, 7, 8, 9, 10, TimeSpan.Zero);

    private static TResult Project<TSource, TResult>(Expression<Func<TSource, TResult>> projection, TSource source) =>
        projection.Compile().Invoke(source);

    // ---- Event -------------------------------------------------------------

    [Fact]
    public void Event_projection_maps_scalars_and_categories()
    {
        var categoryTypeId = Guid.NewGuid();
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Conf",
            Subtitle = "Sub",
            Description = "{}",
            EventStartsAt = new DateOnly(2024, 8, 1),
            EventEndsAt = new DateOnly(2024, 8, 3),
            SignupStartsAt = Created,
            SignupEndsAt = Updated,
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
            Featured = true,
            Categories =
            [
                new EventCategory
                {
                    EventCategoryTypeId = categoryTypeId,
                    EventCategoryType = new EventCategoryType { Id = categoryTypeId, Name = "Tech", Color = "#111" },
                },
            ],
        };

        var response = Project(Projections.Event, @event);

        response.Id.Should().Be(@event.Id);
        response.Title.Should().Be("Conf");
        response.Subtitle.Should().Be("Sub");
        response.Description.Should().Be("{}");
        response.EventStartsAt.Should().Be(new DateOnly(2024, 8, 1));
        response.EventEndsAt.Should().Be(new DateOnly(2024, 8, 3));
        response.SignupStartsAt.Should().Be(Created);
        response.SignupEndsAt.Should().Be(Updated);
        response.CreatedAt.Should().Be(Created);
        response.UpdatedAt.Should().Be(Updated);
        response.CreatedBy.Should().Be(@event.CreatedBy);
        response.UpdatedBy.Should().Be(@event.UpdatedBy);
        response.ThumbnailId.Should().Be(@event.ThumbnailId);
        response.Featured.Should().BeTrue();
        response.Categories.Should().ContainSingle().Which.Should()
            .Be(new EventCategoryResponse(categoryTypeId, "Tech", "#111"));
    }

    [Fact]
    public void Event_projection_yields_empty_categories_when_none()
    {
        var @event = new Event { Title = "T", Subtitle = "S" };

        Project(Projections.Event, @event).Categories.Should().BeEmpty();
    }

    // ---- Announcement ------------------------------------------------------

    [Fact]
    public void Announcement_projection_maps_all_fields()
    {
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Subtitle = "S",
            Description = "{}",
            CreatedAt = Created,
            UpdatedAt = null,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = null,
            ThumbnailId = Guid.NewGuid(),
            Featured = true,
        };

        var response = Project(Projections.Announcement, announcement);

        response.Should().BeEquivalentTo(new AnnouncementResponse(
            announcement.Id, "T", "S", "{}", Created, null, announcement.CreatedBy, null,
            announcement.ThumbnailId, true));
    }

    // ---- Resource ----------------------------------------------------------

    [Fact]
    public void Resource_projection_maps_all_fields()
    {
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Subtitle = "S",
            Description = "{}",
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
        };

        var response = Project(Projections.Resource, resource);

        response.Should().BeEquivalentTo(new ResourceResponse(
            resource.Id, "T", "S", "{}", Created, Updated, resource.CreatedBy, resource.UpdatedBy,
            resource.ThumbnailId));
    }

    // ---- Partner -----------------------------------------------------------

    [Fact]
    public void Partner_projection_maps_web_to_website_and_all_fields()
    {
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            FromDate = new DateOnly(2024, 5, 6),
            Tier = 2,
            Web = "https://acme.test",
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
        };

        var response = Project(Projections.Partner, partner);

        response.Should().BeEquivalentTo(new PartnerResponse(
            partner.Id, "Acme", new DateOnly(2024, 5, 6), 2, "https://acme.test", Created, Updated,
            partner.CreatedBy, partner.UpdatedBy, partner.ThumbnailId));
    }

    // ---- Activity ----------------------------------------------------------

    [Fact]
    public void Activity_projection_maps_scalars_modality_and_allowed_roles()
    {
        var modalityId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Title = "Talk",
            Description = "Desc",
            Location = "Room 1",
            ActivityStartsAt = Created,
            ActivityEndsAt = Updated,
            EventId = Guid.NewGuid(),
            ActivityModalityTypeId = modalityId,
            ActivityModalityType = new ActivityModalityType { Id = modalityId, Name = "InPerson" },
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
            AllowedRoleTypes =
            [
                new ActivityAllowedRoleType
                {
                    ActivityRoleTypeId = roleTypeId,
                    ActivityRoleType = new ActivityRoleType { Id = roleTypeId, Name = "Speaker" },
                },
            ],
        };

        var response = Project(Projections.Activity, activity);

        response.Id.Should().Be(activity.Id);
        response.Title.Should().Be("Talk");
        response.Description.Should().Be("Desc");
        response.Location.Should().Be("Room 1");
        response.ActivityStartsAt.Should().Be(Created);
        response.ActivityEndsAt.Should().Be(Updated);
        response.EventId.Should().Be(activity.EventId);
        response.ModalityId.Should().Be(modalityId);
        response.ModalityName.Should().Be("InPerson");
        response.ThumbnailId.Should().Be(activity.ThumbnailId);
        response.CreatedAt.Should().Be(Created);
        response.UpdatedAt.Should().Be(Updated);
        response.CreatedBy.Should().Be(activity.CreatedBy);
        response.UpdatedBy.Should().Be(activity.UpdatedBy);
        response.AllowedRoleTypes.Should().ContainSingle().Which.Should()
            .Be(new ActivityAllowedRoleResponse(roleTypeId, "Speaker"));
    }

    // ---- User --------------------------------------------------------------

    [Fact]
    public void User_projection_maps_scalars_status_and_roles()
    {
        var statusId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.dev",
            Phone = "+34",
            BirthDate = new DateOnly(1990, 3, 4),
            LastLoginAt = Updated,
            CreatedAt = Created,
            UpdatedAt = Updated,
            ParentId = parentId,
            UserStatusTypeId = statusId,
            UserStatusType = new UserStatusType { Id = statusId, Name = "Active", Color = "#0f0" },
            TypeAssignments =
            [
                new UserTypeAssignment
                {
                    UserTypeId = roleId,
                    UserType = new UserType { Id = roleId, Name = "Member", Color = "#00f" },
                },
            ],
        };

        var response = Project(Projections.User, user);

        response.Id.Should().Be(user.Id);
        response.FirstName.Should().Be("Ada");
        response.LastName.Should().Be("Lovelace");
        response.Email.Should().Be("ada@test.dev");
        response.Phone.Should().Be("+34");
        response.BirthDate.Should().Be(new DateOnly(1990, 3, 4));
        response.LastLoginAt.Should().Be(Updated);
        response.CreatedAt.Should().Be(Created);
        response.UpdatedAt.Should().Be(Updated);
        response.ParentId.Should().Be(parentId);
        response.Status.Should().Be(new UserStatusResponse(statusId, "Active", "#0f0"));
        response.Roles.Should().ContainSingle().Which.Should().Be(new UserRoleResponse(roleId, "Member", "#00f"));
    }

    // ---- Reference-type projections ---------------------------------------

    [Fact]
    public void ActivityRoleType_projection_maps_id_name_description()
    {
        var roleType = new ActivityRoleType { Id = Guid.NewGuid(), Name = "Vol", Description = "Helps" };

        Project(Projections.ActivityRoleType, roleType).Should()
            .Be(new ActivityRoleTypeResponse(roleType.Id, "Vol", "Helps"));
    }

    [Fact]
    public void AssignmentStatusType_projection_maps_all_fields()
    {
        var statusType = new AssignmentStatusType
        {
            Id = Guid.NewGuid(),
            Name = "Confirmed",
            Description = "Done",
            Color = "#0a0",
        };

        Project(Projections.AssignmentStatusType, statusType).Should()
            .Be(new AssignmentStatusTypeResponse(statusType.Id, "Confirmed", "Done", "#0a0"));
    }

    [Fact]
    public void EventCategoryType_projection_maps_id_name_color()
    {
        var categoryType = new EventCategoryType { Id = Guid.NewGuid(), Name = "Tech", Color = "#111" };

        Project(Projections.EventCategoryType, categoryType).Should()
            .Be(new EventCategoryTypeResponse(categoryType.Id, "Tech", "#111"));
    }

    [Fact]
    public void ActivityModalityType_projection_maps_id_name()
    {
        var modalityType = new ActivityModalityType { Id = Guid.NewGuid(), Name = "Online" };

        Project(Projections.ActivityModalityType, modalityType).Should()
            .Be(new ActivityModalityTypeResponse(modalityType.Id, "Online"));
    }

    [Fact]
    public void UserStatusType_projection_maps_all_fields()
    {
        var statusType = new UserStatusType
        {
            Id = Guid.NewGuid(),
            Name = "Active",
            Description = "Enabled",
            Color = "#0f0",
        };

        Project(Projections.UserStatusType, statusType).Should()
            .Be(new UserStatusTypeResponse(statusType.Id, "Active", "Enabled", "#0f0"));
    }

    [Fact]
    public void UserType_projection_maps_all_fields()
    {
        var userType = new UserType
        {
            Id = Guid.NewGuid(),
            Name = "Member",
            Description = "Standard",
            Color = "#00f",
        };

        Project(Projections.UserType, userType).Should()
            .Be(new UserTypeResponse(userType.Id, "Member", "Standard", "#00f"));
    }

    [Fact]
    public void RegistrationType_projection_maps_all_fields_including_allowance_flags()
    {
        var userType = new UserType
        {
            Id = Guid.NewGuid(),
            Name = "Member",
            Description = "Standard",
            Color = "#00f",
            IsAllowedForMinors = true,
            IsAllowedForAdults = false,
        };

        Project(Projections.RegistrationType, userType).Should()
            .Be(new RegistrationTypeResponse(userType.Id, "Member", "Standard", "#00f", true, false));
    }

    // ---- AssignedActivity --------------------------------------------------

    [Fact]
    public void AssignedActivity_projection_maps_activity_chain_role_and_status()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var assignment = new ActivityUserRoleAssignment
        {
            ActivityId = activityId,
            Activity = new Activity
            {
                Id = activityId,
                Title = "Talk",
                Description = "Desc",
                ActivityStartsAt = Created,
                ActivityEndsAt = Updated,
                EventId = eventId,
            },
            ActivityRoleTypeId = roleTypeId,
            ActivityRoleType = new ActivityRoleType { Id = roleTypeId, Name = "Speaker" },
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusType { Id = statusId, Name = "Confirmed" },
        };

        var response = Project(Projections.AssignedActivity, assignment);

        response.ActivityId.Should().Be(activityId);
        response.Title.Should().Be("Talk");
        response.Description.Should().Be("Desc");
        response.ActivityStartsAt.Should().Be(Created);
        response.ActivityEndsAt.Should().Be(Updated);
        response.EventId.Should().Be(eventId);
        response.RoleType.Should().Be(new AssignedActivityRoleResponse(roleTypeId, "Speaker"));
        response.Status.Should().Be(new AssignedActivityStatusResponse(statusId, "Confirmed"));
    }
}
