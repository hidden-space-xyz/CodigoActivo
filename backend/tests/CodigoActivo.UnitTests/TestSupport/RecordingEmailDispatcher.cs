using CodigoActivo.Domain.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class RecordingEmailDispatcher : IEmailDispatcher
{
    private readonly List<EmailMessage> enqueued = [];

    public IReadOnlyList<EmailMessage> Enqueued => enqueued;

    public bool RejectAll { get; set; }

    public bool TryEnqueue(EmailMessage message)
    {
        if (RejectAll)
        {
            return false;
        }

        enqueued.Add(message);
        return true;
    }
}
