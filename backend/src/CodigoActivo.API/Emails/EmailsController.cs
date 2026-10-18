using System.ComponentModel.DataAnnotations;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Emails.Contracts;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Application.Emails.Queries;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Emails;

/// <summary>
/// Exposes HTTP endpoints for querying and managing emails.
/// </summary>
[ApiController]
[Route("api/emails")]
public class EmailsController : ApiControllerBase
{
    /// <summary>
    /// Sends the to user message to its recipients.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="subject">The subject value.</param>
    /// <param name="body">The body value.</param>
    /// <param name="purpose">What the email is written for, which decides who it reaches.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a send email, or an error response.</returns>
    [HttpPost("users/{userId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    [EnableRateLimiting(SecurityPolicies.SingleRecipientEmail)]
    public async Task<ActionResult<SendEmailResultResponse>> SendToUserAsync(
        Guid userId,
        [FromForm] [Required] [MaxLength(ManualEmailText.SubjectMaxLength)] string subject,
        [FromForm] [Required] [MaxLength(ManualEmailText.BodyMaxLength)] string body,
        [FromForm]
        [BindRequired]
        [EnumDataType(typeof(ManualEmailPurpose))]
            ManualEmailPurpose purpose,
        [FromForm] IEnumerable<IFormFile>? attachments,
        [FromServices] ICommandHandler<SendEmailToUserCommand, Result<EmailDispatch>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new SendEmailToUserCommand(
                    UserId.From(userId),
                    new ManualEmailText(subject, body),
                    purpose,
                    ToAttachments(attachments)
                ),
                ct
            ),
            ToResponse
        );
    }

    /// <summary>
    /// Sends the to users message to its recipients.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="subject">The subject value.</param>
    /// <param name="body">The body value.</param>
    /// <param name="purpose">What the email is written for, which decides who it reaches.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a send email, or an error response.</returns>
    [HttpPost("users")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    [EnableRateLimiting(SecurityPolicies.BulkEmail)]
    public async Task<ActionResult<SendEmailResultResponse>> SendToUsersAsync(
        [FromQuery] UserListQuery query,
        [FromForm] [Required] [MaxLength(ManualEmailText.SubjectMaxLength)] string subject,
        [FromForm] [Required] [MaxLength(ManualEmailText.BodyMaxLength)] string body,
        [FromForm]
        [BindRequired]
        [EnumDataType(typeof(ManualEmailPurpose))]
            ManualEmailPurpose purpose,
        [FromForm] IEnumerable<IFormFile>? attachments,
        [FromServices] ICommandHandler<SendEmailToUsersCommand, Result<EmailDispatch>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new SendEmailToUsersCommand(
                    query,
                    new ManualEmailText(subject, body),
                    purpose,
                    ToAttachments(attachments)
                ),
                ct
            ),
            ToResponse
        );
    }

    /// <summary>
    /// Sends the to event attendees message to its recipients.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="subject">The subject value.</param>
    /// <param name="body">The body value.</param>
    /// <param name="purpose">What the email is written for, which decides who it reaches.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a send email, or an error response.</returns>
    [HttpPost("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    [EnableRateLimiting(SecurityPolicies.BulkEmail)]
    public async Task<ActionResult<SendEmailResultResponse>> SendToEventAttendeesAsync(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        [FromForm] [Required] [MaxLength(ManualEmailText.SubjectMaxLength)] string subject,
        [FromForm] [Required] [MaxLength(ManualEmailText.BodyMaxLength)] string body,
        [FromForm]
        [BindRequired]
        [EnumDataType(typeof(ManualEmailPurpose))]
            ManualEmailPurpose purpose,
        [FromForm] IEnumerable<IFormFile>? attachments,
        [FromServices]
            ICommandHandler<SendEmailToEventAttendeesCommand, Result<EmailDispatch>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new SendEmailToEventAttendeesCommand(
                    EventId.From(eventId),
                    query,
                    new ManualEmailText(subject, body),
                    purpose,
                    ToAttachments(attachments)
                ),
                ct
            ),
            ToResponse
        );
    }

    /// <summary>
    /// Previews who an email to users would reach with the same filters as the send endpoint.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the audience summary, or an error response.</returns>
    [HttpGet("users/audience")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EmailAudienceResponse>> GetUsersAudienceAsync(
        [FromQuery] UserListQuery query,
        [FromServices]
            IQueryHandler<GetUsersEmailAudienceQuery, Result<EmailAudienceResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetUsersEmailAudienceQuery(query), ct));
    }

    /// <summary>
    /// Previews who an email to event attendees would reach with the same filters as the send
    /// endpoint.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the audience summary, or an error response.</returns>
    [HttpGet("events/{eventId:guid}/attendees/audience")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EmailAudienceResponse>> GetEventAttendeesAudienceAsync(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        [FromServices]
            IQueryHandler<
            GetEventAttendeesEmailAudienceQuery,
            Result<EmailAudienceResponse>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new GetEventAttendeesEmailAudienceQuery(EventId.From(eventId), query),
                ct
            )
        );
    }

    /// <summary>
    /// Withdraws the promotional consent of the account the unsubscribe link of a promotional
    /// email names. It needs no session, so the link works wherever the email is read, and it
    /// answers 204 again when the link is followed once more.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("unsubscribe")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> UnsubscribeAsync(
        [FromBody] UnsubscribeRequest request,
        [FromServices] ICommandHandler<UnsubscribeCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
    }

    /// <summary>
    /// Withdraws the promotional consent of the account named by the address in the
    /// <c>List-Unsubscribe</c> header of a promotional email, which the mail client of the holder
    /// posts to when they press its unsubscribe button (RFC 8058). The mail client sends no
    /// cookie and no antiforgery token, so neither is asked for: the token is the only proof, as
    /// in <see cref="UnsubscribeAsync"/>. It answers without redirecting, and 204 again when the
    /// button is pressed once more.
    /// </summary>
    /// <param name="token">Token taken from the address.</param>
    /// <param name="listUnsubscribe">
    /// The <c>List-Unsubscribe=One-Click</c> pair of the <c>List-Unsubscribe-Post</c> header,
    /// which the mail client sends as the form.
    /// </param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("unsubscribe/one-click")]
    [AllowAnonymous]
    [RequireAntiforgeryToken(required: false)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> UnsubscribeWithOneClickAsync(
        [FromQuery] [Required] [MaxLength(UnsubscribeCommand.TokenMaxLength)] string token,
        [FromForm(Name = "List-Unsubscribe")]
        [Required]
        [AllowedValues("One-Click")]
            string listUnsubscribe,
        [FromServices] ICommandHandler<UnsubscribeCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new UnsubscribeCommand(token, PromotionalConsentSource.OneClickUnsubscribe),
                ct
            )
        );
    }

    private static SendEmailResultResponse ToResponse(EmailDispatch dispatch)
    {
        return new SendEmailResultResponse(dispatch.Queued, dispatch.Skipped);
    }

    private static List<EmailAttachmentUpload> ToAttachments(IEnumerable<IFormFile>? files)
    {
        return files is null
            ? []
            :
            [
                .. files.Select(file => new EmailAttachmentUpload(
                    file.OpenReadStream(),
                    file.FileName,
                    file.ContentType,
                    file.Length
                )),
            ];
    }
}
