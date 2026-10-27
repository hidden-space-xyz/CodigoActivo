using AwesomeAssertions;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.BlockedData;

public sealed class BlockedDataConsultationTests
{
    private static readonly DateTimeOffset Opened = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly UserId Officer = UserId.New();

    private static BlockedDataConsultation Open()
    {
        return BlockedDataConsultation
            .Open(Officer, BlockedDataAuthority.Court, "  Diligencias 123/2026  ", Opened)
            .Value;
    }

    [Fact]
    public void OpenValidRequestLastsFifteenMinutesWithItsNoticePending()
    {
        var consultation = Open();

        consultation.OfficerId.Should().Be(Officer);
        consultation.Authority.Should().Be(BlockedDataAuthority.Court);
        consultation.Reference.Should().Be("Diligencias 123/2026");
        consultation.OpenedAt.Should().Be(Opened);
        consultation.ExpiresAt.Should().Be(Opened.AddMinutes(15));
        consultation.NoticePending.Should().BeTrue();
    }

    [Fact]
    public void RecordNoticeClearsThePendingNotice()
    {
        var consultation = Open();

        consultation.RecordNotice();

        consultation.NoticePending.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void OpenWithoutReferenceIsRefused(string? reference)
    {
        BlockedDataConsultation
            .Open(Officer, BlockedDataAuthority.Claim, reference, Opened)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.BlockedDataReferenceRequired);
    }

    [Fact]
    public void OpenUnknownAuthorityIsRefused()
    {
        BlockedDataConsultation
            .Open(Officer, (BlockedDataAuthority)99, "Ref", Opened)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.BlockedDataReferenceRequired);
    }

    [Fact]
    public void EnsureOpenForItsOfficerBeforeItEndsSucceeds()
    {
        Open().EnsureOpenFor(Officer, Opened.AddMinutes(14)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureOpenForAnotherAccountIsNotFound()
    {
        Open()
            .EnsureOpenFor(UserId.New(), Opened)
            .ShouldFail(ErrorKind.NotFound, DomainErrorCode.BlockedDataConsultationNotFound);
    }

    [Fact]
    public void EnsureOpenForOnceItEndsIsExpired()
    {
        Open()
            .EnsureOpenFor(Officer, Opened.AddMinutes(15))
            .ShouldFail(ErrorKind.Forbidden, DomainErrorCode.BlockedDataConsultationExpired);
    }

    [Fact]
    public void RecordFindingsKeepsEachAccountOnceWithItsFirstFinding()
    {
        var consultation = Open();
        var account = UserId.New();
        var other = UserId.New();

        consultation.RecordFindings([account, account], Opened);
        consultation.RecordFindings([account, other], Opened.AddMinutes(1));

        consultation
            .Findings.Select(finding => (finding.AccountId, finding.FoundAt))
            .Should()
            .BeEquivalentTo([(account, Opened), (other, Opened.AddMinutes(1))]);
        consultation.Found(account).Should().BeTrue();
        consultation.Found(UserId.New()).Should().BeFalse();
    }

    [Fact]
    public void RecordExtractOfAFoundAccountKeepsItsFilters()
    {
        var consultation = Open();
        var account = UserId.New();
        consultation.RecordFindings([account], Opened);
        var filter = new BlockedDataFilter(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            ["User", "Assignment"]
        );

        var recorded = consultation.RecordExtract(
            account,
            filter,
            7,
            includesCurrentData: true,
            Opened.AddMinutes(2)
        );

        recorded.IsSuccess.Should().BeTrue();
        var extract = consultation.Extracts.Should().ContainSingle().Subject;
        extract.AccountId.Should().Be(account);
        extract.From.Should().Be(filter.From);
        extract.To.Should().Be(filter.To);
        extract.Entities.Should().Equal("User", "Assignment");
        extract.EntryCount.Should().Be(7);
        extract.IncludesCurrentData.Should().BeTrue();
        extract.ExtractedAt.Should().Be(Opened.AddMinutes(2));
    }

    [Fact]
    public void RecordExtractOfAnAccountNoSearchFoundIsRefused()
    {
        var consultation = Open();

        consultation
            .RecordExtract(UserId.New(), new BlockedDataFilter(null, null, []), 0, false, Opened)
            .ShouldFail(ErrorKind.NotFound, DomainErrorCode.BlockedDataAccountNotFound);
        consultation.Extracts.Should().BeEmpty();
    }

    [Fact]
    public void PurgeCutoffKeepsConsultationsFiveYears()
    {
        BlockedDataConsultation.PurgeCutoff(Opened).Should().Be(Opened.AddYears(-5));
    }
}
