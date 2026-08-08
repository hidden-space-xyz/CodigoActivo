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

[ApiController]
[Route("api/emails")]
public class EmailsController : ApiControllerBase
{
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
