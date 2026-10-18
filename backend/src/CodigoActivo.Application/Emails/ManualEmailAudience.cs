using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Emails.Contracts;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Recipients a manual email reaches once the users matching its filters are known: only users
/// with an email address they confirmed, each address once regardless of letter case, and for a
/// news email only those with news consent. An account still pending verification is left out, since
/// anyone may have registered it with someone else's address. The send commands and the audience
/// queries share it so the preview always matches what is sent.
/// </summary>
/// <param name="Recipients">Distinct recipients that have a confirmed email address.</param>
/// <param name="Skipped">
/// Matching users left out because they have no email address or have not confirmed it.
/// </param>
public sealed record ManualEmailAudience(IReadOnlyList<Recipient> Recipients, int Skipped)
{
    /// <summary>
    /// Loads the recipients of the users in <paramref name="source"/>.
    /// </summary>
    /// <param name="source">Users already narrowed down by the email filters.</param>
    /// <param name="executor">Query executor used to materialize database results.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the audience of the email.</returns>
    public static async Task<ManualEmailAudience> LoadAsync(
        IQueryable<UserRow> source,
        IQueryExecutor executor,
        CancellationToken ct
    )
    {
        var matched = await executor.ToListAsync(
            source.Select(ManualEmailDispatcher.ToRecipient),
            ct
        );
        var addressable = matched
            .Where(r => r.EmailConfirmed && !string.IsNullOrWhiteSpace(r.Email))
            .ToList();
        var recipients = addressable
            .DistinctBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ManualEmailAudience(recipients, matched.Count - addressable.Count);
    }

    /// <summary>
    /// Selects the recipients an email written for <paramref name="purpose"/> reaches: a
    /// news email only reaches those who gave their news consent.
    /// </summary>
    /// <param name="purpose">What the email is written for.</param>
    /// <returns>The recipients the email reaches.</returns>
    public IReadOnlyList<Recipient> ReachedBy(ManualEmailPurpose purpose)
    {
        return purpose is ManualEmailPurpose.News
            ? [.. Recipients.Where(r => r.NewsConsent)]
            : Recipients;
    }

    /// <summary>
    /// Summarizes the audience for the administrator composing the email.
    /// </summary>
    /// <returns>
    /// How many recipients a notice reaches and how many of them a news email
    /// leaves out for lack of news consent.
    /// </returns>
    public EmailAudienceResponse ToResponse()
    {
        return new EmailAudienceResponse(Recipients.Count, Recipients.Count(r => !r.NewsConsent));
    }
}
