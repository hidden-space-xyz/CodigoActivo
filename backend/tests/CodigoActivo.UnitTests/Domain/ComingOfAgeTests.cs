using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class ComingOfAgeTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private static User Dependent(DateOnly? birthDate, DateTimeOffset? noticeSentAt = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Leo",
                LastName = "Ruiz",
                BirthDate = birthDate,
                ParentId = Guid.NewGuid(),
                Status = UserStatus.Dependent,
                ComingOfAgeNoticeSentAt = noticeSentAt,
            }
        );
    }

    private static User Adult()
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "Ruiz",
                Status = UserStatus.Active,
            }
        );
    }

    [Fact]
    public void ComingOfAgeNoticeCutoffIsTheBirthDateOfWhoComesOfAgeInThirtyDays()
    {
        User.ComingOfAgeNoticeCutoff(Today).Should().Be(new DateOnly(2008, 8, 3));
    }

    [Theory]
    [InlineData(2008, 8, 3, true)]
    [InlineData(2008, 7, 5, true)]
    [InlineData(2008, 8, 4, false)]
    [InlineData(2008, 7, 4, false)]
    [InlineData(2016, 1, 1, false)]
    public void IsComingOfAgeNoticeDueOnlyForAMinorWhoComesOfAgeWithinThirtyDays(
        int year,
        int month,
        int day,
        bool expected
    )
    {
        var dependent = Dependent(new DateOnly(year, month, day));

        dependent.IsComingOfAgeNoticeDue(Today).Should().Be(expected);
    }

    [Fact]
    public void IsComingOfAgeNoticeDueOnceRecordedIsNotDueAgain()
    {
        var dependent = Dependent(new DateOnly(2008, 7, 20));

        dependent.RecordComingOfAgeNotice(Now);

        dependent.ComingOfAgeNoticeSentAt.Should().Be(Now);
        dependent.IsComingOfAgeNoticeDue(Today).Should().BeFalse();
    }

    [Fact]
    public void IsComingOfAgeNoticeDueNeverForAnIndependentAccountOrADependentWithoutBirthDate()
    {
        Adult().IsComingOfAgeNoticeDue(Today).Should().BeFalse();
        Dependent(birthDate: null).IsComingOfAgeNoticeDue(Today).Should().BeFalse();
    }

    [Theory]
    [InlineData(2008, 7, 4, true)]
    [InlineData(1990, 1, 1, true)]
    [InlineData(2008, 7, 5, false)]
    public void HasComeOfAgeFromTheEighteenthBirthday(int year, int month, int day, bool expected)
    {
        Dependent(new DateOnly(year, month, day)).HasComeOfAge(Today).Should().Be(expected);
    }

    [Fact]
    public void HasComeOfAgeNeverForAnIndependentAccountOrADependentWithoutBirthDate()
    {
        Adult().HasComeOfAge(Today).Should().BeFalse();
        Dependent(birthDate: null).HasComeOfAge(Today).Should().BeFalse();
    }

    [Fact]
    public void ApplyProfileChangeAnotherBirthDateMakesTheNoticeDueAgain()
    {
        var dependent = Dependent(new DateOnly(2008, 7, 20), noticeSentAt: Now.AddDays(-5));
        var change = dependent
            .PlanProfileChange(
                new PersonDetails(
                    "Leo",
                    "Ruiz",
                    Gender.Male,
                    null,
                    null,
                    null,
                    null,
                    false,
                    new DateOnly(2008, 7, 25)
                ),
                null,
                Today
            )
            .Value;

        dependent.ApplyProfileChange(change, Now);

        dependent.ComingOfAgeNoticeSentAt.Should().BeNull();
        dependent.IsComingOfAgeNoticeDue(Today).Should().BeTrue();
    }

    [Fact]
    public void ApplyProfileChangeSameBirthDateKeepsTheNotice()
    {
        var sentAt = Now.AddDays(-5);
        var dependent = Dependent(new DateOnly(2008, 7, 20), noticeSentAt: sentAt);
        var change = dependent
            .PlanProfileChange(
                new PersonDetails(
                    "Leonardo",
                    "Ruiz",
                    Gender.Male,
                    null,
                    null,
                    null,
                    null,
                    false,
                    new DateOnly(2008, 7, 20)
                ),
                null,
                Today
            )
            .Value;

        dependent.ApplyProfileChange(change, Now);

        dependent.ComingOfAgeNoticeSentAt.Should().Be(sentAt);
    }
}
