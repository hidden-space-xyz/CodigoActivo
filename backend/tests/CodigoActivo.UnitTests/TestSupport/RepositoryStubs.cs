using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using NSubstitute;

namespace CodigoActivo.UnitTests.TestSupport;

public static class RepositoryStubs
{
    public static void Finds(this IUserRepository users, User? user)
    {
        users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(user);
        users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
    }

    public static void Finds(this IPartnerRepository partners, Partner? partner)
    {
        partners.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(partner);
    }

    public static void Finds(this INewsItemRepository news, NewsItem? newsItem)
    {
        news.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(newsItem);
    }

    public static void Finds(this IResourceRepository resources, Resource? resource)
    {
        resources.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(resource);
    }

    public static void Finds(
        this IEventCategoryTypeRepository categoryTypes,
        EventCategoryType? categoryType
    )
    {
        categoryTypes
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(categoryType);
    }

    public static void Finds(
        this ITermsDocumentRepository termsDocuments,
        TermsDocument? termsDocument
    )
    {
        termsDocuments
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(termsDocument);
    }

    public static void Finds(this IStoredFileRepository files, StoredFile? file)
    {
        files.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(file);
    }

    public static void Finds(this IEventRepository events, Event? ev)
    {
        events.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ev);
    }

    public static void Finds(this IActivityRepository activities, Activity? activity)
    {
        activities.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(activity);
    }

    public static void ThumbnailExists(this IStoredFileRepository files, bool exists)
    {
        files.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(exists);
    }
}
