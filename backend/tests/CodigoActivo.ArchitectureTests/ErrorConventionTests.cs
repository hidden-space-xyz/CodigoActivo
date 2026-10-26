using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed class ErrorConventionTests
{
    [Fact]
    public void ExceptionTypesAreOnlyTheReviewedTechnicalSignals()
    {
        var exceptions = ProductionAssemblies.AllTypes.Where(typeof(Exception).IsAssignableFrom);

        exceptions
            .Should()
            .BeEquivalentTo(
                [
                    typeof(ConcurrentUpdateException),
                    typeof(EmailRateLimitedException),
                    typeof(UniqueConstraintViolationException),
                ],
                "expected failures return a Result with a DomainErrorCode or an "
                    + "ApplicationErrorCode instead of throwing"
            );
    }
}
