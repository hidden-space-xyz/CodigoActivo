using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

internal static class TestCancellation
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
}
