using System.Globalization;
using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class UserProfileRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);
    private static readonly DateOnly MinorDob = new(2016, 7, 4);
    private static readonly DateOnly AdultDob = new(1986, 7, 4);
    private static readonly DateTimeOffset Created = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private static PersonDetails AdultDetails(
        string? email = "ana@test.com",
        string? phone = "600111222",
        string? secondaryPhone = "700111222",
        string? nationalId = "12345678Z",
        bool promotionalConsent = false,
        DateOnly? birthDate = null
    )
    {
        return new(
            "Ana",
            "Ruiz",
            Gender.Female,
            email,
            phone,
            secondaryPhone,
            nationalId,
            promotionalConsent,
            birthDate
        );
    }

    private static PersonDetails ChildDetails(DateOnly? birthDate)
    {
        return new(
            " Leo ",
            " Ruiz ",
            Gender.Other,
            "leo@test.com",
            "600999999",
            "700999999",
            "12345678Z",
            true,
            birthDate
        );
    }

    private static User StoredAdult()
    {
        return User.CreateIndependent(AdultDetails(), Created).Value;
    }

    private static User StoredChild(User guardian, DateOnly birthDate)
    {
        return User.CreateDependent(guardian, ChildDetails(birthDate), birthDate, Created).Value;
    }

    private static DateOnly? DateOrNull(string? value)
    {
        return value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void CreateIndependentValidDetailsStoresThemNormalizedAsAPendingParticipant()
    {
        var details = new PersonDetails(
            "  Ana ",
            " Ruiz  ",
            Gender.Female,
            "  Ana@Test.COM ",
            " 600111222 ",
            "   ",
            " x-1234567-l ",
            true
        );

        var result = User.CreateIndependent(details, Now);

        result.IsSuccess.Should().BeTrue();
        var account = result.Value;
        account.FirstName.Should().Be("Ana");
        account.LastName.Should().Be("Ruiz");
        account.Gender.Should().Be(Gender.Female);
        account.Email.Should().Be("ana@test.com");
        account.Phone.Should().Be("600111222");
        account.SecondaryPhone.Should().BeNull();
        account.NationalId.Should().Be("X1234567L");
        account.PromotionalConsent.Should().BeTrue();
        account.BirthDate.Should().BeNull();
        account.ParentId.Should().BeNull();
        account.PasswordHash.Should().BeNull();
        account.IsAdmin.Should().BeFalse();
        account.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        account.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
        account.CreatedAt.Should().Be(Now);
        account.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "600111222", null, "12345678Z", ErrorCode.UserContactInfoRequired)]
    [InlineData("   ", "600111222", null, "12345678Z", ErrorCode.UserContactInfoRequired)]
    [InlineData("ana@test.com", " ", null, "12345678Z", ErrorCode.UserContactInfoRequired)]
    [InlineData(
        "ana@test.com",
        "600111222",
        " 600111222 ",
        "12345678Z",
        ErrorCode.SecondaryPhoneSameAsPrimary
    )]
    [InlineData("ana@test.com", "600111222", null, null, ErrorCode.UserNationalIdRequired)]
    [InlineData("ana@test.com", "600111222", null, " - ", ErrorCode.UserNationalIdRequired)]
    [InlineData("ana@test.com", "600111222", null, "12345678A", ErrorCode.RequestValidationFailed)]
    [InlineData(null, null, null, null, ErrorCode.UserNationalIdRequired)]
    public void CreateIndependentAndPlanProfileChangeRefuseTheSameBrokenIndependentRules(
        string? email,
        string? phone,
        string? secondaryPhone,
        string? nationalId,
        ErrorCode expected
    )
    {
        var details = AdultDetails(email, phone, secondaryPhone, nationalId);
        var stored = StoredAdult();

        var created = User.CreateIndependent(details, Now);
        var planned = stored.PlanProfileChange(details, guardianId: null, Today);

        created.ShouldFail(ErrorKind.BadRequest, expected);
        planned.ShouldFail(ErrorKind.BadRequest, expected);
    }

    [Fact]
    public void CreateIndependentWithABirthDateIsRefused()
    {
        var result = User.CreateIndependent(AdultDetails(birthDate: AdultDob), Now);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserBirthDateNotAllowedForAdult);
    }

    [Fact]
    public void CreateDependentMinorKeepsOnlyNamesGenderAndBirthDate()
    {
        var guardian = StoredAdult();

        var result = User.CreateDependent(guardian, ChildDetails(MinorDob), Today, Now);

        result.IsSuccess.Should().BeTrue();
        var child = result.Value;
        child.FirstName.Should().Be("Leo");
        child.LastName.Should().Be("Ruiz");
        child.Gender.Should().Be(Gender.Other);
        child.BirthDate.Should().Be(MinorDob);
        child.ParentId.Should().Be(guardian.Id);
        child.Email.Should().BeNull();
        child.Phone.Should().BeNull();
        child.SecondaryPhone.Should().BeNull();
        child.NationalId.Should().BeNull();
        child.PromotionalConsent.Should().BeFalse();
        child.PasswordHash.Should().BeNull();
        child.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
        child.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
        child.CreatedAt.Should().Be(Now);
        child.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("2008-07-05")]
    [InlineData("2026-07-04")]
    public void CreateDependentBornAfterTheLastEighteenthBirthdayIsAccepted(string birthDate)
    {
        var result = User.CreateDependent(
            StoredAdult(),
            ChildDetails(DateOrNull(birthDate)),
            Today,
            Now
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, ErrorCode.UserChildBirthDateRequired)]
    [InlineData("2008-07-04", ErrorCode.UserChildBirthDateNotMinor)]
    [InlineData("1986-07-04", ErrorCode.UserChildBirthDateNotMinor)]
    [InlineData("2026-07-05", ErrorCode.RequestValidationFailed)]
    public void CreateDependentWithoutAMinorBirthDateIsRefused(
        string? birthDate,
        ErrorCode expected
    )
    {
        var result = User.CreateDependent(
            StoredAdult(),
            ChildDetails(DateOrNull(birthDate)),
            Today,
            Now
        );

        result.ShouldFail(ErrorKind.BadRequest, expected);
    }

    [Fact]
    public void CreateDependentUnderAnotherDependentIsRefused()
    {
        var dependent = StoredChild(StoredAdult(), MinorDob);

        var result = User.CreateDependent(dependent, ChildDetails(MinorDob), Today, Now);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentIsMinor);
    }

    [Fact]
    public void PlanProfileChangeIndependentAccountChangesNothingUntilApplied()
    {
        var account = StoredAdult();
        var details = new PersonDetails(
            " Anabel ",
            " Soto ",
            Gender.PreferNotToSay,
            " NEW@test.com ",
            " 600000000 ",
            null,
            "y-1234-567-x",
            true
        );

        var planned = account.PlanProfileChange(details, guardianId: null, Today);

        planned.IsSuccess.Should().BeTrue();
        account.FirstName.Should().Be("Ana");
        account.Email.Should().Be("ana@test.com");
        account.UpdatedAt.Should().BeNull();

        account.ApplyProfileChange(planned.Value, Now);

        account.FirstName.Should().Be("Anabel");
        account.LastName.Should().Be("Soto");
        account.Gender.Should().Be(Gender.PreferNotToSay);
        account.Email.Should().Be("new@test.com");
        account.Phone.Should().Be("600000000");
        account.SecondaryPhone.Should().BeNull();
        account.NationalId.Should().Be("Y1234567X");
        account.PromotionalConsent.Should().BeTrue();
        account.BirthDate.Should().BeNull();
        account.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("  ANA@test.com ", " 600111222 ", "700111222", null, false)]
    [InlineData("new@test.com", "600111222", "700111222", "new@test.com", true)]
    [InlineData("ana@test.com", "600999999", "700111222", null, true)]
    [InlineData("ana@test.com", "600111222", "700999999", null, true)]
    [InlineData("ana@test.com", "600111222", "   ", null, true)]
    public void PlanProfileChangeIndependentAccountReportsWhatTheChangeReplaces(
        string email,
        string phone,
        string secondaryPhone,
        string? expectedNewEmail,
        bool expectedReplacesContact
    )
    {
        var account = StoredAdult();

        var change = account
            .PlanProfileChange(AdultDetails(email, phone, secondaryPhone), null, Today)
            .Value;

        change.Email.Should().Be(email.Trim().ToLowerInvariant());
        change.NewEmail.Should().Be(expectedNewEmail);
        change.ReplacesContact.Should().Be(expectedReplacesContact);
    }

    [Fact]
    public void PlanProfileChangeIndependentAccountWithoutContactGainingOneReplacesIt()
    {
        var account = StoredAdult();
        account.Email = null;
        account.Phone = null;
        account.SecondaryPhone = null;

        var change = account.PlanProfileChange(AdultDetails(), null, Today).Value;

        change.NewEmail.Should().Be("ana@test.com");
        change.ReplacesContact.Should().BeTrue();
    }

    [Fact]
    public void PlanProfileChangeIndependentAccountGivenABirthDateIsRefusedBeforeTheGuardian()
    {
        var account = StoredAdult();

        var result = account.PlanProfileChange(
            AdultDetails(birthDate: MinorDob),
            Guid.NewGuid(),
            Today
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserBirthDateNotAllowedForAdult);
    }

    [Fact]
    public void PlanProfileChangeIndependentAccountGivenAGuardianIsRefused()
    {
        var account = StoredAdult();

        var result = account.PlanProfileChange(AdultDetails(), Guid.NewGuid(), Today);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentNotAllowedForAdult);
    }

    [Fact]
    public void PlanProfileChangeDependentIgnoresContactAndIdentityDetails()
    {
        var guardian = StoredAdult();
        var child = StoredChild(guardian, MinorDob);

        var change = child
            .PlanProfileChange(ChildDetails(MinorDob.AddDays(1)), guardian.Id, Today)
            .Value;
        child.ApplyProfileChange(change, Now);

        change.Email.Should().BeNull();
        change.NewEmail.Should().BeNull();
        change.ReplacesContact.Should().BeFalse();
        child.BirthDate.Should().Be(MinorDob.AddDays(1));
        child.ParentId.Should().Be(guardian.Id);
        child.Email.Should().BeNull();
        child.Phone.Should().BeNull();
        child.SecondaryPhone.Should().BeNull();
        child.NationalId.Should().BeNull();
        child.PromotionalConsent.Should().BeFalse();
        child.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void PlanProfileChangeDependentGivenAnotherGuardianIsForbidden()
    {
        var child = StoredChild(StoredAdult(), MinorDob);

        var result = child.PlanProfileChange(ChildDetails(MinorDob), Guid.NewGuid(), Today);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserParentReassignmentForbidden);
    }

    [Theory]
    [InlineData("2016-07-04", "2016-07-04", null)]
    [InlineData("2016-07-04", "2010-01-01", null)]
    [InlineData("1986-07-04", "1986-07-04", null)]
    [InlineData("1986-07-04", "2016-07-04", null)]
    [InlineData("2016-07-04", null, ErrorCode.UserChildBirthDateRequired)]
    [InlineData("2016-07-04", "2008-07-04", ErrorCode.UserChildBirthDateNotMinor)]
    [InlineData("1986-07-04", "1986-07-05", ErrorCode.UserChildBirthDateNotMinor)]
    [InlineData("2016-07-04", "2026-07-05", ErrorCode.RequestValidationFailed)]
    public void PlanProfileChangeDependentBirthDateMustKeepItAMinorOnlyWhenItChanges(
        string stored,
        string? requested,
        ErrorCode? expected
    )
    {
        var guardian = StoredAdult();
        var child = StoredChild(guardian, DateOrNull(stored)!.Value);

        var result = child.PlanProfileChange(ChildDetails(DateOrNull(requested)), null, Today);

        if (expected is { } code)
        {
            result.ShouldFail(ErrorKind.BadRequest, code);
            return;
        }

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ApplyProfileChangePlannedForAnotherAccountThrows()
    {
        var change = StoredAdult().PlanProfileChange(AdultDetails(), null, Today).Value;
        var other = StoredAdult();

        var act = () => other.ApplyProfileChange(change, Now);

        act.Should().Throw<ArgumentException>();
        other.UpdatedAt.Should().BeNull();
    }
}
