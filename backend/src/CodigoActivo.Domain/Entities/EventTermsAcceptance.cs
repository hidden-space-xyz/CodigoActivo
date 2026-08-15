namespace CodigoActivo.Domain.Entities;

public class EventTermsAcceptance
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TermsDocumentId { get; set; }

    public DateTimeOffset AcceptedAt { get; set; }
}
