namespace CodigoActivo.IntegrationTests.Infrastructure;

internal static class TestUri
{
    internal static Uri Rel(string path)
    {
        return new Uri(path, UriKind.Relative);
    }
}
