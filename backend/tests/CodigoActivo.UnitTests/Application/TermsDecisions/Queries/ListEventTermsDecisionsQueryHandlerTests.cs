using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.TermsDecisions.Contracts;
using CodigoActivo.Application.TermsDecisions.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.TermsDecisions.Queries;

public sealed class ListEventTermsDecisionsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly Guid eventId = Guid.NewGuid();
    private readonly TermsDocumentRow rules = Document("Normas");
    private readonly TermsDocumentRow image = Document("Autorización de imagen");
    private readonly ListEventTermsDecisionsQueryHandler sut;

    public ListEventTermsDecisionsQueryHandlerTests()
    {
        store.Events.Add(new EventRow { Id = eventId });
        sut = new ListEventTermsDecisionsQueryHandler(store, new FakeQueryExecutor());
    }

    private static TermsDocumentRow Document(string name)
    {
        return new TermsDocumentRow { Id = Guid.NewGuid(), Name = name };
    }

    private static UserRow Person(string firstName, string lastName, string? email = null)
    {
        return new UserRow
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
        };
    }

    private EventTermsAcceptanceRow Decision(
        UserRow user,
        TermsDocumentRow document,
        DateTimeOffset decidedAt,
        bool accepted = true,
        byte[]? signature = null,
        Guid? onEvent = null,
        UserRow? decidedBy = null
    )
    {
        var decision = new EventTermsAcceptanceRow
        {
            EventId = onEvent ?? eventId,
            UserId = user.Id,
            User = user,
            DecidedById = (decidedBy ?? user).Id,
            DecidedBy = decidedBy ?? user,
            TermsDocumentId = document.Id,
            TermsDocument = document,
            TermsDocumentVersion = 2,
            Accepted = accepted,
            DecidedAt = decidedAt,
            Signature = signature,
        };
        store.EventTermsAcceptances.Add(decision);
        return decision;
    }

    private async Task<PagedResult<EventTermsDecisionResponse>> ListAsync(
        EventTermsDecisionListQuery? filters = null
    )
    {
        var result = await sut.HandleAsync(
            new ListEventTermsDecisionsQuery(
                EventId.From(eventId),
                filters ?? new EventTermsDecisionListQuery()
            ),
            TestContext.Current.CancellationToken
        );
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new ListEventTermsDecisionsQuery(EventId.New(), new EventTermsDecisionListQuery()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventNotFound);
    }

    [Fact]
    public async Task HandleAsyncDecisionsOfTheEventListsThemLatestFirstTellingWhichAreSigned()
    {
        var ana = Person("Ana", "Gil", "ana@example.com");
        var luis = Person("Luis", "Mora");
        Decision(ana, rules, Now.AddHours(-2), accepted: false);
        Decision(luis, image, Now, signature: SignatureImages.Png());
        Decision(ana, image, Now.AddHours(-1), signature: SignatureImages.Png());
        Decision(ana, rules, Now, onEvent: Guid.NewGuid());

        var page = await ListAsync();

        page.Total.Should().Be(3);
        page.Items.Should()
            .Equal(
                new EventTermsDecisionResponse(
                    luis.Id,
                    "Luis",
                    "Mora",
                    null,
                    luis.Id,
                    "Luis",
                    "Mora",
                    image.Id,
                    "Autorización de imagen",
                    image.Kind,
                    2,
                    true,
                    false,
                    Now,
                    true
                ),
                new EventTermsDecisionResponse(
                    ana.Id,
                    "Ana",
                    "Gil",
                    "ana@example.com",
                    ana.Id,
                    "Ana",
                    "Gil",
                    image.Id,
                    "Autorización de imagen",
                    image.Kind,
                    2,
                    true,
                    false,
                    Now.AddHours(-1),
                    true
                ),
                new EventTermsDecisionResponse(
                    ana.Id,
                    "Ana",
                    "Gil",
                    "ana@example.com",
                    ana.Id,
                    "Ana",
                    "Gil",
                    rules.Id,
                    "Normas",
                    rules.Kind,
                    2,
                    false,
                    false,
                    Now.AddHours(-2),
                    false
                )
            );
    }

    [Fact]
    public async Task HandleAsyncDocumentFilterKeepsOnlyItsDecisions()
    {
        var ana = Person("Ana", "Gil");
        Decision(ana, rules, Now);
        Decision(ana, image, Now, signature: SignatureImages.Png());

        var page = await ListAsync(new EventTermsDecisionListQuery { TermsDocumentId = image.Id });

        page.Items.Should().ContainSingle().Which.TermsDocumentId.Should().Be(image.Id);
    }

    [Fact]
    public async Task HandleAsyncSearchMatchesNameOrEmailIgnoringAccents()
    {
        Decision(Person("Ana", "Gil", "ana@example.com"), rules, Now);
        Decision(Person("Lucía", "Mora", "lucia@example.com"), rules, Now);

        var byName = await ListAsync(new EventTermsDecisionListQuery { Search = "lucia mora" });
        var byEmail = await ListAsync(new EventTermsDecisionListQuery { Search = "ana@" });

        byName.Items.Should().ContainSingle().Which.FirstName.Should().Be("Lucía");
        byEmail.Items.Should().ContainSingle().Which.FirstName.Should().Be("Ana");
    }

    [Fact]
    public async Task HandleAsyncDecisionAboutADependentNamesTheGuardianWhoDecidedAndIsFoundByThem()
    {
        var marta = Person("Marta", "Ruiz", "marta@example.com");
        var leo = Person("Leo", "Ruiz");
        Decision(leo, rules, Now, decidedBy: marta);

        var page = await ListAsync(new EventTermsDecisionListQuery { Search = "marta@" });

        var decision = page.Items.Should().ContainSingle().Subject;
        decision.UserId.Should().Be(leo.Id);
        decision.FirstName.Should().Be("Leo");
        decision.DecidedById.Should().Be(marta.Id);
        decision.DecidedByFirstName.Should().Be("Marta");
        decision.DecidedByLastName.Should().Be("Ruiz");
    }

    [Fact]
    public async Task HandleAsyncSortByNameBreaksTiesByPersonThenDocument()
    {
        var ana = Person("Ana", "Gil");
        var bea = Person("Bea", "Gil");
        Decision(bea, rules, Now);
        Decision(ana, image, Now);
        Decision(ana, rules, Now);

        var page = await ListAsync(new EventTermsDecisionListQuery { Sort = "lastName" });

        page.Items.Select(d => (d.UserId, d.TermsDocumentId))
            .Should()
            .Equal(
                new[] { (bea.Id, rules.Id), (ana.Id, image.Id), (ana.Id, rules.Id) }
                    .OrderBy(d => d.Item1)
                    .ThenBy(d => d.Item2)
            );
    }
}
