using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodigoActivo.UnitTests.Application.Emails;

internal static class EmailTestData
{
    public static readonly DateOnly Birth = new(1990, 1, 1);

    public static UserRow NewUserRow(
        string first,
        string? email,
        UserRow? parent = null,
        bool promotionalConsent = false
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = first + " Apellido",
            Email = email,
            BirthDate = parent is null ? null : Birth,
            PromotionalConsent = promotionalConsent,
            Gender = Gender.Other,
            ParentId = parent?.Id,
            Parent = parent,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
        };
    }

    public static SendEmailRequest Request(string subject = "Asunto", string body = "Cuerpo")
    {
        return new(subject, body);
    }

    public static ManualEmailDispatcher NewDispatcher(
        RecordingEmailOutbox outbox,
        ManualEmailOptions options
    )
    {
        return new ManualEmailDispatcher(
            outbox,
            options,
            new ManualEmailComposer(new ApplicationOptions()),
            NullLogger<ManualEmailDispatcher>.Instance
        );
    }
}
