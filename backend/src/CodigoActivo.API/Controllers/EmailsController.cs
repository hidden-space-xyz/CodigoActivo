using System.ComponentModel.DataAnnotations;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Validation;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

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
    /// <param name="attachments">The attachments value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a send email, or an error response.</returns>
    [HttpPost("users/{userId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<SendEmailResultResponse>> SendToUserAsync(
        Guid userId,
        [FromForm]
        [Required]
        [MaxLength(SendEmailRequest.SubjectMaxLength)]
        [NotBlank]
            string subject,
        [FromForm] [Required] [MaxLength(SendEmailRequest.BodyMaxLength)] [NotBlank] string body,
        [FromForm] IEnumerable<IFormFile>? attachments,
        [FromServices] SendEmailToUserCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new SendEmailToUserCommand(
                    userId,
                    new SendEmailRequest(subject, body),
                    ToAttachments(attachments)
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Sends the to users message to its recipients.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="subject">The subject value.</param>
    /// <param name="body">The body value.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a send email, or an error response.</returns>
    [HttpPost("users")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<SendEmailResultResponse>> SendToUsersAsync(
        [FromQuery] UserListQuery query,
        [FromForm]
        [Required]
        [MaxLength(SendEmailRequest.SubjectMaxLength)]
        [NotBlank]
            string subject,
        [FromForm] [Required] [MaxLength(SendEmailRequest.BodyMaxLength)] [NotBlank] string body,
        [FromForm] IEnumerable<IFormFile>? attachments,
        [FromServices] SendEmailToUsersCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new SendEmailToUsersCommand(
                    query,
                    new SendEmailRequest(subject, body),
                    ToAttachments(attachments)
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Sends the to event attendees message to its recipients.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="subject">The subject value.</param>
    /// <param name="body">The body value.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a send email, or an error response.</returns>
    [HttpPost("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<SendEmailResultResponse>> SendToEventAttendeesAsync(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        [FromForm]
        [Required]
        [MaxLength(SendEmailRequest.SubjectMaxLength)]
        [NotBlank]
            string subject,
        [FromForm] [Required] [MaxLength(SendEmailRequest.BodyMaxLength)] [NotBlank] string body,
        [FromForm] IEnumerable<IFormFile>? attachments,
        [FromServices] SendEmailToEventAttendeesCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new SendEmailToEventAttendeesCommand(
                    eventId,
                    query,
                    new SendEmailRequest(subject, body),
                    ToAttachments(attachments)
                ),
                ct
            )
        );
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
