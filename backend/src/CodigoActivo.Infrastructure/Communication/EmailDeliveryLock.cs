using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Lets one API instance deliver email at a time.
/// </summary>
public interface IEmailDeliveryLock : IAsyncDisposable
{
    /// <summary>
    /// Makes sure this instance holds the lock, taking it when it is free.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when this instance holds the lock, and
    /// <see langword="false"/> while another instance does.
    /// </returns>
    public Task<bool> HoldAsync(CancellationToken ct);
}

/// <summary>
/// Holds the PostgreSQL session lock that lets one API instance deliver email at a time, so the pace
/// of the SMTP provider and of every recipient, kept in that instance's memory, is the pace of the
/// whole application. The lock lives on a connection of its own: if the instance dies or the
/// connection drops, the database releases it and another instance takes over.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the context that holds the connection.</param>
public sealed class EmailDeliveryLock(IServiceScopeFactory scopes) : IEmailDeliveryLock, IDisposable
{
    private const long LockKey = 6_710_917_394_118_620_401;

    private AsyncServiceScope? scope;
    private CodigoActivoDbContext? holder;

    /// <summary>
    /// Makes sure this instance holds the lock: checks that the connection that holds it is still
    /// alive, and otherwise tries to take it on a new connection without waiting.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when this instance holds the lock, and
    /// <see langword="false"/> while another instance does.
    /// </returns>
    public async Task<bool> HoldAsync(CancellationToken ct)
    {
        if (holder is not null)
        {
            try
            {
                await holder.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"").SingleAsync(ct);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await DisposeAsync();
            }
        }

        var candidate = scopes.CreateAsyncScope();
        try
        {
            var db = candidate.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
            await db.Database.OpenConnectionAsync(ct);
            var acquired = await db
                .Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockKey}) AS \"Value\"")
                .SingleAsync(ct);
            if (!acquired)
            {
                await candidate.DisposeAsync();
                return false;
            }

            scope = candidate;
            holder = db;
            return true;
        }
        catch
        {
            await candidate.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Releases the lock and closes the connection that held it. The connection goes back to the
    /// pool, so the lock is released explicitly rather than left to the next reset.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (holder is { } db)
        {
            try
            {
                await db
                    .Database.SqlQuery<bool>($"SELECT pg_advisory_unlock({LockKey}) AS \"Value\"")
                    .SingleAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                await CloseAsync();
                return;
            }
        }

        await CloseAsync();
    }

    /// <summary>
    /// Closes the connection that held the lock when the container is disposed synchronously; the
    /// database releases the lock once the pool closes the connection or resets it for reuse.
    /// </summary>
    public void Dispose()
    {
        holder = null;
        if (scope is { } held)
        {
            scope = null;
            held.Dispose();
        }
    }

    private async ValueTask CloseAsync()
    {
        holder = null;
        if (scope is { } held)
        {
            scope = null;
            await held.DisposeAsync();
        }
    }
}
