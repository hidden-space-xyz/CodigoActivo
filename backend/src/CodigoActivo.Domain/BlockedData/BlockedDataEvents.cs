using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// A consultation of the blocked data was opened.
/// </summary>
/// <param name="ConsultationId">Identifier of the consultation.</param>
public sealed record BlockedDataConsultationOpened(BlockedDataConsultationId ConsultationId)
    : IDomainEvent;
