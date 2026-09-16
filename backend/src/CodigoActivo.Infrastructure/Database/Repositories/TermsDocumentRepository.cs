using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves terms document data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class TermsDocumentRepository(CodigoActivoDbContext context)
    : Repository<TermsDocument>(context),
        ITermsDocumentRepository;
