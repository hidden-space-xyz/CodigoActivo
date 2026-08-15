using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

public sealed record CreateTermsDocumentCommand(CreateTermsDocumentRequest Request)
    : ICommand<Result<TermsDocumentResponse>>;

public sealed class CreateTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateTermsDocumentCommand, Result<TermsDocumentResponse>>
{
    public async Task<Result<TermsDocumentResponse>> HandleAsync(
        CreateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var name = request.Name.Trim();
        if (await termsDocuments.ExistsAsync(x => x.Name == name, ct))
        {
            return Error.Conflict(ErrorCode.TermsDocumentNameAlreadyExists);
        }

        var termsDocument = new TermsDocument { Name = name, Description = request.Description };
        await termsDocuments.AddAsync(termsDocument, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.TermsDocuments);
        return termsDocument.ToResponse();
    }
}
