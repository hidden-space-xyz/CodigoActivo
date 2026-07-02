using CodigoActivo.Application.DTOs;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace CodigoActivo.API.OData;

/// <summary>
/// The read model exposed over OData. Every collection here is a read-only projection of a
/// domain entity to its response DTO; clients compose $filter/$orderby/$top/$skip/$count on top.
/// Computed reads (reports, overlap checks, household assignments) are exposed as unbound
/// functions returning complex types. There is intentionally no write surface — mutations stay
/// on the command controllers.
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

        // Computed-read DTOs are function return shapes. They carry Id-like properties, so they
        // must be declared complex explicitly or the convention builder would infer entity sets.
        builder.ComplexType<EventSummaryResponse>();
        builder.ComplexType<EventRoleTypeSummaryResponse>();
        builder.ComplexType<EventAssignmentsReportResponse>();
        builder.ComplexType<AssignmentReportItemResponse>();
        builder.ComplexType<ActivityAssignmentsReportResponse>();
        builder.ComplexType<ActivityRoleTypeSummaryResponse>();
        builder.ComplexType<ActivityAssignmentRowResponse>();
        builder.ComplexType<DashboardSummaryResponse>();
        builder.ComplexType<TimeOverlapResponse>();
        builder.ComplexType<OverlappingActivityResponse>();
        builder.ComplexType<HouseholdMemberAssignmentResponse>();

        builder.EntitySet<EventResponse>("Events");
        builder.EntitySet<AnnouncementResponse>("Announcements");
        builder.EntitySet<ResourceResponse>("Resources");
        builder.EntitySet<PartnerResponse>("Partners");
        builder.EntitySet<ActivityResponse>("Activities");
        builder.EntitySet<UserResponse>("Users");
        builder.EntitySet<FileResponse>("Files");
        builder.EntitySet<ActivityRoleTypeResponse>("ActivityRoleTypes");
        builder.EntitySet<AssignmentStatusTypeResponse>("AssignmentStatusTypes");
        builder.EntitySet<UserTypeResponse>("UserTypes");
        builder.EntitySet<UserStatusTypeResponse>("UserStatusTypes");
        builder.EntitySet<RegistrationTypeResponse>("RegistrationTypes");

        // The assignment projection is keyed by the activity within the caller's own scope.
        builder
            .EntitySet<AssignedActivityResponse>("AssignedActivities")
            .EntityType.HasKey(x => x.ActivityId);

        RegisterFunctions(builder);

        return builder.GetEdmModel();
    }

    /// <summary>
    /// Unbound functions expose server-computed reads (formerly the REST report/verify endpoints).
    /// Each maps to a GET action on an OData controller; see OData/ReportsController and
    /// OData/ActivitiesController.
    /// </summary>
    private static void RegisterFunctions(ODataConventionModelBuilder builder)
    {
        var eventSummary = builder.Function("EventSummary");
        eventSummary.Parameter<Guid>("eventId");
        eventSummary.Returns<EventSummaryResponse>();

        var eventAssignments = builder.Function("EventAssignments");
        eventAssignments.Parameter<Guid>("eventId");
        eventAssignments.Returns<EventAssignmentsReportResponse>();

        var activityAssignments = builder.Function("ActivityAssignments");
        activityAssignments.Parameter<Guid>("activityId");
        activityAssignments.Returns<ActivityAssignmentsReportResponse>();

        builder.Function("DashboardSummary").Returns<DashboardSummaryResponse>();

        var verifyTimeOverlaps = builder.Function("VerifyTimeOverlaps");
        verifyTimeOverlaps.Parameter<Guid>("activityId");
        verifyTimeOverlaps.Parameter<Guid>("userId");
        verifyTimeOverlaps.Returns<TimeOverlapResponse>();

        var householdAssignments = builder.Function("HouseholdAssignments");
        householdAssignments.Parameter<Guid>("eventId");
        householdAssignments.ReturnsCollection<HouseholdMemberAssignmentResponse>();
    }
}
