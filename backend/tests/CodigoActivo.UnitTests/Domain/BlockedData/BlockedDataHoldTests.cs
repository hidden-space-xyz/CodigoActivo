using AwesomeAssertions;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.BlockedData;

public sealed class BlockedDataHoldTests
{
    private static readonly DateTimeOffset Opened = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly UserId Officer = UserId.New();
    private static readonly UserId Account = UserId.New();
    private static readonly UserId Minor = UserId.New();

    private static BlockedDataConsultation Consultation()
    {
        var consultation = BlockedDataConsultation
            .Open(Officer, "Persona reclamante", "Reclamación 7/2026", Opened)
            .Value;
        consultation.RecordFindings([Account, Minor], Opened);
        return consultation;
    }

    private static BlockedDataHold Placed()
    {
        return BlockedDataHold.Place(Consultation(), Account, [Minor], Opened.AddMinutes(1)).Value;
    }

    [Fact]
    public void PlaceFoundAccountKeepsItAndItsMinorsForTheRequestOfTheConsultation()
    {
        var hold = BlockedDataHold
            .Place(Consultation(), Account, [Minor, Account, Minor], Opened.AddMinutes(1))
            .Value;

        hold.AccountId.Should().Be(Account);
        hold.Requester.Should().Be("Persona reclamante");
        hold.Reference.Should().Be("Reclamación 7/2026");
        hold.PlacedById.Should().Be(Officer);
        hold.PlacedAt.Should().Be(Opened.AddMinutes(1));
        hold.LiftedAt.Should().BeNull();
        hold.LiftedById.Should().BeNull();
        hold.Subjects.Select(subject => subject.AccountId).Should().Equal(Account, Minor);
        hold.Subjects.Should().OnlyContain(subject => subject.HoldId == hold.Id);
    }

    [Fact]
    public void PlaceAccountNoSearchFoundIsRefused()
    {
        BlockedDataHold
            .Place(Consultation(), UserId.New(), [], Opened)
            .ShouldFail(ErrorKind.NotFound, DomainErrorCode.BlockedDataAccountNotFound);
    }

    [Fact]
    public void LiftActiveHoldRecordsWhoLiftedItAndWhen()
    {
        var hold = Placed();
        var lifter = UserId.New();

        hold.Lift(lifter, Opened.AddYears(2)).IsSuccess.Should().BeTrue();

        hold.LiftedById.Should().Be(lifter);
        hold.LiftedAt.Should().Be(Opened.AddYears(2));
    }

    [Fact]
    public void LiftLiftedHoldIsRefusedAndKeepsTheFirstLifting()
    {
        var hold = Placed();
        hold.Lift(Officer, Opened.AddYears(2));

        hold.Lift(UserId.New(), Opened.AddYears(3))
            .ShouldFail(ErrorKind.Conflict, DomainErrorCode.BlockedDataHoldLifted);

        hold.LiftedById.Should().Be(Officer);
        hold.LiftedAt.Should().Be(Opened.AddYears(2));
    }

    [Fact]
    public void PurgeCutoffKeepsLiftedHoldsFiveYears()
    {
        BlockedDataHold.PurgeCutoff(Opened).Should().Be(Opened.AddYears(-5));
    }
}
