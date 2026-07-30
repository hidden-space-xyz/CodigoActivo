using System.ComponentModel.DataAnnotations;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Application.Validation;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/emails")]
public class EmailsController(IEmailService emails) : ApiControllerBase
{
    [HttpPost("users/{userId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<SendEmailResultResponse>> SendToUser(
        Guid userId,
        [FromForm]
        [Required]
        [MaxLength(SendEmailRequest.SubjectMaxLength)]
        [NotBlank]
            string subject,
        [FromForm] [Required] [MaxLength(SendEmailRequest.BodyMaxLength)] [NotBlank] string body,
        [FromForm] IEnumerable<IFormFile>? attachments,
        CancellationToken ct
    )
    {
        return ToOk(
            await emails.SendToUserAsync(
                userId,
                new SendEmailRequest(subject, body),
                ToAttachments(attachments),
                ct
            )
        );
    }

    [HttpPost("users")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<SendEmailResultResponse>> SendToUsers(
        [FromQuery] UserListQuery query,
        [FromForm]
        [Required]
        [MaxLength(SendEmailRequest.SubjectMaxLength)]
        [NotBlank]
            string subject,
        [FromForm] [Required] [MaxLength(SendEmailRequest.BodyMaxLength)] [NotBlank] string body,
        [FromForm] IEnumerable<IFormFile>? attachments,
        CancellationToken ct
    )
    {
        return ToOk(
            await emails.SendToUsersAsync(
                query,
                new SendEmailRequest(subject, body),
                ToAttachments(attachments),
                ct
            )
        );
    }

    [HttpPost("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<SendEmailResultResponse>> SendToEventAttendees(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        [FromForm]
        [Required]
        [MaxLength(SendEmailRequest.SubjectMaxLength)]
        [NotBlank]
            string subject,
        [FromForm] [Required] [MaxLength(SendEmailRequest.BodyMaxLength)] [NotBlank] string body,
        [FromForm] IEnumerable<IFormFile>? attachments,
        CancellationToken ct
    )
    {
        return ToOk(
            await emails.SendToEventAttendeesAsync(
                eventId,
                query,
                new SendEmailRequest(subject, body),
                ToAttachments(attachments),
                ct
            )
        );
    }

    private static List<EmailAttachmentUpload> ToAttachments(IEnumerable<IFormFile>? files)
    {
        return files is null
            ? []
            : files
                .Select(file => new EmailAttachmentUpload(
                    file.OpenReadStream(),
                    file.FileName,
                    file.ContentType,
                    file.Length
                ))
                .ToList();
    }
}
