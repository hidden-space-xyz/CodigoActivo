using CodigoActivo.Application.DTOs;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace CodigoActivo.API.OData;

public static class EdmModelBuilder
{
    public static IEdmModel Build()
    {
        var builder = new ODataConventionModelBuilder
        {
            Namespace = "CodigoActivo",
            ContainerName = "Read"
        };
        builder.EnableLowerCamelCase();

        builder.ComplexType<UserStatusResponse>();
        builder.ComplexType<UserRoleResponse>();
        builder.ComplexType<ActivityAllowedRoleResponse>();
        builder.ComplexType<EventCategoryResponse>();
        builder.ComplexType<AssignedActivityRoleResponse>();
        builder.ComplexType<AssignedActivityStatusResponse>();

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
        builder.EntitySet<EventCategoryTypeResponse>("EventCategoryTypes");
        builder.EntitySet<ActivityModalityTypeResponse>("ActivityModalityTypes");

        builder
            .EntitySet<AssignedActivityResponse>("AssignedActivities")
            .EntityType.HasKey(x => x.ActivityId);

        RegisterFunctions(builder);

        return builder.GetEdmModel();
    }

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