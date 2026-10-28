using CodigoActivo.Application.Legal;
using CodigoActivo.Application.TermsDecisions;
using NSubstitute;

namespace CodigoActivo.UnitTests.TestSupport;

public static class ConsentWithdrawalNotifiers
{
    public static ConsentWithdrawalNotifier WithoutLegalContact()
    {
        return new ConsentWithdrawalNotifier(
            new FakeReadStore(),
            new FakeQueryExecutor(),
            new LegalEntityOptions(),
            new RecordingEmailSender(),
            Substitute.For<IConsentWithdrawalEmailComposer>()
        );
    }
}
