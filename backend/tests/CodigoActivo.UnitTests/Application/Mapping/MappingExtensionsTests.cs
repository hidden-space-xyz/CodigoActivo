using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Mapping;

/// <summary>
/// Unit tests for the entity → DTO <c>ToResponse</c> extension methods, covering the null-navigation
/// fallbacks on <see cref="User"/> as well as the flat mappings for the remaining entities.
/// </summary>
public sealed class MappingExtensionsTests
{
    private static readonly DateTimeOffset Created = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2025, 6, 7, 8, 9, 10, TimeSpan.Zero);

    // ---- User.ToResponse ---------------------------------------------------

    [Fact]
    public void User_ToResponse_maps_scalars_status_and_roles()
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
            Phone = "+34123",
            BirthDate = new DateOnly(1990, 3, 4),
            LastLoginAt = Updated,
            CreatedAt = Created,
            UpdatedAt = Updated,
            ParentId = parentId,
            UserStatusTypeId = statusId,
            UserStatusType = new UserStatusType { Id = statusId, Name = "Active", Color = "#00ff00" },
            TypeAssignments =
            [
                new UserTypeAssignment
                {
                    UserTypeId = roleId,
                    UserType = new UserType { Id = roleId, Name = "Member", Color = "#123456" },
                },
            ],
        };

        var response = user.ToResponse();

        response.Id.Should().Be(user.Id);
        response.FirstName.Should().Be("Ada");
        response.LastName.Should().Be("Lovelace");
        response.Email.Should().Be("ada@test.dev");
        response.Phone.Should().Be("+34123");
        response.BirthDate.Should().Be(new DateOnly(1990, 3, 4));
        response.LastLoginAt.Should().Be(Updated);
        response.CreatedAt.Should().Be(Created);
        response.UpdatedAt.Should().Be(Updated);
        response.ParentId.Should().Be(parentId);
        response.Status.Should().Be(new UserStatusResponse(statusId, "Active", "#00ff00"));
        response.Roles.Should().ContainSingle().Which.Should().Be(new UserRoleResponse(roleId, "Member", "#123456"));
    }

    [Fact]
    public void User_ToResponse_uses_empty_status_name_and_color_when_status_navigation_null()
    {
        var statusId = Guid.NewGuid();
        var user = new User
        {
            FirstName = "N",
            LastName = "N",
            UserStatusTypeId = statusId,
            UserStatusType = null!,
        };

        var response = user.ToResponse();

        response.Status.Id.Should().Be(statusId);
        response.Status.Name.Should().BeEmpty();
        response.Status.Color.Should().BeEmpty();
    }

    [Fact]
    public void User_ToResponse_returns_empty_roles_when_assignments_null()
    {
        var user = new User
        {
            FirstName = "N",
            LastName = "N",
            UserStatusType = new UserStatusType { Name = "S", Color = "#fff" },
            TypeAssignments = null!,
        };

        var response = user.ToResponse();

        response.Roles.Should().BeEmpty();
    }

    [Fact]
    public void User_ToResponse_uses_empty_role_name_and_color_when_user_type_navigation_null()
    {
        var roleId = Guid.NewGuid();
        var user = new User
        {
            FirstName = "N",
            LastName = "N",
            UserStatusType = new UserStatusType { Name = "S", Color = "#fff" },
            TypeAssignments = [new UserTypeAssignment { UserTypeId = roleId, UserType = null! }],
        };

        var response = user.ToResponse();

        var role = response.Roles.Should().ContainSingle().Subject;
        role.Id.Should().Be(roleId);
        role.Name.Should().BeEmpty();
        role.Color.Should().BeEmpty();
    }

    // ---- Resource.ToResponse ----------------------------------------------

    [Fact]
    public void Resource_ToResponse_maps_all_fields()
    {
        var updatedBy = Guid.NewGuid();
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Subtitle = "Sub",
            Description = "{\"d\":1}",
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = updatedBy,
            ThumbnailId = Guid.NewGuid(),
        };

        var response = resource.ToResponse();

        response.Should().BeEquivalentTo(new ResourceResponse(
            resource.Id,
            "T",
            "Sub",
            "{\"d\":1}",
            Created,
            Updated,
            resource.CreatedBy,
            updatedBy,
            resource.ThumbnailId
        ));
    }

    // ---- Announcement.ToResponse ------------------------------------------

    [Fact]
    public void Announcement_ToResponse_maps_all_fields_including_featured()
    {
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Subtitle = "Sub",
            Description = "{}",
            CreatedAt = Created,
            UpdatedAt = null,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = null,
            ThumbnailId = Guid.NewGuid(),
            Featured = true,
        };

        var response = announcement.ToResponse();

        response.Should().BeEquivalentTo(new AnnouncementResponse(
            announcement.Id,
            "T",
            "Sub",
            "{}",
            Created,
            null,
            announcement.CreatedBy,
            null,
            announcement.ThumbnailId,
            true
        ));
    }

    // ---- Partner.ToResponse -----------------------------------------------

    [Fact]
    public void Partner_ToResponse_maps_all_fields_and_maps_web_to_website()
    {
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            FromDate = new DateOnly(2024, 5, 6),
            Tier = 3,
            Web = "https://acme.test",
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
        };

        var response = partner.ToResponse();

        response.Should().BeEquivalentTo(new PartnerResponse(
            partner.Id,
            "Acme",
            new DateOnly(2024, 5, 6),
            3,
            "https://acme.test",
            Created,
            Updated,
            partner.CreatedBy,
            partner.UpdatedBy,
            partner.ThumbnailId
        ));
    }

    [Fact]
    public void Partner_ToResponse_preserves_null_website()
    {
        var partner = new Partner { Name = "Acme", Web = null };

        partner.ToResponse().Website.Should().BeNull();
    }

    // ---- FileEntity.ToResponse --------------------------------------------

    [Fact]
    public void File_ToResponse_maps_all_fields()
    {
        var uploadedBy = Guid.NewGuid();
        var file = new FileEntity
        {
            Id = Guid.NewGuid(),
            Name = "photo",
            Extension = ".png",
            UploadedAt = Created,
            UploadedBy = uploadedBy,
        };

        var response = file.ToResponse();

        response.Should().Be(new FileResponse(file.Id, "photo", ".png", Created, uploadedBy));
    }

    // ---- ActivityRoleType.ToResponse --------------------------------------

    [Fact]
    public void ActivityRoleType_ToResponse_maps_id_name_description()
    {
        var roleType = new ActivityRoleType
        {
            Id = Guid.NewGuid(),
            Name = "Volunteer",
            Description = "Helps out",
        };

        var response = roleType.ToResponse();

        response.Should().Be(new ActivityRoleTypeResponse(roleType.Id, "Volunteer", "Helps out"));
    }

    // ---- EventCategoryType.ToResponse -------------------------------------

    [Fact]
    public void EventCategoryType_ToResponse_maps_id_name_color()
    {
        var categoryType = new EventCategoryType
        {
            Id = Guid.NewGuid(),
            Name = "Workshop",
            Color = "#abcdef",
        };

        var response = categoryType.ToResponse();

        response.Should().Be(new EventCategoryTypeResponse(categoryType.Id, "Workshop", "#abcdef"));
    }
}
