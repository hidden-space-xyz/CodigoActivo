using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Activities.Signups;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Infrastructure.Activities;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.EventCategories;
using CodigoActivo.Infrastructure.Events;
using CodigoActivo.Infrastructure.TermsDecisions;
using CodigoActivo.Infrastructure.TermsDocuments;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Events;

/// <summary>
/// Registers events and what hangs from them: category types, terms documents, activities,
/// signups, ratings and the removal of participation.
/// </summary>
internal static class EventsRegistration
{
    /// <summary>
    /// Adds the event and activity services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEvents(this IServiceCollection services)
    {
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventRatingRepository, EventRatingRepository>();
        services.AddScoped<IEventTermsAcceptanceRepository, EventTermsAcceptanceRepository>();
        services.AddScoped<IEventCategoryTypeRepository, EventCategoryTypeRepository>();
        services.AddScoped<ITermsDocumentRepository, TermsDocumentRepository>();
        services.AddScoped<EventCategoryChecker>();
        services.AddSingleton<IConsentWithdrawalEmailComposer, ConsentWithdrawalEmailComposer>();
        services.AddScoped<ConsentWithdrawalNotifier>();

        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddSingleton<ISignupEmailComposer, SignupEmailComposer>();
        services.AddScoped<SignupGate>();
        services.AddScoped<TermsGate>();
        services.AddScoped<ActivityValidator>();
        services.AddScoped<ActivitySignupNotifier>();
        services.AddScoped<IParticipationErasureStore, ParticipationErasureStore>();
        services.AddScoped<ParticipationEraser>();
        return services;
    }
}
