using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.API.Resources.Contracts;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.API.Contracts;

public sealed class CatalogRequestTests
{
    private static readonly Guid Unknown = Guid.NewGuid();

    private static CreateActivityRequest ActivityRequest(
        Guid modalityId,
        IReadOnlyList<ActivityRoleCapacityRequest>? capacities = null
    )
    {
        return new CreateActivityRequest(
            "Taller",
            "Descripción",
            "Sala",
            modalityId,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(1),
            Guid.NewGuid(),
            capacities
        );
    }

    private static CreateResourceRequest ResourceRequest(Guid typeId)
    {
        return new CreateResourceRequest("Título", "Subtítulo", null, null, typeId, Guid.NewGuid());
    }

    private static UpdateActivityRequest UpdateActivity(Guid modalityId)
    {
        return new UpdateActivityRequest(
            "Taller",
            "Descripción",
            "Sala",
            modalityId,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(1),
            Guid.NewGuid(),
            null
        );
    }

    private static UpdateResourceRequest UpdateResource(Guid typeId)
    {
        return new UpdateResourceRequest("Título", "Subtítulo", null, null, typeId, Guid.NewGuid());
    }

    private static bool IsValid(object request)
    {
        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            [],
            validateAllProperties: true
        );
    }

    public static TheoryData<object> RequestsWithAnUnknownIdentifier()
    {
        return
        [
            new AssignRequest(Unknown),
            new HouseholdAssignmentRequest(Guid.NewGuid(), Unknown),
            new ActivityRoleCapacityRequest(Unknown, 1),
            new ChangeAssignmentRoleRequest(Unknown),
            new ChangeAssignmentStatusRequest(Unknown),
            ActivityRequest(Unknown),
            ResourceRequest(Unknown),
            UpdateActivity(Unknown),
            UpdateResource(Unknown),
        ];
    }

    public static TheoryData<object> RequestsWithKnownIdentifiers()
    {
        return
        [
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            new HouseholdAssignmentRequest(Guid.NewGuid(), KnownIds.ActivityRoleTypes.Leader),
            new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Volunteer, 1),
            new ChangeAssignmentRoleRequest(KnownIds.ActivityRoleTypes.Leader),
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed),
            ActivityRequest(KnownIds.ActivityModalityTypes.Presencial),
            ResourceRequest(KnownIds.ResourceTypes.External),
            UpdateActivity(KnownIds.ActivityModalityTypes.Presencial),
            UpdateResource(KnownIds.ResourceTypes.Internal),
        ];
    }

    [Theory]
    [MemberData(nameof(RequestsWithAnUnknownIdentifier))]
    public void ValidateUnknownCatalogIdentifierRejectsTheRequest(object request)
    {
        IsValid(request).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(RequestsWithKnownIdentifiers))]
    public void ValidateKnownCatalogIdentifiersAcceptsTheRequest(object request)
    {
        IsValid(request).Should().BeTrue();
    }

    [Fact]
    public void ToCommandActivityRequestCarriesTheDomainValues()
    {
        var command = ActivityRequest(
                KnownIds.ActivityModalityTypes.Presencial,
                [new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Volunteer, 3)]
            )
            .ToCommand(EventId.New());

        command.Activity.Modality.Should().Be(ActivityModality.Presencial);
        command
            .Activity.RoleCapacities.Should()
            .Equal(new RoleCapacityDraft(ActivityRole.Volunteer, 3));
    }

    [Fact]
    public void ToCommandSignupRequestsCarryTheDomainRoleAndStatus()
    {
        var activityId = ActivityId.New();
        var userId = UserId.New();

        new AssignRequest(KnownIds.ActivityRoleTypes.Leader)
            .ToCommand(activityId, userId)
            .Role.Should()
            .Be(ActivityRole.Leader);
        new ChangeAssignmentRoleRequest(KnownIds.ActivityRoleTypes.Volunteer)
            .ToCommand(activityId, userId)
            .Role.Should()
            .Be(ActivityRole.Volunteer);
        new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed)
            .ToCommand(activityId, userId)
            .Status.Should()
            .Be(AssignmentStatus.Confirmed);
    }

    [Fact]
    public void ToCommandResourceRequestCarriesTheDomainType()
    {
        ResourceRequest(KnownIds.ResourceTypes.External)
            .ToCommand()
            .Type.Should()
            .Be(ResourceType.External);
    }
}
