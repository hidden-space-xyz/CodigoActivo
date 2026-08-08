using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Emails;

internal static class EmailTestData
{
    public static readonly DateOnly Birth = new(1990, 1, 1);

    public static User NewUser(string first, string? email, User? parent = null)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = first + " Apellido",
            Email = email,
            BirthDate = Birth,
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
        RecordingEmailSender emailSender,
        ManualEmailOptions options
    )
    {
        return new ManualEmailDispatcher(
            emailSender,
            options,
            new ApplicationOptions(),
            NullLogger<ManualEmailDispatcher>.Instance
        );
    }

    public static void HasUsers(this IUserRepository users, params User[] items)
    {
        users.Query().Returns(items.AsQueryable());
    }
}
