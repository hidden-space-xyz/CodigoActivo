using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Partners;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Application.Users;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common;

public sealed class ProjectionsTests
{
    private static readonly DateTimeOffset Created = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2025, 6, 7, 8, 9, 10, TimeSpan.Zero);

    private static TResult Project<TSource, TResult>(
        Expression<Func<TSource, TResult>> projection,
        TSource source
    )
    {
        return projection.Compile().Invoke(source);
    }

    private static EventRow NewEvent(Guid categoryTypeId, string description)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Title = "Conf",
            Subtitle = "Sub",
            Description = description,
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
                new EventCategoryRow
                {
                    EventCategoryTypeId = categoryTypeId,
                    EventCategoryType = new EventCategoryTypeRow
                    {
                        Id = categoryTypeId,
                        Name = "Tech",
                        Color = "#111",
                    },
                },
            ],
        };
    }

    [Fact]
    public void EventEventWithCategoriesMapsScalarsAndCategories()
    {
        var categoryTypeId = Guid.NewGuid();
        var @event = NewEvent(categoryTypeId, "{}");

        var response = Project(EventProjections.Event, @event);

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
        response.ThumbnailId.Should().Be(@event.ThumbnailId);
        response.Featured.Should().BeTrue();
        response
            .Categories.Should()
            .ContainSingle()
            .Which.Should()
            .Be(new EventCategoryResponse(categoryTypeId, "Tech", "#111"));
    }

    [Fact]
    public void EventEventWithoutCategoriesYieldsEmptyCategories()
    {
        var @event = new EventRow { Title = "T", Subtitle = "S" };

        Project(EventProjections.Event, @event).Categories.Should().BeEmpty();
    }

    [Fact]
    public void EventListItemEventWithCategoriesMapsScalarsAndCategoriesWithoutDescription()
    {
        var categoryTypeId = Guid.NewGuid();
        var @event = NewEvent(categoryTypeId, "{\"heavy\":true}");

        var response = Project(EventProjections.EventListItem, @event);

        response
            .Should()
            .BeEquivalentTo(
                new EventListItemResponse(
                    @event.Id,
                    "Conf",
                    "Sub",
                    new DateOnly(2024, 8, 1),
                    new DateOnly(2024, 8, 3),
                    null,
                    Created,
                    Updated,
                    Created,
                    Updated,
                    @event.ThumbnailId,
                    true,
                    [new EventCategoryResponse(categoryTypeId, "Tech", "#111")],
                    EventStage.Upcoming
                )
            );
        typeof(EventListItemResponse).GetProperty("Description").Should().BeNull();
        typeof(EventListItemResponse).GetProperty("CreatedBy").Should().BeNull();
        typeof(EventListItemResponse).GetProperty("UpdatedBy").Should().BeNull();
    }

    [Fact]
    public void PartnerPartnerWithWebMapsAllFieldsIncludingWebsite()
    {
        var partner = new PartnerRow
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

        var response = Project(PartnerProjections.Partner, partner);

        response
            .Should()
            .BeEquivalentTo(
                new PartnerResponse(
                    partner.Id,
                    "Acme",
                    new DateOnly(2024, 5, 6),
                    2,
                    "https://acme.test",
                    Created,
                    Updated,
                    partner.ThumbnailId
                )
            );
    }

    [Fact]
    public void ActivityActivityWithModalityMapsScalarsAndModality()
    {
        var modalityId = Guid.NewGuid();
        var activity = new ActivityRow
        {
            Id = Guid.NewGuid(),
            Title = "Talk",
            Description = "Desc",
            Location = "Room 1",
            ActivityStartsAt = Created,
            ActivityEndsAt = Updated,
            EventId = Guid.NewGuid(),
            ActivityModalityTypeId = modalityId,
            ActivityModalityType = new ActivityModalityTypeRow
            {
                Id = modalityId,
                Name = "InPerson",
            },
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = Created,
            UpdatedAt = Updated,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
        };

        var response = Project(ActivityProjections.Activity, activity);

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
    }

    private static UserRow NewUser(
        Guid statusId,
        Guid typeId,
        Guid? parentId = null,
        int children = 0,
        Guid? parentTypeId = null
    )
    {
        var user = new UserRow
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.dev",
            Phone = "+34",
            BirthDate = new DateOnly(1990, 3, 4),
            NationalId = "12345678Z",
            PromotionalConsent = true,
            Gender = Gender.Other,
            LastLoginAt = Updated,
            CreatedAt = Created,
            UpdatedAt = Updated,
            ParentId = parentId,
            Parent = parentId is null
                ? null
                : new UserRow
                {
                    Id = parentId.Value,
                    FirstName = "Grace",
                    LastName = "Hopper",
                    UserTypeId = parentTypeId ?? Guid.NewGuid(),
                },
            UserStatusTypeId = statusId,
            UserStatusType = new UserStatusTypeRow
            {
                Description = "Descripción de prueba",
                Id = statusId,
                Name = "Active",
                Color = "#0f0",
            },
            IsAdmin = true,
            UserTypeId = typeId,
            UserType = new UserTypeRow
            {
                Description = "Descripción de prueba",
                Id = typeId,
                Name = "Member",
                Color = "#00f",
            },
        };
        for (var i = 0; i < children; i++)
        {
            user.Children.Add(new UserRow { FirstName = "Kid", LastName = "One" });
        }

        return user;
    }

    [Fact]
    public void UserUserWithParentAndChildrenMapsScalarsLeavingTypeParentNameAndDependentCountNull()
    {
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(statusId, typeId, parentId, children: 1);

        var response = Project(UserProjections.User, user);

        response.Id.Should().Be(user.Id);
        response.FirstName.Should().Be("Ada");
        response.LastName.Should().Be("Lovelace");
        response.Email.Should().Be("ada@test.dev");
        response.Phone.Should().Be("+34");
        response.BirthDate.Should().Be(new DateOnly(1990, 3, 4));
        response.NationalId.Should().Be("12345678Z");
        response.PromotionalConsent.Should().BeTrue();
        response.Gender.Should().Be(Gender.Other);
        response.LastLoginAt.Should().Be(Updated);
        response.CreatedAt.Should().Be(Created);
        response.UpdatedAt.Should().Be(Updated);
        response.ParentId.Should().Be(parentId);
        response.ParentName.Should().BeNull();
        response.DependentCount.Should().BeNull();
        response.Status.Should().Be(new UserStatusResponse(statusId, "Active", "#0f0"));
        response.IsAdmin.Should().BeTrue();
        response.Type.Should().BeNull();
    }

    [Fact]
    public void UserWithTypeUserWithParentAndChildrenMapsScalarsTypeParentNameAndDependentCount()
    {
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(statusId, typeId, parentId, children: 2);

        var response = Project(UserProjections.UserWithType, user);

        response.Id.Should().Be(user.Id);
        response.FirstName.Should().Be("Ada");
        response.LastName.Should().Be("Lovelace");
        response.Email.Should().Be("ada@test.dev");
        response.Phone.Should().Be("+34");
        response.BirthDate.Should().Be(new DateOnly(1990, 3, 4));
        response.NationalId.Should().Be("12345678Z");
        response.PromotionalConsent.Should().BeTrue();
        response.Gender.Should().Be(Gender.Other);
        response.LastLoginAt.Should().Be(Updated);
        response.CreatedAt.Should().Be(Created);
        response.UpdatedAt.Should().Be(Updated);
        response.ParentId.Should().Be(parentId);
        response.ParentName.Should().Be("Grace Hopper");
        response.DependentCount.Should().Be(2);
        response.Status.Should().Be(new UserStatusResponse(statusId, "Active", "#0f0"));
        response.IsAdmin.Should().BeTrue();
        response.Type.Should().Be(new UserTypeSummaryResponse(typeId, "Member", "#00f"));
    }

    [Fact]
    public void UserWithTypeUserWithoutParentOrChildrenYieldsNullParentNameAndZeroDependentCount()
    {
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var user = NewUser(statusId, typeId);

        var response = Project(UserProjections.UserWithType, user);

        response.ParentId.Should().BeNull();
        response.ParentName.Should().BeNull();
        response.DependentCount.Should().Be(0);
    }

    public static TheoryData<Guid, bool> EarlySignupTypes =>
        new()
        {
            { SeedIds.UserTypes.Member, true },
            { SeedIds.UserTypes.Sponsor, true },
            { Guid.NewGuid(), false },
        };

    [Theory]
    [MemberData(nameof(EarlySignupTypes))]
    public void UserIndependentAccountEarlySignupFollowsItsOwnType(Guid typeId, bool expected)
    {
        var user = NewUser(Guid.NewGuid(), typeId);

        Project(UserProjections.User, user).EarlySignupEligible.Should().Be(expected);
        Project(UserProjections.UserWithType, user).EarlySignupEligible.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EarlySignupTypes))]
    public void UserDependentEarlySignupFollowsItsGuardianType(Guid guardianTypeId, bool expected)
    {
        var user = NewUser(
            Guid.NewGuid(),
            SeedIds.UserTypes.Member,
            Guid.NewGuid(),
            parentTypeId: guardianTypeId
        );

        Project(UserProjections.User, user).EarlySignupEligible.Should().Be(expected);
        Project(UserProjections.UserWithType, user).EarlySignupEligible.Should().Be(expected);
    }

    [Fact]
    public void AssignedActivityAssignmentWithActivityRoleAndStatusMapsActivityChainRoleAndStatus()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var assignment = new AssignmentRow
        {
            ActivityId = activityId,
            Activity = new ActivityRow
            {
                Location = "Sala principal",
                Id = activityId,
                Title = "Talk",
                Description = "Desc",
                ActivityStartsAt = Created,
                ActivityEndsAt = Updated,
                EventId = eventId,
            },
            ActivityRoleTypeId = roleTypeId,
            ActivityRoleType = new ActivityRoleTypeRow
            {
                Description = "Descripción de prueba",
                Id = roleTypeId,
                Name = "Speaker",
            },
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusTypeRow
            {
                Description = "Descripción de prueba",
                Color = "#0EA5E9",
                Id = statusId,
                Name = "Confirmed",
            },
        };

        var response = Project(ActivityProjections.AssignedActivity, assignment);

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
