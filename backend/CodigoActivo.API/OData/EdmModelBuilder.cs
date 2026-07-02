using CodigoActivo.Application.DTOs;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace CodigoActivo.API.OData;

/// <summary>
/// The read model exposed over OData. Every collection here is a read-only projection of a
/// domain entity to its response DTO; clients compose $filter/$orderby/$top/$skip/$count on top.
/// There is intentionally no write surface — mutations stay on the REST controllers.
/// </summary>
public static class EdmModelBuilder
{
    public static IEdmModel Build()
    {
        var builder = new ODataConventionModelBuilder
        {
            Namespace = "CodigoActivo",
            ContainerName = "Read",
        };
        builder.EnableLowerCamelCase();

        // Nested value objects are structural complex types, never their own entity sets.
        builder.ComplexType<UserStatusResponse>();
        builder.ComplexType<UserRoleResponse>();
        builder.ComplexType<ActivityAllowedRoleResponse>();
        builder.ComplexType<AssignedActivityRoleResponse>();
        builder.ComplexType<AssignedActivityStatusResponse>();

        builder.EntitySet<EventResponse>("Events");
        builder.EntitySet<AnnouncementResponse>("Announcements");
        builder.EntitySet<ResourceResponse>("Resources");
        builder.EntitySet<PartnerResponse>("Partners");
        builder.EntitySet<ActivityResponse>("Activities");
        builder.EntitySet<UserResponse>("Users");
        builder.EntitySet<ActivityRoleTypeResponse>("ActivityRoleTypes");
        builder.EntitySet<AssignmentStatusTypeResponse>("AssignmentStatusTypes");
        builder.EntitySet<UserTypeResponse>("UserTypes");
        builder.EntitySet<UserStatusTypeResponse>("UserStatusTypes");
        builder.EntitySet<RegistrationTypeResponse>("RegistrationTypes");

        // The assignment projection is keyed by the activity within the caller's own scope.
        builder.EntitySet<AssignedActivityResponse>("AssignedActivities").EntityType.HasKey(x =>
            x.ActivityId
        );

        return builder.GetEdmModel();
    }
}
