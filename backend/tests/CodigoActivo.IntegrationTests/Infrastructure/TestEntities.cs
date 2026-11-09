using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class TestEntities
{
    private static readonly Dictionary<Type, object> Defaults = new()
    {
        [typeof(Event)] = new
        {
            Title = "Evento",
            Subtitle = "Sub",
            Description = "{}",
            CreatedBy = TestSeedData.Users.AdminId,
        },
        [typeof(Activity)] = new
        {
            Description = "Descripción",
            Location = "Sala",
            Modality = ActivityModality.Presencial,
            CreatedBy = TestSeedData.Users.AdminId,
        },
        [typeof(NewsItem)] = new
        {
            Subtitle = "Sub",
            Description = "{}",
            CreatedBy = TestSeedData.Users.AdminId,
        },
        [typeof(TermsDocument)] = new { Description = "{}" },
        [typeof(StoredFile)] = new { Extension = "png", UploadedBy = TestSeedData.Users.AdminId },
    };

    public static T New<T>(object state)
        where T : class
    {
        var entity = Persisted.As<T>(Defaults[typeof(T)]);
        Persisted.Overwrite(entity, state);
        return entity;
    }
}
