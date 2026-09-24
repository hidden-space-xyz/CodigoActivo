using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Recipients a manual email reaches once the users matching its filters are known: only users
/// with an email address, each address once regardless of letter case. The send commands and the
/// audience queries share it so the preview always matches what is sent.
/// </summary>
/// <param name="Recipients">Distinct recipients that have an email address.</param>
/// <param name="Skipped">Matching users left out because they have no email address.</param>
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
        IQueryable<User> source,
        IQueryExecutor executor,
        CancellationToken ct
    )
    {
        var matched = await executor.ToListAsync(
            source.Select(ManualEmailDispatcher.ToRecipient),
            ct
        );
        var addressable = matched.Where(r => !string.IsNullOrWhiteSpace(r.Email)).ToList();
        var recipients = addressable
            .DistinctBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ManualEmailAudience(recipients, matched.Count - addressable.Count);
    }

    /// <summary>
    /// Summarizes the audience for the administrator composing the email.
    /// </summary>
    /// <returns>How many recipients there are and how many of them lack promotional consent.</returns>
    public EmailAudienceResponse ToResponse()
    {
        return new EmailAudienceResponse(
            Recipients.Count,
            Recipients.Count(r => !r.PromotionalConsent)
        );
    }
}
