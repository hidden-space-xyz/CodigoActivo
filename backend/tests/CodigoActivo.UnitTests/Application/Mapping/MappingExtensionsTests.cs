using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Entities;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Mapping;

public sealed class MappingExtensionsTests
{
    private static readonly DateTimeOffset Created = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2025, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public void ToResponse_FullUserWithNavigations_MapsScalarsStatusAndNullType()
    {
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
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
            UserStatusType = new UserStatusType
            {
                Id = statusId,
                Name = "Active",
                Color = "#00ff00",
            },
            IsAdmin = true,
            UserTypeId = typeId,
            UserType = new UserType
            {
                Id = typeId,
                Name = "Member",
                Color = "#123456",
            },
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
        response.IsAdmin.Should().BeTrue();
        response.Type.Should().BeNull();
    }

    [Fact]
    public void ToResponse_NullStatusNavigation_MapsEmptyStatusNameAndColor()
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
}
