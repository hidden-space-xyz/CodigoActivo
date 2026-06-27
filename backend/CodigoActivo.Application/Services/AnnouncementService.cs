using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class AnnouncementService(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IUnitOfWork uow
) : IAnnouncementService
{
    public async Task<IReadOnlyList<AnnouncementResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await announcements.GetAllAsync(ct);
        return items.OrderByDescending(a => a.CreatedAt).Select(a => a.ToResponse()).ToList();
    }

    public async Task<Result<AnnouncementResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var announcement = await announcements.FindAsync(a => a.Id == id, ct);
        var response = announcement?.ToResponse();

        return response is null ? Error.NotFound() : Result.Success(response);
    }

    public async Task<Result<AnnouncementResponse>> CreateAsync(
        CreateAnnouncementRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };
        await announcements.AddAsync(announcement, ct);
        await uow.SaveChangesAsync(ct);
        return announcement.ToResponse();
    }

    public async Task<Result<AnnouncementResponse>> UpdateAsync(
        Guid id,
        UpdateAnnouncementRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var announcement = await announcements.FindAsync(a => a.Id == id, ct);
        if (announcement is null)
        {
            return Error.NotFound();
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        announcement.Title = request.Title.Trim();
        announcement.Subtitle = request.Subtitle.Trim();
        announcement.Description = request.Description;
        announcement.ThumbnailId = request.ThumbnailId;
        announcement.UpdatedAt = DateTimeOffset.UtcNow;
        announcement.UpdatedBy = userId;

        announcements.Update(announcement);
        await uow.SaveChangesAsync(ct);
        return announcement.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await announcements.ExistsAsync(a => a.Id == id, ct))
        {
            return Error.NotFound();
        }

        await announcements.RemoveAsync(a => a.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<AnnouncementResponse>> SetFeaturedAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        if (!await announcements.ExistsAsync(a => a.Id == id, ct))
        {
            return Error.NotFound();
        }

        await announcements.SetFeaturedAsync(id, ct);

        var announcement = await announcements.FindAsync(a => a.Id == id, ct);
        return announcement!.ToResponse();
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
