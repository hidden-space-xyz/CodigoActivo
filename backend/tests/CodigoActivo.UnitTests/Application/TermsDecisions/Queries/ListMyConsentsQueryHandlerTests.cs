using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Application.Participation.Queries;
using CodigoActivo.Application.TermsDecisions.Contracts;
using CodigoActivo.Application.TermsDecisions.Queries;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.TermsDecisions.Queries;

public sealed class ListMyConsentsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly UserRow marta = Person("Marta", "Ruiz");
    private readonly UserRow leo;
    private readonly TermsDocumentRow image = Document("Imagen", TermsDocumentKind.Consent);
    private readonly TermsDocumentRow rules = Document("Normas", TermsDocumentKind.Conditions);
    private readonly EventRow fair = Event("Feria", new DateOnly(2026, 8, 1));
    private readonly EventRow camp = Event("Campamento", new DateOnly(2026, 9, 1));
    private readonly ListMyConsentsQueryHandler sut;

    public ListMyConsentsQueryHandlerTests()
    {
        leo = Person("Leo", "Ruiz", marta.Id);
        store.Users.AddRange(marta, leo);
        sut = new ListMyConsentsQueryHandler(
            store,
            new FakeQueryExecutor(),
            new TestCurrentUser(UserId.From(marta.Id))
        );
    }

    private static UserRow Person(string firstName, string lastName, Guid? parentId = null)
    {
        return new UserRow
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            ParentId = parentId,
        };
    }

    private static TermsDocumentRow Document(string name, TermsDocumentKind kind)
    {
        return new TermsDocumentRow
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = kind,
        };
    }

    private static EventRow Event(string title, DateOnly startsAt)
    {
        return new EventRow
        {
            Id = Guid.NewGuid(),
            Title = title,
            EventStartsAt = startsAt,
        };
    }

    private void Decision(
        UserRow person,
        EventRow ev,
        TermsDocumentRow document,
        params bool[] history
    )
    {
        store.EventTermsAcceptances.Add(
            new EventTermsAcceptanceRow
            {
                EventId = ev.Id,
                Event = ev,
                UserId = person.Id,
                User = person,
                DecidedById = marta.Id,
                DecidedBy = marta,
                TermsDocumentId = document.Id,
                TermsDocument = document,
                Accepted = history[^1],
                DecidedAt = Now,
                Changes =
                [
                    .. history.Select(accepted => new TermsDecisionChangeRow
                    {
                        Accepted = accepted,
                    }),
                ],
            }
        );
    }

    private Task<IReadOnlyList<MyConsentResponse>> ListAsync()
    {
        return sut.HandleAsync(new ListMyConsentsQuery(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncListsTheConsentsOfTheHouseholdLatestEventFirstWithoutConditions()
    {
        Decision(marta, fair, image, true);
        Decision(leo, camp, image, true);
        Decision(leo, fair, rules, true);

        var consents = await ListAsync();

        consents
            .Should()
            .Equal(
                new MyConsentResponse(
                    camp.Id,
                    "Campamento",
                    new DateOnly(2026, 9, 1),
                    image.Id,
                    "Imagen",
                    leo.Id,
                    "Leo Ruiz",
                    true,
                    false,
                    Now,
                    "Marta Ruiz"
                ),
                new MyConsentResponse(
                    fair.Id,
                    "Feria",
                    new DateOnly(2026, 8, 1),
                    image.Id,
                    "Imagen",
                    marta.Id,
                    "Marta Ruiz",
                    true,
                    false,
                    Now,
                    "Marta Ruiz"
                )
            );
    }

    [Fact]
    public async Task HandleAsyncTellsAWithdrawnConsentFromOneNeverGiven()
    {
        Decision(marta, fair, image, true, false);
        Decision(leo, fair, image, false);

        var consents = await ListAsync();

        consents
            .Select(consent => (consent.PersonName, consent.Accepted, consent.Withdrawn))
            .Should()
            .Equal(("Leo Ruiz", false, false), ("Marta Ruiz", false, true));
    }

    [Fact]
    public async Task HandleAsyncLeavesOutOtherHouseholds()
    {
        var stranger = Person("Ana", "Gil");
        store.Users.Add(stranger);
        Decision(stranger, fair, image, true);

        var consents = await ListAsync();

        consents.Should().BeEmpty();
    }
}
