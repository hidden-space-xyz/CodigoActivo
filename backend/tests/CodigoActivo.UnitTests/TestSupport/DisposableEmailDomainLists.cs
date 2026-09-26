using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public static class DisposableEmailDomainLists
{
    public static IEnumerable<string> Domains(int count = DisposableEmailDomainList.MinDomains)
    {
        return Enumerable.Range(0, count).Select(index => $"disposable{index}.test");
    }

    public static string Genuine(params string[] extraLines)
    {
        return string.Join('\n', Domains().Concat(extraLines));
    }
}
