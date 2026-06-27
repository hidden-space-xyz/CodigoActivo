using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class EventService(IEventRepository events, IFileRepository files, IUnitOfWork uow)
    : IEventService
{
    public async Task<IReadOnlyList<EventResponse>> ListAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    )
    {
        var list = await events.ListAsync(startDate, endDate, ct);
        return list.Select(e => e.ToResponse()).ToList();
    }

    public async Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ev = await events.GetWithThumbnailAsync(id, ct);
        var response = ev?.ToResponse();

        return response is null ? Error.NotFound() : Result.Success(response);
    }

    public async Task<Result<EventResponse>> CreateAsync(
        CreateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        var ev = new Event
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            EventStartsAt = request.EventStartsAt,
            EventEndsAt = request.EventEndsAt,
            SignupStartsAt = request.SignupStartsAt,
            SignupEndsAt = request.SignupEndsAt,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };

        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);

        return ev.ToResponse();
    }

    public async Task<Result<EventResponse>> UpdateAsync(
        Guid id,
        UpdateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var ev = await events.FindAsync(e => e.Id == id, ct);
        if (ev is null)
        {
            return Error.NotFound();
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        ev.Title = request.Title.Trim();
        ev.Subtitle = request.Subtitle.Trim();
        ev.Description = request.Description;
        ev.EventStartsAt = request.EventStartsAt;
        ev.EventEndsAt = request.EventEndsAt;
        ev.SignupStartsAt = request.SignupStartsAt;
        ev.SignupEndsAt = request.SignupEndsAt;
        ev.ThumbnailId = request.ThumbnailId;
        ev.UpdatedAt = DateTimeOffset.UtcNow;
        ev.UpdatedBy = userId;

        events.Update(ev);
        await uow.SaveChangesAsync(ct);

        return ev.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await events.ExistsAsync(e => e.Id == id, ct))
        {
            return Error.NotFound();
        }

        await events.RemoveAsync(e => e.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
        {
            return Error.Validation();
        }

        return Result.Success();
    }
}
